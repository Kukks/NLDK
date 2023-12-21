using AsyncKeyedLock;
using Microsoft.EntityFrameworkCore;
using NBitcoin;
using NBXplorer;
using NLDK;
using nldksample.Components;
using nldksample.LDK;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var nbxNetworkProvider = new NBXplorerNetworkProvider(ChainName.Regtest);

builder.Services
    .AddHostedService<MigratonHostedService>()
    .AddHostedService<NBXListener>(provider => provider.GetRequiredService<NBXListener>())
    .AddSingleton(new AsyncKeyedLocker<string>(o =>
    {
        o.PoolSize = 20;
        o.PoolInitialFill = 1;
    }))
    .AddSingleton<WalletService>()
    .AddLDK()
    .AddSingleton<NBXListener>()
    .AddSingleton<Network>(provider => provider.GetRequiredService<ExplorerClient>().Network.NBitcoinNetwork)
    .AddSingleton<ExplorerClient>(provider =>
        new ExplorerClient(nbxNetworkProvider.GetFromCryptoCode("BTC"), new Uri("http://localhost:24446")))
    .AddDbContextFactory<WalletContext>(optionsBuilder => optionsBuilder.UseSqlite("wallet.db"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


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
