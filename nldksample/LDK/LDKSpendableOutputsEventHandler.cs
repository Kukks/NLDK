using NLDK;
using org.ldk.structs;
using TxOut = NBitcoin.TxOut;

namespace nldksample.LDK;

public class LDKSpendableOutputsEventHandler : ILDKEventHandler<Event.Event_SpendableOutputs>
{
    private readonly WalletService _walletService;
    private readonly CurrentWalletService _currentWalletService;

    public LDKSpendableOutputsEventHandler(WalletService walletService, CurrentWalletService currentWalletService)
    {
        _walletService = walletService;
        _currentWalletService = currentWalletService;
        _walletService = walletService;
    }

    public async Task Handle(Event.Event_SpendableOutputs eventSpendableOutputs)
    {
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

        _walletService.AddSpendableToCoin(_currentWalletService.CurrentWallet, outputs.ToArray()).GetAwaiter()
            .GetResult();
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
    }
}