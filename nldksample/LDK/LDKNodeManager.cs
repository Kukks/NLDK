using System.Collections.Concurrent;
using NLDK;

namespace nldksample.LDK;

public class LDKNodeManager : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly WalletService _walletService;

    public LDKNodeManager(IServiceScopeFactory serviceScopeFactory, WalletService walletService)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _walletService = walletService;
    }

    private ConcurrentDictionary<string, LDKNode> Nodes { get; } = new();

    public async Task<LDKNode> GetLDKNodeForWallet(string walletId, CancellationToken cancellationToken = default)
    {
        var result = Nodes.GetOrAdd(walletId, s =>
        {
            var scope = _serviceScopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<CurrentWalletService>().CurrentWallet = walletId;
            var node = scope.ServiceProvider.GetRequiredService<LDKNode>();
            node.OnDisposing += (sender, args) =>
            {
                Nodes.TryRemove(walletId, out _);
                scope.Dispose();
            };
            return node;
        });

        await result.StartAsync(cancellationToken);
        return result;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var wallets = await _walletService.GetAll(cancellationToken);
        await Task.WhenAll(wallets.Select(async wallet =>
        {
            await GetLDKNodeForWallet(wallet.Id, cancellationToken);
        }));
    }

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