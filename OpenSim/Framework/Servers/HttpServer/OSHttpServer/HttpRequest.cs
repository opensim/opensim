using System;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using OSHttpServer.Exceptions;


namespace OSHttpServer
{
    /// <summary>
    /// Contains server side HTTP request information.
    /// </summary>
    public class HttpRequest : IHttpRequest
    {
        /// <summary>
        /// Chars used to split an URL path into multiple parts.
        /// </summary>
        public static readonly char[] UriSplitters = new[] { '/' };
        public static uint baseID = 0;

        private readonly NameValueCollection m_headers = new NameValueCollection();
        private readonly HttpParam m_param = new HttpParam(HttpInput.Empty, HttpInput.Empty);
        private Stream m_body = new MemoryStream();
        private int m_bodyBytesLeft;
        private ConnectionType m_connection = ConnectionType.KeepAlive;
        private int m_contentLength;
        private string m_httpVersion = string.Empty;
        private string m_method = string.Empty;
        private HttpInput m_queryString = HttpInput.Empty;
        private Uri m_uri = HttpHelper.EmptyUri;
        private string m_uriPath;
        public readonly IHttpClientContext m_context;

        public HttpRequest(IHttpClientContext pContext)
        {
            ID = ++baseID;
            m_context = pContext;
        }

        public uint ID { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="HttpRequest"/> is secure.
        /// </summary>
        public bool Secure { get; internal set; }

        public IHttpClientContext Context { get { return m_context; } }
        /// <summary>
        /// Path and query (will be merged with the host header) and put in Uri
        /// </summary>
        /// <see cref="Uri"/>
        public string UriPath
        {
            get { return m_uriPath; }
            set
            {
                m_uriPath = value;
                int pos = m_uriPath.IndexOf('?');
                if (pos != -1)
                {
                    m_queryString = HttpHelper.ParseQueryString(m_uriPath.Substring(pos + 1));
                    m_param.SetQueryString(m_queryString);
                    string path = m_uriPath.Substring(0, pos);
                    m_uriPath = System.Web.HttpUtility.UrlDecode(path) + "?" + m_uriPath.Substring(pos + 1);
                    UriParts = value.Substring(0, pos).Split(UriSplitters, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    m_uriPath = System.Web.HttpUtility.UrlDecode(m_uriPath);
                    UriParts = value.Split(UriSplitters, StringSplitOptions.RemoveEmptyEntries);
                }
            }
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
        /// Gets whether the body is complete.
        /// </summary>
        public bool BodyIsComplete
        {
            get { return m_bodyBytesLeft == 0; }
        }

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
        public HttpInput QueryString
        {
            get { return m_queryString; }
        }


        /// <summary>
        /// Gets or sets requested URI.
        /// </summary>
        public Uri Uri
        {
            get { return m_uri; }
            set
            {
                m_uri = value ?? HttpHelper.EmptyUri;
                UriParts = m_uri.AbsolutePath.Split(UriSplitters, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        /// <summary>
        /// Uri absolute path splitted into parts.
        /// </summary>
        /// <example>
        /// // uri is: http://gauffin.com/code/tiny/
        /// Console.WriteLine(request.UriParts[0]); // result: code
        /// Console.WriteLine(request.UriParts[1]); // result: tiny
        /// </example>
        /// <remarks>
        /// If you're using controllers than the first part is controller name,
        /// the second part is method name and the third part is Id property.
        /// </remarks>
        /// <seealso cref="Uri"/>
        public string[] UriParts { get; private set; }

        /// <summary>
        /// Gets parameter from <see cref="QueryString"/> or <see cref="Form"/>.
        /// </summary>
        public HttpParam Param
        {
            get { return m_param; }
        }

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
            var request = new HttpRequest(Context);
            request.Method = m_method;
            if (AcceptTypes != null)
            {
                request.AcceptTypes = new string[AcceptTypes.Length];
                AcceptTypes.CopyTo(request.AcceptTypes, 0);
            }
            request.m_httpVersion = m_httpVersion;
            request.m_queryString = m_queryString;
            request.Uri = m_uri;

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
                    int t;
                    if (!int.TryParse(value, out t))
                        throw new BadRequestException("Invalid content length.");
                    ContentLength = t;
                    break; //todo: maybe throw an exception
                case "host":
                    try
                    {
                        m_uri = new Uri(Secure ? "https://" : "http://" + value + m_uriPath);
                        UriParts = m_uri.AbsolutePath.Split(UriSplitters, StringSplitOptions.RemoveEmptyEntries);
                    }
                    catch (UriFormatException err)
                    {
                        throw new BadRequestException("Failed to parse uri: " + value + m_uriPath, err);
                    }
                    break;
                case "remote_addr":
                    // to prevent hacking (since it's added by IHttpClientContext before parsing).
                    if (m_headers[name] == null)
                        m_headers.Add(name, value);
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

                case "expect":
                    if (value.Contains("100-continue"))
                    {

                    }
                    m_headers.Add(name, value);
                    break;

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
            if (m_body != null && m_body.CanRead)
                m_body.Dispose();
            m_body = null;
            m_contentLength = 0;
            m_method = string.Empty;
            m_uri = HttpHelper.EmptyUri;
            m_queryString = HttpInput.Empty;
            m_bodyBytesLeft = 0;
            m_headers.Clear();
            m_connection = ConnectionType.KeepAlive;
            IsAjax = false;
            //_form.Clear();
        }

        #endregion
    }
}