﻿
using AsyncKeyedLock;
using BTCPayServer.Lightning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using NLDK;
using nldksample.LDK;
using org.ldk.enums;
using org.ldk.structs;
using Network = NBitcoin.Network;
using NodeInfo = BTCPayServer.Lightning.NodeInfo;
using UInt128 = org.ldk.util.UInt128;


var builder = WebApplication.CreateBuilder(args);


var nbxNetworkProvider = new NBXplorerNetworkProvider(ChainName.Regtest);

builder.Configuration.AddEnvironmentVariables("NLDK_");
builder.Configuration.AddJsonFile("appsettings.Development.json", false);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    {"CONNECTIONSTRING", $"Data Source=wallet{Convert.ToHexString(RandomUtils.GetBytes(4))}.db"}
});
builder.Services
    .Configure<NLDKOptions>(builder.Configuration)
    .AddHostedService<MigratonHostedService>()
    .AddHostedService<NBXListener>(provider => provider.GetRequiredService<NBXListener>())
    .AddSingleton(new AsyncKeyedLocker<string>(o =>
    {
        o.PoolSize = 20;
        o.PoolInitialFill = 1;
    }))
    .AddSingleton<WalletService>()
    .AddLDK()
    .AddSingleton<NBXListener>()
    .AddSingleton<Network>(provider => provider.GetRequiredService<NBXplorerNetwork>().NBitcoinNetwork)
    .AddSingleton<NBXplorerNetwork>(provider => provider.GetRequiredService<ExplorerClient>().Network)
    .AddSingleton<ExplorerClient>(sp => 
        new ExplorerClient(nbxNetworkProvider.GetFromCryptoCode("BTC"), new Uri(sp.GetRequiredService<IOptions<NLDKOptions>>().Value.NBXplorerConnection)))
    .AddDbContextFactory<WalletContext>((provider, optionsBuilder) => optionsBuilder.UseSqlite(provider.GetRequiredService<IOptions<NLDKOptions>>().Value.ConnectionString));

var app = builder.Build();
_ = app.RunAsync();



async Task<string> CreateWallet(IServiceProvider serviceProvider)
{
    var explorerClient = serviceProvider.GetRequiredService<ExplorerClient>();
    var walletService = serviceProvider.GetRequiredService<WalletService>();

    var genWallet = await explorerClient.GenerateWalletAsync();
    var hash = await explorerClient.RPCClient.GetBestBlockHashAsync();
    var ts = new DerivationSchemeTrackedSource(genWallet.DerivationScheme);
    var idx = await explorerClient.CreateGroupAsync();// genWallet.GetMnemonic().DeriveExtKey().GetPublicKey().GetHDFingerPrint().ToString();
    var wts = new GroupTrackedSource(idx.GroupId);
    
    await explorerClient.AddGroupChildrenAsync(wts.GroupId, new[]
    {
        new GroupChild()
        {
            TrackedSource = ts.ToString(),
            CryptoCode = "BTC"
        }
    });
    return await walletService.Create(genWallet.GetMnemonic(), null, genWallet.AccountKeyPath.KeyPath + "/*",
        hash.ToString(), new[] {ts.ToString(), wts.ToString()});
}
var explorerClient = app.Services.GetRequiredService<ExplorerClient>();
var walletService = app.Services.GetRequiredService<WalletService>();
var nodeManager = app.Services.GetRequiredService<LDKNodeManager>();

var wallet1 = await CreateWallet(app.Services);
var wallet2 = await CreateWallet(app.Services);

var wallet1Node = await nodeManager.GetLDKNodeForWallet(wallet1);
var wallet2Node = await nodeManager.GetLDKNodeForWallet(wallet2);

var wallet1Script = await walletService.DeriveScript(wallet1);
var wallet2Script = await walletService.DeriveScript(wallet1);
await explorerClient.RPCClient.GenerateAsync(2);
await explorerClient.RPCClient.SendToAddressAsync(wallet1Script.ToScript(), Money.Coins(1));
await explorerClient.RPCClient.SendToAddressAsync(wallet2Script.ToScript(), Money.Coins(2));
await explorerClient.RPCClient.GenerateAsync(1);

while (wallet1Node.NodeInfo is null || wallet2Node.NodeInfo is null)
{
    Console.WriteLine($"Waiting until both nodes have ports assigned. " +
                      $"Currently node1: {wallet1Node.NodeInfo?.ToString()??wallet1Node.NodeId.ToString()}" + 
                      $"Currently node2: {wallet2Node.NodeInfo?.ToString()??wallet2Node.NodeId.ToString()}");
    await Task.Delay(100);
}

var wallet1PeerHandler = wallet1Node.ServiceProvider.GetRequiredService<LDKPeerHandler>();
var wallet2PeerHandler = wallet2Node.ServiceProvider.GetRequiredService<LDKPeerHandler>();

var wallet1PeerChangedTcs = new TaskCompletionSource<List<NodeInfo>>();
var wallet2PeerChangedTcs = new TaskCompletionSource<List<NodeInfo>>();

wallet1PeerHandler.OnPeersChange += (sender, args) => wallet1PeerChangedTcs.SetResult(args.PeerNodeIds);
wallet2PeerHandler.OnPeersChange += (sender, args) => wallet2PeerChangedTcs.SetResult(args.PeerNodeIds);
LDKTcpDescriptor? wallet1Peer = null;


while (wallet1Peer is null )
{
    Console.WriteLine("Attempting to connect node 1 to 2");
    wallet1Peer = await wallet1PeerHandler.ConnectAsync(wallet2Node.NodeInfo);
}

while(wallet1PeerHandler.ActiveDescriptors.Length == 0)
{
    Console.WriteLine("Waiting until node 1 has a peer");
    await Task.Delay(1000);
}

while(wallet2PeerHandler.ActiveDescriptors.Length == 0)
{
    Console.WriteLine("Waiting until node 2 has a peer");
    await Task.Delay(1000);
}

while(!wallet1PeerChangedTcs.Task.IsCompleted || !wallet2PeerChangedTcs.Task.IsCompleted)
{
    Console.WriteLine("Waiting until both nodes have noticed the peer within LDK");
    await Task.Delay(1000);
    await wallet1PeerHandler.GetPeerNodeIds();
    await wallet2PeerHandler.GetPeerNodeIds();
}




var wallet1ChannelManager= wallet1Node.ServiceProvider.GetRequiredService<ChannelManager>();
var wallet2ChannelManager= wallet2Node.ServiceProvider.GetRequiredService<ChannelManager>();
var wallet1UserConfig= wallet1Node.ServiceProvider.GetRequiredService<UserConfig>();
var userChannelId = new UInt128(RandomUtils.GetBytes(16));
Console.WriteLine($"Attempting to open a channel from node 1 to 2 of 0.01BTC with a user channel id {Convert.ToHexString(userChannelId.getLEBytes())}");
var channelResult = wallet1ChannelManager.create_channel(
    wallet2Node.NodeId.ToBytes(), 
    Money.Coins(0.01m).Satoshi, 
    0,
    userChannelId, 
    Option_ThirtyTwoBytesZ.none(), 
    wallet1UserConfig);

while(wallet1ChannelManager.list_channels().Length == 0 || wallet2ChannelManager.list_channels().Length == 0)
{
    Console.WriteLine("Waiting until ldk notices channel");
    await Task.Delay(100);
}

var channels = wallet1ChannelManager.list_channels();
var channels2 = wallet2ChannelManager.list_channels();
var confs = 0;
while (!channels[0].get_is_channel_ready() && !channels2[0].get_is_channel_ready())
{
    await Task.Delay(1000);
    
    channels = wallet1ChannelManager.list_channels();
    channels2 = wallet2ChannelManager.list_channels();
    
    var channel = channels[0];
    
    await explorerClient.RPCClient.GenerateAsync(1);
    confs += 1;
    Console.WriteLine($"Waiting until channel is ready. " +
                      $"node1 ready={channel.get_is_channel_ready()}, node2={channels2[0].get_is_channel_ready()} " + Environment.NewLine +
                      $"node1 usable={channel.get_is_usable()}, node2={channels2[0].get_is_usable()}"+ Environment.NewLine +
                      $"node1 confs={channel.get_confirmations().Get()}/{channel.get_confirmations_required().Get()}, node2={channels2[0].get_confirmations().Get()}/{channels2[0].get_confirmations_required().Get()}");

}

var node2PaymentManager = wallet2Node.ServiceProvider.GetRequiredService<PaymentsManager>();
var node1PaymentManager = wallet1Node.ServiceProvider.GetRequiredService<PaymentsManager>();

var node2PaymentRequest = await node2PaymentManager.RequestPayment(LightMoney.Satoshis(1000), TimeSpan.FromMinutes(10), "test payment");
await  node1PaymentManager.PayInvoice(node2PaymentRequest);




while (true)
{
    await Task.Delay(1000);
}



