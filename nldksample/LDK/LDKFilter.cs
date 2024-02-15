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
    private readonly WalletService _walletService;
    private readonly ExplorerClient _explorerClient;
    private readonly Network _network;
    private readonly GroupTrackedSource _groupTrackedSource;

    public LDKFilter(CurrentWalletService currentWalletService, WalletService walletService, ExplorerClient explorerClient, Network network, GroupTrackedSource groupTrackedSource)
    {
        _walletId = currentWalletService.CurrentWallet;
        _walletService = walletService;
        _explorerClient = explorerClient;
        _network = network;
        _groupTrackedSource = groupTrackedSource;
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
        await _explorerClient.AddGroupAddressAsync("BTC",_groupTrackedSource.GroupId, new []{address.ToString()});
        await _walletService.TrackScript(_walletId, script);
    }
}