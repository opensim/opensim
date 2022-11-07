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

        private static readonly object m_mainLock = new object();
        private static int m_numberScenes;
        private static int m_httpTimeout = 30000;
        private static readonly string m_name = "HttpScriptRequests";

        private static OutboundUrlFilter m_outboundUrlFilter;
        private static string m_proxyurl = "";
        private static string m_proxyexcepts = "";

        private static float m_primPerSec = 1.0f;
        private static float m_primBurst = 3.0f;
        private static float m_primOwnerPerSec = 25.0f;
        private static float m_primOwnerBurst = 5.0f;

        public static JobEngine m_jobEngine = null;
        private static Dictionary<UUID, HttpRequestClass> m_pendingRequests;

        //this are per region/module
        private readonly ConcurrentQueue<HttpRequestClass> m_CompletedRequests = new ConcurrentQueue<HttpRequestClass>();
        private readonly ConcurrentDictionary<uint, ThrottleData> m_RequestsThrottle = new ConcurrentDictionary<uint, ThrottleData>();
        private readonly ConcurrentDictionary<UUID, ThrottleData> m_OwnerRequestsThrottle = new ConcurrentDictionary<UUID, ThrottleData>();


        public HttpRequestModule()
        {
        }

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            lock (m_mainLock)
            {
                // shared items
                if (m_jobEngine == null)
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
                        if (m_httpTimeout > 60000)
                            m_httpTimeout = 60000;
                        else if (m_httpTimeout < 200)
                            m_httpTimeout = 200;
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
                    if (m_jobEngine != null)
                    {
                        m_jobEngine.Stop();
                        m_jobEngine = null;
                    }
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

            lock (m_mainLock)
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

        public void StopHttpRequest(uint localID, UUID m_itemID)
        {
            List<UUID> toremove = new List<UUID>();
            lock (m_mainLock)
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
            if (m_RequestsThrottle.TryGetValue(localID, out ThrottleData th))
            {
                if (th.control + m_primOwnerPerSec * (Util.GetTimeStamp() - th.lastTime) >= m_primBurst)
                    m_RequestsThrottle.TryRemove(localID, out ThrottleData dummy);
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
        public bool Removed;

        public static int HttpBodyMaxLenMAX = 16384;

        // Parameter members and default values
        public int HttpBodyMaxLen = 2048;
        public string HttpMethod  = "GET";
        public string HttpMIMEType = "text/plain;charset=utf-8";
        public int HttpTimeout;
        public bool HttpVerifyCert = true;

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

        public string ResponseBody;
        public List<string> ResponseMetadata;
        public Dictionary<string, string> ResponseHeaders;
        public int Status;
        public string Url;

        public void Process()
        {
            HttpRequestModule.m_jobEngine?.QueueJob("", SendRequest);
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
                HttpWebRequest Request = sender as HttpWebRequest;
                // We don't case about encryption, get out of here
                if (Request.Headers.Get("NoVerifyCert") != null)
                    return true;
            }

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

            HttpWebRequest Request;
            HttpWebResponse response = null;
            Stream resStream = null;
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

                if (Removed)
                    return;

                // Encode outbound data
                if (!string.IsNullOrEmpty(OutboundBody))
                {
                    byte[] data = Util.UTF8.GetBytes(OutboundBody);

                    Request.ContentLength = data.Length;
                    using (Stream bstream = Request.GetRequestStream())
                        bstream.Write(data, 0, data.Length);
                    data = null;
                }

                if (Removed)
                    return;

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

                if (Removed)
                    return;

                Status = (int)response.StatusCode;

                byte[] buf = new byte[HttpBodyMaxLenMAX + 16];
                int count = 0;

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
                    string tempString = Util.UTF8.GetString(buf, 0, totalBodyBytes);
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
                    Status = 499; //ClientErrorJoker;
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

                if(!Removed)
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
                        else
                        {
                            string location = response.Headers["Location"];
                            if (location == null)
                            {
                                Status = 499;//ClientErrorJoker;
                                ResponseBody = "HTTP redirect code but no location header";
                                RequestModule.GotCompletedRequest(this);
                            }
                            else
                            {
                                if(Uri.TryCreate(location, UriKind.RelativeOrAbsolute, out Uri locationUri))
                                {
                                    bool validredir = true;
                                    if(!locationUri.IsAbsoluteUri)
                                    {
                                        string newloc = response.ResponseUri.Scheme +"://" + response.ResponseUri.DnsSafeHost + ":" + 
                                            response.ResponseUri.Port +"/" + location;
                                        if (!Uri.TryCreate(newloc, UriKind.RelativeOrAbsolute, out locationUri))
                                        {
                                            Status = 499;//ClientErrorJoker;
                                            ResponseBody = "HTTP redirect code but invalid location header";
                                            RequestModule.GotCompletedRequest(this);
                                            validredir = false;
                                        }
                                        location = newloc;
                                    }
                                    if(validredir)
                                    {
                                        if (!RequestModule.CheckAllowed(locationUri))
                                        {
                                            Status = 499;//ClientErrorJoker;
                                            ResponseBody = "URL from HTTP redirect blocked: " + location;
                                            RequestModule.GotCompletedRequest(this);
                                        }
                                        else
                                        {
                                            Status = 0;
                                            Url = location;
                                            Redirects++;
                                            ResponseBody = null;

                                            //m_log.DebugFormat("Redirecting to [{0}]", Url);

                                            Process();
                                        }
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
                        if (ResponseBody == null)
                            ResponseBody = string.Empty;
                        RequestModule.GotCompletedRequest(this);
                    }
                }
            }
        }

        public void Stop()
        {
            Removed = true;
        }
    }
}
