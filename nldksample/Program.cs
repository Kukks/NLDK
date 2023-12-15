using AsyncKeyedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using NBXplorer;
using NLDK;
using nldksample.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var nbxNetworkProvider = new NBXplorerNetworkProvider(ChainName.Regtest);

builder.Services
    .AddScoped<WalletService>()
    .AddSingleton<AsyncKeyedLocker<string>>()
    .AddSingleton<WalletService>()
    // .AddSingleton(builder.Services)
    .AddHostedService<NBXListener>()
    
    .AddSingleton<Network>(provider => provider.GetRequiredService<ExplorerClient>().Network.NBitcoinNetwork)
    .AddSingleton<ExplorerClient>(provider => new ExplorerClient(nbxNetworkProvider.GetFromCryptoCode("BTC"), new Uri("http://localhost:4774")))
    .AddDbContextFactory<WalletContext>(optionsBuilder => optionsBuilder.UseSqlite("wallet.db"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.Services.GetRequiredService<IDbContextFactory<WalletContext>>().CreateDbContext().Database.Migrate();
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();