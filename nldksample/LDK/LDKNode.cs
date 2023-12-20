using EventHandler = System.EventHandler;

namespace nldksample.LDK;

public class LDKNode : IAsyncDisposable, IHostedService
{
    private readonly CurrentWalletService _currentWalletService;
    private readonly ILogger _logger;
    public LDKNode(IServiceProvider serviceProvider,
        CurrentWalletService currentWalletService, LDKWalletLogger logger)
    {
        _currentWalletService = currentWalletService;
        _logger = logger;
        ServiceProvider = serviceProvider;
    }


    public event EventHandler OnDisposing;


    public IServiceProvider ServiceProvider { get; }
    private TaskCompletionSource? _started = null;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _currentWalletService.WalletSelected.Task;

        _logger.LogInformation("Wallet selected, starting LDKNode");
        await Semaphore.WaitAsync(cancellationToken);
        var exists = _started is not null;
        _started ??= new TaskCompletionSource();
        Semaphore.Release();
        if (exists)
        {
            _logger.LogInformation("LDKNode already started, will not run start again");
            await _started.Task;
            return;
        }


        var services = ServiceProvider.GetServices<IScopedHostedService>();
        
        _logger.LogInformation("Starting LDKNode services" );
        foreach (var service in services)
        {
            await service.StartAsync(cancellationToken);
        }
        _started.SetResult();
        _logger.LogInformation("LDKNode started");
    }

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