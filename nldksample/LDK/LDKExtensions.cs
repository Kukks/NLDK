using NBitcoin;
using org.ldk.enums;
using org.ldk.structs;
using EventHandler = org.ldk.structs.EventHandler;
using Network = NBitcoin.Network;
using OutPoint = org.ldk.structs.OutPoint;
using TxOut = NBitcoin.TxOut;

namespace nldksample.LDK;

public class LDKLockableScore: LockableScoreInterface
{
    public ScoreLookUp read_lock()
    {
        throw new NotImplementedException();
    }

    public ScoreUpdate write_lock()
    {
        throw new NotImplementedException();
    }
}

public static class LDKExtensions
{
    public static IServiceCollection AddLDK(this IServiceCollection services)
    {
        services.AddScoped<CurrentWalletService>();
        services.AddScoped<LDKNode>();
        services.AddScoped<IScopedHostedService>(provider => provider.GetRequiredService<LDKNode>());
        services.AddScoped<LDKEventHandler>();
        services.AddScoped<EventHandler>(provider => EventHandler.new_impl(provider.GetRequiredService<LDKEventHandler>()));
        services.AddScoped<LDKFeeEstimator>();
        services.AddScoped<FeeEstimator>(provider => FeeEstimator.new_impl(provider.GetRequiredService<LDKFeeEstimator>()));
        services.AddScoped<LDKPersistInterface>();
        services.AddScoped<Persist>(provider => Persist.new_impl(provider.GetRequiredService<LDKPersistInterface>()));
        services.AddScoped<LDKSignerProvider>();
        services.AddScoped<SignerProvider>(provider => SignerProvider.new_impl(provider.GetRequiredService<LDKSignerProvider>()));
        services.AddScoped<LDKFilter>();
        services.AddScoped<Filter>(provider => Filter.new_impl(provider.GetRequiredService<LDKFilter>()));
        services.AddScoped<ChainMonitor>(provider => 
            ChainMonitor.of(
                Option_FilterZ.some(provider.GetRequiredService<Filter>()), 
                provider.GetRequiredService<BroadcasterInterface>(), 
                provider.GetRequiredService<Logger>(),
                provider.GetRequiredService<FeeEstimator>(),
                provider.GetRequiredService<Persist>()
                ));
        services.AddScoped<Watch>( provider => provider.GetRequiredService<ChainMonitor>().as_Watch());
        services.AddScoped<KeysManager>( provider => provider.GetRequiredService<LDKNode>().KeysManager);
        services.AddScoped<ChannelManager>( provider => provider.GetRequiredService<LDKNode>().ChannelManager);
        services.AddScoped<Filter>(provider => Filter.new_impl(provider.GetRequiredService<LDKFilter>()));
        services.AddSingleton<LDKLogger>();
        services.AddSingleton<LockableScore>(provider => LockableScore.new_impl(provider.GetRequiredService<LDKLockableScore>()));
        services.AddSingleton<Logger>(provider => Logger.new_impl(provider.GetRequiredService<LDKLogger>()));
        services.AddSingleton<NetworkGraph>(provider => NetworkGraph.of(provider.GetRequiredService<Network>().GetLdkNetwork(), provider.GetRequiredService<Logger>()));
        services.AddSingleton<DefaultRouter>(provider => DefaultRouter.of(provider.GetRequiredService<NetworkGraph>(), provider.GetRequiredService<Logger>(), RandomUtils.GetBytes(32), provider.GetRequiredService<LockableScore>(),
            ProbabilisticScoringFeeParameters.with_default()));
        services.AddSingleton<Router>(provider => provider.GetRequiredService<DefaultRouter>().as_Router());
        services.AddSingleton<LDKNodeManager>();
        services.AddHostedService<LDKNodeManager>(provider => provider.GetRequiredService<LDKNodeManager>());
        

    }

    public static org.ldk.enums.Network GetLdkNetwork(this Network network)
    {
        return network.ChainName switch
        {
            { } cn when cn == ChainName.Mainnet => org.ldk.enums.Network.LDKNetwork_Bitcoin,
            { } cn when cn == ChainName.Testnet => org.ldk.enums.Network.LDKNetwork_Testnet,
            { } cn when cn == ChainName.Regtest => org.ldk.enums.Network.LDKNetwork_Regtest,
            _ => throw new NotSupportedException()
        };
    }

    public static TxOut TxOut(this org.ldk.structs.TxOut txOut)
    {
        return new TxOut(Money.Satoshis(txOut.value), Script.FromBytesUnsafe(txOut.script_pubkey));
    }

    public static NBitcoin.OutPoint Outpoint(this org.ldk.structs.OutPoint outPoint)
    {
        return new NBitcoin.OutPoint(new uint256(outPoint.get_txid()), outPoint.get_index());
    }

    public static byte[]? GetPreimage(this PaymentPurpose purpose, out byte[]? secret)
    {
        switch (purpose)
        {
            case PaymentPurpose.PaymentPurpose_InvoicePayment paymentPurposeInvoicePayment:
                secret = paymentPurposeInvoicePayment.payment_secret;
                if (paymentPurposeInvoicePayment.payment_preimage is Option_ThirtyTwoBytesZ.Option_ThirtyTwoBytesZ_Some
                    some)
                    return some.some;
                return null;
            case PaymentPurpose.PaymentPurpose_SpontaneousPayment paymentPurposeSpontaneousPayment:
                secret = null;
                return paymentPurposeSpontaneousPayment.spontaneous_payment;
            default:
                throw new ArgumentOutOfRangeException(nameof(purpose));
        }
    }
}