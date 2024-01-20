using Microsoft.EntityFrameworkCore;
using NLDK;

public class MigratonHostedService : IHostedService
{
    private readonly IDbContextFactory<WalletContext> _dbContextFactory;
    private readonly ILogger<MigratonHostedService> _logger;

    public MigratonHostedService(IDbContextFactory<WalletContext> dbContextFactory,
        ILogger<MigratonHostedService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Migrating database");
        await using var ctx = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ctx.Database.MigrateAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Database migrated");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}