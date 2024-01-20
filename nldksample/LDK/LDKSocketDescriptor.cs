// using System.Net.Sockets;
// using org.ldk.structs;
//
// public class LDKSocketDescriptor : SocketDescriptorInterface
// {
//     private readonly Socket _socket;
//     private readonly string _id;
//     private readonly ILogger _logger;
//
//     public LDKSocketDescriptor(Socket socket, string id, ILogger logger)
//     {
//         _socket = socket;
//         _id = id;
//         _logger = logger;
//     }
//     public long send_data(byte[] data, bool resumeRead)
//     {
//         try
//         {
//
//             var result = _socket.Send(data);
//             return result;
//         }
//         catch (Exception e)
//         {
//             disconnect_socket();
//             return 0;
//         }
//     }
//
//     public void disconnect_socket()
//     {
//         if(!_socket.Connected)
//             return;
//         _logger.LogDebug($"Disconnecting with {_socket.RemoteEndPoint}");
//         _socket.Disconnect(true);
//     }
//
//     public bool eq(SocketDescriptor otherArg)
//     {
//         return hash() == otherArg.hash();
//     }
//
//     public long hash()
//     {
//         return _id.GetHashCode();
//     }
// }