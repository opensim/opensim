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
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;
using System.Net.Http;
using System.Security.Authentication;
using System.Net.Http.Headers;

/*****************************************************
 *
 * ScriptsHttpRequests
 *
 * Implements the llHttpRequest and http_response
 * callback.
 *
 * This is a non shared module with shared static parts
 * **************************************************/

namespace OpenSim.Region.CoreModules.Scripting.HttpRequest
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "HttpRequestModule")]
    public class HttpRequestModule : INonSharedRegionModule, IHttpRequestModule
    {
        private struct ThrottleData
        {
            public double lastTime;
            public float control;
        }

        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static HttpClient VeriFyCertClient = null;
        private static HttpClient VeriFyNoCertClient = null;

        private static readonly object m_mainLock = new();
        private static int m_numberScenes;

        private static readonly string m_name = "HttpScriptRequests";

        private static OutboundUrlFilter m_outboundUrlFilter;
        private static int m_HttpBodyMaxLenMAX = 16384;

        private static float m_primPerSec = 1.0f;
        private static float m_primBurst = 3.0f;
        private static float m_primOwnerPerSec = 25.0f;
        private static float m_primOwnerBurst = 5.0f;

        public static JobEngine m_jobEngine = null;
        private static Dictionary<UUID, HttpRequestClass> m_pendingRequests;

        //this are per region/module
        private readonly ConcurrentQueue<HttpRequestClass> m_CompletedRequests = new();
        private readonly ConcurrentDictionary<uint, ThrottleData> m_RequestsThrottle = new();
        private readonly ConcurrentDictionary<UUID, ThrottleData> m_OwnerRequestsThrottle = new();

        public HttpRequestModule()
        {
        }

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            lock (m_mainLock)
            {
                // shared items
                if (m_jobEngine is null)
                {
                    WebProxy proxy = null;
                    string proxyurl = config.Configs["Startup"].GetString("HttpProxy");
                    if (!string.IsNullOrEmpty(proxyurl))
                    {
                        string[] proxyexceptsArray = null;
                        string proxyexcepts = config.Configs["Startup"].GetString("HttpProxyExceptions");
                        if (!string.IsNullOrEmpty(proxyexcepts))
                        {
                            proxyexceptsArray = proxyexcepts.Split(';');
                            if(proxyexceptsArray.Length == 0)
                                proxyexceptsArray = null;
                        }
                        proxy = proxyexceptsArray is null ?
                                new WebProxy(proxyurl, true) :
                                new WebProxy(proxyurl, true, proxyexceptsArray);
                    }

                    m_HttpBodyMaxLenMAX = config.Configs["Network"].GetInt("HttpBodyMaxLenMAX", m_HttpBodyMaxLenMAX);

                    m_outboundUrlFilter = new OutboundUrlFilter("Script HTTP request module", config);

                    int maxThreads = 8;
                    IConfig httpConfig = config.Configs["ScriptsHttpRequestModule"];
                    int httpTimeout = 30000;
                    if (httpConfig is not null)
                    {
                        maxThreads = httpConfig.GetInt("MaxPoolThreads", maxThreads);
                        m_primBurst = httpConfig.GetFloat("PrimRequestsBurst", m_primBurst);
                        m_primPerSec = httpConfig.GetFloat("PrimRequestsPerSec", m_primPerSec);
                        m_primOwnerBurst = httpConfig.GetFloat("PrimOwnerRequestsBurst", m_primOwnerBurst);
                        m_primOwnerPerSec = httpConfig.GetFloat("PrimOwnerRequestsPerSec", m_primOwnerPerSec);
                        httpTimeout = httpConfig.GetInt("RequestsTimeOut", httpTimeout);
                        if (httpTimeout > 60000)
                            httpTimeout = 60000;
                        else if (httpTimeout < 200)
                            httpTimeout = 200;
                    }

                    if (VeriFyNoCertClient is null)
                    {
                        SocketsHttpHandler shhnc = new()
                        {
                            AllowAutoRedirect = false,
                            AutomaticDecompression = DecompressionMethods.None,
                            ConnectTimeout = TimeSpan.FromMilliseconds(httpTimeout),
                            PreAuthenticate = false,
                            UseCookies = false,
                            MaxConnectionsPerServer = maxThreads < 10 ? maxThreads : 10,
                            PooledConnectionLifetime = TimeSpan.FromMinutes(3)
                        };
                        //shhnc.SslOptions.ClientCertificates = null,
                        shhnc.SslOptions.EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                        shhnc.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                        shhnc.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                        {
                            errors &= ~(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch);
                            return errors == SslPolicyErrors.None;
                        };
                        if (proxy is null)
                            shhnc.UseProxy = false;
                        else
                        {
                            shhnc.Proxy = proxy;
                            shhnc.UseProxy = true;
                        }

                        VeriFyNoCertClient = new HttpClient(shhnc)
                        {
                            Timeout = TimeSpan.FromMilliseconds(httpTimeout),
                            MaxResponseContentBufferSize = 2 * m_HttpBodyMaxLenMAX,
                        };
                        VeriFyNoCertClient.DefaultRequestHeaders.ExpectContinue = false;
                        VeriFyNoCertClient.DefaultRequestHeaders.ConnectionClose = true;
                    }

                    if (VeriFyCertClient is null)
                    {
                        SocketsHttpHandler shh = new()
                        {
                            AllowAutoRedirect = false,
                            AutomaticDecompression = DecompressionMethods.None,
                            ConnectTimeout = TimeSpan.FromMilliseconds((double)httpTimeout),
                            PreAuthenticate = false,
                            UseCookies = false,
                            MaxConnectionsPerServer = maxThreads < 10 ? maxThreads : 10,
                            PooledConnectionLifetime = TimeSpan.FromMinutes(3)
                        };

                        //shhnc.SslOptions.ClientCertificates = null,
                        shh.SslOptions.EnabledSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                        shh.SslOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                        shh.SslOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) =>
                        {
                            errors &= ~SslPolicyErrors.RemoteCertificateChainErrors;
                            return errors == SslPolicyErrors.None;
                        };
                        if (proxy is null)
                            shh.UseProxy = false;
                        else
                        {
                            shh.Proxy = proxy;
                            shh.UseProxy = true;
                        }
                        VeriFyCertClient = new HttpClient(shh)
                        {
                            Timeout = TimeSpan.FromMilliseconds(httpTimeout),
                            MaxResponseContentBufferSize = 2 * m_HttpBodyMaxLenMAX
                        };
                        VeriFyCertClient.DefaultRequestHeaders.ExpectContinue = false;
                        VeriFyCertClient.DefaultRequestHeaders.ConnectionClose = true;
                    }

                    m_pendingRequests = new Dictionary<UUID, HttpRequestClass>();

                    m_jobEngine = new JobEngine("ScriptsHttpReq", "ScriptsHttpReq", 2000, maxThreads);
                    m_jobEngine.Start();
                }
            }
        }

        public void AddRegion(Scene scene)
        {
            scene.RegisterModuleInterface<IHttpRequestModule>(this);
            Interlocked.Increment(ref m_numberScenes);
        }

        public void RemoveRegion(Scene scene)
        {
            scene.UnregisterModuleInterface<IHttpRequestModule>(this);
        }

        public void PostInitialise()
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
            int n = Interlocked.Decrement(ref m_numberScenes);
            if (n == 0)
            {
                lock(m_mainLock)
                {
                    if (m_jobEngine is not null)
                    {
                        m_jobEngine.Stop();
                        m_jobEngine = null;
                    }
                    VeriFyCertClient?.Dispose();
                    VeriFyCertClient = null;
                    VeriFyNoCertClient?.Dispose();
                    VeriFyNoCertClient = null;
                }
            }
        }

        public string Name
        {
            get { return m_name; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region IHttpRequestModule Members

        public UUID MakeHttpRequest(string url, string parameters, string body)
        {
            return UUID.Zero;
        }

        public HttpClient GetHttpClient(bool verify)
        {
            return verify ? VeriFyCertClient : VeriFyNoCertClient;
        }

        public bool CheckThrottle(uint localID, UUID ownerID)
        {
            double now = Util.GetTimeStamp();
            bool ret;

            if (m_RequestsThrottle.TryGetValue(localID, out ThrottleData th))
            {
                double delta = now - th.lastTime;
                th.lastTime = now;

                float add = (float)(m_primPerSec * delta);
                th.control += add;
                if (th.control > m_primBurst)
                {
                    th.control = m_primBurst - 1;
                    ret = true;
                }
                else
                {
                    ret = th.control > 0;
                    if (ret)
                        th.control--;
                }
            }
            else
            {
                th = new ThrottleData()
                {
                    lastTime = now,
                    control = m_primBurst - 1,
                };
                ret = true;
            }
            m_RequestsThrottle[localID] = th;

            if(!ret)
                return false;

            if (m_OwnerRequestsThrottle.TryGetValue(ownerID, out th))
            {
                double delta = now - th.lastTime;
                th.lastTime = now;

                float add = (float)(m_primOwnerPerSec * delta);
                th.control += add;
                if (th.control > m_primOwnerBurst)
                    th.control = m_primOwnerBurst - 1;
                else
                {
                    ret = th.control > 0;
                    if (ret)
                        th.control--;
                }
            }
            else
            {
                th = new ThrottleData()
                {
                    lastTime = now,
                    control = m_primBurst - 1
                };
            }
            m_OwnerRequestsThrottle[ownerID] = th;

            return ret;
        }

        public UUID StartHttpRequest(uint localID, UUID itemID, string url,
                List<string> parameters, Dictionary<string, string> headers, string body)
        {
            UUID reqID = UUID.Random();
            HttpRequestClass htc = new();

            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/LlHTTPRequest
            //
            // Parameters are expected in {key, value, ... , key, value}
            if (parameters is not null)
            {
                for (int i = 0; i < parameters.Count; i += 2)
                {
                    switch (Int32.Parse(parameters[i]))
                    {
                        case (int)HttpRequestConstants.HTTP_METHOD:
                            htc.HttpMethod = parameters[i + 1];
                            break;

                        case (int)HttpRequestConstants.HTTP_MIMETYPE:
                            htc.HttpMIMEType = parameters[i + 1];
                            break;

                        case (int)HttpRequestConstants.HTTP_BODY_MAXLENGTH:
                            if(int.TryParse(parameters[i + 1], out int len))
                            {
                                if(len > m_HttpBodyMaxLenMAX)
                                    len = m_HttpBodyMaxLenMAX;
                                else if(len < 64) //???
                                    len = 64;
                                htc.HttpBodyMaxLen = len;
                            }
                            break;

                        case (int)HttpRequestConstants.HTTP_VERIFY_CERT:
                            htc.HttpVerifyCert = (int.Parse(parameters[i + 1]) != 0);
                            break;

                        case (int)HttpRequestConstants.HTTP_VERBOSE_THROTTLE:
                            break;

                        case (int)HttpRequestConstants.HTTP_CUSTOM_HEADER:
                            // should not happen
                            //Parameters are in pairs and custom header takes
                            //arguments in pairs so adjust for header marker.
                            ++i;

                            //Maximum of 8 headers are allowed based on the
                            //Second Life documentation for llHTTPRequest.
                            for (int count = 1; count <= 8; ++count)
                            {
                                //Not enough parameters remaining for a header?
                                if (parameters.Count - i < 2)
                                   break;

                                int nexti = i + 2;
                                if (nexti >= parameters.Count || Char.IsDigit(parameters[nexti][0]))
                                    break;

                                i = nexti;
                            }
                            break;

                        case (int)HttpRequestConstants.HTTP_PRAGMA_NO_CACHE:
                            htc.HttpPragmaNoCache = (int.Parse(parameters[i + 1]) != 0);
                            break;
                    }
                }
            }

            htc.RequestModule = this;
            htc.LocalID = localID;
            htc.ItemID = itemID;
            htc.Url = url;
            htc.ReqID = reqID;
            htc.OutboundBody = body;
            htc.Headers = headers;

            lock (m_mainLock)
                m_pendingRequests.Add(reqID, htc);

            htc.Process();
            return reqID;
        }

        /// <summary>
        /// Would a caller to this module be allowed to make a request to the given URL?
        /// </summary>
        /// <returns></returns>
        public bool CheckAllowed(Uri url)
        {
            return m_outboundUrlFilter.CheckAllowed(url);
        }

        public void StopHttpRequest(uint localID, UUID m_itemID)
        {
            List<UUID> toremove = new();
            lock (m_mainLock)
            {
                foreach (HttpRequestClass tmpReq in m_pendingRequests.Values)
                {
                    if(m_itemID.Equals(tmpReq.ItemID))
                    {
                        tmpReq.Stop();
                        toremove.Add(tmpReq.ReqID);
                    }
                }
                foreach(UUID id in toremove)
                    m_pendingRequests.Remove(id);
            }
            if (m_RequestsThrottle.TryGetValue(localID, out ThrottleData th))
            {
                if (th.control + m_primOwnerPerSec * (Util.GetTimeStamp() - th.lastTime) >= m_primBurst)
                    m_RequestsThrottle.TryRemove(localID, out _);
            }
        }

        /*
        * TODO
        * Not sure how important ordering is is here - the next first
        * one completed in the list is returned, based soley on its list
        * position, not the order in which the request was started or
        * finished.  I thought about setting up a queue for this, but
        * it will need some refactoring and this works 'enough' right now
        */
        public void GotCompletedRequest(HttpRequestClass req)
        {
            lock (m_mainLock)
            {
                m_pendingRequests.Remove(req.ReqID);
                if (!req.Removed)
                    m_CompletedRequests.Enqueue(req);
            }
        }

        public IServiceRequest GetNextCompletedRequest()
        {
            if(m_CompletedRequests.TryDequeue(out HttpRequestClass req))
                return req;

            return null;
        }

        public void RemoveCompletedRequest(UUID reqId)
        {
            lock (m_mainLock)
            {
                if (m_pendingRequests.TryGetValue(reqId, out HttpRequestClass tmpReq))
                {
                    tmpReq.Stop();
                    m_pendingRequests.Remove(reqId);
                }
            }
        }

        #endregion
    }

    public class HttpRequestClass : IServiceRequest
    {
        private static readonly string[] s_wellKnownContentHeaders = {
            "Content-Disposition",
            "Content-Encoding",
            "Content-Language",
            "Content-Length",
            "Content-Location",
            "Content-MD5",
            "Content-Range",
            "Content-Type",
            "Expires",
            "Last-Modified"
        };

        private bool IsWellKnownContentHeader(string header)
        {
            foreach (string contentHeaderName in s_wellKnownContentHeaders)
            {
                if (string.Equals(header, contentHeaderName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        private void AddHeader(string headerName, string value, HttpRequestMessage request)
        {
            if (IsWellKnownContentHeader(headerName))
            {
                request.Content ??= new ByteArrayContent(Array.Empty<byte>());
                request.Content.Headers.TryAddWithoutValidation(headerName, value);
            }
            else
                request.Headers.TryAddWithoutValidation(headerName, value);
        }

        /// <summary>
        /// Module that made this request.
        /// </summary>
        public HttpRequestModule RequestModule { get; set; }

        public bool HttpVerifyCert = true;
        public bool Removed;

        // Parameter members and default values
        public int HttpBodyMaxLen = 2048;
        public string HttpMethod  = "GET";
        public string HttpMIMEType = "text/plain;charset=utf-8";

        public bool HttpPragmaNoCache = false;

        // Request info
        public bool Finished { get; }
        public UUID ReqID { get; set; }
        public UUID ItemID {  get; set;}
        public uint LocalID { get; set;}

        /// <summary>
        /// Number of HTTP redirects that this request has been through.
        /// </summary>
        public int Redirects { get; private set; }

        /// <summary>
        /// Maximum number of HTTP redirects allowed for this request.
        /// </summary>
        public int MaxRedirects { get; set; } = 10;

        public string OutboundBody;
        public string ResponseBody;
        public Dictionary<string, string> Headers;
        public int Status;
        public string Url;

        public void Process()
        {
            HttpRequestModule.m_jobEngine?.QueueJob("", SendRequest);
        }

        public void SendRequest()
        {
            if (Removed)
                 return;

            HttpResponseMessage responseMessage = null;
            HttpRequestMessage request = null;
            try
            {
                HttpClient client = RequestModule.GetHttpClient(HttpVerifyCert);
                request = new (new HttpMethod(HttpMethod), Url);

                int datalen;
                if (!string.IsNullOrEmpty(OutboundBody))
                {
                    byte[] data = Util.UTF8.GetBytes(OutboundBody);
                    datalen = data.Length;
                    request.Content = new ByteArrayContent(data);
                }
                else
                    datalen = -1;

                foreach (KeyValuePair<string, string> entry in Headers)
                    AddHeader(entry.Key, entry.Value, request);

                if (HttpPragmaNoCache)
                    request.Headers.TryAddWithoutValidation("Pragma", "no-cache");

                request.Headers.TransferEncodingChunked = false;
                request.Headers.ConnectionClose = true;

                if (datalen > 0)
                {
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", HttpMIMEType);
                    request.Content.Headers.TryAddWithoutValidation("Content-Length", datalen.ToString());
                }

                if (Removed)
                    return;

                responseMessage = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
                
                if (Removed)
                    return;

                Status = (int)responseMessage.StatusCode;
                if (responseMessage.Content is not null)
                {
                    int len;
                    if(responseMessage.Content.Headers is not null && responseMessage.Content.Headers.ContentLength is long l)
                        len = (int)l;
                    else
                        len = -1;

                    Stream resStream = responseMessage.Content.ReadAsStream();

                    if(resStream is not null)
                    {
                        int maxBytes =  (len < 0  || len > HttpBodyMaxLen) ? HttpBodyMaxLen : len;
                        byte[] buf = new byte[maxBytes];

                        int totalBodyBytes = 0;
                        int count;
                        do
                        {
                            count = resStream.Read(buf, totalBodyBytes, maxBytes - totalBodyBytes);
                            totalBodyBytes += count;
                        } while (count > 0 && totalBodyBytes < maxBytes); // any more data to read?
                        resStream.Dispose();

                        if (totalBodyBytes > 0)
                        {
                            string tempString = Util.UTF8.GetString(buf, 0, totalBodyBytes);
                            ResponseBody = tempString.Replace("\r", "");
                        }
                    }
                }
            }
            catch (HttpRequestException e)
            {              
                Status = e.StatusCode is null ? 499 : (int)e.StatusCode;
                ResponseBody = e.Message;
            }
            //catch (Exception e)
            catch
            {
                // Don't crash on anything else
            }
            finally
            {
                if (!Removed)
                {
                    // We need to resubmit ?
                    if (Status == (int)HttpStatusCode.MovedPermanently ||
                            Status == (int)HttpStatusCode.Found ||
                            Status == (int)HttpStatusCode.SeeOther ||
                            Status == (int)HttpStatusCode.TemporaryRedirect)
                    {
                        if (Redirects >= MaxRedirects)
                        {
                            Status = 499;//.ClientErrorJoker;
                            ResponseBody = "Number of redirects exceeded max redirects";
                            RequestModule.GotCompletedRequest(this);
                        }
                        else if (responseMessage is not null && responseMessage.Headers is not null)
                        {
                            Uri locationUri = responseMessage.Headers.Location;
                            if (locationUri == null)
                            {
                                Status = 499;//ClientErrorJoker;
                                ResponseBody = "HTTP redirect code but no location header";
                                RequestModule.GotCompletedRequest(this);
                            }
                            else
                            {
                                bool validredir = true;
                                if(!locationUri.IsAbsoluteUri)
                                {
                                    Uri reqUri = responseMessage.RequestMessage.RequestUri;
                                    string newloc = reqUri.Scheme +"://" + reqUri.DnsSafeHost + ":" +
                                        reqUri.Port +"/" + locationUri.OriginalString;
                                    if (!Uri.TryCreate(newloc, UriKind.RelativeOrAbsolute, out locationUri))
                                    {
                                        Status = 499;//ClientErrorJoker;
                                        ResponseBody = "HTTP redirect code but invalid location header";
                                        RequestModule.GotCompletedRequest(this);
                                        validredir = false;
                                    }
                                }
                                if(validredir)
                                {
                                    if (!RequestModule.CheckAllowed(locationUri))
                                    {
                                        Status = 499;//ClientErrorJoker;
                                        ResponseBody = "URL from HTTP redirect blocked: " + locationUri.AbsoluteUri;
                                        RequestModule.GotCompletedRequest(this);
                                    }
                                    else
                                    {
                                        Status = 0;
                                        Url = locationUri.AbsoluteUri;
                                        Redirects++;
                                        ResponseBody = null;

                                        //m_log.DebugFormat("Redirecting to [{0}]", Url);

                                        Process();
                                    }
                                }
                                else
                                {
                                    Status = 499;//ClientErrorJoker;
                                    ResponseBody = "HTTP redirect code but invalid location header";
                                    RequestModule.GotCompletedRequest(this);
                                }
                            }
                        }
                    }
                    else
                    {
                        if(Status == 0)
                            Status = 499;

                        ResponseBody ??= string.Empty;
                        RequestModule.GotCompletedRequest(this);
                    }
                }
                responseMessage?.Dispose();
                request.Dispose();
            }
        }

        public void Stop()
        {
            Removed = true;
        }
    }
}
