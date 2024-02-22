using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using org.ldk.structs;
using Wallet = NLDK.Wallet;

namespace nldksample.LDK;

public class LDKChannelSync : IScopedHostedService, IDisposable
{
    private readonly Confirm[] _confirms;
    private readonly NBXListener _nbxListener;
    private readonly CurrentWalletService _currentWalletService;
    private readonly ExplorerClient _explorerClient;
    private readonly Watch _watch;
    private List<IDisposable> _disposables = new();
    

    public LDKChannelSync(
        IEnumerable<Confirm> confirms,
        NBXListener nbxListener,
        CurrentWalletService currentWalletService,
        ExplorerClient explorerClient,
        Watch watch)
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
                Height: zz.get_b(),
                Block: zz.get_c() is Option_ThirtyTwoBytesZ.Option_ThirtyTwoBytesZ_Some some
                    ? new uint256(some.some)
                    : null)));

        Dictionary<uint256, uint256?> txsToBlockHash = new();
        foreach (var (transactionId, height, block) in txs1)
        {
            txsToBlockHash.TryAdd(transactionId, block);
        }

        var txsFetch = await Task.WhenAll(txsToBlockHash.Keys.Select(
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
            var txBlockHash = txsToBlockHash[transactionResult.TransactionHash];

            if (txBlockHash is not null &&
                (transactionResult.Confirmations == 0 || transactionResult.BlockId != txBlockHash))
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

        var headerBytes = latestheader.ToBytes();
        foreach (var confirm in _confirms)
        {
            confirm.best_block_updated(headerBytes, latest.Blocks);
        }

        var monitors = await _currentWalletService.GetInitialChannelMonitors();
        foreach (var channelMonitor in monitors)
        {
            _watch.watch_channel(channelMonitor.get_funding_txo().get_a(), channelMonitor);
        }
        
        _disposables.Add(ChannelExtensions.SubscribeToEventWithChannelQueue<NewBlockEvent>(
            action => _nbxListener.NewBlock += action,
            action => _nbxListener.NewBlock -= action, OnNewBlock,
            cancellationToken)); 
        
        _disposables.Add(ChannelExtensions.SubscribeToEventWithChannelQueue<TransactionUpdateEvent>(
            action => _nbxListener.TransactionUpdate += action,
            action => _nbxListener.TransactionUpdate -= action, OnTransactionUpdate,
            cancellationToken));
    }

  

    private async Task OnTransactionUpdate(TransactionUpdateEvent txUpdate, CancellationToken cancellationToken)
    {
        if (_currentWalletService.CurrentWallet != txUpdate.Wallet.Id)
            return;

        var tx = txUpdate.TransactionInformation.Transaction;
        var txHash = tx.GetHash();
        byte[]? headerBytes = null;
        if (txUpdate.TransactionInformation.Confirmations > 0)
        {
            var header = await _explorerClient.RPCClient
                .GetBlockHeaderAsync(txUpdate.TransactionInformation.BlockHash, CancellationToken.None);
            headerBytes = header.ToBytes();
        }

        foreach (var confirm in _confirms)
        {
            if (txUpdate.TransactionInformation.Confirmations == 0)
                confirm.transaction_unconfirmed(txHash.ToBytes());
            else
                confirm.transactions_confirmed(headerBytes, new[] {TwoTuple_usizeTransactionZ.of(1, tx.ToBytes()),},
                    (int) txUpdate.TransactionInformation.Height);
        }
    }
    private async Task OnNewBlock(NewBlockEvent e, CancellationToken arg2)
    {
        var header = await _explorerClient.RPCClient.GetBlockHeaderAsync(e.Hash, CancellationToken.None);
        var headerBytes = header.ToBytes();
        foreach (var confirm in _confirms)
        {
            confirm.best_block_updated(headerBytes, e.Height);
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}