﻿using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;
using NBitcoin;
using org.ldk.structs;
using WalletWasabi.Userfacing;
using NodeInfo = BTCPayServer.Lightning.NodeInfo;

namespace nldksample.LDK;

public class LDKPeerHandler : IScopedHostedService
{
    private readonly ILogger<LDKPeerHandler> _logger;
    private readonly PeerManager _peerManager;
    private readonly ChannelManager _channelManager;
    private CancellationTokenSource? _cts;

    public event EventHandler<PeersChangedEventArgs> OnPeersChange;

    readonly ObservableConcurrentDictionary<string, LDKTcpDescriptor> _descriptors = new();

    public LDKTcpDescriptor[] ActiveDescriptors => _descriptors.Values.ToArray();
    
    public LDKPeerHandler(PeerManager peerManager, LDKWalletLoggerFactory logger, ChannelManager channelManager)
    {
        _peerManager = peerManager;
        _channelManager = channelManager;
        _logger = logger.CreateLogger<LDKPeerHandler>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ListenForInboundConnections(_cts.Token);
        _ = PeriodicTicker(_cts.Token, 1000, () =>  _peerManager.process_events());
        _descriptors.CollectionChanged += DescriptorsOnCollectionChanged;
    }

    private void DescriptorsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Task.Run(async () =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                await Task.Delay(2000);
            }
            var nodes = await GetPeerNodeIds();
            OnPeersChange?.Invoke(this, new PeersChangedEventArgs(nodes));
        });
    }

    private async Task PeriodicTicker(CancellationToken cancellationToken, int ms, Action action)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(ms, cancellationToken);
            action.Invoke();
        }
    }
    private async Task PeriodicTickerAsync(CancellationToken cancellationToken, int ms, Func<Task> action)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(ms, cancellationToken);
            await action.Invoke();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        _logger.LogInformation("Stopping, disconnecting all peers");
        _peerManager.disconnect_all_peers();
        
        _descriptors.CollectionChanged -= DescriptorsOnCollectionChanged;
    }

    private async Task ListenForInboundConnections(CancellationToken cancellationToken = default)
    {
        using var listener = new TcpListener(new IPEndPoint(IPAddress.Any, 0));
        listener.Start();
        var ip = listener.LocalEndpoint;
        Endpoint = new IPEndPoint(IPAddress.Loopback, (int)ip.Port());
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = LDKTcpDescriptor.Inbound(_peerManager, await listener.AcceptTcpClientAsync(cancellationToken),
                _logger, _descriptors);
            if (result is not null)
            {
                _descriptors.TryAdd(result.Id, result);
                _peerManager.process_events();
            }
        }
    }

    public EndPoint Endpoint { get; set; }

    public async Task<LDKTcpDescriptor?> ConnectAsync(NodeInfo nodeInfo,
        CancellationToken cancellationToken = default)
    {
        var remote = IPEndPoint.Parse(nodeInfo.Host + ":" + nodeInfo.Port);
        return await ConnectAsync(nodeInfo.NodeId, remote, cancellationToken);
    }

    public async Task<LDKTcpDescriptor?> ConnectAsync(PubKey theirNodeId, EndPoint remote,
        CancellationToken cancellationToken = default)
    {
        if (_channelManager.get_our_node_id().SequenceEqual(theirNodeId.ToBytes()))
            return null;

        var client = new TcpClient();
        await client.ConnectAsync(remote.IPEndPoint(), cancellationToken);
        var result = LDKTcpDescriptor.Outbound(_peerManager, client, _logger, theirNodeId, _descriptors);
        if (result is not null)
        {
            _descriptors.TryAdd(result.Id, result);
            _peerManager.process_events();
        }

        return result;
    }

    public async Task<List<NodeInfo>> GetPeerNodeIds()
    {
        return await Task.Run(() => 
        _peerManager.get_peer_node_ids().Select(zz =>
        {
            var pubKey = new PubKey(zz.get_a());
            var addr = zz.get_b() is Option_SocketAddressZ.Option_SocketAddressZ_Some x ? x.some.to_str() : null;
            EndPointParser.TryParse(addr, 9735, out var endpoint);
            return new NodeInfo(pubKey, endpoint.Host(), endpoint.Port().Value);
        }).ToList());
        
       
    }
}

public class PeersChangedEventArgs : EventArgs
{
    public List<NodeInfo> PeerNodeIds { get; set; }

    public PeersChangedEventArgs(List<NodeInfo> peerNodeIds)
    {
        PeerNodeIds = peerNodeIds;
    }
}
