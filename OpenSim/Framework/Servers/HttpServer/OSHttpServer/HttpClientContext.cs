using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OSHttpServer.Exceptions;
using OSHttpServer.Parser;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using OpenMetaverse;
using System.Threading.Tasks;

namespace OSHttpServer
{
    /// <summary>
    /// Contains a connection to a browser/client.
    /// </summary>
    /// <remarks>
    /// Remember to <see cref="Start"/> after you have hooked the <see cref="RequestReceived"/> event.
    /// </remarks>
    public class HttpClientContext : IHttpClientContext, IDisposable
    {
        const int MAXREQUESTS = 20;
        const int MAXKEEPALIVE = 120000;

        static private int basecontextID;

        Queue<HttpRequest> m_requests;
        readonly object m_requestsLock = new();
        public int m_maxRequests = MAXREQUESTS;
        public bool m_waitingResponse; 

        private readonly byte[] m_ReceiveBuffer;
        private int m_ReceiveBytesLeft;
        private ILogWriter m_log;
        private readonly IHttpRequestParser m_parser;
        private Socket m_sock;

        public bool Available = true;
        public bool StreamPassedOff = false;

        public int LastActivityTimeMS = 0;
        public int MonitorKeepaliveStartMS = 0;
        public bool TriggerKeepalive = false;
        public int TimeoutFirstLine = 10000; // 10 seconds
        public int TimeoutRequestReceived = 30000; // 30 seconds

        public int TimeoutMaxIdle = 180000; // 3 minutes
        public int m_TimeoutKeepAlive = 30000;

        public bool FirstRequestLineReceived;
        public bool FullRequestReceived;

        private bool isSendingResponse = false;
        private bool m_isClosing = false;

        private HttpRequest m_currentRequest;
        private HttpResponse m_currentResponse;

        public int contextID { get; private set; }
        public int TimeoutKeepAlive
        {
            get { return m_TimeoutKeepAlive; }
            set
            {
                m_TimeoutKeepAlive = (value > MAXKEEPALIVE) ? MAXKEEPALIVE : value;
            }
        }

        public bool IsClosing
        {
            get { return m_isClosing;}
        }

        public int MaxRequests
        {
            get { return m_maxRequests; }
            set
            {
                if(value <= 1)
                    m_maxRequests = 1;
                else
                   m_maxRequests = value > MAXREQUESTS ? MAXREQUESTS : value;
            }
        }

        public bool IsSending()
        {
            return isSendingResponse;
        }

        public bool StopMonitoring;

        public IPEndPoint LocalIPEndPoint {get; set;}

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientContext"/> class.
        /// </summary>
        /// <param name="secured">true if the connection is secured (SSL/TLS)</param>
        /// <param name="remoteEndPoint">client that connected.</param>
        /// <param name="stream">Stream used for communication</param>
        /// <param name="parserFactory">Used to create a <see cref="IHttpRequestParser"/>.</param>
        /// <param name="bufferSize">Size of buffer to use when reading data. Must be at least 4096 bytes.</param>
        /// <exception cref="SocketException">If <see cref="Socket.BeginReceive(byte[],int,int,SocketFlags,AsyncCallback,object)"/> fails</exception>
        /// <exception cref="ArgumentException">Stream must be writable and readable.</exception>
        public HttpClientContext(bool secured, IPEndPoint remoteEndPoint,
                                    Stream stream, ILogWriter m_logWriter, Socket sock)
        {
            if (!stream.CanWrite || !stream.CanRead)
                throw new ArgumentException("Stream must be writable and readable.");

            LocalIPEndPoint = remoteEndPoint;
            m_log = m_logWriter;
            m_isClosing = false;
            m_currentRequest = new HttpRequest(this);
            m_parser = new HttpRequestParser(m_log);
            m_parser.RequestCompleted += OnRequestCompleted;
            m_parser.RequestLineReceived += OnRequestLine;
            m_parser.HeaderReceived += OnHeaderReceived;
            m_parser.BodyBytesReceived += OnBodyBytesReceived;
            IsSecured = secured;
            m_stream = stream;
            m_sock = sock;

            m_ReceiveBuffer = new byte[16384];
            m_requests = new Queue<HttpRequest>();

            SSLCommonName = "";
            if (secured)
            {
                SslStream _ssl = (SslStream)m_stream;
                X509Certificate _cert1 = _ssl.RemoteCertificate;
                if (_cert1 is not null)
                {
                    X509Certificate2 _cert2 = new(_cert1);
                    if (_cert2 is not null)
                        SSLCommonName = _cert2.GetNameInfo(X509NameType.SimpleName, false);
                }
            }

            ++basecontextID;
            if (basecontextID <= 0)
                basecontextID = 1;

            contextID = basecontextID;
        }

        public bool CanSend()
        {
            if (contextID < 0 || m_isClosing)
                return false;

            if (m_stream is null || m_sock is null || !m_sock.Connected)
                return false;

            return true;
        }

        /// <summary>
        /// Process incoming body bytes.
        /// </summary>
        /// <param name="sender"><see cref="IHttpRequestParser"/></param>
        /// <param name="e">Bytes</param>
        protected virtual void OnBodyBytesReceived(object sender, BodyEventArgs e)
        {
            m_currentRequest.AddToBody(e.Buffer, e.Offset, e.Count);
        }

        private static readonly byte[] OSUTF8expect = osUTF8.GetASCIIBytes("expect");
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void OnHeaderReceived(object sender, HeaderEventArgs e)
        {
            if (e.Name.ACSIILowerEquals(OSUTF8expect) && e.Value.Contains("100-continue"))
            {
                lock (m_requestsLock)
                {
                    if (m_maxRequests == MAXREQUESTS)
                        Respond("HTTP/1.1", HttpStatusCode.Continue, null);
                }
            }
            m_currentRequest.AddHeader(e.Name.ToString(), e.Value.ToString());
        }

        private void OnRequestLine(object sender, RequestLineEventArgs e)
        {
            m_currentRequest.Method = e.HttpMethod.ToString();
            m_currentRequest.HttpVersion = e.HttpVersion.ToString();
            m_currentRequest.UriPath = e.UriPath.ToString();
            m_currentRequest.AddHeader("remote_addr", LocalIPEndPoint.Address.ToString());
            m_currentRequest.AddHeader("remote_port", LocalIPEndPoint.Port.ToString());
            m_currentRequest.ArrivalTS = ContextTimeoutManager.GetTimeStamp();

            FirstRequestLineReceived = true;
            TriggerKeepalive = false;
            MonitorKeepaliveStartMS = 0;
            LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();
        }

        /// <summary>
        /// Start reading content.
        /// </summary>
        /// <remarks>
        /// Make sure to call base.Start() if you override this method.
        /// </remarks>
        public virtual void Start()
        {
            Task.Run(ReceiveLoop).ConfigureAwait(false);
        }

        /// <summary>
        /// Clean up context.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public virtual void Cleanup()
        {

            if (StreamPassedOff)
                return;

            contextID = -100;

            if (m_stream is not null)
            {
                m_stream.Close();
                m_stream = null;
                m_sock = null;
            }

            m_currentRequest?.Clear();
            m_currentRequest = null;
            m_currentResponse?.Clear();
            m_currentResponse = null;
            if(m_requests != null)
            {
                while(m_requests.Count > 0)
                {
                    HttpRequest req = m_requests.Dequeue();
                    req.Clear();
                }
                m_requests = null;
            }

            m_parser.Clear();

            FirstRequestLineReceived = false;
            FullRequestReceived = false;
            LastActivityTimeMS = 0;
            StopMonitoring = true;
            MonitorKeepaliveStartMS = 0;
            TriggerKeepalive = false;

            isSendingResponse = false;
            m_ReceiveBytesLeft = 0;
        }

        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Using SSL or other encryption method.
        /// </summary>
        [Obsolete("Use IsSecured instead.")]
        public bool Secured
        {
            get { return IsSecured; }
        }

        /// <summary>
        /// Using SSL or other encryption method.
        /// </summary>
        public bool IsSecured { get; internal set; }


        // returns the SSL commonName of remote Certificate
        public string SSLCommonName { get; internal set; }

        /// <summary>
        /// Specify which logger to use.
        /// </summary>
        public ILogWriter LogWriter
        {
            get { return m_log; }
            set
            {
                m_log = value ?? NullLogWriter.Instance;
                m_parser.LogWriter = m_log;
            }
        }

        private Stream m_stream;

        /// <summary>
        /// Gets or sets the network stream.
        /// </summary>
        internal Stream Stream
        {
            get { return m_stream; }
            set { m_stream = value; }
        }

        /// <summary>
        /// Disconnect from client
        /// </summary>
        /// <param name="error">error to report in the <see cref="Disconnected"/> event.</param>
        public void Disconnect(SocketError error)
        {
            // disconnect may not throw any exceptions
            try
            {
                try
                {
                    if (m_stream is not null)
                    {
                        if (error == SocketError.Success)
                        {
                            try
                            {
                                m_stream.Flush();
                            }
                            catch { }

                        }
                        m_stream.Close();
                        m_stream = null;
                    }
                    m_sock = null;
                }
                catch { }

                Disconnected?.Invoke(this, new DisconnectedEventArgs(error));
            }
            catch (Exception err)
            {
                LogWriter.Write(this, LogPrio.Error, "Disconnect threw an exception: " + err);
            }
        }

        private async void ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    if (m_stream == null || !m_stream.CanRead)
                        return;

                    int bytesRead = 
                        await m_stream.ReadAsync(m_ReceiveBuffer, m_ReceiveBytesLeft, m_ReceiveBuffer.Length - m_ReceiveBytesLeft).ConfigureAwait(false);

                    if (bytesRead == 0)
                    {
                        Disconnect(SocketError.Success);
                        return;
                    }

                    if (m_isClosing)
                        return;

                    m_ReceiveBytesLeft += bytesRead;

                    int offset = m_parser.Parse(m_ReceiveBuffer, 0, m_ReceiveBytesLeft);
                    if (m_stream is null)
                        return; // "Connection: Close" in effect.

                    if(offset > 0)
                    {
                        int nextBytesleft, nextOffset;
                        while ((nextBytesleft = m_ReceiveBytesLeft - offset) > 0)
                        {
                            nextOffset = m_parser.Parse(m_ReceiveBuffer, offset, nextBytesleft);

                            if (m_stream is null)
                                return; // "Connection: Close" in effect.

                            if (nextOffset == 0)
                                break;

                            offset = nextOffset;
                        }
                    }

                    // copy unused bytes to the beginning of the array
                    if (offset > 0 && m_ReceiveBytesLeft > offset)
                        Buffer.BlockCopy(m_ReceiveBuffer, offset, m_ReceiveBuffer, 0, m_ReceiveBytesLeft - offset);

                    m_ReceiveBytesLeft -= offset;
                    if (StreamPassedOff)
                        return; //?
                }
            }
            catch (BadRequestException err)
            {
                LogWriter.Write(this, LogPrio.Warning, "Bad request, responding with it. Error: " + err);
                try
                {
                    Respond("HTTP/1.1", HttpStatusCode.BadRequest, err.Message);
                }
                catch (Exception err2)
                {
                    LogWriter.Write(this, LogPrio.Fatal, "Failed to reply to a bad request. " + err2);
                }
                //Disconnect(SocketError.NoRecovery);
                Disconnect(SocketError.Success); // try to flush
            }
            catch (HttpException err)
            {
                LogWriter.Write(this, LogPrio.Warning, "Bad request, responding with it. Error: " + err.Message);
                try
                {
                    Respond("HTTP/1.1", err.HttpStatusCode, err.Message);
                }
                catch (Exception err2)
                {
                    LogWriter.Write(this, LogPrio.Fatal, "Failed to reply to a bad request. " + err2);
                }
                //Disconnect(SocketError.NoRecovery);
                Disconnect(SocketError.Success); // try to flush
            }
            catch (IOException err)
            {
                LogWriter.Write(this, LogPrio.Debug, "Failed to end receive: " + err.Message);
                if (err.InnerException is SocketException exception)
                    Disconnect((SocketError)exception.ErrorCode);
                else
                    Disconnect(SocketError.ConnectionReset);
            }
            catch (ObjectDisposedException err)
            {
                LogWriter.Write(this, LogPrio.Debug, "Failed to end receive : " + err.Message);
                Disconnect(SocketError.NotSocket);
            }
            catch (NullReferenceException err)
            {
                LogWriter.Write(this, LogPrio.Debug, "Failed to end receive : NullRef: " + err.Message);
                Disconnect(SocketError.NoRecovery);
            }
            catch (Exception err)
            {
                LogWriter.Write(this, LogPrio.Debug, "Failed to end receive: " + err.Message);
                Disconnect(SocketError.NoRecovery);
            }
        }

        private void OnRequestCompleted(object source, EventArgs args)
        {
            TriggerKeepalive = false;
            MonitorKeepaliveStartMS = 0;
            FullRequestReceived = true;
            LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();

            if (m_maxRequests <= 0 || RequestReceived is null)
                return;

            if (--m_maxRequests == 0)
                m_currentRequest.Connection = ConnectionType.Close;

            if(m_currentRequest.Uri is null)
            {
                // should not happen
                try
                {
                    Uri uri = new(m_currentRequest.Secure ? "https://" : "http://" + m_currentRequest.UriPath);
                    m_currentRequest.Uri = uri;
                    m_currentRequest.UriPath = uri.AbsolutePath;
                }
                catch
                {
                    return;
                }
            }

            // load cookies if they exist
            if(m_currentRequest.Headers["cookie"] is not null)
                m_currentRequest.SetCookies(new RequestCookies(m_currentRequest.Headers["cookie"]));

            m_currentRequest.Body.Seek(0, SeekOrigin.Begin);

            HttpRequest currentRequest = m_currentRequest;
            m_currentRequest = new HttpRequest(this);

            lock (m_requestsLock)
            {
                if(m_waitingResponse)
                {
                    m_requests.Enqueue(currentRequest);
                    return;
                }
                else
                    m_waitingResponse = true;
            }
            RequestReceived?.Invoke(this, new RequestEventArgs(currentRequest));
        }

        public void StartSendResponse(HttpResponse response)
        {
            LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();
            isSendingResponse = true;
            m_currentResponse = response;
            ContextTimeoutManager.EnqueueSend(this, response.Priority);
        }

        public bool TrySendResponse(int bytesLimit)
        {
            if (m_currentResponse is null)
                return false;
            try
            {
                if (m_currentResponse.Sent)
                    return false;

                if(!CanSend())
                    return false;

                LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();
                return m_currentResponse.SendNextAsync(bytesLimit);
            }
            catch 
            {
                return false;
            }
        }

        public void ContinueSendResponse()
        {
            if(m_currentResponse is null)
                return;
            LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();
            ContextTimeoutManager.EnqueueSend(this, m_currentResponse.Priority);
        }

        public void EndSendResponse(uint requestID, ConnectionType ctype)
        {
            isSendingResponse = false;
            m_currentResponse?.Clear();
            m_currentResponse = null;
            lock (m_requestsLock)
                m_waitingResponse = false;

            if(contextID < 0)
                return;

            if (ctype == ConnectionType.Close)
            {
                m_isClosing = true;
                m_requests.Clear();
                TriggerKeepalive = true;
                return;
            }
            else
            {
                if (Stream is null || !Stream.CanWrite)
                    return;

                LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();
                HttpRequest nextRequest = null;
                lock (m_requestsLock)
                {
                    if (m_requests is not  null && m_requests.Count > 0)
                        nextRequest = m_requests.Dequeue();
                    if (nextRequest is not null && RequestReceived is not null)
                    {
                        m_waitingResponse = true;
                        TriggerKeepalive = false;
                    }
                    else
                        TriggerKeepalive = true;
                }
                if (nextRequest is not null)
                    RequestReceived?.Invoke(this, new RequestEventArgs(nextRequest));
            }
            ContextTimeoutManager.PulseWaitSend();
        }

        /// <summary>
        /// Send a response.
        /// </summary>
        /// <param name="httpVersion">Either <see cref="HttpHelper.HTTP10"/> or <see cref="HttpHelper.HTTP11"/></param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="reason">reason for the status code.</param>
        /// <param name="body">HTML body contents, can be null or empty.</param>
        /// <param name="contentType">A content type to return the body as, i.e. 'text/html' or 'text/plain', defaults to 'text/html' if null or empty</param>
        /// <exception cref="ArgumentException">If <paramref name="httpVersion"/> is invalid.</exception>
        public void Respond(string httpVersion, HttpStatusCode statusCode, string reason, string body, string contentType)
        {
            LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();

            if (string.IsNullOrEmpty(reason))
                reason = statusCode.ToString();

            byte[] buffer;
            if(string.IsNullOrEmpty(body))
                buffer = Encoding.ASCII.GetBytes(httpVersion + " " + (int)statusCode + " " + reason + "\r\n\r\n");
            else
            {
                if (string.IsNullOrEmpty(contentType))
                    contentType = "text/html";
                buffer = Encoding.UTF8.GetBytes(
                        string.Format("{0} {1} {2}\r\nContent-Type: {5}\r\nContent-Length: {3}\r\n\r\n{4}",
                                                  httpVersion, (int)statusCode, reason ?? statusCode.ToString(),
                                                  body.Length, body, contentType));
            }
            Send(buffer);
        }

        /// <summary>
        /// Send a response.
        /// </summary>
        /// <param name="httpVersion">Either <see cref="HttpHelper.HTTP10"/> or <see cref="HttpHelper.HTTP11"/></param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="reason">reason for the status code.</param>
        public void Respond(string httpVersion, HttpStatusCode statusCode, string reason)
        {
            if (string.IsNullOrEmpty(reason))
                reason = statusCode.ToString();
            byte[] buffer = Encoding.ASCII.GetBytes(httpVersion + " " + (int)statusCode + " " + reason + "\r\n\r\n");
            Send(buffer);
        }

        /// <summary>
        /// send a whole buffer
        /// </summary>
        /// <param name="buffer">buffer to send</param>
        /// <exception cref="ArgumentNullException"></exception>
        public bool Send(byte[] buffer)
        {
            if (buffer is null)
                throw new ArgumentNullException("buffer");
            return Send(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Send data using the stream
        /// </summary>
        /// <param name="buffer">Contains data to send</param>
        /// <param name="offset">Start position in buffer</param>
        /// <param name="size">number of bytes to send</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>

        private readonly object sendLock = new();

        public bool Send(byte[] buffer, int offset, int size)
        {
            if (m_stream is null || m_sock is null || !m_sock.Connected)
                return false;

            if (offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", offset, "offset + size is beyond end of buffer.");

            LastActivityTimeMS = ContextTimeoutManager.EnvironmentTickCount();

            bool ok = true;
            ContextTimeoutManager.ContextEnterActiveSend();
            lock (sendLock) // can't have overlaps here
            {
                try
                {
                    m_stream.Write(buffer, offset, size);
                }
                catch
                {
                    ok = false;
                }
            }

            ContextTimeoutManager.ContextLeaveActiveSend();
            if (!ok && m_stream is not null)
                Disconnect(SocketError.NoRecovery);
            return ok;
        }

        private void SendAsyncEnd(IAsyncResult res)
        {
            bool didleave = false;
            try
            {
                m_stream.EndWrite(res);

                ContextTimeoutManager.ContextLeaveActiveSend();
                m_currentResponse.CheckSendNextAsyncContinue();
                didleave = true;
            }
            catch (Exception e)
            {
                e.GetHashCode();
                if (m_stream is not null)
                    Disconnect(SocketError.NoRecovery);
            }
            if(!didleave)
                ContextTimeoutManager.ContextLeaveActiveSend();
        }

        public bool SendAsyncStart(byte[] buffer, int offset, int size)
        {
            if (m_stream is null || m_sock is null || !m_sock.Connected)
                return false;

            if (offset + size > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", offset, "offset + size is beyond end of buffer.");

            bool ok = true;
            ContextTimeoutManager.ContextEnterActiveSend();
            try
            {
                m_stream.BeginWrite(buffer, offset, size, SendAsyncEnd, null);
            }
            catch (Exception e)
            {
                e.GetHashCode();
                ContextTimeoutManager.ContextLeaveActiveSend();
                ok = false;
            }

            if (!ok && m_stream is not null)
                Disconnect(SocketError.NoRecovery);
            return ok;
        }

        /// <summary>
        /// The context have been disconnected.
        /// </summary>
        /// <remarks>
        /// Event can be used to clean up a context, or to reuse it.
        /// </remarks>
        public event EventHandler<DisconnectedEventArgs> Disconnected;
        /// <summary>
        /// A request have been received in the context.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived;

        public HTTPNetworkContext GiveMeTheNetworkStreamIKnowWhatImDoing()
        {
            StreamPassedOff = true;
            m_parser.RequestCompleted -= OnRequestCompleted;
            m_parser.RequestLineReceived -= OnRequestLine;
            m_parser.HeaderReceived -= OnHeaderReceived;
            m_parser.BodyBytesReceived -= OnBodyBytesReceived;
            m_parser.Clear();

            m_currentRequest?.Clear();
            m_currentRequest = null;
            m_currentResponse?.Clear();
            m_currentResponse = null;
            if (m_requests is not null)
            {
                while (m_requests.Count > 0)
                {
                    HttpRequest req = m_requests.Dequeue();
                    req.Clear();
                }
            }
            m_requests.Clear();
            m_requests = null;

            return new HTTPNetworkContext() { Socket = m_sock, Stream = m_stream as NetworkStream };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (contextID >= 0)
            {
                StreamPassedOff = false;
                Cleanup();
            }
        }
    }
}