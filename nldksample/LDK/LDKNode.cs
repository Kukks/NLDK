using System.Threading.Channels;
using AsyncKeyedLock;
using NBitcoin;
using org.ldk.structs;
using EventHandler = System.EventHandler;
using NodeInfo = BTCPayServer.Lightning.NodeInfo;

namespace nldksample.LDK;

public class LDKNode : IAsyncDisposable, IHostedService
{
    private readonly CurrentWalletService _currentWalletService;
    private readonly ILogger _logger;
    private readonly ChannelManager _channelManager;
    private readonly LDKPeerHandler _peerHandler;

    public LDKNode(IServiceProvider serviceProvider,
        CurrentWalletService currentWalletService, LDKWalletLogger logger, ChannelManager channelManager, LDKPeerHandler peerHandler)
    {
        _currentWalletService = currentWalletService;
        _logger = logger;
        _channelManager = channelManager;
        _peerHandler = peerHandler;
        ServiceProvider = serviceProvider;
    }

    public PubKey NodeId => new(_channelManager.get_our_node_id());
    
    public NodeInfo? NodeInfo => _peerHandler.Endpoint is null ? null :  NodeInfo.Parse($"{NodeId}@{_peerHandler.Endpoint}");
    

    public event EventHandler OnDisposing;


    public IServiceProvider ServiceProvider { get; }
    private TaskCompletionSource? _started = null;
    private static readonly AsyncNonKeyedLocker Semaphore = new(1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _currentWalletService.WalletSelected.Task;

        bool exists;
        using (await Semaphore.LockAsync(cancellationToken))
        {
            exists = _started is not null;
            _started ??= new TaskCompletionSource();
        }
        if (exists)
        {
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
        bool exists;
        using (await Semaphore.LockAsync(cancellationToken))
        {
            exists = _started is not null;
        }
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