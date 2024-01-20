using NBitcoin;
using NBXplorer;
using nldksample.LSP.Flow;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKBroadcaster : BroadcasterInterfaceInterface
{
    private readonly ExplorerClient _explorerClient;
    private readonly IEnumerable<IBroadcastGateKeeper> _broadcastGateKeepers;

    public LDKBroadcaster(ExplorerClient explorerClient, IEnumerable<IBroadcastGateKeeper> broadcastGateKeepers)
    {
        _explorerClient = explorerClient;
        _broadcastGateKeepers = broadcastGateKeepers;
    }

    public void broadcast_transactions(byte[][] txs)
    {
        foreach (var tx in txs)
        {
            var loadedTx = Transaction.Load(tx, _explorerClient.Network.NBitcoinNetwork);
            if(_broadcastGateKeepers.Any(gk => gk.DontBroadcast(loadedTx)))
                continue;
            Broadcast(loadedTx).GetAwaiter().GetResult();
        }
    }

    public async Task Broadcast(Transaction transaction, CancellationToken cancellationToken = default)
    {
       await  _explorerClient.BroadcastAsync(transaction, cancellationToken);
    }
}

