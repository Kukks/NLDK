using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using NLDK;
using org.ldk.structs;
using Script = NBitcoin.Script;

namespace nldksample.LDK;

public class LDKFilter : FilterInterface
{
    private readonly string _walletId;
    private readonly CurrentWalletService _currentWalletService;
    private readonly WalletService _walletService;
    private readonly ExplorerClient _explorerClient;
    private readonly Network _network;
    private readonly GroupTrackedSource _ts;

    public LDKFilter(CurrentWalletService currentWalletService, WalletService walletService, ExplorerClient explorerClient, Network network)
    {
        _walletId = currentWalletService.CurrentWallet;
        _currentWalletService = currentWalletService;
        _walletService = walletService;
        _explorerClient = explorerClient;
        _network = network;
        _ts = new GroupTrackedSource(currentWalletService.CurrentWallet);
    }

    public void register_tx(byte[] txid, byte[] script_pubkey)
    {
        var script = Script.FromBytesUnsafe(script_pubkey);
        _ = Track(script);
    }

    public void register_output(WatchedOutput output)
    {
        var script = Script.FromBytesUnsafe(output.get_script_pubkey());
        _ = Track(script);
    }

    private async Task Track(Script script)
    {
        var address = script.GetDestinationAddress(_network);
        await _explorerClient.AddGroupAddressAsync("BTC",_ts.GroupId, new []{address.ToString()});
        await _walletService.TrackScript(_walletId, script);
    }
}