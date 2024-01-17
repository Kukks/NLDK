using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration.Memory;
using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using nldksample.LDK;

namespace NLDK.Tests;

public class UnitTest1
{
    public UnitTest1()
    {
    }

    [Fact]
    public async Task Test1()
    {
        await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder =>
            {
                builder.ConfigureAppConfiguration(configurationBuilder =>
                {
                    configurationBuilder.Add(
                        new MemoryConfigurationSource
                        {
                            InitialData = new Dictionary<string, string?>(StringComparer.InvariantCultureIgnoreCase)
                            {
                                {"ConnectionString", $"Data Source={Guid.NewGuid()}.db"},
                                {"NBXplorerConnection","http://localhost:24446"}
                            }
                        });
                });
            });


        var explorerClient = factory.Services.GetRequiredService<ExplorerClient>();
        var walletService = factory.Services.GetRequiredService<WalletService>();
        var nodeManager = factory.Services.GetRequiredService<LDKNodeManager>();

        var wallet1 = await CreateWallet(factory.Services);
        var wallet2 = await CreateWallet(factory.Services);

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
            await Task.Delay(100);
        }

        var wallet1PeerHandler = wallet1Node.ServiceProvider.GetRequiredService<LDKPeerHandler>();
        var wallet2PeerHandler = wallet2Node.ServiceProvider.GetRequiredService<LDKPeerHandler>();
        Task? wallet1Peer = null;
        
        while(wallet1Peer is null)
        {
            wallet1Peer = await wallet1PeerHandler.ConnectAsync(wallet2Node.NodeInfo);
        }
        
    }


    private async Task<string> CreateWallet(IServiceProvider serviceProvider)
    {
        var explorerClient = serviceProvider.GetRequiredService<ExplorerClient>();
        var walletService = serviceProvider.GetRequiredService<WalletService>();

        var genWallet = await explorerClient.GenerateWalletAsync();
        var hash = await explorerClient.RPCClient.GetBestBlockHashAsync();
        var ts = new DerivationSchemeTrackedSource(genWallet.DerivationScheme);
        var idx = genWallet.GetMnemonic().DeriveExtKey().GetPublicKey().GetHDFingerPrint().ToString();
        var wts = new WalletTrackedSource(idx);
        await explorerClient.TrackAsync(wts);
        await explorerClient.AddParentWallet(ts, wts);
        return await walletService.Create(genWallet.GetMnemonic(), null, genWallet.AccountKeyPath.KeyPath + "/*",
            hash.ToString(), new[] {ts.ToString(), wts.ToString()});
    }
}