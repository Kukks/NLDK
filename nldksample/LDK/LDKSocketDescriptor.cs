using System.Net.Sockets;
using org.ldk.structs;

public class LDKSocketDescriptor : SocketDescriptorInterface
{
    private readonly Socket _socket;
    public Guid Id { get; init; }

    public LDKSocketDescriptor(Socket socket)
    {
        _socket = socket;
        Id = Guid.NewGuid();
    }

    public SocketAddress? GetSocketAddress()
    {
        if (_socket.RemoteEndPoint is null)
        {
            return null;
        }

        var result = SocketAddress.from_str(_socket.RemoteEndPoint.ToString()!);
        return result is Result_SocketAddressSocketAddressParseErrorZ.
            Result_SocketAddressSocketAddressParseErrorZ_OK ok
            ? ok.res
            : null;
    }

    public Option_SocketAddressZ RemoteEndPoint()
    {
        if (_socket.RemoteEndPoint is null)
        {
            return Option_SocketAddressZ.none();
        }

        var socketAddress = GetSocketAddress();
        if (socketAddress is null)
        {
            return Option_SocketAddressZ.none();
        }

        return Option_SocketAddressZ.some(socketAddress);
    }

    public long send_data(byte[] data, bool resume_read)
    {
        return _socket.Send(data);
    }

    public void disconnect_socket()
    {
        _socket.Disconnect(true);
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

namespace nldksample.LDK
{
}