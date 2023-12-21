using System.Net;
using System.Net.Sockets;
using NBitcoin;
using nldksample.LDK;
using org.ldk.structs;

public class LDKPeerHandler : IScopedHostedService
{
    private readonly PeerManager _peerManager;
    private readonly ILogger<LDKPeerHandler> _logger;
    private CancellationTokenSource? _cts;
    public EndPoint? Endpoint { get; private set; }

    public LDKPeerHandler(PeerManager peerManager, LDKWalletLoggerFactory logger)
    {
        _peerManager = peerManager;
        _logger = logger.CreateLogger<LDKPeerHandler>();
    }

    public async Task ConnectAsync(BTCPayServer.Lightning.NodeInfo nodeInfo, CancellationToken cancellationToken)
    {
        await ConnectAsync(nodeInfo.NodeId, IPEndPoint.Parse(nodeInfo.Host + ":" + nodeInfo.Port),
            cancellationToken);
    }

    public async Task ConnectAsync(PubKey theirNodeId, EndPoint remote, CancellationToken cancellationToken)
    {
        using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        SocketDescriptor? descriptor = null;
        try
        {
            socket.Blocking = false;

            await socket.ConnectAsync(remote, cancellationToken);
            if (!socket.Connected)
            {
                return;
            }

            var ldkSocket = new LDKSocketDescriptor(socket);
            descriptor = SocketDescriptor.new_impl(ldkSocket);
            var result =
                _peerManager.new_outbound_connection(theirNodeId.ToBytes(), descriptor, ldkSocket.RemoteEndPoint());
            if (!result.is_ok())
            {
                return;
            }

            var initialBytes = ((Result_CVec_u8ZPeerHandleErrorZ.Result_CVec_u8ZPeerHandleErrorZ_OK) result).res;

            if (initialBytes.Length != descriptor.send_data(initialBytes, true))
            {
                return;
            }

            while (socket.Connected && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }
        }
        finally
        {
            descriptor?.disconnect_socket();
        }
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ListenLoop(cancellationToken, _cts.Token);
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

    private async Task ListenLoop(CancellationToken startCancellationToken, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting LDKPeerHandler");
        var ip = new IPEndPoint(IPAddress.Any, 0);
        using var listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(ip);
        listener.Listen(100);
        Endpoint = new IPEndPoint(IPAddress.Loopback,
            int.Parse(listener.LocalEndPoint.ToEndpointString().Split(":").Last()));
        _logger.LogInformation($"Started listening on {Endpoint}");
        while (!cancellationToken.IsCancellationRequested)
        {
            var socket = await listener.AcceptAsync(startCancellationToken);
            _logger.LogInformation($"Accepted connection from {socket.RemoteEndPoint}");
            var descriptor = new LDKSocketDescriptor(socket);
            var sd = SocketDescriptor.new_impl(descriptor);
            if (!_peerManager.new_inbound_connection(sd, descriptor.RemoteEndPoint()).is_ok())
            {
                sd.disconnect_socket();
            }
            else
            {
                _ = InboundConnectionReader(cancellationToken, socket, sd);
            }
        }
    }

    private async Task InboundConnectionReader(CancellationToken cancellationToken, Socket socket, SocketDescriptor sd)
    {
        await using NetworkStream networkStream = new NetworkStream(socket);
        var bufSz = 1024 * 16;
        var buffer = new byte[bufSz];
        while (!cancellationToken.IsCancellationRequested && socket.Connected)
        {
            var read = await networkStream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var data = buffer[..read];
            if (!_peerManager.read_event(sd, data).is_ok())
            {
                sd.disconnect_socket();
            }
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if(_cts is not null)
            await _cts.CancelAsync();
        
        _peerManager.disconnect_all_peers();
    }
}