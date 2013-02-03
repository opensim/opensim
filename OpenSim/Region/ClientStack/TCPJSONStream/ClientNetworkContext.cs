using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenSim.Region.ClientStack.TCPJSONStream
{
    public class ClientNetworkContext
    {
        private Socket _socket;
        private string  _remoteAddress;
        private string _remotePort;
        private WebSocketConnectionStage _wsConnectionStatus = WebSocketConnectionStage.Accept;
        private int _bytesLeft;
        private NetworkStream _stream;
        private byte[] _buffer;
        public event EventHandler<DisconnectedEventArgs> Disconnected = delegate { };

        public ClientNetworkContext(IPEndPoint endPoint, int port, Stream stream, int buffersize, Socket sock)
        {
            _socket = sock;
            _remoteAddress = endPoint.Address.ToString();
            _remotePort = port.ToString();
            _stream = stream as NetworkStream;
            _buffer = new byte[buffersize];
            

        }

        public void BeginRead()
        {
            _wsConnectionStatus = WebSocketConnectionStage.Http;
            try
            {
                _stream.BeginRead(_buffer, 0, _buffer.Length, OnReceive, _wsConnectionStatus);
            }
            catch (IOException err)
            {
                //m_log.Debug(err.ToString());
            }
        }

        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                int bytesRead = _stream.EndRead(ar);
                if (bytesRead == 0)
                {

                    Disconnected(this, new DisconnectedEventArgs(SocketError.ConnectionReset));
                    return;
                }

            }
        }
        /// <summary>
        /// send a whole buffer
        /// </summary>
        /// <param name="buffer">buffer to send</param>
        /// <exception cref="ArgumentNullException"></exception>
        public void Send(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");
            Send(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Send data using the stream
        /// </summary>
        /// <param name="buffer">Contains data to send</param>
        /// <param name="offset">Start position in buffer</param>
        /// <param name="size">number of bytes to send</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void Send(byte[] buffer, int offset, int size)
        {

            if (offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", offset, "offset + size is beyond end of buffer.");

            if (_stream != null && _stream.CanWrite)
            {
                try
                {
                    _stream.Write(buffer, offset, size);
                }
                catch (IOException)
                {

                }
            }

        }
        private void Reset()
        {
            if (_stream == null)
                return;
            _stream.Dispose();
            _stream = null;
            if (_socket == null)
                return;
            if (_socket.Connected)
                _socket.Disconnect(true);
            _socket = null;
        }
    }

    public enum WebSocketConnectionStage
    {
        Reuse,
        Accept,
        Http,
        WebSocket, 
        Closed
    }

    public enum FrameOpCodesRFC6455
    {
        Continue = 0x0,
        Text = 0x1,
        Binary = 0x2,
        Close = 0x8,
        Ping = 0x9,
        Pong = 0xA
    }

    public enum DataState
    {
        Empty = 0,
        Waiting = 1,
        Receiving = 2, 
        Complete  = 3,
        Closed = 4,
        Ping = 5,
        Pong = 6
    }
    

}
