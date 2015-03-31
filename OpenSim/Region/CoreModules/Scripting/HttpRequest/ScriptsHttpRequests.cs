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
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
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

        private object HttpListLock = new object();
        private int httpTimeout = 30000;
        private string m_name = "HttpScriptRequests";

        private OutboundUrlFilter m_outboundUrlFilter;
        private string m_proxyurl = "";
        private string m_proxyexcepts = "";

        // <request id, HttpRequestClass>
        private Dictionary<UUID, HttpRequestClass> m_pendingRequests;
        private Scene m_scene;
        // private Queue<HttpRequestClass> rpcQueue = new Queue<HttpRequestClass>();

        public HttpRequestModule()
        {
            ServicePointManager.ServerCertificateValidationCallback +=ValidateServerCertificate;
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

                // Check for policy and execute it if defined
                if (ServicePointManager.CertificatePolicy != null)
                {
                    return ServicePointManager.CertificatePolicy.CheckValidationResult (sp, certificate, Request, 0);
                }

                return true;
            }

            // If it's not HTTP, trust .NET to check it
            if ((((int)sslPolicyErrors) & ~4) != 0)
                return false;

            return true;
        }
        #region IHttpRequestModule Members

        public UUID MakeHttpRequest(string url, string parameters, string body)
        {
            return UUID.Zero;
        }

        public UUID StartHttpRequest(
            uint localID, UUID itemID, string url, List<string> parameters, Dictionary<string, string> headers, string body,
            out HttpInitialRequestStatus status)
        {
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

                            // TODO implement me
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

                                //Have we reached the end of the list of headers?
                                //End is marked by a string with a single digit.
                                //We already know we have at least one parameter
                                //so it is safe to do this check at top of loop.
                                if (Char.IsDigit(parms[i][0]))
                                    break;

                                if (htc.HttpCustomHeaders == null)
                                    htc.HttpCustomHeaders = new List<string>();

                                htc.HttpCustomHeaders.Add(parms[i]);
                                htc.HttpCustomHeaders.Add(parms[i+1]);

                                i += 2;
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
            htc.HttpTimeout = httpTimeout;
            htc.OutboundBody = body;
            htc.ResponseHeaders = headers;
            htc.proxyurl = m_proxyurl;
            htc.proxyexcepts = m_proxyexcepts;

            // Same number as default HttpWebRequest.MaximumAutomaticRedirections
            htc.MaxRedirects = 50;

            if (StartHttpRequest(htc))
            {
                status = HttpInitialRequestStatus.OK;
                return htc.ReqID;
            }
            else
            {
                status = HttpInitialRequestStatus.DISALLOWED_BY_FILTER;
                return UUID.Zero;
            }
        }

        /// <summary>
        /// Would a caller to this module be allowed to make a request to the given URL?
        /// </summary>
        /// <returns></returns>
        public bool CheckAllowed(Uri url)
        {
            return m_outboundUrlFilter.CheckAllowed(url);
        }

        public bool StartHttpRequest(HttpRequestClass req)
        {          
            if (!CheckAllowed(new Uri(req.Url)))
                return false;

            lock (HttpListLock)
            {
                m_pendingRequests.Add(req.ReqID, req);
            }

            req.Process();

            return true;
        }

        public void StopHttpRequestsForScript(UUID id)
        {
            if (m_pendingRequests != null)
            {
                List<UUID> keysToRemove = null;

                lock (HttpListLock)
                {
                    foreach (HttpRequestClass req in m_pendingRequests.Values)
                    {
                        if (req.ItemID == id)
                        {
                            req.Stop();

                            if (keysToRemove == null)
                                keysToRemove = new List<UUID>();

                            keysToRemove.Add(req.ReqID);
                        }
                    }

                    if (keysToRemove != null)
                        keysToRemove.ForEach(keyToRemove => m_pendingRequests.Remove(keyToRemove));
                }
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

        public IServiceRequest GetNextCompletedRequest()
        {
            lock (HttpListLock)
            {
                foreach (HttpRequestClass req in m_pendingRequests.Values)
                {
                    if (req.Finished)
                        return req;
                }
            }

            return null;
        }

        public void RemoveCompletedRequest(UUID id)
        {
            lock (HttpListLock)
            {
                HttpRequestClass tmpReq;
                if (m_pendingRequests.TryGetValue(id, out tmpReq))
                {
                    tmpReq.Stop();
                    tmpReq = null;
                    m_pendingRequests.Remove(id);
                }
            }
        }

        #endregion

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            m_proxyurl = config.Configs["Startup"].GetString("HttpProxy");
            m_proxyexcepts = config.Configs["Startup"].GetString("HttpProxyExceptions");

            m_outboundUrlFilter = new OutboundUrlFilter("Script HTTP request module", config);

            m_pendingRequests = new Dictionary<UUID, HttpRequestClass>();
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;

            m_scene.RegisterModuleInterface<IHttpRequestModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            scene.UnregisterModuleInterface<IHttpRequestModule>(this);
            if (scene == m_scene)
                m_scene = null;
        }

        public void PostInitialise()
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
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
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        private bool _finished;
        public bool Finished
        {
            get { return _finished; }
        }
        // public int HttpBodyMaxLen = 2048; // not implemented

        // Parameter members and default values
        public string HttpMethod  = "GET";
        public string HttpMIMEType = "text/plain;charset=utf-8";
        public int HttpTimeout;
        public bool HttpVerifyCert = true;
        //public bool HttpVerboseThrottle = true; // not implemented
        public List<string> HttpCustomHeaders = null;
        public bool HttpPragmaNoCache = true;

        // Request info
        private UUID _itemID;
        public UUID ItemID
        {
            get { return _itemID; }
            set { _itemID = value; }
        }
        private uint _localID;
        public uint LocalID
        {
            get { return _localID; }
            set { _localID = value; }
        }
        public DateTime Next;
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
        private UUID _reqID;
        public UUID ReqID
        {
            get { return _reqID; }
            set { _reqID = value; }
        }
        public HttpWebRequest Request;
        public string ResponseBody;
        public List<string> ResponseMetadata;
        public Dictionary<string, string> ResponseHeaders;
        public int Status;
        public string Url;

        public void Process()
        {
            SendRequest();
        }

        public void SendRequest()
        {
            try
            {
                Request = (HttpWebRequest)WebRequest.Create(Url);
                Request.AllowAutoRedirect = false;               
                Request.Method = HttpMethod;
                Request.ContentType = HttpMIMEType;

                if (!HttpVerifyCert)
                {
                    // We could hijack Connection Group Name to identify
                    // a desired security exception.  But at the moment we'll use a dummy header instead.
//                    Request.ConnectionGroupName = "NoVerify";
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

                if (ResponseHeaders != null)
                {
                    foreach (KeyValuePair<string, string> entry in ResponseHeaders)
                        if (entry.Key.ToLower().Equals("user-agent") && Request is HttpWebRequest)
                            ((HttpWebRequest)Request).UserAgent = entry.Value;
                        else
                            Request.Headers[entry.Key] = entry.Value;
                }

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
                    IAsyncResult result = (IAsyncResult)Request.BeginGetResponse(ResponseCallback, null);

                    ThreadPool.RegisterWaitForSingleObject(
                        result.AsyncWaitHandle, new WaitOrTimerCallback(TimeoutCallback), null, HttpTimeout, true);
                }
                catch (WebException e)
                {
                    if (e.Status != WebExceptionStatus.ProtocolError)
                    {
                        throw;
                    }

                    HttpWebResponse response = (HttpWebResponse)e.Response;

                    Status = (int)response.StatusCode;
                    ResponseBody = response.StatusDescription;
                    _finished = true;
                }
            }
            catch (Exception e)
            {
//                m_log.Debug(
//                    string.Format("[SCRIPTS HTTP REQUESTS]: Exception on request to {0} for {1}  ", Url, ItemID), e);

                Status = (int)OSHttpStatusCode.ClientErrorJoker;
                ResponseBody = e.Message;
                _finished = true;
            }
        }

        private void ResponseCallback(IAsyncResult ar)
        {
            HttpWebResponse response = null;

            try
            {
                try
                {
                    response = (HttpWebResponse)Request.EndGetResponse(ar);
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

                using (Stream stream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    ResponseBody = reader.ReadToEnd();
                } 
            }
            catch (Exception e)
            {
                Status = (int)OSHttpStatusCode.ClientErrorJoker;
                ResponseBody = e.Message;

//                m_log.Debug(
//                    string.Format("[SCRIPTS HTTP REQUESTS]: Exception on response to {0} for {1}  ", Url, ItemID), e);
            }
            finally
            {
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
                        _finished = true;
                    }
                    else
                    {
                        string location = response.Headers["Location"];

                        if (location == null)
                        {
                            Status = (int)OSHttpStatusCode.ClientErrorJoker;
                            ResponseBody = "HTTP redirect code but no location header";
                            _finished = true;
                        }
                        else if (!RequestModule.CheckAllowed(new Uri(location)))
                        {
                            Status = (int)OSHttpStatusCode.ClientErrorJoker;
                            ResponseBody = "URL from HTTP redirect blocked: " + location;
                            _finished = true;
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
                    _finished = true;
                }
            }
        }

        private void TimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
                Request.Abort();
        }

        public void Stop()
        {
//            m_log.DebugFormat("[SCRIPTS HTTP REQUESTS]: Stopping request to {0} for {1}  ", Url, ItemID);

            if (Request != null)
                Request.Abort();
        }
    }
}