using AsyncKeyedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBXplorer;
using NLDK;
using nldksample.Components;
using nldksample.LDK;
using nldksample.LSP.Flow;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Configuration.AddEnvironmentVariables("NLDK_");
var nbxNetworkProvider = new NBXplorerNetworkProvider(ChainName.Regtest);

builder.Services
    .Configure<NLDKOptions>(builder.Configuration)
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
    .AddSingleton<ExplorerClient>(sp => 
        new ExplorerClient(nbxNetworkProvider.GetFromCryptoCode("BTC"), new Uri(sp.GetRequiredService<IOptions<NLDKOptions>>().Value.NBXplorerConnection)))
    .AddDbContextFactory<WalletContext>((provider, optionsBuilder) => optionsBuilder.UseSqlite(provider.GetRequiredService<IOptions<NLDKOptions>>().Value.ConnectionString));

var app = builder.Build().AddFlowServer();


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
public partial class Program { }