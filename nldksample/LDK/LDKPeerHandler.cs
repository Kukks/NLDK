using System.Net;
using System.Net.Sockets;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKPeerHandler : IScopedHostedService
{
    private readonly PeerManager _peerManager;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cts;

    public LDKPeerHandler(PeerManager peerManager, LDKWalletLoggerFactory logger)
    {
        _peerManager = peerManager;
        _logger = logger.CreateLogger<LDKPeerHandler>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Listen();
    }


    private async Task Listen()
    {
        try
        {
            _logger.LogInformation("Starting LDKPeerHandler");
            var ip = new IPEndPoint(IPAddress.Any, 9735);
            using Socket listener = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(ip);
            listener.Listen(100);

            _logger.LogInformation("LDKPeerHandler started");

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptAsync().ConfigureAwait(false);
                    // Handle the client connection in a separate task to allow the listener to continue accepting new connections
                    _ = Task.Run(() => HandleClientAsync(client, _cts.Token));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client connection.");
                    if (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error starting LDKPeerHandler");
        }
    }

    private async Task HandleClientAsync(Socket client, CancellationToken cancellationToken)
    {
        SocketDescriptor descriptor = null;
        try
        {
            descriptor = SocketDescriptor.new_impl(new LDKSocketDescriptor(client));
            var remoteSocketAddress = client.RemoteEndPoint?.Endpoint();
            var result = _peerManager.new_inbound_connection(descriptor,
                remoteSocketAddress is null
                    ? Option_SocketAddressZ.none()
                    : Option_SocketAddressZ.some(remoteSocketAddress));
            if (result is Result_NonePeerHandleErrorZ.Result_NonePeerHandleErrorZ_Err)
            {
                return;
            }

            await using NetworkStream networkStream = new NetworkStream(client, true);

            int bufSz = 1024 * 16;
            byte[] buffer = new byte[bufSz];
            // Assuming PeerManager has methods for handling incoming data and possibly sending responses
            while (!cancellationToken.IsCancellationRequested)
            {
                Array.Clear(buffer, 0, buffer.Length);

                if (_peerManager.write_buffer_space_avail(descriptor) is Result_NonePeerHandleErrorZ
                        .Result_NonePeerHandleErrorZ_Err)
                {
                    return;
                }

                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false);


                if (bytesRead <= 0)
                {
                    // The client closed the connection
                    break;
                }

                var read_result_pointer = _peerManager.read_event(descriptor, buffer.AsSpan(0, bytesRead).ToArray());
                if (!read_result_pointer.is_ok())
                {
                    break;
                }

                _peerManager.process_events();
            }
        }
        catch (IOException ex) when (ex.InnerException is SocketException)
        {
            // Handle specific socket exceptions if needed
            _logger.LogError(ex, "Socket exception occurred.");
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            _logger.LogInformation("Operation canceled while handling client connection.");
        }
        catch (Exception ex)
        {
            // Handle other exceptions
            _logger.LogError(ex, "Error occurred while handling client connection.");
        }
        finally
        {
            descriptor?.disconnect_socket();
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();
    }
}