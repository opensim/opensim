using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using OpenSim.Framework;
using OSHttpServer.Exceptions;


namespace OSHttpServer
{
    /// <summary>
    /// Contains server side HTTP request information.
    /// </summary>
    public class HttpRequest : IHttpRequest
    {
        private const int MAXCONTENTLENGTH = 250 * 1024 * 1024;
        /// <summary>
        /// Chars used to split an URL path into multiple parts.
        /// </summary>
        public static readonly char[] UriSplitters = new[] { '/' };
        public static uint baseID = 0;

        private readonly NameValueCollection m_headers = new();
        //private readonly HttpParam m_param = new(HttpInput.Empty, HttpInput.Empty);
        private Stream m_body = new MemoryStream();
        private int m_bodyBytesLeft;
        private ConnectionType m_connection = ConnectionType.KeepAlive;
        private int m_contentLength;
        private string m_httpVersion = string.Empty;
        private string m_method = string.Empty;
        private NameValueCollection m_queryString = null;
        private Uri m_uri = null;
        private string m_uriPath;
        public IHttpClientContext m_context;
        IPEndPoint m_remoteIPEndPoint = null;

        public HttpRequest(IHttpClientContext pContext)
        {
            ID = ++baseID;
            m_context = pContext;
        }

        public uint ID { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="HttpRequest"/> is secure.
        /// </summary>
        public bool Secure { get { return m_context.IsSecured; } }

        public IHttpClientContext Context { get { return m_context; } }
        /// <summary>
        /// Path and query (will be merged with the host header) and put in Uri
        /// </summary>
        /// <see cref="Uri"/>
        public string UriPath
        {
            get { return m_uriPath; }
            set { m_uriPath = value; }
        }

        /// <summary>
        /// Assign a form.
        /// </summary>
        /// <param name="form"></param>
        /*
        internal void AssignForm(HttpForm form)
        {
            _form = form;
        }
        */

        #region IHttpRequest Members

        /// <summary>
        /// Gets kind of types accepted by the client.
        /// </summary>
        public string[] AcceptTypes { get; private set; }

        /// <summary>
        /// Gets or sets body stream.
        /// </summary>
        public Stream Body
        {
            get { return m_body; }
            set { m_body = value; }
        }

        /// <summary>
        /// Gets or sets kind of connection used for the session.
        /// </summary>
        public ConnectionType Connection
        {
            get { return m_connection; }
            set { m_connection = value; }
        }

        /// <summary>
        /// Gets or sets number of bytes in the body.
        /// </summary>
        public int ContentLength
        {
            get { return m_contentLength; }
            set
            {
                m_contentLength = value;
                m_bodyBytesLeft = value;
            }
        }

        /// <summary>
        /// Gets headers sent by the client.
        /// </summary>
        public NameValueCollection Headers
        {
            get { return m_headers; }
        }

        /// <summary>
        /// Gets or sets version of HTTP protocol that's used.
        /// </summary>
        /// <remarks>
        /// Probably <see cref="HttpHelper.HTTP10"/> or <see cref="HttpHelper.HTTP11"/>.
        /// </remarks>
        /// <seealso cref="HttpHelper"/>
        public string HttpVersion
        {
            get { return m_httpVersion; }
            set { m_httpVersion = value; }
        }

        /// <summary>
        /// Gets or sets requested method.
        /// </summary>
        /// <value></value>
        /// <remarks>
        /// Will always be in upper case.
        /// </remarks>
        /// <see cref="OSHttpServer.Method"/>
        public string Method
        {
            get { return m_method; }
            set { m_method = value; }
        }

        /// <summary>
        /// Gets variables sent in the query string
        /// </summary>
        public NameValueCollection QueryString
        {
            get
            {
                if(m_queryString is null)
                {
                    if(m_uri is null || m_uri.Query.Length == 0)
                        m_queryString = new NameValueCollection();
                    else
                    {
                        try
                        {
                            m_queryString = HttpUtility.ParseQueryString(m_uri.Query);
                        }
                        catch { m_queryString = new NameValueCollection(); }
                    }
                }

            return m_queryString;
            }
        }

        public static readonly Uri EmptyUri = new("http://localhost/");
        /// <summary>
        /// Gets or sets requested URI.
        /// </summary>
        public Uri Uri
        {
            get { return m_uri; }
            set { m_uri = value ?? EmptyUri; } // not safe
        }

        /*
        /// <summary>
        /// Gets parameter from <see cref="QueryString"/> or <see cref="Form"/>.
        /// </summary>
        public HttpParam Param
        {
            get { return m_param; }
        }
        */

        /// <summary>
        /// Gets form parameters.
        /// </summary>
        /*
        public HttpForm Form
        {
            get { return _form; }
        }
        */
        /// <summary>
        /// Gets whether the request was made by Ajax (Asynchronous JavaScript)
        /// </summary>
        public bool IsAjax { get; private set; }

        /// <summary>
        /// Gets cookies that was sent with the request.
        /// </summary>
        public RequestCookies Cookies { get; private set; }

        public double ArrivalTS { get; set;}
        ///<summary>
        ///Creates a new object that is a copy of the current instance.
        ///</summary>
        ///
        ///<returns>
        ///A new object that is a copy of this instance.
        ///</returns>
        ///<filterpriority>2</filterpriority>
        public object Clone()
        {
            // this method was mainly created for testing.
            // dont use it that much...
            var request = new HttpRequest(Context)
            {
                Method = m_method,
                m_httpVersion = m_httpVersion,
                m_queryString = m_queryString,
                Uri = m_uri
            };
            if (AcceptTypes != null)
            {
                request.AcceptTypes = new string[AcceptTypes.Length];
                AcceptTypes.CopyTo(request.AcceptTypes, 0);
            }

            var buffer = new byte[m_body.Length];
            m_body.Read(buffer, 0, (int)m_body.Length);
            request.Body = new MemoryStream();
            request.Body.Write(buffer, 0, buffer.Length);
            request.Body.Seek(0, SeekOrigin.Begin);
            request.Body.Flush();

            request.m_headers.Clear();
            foreach (string key in m_headers)
            {
                string[] values = m_headers.GetValues(key);
                if (values != null)
                    foreach (string value in values)
                        request.AddHeader(key, value);
            }
            return request;
        }

        /// <summary>
        /// Decode body into a form.
        /// </summary>
        /// <param name="providers">A list with form decoders.</param>
        /// <exception cref="InvalidDataException">If body contents is not valid for the chosen decoder.</exception>
        /// <exception cref="InvalidOperationException">If body is still being transferred.</exception>
        /*
        public void DecodeBody(FormDecoderProvider providers)
        {
            if (_bodyBytesLeft > 0)
                throw new InvalidOperationException("Body have not yet been completed.");

            _form = providers.Decode(_headers["content-type"], _body, Encoding.UTF8);
            if (_form != HttpInput.Empty)
                _param.SetForm(_form);
        }
        */
        ///<summary>
        /// Cookies
        ///</summary>
        ///<param name="cookies">the cookies</param>
        public void SetCookies(RequestCookies cookies)
        {
            Cookies = cookies;
        }

        public IPEndPoint LocalIPEndPoint { get {return m_context.LocalIPEndPoint; }}

        public IPEndPoint RemoteIPEndPoint
        {
            get
            {
                if(m_remoteIPEndPoint == null)
                {
                    string addr = m_headers["x-forwarded-for"];
                    if(!string.IsNullOrEmpty(addr))
                    {
                        int port = m_context.LocalIPEndPoint.Port;
                        try
                        {
                            m_remoteIPEndPoint = new IPEndPoint(IPAddress.Parse(addr), port);
                        }
                        catch
                        {
                            m_remoteIPEndPoint = null;
                        }
                    }
                }
                m_remoteIPEndPoint ??= m_context.LocalIPEndPoint;

                return m_remoteIPEndPoint;
            }
        }
        /*
        /// <summary>
        /// Create a response object.
        /// </summary>
        /// <returns>A new <see cref="IHttpResponse"/>.</returns>
        public IHttpResponse CreateResponse(IHttpClientContext context)
        {
            return new HttpResponse(context, this);
        }
        */
        /// <summary>
        /// Called during parsing of a <see cref="IHttpRequest"/>.
        /// </summary>
        /// <param name="name">Name of the header, should not be URL encoded</param>
        /// <param name="value">Value of the header, should not be URL encoded</param>
        /// <exception cref="BadRequestException">If a header is incorrect.</exception>
        public void AddHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
                throw new BadRequestException("Invalid header name: " + name ?? "<null>");
            if (string.IsNullOrEmpty(value))
                throw new BadRequestException("Header '" + name + "' do not contain a value.");

            name = name.ToLowerInvariant();

            switch (name)
            {
                case "http_x_requested_with":
                case "x-requested-with":
                    if (string.Compare(value, "XMLHttpRequest", true) == 0)
                        IsAjax = true;
                    break;
                case "accept":
                    AcceptTypes = value.Split(',');
                    for (int i = 0; i < AcceptTypes.Length; ++i)
                        AcceptTypes[i] = AcceptTypes[i].Trim();
                    break;
                case "content-length":
                    if (!int.TryParse(value, out int t))
                        throw new BadRequestException("Invalid content length.");
                    if (t > MAXCONTENTLENGTH)
                        throw new OSHttpServer.Exceptions.HttpException(HttpStatusCode.RequestEntityTooLarge,"Request Entity Too Large");
                    ContentLength = t;
                    break;
                case "host":
                    try
                    {
                        m_uri = new Uri((Secure ? "https://" : "http://") + value + m_uriPath);
                        m_uriPath = m_uri.AbsolutePath;
                    }
                    catch (UriFormatException err)
                    {
                        throw new BadRequestException("Failed to parse uri: " + value + m_uriPath, err);
                    }
                    break;
                case "remote_addr":
                    if (m_headers[name] == null)
                        m_headers.Add(name, value);
                    break;

                case "forwarded":
                    string[] parts = value.Split(Util.SplitSemicolonArray);
                    string addr = string.Empty;
                    for(int i = 0; i < parts.Length; ++i)
                    {
                        string s = parts[i].TrimStart();
                        if(s.Length < 10)
                            continue;
                        if(s.StartsWith("for", StringComparison.InvariantCultureIgnoreCase))
                        {
                            int indx = s.IndexOf("=", 3);
                            if(indx < 0 || indx >= s.Length - 1)
                                continue;
                            s = s[indx..];
                            addr = s.Trim();
                        }
                    }
                    if(addr.Length > 7)
                    {
                        m_headers.Add("x-forwarded-for", addr);
                    }
                    break;
                case "x-forwarded-for":
                    if (value.Length > 7)
                    {
                        string[] xparts = value.Split(Util.SplitCommaArray);
                        if(xparts.Length > 0)
                        {
                            string xs = xparts[0].Trim();
                            if(xs.Length > 7)
                                m_headers.Add("x-forwarded-for", xs);
                        }
                    }
                    break;
                case "connection":
                    if (string.Compare(value, "close", true) == 0)
                        Connection = ConnectionType.Close;
                    else if (value.StartsWith("keep-alive", StringComparison.CurrentCultureIgnoreCase))
                        Connection = ConnectionType.KeepAlive;
                    else if (value.StartsWith("Upgrade", StringComparison.CurrentCultureIgnoreCase))
                        Connection = ConnectionType.KeepAlive;
                    else
                        throw new BadRequestException("Unknown 'Connection' header type.");
                    break;

                /*
                case "expect":
                    if (value.Contains("100-continue"))
                    {

                    }
                    m_headers.Add(name, value);
                    break;
                case "user-agent":

                    break;
                */
                default:
                    m_headers.Add(name, value);
                    break;
            }
        }

        /// <summary>
        /// Add bytes to the body
        /// </summary>
        /// <param name="bytes">buffer to read bytes from</param>
        /// <param name="offset">where to start read</param>
        /// <param name="length">number of bytes to read</param>
        /// <returns>Number of bytes actually read (same as length unless we got all body bytes).</returns>
        /// <exception cref="InvalidOperationException">If body is not writable</exception>
        /// <exception cref="ArgumentNullException"><c>bytes</c> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><c>offset</c> is out of range.</exception>
        public int AddToBody(byte[] bytes, int offset, int length)
        {
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            if (offset + length > bytes.Length)
                throw new ArgumentOutOfRangeException("offset");
            if (length == 0)
                return 0;
            if (!m_body.CanWrite)
                throw new InvalidOperationException("Body is not writable.");

            if (length > m_bodyBytesLeft)
            {
                length = m_bodyBytesLeft;
            }

            m_body.Write(bytes, offset, length);
            m_bodyBytesLeft -= length;

            return length;
        }

        /// <summary>
        /// Clear everything in the request
        /// </summary>
        public void Clear()
        {
            if (m_body != null)
            {
                m_body.Dispose();
                m_body = null;
            }
            m_contentLength = 0;
            m_method = string.Empty;
            m_uri = null;
            m_queryString = null;
            m_bodyBytesLeft = 0;
            m_headers.Clear();
            m_connection = ConnectionType.KeepAlive;
            IsAjax = false;
            m_context = null;
            //_form.Clear();
        }

        #endregion
    }
}