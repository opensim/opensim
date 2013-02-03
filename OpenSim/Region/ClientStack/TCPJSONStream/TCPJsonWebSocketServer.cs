using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using log4net;

namespace OpenSim.Region.ClientStack.TCPJSONStream
{
    public delegate void ExceptionHandler(object source, Exception exception);

    public class TCPJsonWebSocketServer
    {
        private readonly IPAddress _address;
        private readonly int _port;
        private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private TcpListener _listener;
        private int _pendingAccepts;
        private bool _shutdown;
        private int _backlogAcceptQueueLength = 5;
        private Scene m_scene;
        private Location m_location;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event EventHandler<ClientAcceptedEventArgs> Accepted = delegate { };


        public TCPJsonWebSocketServer(IPAddress _listenIP, ref uint port, int proxyPortOffsetParm,
                                      bool allow_alternate_port, IConfigSource configSource,
                                      AgentCircuitManager authenticateClass)
        {
            _address = _listenIP;
            _port = (int)port;  //Why is a uint passed in?
        }
        public void Stop()
        {
            _shutdown = true;
            _listener.Stop();
            if (!_shutdownEvent.WaitOne())
                m_log.Error("[WEBSOCKETSERVER]: Failed to shutdown listener properly.");
            _listener = null;
        }

        public bool HandlesRegion(Location x)
        {
            return x == m_location;
        }

        public void AddScene(IScene scene)
        {
            if (m_scene != null)
            {
                m_log.Debug("[WEBSOCKETSERVER]: AddScene() called but I already have a scene.");
                return;
            }
            if (!(scene is Scene))
            {
                m_log.Error("[WEBSOCKETSERVER]: AddScene() called with an unrecognized scene type " + scene.GetType());
                return;
            }

            m_scene = (Scene)scene;
            m_location = new Location(m_scene.RegionInfo.RegionHandle);
        }

        public void Start()
        {
            _listener = new TcpListener(_address, _port);
            _listener.Start(_backlogAcceptQueueLength);
            Interlocked.Increment(ref _pendingAccepts);
            _listener.BeginAcceptSocket(OnAccept, null);
        }

        private void OnAccept(IAsyncResult ar)
        {
            bool beginAcceptCalled = false;
            try
            {
                int count = Interlocked.Decrement(ref _pendingAccepts);
                if (_shutdown)
                {
                    if (count == 0)
                        _shutdownEvent.Set();
                    return;
                }
                Interlocked.Increment(ref _pendingAccepts);
                _listener.BeginAcceptSocket(OnAccept, null);
                beginAcceptCalled = true;
                Socket socket = _listener.EndAcceptSocket(ar);
                if (!OnAcceptingSocket(socket))
                {
                    socket.Disconnect(true);
                    return;
                }
                ClientNetworkContext context = new ClientNetworkContext((IPEndPoint) socket.RemoteEndPoint, _port,
                                                                        new NetworkStream(socket), 16384, socket);
                HttpRequestParser parser;
                context.BeginRead();

            }
            catch (Exception err)
            {
                if (ExceptionThrown == null)
#if DEBUG
                    throw;
#else
                   _logWriter.Write(this, LogPrio.Fatal, err.Message);
                // we can't really do anything but close the connection
#endif
                if (ExceptionThrown != null)
                    ExceptionThrown(this, err);

                if (!beginAcceptCalled)
                    RetryBeginAccept();

            }
        }

        private void RetryBeginAccept()
        {
            try
            {
                
                _listener.BeginAcceptSocket(OnAccept, null);
            }
            catch (Exception err)
            {
                
                if (ExceptionThrown == null)
#if DEBUG
                    throw;
#else
                // we can't really do anything but close the connection
#endif
                if (ExceptionThrown != null)
                    ExceptionThrown(this, err);
            }
        }

        private bool OnAcceptingSocket(Socket sock)
        {
            ClientAcceptedEventArgs args = new ClientAcceptedEventArgs(sock);
            Accepted(this, args);
            return !args.Revoked;
        }
        /// <summary>
        /// Catch exceptions not handled by the listener.
        /// </summary>
        /// <remarks>
        /// Exceptions will be thrown during debug mode if this event is not used,
        /// exceptions will be printed to console and suppressed during release mode.
        /// </remarks>
        public event ExceptionHandler ExceptionThrown = delegate { };

        

    }
}
