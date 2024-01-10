using System.Collections.Concurrent;
using BTCPayServer.Lightning;
using NBitcoin;
using NBXplorer.Models;
using NLDK;
using nldksample.LSP.Flow;
using org.ldk.structs;
using LightningPayment = NLDK.LightningPayment;
using Script = NBitcoin.Script;
using Transaction = NBitcoin.Transaction;
using TxOut = NBitcoin.TxOut;
using UInt128 = org.ldk.util.UInt128;

namespace nldksample.LDK;

public class LDKFundingGenerationReadyEventHandler: ILDKEventHandler<Event.Event_FundingGenerationReady>
{
    private readonly LDKFeeEstimator _feeEstimator;
    private readonly CurrentWalletService _currentWalletService;
    private readonly ChannelManager _channelManager;
    private readonly WalletService _walletService;

    public record FundingTransactionGeneratedEvent(Event.Event_FundingGenerationReady evt, Transaction tx);
    public event EventHandler<FundingTransactionGeneratedEvent>? FundingTransactionGenerated;

    public LDKFundingGenerationReadyEventHandler(LDKFeeEstimator feeEstimator, CurrentWalletService currentWalletService, ChannelManager channelManager, WalletService walletService)
    {
        _feeEstimator = feeEstimator;
        _currentWalletService = currentWalletService;
        _channelManager = channelManager;
        _walletService = walletService;
    }
    public async Task Handle(Event.Event_FundingGenerationReady eventFundingGenerationReady)
    {
        var feeRate = _feeEstimator.GetFeeRate().GetAwaiter().GetResult();
        var txOuts = new List<TxOut>()
        {
            new(Money.Satoshis(eventFundingGenerationReady.channel_value_satoshis),
                Script.FromBytesUnsafe(eventFundingGenerationReady.output_script))
        };
        var tx = _walletService.CreateTransaction(_currentWalletService.CurrentWallet, txOuts, feeRate).GetAwaiter().GetResult();
        if (tx is null)
        {
            _channelManager.close_channel(eventFundingGenerationReady.temporary_channel_id, eventFundingGenerationReady.counterparty_node_id);
        }
        else
        {
          var result =   _channelManager.funding_transaction_generated(eventFundingGenerationReady.temporary_channel_id,
                eventFundingGenerationReady.counterparty_node_id, tx.Value.Tx.ToBytes());
          if (result.is_ok())
          {
              FundingTransactionGenerated?.Invoke(this, new FundingTransactionGeneratedEvent(eventFundingGenerationReady, tx.Value.Tx));
          }
        }
    }
}

public class LDKEventHandler : EventHandlerInterface
{
    private readonly string _walletId;
    private readonly KeysManager _keysManager;
    private readonly ChannelManager _channelManager;
    private readonly WalletService _walletService;
    private readonly LDKFeeEstimator _feeEstimator;
    private readonly IEnumerable<ILDKEventHandler> _eventHandlers;

    public ConcurrentBag<(DateTimeOffset, Func<Task>)> ScheduledTasks { get; } = new();

    public LDKEventHandler(CurrentWalletService currentWalletService, KeysManager keysManager, ChannelManager channelManager,
        WalletService walletService, LDKFeeEstimator feeEstimator,  IEnumerable<ILDKEventHandler> eventHandlers)
    {
        
        _walletId = currentWalletService.CurrentWallet;
        _keysManager = keysManager;
        _channelManager = channelManager;
        _walletService = walletService;
        _feeEstimator = feeEstimator;
        _eventHandlers = eventHandlers;
    }


    public void handle_event(Event @event)
    {
        _eventHandlers.AsParallel().ForAll(handler => handler.Handle(@event).GetAwaiter().GetResult());
        
        byte[]? preimage;
        switch (@event)
        {
            case Event.Event_BumpTransaction eventBumpTransaction:
                
//                 switch (eventBumpTransaction.bump_transaction)
//                 {
//                     case BumpTransactionEvent.BumpTransactionEvent_ChannelClose bumpTransactionEventChannelClose:
//
// BumpTransactionEventHandler.
//                         break;
//                     case BumpTransactionEvent.BumpTransactionEvent_HTLCResolution bumpTransactionEventHtlcResolution:
//                         break;
//                     default:
//                         throw new ArgumentOutOfRangeException();
//                 }

                break;
            case Event.Event_ChannelClosed eventChannelClosed:
                // ignored

                break;
            case Event.Event_ChannelPending eventChannelPending:
                // ignored
                break;
            case Event.Event_ChannelReady eventChannelReady:
                // ignored
                break;

            case Event.Event_ProbeFailed eventProbeFailed:
                // ignored
                break;
            case Event.Event_ProbeSuccessful eventProbeSuccessful:
                // ignored
                break;
            case Event.Event_DiscardFunding eventDiscardFunding:
                break;
            case Event.Event_FundingGenerationReady eventFundingGenerationReady:
                
                break;
            case Event.Event_HTLCHandlingFailed eventHtlcHandlingFailed:
                break;
            case Event.Event_HTLCIntercepted eventHtlcIntercepted:
                // this looks juicy  for when we build lsps
                break;
            case Event.Event_InvoiceRequestFailed eventInvoiceRequestFailed:
                break;
            case Event.Event_OpenChannelRequest eventOpenChannelRequest:
                if (eventOpenChannelRequest.channel_type.supports_zero_conf())
                {
                    _channelManager.accept_inbound_channel_from_trusted_peer_0conf(
                        eventOpenChannelRequest.temporary_channel_id,
                        eventOpenChannelRequest.counterparty_node_id,
                        new UInt128(RandomUtils.GetBytes(16))
                    );
                }
                else
                {
                    _channelManager.accept_inbound_channel(
                        eventOpenChannelRequest.temporary_channel_id,
                        eventOpenChannelRequest.counterparty_node_id,
                        new UInt128(RandomUtils.GetBytes(16)));
                }

                break;
            case Event.Event_PaymentClaimable eventPaymentClaimable:

                preimage = eventPaymentClaimable.purpose.GetPreimage(out _);
                if (preimage is not null)
                    _channelManager.claim_funds(preimage);
                else

                    _channelManager.fail_htlc_backwards(eventPaymentClaimable.payment_hash);
                break;
            case Event.Event_PaymentClaimed eventPaymentClaimed:
                preimage = eventPaymentClaimed.purpose.GetPreimage(out var secret);
                _walletService.Payment(new LightningPayment()
                {
                    PaymentHash = Convert.ToHexString(eventPaymentClaimed.payment_hash),
                    Inbound = true,
                    WalletId = _walletId,
                    Secret = secret is null ? null : Convert.ToHexString(secret),
                    Timestamp = DateTimeOffset.UtcNow,
                    Preimage = preimage is null ? null : Convert.ToHexString(preimage),
                    Value = eventPaymentClaimed.amount_msat,
                    Status = LightningPaymentStatus.Complete
                }).GetAwaiter().GetResult();
                break;
            case Event.Event_PaymentFailed eventPaymentFailed:
                
                _walletService.PaymentUpdate(_walletId, Convert.ToHexString(eventPaymentFailed.payment_hash), false, Convert.ToHexString(eventPaymentFailed.payment_id),true, null).GetAwaiter().GetResult();
                break;
            case Event.Event_PaymentForwarded eventPaymentForwarded:
                break;
            case Event.Event_PaymentPathFailed eventPaymentPathFailed:

                break;
            case Event.Event_PaymentPathSuccessful eventPaymentPathSuccessful:
                break;
            case Event.Event_PaymentSent eventPaymentSent:
                
                _walletService.PaymentUpdate(_walletId, Convert.ToHexString(eventPaymentSent.payment_hash), false, Convert.ToHexString(
                    ((Option_ThirtyTwoBytesZ.Option_ThirtyTwoBytesZ_Some)eventPaymentSent.payment_id).some),false, Convert.ToHexString(eventPaymentSent.payment_preimage)).GetAwaiter().GetResult();
                break;
            case Event.Event_PendingHTLCsForwardable eventPendingHtlCsForwardable:
                var time = Random.Shared.NextInt64(eventPendingHtlCsForwardable.time_forwardable,
                    5 * eventPendingHtlCsForwardable.time_forwardable);
                ScheduledTasks.Add((DateTimeOffset.UtcNow.AddMilliseconds(time), () =>
                {
                    _channelManager.process_pending_htlc_forwards();
                    return Task.CompletedTask;
                }));
                break;
            case Event.Event_SpendableOutputs eventSpendableOutputs:

                List<( NBitcoin.OutPoint outPoint, TxOut txOut, byte[] data)> outputs = new();
                foreach (var spendableOutputDescriptor in eventSpendableOutputs.outputs)
                {
                    NBitcoin.TxOut txout = null;
                    NBitcoin.OutPoint outpoint = null;
                    switch (spendableOutputDescriptor)
                    {
                        case SpendableOutputDescriptor.SpendableOutputDescriptor_DelayedPaymentOutput
                            spendableOutputDescriptorDelayedPaymentOutput:
                            txout = spendableOutputDescriptorDelayedPaymentOutput.delayed_payment_output.get_output()
                                .TxOut();
                            outpoint = spendableOutputDescriptorDelayedPaymentOutput.delayed_payment_output
                                .get_outpoint().Outpoint();
                            break;
                        case SpendableOutputDescriptor.SpendableOutputDescriptor_StaticPaymentOutput
                            spendableOutputDescriptorStaticPaymentOutput:
                            txout = spendableOutputDescriptorStaticPaymentOutput.static_payment_output.get_output()
                                .TxOut();
                            outpoint = spendableOutputDescriptorStaticPaymentOutput.static_payment_output
                                .get_outpoint().Outpoint();
                            break;
                    }

                    if (outpoint is null || txout is null) continue;
                    outputs.Add((outpoint, txout, spendableOutputDescriptor.write()));
                }

                _walletService.AddSpendableToCoin(_walletId, outputs.ToArray()).GetAwaiter().GetResult();
                break;
            //tried to do crazy stuff here, but it's not trivial and not all stuff i need is exposed in bindings (like chan_utils)
            // Sequence? sequence;
            // switch (spendableOutputDescriptor)
            // {
            //     case SpendableOutputDescriptor.SpendableOutputDescriptor_DelayedPaymentOutput spendableOutputDescriptorDelayedPaymentOutput:
            //         //TODO: 
            //         // sequence = new Sequence(spendableOutputDescriptorDelayedPaymentOutput
            //         //     .delayed_payment_output.get_to_self_delay());
            //         // spendableOutputDescriptorDelayedPaymentOutput.delayed_payment_output
            //         //     .get_per_commitment_point();
            //         // spendableOutputDescriptorDelayedPaymentOutput.delayed_payment_output
            //         //     .de();
            //         //
            //         break;
            //     case SpendableOutputDescriptor.SpendableOutputDescriptor_StaticOutput spendableOutputDescriptorStaticOutput:
            //         outputs.Add((spendableOutputDescriptorStaticOutput.output.TxOut(),
            //             spendableOutputDescriptorStaticOutput.outpoint.Outpoint(), null, null, null));
            //         break;
            //     case SpendableOutputDescriptor.SpendableOutputDescriptor_StaticPaymentOutput spendableOutputDescriptorStaticPaymentOutput:
            //         var wit =
            //             spendableOutputDescriptorStaticPaymentOutput.static_payment_output.witness_script();
            //         sequence = spendableOutputDescriptorStaticPaymentOutput.static_payment_output
            //             .get_channel_transaction_parameters().get_channel_type_features()
            //             .supports_anchors_zero_fee_htlc_tx()
            //             ? new Sequence(1)
            //             : null;
            //         var signer = _keysManager.derive_channel_keys(
            //             spendableOutputDescriptorStaticPaymentOutput.static_payment_output
            //                 .get_channel_value_satoshis(),
            //             spendableOutputDescriptorStaticPaymentOutput.static_payment_output
            //                 .get_channel_keys_id()).as_ChannelSigner();
            //         
            //         signer.provide_channel_parameters(spendableOutputDescriptorStaticPaymentOutput.static_payment_output.get_channel_transaction_parameters());
            //         signer.
            //         switch (wit)
            //         {
            //             case Option_CVec_u8ZZ.Option_CVec_u8ZZ_None optionCVecU8ZzNone:
            //                 outputs.Add((spendableOutputDescriptorStaticPaymentOutput.static_payment_output.get_output().TxOut(),
            //                     spendableOutputDescriptorStaticPaymentOutput.static_payment_output.get_outpoint().Outpoint(), null, sequence ));
            //                 break;
            //             case Option_CVec_u8ZZ.Option_CVec_u8ZZ_Some optionCVecU8ZzSome:
            //                 outputs.Add((spendableOutputDescriptorStaticPaymentOutput.static_payment_output.get_output().TxOut(),
            //                     spendableOutputDescriptorStaticPaymentOutput.static_payment_output.get_outpoint().Outpoint(), Script.FromBytesUnsafe(optionCVecU8ZzSome.some).ToString(), sequence));
            //                 break;
            //             default:
            //                 throw new ArgumentOutOfRangeException(nameof(wit));
            //         }
            //         break;
            //     default:
            //         throw new ArgumentOutOfRangeException(nameof(spendableOutputDescriptor));
            // }
            //   }
            default:
                throw new ArgumentOutOfRangeException(nameof(@event));
        }
    }
}