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
using System.Net.Mail;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;
using Amib.Threading;

/*****************************************************
 *
 * ScriptsHttpRequests
 *
 * Implements the llHttpRequest and http_response
 * callback.
 *
 * Some stuff was already in LSLLongCmdHandler, and then
 * there was this file with a stub class in it.  So,
 * I am moving some of the objects and functions out of
 * LSLLongCmdHandler, such as the HttpRequestClass, the
 * start and stop methods, and setting up pending and
 * completed queues.  These are processed in the
 * LSLLongCmdHandler polling loop.  Similiar to the
 * XMLRPCModule, since that seems to work.
 *
 * //TODO
 *
 * This probably needs some throttling mechanism but
 * it's wide open right now.  This applies to both
 * number of requests and data volume.
 *
 * Linden puts all kinds of header fields in the requests.
 * Not doing any of that:
 * User-Agent
 * X-SecondLife-Shard
 * X-SecondLife-Object-Name
 * X-SecondLife-Object-Key
 * X-SecondLife-Region
 * X-SecondLife-Local-Position
 * X-SecondLife-Local-Velocity
 * X-SecondLife-Local-Rotation
 * X-SecondLife-Owner-Name
 * X-SecondLife-Owner-Key
 *
 * HTTPS support
 *
 * Configurable timeout?
 * Configurable max response size?
 * Configurable
 *
 * **************************************************/

namespace OpenSim.Region.CoreModules.Scripting.HttpRequest
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "HttpRequestModule")]
    public class HttpRequestModule : ISharedRegionModule, IHttpRequestModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private object m_httpListLock = new object();
        private int m_httpTimeout = 30000;
        private string m_name = "HttpScriptRequests";

        private OutboundUrlFilter m_outboundUrlFilter;
        private string m_proxyurl = "";
        private string m_proxyexcepts = "";

        private float m_primPerSec = 1.0f;
        private float m_primBurst = 3.0f;
        private float m_primOwnerPerSec = 25.0f;
        private float m_primOwnerBurst = 5.0f;

        private struct ThrottleData
        {
            public double lastTime;
            public float count;
        }

        // <request id, HttpRequestClass>
        private Dictionary<UUID, HttpRequestClass> m_pendingRequests;
        private ConcurrentQueue<HttpRequestClass> m_CompletedRequests;
        private ConcurrentDictionary<uint, ThrottleData> m_RequestsThrottle;
        private ConcurrentDictionary<UUID, ThrottleData> m_OwnerRequestsThrottle;

        public static SmartThreadPool ThreadPool = null;

        public HttpRequestModule()
        {
        }

        #region IHttpRequestModule Members

        public UUID MakeHttpRequest(string url, string parameters, string body)
        {
            return UUID.Zero;
        }

        public bool CheckThrottle(uint localID, UUID ownerID)
        {
            ThrottleData th;
            double now = Util.GetTimeStamp();
            bool ret;

            if (m_RequestsThrottle.TryGetValue(localID, out th))
            {
                double delta = now - th.lastTime;
                th.lastTime = now;

                float add = (float)(m_primPerSec * delta);
                th.count += add;
                if (th.count > m_primBurst)
                    th.count = m_primBurst;

                ret = th.count > 0;
                if (ret)
                    th.count--;
            }
            else
            {
                th = new ThrottleData()
                {
                    lastTime = now,
                    count = m_primBurst - 1
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
                th.count += add;
                if (th.count > m_primOwnerBurst)
                    th.count = m_primOwnerBurst;

                ret = th.count > 0;
                if (ret)
                    th.count--;
            }
            else
            {
                th = new ThrottleData()
                {
                    lastTime = now,
                    count = m_primOwnerBurst - 1
                };
            }
            m_OwnerRequestsThrottle[ownerID] = th;

            return ret;
        }

        public UUID StartHttpRequest(
            uint localID, UUID itemID, string url, List<string> parameters, Dictionary<string, string> headers, string body,
            out HttpInitialRequestStatus status)
        {
            if (!CheckAllowed(new Uri(url)))
            {
                status = HttpInitialRequestStatus.DISALLOWED_BY_FILTER;
                return UUID.Zero;
            }

            UUID reqID = UUID.Random();
            HttpRequestClass htc = new HttpRequestClass();

            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/LlHTTPRequest
            //
            // Parameters are expected in {key, value, ... , key, value}
            if (parameters != null)
            {
                string[] parms = parameters.ToArray();
                for (int i = 0; i < parms.Length; i += 2)
                {
                    switch (Int32.Parse(parms[i]))
                    {
                        case (int)HttpRequestConstants.HTTP_METHOD:

                            htc.HttpMethod = parms[i + 1];
                            break;

                        case (int)HttpRequestConstants.HTTP_MIMETYPE:

                            htc.HttpMIMEType = parms[i + 1];
                            break;

                        case (int)HttpRequestConstants.HTTP_BODY_MAXLENGTH:

                            int len;
                            if(int.TryParse(parms[i + 1], out len))
                            {
                                if(len > HttpRequestClass.HttpBodyMaxLenMAX)
                                    len = HttpRequestClass.HttpBodyMaxLenMAX;
                                else if(len < 64) //???
                                    len = 64;
                                htc.HttpBodyMaxLen = len;
                            }
                            break;

                        case (int)HttpRequestConstants.HTTP_VERIFY_CERT:
                            htc.HttpVerifyCert = (int.Parse(parms[i + 1]) != 0);
                            break;

                        case (int)HttpRequestConstants.HTTP_VERBOSE_THROTTLE:

                            // TODO implement me
                            break;

                        case (int)HttpRequestConstants.HTTP_CUSTOM_HEADER:
                            //Parameters are in pairs and custom header takes
                            //arguments in pairs so adjust for header marker.
                            ++i;

                            //Maximum of 8 headers are allowed based on the
                            //Second Life documentation for llHTTPRequest.
                            for (int count = 1; count <= 8; ++count)
                            {
                                //Not enough parameters remaining for a header?
                                if (parms.Length - i < 2)
                                    break;

                                if (htc.HttpCustomHeaders == null)
                                    htc.HttpCustomHeaders = new List<string>();

                                htc.HttpCustomHeaders.Add(parms[i]);
                                htc.HttpCustomHeaders.Add(parms[i+1]);
                                int nexti = i + 2;
                                if (nexti >= parms.Length || Char.IsDigit(parms[nexti][0]))
                                    break;

                                i = nexti;
                            }
                            break;

                        case (int)HttpRequestConstants.HTTP_PRAGMA_NO_CACHE:
                            htc.HttpPragmaNoCache = (int.Parse(parms[i + 1]) != 0);
                            break;
                    }
                }
            }

            htc.RequestModule = this;
            htc.LocalID = localID;
            htc.ItemID = itemID;
            htc.Url = url;
            htc.ReqID = reqID;
            htc.HttpTimeout = m_httpTimeout;
            htc.OutboundBody = body;
            htc.ResponseHeaders = headers;
            htc.proxyurl = m_proxyurl;
            htc.proxyexcepts = m_proxyexcepts;

            // Same number as default HttpWebRequest.MaximumAutomaticRedirections
            htc.MaxRedirects = 50;

            lock (m_httpListLock)
                m_pendingRequests.Add(reqID, htc);

            htc.Process();
            status = HttpInitialRequestStatus.OK;
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

        public void StopHttpRequest(uint m_localID, UUID m_itemID)
        {
            List<UUID> toremove = new List<UUID>();
            lock (m_httpListLock)
            {
                foreach (HttpRequestClass tmpReq in m_pendingRequests.Values)
                {
                    if(tmpReq.ItemID == m_itemID)
                    {
                        tmpReq.Stop();
                        toremove.Add(tmpReq.ReqID);
                    }
                }
                foreach(UUID id in toremove)
                    m_pendingRequests.Remove(id);
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
            lock (m_httpListLock)
            {
                if (req.Removed)
                    return;
                m_pendingRequests.Remove(req.ReqID);
                m_CompletedRequests.Enqueue(req);
            }
        }

        public IServiceRequest GetNextCompletedRequest()
        {
            HttpRequestClass req;
            if(m_CompletedRequests.TryDequeue(out req))
                return req;

            return null;
        }

        public void RemoveCompletedRequest(UUID reqId)
        {
            lock (m_httpListLock)
            {
                HttpRequestClass tmpReq;
                if (m_pendingRequests.TryGetValue(reqId, out tmpReq))
                {
                    tmpReq.Stop();
                    m_pendingRequests.Remove(reqId);
                }
            }
        }

        #endregion

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            m_proxyurl = config.Configs["Startup"].GetString("HttpProxy");
            m_proxyexcepts = config.Configs["Startup"].GetString("HttpProxyExceptions");

            HttpRequestClass.HttpBodyMaxLenMAX = config.Configs["Network"].GetInt("HttpBodyMaxLenMAX", 16384);

            m_outboundUrlFilter = new OutboundUrlFilter("Script HTTP request module", config);

            int maxThreads = 8;
            IConfig httpConfig = config.Configs["ScriptsHttpRequestModule"];
            if (httpConfig != null)
            {
                maxThreads = httpConfig.GetInt("MaxPoolThreads", maxThreads);
                m_primBurst = httpConfig.GetFloat("PrimRequestsBurst", m_primBurst);
                m_primPerSec = httpConfig.GetFloat("PrimRequestsPerSec", m_primPerSec);
                m_primOwnerBurst = httpConfig.GetFloat("PrimOwnerRequestsBurst", m_primOwnerBurst);
                m_primOwnerPerSec = httpConfig.GetFloat("PrimOwnerRequestsPerSec", m_primOwnerPerSec);
                m_httpTimeout = httpConfig.GetInt("RequestsTimeOut", m_httpTimeout);
                if(m_httpTimeout > 60000)
                    m_httpTimeout = 60000;
                else if(m_httpTimeout < 200)
                    m_httpTimeout = 200;
            }

            m_pendingRequests = new Dictionary<UUID, HttpRequestClass>();
            m_CompletedRequests = new ConcurrentQueue<HttpRequestClass>();
            m_RequestsThrottle = new ConcurrentDictionary<uint, ThrottleData>();
            m_OwnerRequestsThrottle = new ConcurrentDictionary<UUID, ThrottleData>();

            // First instance sets this up for all sims
            if (ThreadPool == null)
            {
                STPStartInfo startInfo = new STPStartInfo();
                startInfo.IdleTimeout = 2000;
                startInfo.MaxWorkerThreads = maxThreads;
                startInfo.MinWorkerThreads = 0;
                startInfo.ThreadPriority = ThreadPriority.BelowNormal;
                startInfo.StartSuspended = true;
                startInfo.ThreadPoolName = "ScriptsHttpReq";

                ThreadPool = new SmartThreadPool(startInfo);
                ThreadPool.Start();
            }
        }

        public void AddRegion(Scene scene)
        {
            scene.RegisterModuleInterface<IHttpRequestModule>(this);
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
            ThreadPool.Shutdown();
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
    }

    public class HttpRequestClass : IServiceRequest
    {
        // Constants for parameters
        // public const int HTTP_BODY_MAXLENGTH = 2;
        // public const int HTTP_METHOD = 0;
        // public const int HTTP_MIMETYPE = 1;
        // public const int HTTP_VERIFY_CERT = 3;
        // public const int HTTP_VERBOSE_THROTTLE = 4;
        // public const int HTTP_CUSTOM_HEADER = 5;
        // public const int HTTP_PRAGMA_NO_CACHE = 6;

        /// <summary>
        /// Module that made this request.
        /// </summary>
        public HttpRequestModule RequestModule { get; set; }

        public bool Finished { get; private set;}
        public bool Removed{ get; set;}

        public static int HttpBodyMaxLenMAX = 16384;

        // Parameter members and default values
        public int HttpBodyMaxLen = 2048;
        public string HttpMethod  = "GET";
        public string HttpMIMEType = "text/plain;charset=utf-8";
        public int HttpTimeout;
        public bool HttpVerifyCert = true;
        public IWorkItemResult WorkItem = null;

        //public bool HttpVerboseThrottle = true; // not implemented
        public List<string> HttpCustomHeaders = null;
        public bool HttpPragmaNoCache = true;

        // Request info
        public UUID ReqID { get; set; }
        public UUID ItemID {  get; set;}
        public uint LocalID { get; set;}

        public string proxyurl;
        public string proxyexcepts;

        /// <summary>
        /// Number of HTTP redirects that this request has been through.
        /// </summary>
        public int Redirects { get; private set; }

        /// <summary>
        /// Maximum number of HTTP redirects allowed for this request.
        /// </summary>
        public int MaxRedirects { get; set; }

        public string OutboundBody;

        public HttpWebRequest Request;
        public string ResponseBody;
        public List<string> ResponseMetadata;
        public Dictionary<string, string> ResponseHeaders;
        public int Status;
        public string Url;

        public void Process()
        {
            WorkItem = HttpRequestModule.ThreadPool.QueueWorkItem(new WorkItemCallback(StpSendWrapper), null);
        }

        private object StpSendWrapper(object o)
        {
            SendRequest();
            return null;
        }

        public static bool ValidateServerCertificate(
            object sender,
            X509Certificate  certificate,
            X509Chain  chain,
            SslPolicyErrors  sslPolicyErrors)
        {
            // If this is a web request we need to check the headers first
            // We may want to ignore SSL
            if (sender is HttpWebRequest)
            {
                HttpWebRequest Request = (HttpWebRequest)sender;
                ServicePoint sp = Request.ServicePoint;

                // We don't case about encryption, get out of here
                if (Request.Headers.Get("NoVerifyCert") != null)
                {
                    return true;
                }

                // If there was an upstream cert verification error, bail
                if ((((int)sslPolicyErrors) & ~4) != 0)
                    return false;

                return true;
            }

            // If it's not HTTP, trust .NET to check it
            if ((((int)sslPolicyErrors) & ~4) != 0)
                return false;

            return true;
        }

        /*
         * TODO: More work on the response codes.  Right now
         * returning 200 for success or 499 for exception
         */

        public void SendRequest()
        {
            if(Removed)
                 return;

            HttpWebResponse response = null;
            Stream resStream = null;
            byte[] buf = new byte[HttpBodyMaxLenMAX + 16];
            string tempString = null;
            int count = 0;

            try
            {
                Request = (HttpWebRequest)WebRequest.Create(Url);
                Request.ServerCertificateValidationCallback = ValidateServerCertificate;

                Request.AllowAutoRedirect = false;
                Request.KeepAlive = false;
                Request.Timeout = HttpTimeout;

                //This works around some buggy HTTP Servers like Lighttpd
                Request.ServicePoint.Expect100Continue = false;

                Request.Method = HttpMethod;
                Request.ContentType = HttpMIMEType;

                if (!HttpVerifyCert)
                {
                    // We could hijack Connection Group Name to identify
                    // a desired security exception.  But at the moment we'll use a dummy header instead.
                    Request.Headers.Add("NoVerifyCert", "true");
                }
//                else
//                {
//                    Request.ConnectionGroupName="Verify";
//                }

                if (!HttpPragmaNoCache)
                {
                    Request.Headers.Add("Pragma", "no-cache");
                }

                if (HttpCustomHeaders != null)
                {
                    for (int i = 0; i < HttpCustomHeaders.Count; i += 2)
                        Request.Headers.Add(HttpCustomHeaders[i],
                                            HttpCustomHeaders[i+1]);
                }

                if (!string.IsNullOrEmpty(proxyurl))
                {
                    if (!string.IsNullOrEmpty(proxyexcepts))
                    {
                        string[] elist = proxyexcepts.Split(';');
                        Request.Proxy = new WebProxy(proxyurl, true, elist);
                    }
                    else
                    {
                        Request.Proxy = new WebProxy(proxyurl, true);
                    }
                }

                foreach (KeyValuePair<string, string> entry in ResponseHeaders)
                    if (entry.Key.ToLower().Equals("user-agent"))
                        Request.UserAgent = entry.Value;
                    else
                        Request.Headers[entry.Key] = entry.Value;

                // Encode outbound data
                if (!string.IsNullOrEmpty(OutboundBody))
                {
                    byte[] data = Util.UTF8.GetBytes(OutboundBody);

                    Request.ContentLength = data.Length;
                    using (Stream bstream = Request.GetRequestStream())
                        bstream.Write(data, 0, data.Length);
                }

                try
                {
                    // execute the request
                    response = (HttpWebResponse) Request.GetResponse();
                }
                catch (WebException e)
                {
                    if (e.Status != WebExceptionStatus.ProtocolError)
                    {
                        throw;
                    }
                    response = (HttpWebResponse)e.Response;
                }

                Status = (int)response.StatusCode;

                resStream = response.GetResponseStream();
                int totalBodyBytes = 0;
                int maxBytes = HttpBodyMaxLen;
                if(maxBytes > buf.Length)
                    maxBytes = buf.Length;

                // we need to read all allowed or UFT8 conversion may fail
                do
                {
                    // fill the buffer with data
                    count = resStream.Read(buf, totalBodyBytes, maxBytes - totalBodyBytes);
                    totalBodyBytes += count;
                    if (totalBodyBytes >= maxBytes)
                        break;

                } while (count > 0); // any more data to read?

                if(totalBodyBytes > 0)
                {
                    tempString = Util.UTF8.GetString(buf, 0, totalBodyBytes);
                    ResponseBody = tempString.Replace("\r", "");
                }
                else
                    ResponseBody = "";
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse webRsp = (HttpWebResponse)((WebException)e).Response;
                    Status = (int)webRsp.StatusCode;
                    try
                    {
                        using (Stream responseStream = webRsp.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(responseStream))
                                ResponseBody = reader.ReadToEnd();
                        }
                    }
                    catch
                    {
                        ResponseBody = webRsp.StatusDescription;
                    }
                }
                else
                {
                    Status = (int)OSHttpStatusCode.ClientErrorJoker;
                    ResponseBody = e.Message;
                }
            }
//            catch (Exception e)
            catch
            {
                // Don't crash on anything else
            }
            finally
            {
                if (resStream != null)
                    resStream.Close();
                if (response != null)
                    response.Close();

                // We need to resubmit
                if (
                    (Status == (int)HttpStatusCode.MovedPermanently
                        || Status == (int)HttpStatusCode.Found
                        || Status == (int)HttpStatusCode.SeeOther
                        || Status == (int)HttpStatusCode.TemporaryRedirect))
                {
                    if (Redirects >= MaxRedirects)
                    {
                        Status = (int)OSHttpStatusCode.ClientErrorJoker;
                        ResponseBody = "Number of redirects exceeded max redirects";
                        WorkItem = null;
                        RequestModule.GotCompletedRequest(this);
                    }
                    else
                    {
                        string location = response.Headers["Location"];

                        if (location == null)
                        {
                            Status = (int)OSHttpStatusCode.ClientErrorJoker;
                            ResponseBody = "HTTP redirect code but no location header";
                            WorkItem = null;
                            RequestModule.GotCompletedRequest(this);
                        }
                        else if (!RequestModule.CheckAllowed(new Uri(location)))
                        {
                            Status = (int)OSHttpStatusCode.ClientErrorJoker;
                            ResponseBody = "URL from HTTP redirect blocked: " + location;
                            WorkItem = null;
                            RequestModule.GotCompletedRequest(this);
                        }
                        else
                        {
                            Status = 0;
                            Url = response.Headers["Location"];
                            Redirects++;
                            ResponseBody = null;

//                            m_log.DebugFormat("Redirecting to [{0}]", Url);

                            Process();
                        }
                    }
                }
                else
                {
                    WorkItem = null;
                    if (ResponseBody == null)
                        ResponseBody = String.Empty;
                    RequestModule.GotCompletedRequest(this);
                }
            }
        }

        public void Stop()
        {
            try
            {
                Removed = true;
                if(WorkItem == null)
                    return;

                if (!WorkItem.Cancel())
                    WorkItem.Cancel(true);
            }
            catch (Exception)
            {
            }
        }
    }
}
