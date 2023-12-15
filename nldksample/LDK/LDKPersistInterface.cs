using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using NLDK;
using org.ldk.enums;
using org.ldk.structs;
using OutPoint = org.ldk.structs.OutPoint;

namespace nldksample.LDK;

public class LDKPersistInterface : PersistInterface
{
    private readonly WalletService _walletService;
    private readonly string _walletId;

    public LDKPersistInterface(CurrentWalletService currentWalletService, WalletService walletService )
    {
        _walletService = walletService;
        _walletId = currentWalletService.CurrentWallet;
    }

    public ChannelMonitorUpdateStatus persist_new_channel(OutPoint channel_id, ChannelMonitor data,
        MonitorUpdateId update_id)
    {
        _walletService.AddOrUpdateChannel(new Channel()
        {
            WalletId = _walletId, FundingTransactionHash = new uint256(channel_id.get_txid()).ToString(),
            FundingTransactionOutputIndex = channel_id.get_index(), Data = data.write()
        }).GetAwaiter().GetResult();

        return ChannelMonitorUpdateStatus.LDKChannelMonitorUpdateStatus_Completed;
    }


    public ChannelMonitorUpdateStatus update_persisted_channel(OutPoint channel_id, ChannelMonitorUpdate update,
        ChannelMonitor data, MonitorUpdateId update_id)
    {
        _walletService.AddOrUpdateChannel(new Channel()
        {
            WalletId = _walletId, FundingTransactionHash = new uint256(channel_id.get_txid()).ToString(),
            FundingTransactionOutputIndex = channel_id.get_index(), Data = data.write()
        }).GetAwaiter().GetResult();

        return ChannelMonitorUpdateStatus.LDKChannelMonitorUpdateStatus_Completed;
    }
}