using System.Net.Sockets;
using org.ldk.structs;

public class LDKSocketDescriptor : SocketDescriptorInterface
{
    private readonly Socket _socket;
    private readonly string _id;

    public LDKSocketDescriptor(Socket socket, string id)
    {
        _socket = socket;
        _id = id;
    }
    public long send_data(byte[] data, bool resumeRead)
    {
        return _socket.Send(data);
    }

    public void disconnect_socket()
    {
        _socket.Disconnect(true);
    }

    public bool eq(SocketDescriptor otherArg)
    {
        return hash() == otherArg.hash();
    }

    public long hash()
    {
        return _id.GetHashCode();
    }
}