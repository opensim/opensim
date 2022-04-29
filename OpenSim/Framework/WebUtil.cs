/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Linq;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;

namespace OpenSim.Framework
{
    /// <summary>
    /// Miscellaneous static methods and extension methods related to the web
    /// </summary>
    /// 

    public static class WebUtil
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static ExpiringKey<string> GlobalExpiringBadURLs = new ExpiringKey<string>(30000);
        /// <summary>
        /// Control the printing of certain debug messages.
        /// </summary>
        /// <remarks>
        /// If DebugLevel >= 3 then short notices about outgoing HTTP requests are logged.
        /// </remarks>
        public static int DebugLevel { get; set; }

        /// <summary>
        /// Request number for diagnostic purposes.
        /// </summary>
        public static int RequestNumber { get; set; }

        /// <summary>
        /// this is the header field used to communicate the local request id
        /// used for performance and debugging
        /// </summary>
        public const string OSHeaderRequestID = "opensim-request-id";

        /// <summary>
        /// Number of milliseconds a call can take before it is considered
        /// a "long" call for warning & debugging purposes
        /// </summary>
        public const int LongCallTime = 3000;

        /// <summary>
        /// The maximum length of any data logged because of a long request time.
        /// </summary>
        /// <remarks>
        /// This is to truncate any really large post data, such as an asset.  In theory, the first section should
        /// give us useful information about the call (which agent it relates to if applicable, etc.).
        /// This is also used to truncate messages when using DebugLevel 5.
        /// </remarks>
        public const int MaxRequestDiagLength = 200;

        public static bool ValidateServerCertificateNoChecks(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateChainErrors;
            sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            return false;
        }
        #region JSONRequest

        /// <summary>
        /// PUT JSON-encoded data to a web service that returns LLSD or
        /// JSON data
        /// </summary>
        public static OSDMap PutToServiceCompressed(string url, OSDMap data, int timeout)
        {
            return ServiceOSDRequest(url, data, "PUT", timeout, true, false);
        }

        public static OSDMap PutToService(string url, OSDMap data, int timeout)
        {
            return ServiceOSDRequest(url, data, "PUT", timeout, false, false);
        }

        public static OSDMap PostToService(string url, OSDMap data, int timeout, bool rpc)
        {
            return ServiceOSDRequest(url, data, "POST", timeout, false, rpc);
        }

        public static OSDMap PostToServiceCompressed(string url, OSDMap data, int timeout)
        {
            return ServiceOSDRequest(url, data, "POST", timeout, true, false);
        }

        public static OSDMap GetFromService(string url, int timeout)
        {
            return ServiceOSDRequest(url, null, "GET", timeout, false, false);
        }

        public static void LogOutgoingDetail(Stream outputStream)
        {
            LogOutgoingDetail("", outputStream);
        }

        public static void LogOutgoingDetail(string context, Stream outputStream)
        {
            using (Stream stream = Util.Copy(outputStream))
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                string output;

                if (DebugLevel == 5)
                {
                    char[] chars = new char[WebUtil.MaxRequestDiagLength + 1];  // +1 so we know to add "..." only if needed
                    int len = reader.Read(chars, 0, WebUtil.MaxRequestDiagLength + 1);
                    output = new string(chars, 0, len);
                }
                else
                {
                    output = reader.ReadToEnd();
                }

                LogOutgoingDetail(context, output);
            }
        }

        public static void LogOutgoingDetail(string type, int reqnum, string output)
        {
            LogOutgoingDetail(string.Format("{0} {1}: ", type, reqnum), output);
        }

        public static void LogOutgoingDetail(string context, string output)
        {
            if (DebugLevel == 5)
            {
                if (output.Length > MaxRequestDiagLength)
                    output = output.Substring(0, MaxRequestDiagLength) + "...";
            }

            m_log.DebugFormat("[LOGHTTP]: {0}{1}", context, Util.BinaryToASCII(output));
        }

        public static void LogResponseDetail(int reqnum, Stream inputStream)
        {
            LogOutgoingDetail(string.Format("RESPONSE {0}: ", reqnum), inputStream);
        }

        public static void LogResponseDetail(int reqnum, string input)
        {
            LogOutgoingDetail(string.Format("RESPONSE {0}: ", reqnum), input);
        }

        public static OSDMap ServiceOSDRequest(string url, OSDMap data, string method, int timeout, bool compressed, bool rpc, bool keepalive = false)
        {
            int reqnum = RequestNumber++;

            if (DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} JSON-RPC {1} to {2}",
                    reqnum, method, url);

            string errorMessage = "unknown error";
            int tickstart = Util.EnvironmentTickCount();
            int sendlen = 0;
            int rcvlen = 0;
            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.Timeout = timeout;
                request.KeepAlive = keepalive;
                request.MaximumAutomaticRedirections = 10;
                request.ReadWriteTimeout = timeout / 2;
                request.Headers[OSHeaderRequestID] = reqnum.ToString();
                request.AllowWriteStreamBuffering = false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                m_log.Debug("[WEB UTIL]: SvcOSD error creating request " + ex.Message);
                return ErrorResponseMap(errorMessage);
            }

            try
            {
                // If there is some input, write it into the request
                if (data != null)
                {
                    byte[] buffer;
                    if (DebugLevel >= 5)
                    {
                        string strBuffer = OSDParser.SerializeJsonString(data);
                        LogOutgoingDetail(method, reqnum, strBuffer);
                        buffer = Util.UTF8Getbytes(strBuffer);
                    }
                    else
                        buffer = OSDParser.SerializeJsonToBytes(data);

                    request.ContentType = rpc ? "application/json-rpc" : "application/json";

                    if (compressed)
                    {
                        request.Headers["X-Content-Encoding"] = "gzip"; // can't set "Content-Encoding" because old OpenSims fail if they get an unrecognized Content-Encoding

                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (GZipStream comp = new GZipStream(ms, CompressionMode.Compress, true))
                            {
                                comp.Write(buffer, 0, buffer.Length);
                             }
                            buffer = ms.ToArray();
                        }
                    }

                    sendlen = buffer.Length;
                    request.ContentLength = buffer.Length;   //Count bytes to send
                    using (Stream requestStream = request.GetRequestStream())
                        requestStream.Write(buffer, 0, buffer.Length);         //Send it
                    buffer = null;
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string responseStr = reader.ReadToEnd();
                        if (WebUtil.DebugLevel >= 5)
                            WebUtil.LogResponseDetail(reqnum, responseStr);
                        rcvlen = responseStr.Length;
                        return CanonicalizeResults(responseStr);
                    }
                }
            }
            catch (WebException we)
            {
                errorMessage = we.Message;
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    using (HttpWebResponse webResponse = (HttpWebResponse)we.Response)
                        errorMessage = String.Format("[{0}] {1}", webResponse.StatusCode, webResponse.StatusDescription);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                m_log.Debug("[WEB UTIL]: Exception making request: " + ex.ToString());
            }
            finally
            {
                int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
                if (tickdiff > LongCallTime)
                {
                    m_log.InfoFormat(
                        "[WEB UTIL]: SvcOSD {0} {1} {2} took {3}ms, {4}/{5}bytes",
                        reqnum, method, url, tickdiff, sendlen, rcvlen );
                }
                else if (DebugLevel >= 4)
                {
                    m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms",
                        reqnum, tickdiff);
                }
            }

            m_log.DebugFormat("[LOGHTTP]: JSON request {0} {1} to {2} FAILED: {3}", reqnum, method, url, errorMessage);

            return ErrorResponseMap(errorMessage);
        }

        /// <summary>
        /// Since there are no consistencies in the way web requests are
        /// formed, we need to do a little guessing about the result format.
        /// Keys:
        ///     Success|success == the success fail of the request
        ///     _RawResult == the raw string that came back
        ///     _Result == the OSD unpacked string
        /// </summary>
        private static OSDMap CanonicalizeResults(string response)
        {
            OSDMap result = new OSDMap();

            // Default values
            result["Success"] = OSD.FromBoolean(true);
            result["success"] = OSD.FromBoolean(true);
            result["_RawResult"] = OSD.FromString(response);
            result["_Result"] = new OSDMap();

            if (response.Equals("true", StringComparison.OrdinalIgnoreCase))
                return result;

            if (response.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                result["Success"] = OSD.FromBoolean(false);
                result["success"] = OSD.FromBoolean(false);
                return result;
            }

            try
            {
                OSD responseOSD = OSDParser.Deserialize(response);
                if (responseOSD.Type == OSDType.Map)
                {
                    result["_Result"] = (OSDMap)responseOSD;
                    return result;
                }
            }
            catch
            {
                // don't need to treat this as an error... we're just guessing anyway
//                m_log.DebugFormat("[WEB UTIL] couldn't decode <{0}>: {1}",response,e.Message);
            }

            return result;
        }

        #endregion JSONRequest

        #region FormRequest

        /// <summary>
        /// POST URL-encoded form data to a web service that returns LLSD or
        /// JSON data
        /// </summary>
        public static OSDMap PostToService(string url, NameValueCollection data)
        {
            return ServiceFormRequest(url,data, 30000);
        }

        public static OSDMap ServiceFormRequest(string url, NameValueCollection data, int timeout)
        {
            int reqnum = RequestNumber++;
            string method = (data != null && data["RequestMethod"] != null) ? data["RequestMethod"] : "unknown";

            if (DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} ServiceForm '{1}' to {2}",
                    reqnum, method, url);

            string errorMessage = "unknown error";
            int tickstart = Util.EnvironmentTickCount();
            int sendlen = 0;
            int rcvlen = 0;

            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.Timeout = timeout;
                request.KeepAlive = false;
                request.MaximumAutomaticRedirections = 10;
                request.ReadWriteTimeout = timeout / 2;
                request.Headers[OSHeaderRequestID] = reqnum.ToString();
                request.AllowWriteStreamBuffering = false;
            }
            catch (Exception ex)
            {
                return ErrorResponseMap(ex.Message);
            }

            try
            {
                if (data != null)
                {
                    string queryString = BuildQueryString(data);

                    if (DebugLevel >= 5)
                        LogOutgoingDetail("SEND", reqnum, queryString);

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(queryString);
                    queryString = null;

                    request.ContentLength = buffer.Length;
                    sendlen = buffer.Length;
                    request.ContentType = "application/x-www-form-urlencoded";
                    using (Stream requestStream = request.GetRequestStream())
                        requestStream.Write(buffer, 0, buffer.Length);
                    buffer = null;
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string responseStr = reader.ReadToEnd();
                        rcvlen = responseStr.Length;
                        if (DebugLevel >= 5)
                            LogResponseDetail(reqnum, responseStr);
                        OSD responseOSD = OSDParser.Deserialize(responseStr);

                        if (responseOSD.Type == OSDType.Map)
                            return (OSDMap)responseOSD;
                    }
                }
            }
            catch (WebException we)
            {
                errorMessage = we.Message;
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    using (HttpWebResponse webResponse = (HttpWebResponse)we.Response)
                        errorMessage = String.Format("[{0}] {1}",webResponse.StatusCode,webResponse.StatusDescription);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
            finally
            {
                int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
                if (tickdiff > LongCallTime)
                {
                    m_log.InfoFormat(
                        "[LOGHTTP]: Slow ServiceForm request {0} '{1}' to {2} took {3}ms, {4}/{5}bytes",
                        reqnum, method, url, tickdiff, sendlen, rcvlen);
                }
                else if (DebugLevel >= 4)
                {
                    m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms",
                        reqnum, tickdiff);
                }
            }

            m_log.WarnFormat("[LOGHTTP]: ServiceForm request {0} '{1}' to {2} failed: {3}", reqnum, method, url, errorMessage);

            return ErrorResponseMap(errorMessage);
        }

        /// <summary>
        /// Create a response map for an error, trying to keep
        /// the result formats consistent
        /// </summary>
        private static OSDMap ErrorResponseMap(string msg)
        {
            OSDMap result = new OSDMap();
            result["Success"] = "False";
            result["Message"] = OSD.FromString("Service request failed: " + msg);
            return result;
        }

        #endregion FormRequest

        #region Uri

        /// <summary>
        /// Combines a Uri that can contain both a base Uri and relative path
        /// with a second relative path fragment
        /// </summary>
        /// <param name="uri">Starting (base) Uri</param>
        /// <param name="fragment">Relative path fragment to append to the end
        /// of the Uri</param>
        /// <returns>The combined Uri</returns>
        /// <remarks>This is similar to the Uri constructor that takes a base
        /// Uri and the relative path, except this method can append a relative
        /// path fragment on to an existing relative path</remarks>
        public static Uri Combine(this Uri uri, string fragment)
        {
            string fragment1 = uri.Fragment;
            string fragment2 = fragment;

            if (!fragment1.EndsWith("/"))
                fragment1 = fragment1 + '/';
            if (fragment2.StartsWith("/"))
                fragment2 = fragment2.Substring(1);

            return new Uri(uri, fragment1 + fragment2);
        }

        /// <summary>
        /// Combines a Uri that can contain both a base Uri and relative path
        /// with a second relative path fragment. If the fragment is absolute,
        /// it will be returned without modification
        /// </summary>
        /// <param name="uri">Starting (base) Uri</param>
        /// <param name="fragment">Relative path fragment to append to the end
        /// of the Uri, or an absolute Uri to return unmodified</param>
        /// <returns>The combined Uri</returns>
        public static Uri Combine(this Uri uri, Uri fragment)
        {
            if (fragment.IsAbsoluteUri)
                return fragment;

            string fragment1 = uri.Fragment;
            string fragment2 = fragment.ToString();

            if (!fragment1.EndsWith("/"))
                fragment1 = fragment1 + '/';
            if (fragment2.StartsWith("/"))
                fragment2 = fragment2.Substring(1);

            return new Uri(uri, fragment1 + fragment2);
        }

        /// <summary>
        /// Appends a query string to a Uri that may or may not have existing
        /// query parameters
        /// </summary>
        /// <param name="uri">Uri to append the query to</param>
        /// <param name="query">Query string to append. Can either start with ?
        /// or just containg key/value pairs</param>
        /// <returns>String representation of the Uri with the query string
        /// appended</returns>
        public static string AppendQuery(this Uri uri, string query)
        {
            if (String.IsNullOrEmpty(query))
                return uri.ToString();

            if (query[0] == '?' || query[0] == '&')
                query = query.Substring(1);

            string uriStr = uri.ToString();

            if (uriStr.Contains("?"))
                return uriStr + '&' + query;
            else
                return uriStr + '?' + query;
        }

        #endregion Uri

        #region NameValueCollection

        /// <summary>
        /// Convert a NameValueCollection into a query string. This is the
        /// not exactly the inverse of HttpUtility.ParseQueryString()
        /// </summary>
        /// <param name="parameters">Collection of key/value pairs to convert</param>
        /// <returns>A query string with URL-escaped values</returns>
        public static string BuildQueryString(NameValueCollection parameters)
        {
            if (parameters.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder(4096);
            foreach (string key in parameters.Keys)
            {
                string[] values = parameters.GetValues(key);
                if (values != null)
                {
                    foreach (string value in values)
                    {
                        sb.Append(key);
                        sb.Append("=");
                        if(!string.IsNullOrWhiteSpace(value))
                            sb.Append(HttpUtility.UrlEncode(value));
                        sb.Append("&");
                    }
                }
            }

            if(sb.Length > 1)
                sb.Length--;

            return sb.ToString();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetOne(this NameValueCollection collection, string key)
        {
            string[] values = collection.GetValues(key);
            if (values != null && values.Length > 0)
                return values[0];

            return null;
        }

        #endregion NameValueCollection

        #region Stream

        /// <summary>
        /// Copies the contents of one stream to another, starting at the
        /// current position of each stream
        /// </summary>
        /// <param name="copyFrom">The stream to copy from, at the position
        /// where copying should begin</param>
        /// <param name="copyTo">The stream to copy to, at the position where
        /// bytes should be written</param>
        /// <param name="maximumBytesToCopy">The maximum bytes to copy</param>
        /// <returns>The total number of bytes copied</returns>
        /// <remarks>
        /// Copying begins at the streams' current positions. The positions are
        /// NOT reset after copying is complete.
        /// NOTE!! .NET 4.0 adds the method 'Stream.CopyTo(stream, bufferSize)'.
        /// This function could be replaced with that method once we move
        /// totally to .NET 4.0. For versions before, this routine exists.
        /// This routine used to be named 'CopyTo' but the int parameter has
        /// a different meaning so this method was renamed to avoid any confusion.
        /// </remarks>
        public static int CopyStream(this Stream copyFrom, Stream copyTo, int maximumBytesToCopy)
        {
            byte[] buffer = new byte[4096];
            int readBytes;
            int totalCopiedBytes = 0;

            while ((readBytes = copyFrom.Read(buffer, 0, Math.Min(4096, maximumBytesToCopy))) > 0)
            {
                int writeBytes = Math.Min(maximumBytesToCopy, readBytes);
                copyTo.Write(buffer, 0, writeBytes);
                totalCopiedBytes += writeBytes;
                maximumBytesToCopy -= writeBytes;
            }

            return totalCopiedBytes;
        }

        #endregion Stream

        public class QBasedComparer : IComparer
        {
            public int Compare(Object x, Object y)
            {
                float qx = GetQ(x);
                float qy = GetQ(y);
                return qy.CompareTo(qx); // descending order
            }

            private float GetQ(Object o)
            {
                // Example: image/png;q=0.9

                float qvalue = 1F;
                if (o is String)
                {
                    string mime = (string)o;
                    string[] parts = mime.Split(';');
                    if (parts.Length > 1)
                    {
                        string[] kvp = parts[1].Split('=');
                        if (kvp.Length == 2 && kvp[0] == "q")
                            float.TryParse(kvp[1], NumberStyles.Number, CultureInfo.InvariantCulture, out qvalue);
                    }
                }

                return qvalue;
            }
        }

        /// <summary>
        /// Takes the value of an Accept header and returns the preferred types
        /// ordered by q value (if it exists).
        /// Example input: image/jpg;q=0.7, image/png;q=0.8, image/jp2
        /// Exmaple output: ["jp2", "png", "jpg"]
        /// NOTE: This doesn't handle the semantics of *'s...
        /// </summary>
        /// <param name="accept"></param>
        /// <returns></returns>
        public static string[] GetPreferredImageTypes(string accept)
        {
            if (string.IsNullOrEmpty(accept))
                return new string[0];

            string[] types = accept.Split(new char[] { ',' });
            if (types.Length > 0)
            {
                List<string> list = new List<string>(types);
                list.RemoveAll(delegate(string s) { return !s.ToLower().StartsWith("image"); });
                ArrayList tlist = new ArrayList(list);
                tlist.Sort(new QBasedComparer());

                string[] result = new string[tlist.Count];
                for (int i = 0; i < tlist.Count; i++)
                {
                    string mime = (string)tlist[i];
                    string[] parts = mime.Split(new char[] { ';' });
                    string[] pair = parts[0].Split(new char[] { '/' });
                    if (pair.Length == 2)
                        result[i] = pair[1].ToLower();
                    else // oops, we don't know what this is...
                        result[i] = pair[0];
                }

                return result;
            }

            return new string[0];
        }
    }

    public static class AsynchronousRestObjectRequester
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Perform an asynchronous REST request.
        /// </summary>
        /// <param name="verb">GET or POST</param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        ///
        /// <exception cref="System.Net.WebException">Thrown if we encounter a
        /// network issue while posting the request.  You'll want to make
        /// sure you deal with this as they're not uncommon</exception>
        //
        public static void MakeRequest<TRequest, TResponse>(string verb,
                string requestUrl, TRequest obj, Action<TResponse> action)
        {
            MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, action, 0);
        }

        public static void MakeRequest<TRequest, TResponse>(string verb,
                string requestUrl, TRequest obj, Action<TResponse> action,
                int maxConnections)
        {
            MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, action, maxConnections, null);
        }

        /// <summary>
        /// Perform a synchronous REST request.
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"></param>
        /// <param name="pTimeout">
        /// Request timeout in seconds.  Timeout.Infinite indicates no timeout.  If 0 is passed then the default HttpWebRequest timeout is used (100 seconds)
        /// </param>
        /// <param name="maxConnections"></param>
        /// <returns>
        /// The response.  If there was an internal exception or the request timed out,
        /// then the default(TResponse) is returned.
        /// </returns>
        public static void MakeRequest<TRequest, TResponse>(string verb,
                string requestUrl, TRequest obj, Action<TResponse> action,
                int maxConnections, IServiceAuth auth)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} AsynchronousRequestObject {1} to {2}",
                    reqnum, verb, requestUrl);

            int tickstart = Util.EnvironmentTickCount();
            int tickdata = 0;
            int tickdiff = 0;

            Type type = typeof(TRequest);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUrl);

            if (auth != null)
                auth.AddAuthorization(request.Headers);

            request.AllowWriteStreamBuffering = false;

            if (maxConnections > 0 && request.ServicePoint.ConnectionLimit < maxConnections)
                request.ServicePoint.ConnectionLimit = maxConnections;

            TResponse deserial = default(TResponse);

            request.Method = verb;

            byte[] data = null;
            try
            {
                if (verb == "POST")
                {
                    request.ContentType = "text/xml";

                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Encoding = Encoding.UTF8;
                    using (MemoryStream buffer = new MemoryStream())
                    using (XmlWriter writer = XmlWriter.Create(buffer, settings))
                    {
                        XmlSerializer serializer = new XmlSerializer(type);
                        serializer.Serialize(writer, obj);
                        writer.Flush();
                        data = buffer.ToArray();
                    }

                    int length = data.Length;
                    request.ContentLength = length;

                    if (WebUtil.DebugLevel >= 5)
                        WebUtil.LogOutgoingDetail("SEND", reqnum, System.Text.Encoding.UTF8.GetString(data));

                    request.BeginGetRequestStream(delegate(IAsyncResult res)
                    {
                        using (Stream requestStream = request.EndGetRequestStream(res))
                            requestStream.Write(data, 0, length);

                        // capture how much time was spent writing
                        tickdata = Util.EnvironmentTickCountSubtract(tickstart);

                        request.BeginGetResponse(delegate(IAsyncResult ar)
                        {
                            using (WebResponse response = request.EndGetResponse(ar))
                            {
                                try
                                {
                                    using (Stream respStream = response.GetResponseStream())
                                    {
                                        deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                                            reqnum, respStream, response.ContentLength);
                                    }
                                }
                                catch (System.InvalidOperationException)
                                {
                                }
                            }

                            action(deserial);

                        }, null);
                    }, null);
                }
                else
                {
                    request.BeginGetResponse(delegate(IAsyncResult res2)
                    {
                        try
                        {
                            // If the server returns a 404, this appears to trigger a System.Net.WebException even though that isn't
                            // documented in MSDN
                            using (WebResponse response = request.EndGetResponse(res2))
                            {
                                try
                                {
                                    using (Stream respStream = response.GetResponseStream())
                                    {
                                        deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                                            reqnum, respStream, response.ContentLength);
                                    }
                                }
                                catch (System.InvalidOperationException)
                                {
                                }
                            }
                        }
                        catch (WebException e)
                        {
                            if (e.Status == WebExceptionStatus.ProtocolError)
                            {
                                if (e.Response is HttpWebResponse)
                                {
                                    using (HttpWebResponse httpResponse = (HttpWebResponse)e.Response)
                                    {
                                        if (httpResponse.StatusCode != HttpStatusCode.NotFound)
                                        {
                                            // We don't appear to be handling any other status codes, so log these feailures to that
                                            // people don't spend unnecessary hours hunting phantom bugs.
                                            m_log.DebugFormat(
                                                "[ASYNC REQUEST]: Request {0} {1} failed with unexpected status code {2}",
                                                verb, requestUrl, httpResponse.StatusCode);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                m_log.ErrorFormat(
                                    "[ASYNC REQUEST]: Request {0} {1} failed with status {2} and message {3}",
                                    verb, requestUrl, e.Status, e.Message);
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[ASYNC REQUEST]: Request {0} {1} failed with exception {2}{3}",
                                verb, requestUrl, e.Message, e.StackTrace);
                        }

                        //  m_log.DebugFormat("[ASYNC REQUEST]: Received {0}", deserial.ToString());

                        try
                        {
                            action(deserial);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[ASYNC REQUEST]: Request {0} {1} callback failed with exception {2}{3}",
                                verb, requestUrl, e.Message, e.StackTrace);
                        }

                    }, null);
                }

                tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
                if (tickdiff > WebUtil.LongCallTime)
                {
                    string originalRequest = null;

                    if (data != null)
                    {
                        originalRequest = Encoding.UTF8.GetString(data);

                        if (originalRequest.Length > WebUtil.MaxRequestDiagLength)
                            originalRequest = originalRequest.Remove(WebUtil.MaxRequestDiagLength);
                    }
                     m_log.InfoFormat(
                        "[LOGHTTP]: Slow AsynchronousRequestObject request {0} {1} to {2} took {3}ms, {4}ms writing, {5}",
                        reqnum, verb, requestUrl, tickdiff, tickdata,
                        originalRequest);
                }
                else if (WebUtil.DebugLevel >= 4)
                {
                    m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms, {2}ms writing",

                        reqnum, tickdiff, tickdata);
                }
            }
            catch { }
        }
    }

    public static class SynchronousRestFormsRequester
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Perform a synchronous REST request.
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"> </param>
        /// <param name="timeoutsecs"> </param>
        /// <returns></returns>
        ///
        /// <exception cref="System.Net.WebException">Thrown if we encounter a network issue while posting
        /// the request.  You'll want to make sure you deal with this as they're not uncommon</exception>
        public static string MakeRequest(string verb, string requestUrl, string obj, int timeoutsecs = -1,
                 IServiceAuth auth = null, bool keepalive = true)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} SynchronousRestForms {1} to {2}",
                    reqnum, verb, requestUrl);

            int tickstart = Util.EnvironmentTickCount();

            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = verb;
                if (timeoutsecs > 0)
                    request.Timeout = timeoutsecs * 1000;
                if(!keepalive)
                    request.KeepAlive = false;
                if (auth != null)
                    auth.AddAuthorization(request.Headers);

                request.AllowWriteStreamBuffering = false;
                request.ContentType = "application/x-www-form-urlencoded";
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[FORMS]: Error creating {0} request to : {1}. Request: {2}", verb, requestUrl, e.Message);
                throw e;
            }

            int sendlen = 0;
            if (obj.Length > 0 && (verb == "POST") || (verb == "PUT"))
            {
                byte[] data = Util.UTF8NBGetbytes(obj);
                sendlen = data.Length;
                request.ContentLength = sendlen;

                if (WebUtil.DebugLevel >= 5)
                    WebUtil.LogOutgoingDetail("SEND", reqnum, System.Text.Encoding.UTF8.GetString(data));

                try
                {
                    using(Stream requestStream = request.GetRequestStream())
                        requestStream.Write(data, 0, sendlen);
                    data = null;
                }
                catch (Exception e)
                {
                    m_log.InfoFormat("[FORMS]: Error sending {0} request to: {1}. {2}", verb,requestUrl, e.Message);
                    throw e;
                }
            }

            int rcvlen = 0;
            string respstring = String.Empty;
            try
            {
                using (WebResponse resp = request.GetResponse())
                {
                    if (resp.ContentLength != 0)
                    {
                        using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                            respstring = reader.ReadToEnd();
                        rcvlen = respstring.Length;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[FORMS]: Error receiving response from {0}: {1}.", requestUrl, e.Message);
                throw e;
            }

            int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
            if (tickdiff > WebUtil.LongCallTime)
            {
                m_log.InfoFormat("[FORMS]: request {0} {1} {2} took {3}ms, {4}/{5}bytes",
                    reqnum, verb, requestUrl, tickdiff, sendlen, rcvlen);
            }
            else if (WebUtil.DebugLevel >= 4)
            {
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms",
                    reqnum, tickdiff);
                if (WebUtil.DebugLevel >= 5)
                    WebUtil.LogResponseDetail(reqnum, respstring);
            }

            return respstring;
        }


        public static string MakeRequest(string verb, string requestUrl, string obj, IServiceAuth auth)
        {
            return MakeRequest(verb, requestUrl, obj, -1, auth);
        }

        public static string MakePostRequest(string requestUrl, string obj,
                 IServiceAuth auth = null, int timeoutsecs = -1, bool keepalive = true)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} SynchronousRestForms POST to {1}",
                    reqnum, requestUrl);

            int tickstart = Util.EnvironmentTickCount();

            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.Method = "POST";
                if (timeoutsecs > 0)
                    request.Timeout = timeoutsecs * 1000;
                if (!keepalive)
                    request.KeepAlive = false;
                if (auth != null)
                    auth.AddAuthorization(request.Headers);

                request.AllowWriteStreamBuffering = false;
                request.ContentType = "application/x-www-form-urlencoded";
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[FORMS]: Error creating POST request to {0}: {1}", requestUrl, e.Message);
                throw e;
            }

            byte[] data = Util.UTF8NBGetbytes(obj);
            int sendlen = data.Length;
            request.ContentLength = sendlen;

            if (WebUtil.DebugLevel >= 5)
                WebUtil.LogOutgoingDetail("SEND", reqnum, System.Text.Encoding.UTF8.GetString(data));

            try
            {
                using (Stream requestStream = request.GetRequestStream())
                    requestStream.Write(data, 0, sendlen);
                data = null;
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[FORMS]: Error sending POST request to {0}: {1}", requestUrl, e.Message);
                throw e;
            }

            string respstring = String.Empty;
            int rcvlen = 0;
            try
            {
                using (WebResponse resp = request.GetResponse())
                {
                    if (resp.ContentLength != 0)
                    {
                        using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                            respstring = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.InfoFormat("[FORMS]: Error receiving response from {0}: {1}", requestUrl, e.Message);
                throw e;
            }

            int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
            if (tickdiff > WebUtil.LongCallTime)
            {
                m_log.InfoFormat("[FORMS]: request {0} POST {1} took {2}ms {3}/{4}bytes",
                    reqnum, requestUrl, tickdiff, sendlen, rcvlen);
            }
            else if (WebUtil.DebugLevel >= 4)
            {
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms",
                    reqnum, tickdiff);
                if (WebUtil.DebugLevel >= 5)
                    WebUtil.LogResponseDetail(reqnum, respstring);
            }

            return respstring;
        }
    }

    public class SynchronousRestObjectRequester
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Perform a synchronous REST request.
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"></param>
        /// <returns>
        /// The response.  If there was an internal exception, then the default(TResponse) is returned.
        /// </returns>
        public static TResponse MakeRequest<TRequest, TResponse>(string verb, string requestUrl, TRequest obj)
        {
            return MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, 0, null);
        }

        public static TResponse MakeRequest<TRequest, TResponse>(string verb, string requestUrl, TRequest obj, IServiceAuth auth)
        {
            return MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, 0, auth);
        }
        /// <summary>
        /// Perform a synchronous REST request.
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"></param>
        /// <param name="pTimeout">
        /// Request timeout in milliseconds.  Timeout.Infinite indicates no timeout.  If 0 is passed then the default HttpWebRequest timeout is used (100 seconds)
        /// </param>
        /// <returns>
        /// The response.  If there was an internal exception or the request timed out,
        /// then the default(TResponse) is returned.
        /// </returns>
        public static TResponse MakeRequest<TRequest, TResponse>(string verb, string requestUrl, TRequest obj, int pTimeout)
        {
            return MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, pTimeout, null);
        }

        /// <summary>
        /// Perform a synchronous something request.
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"></param>
        /// <param name="pTimeout">
        /// Request timeout in milliseconds.  Timeout.Infinite indicates no timeout.  If 0 is passed then the default HttpWebRequest timeout is used (100 seconds)
        /// </param>
        /// <returns>
        /// The response.  If there was an internal exception or the request timed out,
        /// then the default(TResponse) is returned.
        /// </returns>
        public static TResponse MakeRequest<TRequest, TResponse>(string verb, string requestUrl, TRequest obj, int pTimeout, IServiceAuth auth)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} SRestObjReq {1} {2}",
                    reqnum, verb, requestUrl);

            int tickstart = Util.EnvironmentTickCount();

            TResponse deserial = default(TResponse);

            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);

                if (auth != null)
                    auth.AddAuthorization(request.Headers);

                if (pTimeout != 0)
                    request.Timeout = pTimeout;

                request.Method = verb;
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[SRestObjReq]: Exception in creating request {0} {1}: {2}{3}",
                    verb, requestUrl, e.Message, e.StackTrace);
                return deserial;
            }

            try
            {
                if ((verb == "POST") || (verb == "PUT"))
                {
                    request.ContentType = "text/xml";

                    byte[] data;
                    XmlWriterSettings settings = new XmlWriterSettings() { Encoding = Util.UTF8 };
                    using (MemoryStream ms = new MemoryStream())
                    using (XmlWriter writer = XmlWriter.Create(ms, settings))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(TRequest));
                        serializer.Serialize(writer, obj);
                        writer.Flush();
                        data = ms.ToArray();
                    }

                    int sendlen = data.Length;
                    request.ContentLength = sendlen;

                    if (WebUtil.DebugLevel >= 5)
                        WebUtil.LogOutgoingDetail("SEND", reqnum, System.Text.Encoding.UTF8.GetString(data));

                    using (Stream requestStream = request.GetRequestStream())
                        requestStream.Write(data, 0, sendlen);
                    data = null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat(
                    "[SRestObjReq]: Exception in making request {0} {1}: {2}{3}",
                    verb, requestUrl, e.Message, e.StackTrace);

                return deserial;
            }

            int rcvlen = 0;
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    if (resp.ContentLength != 0)
                    {
                        rcvlen = (int)resp.ContentLength;
                        using (Stream respStream = resp.GetResponseStream())
                        {
                            deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                                reqnum, respStream, resp.ContentLength);
                        }
                    }
                    else
                    {
                        m_log.DebugFormat("[SRestObjReq]: Oops! no content found in response stream from {0} {1}",
                            verb, requestUrl);
                    }
                }
            }
            catch (WebException e)
            {
                using (HttpWebResponse hwr = (HttpWebResponse)e.Response)
                {
                    if (hwr != null)
                    {
                        if (hwr.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            m_log.ErrorFormat("[SRestObjReq]: {0} requires authentication",
                                requestUrl);
                        }
                        else if (hwr.StatusCode != HttpStatusCode.NotFound)
                        {
                            m_log.WarnFormat("[SRestObjReq]: {0} returned error: {1}",
                                requestUrl, hwr.StatusCode);
                        }
                    }
                    else
                        m_log.ErrorFormat(
                            "[SRestObjReq]: WebException for {0} {1} {2} {3}",
                            verb, requestUrl, typeof(TResponse).ToString(), e.Message);
                }
            }
            catch (System.InvalidOperationException)
            {
                // This is what happens when there is invalid XML
                m_log.DebugFormat("[SRestObjReq]: Invalid XML from {0} {1} {2}",
                    verb, requestUrl, typeof(TResponse).ToString());
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[SRestObjReq]: Exception on response from {0} {1}: {2}",
                    verb, requestUrl, e.Message);
            }

            int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
            if (tickdiff > WebUtil.LongCallTime)
            {
                m_log.InfoFormat("[LOGHTTP]: Slow SRestObjReq {0} {1} {2} took {3}ms, {4}bytes",
                    reqnum, verb, requestUrl, tickdiff, rcvlen);
            }
            else if (WebUtil.DebugLevel >= 4)
            {
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms", reqnum, tickdiff);
            }
            return deserial;
        }

        public static TResponse MakeGetRequest<TResponse>(string requestUrl, int pTimeout, IServiceAuth auth)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} SRestObjReq GET {1}", reqnum, requestUrl);
            int tickstart = Util.EnvironmentTickCount();

            TResponse deserial = default(TResponse);
            HttpWebRequest request = null;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(requestUrl);

                if (auth != null)
                    auth.AddAuthorization(request.Headers);

                request.AllowWriteStreamBuffering = false;

                if (pTimeout != 0)
                    request.Timeout = pTimeout;

                request.Method = "GET";
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[SRestObjReq]: Exception in creating GET request  {0}: {1}{2}",
                    requestUrl, e.Message, e.StackTrace);
                return deserial;
            }

            int rcvlen = 0;
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)request.GetResponse())
                {
                    if (resp.ContentLength != 0)
                    {
                        rcvlen = (int)resp.ContentLength;
                        using (Stream respStream = resp.GetResponseStream())
                        {
                            deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                                reqnum, respStream, resp.ContentLength);
                        }
                    }
                    else
                    {
                        m_log.DebugFormat("[SRestObjReq]: Oops! no content found in response stream from GET {0}",
                            requestUrl);
                    }
                }
            }
            catch (WebException e)
            {
                using (HttpWebResponse hwr = (HttpWebResponse)e.Response)
                {
                    if (hwr != null)
                    {
                        if (hwr.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            m_log.ErrorFormat("[SRestObjReq]:  GET {0} requires authentication",
                                requestUrl);
                        }
                        else if (hwr.StatusCode != HttpStatusCode.NotFound)
                        {
                            m_log.WarnFormat("[SRestObjReq]: GET {0} returned error: {1}",
                                requestUrl, hwr.StatusCode);
                        }
                    }
                    else
                        m_log.ErrorFormat(
                            "[SRestObjReq]: WebException for GET {0} {1} {2}",
                            requestUrl, typeof(TResponse).ToString(), e.Message);
                }
            }
            catch (System.InvalidOperationException)
            {
                // This is what happens when there is invalid XML
                m_log.DebugFormat("[SRestObjReq]: Invalid XML from GET {0} {1}",
                    requestUrl, typeof(TResponse).ToString());
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[SRestObjReq]: Exception on response from GET {0}: {1}",
                    requestUrl, e.Message);
            }

            int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
            if (tickdiff > WebUtil.LongCallTime)
            {
                m_log.InfoFormat("[LOGHTTP]: Slow SRestObjReq  GET {0} {1} took {2}ms, {3}bytes",
                    reqnum, requestUrl, tickdiff, rcvlen);
            }
            else if (WebUtil.DebugLevel >= 4)
            {
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms",
                    reqnum, tickdiff);
            }
            return deserial;
        }
    }

    public static class XMLResponseHelper
    {
        public static TResponse LogAndDeserialize<TResponse>(int reqnum, Stream respStream, long contentLength)
        {
            XmlSerializer deserializer = new XmlSerializer(typeof(TResponse));
            if (WebUtil.DebugLevel >= 5)
            {
                const int blockLength = 4096;
                byte[] dataBuffer = new byte[blockLength];
                int curcount;
                using (MemoryStream ms = new MemoryStream(4 * blockLength))
                {
                    if(contentLength == -1)
                    {
                        while (true)
                        {
                            curcount = respStream.Read(dataBuffer, 0, blockLength);
                            if (curcount <= 0)
                                break;
                            ms.Write(dataBuffer, 0, curcount);
                        }
                    }
                    else
                    {
                        int remaining = (int)contentLength;
                        while (remaining > 0)
                        {
                            curcount = respStream.Read(dataBuffer, 0, remaining);
                            if (curcount <= 0)
                                throw new EndOfStreamException(String.Format("End of stream reached with {0} bytes left to read", remaining));
                            ms.Write(dataBuffer, 0, curcount);
                            remaining -= curcount;
                        }
                    }

                    dataBuffer = ms.ToArray();
                    WebUtil.LogResponseDetail(reqnum, System.Text.Encoding.UTF8.GetString(dataBuffer));

                    ms.Position = 0;
                    return (TResponse)deserializer.Deserialize(ms);
                }
            }
            else
            {
                return (TResponse)deserializer.Deserialize(respStream);
            }
        }
    }

    public static class XMLRPCRequester
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static Hashtable SendRequest(Hashtable ReqParams, string method, string url)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} XML-RPC '{1}' to {2}",
                    reqnum, method, url);

            int tickstart = Util.EnvironmentTickCount();
            string responseStr = null;

            try
            {
                ArrayList SendParams = new ArrayList();
                SendParams.Add(ReqParams);

                XmlRpcRequest Req = new XmlRpcRequest(method, SendParams);

                if (WebUtil.DebugLevel >= 5)
                {
                    string str = Req.ToString();
                    str = XElement.Parse(str).ToString(SaveOptions.DisableFormatting);
                    WebUtil.LogOutgoingDetail("SEND", reqnum, str);
                }

                XmlRpcResponse Resp = Req.Send(url, 30000);

                try
                {
                    if (WebUtil.DebugLevel >= 5)
                    {
                        responseStr = Resp.ToString();
                        responseStr = XElement.Parse(responseStr).ToString(SaveOptions.DisableFormatting);
                        WebUtil.LogResponseDetail(reqnum, responseStr);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("Error parsing XML-RPC response", e);
                }

                if (Resp.IsFault)
                {
                    m_log.DebugFormat(
                        "[LOGHTTP]: XML-RPC request {0} '{1}' to {2} FAILED: FaultCode={3}, FaultMessage={4}",
                        reqnum, method, url, Resp.FaultCode, Resp.FaultString);
                    return null;
                }

                Hashtable RespData = (Hashtable)Resp.Value;
                return RespData;
            }
            finally
            {
                int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
                if (tickdiff > WebUtil.LongCallTime)
                {
                    m_log.InfoFormat(
                        "[LOGHTTP]: Slow XML-RPC request {0} '{1}' to {2} took {3}ms, {4}",
                        reqnum, method, url, tickdiff,
                        responseStr != null
                            ? (responseStr.Length > WebUtil.MaxRequestDiagLength ? responseStr.Remove(WebUtil.MaxRequestDiagLength) : responseStr)
                            : "");
                }
                else if (WebUtil.DebugLevel >= 4)
                {
                    m_log.DebugFormat("[LOGHTTP]: HTTP OUT {0} took {1}ms", reqnum, tickdiff);
                }
            }
        }
    }
}
