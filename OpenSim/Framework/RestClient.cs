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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Web;
using log4net;

using OpenSim.Framework.ServiceAuth;

namespace OpenSim.Framework
{
    /// <summary>
    /// Implementation of a generic REST client
    /// </summary>
    /// <remarks>
    /// This class is a generic implementation of a REST (Representational State Transfer) web service. This
    /// </remarks>
    public class RestClient : IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private string realuri;

        #region member variables

        /// <summary>
        /// The base Uri of the web-service e.g. http://www.google.com
        /// </summary>
        private readonly string _url;

        /// <summary>
        /// Path elements of the query
        /// </summary>
        private readonly List<string> _pathElements = new();

        /// <summary>
        /// Parameter elements of the query, e.g. min=34
        /// </summary>
        private readonly Dictionary<string, string> _parameterElements = new();

        /// <summary>
        /// Request method. E.g. GET, POST, PUT or DELETE
        /// </summary>
        private string _method;

        /// <summary>
        /// Temporary buffer used to store bytes temporarily as they come in from the server
        /// </summary>
        private readonly byte[] _readbuf;

        /// <summary>
        /// MemoryStream representing the resulting resource
        /// </summary>
        private readonly MemoryStream _resource;

        /// <summary>
        /// Default time out period
        /// </summary>
        private const int DefaultTimeout = 90000; // 90 seconds timeout

        /// <summary>
        /// Default Buffer size of a block requested from the web-server
        /// </summary>
        private const int BufferSize = 4 * 4096; // Read blocks of 4 * 4 KB.

        #endregion member variables

        #region constructors

        /// <summary>
        /// Instantiate a new RestClient
        /// </summary>
        /// <param name="url">Web-service to query, e.g. http://osgrid.org:8003</param>
        public RestClient(string url)
        {
            _url = url;
            _readbuf = new byte[BufferSize];
            _resource = new MemoryStream();
            _lock = new object();
        }

        private readonly object _lock;

        #endregion constructors


        #region Dispose

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _resource.Dispose();
            }

            disposed = true;
        }

        #endregion Dispose


        /// <summary>
        /// Add a path element to the query, e.g. assets
        /// </summary>
        /// <param name="element">path entry</param>
        public void AddResourcePath(string element)
        {
            _pathElements.Add(Util.TrimEndSlash(element));
        }

        /// <summary>
        /// Add a query parameter to the Url
        /// </summary>
        /// <param name="name">Name of the parameter, e.g. min</param>
        /// <param name="value">Value of the parameter, e.g. 42</param>
        public void AddQueryParameter(string name, string value)
        {
            try
            {
                _parameterElements.Add(HttpUtility.UrlEncode(name), HttpUtility.UrlEncode(value));
            }
            catch (ArgumentException)
            {
                m_log.Error("[REST]: Query parameter " + name + " is already added.");
            }
            catch (Exception e)
            {
                m_log.Error("[REST]: An exception was raised adding query parameter to dictionary. Exception: {0}",e);
            }
        }

        /// <summary>
        /// Add a query parameter to the Url
        /// </summary>
        /// <param name="name">Name of the parameter, e.g. min</param>
        public void AddQueryParameter(string name)
        {
            try
            {
                _parameterElements.Add(HttpUtility.UrlEncode(name), null);
            }
            catch (ArgumentException)
            {
                m_log.Error("[REST]: Query parameter " + name + " is already added.");
            }
            catch (Exception e)
            {
                m_log.Error("[REST]: An exception was raised adding query parameter to dictionary. Exception: {0}",e);
            }
        }

        /// <summary>
        /// Web-Request method, e.g. GET, PUT, POST, DELETE
        /// </summary>
        public string RequestMethod
        {
            get { return _method; }
            set { _method = value; }
        }

        /// <summary>
        /// Build a Uri based on the initial Url, path elements and parameters
        /// </summary>
        /// <returns>fully constructed Uri</returns>
        private Uri buildUri()
        {
            StringBuilder sb = new();
            sb.Append(_url);

            foreach (string e in _pathElements)
            {
                sb.Append('/');
                sb.Append(e);
            }

            bool firstElement = true;
            foreach (KeyValuePair<string, string> kv in _parameterElements)
            {
                if (firstElement)
                {
                    sb.Append('?');
                    firstElement = false;
                }
                else
                    sb.Append('&');

                sb.Append(kv.Key);
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    sb.Append('=');
                    sb.Append(kv.Value);
                }
            }
            // realuri = sb.ToString();
            //m_log.InfoFormat("[REST CLIENT]: RestURL: {0}", realuri);
            return new Uri(sb.ToString());
        }

        /// <summary>
        /// Perform a synchronous request
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryStream Request()
        {
            return Request(null);
        }

        /// <summary>
        /// Perform a synchronous request
        /// </summary>
        public MemoryStream Request(IServiceAuth auth)
        {
            lock (_lock)
            {
                Uri uri = null;
                HttpResponseMessage responseMessage = null;
                HttpRequestMessage request = null;
                HttpClient client = null;
                try
                {
                    client = WebUtil.GetNewGlobalHttpClient(DefaultTimeout);
                    uri = buildUri();
                    request = new(new HttpMethod(RequestMethod), uri);

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


                    if (WebUtil.DebugLevel >= 3)
                        m_log.DebugFormat("[REST CLIENT] {0} to {1}", RequestMethod, uri);

                    //_request.ContentType = "application/xml";
                    responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                    responseMessage.EnsureSuccessStatusCode();

                    Stream respStream = responseMessage.Content.ReadAsStream();
                    int length = respStream.Read(_readbuf, 0, BufferSize);
                    while (length > 0)
                    {
                        _resource.Write(_readbuf, 0, length);
                        length = respStream.Read(_readbuf, 0, BufferSize);
                    }
                }
                catch (HttpRequestException e)
                {
                    if(uri is not null)
                    {
                        if (e.StatusCode is HttpStatusCode status)
                        {
                            if (status == HttpStatusCode.NotFound)
                            {
                                // This is often benign. E.g., requesting a missing asset will return 404.
                                m_log.DebugFormat("[REST CLIENT] Resource not found (404): {0}", uri.ToString());
                            }
                            else
                            {
                                m_log.Error($"[REST CLIENT] Error fetching resource from server: {uri} status: {status} {e.Message}");
                            }
                        }
                        else
                        {
                            m_log.Error($"[REST CLIENT] Error fetching resource from server: {uri} {e.Message}");
                        }
                    }
                    else
                    {
                        m_log.Error($"[REST CLIENT] Error fetching null resource from server: {e.Message}");
                    }
                    return null;
                }
                finally
                {
                    request?.Dispose();
                    responseMessage?.Dispose();
                    client?.Dispose();
                }

                if (_resource != null)
                {
                    _resource.Flush();
                    _resource.Seek(0, SeekOrigin.Begin);
                }

                if (WebUtil.DebugLevel >= 5)
                    WebUtil.LogOutgoingDetail("[REST CLIENT]", _resource);

                return _resource;
            }
        }

        // just sync post data, ignoring result
        public void POSTRequest(byte[] src, IServiceAuth auth)
        {
            Uri uri = null;
            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            HttpClient client = null;
            try
            {
                client = WebUtil.GetNewGlobalHttpClient(DefaultTimeout);
                uri = buildUri();
                request = new(HttpMethod.Post, uri);

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

                request.Content = new ByteArrayContent(src);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/xml");
                request.Content.Headers.TryAddWithoutValidation("Content-Length", src.Length.ToString());

                responseMessage = client.Send(request, HttpCompletionOption.ResponseContentRead);
                responseMessage.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                if(uri is not null)
                { 
                    if (e.StatusCode is HttpStatusCode status)
                        m_log.Warn($"[REST]: POST {uri} failed with status {status} and message {e.Message}");
                    else
                        m_log.Warn($"[REST]: POST {uri} failed with message {e.Message}");
                }
                else
                    m_log.Warn($"[REST]: POST failed {e.Message}");
                return;
            }
            catch (Exception e)
            {
                if (uri is not null)
                    m_log.Warn($"[REST]: POST {uri} failed with message {e.Message}");
                else
                    m_log.Warn($"[REST]: POST failed {e.Message}");
                return;
            }
            finally
            {
                request?.Dispose();
                responseMessage?.Dispose();
                client?.Dispose();
            }
        }
    }
}
