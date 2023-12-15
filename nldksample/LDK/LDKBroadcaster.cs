using NBitcoin;
using NBXplorer;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKBroadcaster : BroadcasterInterfaceInterface
{
    private readonly ExplorerClient _explorerClient;

    public LDKBroadcaster(ExplorerClient explorerClient)
    {
        _explorerClient = explorerClient;
    }

    public void broadcast_transactions(byte[][] txs)
    {
        foreach (var tx in txs)
        {
            var loadedTx = Transaction.Load(tx, _explorerClient.Network.NBitcoinNetwork);

            _explorerClient.Broadcast(loadedTx);
        }
    }
}