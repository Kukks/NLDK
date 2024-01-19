using System.Collections.Concurrent;
using System.Net.Sockets;
using NBitcoin;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKTcpDescriptor : SocketDescriptorInterface
{
    private readonly PeerManager _peerManager;
    private readonly TcpClient _tcpClient;
    private readonly ILogger _logger;
    private readonly Action<string> _onDisconnect;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cts;

    public SocketDescriptor SocketDescriptor { get; set; }

    public string Id { get; set; }
    readonly SemaphoreSlim _readSemaphore = new(1, 1);

    public static LDKTcpDescriptor? Inbound(PeerManager peerManager, TcpClient tcpClient, ILogger logger, ConcurrentDictionary<string, LDKTcpDescriptor> descriptors)
    {
        var descriptor = new LDKTcpDescriptor(peerManager, tcpClient, logger,s => descriptors.TryRemove(s, out _));
        var result = peerManager.new_inbound_connection(SocketDescriptor.new_impl(descriptor),
            tcpClient.Client.GetSocketAddress());
        if (result.is_ok())
        {
            logger.LogInformation("New inbound connection accepted");
            return descriptor;
        }

        descriptor.disconnect_socket();
        return null;
    }

    public static LDKTcpDescriptor? Outbound(PeerManager peerManager, TcpClient tcpClient, ILogger logger,
        PubKey pubKey, ConcurrentDictionary<string, LDKTcpDescriptor> descriptors)
    {
        var descriptor = new LDKTcpDescriptor(peerManager, tcpClient, logger, s => descriptors.TryRemove(s, out _));
        var result = peerManager.new_outbound_connection(pubKey.ToBytes(), SocketDescriptor.new_impl(descriptor),
            tcpClient.Client.GetSocketAddress());
        if (result is Result_CVec_u8ZPeerHandleErrorZ.Result_CVec_u8ZPeerHandleErrorZ_OK ok)
        {
            descriptor.send_data(ok.res, false);
        }

        if (result.is_ok())
        {
            logger.LogInformation("New outbounf connection accepted");
            return descriptor;
        }
        descriptor.disconnect_socket();
        return null;
    }

    private LDKTcpDescriptor(PeerManager peerManager, TcpClient tcpClient, ILogger logger, Action<string> onDisconnect)
    {
        _peerManager = peerManager;
        _tcpClient = tcpClient;
        _logger = logger;
        _onDisconnect = onDisconnect;
        _stream = tcpClient.GetStream();
        Id = Guid.NewGuid().ToString();
        SocketDescriptor = SocketDescriptor.new_impl(this);

        _cts = new CancellationTokenSource();
        _ = CheckConnection(_cts.Token);
        _ = ReadEvents(_cts.Token);
        
    }

    private async Task CheckConnection(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);
            }

            catch(OperationCanceledException)
            {
            }
        }
        disconnect_socket();
    }
    private async Task ReadEvents(CancellationToken cancellationToken)
    {
        var first = true;
        var bufSz = 1024 * 16;
        var buffer = new byte[bufSz];
        await _readSemaphore.WaitAsync(cancellationToken);
        while (_tcpClient.Connected && !_cts.IsCancellationRequested)
        {
            if(first)
            {
                first = false;
               
            }
            else
            {
                await _readSemaphore.WaitAsync(cancellationToken);
            }
            var read = await _stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                continue;
            }

            var data = buffer[..read];
                
            var pause = _peerManager.read_event(SocketDescriptor, data) is Result_boolPeerHandleErrorZ.Result_boolPeerHandleErrorZ_OK
            {
                res: true
            };
            _peerManager.process_events();
            if (pause)
            {
                _logger.LogInformation("Pausing as per instruction from read_event");
                continue;
            }

            Resume();
            
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

        _logger.LogInformation("Disconnecting socket");
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