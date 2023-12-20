using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using org.ldk.structs;
using Wallet = NLDK.Wallet;

namespace nldksample.LDK;

public class LDKChannelSync : IScopedHostedService
{
    private readonly Confirm[] _confirms;
    private readonly NBXListener _nbxListener;
    private readonly CurrentWalletService _currentWalletService;
    private readonly ExplorerClient _explorerClient;
    private readonly Watch _watch;

    public LDKChannelSync(
        IEnumerable<Confirm> confirms,
        NBXListener nbxListener,
        CurrentWalletService currentWalletService,
        ExplorerClient explorerClient, Watch watch)
    {
        _confirms = confirms.ToArray();
        _nbxListener = nbxListener;
        _currentWalletService = currentWalletService;
        _explorerClient = explorerClient;
        _watch = watch;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var txs1 = _confirms.SelectMany(confirm => confirm.get_relevant_txids().Select(zz =>
            (TransactionId: new uint256(zz.get_a()),
                Block: zz.get_b() is Option_ThirtyTwoBytesZ.Option_ThirtyTwoBytesZ_Some some
                    ? new uint256(some.some)
                    : null)));

        Dictionary<uint256, uint256?> txs = new();
        foreach (var (transactionId, block) in txs1)
        {
            if (txs.TryAdd(transactionId, block)) continue;
            if (block is not null)
                txs[transactionId] = block;
        }

        var txsFetch = await Task.WhenAll(txs.Keys.Select(
            uint256 =>
                _explorerClient.GetTransactionAsync(uint256, cancellationToken)));

        var batch = _explorerClient.RPCClient.PrepareBatch();
        var headersTask = txsFetch.Where(result => result.BlockId is not null && result.BlockId != uint256.Zero)
            .Distinct().ToDictionary(result => result.BlockId, result =>
                batch.GetBlockHeaderAsync(result.BlockId, cancellationToken));
        await batch.SendBatchAsync(cancellationToken);

        var headerToHeight = (await Task.WhenAll(headersTask.Values)).ToDictionary(header => header.GetHash(),
            header => txsFetch.First(result => result.BlockId == header.GetHash()).Height!);
        Dictionary<uint256, List<TwoTuple_usizeTransactionZ>> confirmedTxList = new();
        foreach (var transactionResult in txsFetch)
        {
            var tx = txs[transactionResult.TransactionHash];

            if (tx is not null && transactionResult.Confirmations == 0 || transactionResult.TransactionHash != tx)
            {
                foreach (var confirm in _confirms)
                {
                    confirm.transaction_unconfirmed(transactionResult.TransactionHash.ToBytes());
                }
            }

            if (transactionResult.Confirmations > 0 &&
                headersTask.TryGetValue(transactionResult.BlockId, out var headerTask))
            {
                confirmedTxList.TryAdd(transactionResult.BlockId, new List<TwoTuple_usizeTransactionZ>());
                ;
                var txList = confirmedTxList[transactionResult.BlockId];
                txList.Add(TwoTuple_usizeTransactionZ.of(1, transactionResult.Transaction.ToBytes()));
            }
        }

        foreach (var confirmedTxListItem in confirmedTxList)
        {
            var header = (await headersTask[confirmedTxListItem.Key]).ToBytes();
            var height = headerToHeight[confirmedTxListItem.Key];
            foreach (var confirm in _confirms)
            {
                confirm.transactions_confirmed(header, confirmedTxListItem.Value.ToArray(), (int) height!);
            }
        }

        var latest = await _explorerClient.RPCClient.GetBlockchainInfoAsyncEx(cancellationToken);
        BlockHeader latestheader = null;
        if (headersTask.TryGetValue(latest.BestBlockHash, out var h))
        {
            latestheader = await h;
        }
        else
        {
            latestheader = await _explorerClient.RPCClient.GetBlockHeaderAsync(latest.BestBlockHash, cancellationToken);
        }

        foreach (var confirm in _confirms)
        {
            confirm.best_block_updated(latestheader.ToBytes(), latest.Blocks);
        }

        var monitors = _currentWalletService.GetInitialChannelMonitors(null, null);
        foreach (var channelMonitor in monitors)
        {
            _watch.watch_channel(channelMonitor.get_funding_txo().get_a(), channelMonitor);
        }

        _nbxListener.NewBlock += OnNewBlock;
        _nbxListener.TransactionUpdate += OnTransactionUpdate;
    }

    private void OnTransactionUpdate(object? sender,
        (Wallet Wallet, TrackedSource TrackedSource, TransactionInformation TransactionInformation) valueTuple)
    {
        if (_currentWalletService.CurrentWallet != valueTuple.Wallet.Id)
            return;

        var tx = valueTuple.TransactionInformation.Transaction;

        foreach (var confirm in _confirms)
        {
            confirm.transaction_unconfirmed(tx.ToBytes());
        }
    }


    private void OnNewBlock(object? sender, NewBlockEvent e)
    {
        var header = _explorerClient.RPCClient.GetBlockHeaderAsync(e.Hash, CancellationToken.None).GetAwaiter()
            .GetResult();
        var headerBytes = header.ToBytes();
        foreach (var confirm in _confirms)
        {
            confirm.best_block_updated(headerBytes, e.Height);
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _nbxListener.NewBlock -= OnNewBlock;
        _nbxListener.TransactionUpdate -= OnTransactionUpdate;
    }
}