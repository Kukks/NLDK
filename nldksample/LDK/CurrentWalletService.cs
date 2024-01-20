using AsyncKeyedLock;
using NBitcoin;
using NBXplorer.Models;
using NLDK;
using org.ldk.structs;
using Wallet = NLDK.Wallet;

namespace nldksample.LDK;

public class CurrentWalletService
{
    private readonly WalletService _walletService;
    private Wallet? _wallet;
    // private Dictionary<string, byte[]> _data;
    public CurrentWalletService(WalletService walletService)
    {
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
        // _walletService.GetArbitraryData(_wallet.Id).ContinueWith(task => 
        // {
        //     _data = task.Result.ToDictionary(kv => kv.Key.Replace(_wallet.Id, ""), kv => kv.Value);
        //     
        //     
        // });
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

    // public bool IsThisWallet(TrackedSource trackedSource)
    // {
    //     if(trackedSource is WalletTrackedSource wts)
    //         return wts.WalletId == CurrentWallet;
    //     return _wallet.AliasWalletName.Contains(trackedSource.ToString());
    // }

    private ChannelMonitor[]? _channels = null;
    private readonly AsyncNonKeyedLocker _ss = new(1);
    public ChannelMonitor[] GetInitialChannelMonitors(EntropySource entropySource, SignerProvider signerProvider)
    {
        using (_ss.Lock())
        {
            if (_channels is null)
            {
                var data = _wallet.Channels?.Select(c => c.Data)?.ToArray() ?? Array.Empty<byte[]>();
                _channels = ChannelManagerHelper.GetInitialMonitors(data, entropySource, signerProvider);
            }
            return _channels;
        }
    }
}