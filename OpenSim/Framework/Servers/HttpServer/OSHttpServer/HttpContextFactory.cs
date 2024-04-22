using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace OSHttpServer
{
    /// <summary>
    /// Used to create and reuse contexts.
    /// </summary>
    public class HttpContextFactory : IHttpContextFactory
    {
        private readonly ConcurrentDictionary<int, HttpClientContext> m_activeContexts = new();
        private readonly ILogWriter m_logWriter;

        /// <summary>
        /// A request have been received from one of the contexts.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpContextFactory"/> class.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="bufferSize">Amount of bytes to read from the incoming socket stream.</param>
        /// <param name="factory">Used to create a request parser.</param>
        public HttpContextFactory(ILogWriter writer)
        {
            m_logWriter = writer;
            ContextTimeoutManager.Start();
        }

        /// <summary>
        /// Create a new context.
        /// </summary>
        /// <param name="isSecured">true if socket is running HTTPS.</param>
        /// <param name="endPoint">Client that connected</param>
        /// <param name="stream">Network/SSL stream.</param>
        /// <returns>A context.</returns>
        protected HttpClientContext CreateContext(bool isSecured, IPEndPoint endPoint, Stream stream, Socket sock)
        {
            var context = new HttpClientContext(isSecured, endPoint, stream, m_logWriter, sock);
            context.Disconnected += OnFreeContext;
            context.RequestReceived += OnRequestReceived;

            ContextTimeoutManager.StartMonitoringContext(context);
            m_activeContexts[context.contextID] = context;
            context.Start();
            return context;
        }

        private void OnRequestReceived(object sender, RequestEventArgs e)
        {
            RequestReceived?.Invoke(sender, e);
        }

        private void OnFreeContext(object sender, DisconnectedEventArgs e)
        {
            var imp = sender as HttpClientContext;
            if (imp == null || imp.contextID < 0)
                return;

            m_activeContexts.TryRemove(imp.contextID, out HttpClientContext dummy);
            imp.Close();
        }


        #region IHttpContextFactory Members

        /// <summary>
        /// Create a secure <see cref="IHttpClientContext"/>.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="OSHttpListener"/>).</param>
        /// <param name="certificate">HTTPS certificate to use.</param>
        /// <param name="protocol">Kind of HTTPS protocol. Usually TLS or SSL.</param>
        /// <returns>
        /// A created <see cref="IHttpClientContext"/>.
        /// </returns>
        public IHttpClientContext CreateSecureContext(Socket socket, X509Certificate certificate,
             SslProtocols protocol, RemoteCertificateValidationCallback _clientCallback = null)
        {
            var networkStream = new NetworkStream(socket, true);
            var remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;

            SslStream sslStream = null;
            try
            {
                if (_clientCallback == null)
                {
                    sslStream = new SslStream(networkStream, false);
                    sslStream.AuthenticateAsServer(certificate, false, protocol, false);
                }
                else
                {
                    sslStream = new SslStream(networkStream, false,
                            new RemoteCertificateValidationCallback(_clientCallback));
                    sslStream.AuthenticateAsServer(certificate, true, protocol, false);
                }
            }
            catch (Exception e)
            {
                m_logWriter.Write(this, LogPrio.Error, e.Message);
                sslStream.Close();
                return null;
            }

            return CreateContext(true, remoteEndPoint, sslStream, socket);
        }

        /// <summary>
        /// Creates a <see cref="IHttpClientContext"/> that handles a connected client.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="OSHttpListener"/>).</param>
        /// <returns>
        /// A creates <see cref="IHttpClientContext"/>.
        /// </returns>
        public IHttpClientContext CreateContext(Socket socket)
        {
            var networkStream = new NetworkStream(socket, true);
            var remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;
            return CreateContext(false, remoteEndPoint, networkStream, socket);
        }

        #endregion

        /// <summary>
        /// Server is shutting down so shut down the factory
        /// </summary>
        public void Shutdown()
        {
            ContextTimeoutManager.Stop();
        }
    }

    /// <summary>
    /// Used to create <see cref="IHttpClientContext"/>es.
    /// </summary>
    public interface IHttpContextFactory
    {
        /// <summary>
        /// Creates a <see cref="IHttpClientContext"/> that handles a connected client.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="OSHttpListener"/>).</param>
        /// <returns>A creates <see cref="IHttpClientContext"/>.</returns>
        IHttpClientContext CreateContext(Socket socket);

        /// <summary>
        /// Create a secure <see cref="IHttpClientContext"/>.
        /// </summary>
        /// <param name="socket">Client socket (accepted by the <see cref="OSHttpListener"/>).</param>
        /// <param name="certificate">HTTPS certificate to use.</param>
        /// <param name="protocol">Kind of HTTPS protocol. Usually TLS or SSL.</param>
        /// <returns>A created <see cref="IHttpClientContext"/>.</returns>
        IHttpClientContext CreateSecureContext(Socket socket, X509Certificate certificate,
             SslProtocols protocol, RemoteCertificateValidationCallback _clientCallback = null);

        /// <summary>
        /// A request have been received from one of the contexts.
        /// </summary>
        event EventHandler<RequestEventArgs> RequestReceived;

        /// <summary>
        /// Server is shutting down so shut down the factory
        /// </summary>
        void Shutdown();
    }
}