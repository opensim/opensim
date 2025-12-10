using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace OSHttpServer
{
    public class OSHttpListener: IDisposable
    {
        private readonly IPAddress m_address;
        private readonly X509Certificate m_certificate;
        private readonly IHttpContextFactory m_contextFactory;
        private readonly int m_port;
        private readonly ManualResetEvent m_shutdownEvent = new(false);
        private readonly SslProtocols m_sslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;

        private TcpListener m_listener;
        private ILogWriter m_logWriter = NullLogWriter.Instance;
        private bool m_shutdown;
        public readonly CancellationTokenSource m_CancellationSource = new();
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
        protected OSHttpListener(IPAddress address, int port)
        {
            m_address = address;
            m_port = port;
            m_contextFactory = new HttpContextFactory(m_logWriter);
            m_contextFactory.RequestReceived += OnRequestReceived;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OSHttpListener"/> class.
        /// </summary>
        /// <param name="address">IP Address to accept connections on</param>
        /// <param name="port">TCP Port to listen on, default HTTPS port is 443</param>
        /// <param name="factory">Factory used to create <see cref="IHttpClientContext"/>es.</param>
        /// <param name="certificate">Certificate to use</param>
        protected OSHttpListener(IPAddress address, int port, X509Certificate certificate)
            : this(address, port)
        {
            m_certificate = certificate;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OSHttpListener"/> class.
        /// </summary>
        /// <param name="address">IP Address to accept connections on</param>
        /// <param name="port">TCP Port to listen on, default HTTPS port is 443</param>
        /// <param name="factory">Factory used to create <see cref="IHttpClientContext"/>es.</param>
        /// <param name="certificate">Certificate to use</param>
        /// <param name="protocols">which HTTPS protocol to use, default is TLS.</param>
        protected OSHttpListener(IPAddress address, int port, X509Certificate certificate, SslProtocols protocols)
            : this(address, port)
        {
            m_certificate = certificate;
            m_sslProtocols = protocols;
        }

        public static OSHttpListener Create(IPAddress address, int port)
        {
            return new OSHttpListener(address, port);
        }

        public static OSHttpListener Create(IPAddress address, int port, X509Certificate certificate)
        {
            return new OSHttpListener(address, port, certificate);
        }

        public static OSHttpListener Create(IPAddress address, int port, X509Certificate certificate, SslProtocols protocols)
        {
            return new OSHttpListener(address, port, certificate, protocols);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                if (m_certificate is not null)
                    m_logWriter.Write(this, LogPrio.Info, $"HTTPS({m_sslProtocols}) listening on {m_address}:{m_port}");
                else
                    m_logWriter.Write(this, LogPrio.Info, $"HTTP listening on {m_address}:{m_port}");
            }
        }

        /// <summary>
        /// True if we should turn on trace logs.
        /// </summary>
        public bool UseTraceLogs { get; set; }

        private async void AcceptLoop()
        {
            while (true)
            {
                if (m_shutdown)
                {
                    m_shutdownEvent?.Set();
                    break;
                }

                Socket socket = null;
                try
                {
                    socket = await m_listener.AcceptSocketAsync(m_CancellationSource.Token).ConfigureAwait(false);
                    if (!socket.Connected)
                    {
                        socket.Dispose();
                        continue;
                    }
                }
                catch (OperationCanceledException)
                {
                    m_shutdownEvent?.Set();
                    break;
                }
                catch (SocketException snssErr)
                {
                    m_logWriter.Write(this, LogPrio.Debug, "OSHTTP Accept wait ignoring error: " + snssErr.Message);
                    socket?.Dispose();
                    continue;
                }
                catch (Exception err)
                {
                    m_logWriter.Write(this, LogPrio.Debug, "OSHTTP Accept wait fatal error: " + err.Message);
                    ExceptionThrown?.Invoke(this, err);
                }

                if (m_shutdown)
                {
                    m_shutdownEvent?.Set();
                    socket?.Dispose();
                    break;
                }

                if(socket == null)
                    continue;

                socket.NoDelay = true;
                try
                {
                    if (!OnAcceptingSocket(socket))
                    {
                        socket.Disconnect(true);
                        continue;
                    }

                    if (socket.Connected)
                    {
                        m_logWriter.Write(this, LogPrio.Debug, $"Accepted connection from: {socket.RemoteEndPoint}");

                        if (m_certificate is not null)
                            m_contextFactory.CreateSecureContext(socket, m_certificate, m_sslProtocols, m_clientCertValCallback);
                        else
                            m_contextFactory.CreateContext(socket);
                    }
                    else
                        socket?.Dispose();
                }
                catch (OperationCanceledException)
                {
                    m_shutdownEvent?.Set();
                    break;
                }
                catch (SocketException snssErr)
                {
                    m_logWriter.Write(this, LogPrio.Debug, "OSHTTP Accept processing ignoring error: " + snssErr.Message);
                    socket?.Dispose();
                    continue;
                }
                catch (Exception err)
                {
                    m_logWriter.Write(this, LogPrio.Debug, "OSHTTP Accept processing fatal error: " + err.Message);
                    ExceptionThrown?.Invoke(this, err);
                }
            }
        }

        /// <summary>
        /// Can be used to create filtering of new connections.
        /// </summary>
        /// <param name="socket">Accepted socket</param>
        /// <returns>true if connection can be accepted; otherwise false.</returns>
        protected bool OnAcceptingSocket(Socket socket)
        {
            if(Accepted!=null)
            {
                ClientAcceptedEventArgs args = new(socket);
                Accepted?.Invoke(this, args);
                return !args.Revoked;
            }
            return true;
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
            Task.Run(AcceptLoop).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the listener
        /// </summary>
        /// <exception cref="SocketException"></exception>
        public void Stop()
        {
            m_shutdown = true;
            m_CancellationSource.Cancel();
            m_contextFactory.Shutdown();
            if (!m_shutdownEvent.WaitOne())
                m_logWriter.Write(this, LogPrio.Error, "Failed to shutdown listener properly.");
            m_listener.Stop();
            m_listener = null;
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (m_shutdownEvent != null)
            {
                m_shutdownEvent.Dispose();
                m_CancellationSource.Dispose();
            }
        }
    }
}