using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using OpenMetaverse;

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
        public double RequestTS { get; private set; }

        internal byte[] m_headerBytes = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="IHttpResponse"/> class.
        /// </summary>
        /// <param name="context">Client that send the <see cref="IHttpRequest"/>.</param>
        /// <param name="request">Contains information of what the client want to receive.</param>
        /// <exception cref="ArgumentException"><see cref="IHttpRequest.HttpVersion"/> cannot be empty.</exception>
        public HttpResponse(IHttpRequest request)
        {
            m_httpVersion = request.HttpVersion;
            if (string.IsNullOrEmpty(m_httpVersion))
                m_httpVersion = "HTTP/1.1";

            Status = HttpStatusCode.OK;
            m_context = request.Context;
            m_Connetion = request.Connection;
            requestID = request.ID;
            RequestTS = request.ArrivalTS;
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
            set { m_Connetion = value; }
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
        /// </summary>
        public long ContentLength
        {
            get { return m_contentLength; }
            set { m_contentLength = value; }
        }

        /// <summary>
        /// Kind of content
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
        /// Set response as a http redirect
        /// </summary>
        /// <param name="url">redirection target url</param>
        /// <param name="redirStatusCode">the response Status, must be Found, Redirect, Moved,MovedPermanently,RedirectKeepVerb, RedirectMethod, TemporaryRedirect. Defaults to Redirect</param>
        public void Redirect(string url, HttpStatusCode redirStatusCode = HttpStatusCode.Redirect)
        {
            if (HeadersSent)
                throw new InvalidOperationException("Headers have already been sent.");

            m_headers["Location"] = url;
            Status = redirStatusCode;
        }

        /// <summary>
        /// Add another header to the document.
        /// </summary>
        /// <param name="name">Name of the header, case sensitive.</param>
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

        public byte[] GetHeaders()
        {
            HeadersSent = true;

            var sb = osStringBuilderCache.Acquire();

            if(string.IsNullOrWhiteSpace(m_httpVersion))
                sb.AppendFormat("HTTP/1.1 {0} {1}\r\n", (int)Status,
                                string.IsNullOrEmpty(Reason) ? Status.ToString() : Reason);
            else
                sb.AppendFormat("{0} {1} {2}\r\n", m_httpVersion, (int)Status,
                                string.IsNullOrEmpty(Reason) ? Status.ToString() : Reason);

            sb.AppendFormat("Date: {0}\r\n", DateTime.Now.ToString("r"));

            long len = 0;
            if(m_body!= null)
                len = m_body.Length;
            if (RawBuffer != null && RawBufferLen > 0)
                len += RawBufferLen;
            sb.AppendFormat("Content-Length: {0}\r\n", len);

            if (m_headers["Content-Type"] == null)
                sb.AppendFormat("Content-Type: {0}\r\n", m_contentType ?? DefaultContentType);

            switch(Status)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.PartialContent:
                case HttpStatusCode.Accepted:
                case HttpStatusCode.Continue:
                case HttpStatusCode.Found:
                {
                    int keepaliveS = m_context.TimeoutKeepAlive / 1000;
                    if (Connection == ConnectionType.KeepAlive && keepaliveS > 0 && m_context.MaxRequests > 0)
                    {
                        sb.AppendFormat("Keep-Alive:timeout={0}, max={1}\r\n", keepaliveS, m_context.MaxRequests);
                        sb.Append("Connection: Keep-Alive\r\n");
                    }
                    else
                    {
                        sb.Append("Connection: close\r\n");
                        Connection = ConnectionType.Close;
                    }
                    break;
                }

                default:
                    sb.Append("Connection: close\r\n");
                    Connection = ConnectionType.Close;
                    break;
            }

            for (int i = 0; i < m_headers.Count; ++i)
            {
                string headerName = m_headers.AllKeys[i];
                switch(headerName)
                {
                    case "Connection":
                    case "Content-Length":
                    case "Date":
                    case "Keep-Alive":
                    case "Server":
                        continue;
                }
                string[] values = m_headers.GetValues(i);
                if (values == null) continue;
                foreach (string value in values)
                    sb.AppendFormat("{0}: {1}\r\n", headerName, value);
            }

            sb.Append("Server: OSWebServer\r\n");

            foreach (ResponseCookie cookie in Cookies)
                sb.AppendFormat("Set-Cookie: {0}\r\n", cookie);

            sb.Append("\r\n");

            m_headers.Clear();

            return Encoding.GetBytes(osStringBuilderCache.GetStringAndRelease(sb));
        }

        public void Send()
        {
            if(m_context.IsClosing)
                return;

            if (Sent)
                throw new InvalidOperationException("Everything have already been sent.");

            if (m_context.MaxRequests == 0 || m_keepAlive == 0)
            {
                Connection = ConnectionType.Close;
                m_context.TimeoutKeepAlive = 0;
            }
            else
            {
                if (m_keepAlive > 0)
                    m_context.TimeoutKeepAlive = m_keepAlive * 1000;
            }

            if (RawBuffer != null)
            {
                if (RawBufferStart > RawBuffer.Length)
                    return;

                if (RawBufferStart < 0)
                    RawBufferStart = 0;

                if (RawBufferLen < 0)
                    RawBufferLen = RawBuffer.Length;

                if (RawBufferLen + RawBufferStart > RawBuffer.Length)
                    RawBufferLen = RawBuffer.Length - RawBufferStart;
            }

            m_headerBytes = GetHeaders();

            if (RawBuffer != null && RawBufferLen > 0)
            {
                int tlen = m_headerBytes.Length + RawBufferLen;
                if(tlen < 8 * 1024)
                {
                    byte[] tmp = new byte[tlen];
                    Buffer.BlockCopy(m_headerBytes, 0, tmp, 0, m_headerBytes.Length);
                    Buffer.BlockCopy(RawBuffer, RawBufferStart, tmp, m_headerBytes.Length, RawBufferLen);
                    m_headerBytes = null;
                    RawBuffer = tmp;
                    RawBufferStart = 0;
                    RawBufferLen = tlen;
                }

                if (RawBufferLen == 0)
                    RawBuffer = null;
            }

            if (m_body != null && m_body.Length == 0)
            {
                m_body.Dispose();
                m_body = null;
            }

            if (m_headerBytes == null && RawBuffer == null && m_body == null)
            {
                Sent = true;
                m_context.EndSendResponse(requestID, Connection);
            }
            else
                m_context.StartSendResponse(this);
        }

        public bool SendNextAsync(int bytesLimit)
        {
            if (m_headerBytes != null)
            {
                byte[] b = m_headerBytes;
                m_headerBytes = null;

                if (!m_context.SendAsyncStart(b, 0, b.Length))
                {
                    if (m_body != null)
                    {
                        m_body.Dispose();
                        m_body = null;
                    }
                    RawBuffer = null;
                    Sent = true;
                    return false;
                }
                return true;
            }

            bool sendRes;
            if (RawBuffer != null)
            {
                if(RawBufferLen > 0)
                {
                    byte[] b = RawBuffer;
                    int s = RawBufferStart;

                    if (RawBufferLen > bytesLimit)
                    {
                        RawBufferLen -= bytesLimit;
                        RawBufferStart += bytesLimit;
                        if (RawBufferLen <= 0)
                            RawBuffer = null;
                        sendRes = m_context.SendAsyncStart(b, s, bytesLimit);
                    }
                    else
                    {
                        int l = RawBufferLen;
                        RawBufferLen = 0;
                        RawBuffer = null;
                        sendRes = m_context.SendAsyncStart(b, s, l);
                    }

                    if (!sendRes)
                    {
                        RawBuffer = null;
                        if(m_body != null)
                        {
                            m_body.Dispose();
                            m_body = null;
                        }
                        Sent = true;
                        return false;
                    }
                    return true;
                }
                else
                    RawBuffer = null;
            }

            if (m_body != null)
            {
                if(m_body.Length != 0)
                {
                    MemoryStream mb = m_body as MemoryStream;
                    RawBuffer = mb.GetBuffer();
                    RawBufferStart = 0; // must be a internal buffer, or starting at 0
                    RawBufferLen = (int)mb.Length;
                    m_body.Dispose();
                    m_body = null;

                    if (RawBufferLen > 0)
                    {
                        byte[] b = RawBuffer;
                        int s = RawBufferStart;

                        if (RawBufferLen > bytesLimit)
                        {
                            RawBufferLen -= bytesLimit;
                            RawBufferStart += bytesLimit;
                            if (RawBufferLen <= 0)
                                RawBuffer = null;
                            sendRes = m_context.SendAsyncStart(b, s, bytesLimit);
                        }
                        else
                        {
                            int l = RawBufferLen;
                            sendRes = m_context.SendAsyncStart(b, s, l);
                            RawBufferLen = 0;
                            RawBuffer = null;
                        }

                        if (!sendRes)
                        {
                            RawBuffer = null;
                            Sent = true;
                            return false;
                        }
                        return true; 
                    }
                    else
                        RawBuffer = null;
                }
                else
                {
                    m_body.Dispose();
                    m_body = null;
                }
            }

            Sent = true;
            m_context.EndSendResponse(requestID, Connection);
            return false;
        }

        public void CheckSendNextAsyncContinue()
        {
            if(m_headerBytes == null && RawBuffer == null && m_body == null)
            {
                Sent = true;
                m_context.EndSendResponse(requestID, Connection);
            }
            else
            {
                m_context.ContinueSendResponse();
            }
        }

        public void Clear()
        {
            if(m_body != null && m_body.CanRead)
                m_body.Dispose();
        }
        #endregion
    }
}