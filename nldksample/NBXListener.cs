using NBitcoin;
using NBXplorer;
using NBXplorer.Models;
using NLDK;

public class NBXListener : IHostedService
{
    private readonly ExplorerClient _explorerClient;
    private readonly WalletService _walletService;
    private readonly ILogger<NBXListener> _logger;
    private WebsocketNotificationSession _session;

    public NBXListener(ExplorerClient explorerClient, WalletService walletService, ILogger<NBXListener> logger)
    {
        _explorerClient = explorerClient;
        _walletService = walletService;
        _logger = logger;
    }

    public event EventHandler<NewBlockEvent>? NewBlock;

    public event EventHandler<(TrackedSource TrackedSource, TransactionInformation TransactionInformation)>?
        TransactionUpdate;

    public TaskCompletionSource ConnectedAndSynced { get; private set; } = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var lcts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                try
                {
                    _logger.LogInformation("Waiting for NBXplorer to start...");
                    await _explorerClient.WaitServerStartedAsync(cancellationToken);
                    _session = await _explorerClient.CreateWebsocketNotificationSessionAsync(cancellationToken);
                    await _session.ListenAllTrackedSourceAsync(cancellation: cancellationToken);
                    await _session.ListenNewBlockAsync(cancellation: cancellationToken);
                    var task = Loop(_session, lcts.Token);
                    ConnectedAndSynced.TrySetResult();
                    _logger.LogInformation("NBXplorer started");
                    await task;
                }
                catch (Exception e)
                {
                    await lcts.CancelAsync();
                    _logger.LogError(e, "Error starting NBXplorer");
                    ConnectedAndSynced = new();
                }
            }
        }, cancellationToken);
    }

    private async Task Loop(WebsocketNotificationSession session, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var evt = await session.NextEventAsync(cancellationToken);
            switch (evt)
            {
                case NewBlockEvent newBlockEvent:
                    await _walletService.HandlePendingTxs(async list =>
                    {
                        var txs = await Task.WhenAll(list.Select(transaction => uint256.Parse(transaction.Hash)).Select(
                            uint256 =>
                                _explorerClient.GetTransactionAsync(uint256, cancellationToken)));

                        list.ForEach(transaction =>
                        {
                            var tx = txs.FirstOrDefault(result =>
                                result.TransactionHash.ToString() == transaction.Hash);
                            if (tx is null)
                                return;
                            transaction.BlockHash = tx.BlockId == uint256.Zero ? null : tx.BlockId?.ToString();
                        });
                    }, cancellationToken);
                    NewBlock?.Invoke(this, newBlockEvent);
                    break;
                case NewTransactionEvent newTransactionEvent:

                    var w = await _walletService.Get(newTransactionEvent.TrackedSource.ToString(), cancellationToken);
                    if (w is null)
                        continue;
                    var tx = await _explorerClient.GetTransactionAsync(newTransactionEvent.TrackedSource,
                        newTransactionEvent.TransactionData.TransactionHash, cancellationToken);
                    if (tx is null)
                        continue;

                    await _walletService.OnTransactionSeen(w, tx, cancellationToken);
                    TransactionUpdate?.Invoke(this, (newTransactionEvent.TrackedSource, tx));
                    break;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _session.DisposeAsync(cancellationToken);
    }
}