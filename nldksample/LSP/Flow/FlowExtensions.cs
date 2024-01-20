using nldksample.LDK;
using org.ldk.structs;

namespace nldksample.LSP.Flow;

public static class FlowExtensions{

    
    public static WebApplication AddFlowServer(this WebApplication app)
    {
        
        app.MapGet("wallets/{walletId}/flow/api/v1/info", async (string walletId, LDKNodeManager ldkNodeManager) =>
        {
            var node = await ldkNodeManager.GetLDKNodeForWallet(walletId);
            if (node is null)
                return Results.NotFound();
            var flowServer = node.ServiceProvider.GetRequiredService<FlowServer>();
            return Results.Ok(await flowServer.GetInfo());
        });
        app.MapPost("wallets/{walletId}/flow/api/v1/fee", async (string walletId, FlowFeeRequest request, LDKNodeManager ldkNodeManager) =>
        {
            var node = await ldkNodeManager.GetLDKNodeForWallet(walletId);
            if (node is null)
                return Results.NotFound();
            var flowServer = node.ServiceProvider.GetRequiredService<FlowServer>();
            return Results.Ok(await flowServer.RequestFee(request));
        });
        app.MapPost("wallets/{walletId}/flow/api/v1/proposal", async (string walletId, FlowProposalRequest request, LDKNodeManager ldkNodeManager) =>
        {
            var node = await ldkNodeManager.GetLDKNodeForWallet(walletId);
            if (node is null)
                return Results.NotFound();
            var flowServer = node.ServiceProvider.GetRequiredService<FlowServer>();
            return Results.Ok(await flowServer.GetProposal(request));
        });
        return app;
    }
    public static IServiceCollection AddFlowServer(this  IServiceCollection serviceCollection)
    {
        
        return serviceCollection.AddSingleton<FlowServer>()
            .AddSingleton<IScopedHostedService, FlowServer>(provider => provider.GetRequiredService<FlowServer>())
            .AddSingleton<IBroadcastGateKeeper, FlowServer>(provider => provider.GetRequiredService<FlowServer>())
            .AddSingleton<ILDKEventHandler<Event.Event_HTLCHandlingFailed>, FlowServer>(provider =>
                provider.GetRequiredService<FlowServer>())
            .AddSingleton<ILDKEventHandler<Event.Event_PaymentForwarded>, FlowServer>(provider =>
                provider.GetRequiredService<FlowServer>())
            .AddSingleton<ILDKEventHandler<Event.Event_HTLCIntercepted>, FlowServer>(provider =>
                provider.GetRequiredService<FlowServer>());


    }
}