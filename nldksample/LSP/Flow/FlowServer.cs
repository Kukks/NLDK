using System.Collections.Concurrent;
using System.ComponentModel;
using BTCPayServer.Lightning;
using NBitcoin;
using NLDK;
using nldksample.LDK;
using org.ldk.structs;
using org.ldk.util;
using WalletWasabi.Userfacing;
using Transaction = NBitcoin.Transaction;
using UInt128 = org.ldk.util.UInt128;

namespace nldksample.LSP.Flow;

public class FlowServer : ILDKEventHandler<Event.Event_HTLCIntercepted>, 
    IScopedHostedService, 
    IBroadcastGateKeeper, 
    ILDKEventHandler<Event.Event_HTLCHandlingFailed>,
    ILDKEventHandler<Event.Event_PaymentForwarded>
{
    private readonly LDKFeeEstimator _feeEstimator;
    private readonly PeerManager _peerManager;
    private readonly LDKPeerHandler _peerHandler;
    private readonly ChannelManager _channelManager;
    private readonly LDKWalletLogger _logger;
    private readonly LDKNode _node;
    private readonly Network _network;
    public ConcurrentDictionary<string, FlowServerProposalContext> Proposals = new();
    public ConcurrentDictionary<string, (FlowFeeRequest Request, FlowFeeResponse Response , DateTimeOffset Expiry)> Fees = new();
    private readonly UserConfig _userConfig;
    private readonly LDKFundingGenerationReadyEventHandler _fundingGenerationReadyEventHandler;
    private readonly LDKBroadcaster _broadcaster;
    private readonly IEnumerable<Confirm> _confirms;
    private readonly WalletService _walletService;
    private readonly NodeSigner _nodeSigner;
    private readonly CurrentWalletService _currentWalletService;

    private readonly Logger _ldkLogger;
    // public FlowServerSettings Settings { get; set; }

    public class FlowServerSettings
    {
        public bool Enabled { get; set; }
        public long MinimumAmount { get; set; }
        public long MaximumAmount { get; set; }
        public int FeeTarget { get; set; }

        public LightMoney MinimumChannelBalance { get; set; }
        public LightMoney BaseFee { get; set; }
        public int OnchainByteSizeFootprint { get; set; } = 600;


        public LightMoney GetFee(LightMoney channelBalance, FeeRate feeRate, out LightMoney computedChannelSize)
        {
            var msats = feeRate.GetFee(OnchainByteSizeFootprint).Satoshi * 1000;

            var multiplier = Math.Max(1,
                (int) Math.Ceiling((decimal) (channelBalance.MilliSatoshi / MinimumChannelBalance.MilliSatoshi)));
            
            computedChannelSize = MinimumChannelBalance * multiplier;
            msats += multiplier * BaseFee;

            return new LightMoney(msats);
        }
    }

    public FlowServer(
        LDKFeeEstimator feeEstimator,
        PeerManager peerManager,
        LDKPeerHandler peerHandler,
        ChannelManager channelManager,
        LDKWalletLogger logger,
        LDKNode node,
        Network network, 
        UserConfig userConfig, 
        LDKFundingGenerationReadyEventHandler fundingGenerationReadyEventHandler,
        LDKBroadcaster broadcaster,
        IEnumerable<Confirm> confirms, 
        WalletService walletService,
        NodeSigner nodeSigner,
        CurrentWalletService currentWalletService,
        Logger ldkLogger)
    {
        _feeEstimator = feeEstimator;
        _peerManager = peerManager;
        _peerHandler = peerHandler;
        _channelManager = channelManager;
        _logger = logger;
        _node = node;
        _network = network;
        this._userConfig = userConfig;
        _fundingGenerationReadyEventHandler = fundingGenerationReadyEventHandler;
        _broadcaster = broadcaster;
        _confirms = confirms;
        _walletService = walletService;
        _nodeSigner = nodeSigner;
        _currentWalletService = currentWalletService;
        _ldkLogger = ldkLogger;
    }

    public async Task<FlowServerSettings> GetSettings()
    {
       
        return (await _walletService.GetArbitraryData<FlowServerSettings>(nameof(FlowServerSettings), _currentWalletService.CurrentWallet)) ?? new FlowServerSettings();
    }

    public async Task<FlowFeeResponse> RequestFee(FlowFeeRequest request)
    {
        
        var settings = await GetSettings();
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Flow server is disabled");
        }
        var response = new FlowFeeResponse
        {
            Id = Guid.NewGuid().ToString()
        };
        var feeRate = await _feeEstimator.GetFeeRate(settings.FeeTarget);
        
        response.Amount = settings.GetFee(request.Amount, feeRate, out var computedChannelSize);
        
        Fees.TryAdd(response.Id, (request, response, DateTimeOffset.UtcNow.AddMinutes(10)));
        return response;
    }
    
    public async Task<FlowInfoResponse> GetInfo()
    {
        var settings = await GetSettings();
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Flow server is disabled");
        }
       
       return new FlowInfoResponse
        {
            PubKey = _node.NodeId.ToHex(),
            ConnectionMethods =  _peerHandler.Endpoint is null? Array.Empty<FlowInfoResponse.ConnectionMethod>() : new []
            {
                new FlowInfoResponse.ConnectionMethod()
                {
                    Type = "ipv4",
                    Address =  _peerHandler.Endpoint.Host(),
                    Port = _peerHandler.Endpoint.Port()??0
                }
            }
        };
       
    }

    public async Task<FlowProposalResponse> GetProposal(FlowProposalRequest request)
    {
        var settings = await GetSettings();
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Flow server is disabled");
        }

        var bolt11 = BOLT11PaymentRequest.Parse(request.Bolt11, _network);
        var node = bolt11.GetPayeePubKey();

        LightMoney feeAmt;
        LightMoney? computedChannelSize;
        LightMoney amt = bolt11.MinimumAmount;
        if (request.FeeId is not null)
        {
            var savedFee = Fees[request.FeeId];
           
            if(savedFee.Expiry < DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Fee expired");
            feeAmt = savedFee.Response.Amount;
            amt = savedFee.Request.Amount;
            settings.GetFee(savedFee.Request.Amount, await _feeEstimator.GetFeeRate(settings.FeeTarget), out computedChannelSize);
        }
       else
       {
           amt = bolt11.MinimumAmount;
           feeAmt = settings.GetFee(bolt11.MinimumAmount, await _feeEstimator.GetFeeRate(settings.FeeTarget), out computedChannelSize);
       }
        // UtilMethods.create_invoice_from_channelmanager_and_duration_since_epoch_with_payment_hash(_channelManager, _nodeSigner, _ldkLogger, _network.GetLdkCurrency(),
        //     feeAmt + amt, bolt11.ShortDescription, bolt11.ExpiryDate.ToUnixTimeSeconds(),,bolt11.MinFinalCLTVExpiry)
        BOLT11PaymentRequest propbolt11 = null;
        var proposal = new FlowServerProposalContext()
        {
            OriginalBolt11 = bolt11,
            ProvidedBolt11 = propbolt11,
            FeeCharged = feeAmt,
            FeeId = request.FeeId,
            Expiry = DateTimeOffset.UtcNow.AddMinutes(10),
            NodeId = node,
            NodeEndpoint = request.Host is null ? null :EndPointParser.TryParse(request.Host + ":" + request.Port, 9735, out var ep)? ep: throw new FormatException("Invalid endpoint"),
            ChannelSizeToOpen = computedChannelSize,
            ChannelOutbound = LightMoney.Zero,
            UserChannelId = Convert.ToHexString(RandomUtils.GetBytes(16))
        };

        Proposals.TryAdd(bolt11.PaymentHash.ToString(), proposal);
        return new FlowProposalResponse()
        {
            WrappedBolt11 = propbolt11.ToString()
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _fundingGenerationReadyEventHandler.FundingTransactionGenerated += FundingGenerationReadyEventHandlerOnFundingTransactionGenerated;
    }

    private void FundingGenerationReadyEventHandlerOnFundingTransactionGenerated(object? sender, LDKFundingGenerationReadyEventHandler.FundingTransactionGeneratedEvent e)
    {
        var userChannelId = Convert.ToHexString(e.evt.user_channel_id.getLEBytes());
        if(Proposals.FirstOrDefault(pair => pair.Value.UserChannelId == userChannelId).Value is { } proposal)
        {
            _logger.LogInformation($"A tx was generated for {proposal.NodeId} with user channel id {userChannelId}");
            proposal.ChannelOpenTx = e.tx;
            proposal.TempChannelId = Convert.ToHexString(e.evt.temporary_channel_id);
           
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _fundingGenerationReadyEventHandler.FundingTransactionGenerated -= FundingGenerationReadyEventHandlerOnFundingTransactionGenerated;
    }

    public bool DontBroadcast(Transaction loadedTx)
    {
        var matchedProposal = Proposals.Values.FirstOrDefault(p => p.ChannelOpenTx == loadedTx);
        if(matchedProposal is not null)
        {
            _logger.LogInformation($"We are FAKING broadcasting for a channel open transaction for {matchedProposal.NodeId}");
            var txBytes = loadedTx.GetHash().ToBytes();
            foreach (var confirm in _confirms)
            {
                confirm.transaction_unconfirmed(txBytes);
            }
        }
        return matchedProposal is not null;
    }

    public async Task Handle(Event.Event_HTLCHandlingFailed eventHtlcHandlingFailed)
    {
        //TODO: There is a lot to do here. Apart from the missing branches that may determine the proposal, we also need to handle a "fake" channel close. Maybe we can fake to LDK a double spend of ther channel open utxos that would negate the channel? Or maybe we have to go through its channel close path and then discard the spendableoutput stuff it gives 
        FlowServerProposalContext? proposal = null;
        // cancel the channel open 
        switch (eventHtlcHandlingFailed.failed_next_destination)
        {
            case HTLCDestination.HTLCDestination_FailedPayment htlcDestinationFailedPayment:
                _logger.LogInformation($"We failed to forward a payment for {htlcDestinationFailedPayment.payment_hash}");
                Proposals.TryGetValue(new uint256(htlcDestinationFailedPayment.payment_hash).ToString(),
                    out proposal);
                
                break;
            case HTLCDestination.HTLCDestination_InvalidForward htlcDestinationInvalidForward:
                break;
            case HTLCDestination.HTLCDestination_NextHopChannel htlcDestinationNextHopChannel:
                
                break;
            case HTLCDestination.HTLCDestination_UnknownNextHop htlcDestinationUnknownNextHop:
                
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        if(proposal is null)
            return;

        if (proposal.TempChannelId is not null)
        {
          var channelCloseResult =  _channelManager.close_channel(Convert.FromHexString(proposal.TempChannelId), proposal.NodeId.ToBytes());
        }
    }
    public async Task Handle(Event.Event_PaymentForwarded paymentForwarded)
    {
        if(paymentForwarded.next_channel_id is not Option_ThirtyTwoBytesZ.Option_ThirtyTwoBytesZ_Some some)
        {
            return;
        }
        var channelId = Convert.ToHexString(some.some);
        if (Proposals.FirstOrDefault(pair =>
                (pair.Value.UserChannelId == channelId || pair.Value.TempChannelId == channelId) &&
                pair.Value.ChannelOpenTx is not null).Value is not { } proposal)
        {
            return;
        }
        _logger.LogInformation($"We forwarded a payment on channel {channelId} for {proposal.NodeId} through a channel open. Let's broadcast the channel open transaction as we faked it earlier to ensure we dont gets rugpulled by user");
        await _broadcaster.Broadcast(proposal.ChannelOpenTx!);
        
    }

    public async Task Handle(Event.Event_HTLCIntercepted @event)
    {
        var settings = await GetSettings();
        if (!settings.Enabled)
        {
            _logger.LogInformation("We intercepted an htlc but Flow server is disabled");
            var result = _channelManager.fail_intercepted_htlc(@event.intercept_id);
            if (result is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err err)
                _logger.LogError($"Failed to fail intercepted htlc with error {err.err.GetError()}");
            return;
        }
        else if (!Proposals.TryGetValue(@event.payment_hash.ToString(), out var proposal))
        {
            _logger.LogInformation(
                $"We intercepted an htlc with payment hash {@event.payment_hash} but we don't have a proposal for it");
            var result = _channelManager.fail_intercepted_htlc(@event.intercept_id);
            if (result is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err err)
                _logger.LogError($"Failed to fail intercepted htlc with error {err.err.GetError()}");
            return;
        }
        else if (proposal.Expiry <= DateTimeOffset.Now)
        {
            Proposals.TryRemove(@event.payment_hash.ToString(), out _);
            _logger.LogInformation(
                $"We intercepted an htlc with payment hash {@event.payment_hash} but the proposal is expired");
            var result = _channelManager.fail_intercepted_htlc(@event.intercept_id);
            if (result is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err err)
                _logger.LogError($"Failed to fail intercepted htlc with error {err.err.GetError()}");
            return;
        }
        else
        {
            _logger.LogInformation(
                $"We intercepted an htlc with payment hash {@event.payment_hash} and we have an active proposal for it");
            
            var counterpartyChannels = _channelManager.list_channels_with_counterparty(proposal.NodeId.ToBytes());

            counterpartyChannels = counterpartyChannels.Where(c =>
                    c.get_is_usable() && c.get_next_outbound_htlc_limit_msat() >= @event.expected_outbound_amount_msat)
                .ToArray();

            if (counterpartyChannels.Any())
            {
                //there are channels that can handle this payment. Let's use it. In the future, we can also just decide to open the channel pre-emptively before all other channels are too little.
                var channelToUse = counterpartyChannels.First();
                var result = _channelManager.forward_intercepted_htlc(@event.intercept_id,
                    channelToUse.get_channel_id(), proposal.NodeId.ToBytes(), @event.expected_outbound_amount_msat);
                if (result is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err err)
                    _logger.LogError($"Failed to accept intercepted htlc with error {err.err.GetError()}");

            }
            else
            {
                // We don't have a channel that can handle this payment, let's try to open one
                var expectedFee = proposal.FeeCharged;

                var outboundAmountMsat = @event.expected_outbound_amount_msat - expectedFee.MilliSatoshi;
                var currentPeers = _peerManager.get_peer_node_ids();
                var connectedCounterParty =
                    currentPeers.FirstOrDefault(zz => new PubKey(zz.get_a()) == proposal.NodeId);
                if (connectedCounterParty is null)
                {
                    if (proposal.NodeEndpoint is null)
                    {
                        _logger.LogInformation(
                            $"We intercepted an htlc with payment hash {@event.payment_hash} and we have a proposal for it but we don't have a connected peer and we don't have a host/port to connect to");
                        var failResult = _channelManager.fail_intercepted_htlc(@event.intercept_id);
                        if (failResult is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err failError)
                            _logger.LogError($"Failed to fail intercepted htlc with error {failError.err.GetError()}");
                        return;
                    }
                    else
                    {
                        _logger.LogInformation(
                            $"We intercepted an htlc with payment hash {@event.payment_hash} and we have a proposal for it but we don't have a connected peer. Let's connect to {proposal.NodeEndpoint}");
                        var connected = _peerHandler
                            .ConnectAsync(proposal.NodeId, proposal.NodeEndpoint, CancellationToken.None)
                            .GetAwaiter().GetResult();
                        if (connected is null)
                        {
                            _logger.LogInformation(
                                $"We intercepted an htlc with payment hash {@event.payment_hash} and we have a proposal for it but we don't have a connected peer. Let's connect to {proposal.NodeEndpoint} but we failed to connect");
                            var failResult = _channelManager.fail_intercepted_htlc(@event.intercept_id);
                            if (failResult is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err failError)
                                _logger.LogError(
                                    $"Failed to fail intercepted htlc with error {failError.err.GetError()}");
                            return;

                        }
                        else
                        {



                            //     
                            var result = _channelManager.create_channel(proposal.NodeId.ToBytes(),
                                proposal.ChannelSizeToOpen.MilliSatoshi, proposal.ChannelOutbound.MilliSatoshi,
                                new UInt128(Convert.FromHexString(proposal.UserChannelId)),
                                _userConfig);

                            if (result is Result_ThirtyTwoBytesAPIErrorZ.Result_ThirtyTwoBytesAPIErrorZ_Err errx)
                            {
                                _logger.LogError($"Failed to create channel with error {errx.err.GetError()}");
                                var failResult = _channelManager.fail_intercepted_htlc(@event.intercept_id);
                                if (failResult is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err failError)
                                    _logger.LogError(
                                        $"Failed to fail intercepted htlc with error {failError.err.GetError()}");
                                return;
                            }else if (result is Result_ThirtyTwoBytesAPIErrorZ.Result_ThirtyTwoBytesAPIErrorZ_OK ok)
                            {
                              var channelId =   Convert.ToHexString(ok.res);
                              proposal.UserChannelId = channelId;
                              
                            }

                            



                        }
                        //
                        // _peerHandler.ConnectToPeer(proposal.Item1.Host, proposal.Item1.Port);
                        // _channelManager.create_channel()
                        //
                        // var result = _channelManager.forward_intercepted_htlc(@event.intercept_id, ,counterpartyPubkey.ToBytes(), 0);
                        // if (result is Result_NoneAPIErrorZ.Result_NoneAPIErrorZ_Err err)
                        //     _logger.LogError($"Failed to accept intercepted htlc with error {err.err.GetError()}");
                        // return;
                        // }

                    }



                }
            }


        }

    }
}