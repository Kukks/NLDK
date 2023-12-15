using NBitcoin;
using NBXplorer;
using org.ldk.structs;
using EventHandler = System.EventHandler;

namespace nldksample.LDK;

public class LDKNode : IScopedHostedService, IAsyncDisposable
{
    private readonly CurrentWalletService _currentWalletService;
    private readonly FeeEstimator _feeEstimator;
    private readonly Watch _watch;
    private readonly BroadcasterInterface _broadcasterInterface;
    private readonly Router _router;
    private readonly Logger _logger;
    private readonly SignerProvider _signerProvider;
    private readonly ExplorerClient _explorerClient;
    private readonly Network _network;

    public LDKNode(IServiceProvider serviceProvider, 
        CurrentWalletService currentWalletService,
        FeeEstimator feeEstimator , 
        Watch watch, BroadcasterInterface broadcasterInterface, Router router, Logger logger, SignerProvider signerProvider, ExplorerClient explorerClient, Network network)
    {
        _currentWalletService = currentWalletService;
        _feeEstimator = feeEstimator;
        _watch = watch;
        _broadcasterInterface = broadcasterInterface;
        _router = router;
        _logger = logger;
        _signerProvider = signerProvider;
        _explorerClient = explorerClient;
        _network = network;
        ServiceProvider = serviceProvider;
    }


    public event EventHandler OnDisposing;


    public IServiceProvider ServiceProvider { get; }
    private TaskCompletionSource? _started = null;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _currentWalletService.WalletSelected.Task;

        await Semaphore.WaitAsync(cancellationToken);
        var exists = _started is not null;
        _started ??= new TaskCompletionSource();
        Semaphore.Release();
        if (exists)
        {
            await _started.Task;
            return;
        }

        
        KeysManager = KeysManager.of(_currentWalletService.Seed, DateTimeOffset.Now.ToUnixTimeSeconds(), RandomUtils.GetInt32());

        var hash = await _explorerClient.RPCClient.GetBestBlockHashAsync(cancellationToken);
        var height = await _explorerClient.RPCClient.GetBlockchainInfoAsync(cancellationToken);
        ChannelManager = ChannelManager.of(_feeEstimator, _watch, _broadcasterInterface, _router, _logger,
            KeysManager.as_EntropySource(), KeysManager.as_NodeSigner(), _signerProvider, UserConfig.with_default(),
            ChainParameters.of(_network.GetLdkNetwork(), BestBlock.of(hash.ToBytes(), (int) height.Blocks)),
            (int) DateTimeOffset.Now.ToUnixTimeSeconds());
        
        var services = ServiceProvider.GetServices<IScopedHostedService>();
        foreach (var service in services)
        {
            await service.StartAsync(cancellationToken);
        }

        _started.SetResult();
    }

    public KeysManager KeysManager { get; private set; }
    public ChannelManager ChannelManager { get; private set; }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Semaphore.WaitAsync(cancellationToken);
        var exists = _started is not null;
        Semaphore.Release();
        if (!exists)
            return;

        var services = ServiceProvider.GetServices<IScopedHostedService>();
        foreach (var service in services)
        {
            await service.StopAsync(cancellationToken);
        }
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        OnDisposing?.Invoke(this, EventArgs.Empty);
    }
}