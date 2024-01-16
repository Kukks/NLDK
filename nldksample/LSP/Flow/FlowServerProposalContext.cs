using System.Net;
using BTCPayServer.Lightning;
using NBitcoin;

namespace nldksample.LSP.Flow;

public class FlowServerProposalContext
{
    public BOLT11PaymentRequest OriginalBolt11 { get; set; }
    public BOLT11PaymentRequest ProvidedBolt11 { get; set; }
    public LightMoney FeeCharged { get; set; }
    public string? FeeId { get; set; }
    public DateTimeOffset Expiry { get; set; }
    public PubKey NodeId { get; set; }
    public EndPoint? NodeEndpoint { get; set; }
    public LightMoney ChannelSizeToOpen { get; set; }
    public LightMoney ChannelOutbound { get; set; }
    public string? UserChannelId { get; set; }
    

    public Transaction? ChannelOpenTx { get; set; }
    public string? PaymentPreimage { get; set; }
    public string? TempChannelId { get; set; }
}