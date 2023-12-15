using NBitcoin;
using NLDK;

namespace nldksample.LDK;

public class CurrentWalletService
{
    private readonly Network _network;
    private string? _currentWallet;
    private Wallet _wallet;

    public CurrentWalletService(Network network)
    {
        _network = network;
    }
    
    public void SetWallet(Wallet wallet)
    {
        if (_currentWallet is not null)
        {
            throw new InvalidOperationException("wallet is already selected");
        }
        _currentWallet = wallet.Id;
        _wallet = wallet;
        Seed = new Mnemonic(_wallet.Mnemonic).DeriveExtKey().Derive(new KeyPath(_wallet.DerivationPath.Replace("/*", ""))).PrivateKey.ToBytes();
        WalletSelected.SetResult();
        
    }

    public byte[] Seed { get; private set; }

    public string CurrentWallet
    {
        get
        {
            if(_currentWallet is null)
                throw new InvalidOperationException("No wallet selected");
            return _currentWallet;
        }
    }
    
    public TaskCompletionSource WalletSelected { get; } = new();
}