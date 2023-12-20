using NLDK;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKPersister: PersisterInterface
{
    private readonly CurrentWalletService _currentWalletService;
    private readonly WalletService _walletService;

    public LDKPersister(CurrentWalletService currentWalletService, WalletService walletService)
    {
        _currentWalletService = currentWalletService;
        _walletService = walletService;
    }
    public Result_NoneIOErrorZ persist_manager(ChannelManager channel_manager)
    {
        _walletService.AddOrUpdateArbitraryData(_currentWalletService.CurrentWallet, "ChannelManager", channel_manager.write()).GetAwaiter().GetResult();
        return Result_NoneIOErrorZ.ok();
    }

    public Result_NoneIOErrorZ persist_graph(NetworkGraph network_graph)
    {
        _walletService.AddOrUpdateArbitraryData(null, "NetworkGraph", network_graph.write()).GetAwaiter().GetResult();
        return Result_NoneIOErrorZ.ok();
    }

    public Result_NoneIOErrorZ persist_scorer(WriteableScore scorer)
    {
        _walletService.AddOrUpdateArbitraryData(null, "Score", scorer.write()).GetAwaiter().GetResult();
        return Result_NoneIOErrorZ.ok();
    }
}