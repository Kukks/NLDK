using AsyncKeyedLock;
using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using NLDK;
using org.ldk.structs;
using Wallet = NLDK.Wallet;

namespace nldksample.LDK;

public class CurrentWalletService
{
    private readonly NBXplorerNetwork _network;
    private readonly WalletService _walletService;

    private Wallet? _wallet;
    public CurrentWalletService(NBXplorerNetwork network, WalletService walletService)
    {
        _network = network;
        _walletService = walletService;
    }

    public void SetWallet(Wallet wallet)
    {
        if (_wallet is not null)
        {
            throw new InvalidOperationException("wallet is already selected");
        }
        _wallet = wallet;
        Seed = new Mnemonic(_wallet.Mnemonic).DeriveExtKey().Derive(new KeyPath(_wallet.DerivationPath.Replace("/*", ""))).PrivateKey.ToBytes();

        foreach (var alias in wallet.AliasWalletName)
        {
            if (!TrackedSource.TryParse(alias, out var ts, _network) || ts is not GroupTrackedSource gts) continue;
            GroupTrackedSource = gts;
            break;
        }
        WalletSelected.SetResult();

    }
    

    public byte[] Seed { get; private set; }

    public string CurrentWallet
    {
        get
        {
            if (_wallet is null)
                throw new InvalidOperationException("No wallet selected");
            return _wallet.Id;
        }
    }

    public TaskCompletionSource WalletSelected { get; } = new();

    private readonly TaskCompletionSource<ChannelMonitor[]?> icm = new();
    public GroupTrackedSource GroupTrackedSource { get; private set; } 

    public async Task<ChannelMonitor[]> GetInitialChannelMonitors()
    {
        return await icm.Task;
    }
    private async Task<ChannelMonitor[]> GetInitialChannelMonitors(EntropySource entropySource, SignerProvider signerProvider)
    {
        await WalletSelected.Task;
        var data = _wallet.Channels?.Select(c => c.Data)?.ToArray() ?? Array.Empty<byte[]>();
        var channels = ChannelManagerHelper.GetInitialMonitors(data, entropySource, signerProvider);
        icm.SetResult(channels);
        return channels;
    }

    public async Task<(byte[] serializedChannelManager, ChannelMonitor[] channelMonitors)?> GetSerializedChannelManager(EntropySource entropySource, SignerProvider signerProvider)
    {
        await WalletSelected.Task;
        var data= await _walletService.GetArbitraryData<byte[]>("ChannelManager", CurrentWallet);
        if (data is null)
        {
            icm.SetResult(Array.Empty<ChannelMonitor>());
            return null;
        }

        var channels = await GetInitialChannelMonitors(entropySource, signerProvider);
        return (data, channels);
    }
}