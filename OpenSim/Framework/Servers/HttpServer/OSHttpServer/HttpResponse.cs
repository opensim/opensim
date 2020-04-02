using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OSHttpServer
{
    public class HttpResponse : IHttpResponse
    {
        private const string DefaultContentType = "text/html;charset=UTF-8";
        private readonly IHttpClientContext m_context;
        private readonly ResponseCookies m_cookies = new ResponseCookies();
        private readonly NameValueCollection m_headers = new NameValueCollection();
        private string m_httpVersion;
        private Stream m_body;
        private long m_contentLength;
        private string m_contentType;
        private Encoding m_encoding = Encoding.UTF8;
        private int m_keepAlive = 60;
        public uint requestID { get; private set; }
        public byte[] RawBuffer { get; set; }
        public int RawBufferStart { get; set; }
        public int RawBufferLen { get; set; }

        internal byte[] m_headerBytes = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="IHttpResponse"/> class.
        /// </summary>
        /// <param name="context">Client that send the <see cref="IHttpRequest"/>.</param>
        /// <param name="request">Contains information of what the client want to receive.</param>
        /// <exception cref="ArgumentException"><see cref="IHttpRequest.HttpVersion"/> cannot be empty.</exception>
        public HttpResponse(IHttpClientContext context, IHttpRequest request)
        {
            m_httpVersion = request.HttpVersion;
            if (string.IsNullOrEmpty(m_httpVersion))
                m_httpVersion = "HTTP/1.0";

            Status = HttpStatusCode.OK;
            m_context = context;
            m_Connetion = request.Connection;
            requestID = request.ID;
            RawBufferStart = -1;
            RawBufferLen = -1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IHttpResponse"/> class.
        /// </summary>
        /// <param name="context">Client that send the <see cref="IHttpRequest"/>.</param>
        /// <param name="httpVersion">Version of HTTP protocol that the client uses.</param>
        /// <param name="connectionType">Type of HTTP connection used.</param>
        internal HttpResponse(IHttpClientContext context, string httpVersion, ConnectionType connectionType)
        {
            Status = HttpStatusCode.OK;
            m_context = context;
            m_httpVersion = httpVersion;
            m_Connetion = connectionType;
        }
        private ConnectionType m_Connetion;
        public ConnectionType Connection
        {
            get { return m_Connetion; }
            set { return; }
        }

        private int m_priority = 0;
        public int Priority
        {
            get { return m_priority;}
            set { m_priority = (value > 0 && m_priority < 3)? value : 0;}
        }

        #region IHttpResponse Members

        /// <summary>
        /// The body stream is used to cache the body contents
        /// before sending everything to the client. It's the simplest
        /// way to serve documents.
        /// </summary>
        public Stream Body
        {
            get
            { 
                if(m_body == null)
                    m_body = new MemoryStream();
                return m_body;
            }
            set { m_body = value; }
        }

        /// <summary>
        /// The chunked encoding modifies the body of a message in order to
        /// transfer it as a series of chunks, each with its own size indicator,
        /// followed by an OPTIONAL trailer containing entity-header fields. This
        /// allows dynamically produced content to be transferred along with the
        /// information necessary for the recipient to verify that it has
        /// received the full message.
        /// </summary>
        public bool Chunked { get; set; }


        /// <summary>
        /// Defines the version of the HTTP Response for applications where it's required
        /// for this to be forced.
        /// </summary>
        public string ProtocolVersion
        {
            get { return m_httpVersion; }
            set { m_httpVersion = value; }
        }

        /// <summary>
        /// Encoding to use when sending stuff to the client.
        /// </summary>
        /// <remarks>Default is UTF8</remarks>
        public Encoding Encoding
        {
            get { return m_encoding; }
            set { m_encoding = value; }
        }


        /// <summary>
        /// Number of seconds to keep connection alive
        /// </summary>
        /// <remarks>Only used if Connection property is set to <see cref="ConnectionType.KeepAlive"/>.</remarks>
        public int KeepAlive
        {
            get { return m_keepAlive; }
            set
            {
                if (value > 400)
                    m_keepAlive = 400;
                else if (value <= 0)
                    m_keepAlive = 0;
                else
                    m_keepAlive = value;
            }
        }

        /// <summary>
        /// Status code that is sent to the client.
        /// </summary>
        /// <remarks>Default is <see cref="HttpStatusCode.OK"/></remarks>
        public HttpStatusCode Status { get; set; }

        /// <summary>
        /// Information about why a specific status code was used.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Size of the body. MUST be specified before sending the header,
        /// unless property Chunked is set to true.
        /// </summary>
        public long ContentLength
        {
            get { return m_contentLength; }
            set { m_contentLength = value; }
        }

        /// <summary>
        /// Kind of content in the body
        /// </summary>
        /// <remarks>Default type is "text/html"</remarks>
        public string ContentType
        {
            get { return m_contentType; }
            set { m_contentType = value; }
        }

        /// <summary>
        /// Headers have been sent to the client-
        /// </summary>
        /// <remarks>You can not send any additional headers if they have already been sent.</remarks>
        public bool HeadersSent { get; private set; }

        /// <summary>
        /// The whole response have been sent.
        /// </summary>
        public bool Sent { get; private set; }

        /// <summary>
        /// Cookies that should be created/changed.
        /// </summary>
        public ResponseCookies Cookies
        {
            get { return m_cookies; }
        }

        /// <summary>
        /// Add another header to the document.
        /// </summary>
        /// <param name="name">Name of the header, case sensitive, use lower cases.</param>
        /// <param name="value">Header values can span over multiple lines as long as each line starts with a white space. New line chars should be \r\n</param>
        /// <exception cref="InvalidOperationException">If headers already been sent.</exception>
        /// <exception cref="ArgumentException">If value conditions have not been met.</exception>
        /// <remarks>Adding any header will override the default ones and those specified by properties.</remarks>
        public void AddHeader(string name, string value)
        {
            if (HeadersSent)
                throw new InvalidOperationException("Headers have already been sent.");

            for (int i = 1; i < value.Length; ++i)
            {
                if (value[i] == '\r' && !char.IsWhiteSpace(value[i - 1]))
                    throw new ArgumentException("New line in value do not start with a white space.");
                if (value[i] == '\n' && value[i - 1] != '\r')
                    throw new ArgumentException("Invalid new line sequence, should be \\r\\n (crlf).");
            }

            m_headers[name] = value;
        }

        /// <summary>
        /// Send headers and body to the browser.
        /// </summary>
        /// <exception cref="InvalidOperationException">If content have already been sent.</exception>
        public void SendOri()
        {
            if (Sent)
                throw new InvalidOperationException("Everything have already been sent.");

            m_context.ReqResponseAboutToSend(requestID);
            if (m_context.MAXRequests == 0 || m_keepAlive == 0)
            {
                Connection = ConnectionType.Close;
                m_context.TimeoutKeepAlive = 0;
            }
            else
            {
                if (m_keepAlive > 0)
                    m_context.TimeoutKeepAlive = m_keepAlive * 1000;
            }

            if (!HeadersSent)
            {
                if (!SendHeaders())
                {
                    m_body.Dispose();
                    Sent = true;
                    return;
                }
            }

            if(RawBuffer != null)
            {
                if(RawBufferStart >= 0 && RawBufferLen > 0)
                {
                    if (RawBufferStart > RawBuffer.Length)
                        RawBufferStart = 0;

                    if (RawBufferLen + RawBufferStart > RawBuffer.Length)
                        RawBufferLen = RawBuffer.Length - RawBufferStart;

                    /*
                    int curlen;
                    while(RawBufferLen > 0)
                    {
                        curlen = RawBufferLen;
                        if(curlen > 8192)
                            curlen = 8192;
                        if (!_context.Send(RawBuffer, RawBufferStart, curlen))
                        {
                            RawBuffer = null;
                            RawBufferStart = -1;
                            RawBufferLen = -1;
                            Body.Dispose();
                            return;
                        }
                        RawBufferLen -= curlen;
                        RawBufferStart += curlen;
                    }
                    */
                    if(RawBufferLen > 0)
                    {
                        if (!m_context.Send(RawBuffer, RawBufferStart, RawBufferLen))
                        {
                            RawBuffer = null;
                            RawBufferStart = -1;
                            RawBufferLen = -1;
                            if(m_body != null)
                                m_body.Dispose();
                            Sent = true;
                            return;
                        }
                    }
                }

                RawBuffer = null;
                RawBufferStart = -1;
                RawBufferLen = -1;
            }

            if(m_body != null && m_body.Length > 0)
            {
                m_body.Flush();
                m_body.Seek(0, SeekOrigin.Begin);

                var buffer = new byte[8192];
                int bytesRead = m_body.Read(buffer, 0, 8192);
                while (bytesRead > 0)
                {
                    if (!m_context.Send(buffer, 0, bytesRead))
                        break;
                    bytesRead = m_body.Read(buffer, 0, 8192);
                }

                m_body.Dispose();
            }
            Sent = true;
            m_context.ReqResponseSent(requestID, Connection);
        }


        /// <summary>
        /// Make sure that you have specified <see cref="ContentLength"/> and sent the headers first.
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="InvalidOperationException">If headers have not been sent.</exception>
        /// <see cref="SendHeaders"/>
        /// <param name="offset">offset of first byte to send</param>
        /// <param name="count">number of bytes to send.</param>
        /// <seealso cref="Send"/>
        /// <seealso cref="SendHeaders"/>
        /// <remarks>This method can be used if you want to send body contents without caching them first. This
        /// is recommended for larger files to keep the memory usage low.</remarks>
        public bool SendBody(byte[] buffer, int offset, int count)
        {
            if (!HeadersSent)
                throw new InvalidOperationException("Send headers, and remember to specify ContentLength first.");

            bool sent = m_context.Send(buffer, offset, count);
            Sent = true;
            if (sent)
                m_context.ReqResponseSent(requestID, Connection);
            return sent;
        }

        /// <summary>
        /// Make sure that you have specified <see cref="ContentLength"/> and sent the headers first.
        /// </summary>
        /// <param name="buffer"></param>
        /// <exception cref="InvalidOperationException">If headers have not been sent.</exception>
        /// <see cref="SendHeaders"/>
        /// <seealso cref="Send"/>
        /// <seealso cref="SendHeaders"/>
        /// <remarks>This method can be used if you want to send body contents without caching them first. This
        /// is recommended for larger files to keep the memory usage low.</remarks>
        public bool SendBody(byte[] buffer)
        {
            if (!HeadersSent)
                throw new InvalidOperationException("Send headers, and remember to specify ContentLength first.");

            bool sent = m_context.Send(buffer);
            if (sent)
                m_context.ReqResponseSent(requestID, Connection);
            Sent = true;
            return sent;
        }

        /// <summary>
        /// Send headers to the client.
        /// </summary>
        /// <exception cref="InvalidOperationException">If headers already been sent.</exception>
        /// <seealso cref="AddHeader"/>
        /// <seealso cref="Send"/>
        /// <seealso cref="SendBody(byte[])"/>
        public bool SendHeaders()
        {
            if (HeadersSent)
                throw new InvalidOperationException("Header have already been sent.");

            HeadersSent = true;

            if (m_headers["Date"] == null)
                m_headers["Date"] = DateTime.Now.ToString("r");
            if (m_headers["Content-Length"] == null)
            {
                int len = (int)m_contentLength;
                if(len == 0)
                {
                    if(m_body != null)
                        len = (int)m_body.Length;
                    if(RawBuffer != null)
                        len += RawBufferLen;
                }
                m_headers["Content-Length"] = len.ToString();
            }
            if (m_headers["Content-Type"] == null)
                m_headers["Content-Type"] = m_contentType ?? DefaultContentType;
            if (m_headers["Server"] == null)
                m_headers["Server"] = "Tiny WebServer";

            int keepaliveS = m_context.TimeoutKeepAlive / 1000;
            if (Connection == ConnectionType.KeepAlive && keepaliveS > 0 && m_context.MAXRequests > 0)
            {
                m_headers["Keep-Alive"] = "timeout=" + keepaliveS + ", max=" + m_context.MAXRequests;
                m_headers["Connection"] = "Keep-Alive";
            }
            else
                m_headers["Connection"] = "close";

            var sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2}\r\n", m_httpVersion, (int)Status,
                            string.IsNullOrEmpty(Reason) ? Status.ToString() : Reason);

            for (int i = 0; i < m_headers.Count; ++i)
            {
                string headerName = m_headers.AllKeys[i];
                string[] values = m_headers.GetValues(i);
                if (values == null) continue;
                foreach (string value in values)
                    sb.AppendFormat("{0}: {1}\r\n", headerName, value);
            }

            foreach (ResponseCookie cookie in Cookies)
                sb.AppendFormat("Set-Cookie: {0}\r\n", cookie);

            sb.Append("\r\n");

            m_headers.Clear();

            return m_context.Send(Encoding.GetBytes(sb.ToString()));
        }

        public byte[] GetHeaders()
        {
            HeadersSent = true;

            var sb = new StringBuilder();
            if(string.IsNullOrWhiteSpace(m_httpVersion))
                sb.AppendFormat("HTTP1/0 {0} {1}\r\n", (int)Status,
                                string.IsNullOrEmpty(Reason) ? Status.ToString() : Reason);
            else
                sb.AppendFormat("{0} {1} {2}\r\n", m_httpVersion, (int)Status,
                                string.IsNullOrEmpty(Reason) ? Status.ToString() : Reason);

            if (m_headers["Date"] == null)
                sb.AppendFormat("Date: {0}\r\n", DateTime.Now.ToString("r"));
            if (m_headers["Content-Length"] == null)
            {
                long len = m_contentLength;
                if (len == 0)
                {
                    len = Body.Length;
                    if (RawBuffer != null && RawBufferLen > 0)
                        len += RawBufferLen;
                }
                sb.AppendFormat("Content-Length: {0}\r\n", len);
            }
            if (m_headers["Content-Type"] == null)
                sb.AppendFormat("Content-Type: {0}\r\n", m_contentType ?? DefaultContentType);
            if (m_headers["Server"] == null)
                sb.Append("Server: OSWebServer\r\n");

            int keepaliveS = m_context.TimeoutKeepAlive / 1000;
            if (Connection == ConnectionType.KeepAlive && keepaliveS > 0 && m_context.MAXRequests > 0)
            {
                sb.AppendFormat("Keep-Alive:timeout={0}, max={1}\r\n", keepaliveS, m_context.MAXRequests);
                sb.Append("Connection: Keep-Alive\r\n");
            }
            else
                sb.Append("Connection: close\r\n");

            if (m_headers["Connection"] != null)
                m_headers["Connection"] = null;
            if (m_headers["Keep-Alive"] != null)
                m_headers["Keep-Alive"] = null;

            for (int i = 0; i < m_headers.Count; ++i)
            {
                string headerName = m_headers.AllKeys[i];
                string[] values = m_headers.GetValues(i);
                if (values == null) continue;
                foreach (string value in values)
                    sb.AppendFormat("{0}: {1}\r\n", headerName, value);
            }

            foreach (ResponseCookie cookie in Cookies)
                sb.AppendFormat("Set-Cookie: {0}\r\n", cookie);

            sb.Append("\r\n");

            m_headers.Clear();

            return Encoding.GetBytes(sb.ToString());
        }

        public void Send()
        {
            if (Sent)
                throw new InvalidOperationException("Everything have already been sent.");

            if (m_context.MAXRequests == 0 || m_keepAlive == 0)
            {
                Connection = ConnectionType.Close;
                m_context.TimeoutKeepAlive = 0;
            }
            else
            {
                if (m_keepAlive > 0)
                    m_context.TimeoutKeepAlive = m_keepAlive * 1000;
            }

            m_headerBytes = GetHeaders();
            if (RawBuffer != null)
            {
                if (RawBufferStart < 0 || RawBufferStart > RawBuffer.Length)
                    return;

                if (RawBufferLen < 0)
                    RawBufferLen = RawBuffer.Length;

                if (RawBufferLen + RawBufferStart > RawBuffer.Length)
                    RawBufferLen = RawBuffer.Length - RawBufferStart;

                int tlen = m_headerBytes.Length + RawBufferLen;
                if(RawBufferLen > 0 && tlen < 16384)
                {
                    byte[] tmp = new byte[tlen];
                    Array.Copy(m_headerBytes, tmp, m_headerBytes.Length);
                    Array.Copy(RawBuffer, RawBufferStart, tmp, m_headerBytes.Length, RawBufferLen);
                    m_headerBytes = null;
                    RawBuffer = tmp;
                    RawBufferStart = 0;
                    RawBufferLen = tlen;
                }
            }
            m_context.StartSendResponse(this);
        }

        public async Task SendNextAsync(int bytesLimit)
        {
            if (m_headerBytes != null)
            {
                if(!await m_context.SendAsync(m_headerBytes, 0, m_headerBytes.Length).ConfigureAwait(false))
                {
                    if(m_body != null)
                        m_body.Dispose();
                    RawBuffer = null;
                    Sent = true;
                    return;
                }
                bytesLimit -= m_headerBytes.Length;
                m_headerBytes = null;
                if(bytesLimit <= 0)
                {
                    m_context.ContinueSendResponse();
                    return;
                }
            }

            if (RawBuffer != null)
            {
                if (RawBufferLen > 0)
                {
                    bool sendRes;
                    if(RawBufferLen > bytesLimit)
                    {
                        sendRes = await m_context.SendAsync(RawBuffer, RawBufferStart, bytesLimit).ConfigureAwait(false);
                        RawBufferLen -= bytesLimit;
                        RawBufferStart += bytesLimit;
                    }
                    else
                    {
                        sendRes = await m_context.SendAsync(RawBuffer, RawBufferStart, RawBufferLen).ConfigureAwait(false);
                        RawBufferLen = 0;
                    }

                    if (!sendRes)
                    {
                        RawBuffer = null;
                        if(m_body != null)
                            Body.Dispose();
                        Sent = true;
                        return;
                    }
                }
                if (RawBufferLen <= 0)
                    RawBuffer = null;
                else
                {
                    m_context.ContinueSendResponse();
                    return;
                }
            }

            if (m_body != null && m_body.Length != 0)
            {
                m_body.Flush();
                m_body.Seek(0, SeekOrigin.Begin);

                RawBuffer = new byte[m_body.Length];
                RawBufferLen = m_body.Read(RawBuffer, 0, (int)m_body.Length);
                m_body.Dispose();

                if(RawBufferLen > 0)
                {
                    bool sendRes;
                    if (RawBufferLen > bytesLimit)
                    {
                        sendRes = await m_context.SendAsync(RawBuffer, RawBufferStart, bytesLimit).ConfigureAwait(false);
                        RawBufferLen -= bytesLimit;
                        RawBufferStart += bytesLimit;
                    }
                    else
                    {
                        sendRes = await m_context.SendAsync(RawBuffer, RawBufferStart, RawBufferLen).ConfigureAwait(false);
                        RawBufferLen = 0;
                    }

                    if (!sendRes)
                    {
                        RawBuffer = null;
                        Sent = true;
                        return;
                    }
                }
                if (RawBufferLen > 0)
                {
                    m_context.ContinueSendResponse();
                    return;
                }
            }

            if (m_body != null)
                m_body.Dispose();
            Sent = true;
            m_context.ReqResponseSent(requestID, Connection);
        }

        /// <summary>
        /// Redirect client to somewhere else using the 302 status code.
        /// </summary>
        /// <param name="uri">Destination of the redirect</param>
        /// <exception cref="InvalidOperationException">If headers already been sent.</exception>
        /// <remarks>You can not do anything more with the request when a redirect have been done. This should be your last
        /// action.</remarks>
        public void Redirect(Uri uri)
        {
            Status = HttpStatusCode.Redirect;
            m_headers["location"] = uri.ToString();
        }

        /// <summary>
        /// redirect to somewhere
        /// </summary>
        /// <param name="url">where the redirect should go</param>
        /// <remarks>
        /// No body are allowed when doing redirects.
        /// </remarks>
        public void Redirect(string url)
        {
            Status = HttpStatusCode.Redirect;
            m_headers["location"] = url;
        }

        public void Clear()
        {
            if(Body != null && Body.CanRead)
                Body.Dispose();
        }
        #endregion
    }
}