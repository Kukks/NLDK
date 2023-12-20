using System.Collections.Concurrent;
using System.Collections.Immutable;
using NBitcoin;
using NBXplorer;
using NLDK;
using org.ldk.structs;
using Wallet = NLDK.Wallet;

namespace nldksample.LDK;

public class LDKNodeManager : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WalletService _walletService;
    private readonly ExplorerClient _explorerClient;
    private readonly ILogger<LDKNodeManager> _logger;

    public LDKNodeManager(IServiceScopeFactory serviceScopeFactory, WalletService walletService,
        ExplorerClient explorerClient, ILogger<LDKNodeManager> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _walletService = walletService;
        _explorerClient = explorerClient;
        _logger = logger;
    }

    private ConcurrentDictionary<string, LDKNode> Nodes { get; } = new();

    
    
    
    public async Task<LDKNode> GetLDKNodeForWallet(Wallet wallet, CancellationToken cancellationToken = default)
    {
        
        _logger.LogInformation($"Creating LDKNode for wallet {wallet.Id}");
     
        var result = Nodes.GetOrAdd(wallet.Id, s =>
        {
            var scope = _serviceScopeFactory.CreateScope();
            
            _logger.LogInformation($"Scope for wallet {wallet.Id} created");
            scope.ServiceProvider.GetRequiredService<CurrentWalletService>().SetWallet(wallet);
            
            var node = scope.ServiceProvider.GetRequiredService<LDKNode>();
            node.OnDisposing += (sender, args) =>
            {
                _logger.LogInformation($"LDK wallet {wallet.Id} disposed");
                Nodes.TryRemove(wallet.Id, out _);
                scope.Dispose();
            };
            return node;
        });


        await result.StartAsync(cancellationToken);
        return result;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Data = (await _walletService.GetArbitraryData(null, cancellationToken)).ToImmutableDictionary();
        
        Task.Run(async () =>
        {
            
            await _explorerClient.WaitServerStartedAsync(cancellationToken);
            var wallets = await _walletService.GetAll(cancellationToken);
            await Task.WhenAll(
                wallets.Select(async wallet => { await GetLDKNodeForWallet(wallet, cancellationToken); }));
        }, cancellationToken);
    }

    public ImmutableDictionary<string, byte[]> Data { get; set; }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var node in Nodes.Values)
        {
            await node.StopAsync(cancellationToken);
        }

        Nodes.Clear();
    }

    public void Dispose()
    {
        foreach (var node in Nodes.Values)
        {
            node.Dispose();
        }
    }
}