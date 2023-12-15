using NBitcoin;
using NLDK;
using org.ldk.structs;
using org.ldk.util;
using UInt128 = org.ldk.util.UInt128;

namespace nldksample.LDK;

public class LDKSignerProvider : SignerProviderInterface
{
    private readonly string _walletId;
    private readonly WalletService _walletService;
    private readonly SignerProviderInterface _innerSigner;

    public LDKSignerProvider(CurrentWalletService currentWalletService, WalletService walletService, SignerProviderInterface innerSigner)
    {
        _walletId = currentWalletService.CurrentWallet;
        _walletService = walletService;
        _innerSigner = innerSigner;
    }

    public byte[] generate_channel_keys_id(bool inbound, long channel_value_satoshis, UInt128 user_channel_id)
    {
        return _innerSigner.generate_channel_keys_id(inbound, channel_value_satoshis, user_channel_id);
    }

    public WriteableEcdsaChannelSigner derive_channel_signer(long channel_value_satoshis, byte[] channel_keys_id)
    {
        return _innerSigner.derive_channel_signer(channel_value_satoshis, channel_keys_id);
    }

    public Result_WriteableEcdsaChannelSignerDecodeErrorZ read_chan_signer(byte[] reader)
    {
        return _innerSigner.read_chan_signer(reader);
    }

    public Result_CVec_u8ZNoneZ get_destination_script()
    {
        var script = _walletService.DeriveScript(_walletId).GetAwaiter().GetResult();
        return Result_CVec_u8ZNoneZ.ok(script.ToScript().ToBytes());
    }

    public Result_ShutdownScriptNoneZ get_shutdown_scriptpubkey()
    {
        var script = _walletService.DeriveScript(_walletId).GetAwaiter().GetResult();
        var s = script.ToScript();


        if (!s.IsScriptType(ScriptType.Witness))
        { throw new NotSupportedException("Generated a non witness script."); }


        var witnessParams = PayToWitTemplate.Instance.ExtractScriptPubKeyParameters2(s);
        var result=  ShutdownScript.new_witness_program(new WitnessVersion((byte) witnessParams.Version), witnessParams.Program) as  Result_ShutdownScriptInvalidShutdownScriptZ.Result_ShutdownScriptInvalidShutdownScriptZ_OK;
        return Result_ShutdownScriptNoneZ.ok(result.res);
    }
}