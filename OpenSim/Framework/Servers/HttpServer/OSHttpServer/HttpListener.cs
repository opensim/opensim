using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace OSHttpServer
{
    public class OSHttpListener: IDisposable
    {
        private readonly IPAddress m_address;
        private readonly X509Certificate m_certificate;
        private readonly IHttpContextFactory m_contextFactory;
        private readonly int m_port;
        private readonly ManualResetEvent m_shutdownEvent = new ManualResetEvent(false);
        private readonly SslProtocols m_sslProtocol = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Ssl3 | SslProtocols.Ssl2;
        private TcpListener m_listener;
        private ILogWriter m_logWriter = NullLogWriter.Instance;
        private int m_pendingAccepts;
        private bool m_shutdown;
        protected RemoteCertificateValidationCallback m_clientCertValCallback = null;

        public event EventHandler<ClientAcceptedEventArgs> Accepted;
        public event ExceptionHandler ExceptionThrown;
        public event EventHandler<RequestEventArgs> RequestReceived;

        /// <summary>
        /// Listen for regular HTTP connections
        /// </summary>
        /// <param name="address">IP Address to accept connections on</param>
        /// <param name="port">TCP Port to listen on, default HTTP port is 80.</param>
        /// <param name="factory">Factory used to create <see cref="IHttpClientContext"/>es.</param>
        /// <exception cref="ArgumentNullException"><c>address</c> is null.</exception>
        /// <exception cref="ArgumentException">Port must be a positive number.</exception>
        protected OSHttpListener(IPAddress address, int port, IHttpContextFactory factory)
        {
            m_address = address;
            m_port = port;
            m_contextFactory = factory;
            m_contextFactory.RequestReceived += OnRequestReceived;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OSHttpListener"/> class.
        /// </summary>
        /// <param name="address">IP Address to accept connections on</param>
        /// <param name="port">TCP Port to listen on, default HTTPS port is 443</param>
        /// <param name="factory">Factory used to create <see cref="IHttpClientContext"/>es.</param>
        /// <param name="certificate">Certificate to use</param>
        /// <param name="protocol">which HTTPS protocol to use, default is TLS.</param>
        protected OSHttpListener(IPAddress address, int port, IHttpContextFactory factory, X509Certificate certificate,
                                   SslProtocols protocol)
            : this(address, port, factory, certificate)
        {
            m_sslProtocol = protocol;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OSHttpListener"/> class.
        /// </summary>
        /// <param name="address">IP Address to accept connections on</param>
        /// <param name="port">TCP Port to listen on, default HTTPS port is 443</param>
        /// <param name="factory">Factory used to create <see cref="IHttpClientContext"/>es.</param>
        /// <param name="certificate">Certificate to use</param>
        protected OSHttpListener(IPAddress address, int port, IHttpContextFactory factory, X509Certificate certificate)
            : this(address, port, factory)
        {
            m_certificate = certificate;
        }

        public static OSHttpListener Create(IPAddress address, int port)
        {
            RequestParserFactory requestFactory = new RequestParserFactory();
            HttpContextFactory factory = new HttpContextFactory(NullLogWriter.Instance, requestFactory);
            return new OSHttpListener(address, port, factory);
        }

        public static OSHttpListener Create(IPAddress address, int port, X509Certificate certificate)
        {
            RequestParserFactory requestFactory = new RequestParserFactory();
            HttpContextFactory factory = new HttpContextFactory(NullLogWriter.Instance, requestFactory);
            return new OSHttpListener(address, port, factory, certificate);
        }

        public static OSHttpListener Create(IPAddress address, int port, X509Certificate certificate, SslProtocols protocol)
        {
            RequestParserFactory requestFactory = new RequestParserFactory();
            HttpContextFactory factory = new HttpContextFactory(NullLogWriter.Instance, requestFactory);
            return new OSHttpListener(address, port, factory, certificate, protocol);
        }

        private void OnRequestReceived(object sender, RequestEventArgs e)
        {
            RequestReceived?.Invoke(sender, e);
        }


        public RemoteCertificateValidationCallback CertificateValidationCallback
        {
            set { m_clientCertValCallback = value; }
        }

        /// <summary>
        /// Gives you a change to receive log entries for all internals of the HTTP library.
        /// </summary>
        /// <remarks>
        /// You may not switch log writer after starting the listener.
        /// </remarks>
        public ILogWriter LogWriter
        {
            get { return m_logWriter; }
            set
            {
                m_logWriter = value ?? NullLogWriter.Instance;
                if (m_certificate != null)
                    m_logWriter.Write(this, LogPrio.Info,
                                     "HTTPS(" + m_sslProtocol + ") listening on " + m_address + ":" + m_port);
                else
                    m_logWriter.Write(this, LogPrio.Info, "HTTP listening on " + m_address + ":" + m_port);
            }
        }

        /// <summary>
        /// True if we should turn on trace logs.
        /// </summary>
        public bool UseTraceLogs { get; set; }


        /// <exception cref="Exception"><c>Exception</c>.</exception>
        private void OnAccept(IAsyncResult ar)
        {
            bool beginAcceptCalled = false;
            try
            {
                int count = Interlocked.Decrement(ref m_pendingAccepts);
                if (m_shutdown)
                {
                    if (count == 0)
                        m_shutdownEvent.Set();
                    return;
                }

                Interlocked.Increment(ref m_pendingAccepts);
                m_listener.BeginAcceptSocket(OnAccept, null);
                beginAcceptCalled = true;
                Socket socket = m_listener.EndAcceptSocket(ar);

                if (!OnAcceptingSocket(socket))
                {
                    socket.Disconnect(true);
                    return;
                }

                m_logWriter.Write(this, LogPrio.Debug, "Accepted connection from: " + socket.RemoteEndPoint);

                if (m_certificate != null)
                    m_contextFactory.CreateSecureContext(socket, m_certificate, m_sslProtocol, m_clientCertValCallback);
                else
                    m_contextFactory.CreateContext(socket);
            }
            catch (Exception err)
            {
                m_logWriter.Write(this, LogPrio.Debug, err.Message);
                ExceptionThrown?.Invoke(this, err);

                if (!beginAcceptCalled)
                    RetryBeginAccept();
            }
        }

        /// <summary>
        /// Will try to accept connections one more time.
        /// </summary>
        /// <exception cref="Exception">If any exceptions is thrown.</exception>
        private void RetryBeginAccept()
        {
            try
            {
                m_logWriter.Write(this, LogPrio.Error, "Trying to accept connections again.");
                m_listener.BeginAcceptSocket(OnAccept, null);
            }
            catch (Exception err)
            {
                m_logWriter.Write(this, LogPrio.Fatal, err.Message);
                 ExceptionThrown?.Invoke(this, err);
            }
        }

        /// <summary>
        /// Can be used to create filtering of new connections.
        /// </summary>
        /// <param name="socket">Accepted socket</param>
        /// <returns>true if connection can be accepted; otherwise false.</returns>
        protected bool OnAcceptingSocket(Socket socket)
        {
            ClientAcceptedEventArgs args = new ClientAcceptedEventArgs(socket);
            Accepted?.Invoke(this, args);
            return !args.Revoked;
        }

        /// <summary>
        /// Start listen for new connections
        /// </summary>
        /// <param name="backlog">Number of connections that can stand in a queue to be accepted.</param>
        /// <exception cref="InvalidOperationException">Listener have already been started.</exception>
        public void Start(int backlog)
        {
            if (m_listener != null)
                throw new InvalidOperationException("Listener have already been started.");

            m_listener = new TcpListener(m_address, m_port);
            m_listener.Start(backlog);
            Interlocked.Increment(ref m_pendingAccepts);
            m_listener.BeginAcceptSocket(OnAccept, null);
        }

        /// <summary>
        /// Stop the listener
        /// </summary>
        /// <exception cref="SocketException"></exception>
        public void Stop()
        {
            m_shutdown = true;
            m_contextFactory.Shutdown();
            m_listener.Stop();
            if (!m_shutdownEvent.WaitOne())
                m_logWriter.Write(this, LogPrio.Error, "Failed to shutdown listener properly.");
            m_listener = null;
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (m_shutdownEvent != null)
            {
                m_shutdownEvent.Dispose();
            }
        }
    }
}