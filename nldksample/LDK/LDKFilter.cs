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
    private readonly WalletTrackedSource _ts;

    public LDKFilter(CurrentWalletService currentWalletService, WalletService walletService, ExplorerClient explorerClient)
    {
        _walletId = currentWalletService.CurrentWallet;
        _currentWalletService = currentWalletService;
        _walletService = walletService;
        _explorerClient = explorerClient;
        _ts = new WalletTrackedSource(currentWalletService.CurrentWallet);
    }

    public void register_tx(byte[] txid, byte[] script_pubkey)
    {
        var script = Script.FromBytesUnsafe(script_pubkey);
        Track(script).GetAwaiter().GetResult();
    }

    public void register_output(WatchedOutput output)
    {
        var script = Script.FromBytesUnsafe(output.get_script_pubkey());
        Track(script).GetAwaiter().GetResult();
    }

    private async Task Track(Script script)
    {
        await _explorerClient.AssociateScriptsAsync(_ts, new AssociateScriptRequest[]
        {
            new()
            {
                ScriptPubKey = script
            }
        });
        await _walletService.TrackScript(_walletId, script);
    }
}