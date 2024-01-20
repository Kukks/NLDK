using System.Diagnostics;
using System.Net;
using BTCPayServer.Lightning;
using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Userfacing;

namespace nldksample.LSP.Flow;

/// <summary>
/// https://docs.voltage.cloud/flow/flow-2.0
/// </summary>
public class Flow2Client
{
    private readonly HttpClient _httpClient;
    private readonly Network _network;

    public static Uri? BaseAddress(Network network)
    {
        return network switch
        {
            not null when network == Network.Main => new Uri("https://lsp.voltageapi.com"),
            not null when network == Network.TestNet => new Uri("https://testnet-lsp.voltageapi.com"),
            // not null when network == Network.RegTest => new Uri("https://localhost:5001/jit-lsp"),
            _ => null
        };
    }

    public Flow2Client(HttpClient httpClient, Network network)
    {
        if(httpClient.BaseAddress == null)
            throw new ArgumentException("HttpClient must have a base address, use Flow2Client.BaseAddress to get a predefined URI", nameof(httpClient));
        
        _httpClient = httpClient;
        _network = network;
    }

    public async Task<FlowInfoResponse> GetInfo(CancellationToken cancellationToken = default)
    {
        var path = "/api/v1/info";
        var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonConvert.DeserializeObject<FlowInfoResponse>(content);
    }

    public async Task<FlowFeeResponse> GetFee(LightMoney amount, PubKey pubkey,
        CancellationToken cancellationToken = default)
    {
        var path = "/api/v1/fee";
        var request = new FlowFeeRequest(amount, pubkey);
        var response = await _httpClient.PostAsync(path, new StringContent(JsonConvert.SerializeObject(request)),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonConvert.DeserializeObject<FlowFeeResponse>(content);
    }

    public BOLT11PaymentRequest GetProposal(BOLT11PaymentRequest bolt11PaymentRequest, EndPoint? endPoint = null, string? feeId = null, CancellationToken cancellationToken = default)
    {
        var path = "/api/v1/proposal";
        var request = new FlowProposalRequest()
        {
            Bolt11 = bolt11PaymentRequest.ToString(),
            Host = endPoint?.Host(),
            Port = endPoint?.Port(),
            FeeId = feeId,
            
        };
        var response = _httpClient
            .PostAsync(path, new StringContent(JsonConvert.SerializeObject(request)), cancellationToken).GetAwaiter()
            .GetResult();
        response.EnsureSuccessStatusCode();
        var content = response.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        var result = JsonConvert.DeserializeObject<FlowProposalResponse>(content);

        return BOLT11PaymentRequest.Parse(result.WrappedBolt11, _network);
    }
}

public class FlowProposalResponse
{
    [JsonProperty("jit_bolt11")] public required string WrappedBolt11 { get; set; }
}

public class FlowProposalRequest
{
    [JsonProperty("bolt11")] public required string Bolt11 { get; set; }

    [JsonProperty("host", NullValueHandling = NullValueHandling.Ignore)]
    public string? Host { get; set; }


    [JsonProperty("port", NullValueHandling = NullValueHandling.Ignore)]
    public int? Port { get; set; }

    [JsonProperty("fee_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? FeeId { get; set; }
}

public class FlowInfoResponse
{
    [JsonProperty("connection_methods")] public ConnectionMethod[] ConnectionMethods { get; set; }
    [JsonProperty("pubkey")] public required string PubKey { get; set; }

    NodeInfo[] ToNodeInfo()
    {
        var pubkey = new PubKey(PubKey);
        return ConnectionMethods.Select(method => new NodeInfo(pubkey, method.Address, method.Port)).ToArray();
    }

    public class ConnectionMethod
    {
        [JsonProperty("address")] public string Address { get; set; }
        [JsonProperty("port")] public int Port { get; set; }
        [JsonProperty("type")] public string Type { get; set; }

        public EndPoint? ToEndpoint()
        {
            return EndPointParser.TryParse($"{Address}:{Port}", 9735, out var endpoint) ? endpoint : null;
        }
    }
}

public class FlowFeeResponse
{
    [JsonProperty("amount_msat")] public long Amount { get; set; }
    [JsonProperty("id")] public required string Id { get; set; }
    
}

public class FlowFeeRequest
{
    public FlowFeeRequest()
    {
    }

    public FlowFeeRequest(LightMoney amount, PubKey pubkey)
    {
        Amount = amount.MilliSatoshi;
        PubKey = pubkey.ToHex();
    }

    [JsonProperty("amount_msat")] public long Amount { get; set; }
    [JsonProperty("pubkey")] public string PubKey { get; set; }
}