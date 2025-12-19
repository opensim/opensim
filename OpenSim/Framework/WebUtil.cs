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
using System.Buffers;
using System.Collections;
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
using OpenSim.Framework.ServiceAuth;
using System.Net.Http;
using System.Security.Authentication;
using System.Runtime.CompilerServices;

namespace OpenSim.Framework
{
    /// <summary>
    /// Miscellaneous static methods and extension methods related to the web
    /// </summary>
    /// 

    public static class WebUtil
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static SocketsHttpHandler SharedSocketsHttpHandlerNoRedir = null;
        public static SocketsHttpHandler SharedSocketsHttpHandler = null;

        public static ExpiringKey<string> GlobalExpiringBadURLs = new(30000);
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
            return sslPolicyErrors == SslPolicyErrors.None;
        }
        #region JSONRequest

        public static void SetupHTTPClients(bool NoVerifyCertChain, bool NoVerifyCertHostname, IWebProxy proxy, int MaxConnectionsPerServer )
        {
            SocketsHttpHandler shh = new()
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                ConnectTimeout = TimeSpan.FromSeconds(120),
                PreAuthenticate = false,
                UseCookies = false,
                MaxConnectionsPerServer = MaxConnectionsPerServer,
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(31),
                PooledConnectionLifetime = TimeSpan.FromMinutes(3)
            };
            //shh.SslOptions.ClientCertificates = null,
            shh.SslOptions.EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
            if (NoVerifyCertChain)
            {
                shh.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                if (NoVerifyCertHostname)
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        errors &= ~(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch);
                        return errors == SslPolicyErrors.None;
                    };
                }
                else
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        errors &= ~SslPolicyErrors.RemoteCertificateChainErrors;
                        return errors == SslPolicyErrors.None;
                    };
                }
            }
            else
            {
                shh.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                if (NoVerifyCertHostname)
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        errors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
                        return errors == SslPolicyErrors.None;
                    };
                }
                else
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        return errors == SslPolicyErrors.None;
                    };
                }
            }

            if (proxy is null)
                shh.UseProxy = false;
            else
            {
                shh.Proxy = proxy;
                shh.UseProxy = true;
            }

            SharedSocketsHttpHandlerNoRedir = shh;

            // ****************

            shh = new()
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression = DecompressionMethods.None,
                ConnectTimeout = TimeSpan.FromSeconds(120),
                PreAuthenticate = false,
                UseCookies = false,
                MaxConnectionsPerServer = MaxConnectionsPerServer,
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(31),
                PooledConnectionLifetime = TimeSpan.FromMinutes(3)
            };
            //shh.SslOptions.ClientCertificates = null,
            shh.SslOptions.EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
            if (NoVerifyCertChain)
            {
                shh.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                if (NoVerifyCertHostname)
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        errors &= ~(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch);
                        return errors == SslPolicyErrors.None;
                    };
                }
                else
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        errors &= ~SslPolicyErrors.RemoteCertificateChainErrors;
                        return errors == SslPolicyErrors.None;
                    };
                }
            }
            else
            {
                shh.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                if (NoVerifyCertHostname)
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        errors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
                        return errors == SslPolicyErrors.None;
                    };
                }
                else
                {
                    shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                    {
                        return errors == SslPolicyErrors.None;
                    };
                }
            }

            if (proxy is null)
                shh.UseProxy = false;
            else
            {
                shh.Proxy = proxy;
                shh.UseProxy = true;
            }
            SharedSocketsHttpHandler = shh;
        }

        public static HttpClient GetNewGlobalHttpClient(int timeout)
        {
            var client = new HttpClient(SharedSocketsHttpHandler, false)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout > 0 ? timeout : 30000),
                MaxResponseContentBufferSize = 250 * 1024 * 1024,
            };
            client.DefaultRequestHeaders.ExpectContinue = false;
            return client;
        }

        public static HttpClient GetGlobalNoRedirHttpClient(int timeout)
        {
            var client = new HttpClient(SharedSocketsHttpHandlerNoRedir, false)
            {
                Timeout = TimeSpan.FromMilliseconds(timeout > 0 ? timeout : 30000),
                MaxResponseContentBufferSize = 250 * 1024 * 1024,
            };
            client.DefaultRequestHeaders.ExpectContinue = false;
            return client;
        }

        /// <summary>
        /// PUT JSON-encoded data to a web service that returns LLSD or
        /// JSON data
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OSDMap PutToServiceCompressed(string url, OSDMap data, int timeout)
        {
            return ServiceOSDRequest(url, data, "PUT", timeout, true, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OSDMap PutToService(string url, OSDMap data, int timeout)
        {
            return ServiceOSDRequest(url, data, "PUT", timeout, false, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OSDMap PostToService(string url, OSDMap data, int timeout, bool rpc)
        {
            return ServiceOSDRequest(url, data, "POST", timeout, false, rpc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OSDMap PostToServiceCompressed(string url, OSDMap data, int timeout)
        {
            return ServiceOSDRequest(url, data, "POST", timeout, true, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OSDMap GetFromService(string url, int timeout)
        {
            return ServiceOSDRequest(url, null, "GET", timeout, false, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogOutgoingDetail(Stream outputStream)
        {
            LogOutgoingDetail("", outputStream);
        }

        public static void LogOutgoingDetail(string context, Stream outputStream)
        {
            using Stream stream = Util.Copy(outputStream);
            using StreamReader reader = new(stream, Encoding.UTF8);
            string output;

            if (DebugLevel == 5)
            {
                char[] chars = new char[MaxRequestDiagLength + 1];  // +1 so we know to add "..." only if needed
                int len = reader.Read(chars, 0, MaxRequestDiagLength + 1);
                output = new string(chars, 0, len);
            }
            else
            {
                output = reader.ReadToEnd();
            }

            LogOutgoingDetail(context, output);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogOutgoingDetail(string type, int reqnum, string output)
        {
            LogOutgoingDetail($"{type} {reqnum}: ", output);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogOutgoingDetail(string context, string output)
        {
            if (DebugLevel >= 5)
            {
                if (output.Length > MaxRequestDiagLength)
                    output = output[..MaxRequestDiagLength] + "...";
            }

            m_log.Debug($"[LOGHTTP]: {context} {Util.BinaryToASCII(output)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogResponseDetail(int reqnum, Stream inputStream)
        {
            LogOutgoingDetail($"RESPONSE {reqnum}: ", inputStream);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogResponseDetail(int reqnum, string input)
        {
            LogOutgoingDetail($"RESPONSE {reqnum}: ", input);
        }

        public static OSDMap ServiceOSDRequest(string url, OSDMap data, string method, int timeout, bool compressed, bool rpc, bool keepalive = false)
        {
            int reqnum = RequestNumber++;

            if (DebugLevel >= 3)
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} JSON-RPC {method} to {url}");

            string errorMessage = "unknown error";
            int ticks = Util.EnvironmentTickCount();

            int sendlen = 0;
            int rcvlen = 0;
            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;
            try
            {
                client = GetNewGlobalHttpClient(timeout);
                request = new(new HttpMethod(method), url);

                if (data is not null)
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

                    if (buffer.Length > 0)
                    {
                        if (compressed)
                        {
                            using MemoryStream ms = new();
                            using (GZipStream comp = new(ms, CompressionMode.Compress, true))
                            {
                                comp.Write(buffer, 0, buffer.Length);
                            }
                            buffer = ms.ToArray();

                            request.Headers.TryAddWithoutValidation("X-Content-Encoding", "gzip"); // can't set "Content-Encoding" because old OpenSims fail if they get an unrecognized Content-Encoding
                        }

                        sendlen = buffer.Length;
                        request.Content = new ByteArrayContent(buffer);
                        request.Content.Headers.TryAddWithoutValidation("Content-Type",
                                rpc ? "application/json-rpc" : "application/json");
                        request.Content.Headers.TryAddWithoutValidation("Content-Length", sendlen.ToString());
                    }
                }

                request.Headers.ExpectContinue = false;
                request.Headers.TransferEncodingChunked = false;
                if(keepalive)
                {
                    request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=30, max=10");
                    request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
                    request.Headers.ConnectionClose = false;
                }
                else
                    request.Headers.TryAddWithoutValidation("Connection", "close");

                request.Headers.TryAddWithoutValidation(OSHeaderRequestID, reqnum.ToString());

                responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                responseMessage.EnsureSuccessStatusCode();

                Stream resStream = responseMessage.Content.ReadAsStream();
                if (resStream is not null)
                {
                    using StreamReader reader = new(resStream);
                    string responseStr = reader.ReadToEnd();
                    if (WebUtil.DebugLevel >= 5)
                        WebUtil.LogResponseDetail(reqnum, responseStr);
                    rcvlen = responseStr.Length;
                    return CanonicalizeResults(responseStr);
                }
            }
            catch (HttpRequestException e)
            {
                int Status = e.StatusCode is null ? 499 : (int)e.StatusCode;
                errorMessage = $"[{Status}] {e.Message}";
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                m_log.Debug($"[WEB UTIL]: Exception making request: {errorMessage}");
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();

                ticks = Util.EnvironmentTickCountSubtract(ticks);
                if (ticks > LongCallTime)
                {
                    m_log.Info($"[WEB UTIL]: SvcOSD {reqnum} {method} {url} took {ticks}ms, {sendlen}/{rcvlen}bytes");
                }
                else if (DebugLevel >= 4)
                {
                    m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {ticks}ms");
                }
            }

            m_log.Debug($"[LOGHTTP]: request {reqnum} {method} to {url} FAILED: {errorMessage}");

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
            OSDMap result = new()
            {
                // Default values
                ["Success"] = OSD.FromBoolean(true),
                ["success"] = OSD.FromBoolean(true),
                ["_RawResult"] = OSD.FromString(response),
                ["_Result"] = new OSDMap()
            };

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
                //m_log.DebugFormat("[WEB UTIL] couldn't decode <{0}>: {1}",response,e.Message);
            }

            return result;
        }

        #endregion JSONRequest

        #region FormRequest

        /// <summary>
        /// POST URL-encoded form data to a web service that returns LLSD or
        /// JSON data
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OSDMap PostToService(string url, NameValueCollection data)
        {
            return ServiceFormRequest(url,data, 30000);
        }

        public static OSDMap ServiceFormRequest(string url, NameValueCollection data, int timeout)
        {
            int reqnum = RequestNumber++;
            string method = (data is not null && data["RequestMethod"] is not null) ? data["RequestMethod"] : "unknown";
            if (DebugLevel >= 3)
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} ServiceForm '{method}' to {url}");

            string errorMessage = "unknown error";
            int ticks = Util.EnvironmentTickCount();
            int sendlen = 0;
            int rcvlen = 0;

            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;
            try
            {
                client = GetNewGlobalHttpClient(timeout);

                request = new(HttpMethod.Post, url);

                request.Headers.ExpectContinue = false;
                request.Headers.TransferEncodingChunked = false;

                //if (keepalive)
                //{
                //    request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=30, max=10");
                //    request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
                //    request.Headers.ConnectionClose = false;
                //}
                //else
                    request.Headers.TryAddWithoutValidation("Connection", "close");

                request.Headers.TryAddWithoutValidation(OSHeaderRequestID, reqnum.ToString());

                if (data is not null)
                {
                    string queryString = BuildQueryString(data);

                    if (DebugLevel >= 5)
                        LogOutgoingDetail("SEND", reqnum, queryString);

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(queryString);
                    queryString = null;

                    sendlen = buffer.Length;

                    request.Content = new ByteArrayContent(buffer);
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    request.Content.Headers.TryAddWithoutValidation("Content-Length", sendlen.ToString()); buffer = null;
                }
                else
                {
                    request.Content = new ByteArrayContent(Array.Empty<byte>());
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    request.Content.Headers.TryAddWithoutValidation("Content-Length", "0");
                }

                responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                responseMessage.EnsureSuccessStatusCode();

                using StreamReader reader = new(responseMessage.Content.ReadAsStream());
                string responseStr = reader.ReadToEnd();
                rcvlen = responseStr.Length;
                if (DebugLevel >= 5)
                    LogResponseDetail(reqnum, responseStr);
                OSD responseOSD = OSDParser.Deserialize(responseStr);

                if (responseOSD.Type == OSDType.Map)
                    return (OSDMap)responseOSD;
            }
            catch (HttpRequestException we)
            {
                if (we.StatusCode is HttpStatusCode status)
                    errorMessage = $"[{status}] {we.Message}";
                else
                    errorMessage = we.Message;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();

                ticks = Util.EnvironmentTickCountSubtract(ticks);
                if (ticks > LongCallTime)
                {
                    m_log.Info(
                        $"[LOGHTTP]: Slow ServiceForm request {reqnum} '{method}' to {url} took {ticks}ms, {sendlen}/{rcvlen}bytes");
                }
                else if (DebugLevel >= 4)
                {
                    m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {ticks}ms");
                }
            }

            m_log.Warn($"[LOGHTTP]: ServiceForm request {reqnum} '{method}' to {url} failed: {errorMessage}");

            return ErrorResponseMap(errorMessage);
        }

        /// <summary>
        /// Create a response map for an error, trying to keep
        /// the result formats consistent
        /// </summary>
        private static OSDMap ErrorResponseMap(string msg)
        {
            OSDMap result = new()
            {
                ["Success"] = "False",
                ["Message"] = OSD.FromString("Service request failed: " + msg)
            };
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

            if (!fragment1.EndsWith('/'))
                fragment1 += '/';
            if (fragment2.StartsWith('/'))
                fragment2 = fragment2[1..];

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

            if (!fragment1.EndsWith('/'))
                fragment1 += '/';
            if (fragment2.StartsWith('/'))
                fragment2 = fragment2[1..];

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
            if (string.IsNullOrEmpty(query))
                return uri.ToString();

            if (query[0] == '?' || query[0] == '&')
                query = query[1..];

            string uriStr = uri.ToString();

            if (uriStr.Contains('?'))
                return $"{uriStr}&{query}";
            else
                return $"{uriStr}?{query}";
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

            StringBuilder sb = new(4096);
            foreach (string key in parameters.Keys)
            {
                string[] values = parameters.GetValues(key);
                if (values is not null)
                {
                    foreach (string value in values)
                    {
                        sb.Append(key);
                        sb.Append('=');
                        if(!string.IsNullOrWhiteSpace(value))
                            sb.Append(HttpUtility.UrlEncode(value));
                        sb.Append('&');
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
            if (values is not null && values.Length > 0)
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
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8196);
            int readBytes;
            int totalCopiedBytes = 0;

            while ((readBytes = copyFrom.Read(buffer, 0, Math.Min(8196, maximumBytesToCopy))) > 0)
            {
                int writeBytes = Math.Min(maximumBytesToCopy, readBytes);
                copyTo.Write(buffer, 0, writeBytes);
                totalCopiedBytes += writeBytes;
                maximumBytesToCopy -= writeBytes;
            }
            ArrayPool<byte>.Shared.Return(buffer);
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

            private static float GetQ(Object o)
            {
                // Example: image/png;q=0.9

                float qvalue = 1f;
                if (o is String mime)
                {
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
                return Array.Empty<string>();

            string[] types = accept.Split(Util.SplitCommaArray);
            if (types.Length > 0)
            {
                ArrayList tlist = new();
                foreach(string s in types.AsSpan())
                {
                    if(s.StartsWith("image", StringComparison.InvariantCultureIgnoreCase))
                        tlist.Add(s);
                }
                if(tlist.Count == 0)
                    return Array.Empty<string>();

                tlist.Sort(new QBasedComparer());

                string[] result = new string[tlist.Count];

                for (int i = 0; i < tlist.Count; i++)
                {
                    string mime = (string)tlist[i];
                    string[] parts = mime.Split(Util.SplitSemicolonArray);
                    string[] pair = parts[0].Split(Util.SplitSlashArray);
                    if (pair.Length == 2)
                        result[i] = pair[1].ToLower();
                    else // oops, we don't know what this is...
                        result[i] = pair[0];
                }

                return result;
            }

            return Array.Empty<string>();
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MakeRequest<TRequest, TResponse>(string verb,
                string requestUrl, TRequest obj, Action<TResponse> action)
        {
            MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, action, 0, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// Request timeout in seconds.  Timeout.Infinite indicates no timeout.  If 0 is passed then the default timeout is used (100 seconds)
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
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} AsynchronousRequestObject {verb} to {requestUrl}");

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

            TResponse deserial = default;

            request.Method = verb;

            byte[] data = null;
            try
            {
                if (verb == "POST")
                {
                    request.ContentType = "text/xml";

                    XmlWriterSettings settings = new()
                    {
                        Encoding = Encoding.UTF8
                    };
                    using (MemoryStream buffer = new())
                    using (XmlWriter writer = XmlWriter.Create(buffer, settings))
                    {
                        XmlSerializer serializer = new(type);
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
                                    using Stream respStream = response.GetResponseStream();
                                    deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                                        reqnum, respStream, response.ContentLength);
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
                            using WebResponse response = request.EndGetResponse(res2);
                            try
                            {
                                using Stream respStream = response.GetResponseStream();
                                deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                                    reqnum, respStream, response.ContentLength);
                            }
                            catch (System.InvalidOperationException)
                            {
                                try
                                {
                                    using Stream respStream = response.GetResponseStream();
                                    deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                                        reqnum, respStream, response.ContentLength);
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
                                if (e.Response is HttpWebResponse httpResponse)
                                {
                                    if (httpResponse.StatusCode != HttpStatusCode.NotFound)
                                    {
                                        // We don't appear to be handling any other status codes, so log these feailures to that
                                        // people don't spend unnecessary hours hunting phantom bugs.
                                        m_log.Debug(
                                            $"[ASYNC REQUEST]: Request {verb} {requestUrl} failed with unexpected status code {httpResponse.StatusCode}");
                                    }
                                    httpResponse.Dispose();
                                }
                            }
                            else
                            {
                                m_log.Error(
                                    $"[ASYNC REQUEST]: Request {verb} {requestUrl} failed with status {e.Status} and message {e.Message}");
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Error($"[ASYNC REQUEST]: Request {verb} {requestUrl} failed with exception {e.Message}");
                        }

                        //m_log.DebugFormat("[ASYNC REQUEST]: Received {0}", deserial.ToString());

                        try
                        {
                            action(deserial);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat($"[ASYNC REQUEST]: Request {verb} {requestUrl} callback failed with exception {e.Message}");
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
                    m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {tickdiff}ms, {tickdata}ms writing");
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
        /// <param name="method"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"> </param>
        /// <param name="timeoutsecs"> </param>
        /// <returns></returns>
        ///
        /// <exception cref="System.Net.WebException">Thrown if we encounter a network issue while posting
        /// the request.  You'll want to make sure you deal with this as they're not uncommon</exception>
        public static string MakeRequest(string method, string requestUrl, string obj, int timeoutsecs = -1,
                 IServiceAuth auth = null, bool keepalive = true)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} SynchronousRestForms {method} to {requestUrl}");

            int ticks = Util.EnvironmentTickCount();
            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;
            string respstring = string.Empty;
            int sendlen = 0;
            int rcvlen = 0;
            try
            {
                client = WebUtil.GetNewGlobalHttpClient(timeoutsecs * 1000);

                request = new(new HttpMethod(method), requestUrl);

                auth?.AddAuthorization(request.Headers);

                request.Headers.ExpectContinue = false;
                request.Headers.TransferEncodingChunked = false; if (timeoutsecs > 0)

                if (keepalive)
                {
                    request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=30, max=10");
                    request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
                    request.Headers.ConnectionClose = false;
                }
                else
                    request.Headers.TryAddWithoutValidation("Connection", "close");

                if (obj.Length > 0 && 
                    (method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                    method.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
                {
                    byte[] data = Util.UTF8NBGetbytes(obj);
                    sendlen = data.Length;

                    if (WebUtil.DebugLevel >= 5)
                        WebUtil.LogOutgoingDetail("SEND", reqnum, System.Text.Encoding.UTF8.GetString(data));

                    request.Content = new ByteArrayContent(data);
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                    request.Content.Headers.TryAddWithoutValidation("Content-Length", sendlen.ToString());
                }

                responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                responseMessage.EnsureSuccessStatusCode();

                if ((responseMessage.Content.Headers.ContentLength is long contentLength) && contentLength != 0)
                {
                    using StreamReader reader = new(responseMessage.Content.ReadAsStream());
                    respstring = reader.ReadToEnd();
                    rcvlen = respstring.Length;
                }
            }
            catch (Exception e)
            {
                m_log.Info($"[FORMS]: Error receiving response from {requestUrl}: {e.Message}");
                throw;
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();
            }

            ticks = Util.EnvironmentTickCountSubtract(ticks);
            if (ticks > WebUtil.LongCallTime)
            {
                m_log.Info($"[FORMS]: request {reqnum} {method} {requestUrl} took {ticks}ms, {sendlen}/{rcvlen}bytes");
            }
            else if (WebUtil.DebugLevel >= 4)
            {
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {ticks}ms");
                if (WebUtil.DebugLevel >= 5)
                    WebUtil.LogResponseDetail(reqnum, respstring);
            }

            return respstring;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string MakeRequest(string verb, string requestUrl, string obj, IServiceAuth auth)
        {
            return MakeRequest(verb, requestUrl, obj, -1, auth);
        }

        public static string MakePostRequest(string requestUrl, string obj,
                 IServiceAuth auth = null, int timeoutsecs = -1, bool keepalive = true)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} SynchronousRestForms POST to {requestUrl}");

            int ticks = Util.EnvironmentTickCount();
            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;
            string respstring = String.Empty;
            int sendlen = 0;
            int rcvlen = 0;
            try
            {
                client = WebUtil.GetNewGlobalHttpClient(timeoutsecs * 1000);
                request = new(HttpMethod.Post, requestUrl);

                auth?.AddAuthorization(request.Headers);

                request.Headers.ExpectContinue = false;
                request.Headers.TransferEncodingChunked = false;

                if (keepalive)
                {
                    request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=30, max=10");
                    request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
                    request.Headers.ConnectionClose = false;
                }
                else
                    request.Headers.TryAddWithoutValidation("Connection", "close");

                byte[] data = Util.UTF8NBGetbytes(obj);
                sendlen = data.Length;
                request.Content = new ByteArrayContent(data);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
                request.Content.Headers.TryAddWithoutValidation("Content-Length", sendlen.ToString());

                if (WebUtil.DebugLevel >= 5)
                    WebUtil.LogOutgoingDetail("SEND", reqnum, System.Text.Encoding.UTF8.GetString(data));

                responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);

                if ((responseMessage.Content.Headers.ContentLength is long contentLength) && contentLength != 0)
                {
                    using StreamReader reader = new(responseMessage.Content.ReadAsStream());
                    respstring = reader.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                m_log.Info($"[FORMS]: Error receiving response from {requestUrl}: {e.Message}");
                throw;
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();
            }

            ticks = Util.EnvironmentTickCountSubtract(ticks);
            if (ticks > WebUtil.LongCallTime)
            {
                m_log.Info($"[FORMS]: request {reqnum} POST {requestUrl} took {ticks}ms {sendlen}/{rcvlen}bytes");
            }
            else if (WebUtil.DebugLevel >= 4)
            {
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {ticks}ms");
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResponse MakeRequest<TRequest, TResponse>(string verb, string requestUrl, TRequest obj)
        {
            return MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, 0, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        /// Request timeout in milliseconds.  Timeout.Infinite indicates no timeout.  If 0 is passed then the default timeout is used (100 seconds)
        /// </param>
        /// <returns>
        /// The response.  If there was an internal exception or the request timed out,
        /// then the default(TResponse) is returned.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TResponse MakeRequest<TRequest, TResponse>(string verb, string requestUrl, TRequest obj, int pTimeout)
        {
            return MakeRequest<TRequest, TResponse>(verb, requestUrl, obj, pTimeout, null);
        }

        /// <summary>
        /// Perform a synchronous something request.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"></param>
        /// <param name="pTimeout">
        /// Request timeout in milliseconds.  Timeout.Infinite indicates no timeout.  If 0 is passed then the default timeout is used (100 seconds)
        /// </param>
        /// <returns>
        /// The response.  If there was an internal exception or the request timed out,
        /// then the default(TResponse) is returned.
        /// </returns>
        public static TResponse MakeRequest<TRequest, TResponse>(string method, string requestUrl, TRequest obj, int pTimeout, IServiceAuth auth)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} SRestObjReq {method} {requestUrl}");

            int ticks = Util.EnvironmentTickCount();
            TResponse deserial = default;
            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;

            try
            {
                client = WebUtil.GetNewGlobalHttpClient(pTimeout);

                request = new(new HttpMethod(method), requestUrl);

                auth?.AddAuthorization(request.Headers);

                request.Headers.ExpectContinue = false;
                request.Headers.TransferEncodingChunked = false;

                //if (keepalive)
                {
                    request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=30, max=10");
                    request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
                    request.Headers.ConnectionClose = false;
                }
                //else
                //    request.Headers.TryAddWithoutValidation("Connection", "close");

                if (method.Equals("POST",StringComparison.OrdinalIgnoreCase) || method.Equals("PUT", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] data;
                    XmlWriterSettings settings = new() { Encoding = Util.UTF8 };
                    using (MemoryStream ms = new())
                    using (XmlWriter writer = XmlWriter.Create(ms, settings))
                    {
                        XmlSerializer serializer = new(typeof(TRequest));
                        serializer.Serialize(writer, obj);
                        writer.Flush();
                        data = ms.ToArray();
                    }

                    int sendlen = data.Length;
                    if (WebUtil.DebugLevel >= 5)
                        WebUtil.LogOutgoingDetail("SEND", reqnum, System.Text.Encoding.UTF8.GetString(data));

                    request.Content = new ByteArrayContent(data);
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", "text/xml");
                    request.Content.Headers.TryAddWithoutValidation("Content-Length", sendlen.ToString());
                }

                responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                responseMessage.EnsureSuccessStatusCode();

                int rcvlen = 0;
                if ((responseMessage.Content.Headers.ContentLength is long contentLength) && contentLength != 0)
                {
                    rcvlen = (int)contentLength;
                    using Stream respStream = responseMessage.Content.ReadAsStream();
                    deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                        reqnum, respStream, contentLength);
                }
                else
                {
                    m_log.Debug($"[SRestObjReq]: Oops! no content found in response stream from {method} {requestUrl}");
                }

                ticks = Util.EnvironmentTickCountSubtract(ticks);
                if (ticks > WebUtil.LongCallTime)
                {
                    m_log.Info($"[LOGHTTP]: Slow SRestObjReq {reqnum} {method} {requestUrl} took {ticks}ms, {rcvlen}bytes");
                }
                else if (WebUtil.DebugLevel >= 4)
                {
                    m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {ticks}ms");
                }
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode is HttpStatusCode status)
                {
                    if (status == HttpStatusCode.Unauthorized)
                    {
                        m_log.Error($"[SRestObjReq]:  GET {requestUrl} requires authentication");
                    }
                    else if (status != HttpStatusCode.NotFound)
                    {
                        m_log.Warn($"[SRestObjReq]: GET {requestUrl} returned error: {status}");
                    }
                }
                else
                    m_log.ErrorFormat(
                        "[SRestObjReq]: WebException for {0} {1} {2} {3}",
                        method, requestUrl, typeof(TResponse).ToString(), e.Message);
            }
            catch (System.InvalidOperationException)
            {
                // This is what happens when there is invalid XML
                m_log.Debug($"[SRestObjReq]: Invalid XML from {method} {requestUrl} {typeof(TResponse)}");
            }
            catch (Exception e)
            {
                m_log.Debug($"[SRestObjReq]: Exception on response from {method} {requestUrl}: {e.Message}");
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();
            }

            return deserial;
        }

        public static TResponse MakeGetRequest<TResponse>(string requestUrl, int pTimeout, IServiceAuth auth)
        {
            int reqnum = WebUtil.RequestNumber++;

            if (WebUtil.DebugLevel >= 3)
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} SRestObjReq GET {requestUrl}");

            int ticks = Util.EnvironmentTickCount();
            TResponse deserial = default;
            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;
            try
            {
                client = WebUtil.GetNewGlobalHttpClient(pTimeout);
                request = new(HttpMethod.Get, requestUrl);

                auth?.AddAuthorization(request.Headers);

                request.Headers.ExpectContinue = false;
                request.Headers.TransferEncodingChunked = false;

                //if (keepalive)
                {
                    request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=30, max=10");
                    request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
                    request.Headers.ConnectionClose = false;
                }
                //else
                //    request.Headers.TryAddWithoutValidation("Connection", "close");

                responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                responseMessage.EnsureSuccessStatusCode();

                int rcvlen = 0;
                if ((responseMessage.Content.Headers.ContentLength is long contentLength) && contentLength != 0)
                {
                    rcvlen = (int)contentLength;
                    using Stream respStream = responseMessage.Content.ReadAsStream();
                    deserial = XMLResponseHelper.LogAndDeserialize<TResponse>(
                        reqnum, respStream, contentLength);
                }
                else
                {
                    m_log.Debug($"[SRestObjReq]: Oops! no content found in response stream from GET {requestUrl}");
                }

                ticks = Util.EnvironmentTickCountSubtract(ticks);
                if (ticks > WebUtil.LongCallTime)
                {
                    m_log.Info($"[LOGHTTP]: Slow SRestObjReq  GET {reqnum} {requestUrl} took {ticks}ms, {rcvlen}bytes");
                }
                else if (WebUtil.DebugLevel >= 4)
                {
                    m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {ticks}ms");
                }
            }
            catch (HttpRequestException e)
            {
                if(e.StatusCode is HttpStatusCode status)
                {
                    if (status == HttpStatusCode.Unauthorized)
                    {
                        m_log.Error($"[SRestObjReq]:  GET {requestUrl} requires authentication");
                    }
                    else if (status != HttpStatusCode.NotFound)
                    {
                        m_log.Warn($"[SRestObjReq]: GET {requestUrl} returned error: {status}");
                    }
                }
                else
                    m_log.Error($"[SRestObjReq]: WebException for GET {requestUrl} {typeof(TResponse)} {e.Message}");
            }
            catch (System.InvalidOperationException)
            {
                // This is what happens when there is invalid XML
                m_log.Debug($"[SRestObjReq]: Invalid XML from GET {requestUrl} {typeof(TResponse)}");
            }
            catch (Exception e)
            {
                m_log.Debug($"[SRestObjReq]: Exception on response from GET {requestUrl}: {e.Message}");
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();
            }
            return deserial;
        }
    }

    public static class XMLResponseHelper
    {
        public static TResponse LogAndDeserialize<TResponse>(int reqnum, Stream respStream, long contentLength)
        {
            XmlSerializer deserializer = new (typeof(TResponse));
            if (WebUtil.DebugLevel >= 5)
            {
                const int blockLength = 4096;
                byte[] dataBuffer = new byte[blockLength];
                int curcount;
                using MemoryStream ms = new(4 * blockLength);
                if (contentLength == -1)
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
                            throw new EndOfStreamException($"End of stream reached with {remaining} bytes left to read");
                        ms.Write(dataBuffer, 0, curcount);
                        remaining -= curcount;
                    }
                }

                dataBuffer = ms.ToArray();
                WebUtil.LogResponseDetail(reqnum, System.Text.Encoding.UTF8.GetString(dataBuffer));

                ms.Position = 0;
                return (TResponse)deserializer.Deserialize(ms);
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
                m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} XML-RPC '{method}' to {url}");

            int tickstart = Util.EnvironmentTickCount();
            string responseStr = null;
            HttpClient client = null;
            try
            {
                ArrayList SendParams = new()
                {
                    ReqParams
                };

                XmlRpcRequest Req = new(method, SendParams);

                if (WebUtil.DebugLevel >= 5)
                {
                    string str = Req.ToString();
                    str = XElement.Parse(str).ToString(SaveOptions.DisableFormatting);
                    WebUtil.LogOutgoingDetail("SEND", reqnum, str);
                }

                client = WebUtil.GetNewGlobalHttpClient(-1);
                XmlRpcResponse Resp = Req.Send(url, client);

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
                    m_log.Error($"[LOGHTTP]: Error parsing XML-RPC response: {e.Message}");
                }

                if (Resp.IsFault)
                {
                    m_log.Debug(
                        $"[LOGHTTP]: XML-RPC request {reqnum} '{method}' to {url} FAILED: FaultCode={Resp.FaultCode}, {Resp.FaultString}");
                    return null;
                }

                Hashtable RespData = (Hashtable)Resp.Value;
                return RespData;
            }
            finally
            {
                client?.Dispose();

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
                    m_log.Debug($"[LOGHTTP]: HTTP OUT {reqnum} took {tickdiff}ms");
                }
            }
        }
    }
}
