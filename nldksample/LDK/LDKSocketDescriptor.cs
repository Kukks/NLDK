using System.Net.Sockets;
using org.ldk.structs;

namespace nldksample.LDK;

public class LDKSocketDescriptor: SocketDescriptorInterface
{
    private readonly Socket _socket;

    public LDKSocketDescriptor(Socket socket)
    {
        _socket = socket;
    }

    public long send_data(byte[] data, bool resume_read)
    {
        return  _socket.Send(data);
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
        return _socket.Handle.ToInt64();
    }
}