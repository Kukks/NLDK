using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using NBitcoin;
using nldksample.LDK;
using org.ldk.structs;
using WalletWasabi.Userfacing;
using NodeInfo = BTCPayServer.Lightning.NodeInfo;


public class LDKPeerHandler : IScopedHostedService
{
    private readonly ILogger<LDKPeerHandler> _logger;
    private readonly PeerManager _peerManager;
    private CancellationTokenSource? _cts;
    
    ConcurrentDictionary<string, LDKSTcpDescriptor> _descriptors = new();

    
    public LDKPeerHandler(PeerManager peerManager, LDKWalletLoggerFactory logger)
    {
        _peerManager = peerManager;
        _logger = logger.CreateLogger<LDKPeerHandler>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ListenForInboundConnections(_cts.Token);
        _ = PeriodicTicker(_cts.Token, 10000, () => _peerManager.timer_tick_occurred());
        _ = PeriodicTicker(_cts.Token, 1000, () => _peerManager.process_events());
    }

    private async Task PeriodicTicker(CancellationToken cancellationToken, int ms, Action action)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(ms, cancellationToken);
            action.Invoke();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        _logger.LogInformation("Stopping, disconnecting all peers");
        _peerManager.disconnect_all_peers();
    }

    private async Task ListenForInboundConnections(CancellationToken cancellationToken = default)
    {
        using var listener = new TcpListener(new IPEndPoint(IPAddress.Any, 0));
        listener.Start();
        var ip = listener.LocalEndpoint;
        Endpoint = new IPEndPoint(IPAddress.Loopback, (int)ip.Port());
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = LDKSTcpDescriptor.Inbound(_peerManager, await listener.AcceptTcpClientAsync(cancellationToken),
                _logger, _descriptors);
            if (result is not null)
            {
                _descriptors.TryAdd(result.Id,result);
            }
        }
    }

    public EndPoint Endpoint { get; set; }
    
     public async Task<LDKSTcpDescriptor?> ConnectAsync(BTCPayServer.Lightning.NodeInfo nodeInfo,
         CancellationToken cancellationToken = default)
     {
         var remote = IPEndPoint.Parse(nodeInfo.Host + ":" + nodeInfo.Port);
         return await ConnectAsync(nodeInfo.NodeId, remote, cancellationToken);
     }
    public async Task<LDKSTcpDescriptor?> ConnectAsync(PubKey theirNodeId, EndPoint remote,
        CancellationToken cancellationToken = default)
    {
        var client = new TcpClient();
        await client.ConnectAsync(remote.IPEndPoint(), cancellationToken);
        var result =  LDKSTcpDescriptor.Outbound(_peerManager, client, _logger, theirNodeId, _descriptors);
        if (result is not null)
        {
            _descriptors.TryAdd(result.Id,result);
        }
        return result;
    }
    
    public List<NodeInfo> GetPeerNodeIds()
    {
        return _peerManager.get_peer_node_ids().Select(zz =>
        {
            var pubKey = new PubKey(zz.get_a());
            var addr = zz.get_b() is Option_SocketAddressZ.Option_SocketAddressZ_Some x ? x.some.to_str() : null;
            EndPointParser.TryParse(addr, 9735, out var endpoint);
            return new NodeInfo(pubKey, endpoint.Host(), endpoint.Port().Value);
        }).ToList();
    }

}

public class LDKSTcpDescriptor : SocketDescriptorInterface
{
    private readonly PeerManager _peerManager;
    private readonly TcpClient _tcpClient;
    private readonly ILogger _logger;
    private readonly Action<string> _onDisconnect;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cts;
    private bool _pausedWrite;

    public SocketDescriptor SocketDescriptor { get; set; }

    public string Id { get; set; }

    private Channel<byte[]> readData = Channel.CreateUnbounded<byte[]>();

    readonly SemaphoreSlim _readSemaphore = new(1, 1);

    public static LDKSTcpDescriptor? Inbound(PeerManager peerManager, TcpClient tcpClient, ILogger logger, ConcurrentDictionary<string, LDKSTcpDescriptor> descriptors)
    {
        var descriptor = new LDKSTcpDescriptor(peerManager, tcpClient, logger,s => descriptors.TryRemove(s, out _));
        var result = peerManager.new_inbound_connection(SocketDescriptor.new_impl(descriptor),
            tcpClient.Client.GetSocketAddress());
        if (result.is_ok()) return descriptor;

        descriptor.disconnect_socket();
        return null;
    }

    public static LDKSTcpDescriptor? Outbound(PeerManager peerManager, TcpClient tcpClient, ILogger logger,
        PubKey pubKey, ConcurrentDictionary<string, LDKSTcpDescriptor> descriptors)
    {
        var descriptor = new LDKSTcpDescriptor(peerManager, tcpClient, logger, s => descriptors.TryRemove(s, out _));
        var result = peerManager.new_outbound_connection(pubKey.ToBytes(), SocketDescriptor.new_impl(descriptor),
            tcpClient.Client.GetSocketAddress());
        if (result is Result_CVec_u8ZPeerHandleErrorZ.Result_CVec_u8ZPeerHandleErrorZ_OK ok)
        {
            descriptor.send_data(ok.res, false);
        }

        if (result.is_ok()) return descriptor;
        descriptor.disconnect_socket();
        return null;
    }

    private LDKSTcpDescriptor(PeerManager peerManager, TcpClient tcpClient, ILogger logger, Action<string> onDisconnect)
    {
        _peerManager = peerManager;
        _tcpClient = tcpClient;
        _logger = logger;
        _onDisconnect = onDisconnect;
        _stream = tcpClient.GetStream();

        SocketDescriptor = SocketDescriptor.new_impl(this);

        _cts = new CancellationTokenSource();
        _ = ListenForIncomingData(_cts.Token);
        _ = ReadEvents(_cts.Token);
    }


    private async Task ListenForIncomingData(CancellationToken cancellationToken)
    {
        var bufSz = 1024 * 16;
        var buffer = new byte[bufSz];
        while (_tcpClient.Connected && !_cts.IsCancellationRequested)
        {
            var read = await _stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                continue;
            }

            var data = buffer[..read];
            readData.Writer.TryWrite(data);
        }
    }

    private async Task ReadEvents(CancellationToken cancellationToken)
    {
        var first = true;
        await _readSemaphore.WaitAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested && await readData.Reader.WaitToReadAsync(cancellationToken))
        {
            if(first)
            {
                first = false;
               
            }
            else
            {
                await _readSemaphore.WaitAsync(cancellationToken);
            }
            if(readData.Reader.TryRead(out var data))
            {
                
                var pause = _peerManager.read_event(SocketDescriptor, data) is Result_boolPeerHandleErrorZ.Result_boolPeerHandleErrorZ_OK
                {
                    res: true
                };
                _peerManager.process_events();
                if (pause)
                {
                    // _logger.LogInformation("Pausing as per instruction from read_event");
                    // continue;
                }

                Resume();
            }
        }
        
    }
    private void Resume()
    { try
        {
                
            _readSemaphore.Release();
                
            _logger.LogInformation("resuming read");
        }
        catch (Exception e)
        {
        }
    }

    public long send_data(byte[] data, bool resume_read)
    {
        try
        {
            _logger.LogInformation($"sending {data.Length}bytes of data to peer");
            var result = _tcpClient.Client.Send(data);
            
            
            _logger.LogInformation($"Sent {result}bytes of data to peer");
            if (resume_read)
            {
                Resume();
            }

            return result;
        }
        catch (Exception e)
        {
            disconnect_socket();
            return 0;
        }

    }

    public void disconnect_socket()
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        _cts.Cancel();
        _stream.Dispose();
        _tcpClient.Dispose();
        _peerManager.socket_disconnected(SocketDescriptor);
        
        _onDisconnect(Id);
    }

    public bool eq(SocketDescriptor other_arg)
    {
        return hash() == other_arg.hash();
    }

    public long hash()
    {
        return Id.GetHashCode();
    }
}

//
// public class LDKPeerHandler : IScopedHostedService
// {
//     private readonly PeerManager _peerManager;
//     private readonly ILogger<LDKPeerHandler> _logger;
//     private CancellationTokenSource? _cts;
//     public EndPoint? Endpoint { get; private set; }
//
//     public LDKPeerHandler(PeerManager peerManager, LDKWalletLoggerFactory logger)
//     {
//         _peerManager = peerManager;
//         _logger = logger.CreateLogger<LDKPeerHandler>();
//     }
//
//     public async Task<Task?> ConnectAsync(BTCPayServer.Lightning.NodeInfo nodeInfo,
//         CancellationToken cancellationToken = default)
//     {
//         var remote = IPEndPoint.Parse(nodeInfo.Host + ":" + nodeInfo.Port);
//         return await ConnectAsync(nodeInfo.NodeId, remote, cancellationToken);
//     }
//
//     public async Task<Task?> ConnectAsync(PubKey theirNodeId, EndPoint remote,
//         CancellationToken cancellationToken = default)
//     {
//         var connection = await ConnectCoreAsync(theirNodeId, remote, cancellationToken);
//         if (connection is null)
//             return null;
//
//         return Task.Run(async () =>
//         {
//             while (connection.Value.Item2.Connected && !cancellationToken.IsCancellationRequested)
//             {
//                 await Task.Delay(1000, cancellationToken);
//             }
//
//             connection.Value.Item3.disconnect_socket();
//         }, cancellationToken);
//     }
//
//     private async Task<(LDKSocketDescriptor, Socket, SocketDescriptor )?> ConnectCoreAsync(PubKey theirNodeId,
//         EndPoint remote, CancellationToken cancellationToken)
//     {
//         var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//         SocketDescriptor? descriptor = null;
//
//         await socket.ConnectAsync(remote, cancellationToken);
//         if (!socket.Connected)
//         {
//             return null;
//         }
//
//         _logger.LogInformation("Establishing connection to {SocketRemoteEndPoint}", socket.RemoteEndPoint);
//
//         var remoteAddress = socket.GetSocketAddress();
//         var ldkSocket = new LDKSocketDescriptor(socket, Guid.NewGuid().ToString(), _logger);
//         descriptor = SocketDescriptor.new_impl(ldkSocket);
//         _ = InboundConnectionReader(cancellationToken, socket, descriptor);
//         var result = _peerManager.new_outbound_connection(theirNodeId.ToBytes(), descriptor, remoteAddress);
//         if (!result.is_ok())
//         {
//             _logger.LogWarning("Connecting to {SocketRemoteEndPoint} failed!", socket.RemoteEndPoint);
//             descriptor.disconnect_socket();
//             return null;
//         }
//
//         var initialBytes = ((Result_CVec_u8ZPeerHandleErrorZ.Result_CVec_u8ZPeerHandleErrorZ_OK) result).res;
//         try
//         {
//             if (initialBytes.Length != descriptor.send_data(initialBytes, true))
//             {
//                 _logger.LogWarning("Disconnecting from {SocketRemoteEndPoint}, because send_data byte length mismatch",
//                     socket.RemoteEndPoint);
//                 descriptor.disconnect_socket();
//                 return null;
//             }
//         }
//         catch (Exception)
//         {
//             _logger.LogWarning("Disconnecting from {SocketRemoteEndPoint}, because send_data failed",
//                 socket.RemoteEndPoint);
//             descriptor.disconnect_socket();
//             return null;
//         }
//
//         _logger.LogInformation("Connected to {SocketRemoteEndPoint}", socket.RemoteEndPoint);
//         return (ldkSocket, socket, descriptor);
//     }
//
//     public async Task StartAsync(CancellationToken cancellationToken)
//     {
//         _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
//         _ = ListenLoop(cancellationToken, _cts.Token);
//         _ = PeriodicTicker(_cts.Token, 10000, () => _peerManager.timer_tick_occurred());
//         _ = PeriodicTicker(_cts.Token, 1000, () => _peerManager.process_events());
//     }
//
//     private async Task PeriodicTicker(CancellationToken cancellationToken, int ms, Action action)
//     {
//         while (!cancellationToken.IsCancellationRequested)
//         {
//             await Task.Delay(ms, cancellationToken);
//             action.Invoke();
//         }
//     }
//
//     private async Task ListenLoop(CancellationToken startCancellationToken, CancellationToken cancellationToken)
//     {
//         _logger.LogInformation("Starting LDKPeerHandler");
//         var ip = new IPEndPoint(IPAddress.Any, 0);
//         using var listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
//
//         listener.Bind(ip);
//         listener.Listen(100);
//         Endpoint = new IPEndPoint(IPAddress.Loopback,
//             int.Parse(listener.LocalEndPoint.ToEndpointString().Split(":").Last()));
//         _logger.LogInformation("Started listening on {Endpoint}", Endpoint);
//         while (!cancellationToken.IsCancellationRequested)
//         {
//             var socket = await listener.AcceptAsync(startCancellationToken);
//
//             _logger.LogInformation("Incoming connection from {SocketRemoteEndPoint}", socket.RemoteEndPoint);
//             var remoteAddress = socket.GetSocketAddress();
//             var descriptor = new LDKSocketDescriptor(socket, Guid.NewGuid().ToString(), _logger);
//             var sd = SocketDescriptor.new_impl(descriptor);
//
//             if (!_peerManager.new_inbound_connection(sd, remoteAddress).is_ok())
//             {
//                 _logger.LogWarning("Connecting to {SocketRemoteEndPoint} failed!", socket.RemoteEndPoint);
//                 sd.disconnect_socket();
//             }
//             else
//             {
//                 _logger.LogInformation("Connected to {SocketRemoteEndPoint}", socket.RemoteEndPoint);
//                 _ = InboundConnectionReader(cancellationToken, socket, sd);
//             }
//         }
//
//         _logger.LogInformation("Stopped listening on {Endpoint}", Endpoint);
//     }
//
//     private async Task InboundConnectionReader(CancellationToken cancellationToken, Socket socket, SocketDescriptor sd)
//     {
//         // await using var networkStream = new NetworkStream(socket);
//         var bufSz = 1024 * 16;
//         var buffer = new byte[bufSz];
//         while (!cancellationToken.IsCancellationRequested && socket.Connected)
//         {
//             socket.
//                 var read = await socket.ReceiveAsync(buffer, cancellationToken);
//             if (read == 0)
//             {
//                 _logger.LogWarning("Disconnecting from {SocketRemoteEndPoint}, because read returned 0",
//                     socket.RemoteEndPoint);
//                 break;
//             }
//
//             _logger.LogInformation("Read {Read} bytes from {SocketRemoteEndPoint}", read, socket.RemoteEndPoint);
//             var data = buffer[..read];
//             if (_peerManager.read_event(sd, data).is_ok()) continue;
//             _logger.LogWarning("Disconnecting from {SocketRemoteEndPoint}, because read_event failed",
//                 socket.RemoteEndPoint);
//             sd.disconnect_socket();
//         }
//     }
//
//     public IEnumerable<NodeInfo> GetPeerNodeIds()
//     {
//         return _peerManager.get_peer_node_ids().Select(zz =>
//         {
//             var pubKey = new PubKey(zz.get_a());
//             var addr = zz.get_b() is Option_SocketAddressZ.Option_SocketAddressZ_Some x ? x.some.to_str() : null;
//             EndPointParser.TryParse(addr, 9735, out var endpoint);
//             return new NodeInfo(pubKey, endpoint.Host(), endpoint.Port().Value);
//         });
//     }
//
//     public async Task StopAsync(CancellationToken cancellationToken)
//     {
//         if (_cts is not null)
//             await _cts.CancelAsync();
//
//         _logger.LogInformation("Stopping, disconnecting all peers");
//         _peerManager.disconnect_all_peers();
//     }
// }