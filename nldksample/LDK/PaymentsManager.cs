using BTCPayServer.Lightning;
using NBitcoin;
using NLDK;
using org.ldk.structs;
using LightningPayment = NLDK.LightningPayment;

namespace nldksample.LDK;

public class PaymentsManager
{
    private readonly CurrentWalletService _currentWalletService;
    private readonly ChannelManager _channelManager;
    private readonly Logger _logger;
    private readonly NodeSigner _nodeSigner;
    private readonly Network _network;
    private readonly WalletService _walletService;

    public PaymentsManager(
        CurrentWalletService currentWalletService,
        ChannelManager channelManager,
        Logger logger, 
        NodeSigner nodeSigner, 
        Network network, WalletService walletService)
    {
        _currentWalletService = currentWalletService;
        _channelManager = channelManager;
        _logger = logger;
        _nodeSigner = nodeSigner;
        _network = network;
        _walletService = walletService;
    }

    public async Task<BOLT11PaymentRequest> RequestPayment(LightMoney amount, TimeSpan expiry, string description)
    {
        var amt = amount == LightMoney.Zero ? Option_u64Z.none() : Option_u64Z.some(amount.MilliSatoshi);
        var invoice = org.ldk.util.UtilMethods.create_invoice_from_channelmanager(_channelManager, _nodeSigner, _logger,
            _network.GetLdkCurrency(), amt, description, (int) Math.Ceiling(expiry.TotalSeconds), Option_u16Z.none());

        if (invoice is Result_Bolt11InvoiceSignOrCreationErrorZ.Result_Bolt11InvoiceSignOrCreationErrorZ_Err err)
        {
            throw new Exception(err.err.to_str());
        }
        var bolt11 = ((Result_Bolt11InvoiceSignOrCreationErrorZ.Result_Bolt11InvoiceSignOrCreationErrorZ_OK) invoice).res.to_str();
        var paymentRequest = BOLT11PaymentRequest.Parse(bolt11, _network);
        await _walletService.Payment(new LightningPayment()
        {
            WalletId = _currentWalletService.CurrentWallet,
            Inbound = true,
            Value = amount,
            PaymentHash = paymentRequest.PaymentHash.ToString(),
            Secret = paymentRequest.PaymentSecret.ToString(),
            Status = LightningPaymentStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow,
        });
        return paymentRequest;
    }

    public async Task PayInvoice(BOLT11PaymentRequest paymentRequest, LightMoney explicitAmount = null)
    {
        var paymentHash = paymentRequest.PaymentHash;

        var id = RandomUtils.GetBytes(32);
        var invoiceStr = paymentRequest.ToString();
        var invoice =
            ((Result_Bolt11InvoiceParseOrSemanticErrorZ.Result_Bolt11InvoiceParseOrSemanticErrorZ_OK) Bolt11Invoice
                .from_str(invoiceStr)).res;
        var amt = invoice.amount_milli_satoshis() is Option_u64Z.Option_u64Z_Some amtX ? amtX.some : 0;
        amt = Math.Max(amt, explicitAmount?.MilliSatoshi ?? 0);
        var payParams =
            PaymentParameters.from_node_id(invoice.payee_pub_key(), (int) invoice.min_final_cltv_expiry_delta());
        payParams.set_expiry_time(Option_u64Z.some(invoice.expiry_time()));
        
        var lastHops = invoice.route_hints();
        var payee = Payee.clear(invoice.payee_pub_key(), lastHops, invoice.features(), (int) invoice.min_final_cltv_expiry_delta());
        payParams.set_payee(payee);
        var routeParams = RouteParameters.from_payment_params_and_value(payParams, amt);

        await _walletService.Payment(new LightningPayment()
        {
            WalletId = _currentWalletService.CurrentWallet,
            Inbound = true,
            Value = amt,
            PaymentHash = paymentRequest.PaymentHash.ToString(),
            Secret = paymentRequest.PaymentSecret.ToString(),
            Status = LightningPaymentStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow,
            PaymentId = Convert.ToHexString(RandomUtils.GetBytes(32))
        });
        
        var result = _channelManager.send_payment(invoice.payment_hash(),
            RecipientOnionFields.secret_only(invoice.payment_secret()),
            id, routeParams, Retry.timeout(10));
        
        if (result is Result_NoneRetryableSendFailureZ.Result_NoneRetryableSendFailureZ_Err err)
        {
            throw new Exception(err.err.ToString());
        }
    }
}