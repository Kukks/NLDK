using NBitcoin;
using Newtonsoft.Json;

namespace nldksample.LDK;

public class GetBlockchainInfoResponse
{
    [JsonProperty("headers")]
    public int Headers
    {
        get; set;
    }
    [JsonProperty("blocks")]
    public int Blocks
    {
        get; set;
    }
    [JsonProperty("verificationprogress")]
    public double VerificationProgress
    {
        get; set;
    }

    [JsonProperty("mediantime")]
    public long? MedianTime
    {
        get; set;
    }

    [JsonProperty("initialblockdownload")]
    public bool? InitialBlockDownload
    {
        get; set;
    }
    [JsonProperty("bestblockhash")]
    [JsonConverter(typeof(NBitcoin.JsonConverters.UInt256JsonConverter))]
    public uint256 BestBlockHash { get; set; }
}