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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Xml;
using HttpServer;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse.StructuredData;
using CoolHTTPListener = HttpServer.HttpListener;
using LogPrio = HttpServer.LogPrio;
using OpenSim.Framework.Monitoring;
using System.IO.Compression;
using System.Security.Cryptography;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class BaseHttpServer : IHttpServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private HttpServerLogWriter httpserverlog = new HttpServerLogWriter();
        private static Encoding UTF8NoBOM = new System.Text.UTF8Encoding(false);
        public static PollServiceRequestManager m_pollServiceManager;
        private static object m_generalLock = new object();

        /// <summary>
        /// This is a pending websocket request before it got an sucessful upgrade response.
        /// The consumer must call handler.HandshakeAndUpgrade() to signal to the handler to
        /// start the connection and optionally provide an origin authentication method.
        /// </summary>
        /// <param name="servicepath"></param>
        /// <param name="handler"></param>
        public delegate void WebSocketRequestDelegate(string servicepath, WebSocketHttpServerHandler handler);

        /// <summary>
        /// Gets or sets the debug level.
        /// </summary>
        /// <value>
        /// See MainServer.DebugLevel.
        /// </value>
        public int DebugLevel { get; set; }

        /// <summary>
        /// Request number for diagnostic purposes.
        /// </summary>
        /// <remarks>
        /// This is an internal number.  In some debug situations an external number may also be supplied in the
        /// opensim-request-id header but we are not currently logging this.
        /// </remarks>
        public int RequestNumber { get; private set; }

        /// <summary>
        /// Statistic for holding number of requests processed.
        /// </summary>
        private Stat m_requestsProcessedStat;

        private volatile int NotSocketErrors = 0;
        public volatile bool HTTPDRunning = false;

        // protected HttpListener m_httpListener;
        protected CoolHTTPListener m_httpListener2;
        protected Dictionary<string, XmlRpcMethod> m_rpcHandlers        = new Dictionary<string, XmlRpcMethod>();
        protected Dictionary<string, JsonRPCMethod> jsonRpcHandlers     = new Dictionary<string, JsonRPCMethod>();
        protected Dictionary<string, bool> m_rpcHandlersKeepAlive       = new Dictionary<string, bool>();
        protected DefaultLLSDMethod m_defaultLlsdHandler = null; // <--   Moving away from the monolithic..  and going to /registered/
        protected Dictionary<string, LLSDMethod> m_llsdHandlers         = new Dictionary<string, LLSDMethod>();
        protected Dictionary<string, IRequestHandler> m_streamHandlers  = new Dictionary<string, IRequestHandler>();
        protected Dictionary<string, GenericHTTPMethod> m_HTTPHandlers  = new Dictionary<string, GenericHTTPMethod>();
//        protected Dictionary<string, IHttpAgentHandler> m_agentHandlers = new Dictionary<string, IHttpAgentHandler>();
        protected Dictionary<string, PollServiceEventArgs> m_pollHandlers =
            new Dictionary<string, PollServiceEventArgs>();

        protected Dictionary<string, WebSocketRequestDelegate> m_WebSocketHandlers =
            new Dictionary<string, WebSocketRequestDelegate>();

        protected uint m_port;
        protected bool m_ssl;
        private X509Certificate2 m_cert;
        protected string m_SSLCommonName = "";
        protected List<string> m_certNames = new List<string>();
        protected List<string> m_certIPs = new List<string>();
        protected string m_certCN= "";
        protected RemoteCertificateValidationCallback m_certificateValidationCallback = null;

        protected IPAddress m_listenIPAddress = IPAddress.Any;


        public string Protocol
        {
            get { return m_ssl ? "https://" : "http://"; }
        }

        public uint SSLPort
        {
            get { return m_port; }
        }

        public string SSLCommonName
        {
            get { return m_SSLCommonName; }
        }

        public uint Port
        {
            get { return m_port; }
        }

        public bool UseSSL
        {
            get { return m_ssl; }
        }

        public IPAddress ListenIPAddress
        {
            get { return m_listenIPAddress; }
            set { m_listenIPAddress = value; }
        }

        public BaseHttpServer(uint port)
        {
            m_port = port;
        }

        private void load_cert(string CPath, string CPass)
        {
            try
            {
                 m_cert = new X509Certificate2(CPath, CPass);
                X509Extension ext = m_cert.Extensions["2.5.29.17"];
                if(ext != null)
                {
                    AsnEncodedData asndata = new AsnEncodedData(ext.Oid, ext.RawData);
                    string datastr = asndata.Format(true);
                    string[] lines = datastr.Split(new char[] {'\n','\r'});
                    foreach(string s in lines)
                    {
                        if(String.IsNullOrEmpty(s))
                            continue;
                        string[] parts = s.Split(new char[] {'='});
                        if(String.IsNullOrEmpty(parts[0]))
                            continue;
                        string entryName = parts[0].Replace(" ","");
                        if(entryName == "DNSName")
                            m_certNames.Add(parts[1]);
                        else if(entryName == "IPAddress")
                            m_certIPs.Add(parts[1]);
                        else if(entryName == "Unknown(135)") // stupid mono
                        {
                            try
                            {
                                if(parts[1].Length == 8)
                                {
                                    long tmp = long.Parse(parts[1], NumberStyles.AllowHexSpecifier);
                                    tmp = IPAddress.HostToNetworkOrder(tmp);
                                    tmp = (long)((ulong) tmp >> 32);
                                    IPAddress ia = new IPAddress(tmp);     
                                    m_certIPs.Add(ia.ToString());
                                }
                            }
                            catch {}
                        }
                    }
                }
                m_certCN = m_cert.GetNameInfo(X509NameType.SimpleName, false);
            }
            catch
            {
                throw new Exception("SSL cert load error");
            }
        }

        public BaseHttpServer(uint port, bool ssl, string CN, string CPath, string CPass)
        {
            m_port = port;
            if (ssl)
            {
                if(string.IsNullOrEmpty(CPath))
                    throw new Exception("invalid main http server cert path");

                if(Uri.CheckHostName(CN) == UriHostNameType.Unknown)
                    throw new Exception("invalid main http server CN (ExternalHostName)");

                m_certNames.Clear();
                m_certIPs.Clear();
                m_certCN= "";

                m_ssl = true;
                load_cert(CPath, CPass);
                
                if(!CheckSSLCertHost(CN))
                    throw new Exception("invalid main http server CN (ExternalHostName)");

                m_SSLCommonName = CN;

                if(m_cert.Issuer == m_cert.Subject )
                    m_log.Warn("Self signed certificate. Clients need to allow this (some viewers debug option NoVerifySSLcert must be set to true");
            }
            else
                m_ssl = false;
        }

        public BaseHttpServer(uint port, bool ssl, string CPath, string CPass)
        {
            m_port = port;
            if (ssl)
            {
                load_cert(CPath, CPass);
                if(m_cert.Issuer == m_cert.Subject )
                    m_log.Warn("Self signed certificate. Http clients need to allow this");
                m_ssl = true;
            }
            else
                m_ssl = false;
        }

        static bool MatchDNS(string hostname, string dns)
        {
            int indx = dns.IndexOf('*');
            if (indx == -1)
                return (String.Compare(hostname, dns, true, CultureInfo.InvariantCulture) == 0);

            int dnslen = dns.Length;
            dnslen--;
            if (indx == dnslen)
                return true; // just * ?

            if (indx > dnslen - 2)
                return false; // 2 short ?

            if (dns[indx + 1] != '.')
                return false;

            int indx2 = dns.IndexOf('*', indx + 1);
            if (indx2 != -1)
                return false; // there can only be one;

            string end = dns.Substring(indx + 1);
            int hostlen = hostname.Length;
            int endlen = end.Length;
            int length = hostlen - endlen;
            if (length <= 0)
                return false;

            if (String.Compare(hostname, length, end, 0, endlen, true, CultureInfo.InvariantCulture) != 0)
                return false;

            if (indx == 0)
            {
                indx2 = hostname.IndexOf('.');
                return ((indx2 == -1) || (indx2 >= length));
            }

            string start = dns.Substring(0, indx);
            return (String.Compare(hostname, 0, start, 0, start.Length, true, CultureInfo.InvariantCulture) == 0);
        }

        public bool CheckSSLCertHost(string hostname)
        {
            UriHostNameType htype = Uri.CheckHostName(hostname);

            if(htype == UriHostNameType.Unknown || htype == UriHostNameType.Basic)
                return false;
            if(htype == UriHostNameType.Dns)
            {
                foreach(string name in m_certNames)
                {
                    if(MatchDNS(hostname, name))
                        return true;
                }
                if(MatchDNS(hostname, m_certCN))
                    return true;
            }
            else
            {
                foreach(string ip in m_certIPs)
                {
                    if (String.Compare(hostname, ip, true, CultureInfo.InvariantCulture) == 0)
                        return true;
                }               
            }

            return false;
        }
        /// <summary>
        /// Add a stream handler to the http server.  If the handler already exists, then nothing happens.
        /// </summary>
        /// <param name="handler"></param>
        public void AddStreamHandler(IRequestHandler handler)
        {
            string httpMethod = handler.HttpMethod;
            string path = handler.Path;
            string handlerKey = GetHandlerKey(httpMethod, path);

            lock (m_streamHandlers)
            {
                if (!m_streamHandlers.ContainsKey(handlerKey))
                {
                    // m_log.DebugFormat("[BASE HTTP SERVER]: Adding handler key {0}", handlerKey);
                    m_streamHandlers.Add(handlerKey, handler);
                }
            }
        }

        public void AddWebSocketHandler(string servicepath, WebSocketRequestDelegate handler)
        {
            lock (m_WebSocketHandlers)
            {
                if (!m_WebSocketHandlers.ContainsKey(servicepath))
                    m_WebSocketHandlers.Add(servicepath, handler);
            }
        }

        public void RemoveWebSocketHandler(string servicepath)
        {
            lock (m_WebSocketHandlers)
                if (m_WebSocketHandlers.ContainsKey(servicepath))
                    m_WebSocketHandlers.Remove(servicepath);
        }

        public List<string> GetStreamHandlerKeys()
        {
            lock (m_streamHandlers)
                return new List<string>(m_streamHandlers.Keys);
        }

        private static string GetHandlerKey(string httpMethod, string path)
        {
            return httpMethod + ":" + path;
        }

        public bool AddXmlRPCHandler(string method, XmlRpcMethod handler)
        {
            return AddXmlRPCHandler(method, handler, true);
        }

        public bool AddXmlRPCHandler(string method, XmlRpcMethod handler, bool keepAlive)
        {
            lock (m_rpcHandlers)
            {
                m_rpcHandlers[method] = handler;
                m_rpcHandlersKeepAlive[method] = keepAlive; // default
            }

            return true;
        }

        public XmlRpcMethod GetXmlRPCHandler(string method)
        {
            lock (m_rpcHandlers)
            {
                if (m_rpcHandlers.ContainsKey(method))
                {
                    return m_rpcHandlers[method];
                }
                else
                {
                    return null;
                }
            }
        }

        public List<string> GetXmlRpcHandlerKeys()
        {
            lock (m_rpcHandlers)
                return new List<string>(m_rpcHandlers.Keys);
        }

        // JsonRPC
        public bool AddJsonRPCHandler(string method, JsonRPCMethod handler)
        {
            lock(jsonRpcHandlers)
            {
                jsonRpcHandlers.Add(method, handler);
            }
            return true;
        }

        public JsonRPCMethod GetJsonRPCHandler(string method)
        {
            lock (jsonRpcHandlers)
            {
                if (jsonRpcHandlers.ContainsKey(method))
                {
                    return jsonRpcHandlers[method];
                }
                else
                {
                    return null;
                }
            }
        }

        public List<string> GetJsonRpcHandlerKeys()
        {
            lock (jsonRpcHandlers)
                return new List<string>(jsonRpcHandlers.Keys);
        }

        public bool AddHTTPHandler(string methodName, GenericHTTPMethod handler)
        {
            //m_log.DebugFormat("[BASE HTTP SERVER]: Registering {0}", methodName);

            lock (m_HTTPHandlers)
            {
                if (!m_HTTPHandlers.ContainsKey(methodName))
                {
                    m_HTTPHandlers.Add(methodName, handler);
                    return true;
                }
            }

            //must already have a handler for that path so return false
            return false;
        }

        public List<string> GetHTTPHandlerKeys()
        {
            lock (m_HTTPHandlers)
                return new List<string>(m_HTTPHandlers.Keys);
        }

        public bool AddPollServiceHTTPHandler(string methodName, PollServiceEventArgs args)
        {
            lock (m_pollHandlers)
            {
                if (!m_pollHandlers.ContainsKey(methodName))
                {
                    m_pollHandlers.Add(methodName, args);
                    return true;
                }
            }

            return false;
        }

        public List<string> GetPollServiceHandlerKeys()
        {
            lock (m_pollHandlers)
                return new List<string>(m_pollHandlers.Keys);
        }

//        // Note that the agent string is provided simply to differentiate
//        // the handlers - it is NOT required to be an actual agent header
//        // value.
//        public bool AddAgentHandler(string agent, IHttpAgentHandler handler)
//        {
//            lock (m_agentHandlers)
//            {
//                if (!m_agentHandlers.ContainsKey(agent))
//                {
//                    m_agentHandlers.Add(agent, handler);
//                    return true;
//                }
//            }
//
//            //must already have a handler for that path so return false
//            return false;
//        }
//
//        public List<string> GetAgentHandlerKeys()
//        {
//            lock (m_agentHandlers)
//                return new List<string>(m_agentHandlers.Keys);
//        }

        public bool AddLLSDHandler(string path, LLSDMethod handler)
        {
            lock (m_llsdHandlers)
            {
                if (!m_llsdHandlers.ContainsKey(path))
                {
                    m_llsdHandlers.Add(path, handler);
                    return true;
                }
            }
            return false;
        }

        public List<string> GetLLSDHandlerKeys()
        {
            lock (m_llsdHandlers)
                return new List<string>(m_llsdHandlers.Keys);
        }

        public bool SetDefaultLLSDHandler(DefaultLLSDMethod handler)
        {
            m_defaultLlsdHandler = handler;
            return true;
        }

        public void OnRequest(object source, RequestEventArgs args)
        {
            RequestNumber++;
            try
            {
                IHttpClientContext context = (IHttpClientContext)source;
                IHttpRequest request = args.Request;

                PollServiceEventArgs psEvArgs;

                if (TryGetPollServiceHTTPHandler(request.UriPath.ToString(), out psEvArgs))
                {
                    psEvArgs.RequestsReceived++;

                    PollServiceHttpRequest psreq = new PollServiceHttpRequest(psEvArgs, context, request);

                    if (psEvArgs.Request != null)
                    {
                        OSHttpRequest req = new OSHttpRequest(context, request);
                        string requestBody;
                        Encoding encoding = Encoding.UTF8;
                        using(StreamReader reader = new StreamReader(req.InputStream, encoding))
                            requestBody = reader.ReadToEnd();

                        Hashtable keysvals = new Hashtable();
                        Hashtable headervals = new Hashtable();

                        string[] querystringkeys = req.QueryString.AllKeys;
                        string[] rHeaders = req.Headers.AllKeys;

                        keysvals.Add("body", requestBody);
                        keysvals.Add("uri", req.RawUrl);
                        keysvals.Add("content-type", req.ContentType);
                        keysvals.Add("http-method", req.HttpMethod);

                        foreach (string queryname in querystringkeys)
                        {
                            keysvals.Add(queryname, req.QueryString[queryname]);
                        }

                        foreach (string headername in rHeaders)
                        {
                            headervals[headername] = req.Headers[headername];
                        }

                        keysvals.Add("headers", headervals);
                        keysvals.Add("querystringkeys", querystringkeys);

                        psEvArgs.Request(psreq.RequestID, keysvals);
                    }

                    m_pollServiceManager.Enqueue(psreq);
                }
                else
                {
                    OnHandleRequestIOThread(context, request);
                }
            }
            catch (Exception e)
            {
                m_log.Error(String.Format("[BASE HTTP SERVER]: OnRequest() failed: {0} ", e.Message), e);
            }
        }

        private void OnHandleRequestIOThread(IHttpClientContext context, IHttpRequest request)
        {
            OSHttpRequest req = new OSHttpRequest(context, request);
            WebSocketRequestDelegate dWebSocketRequestDelegate = null;
            lock (m_WebSocketHandlers)
            {
                if (m_WebSocketHandlers.ContainsKey(req.RawUrl))
                    dWebSocketRequestDelegate = m_WebSocketHandlers[req.RawUrl];
            }
            if (dWebSocketRequestDelegate != null)
            {
                dWebSocketRequestDelegate(req.Url.AbsolutePath, new WebSocketHttpServerHandler(req, context, 8192));
                return;
            }

            OSHttpResponse resp = new OSHttpResponse(new HttpResponse(context, request));

            HandleRequest(req, resp);

            // !!!HACK ALERT!!!
            // There seems to be a bug in the underlying http code that makes subsequent requests
            // come up with trash in Accept headers. Until that gets fixed, we're cleaning them up here.
            if (request.AcceptTypes != null)
                for (int i = 0; i < request.AcceptTypes.Length; i++)
                    request.AcceptTypes[i] = string.Empty;
        }

        /// <summary>
        /// This methods is the start of incoming HTTP request handling.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        public virtual void HandleRequest(OSHttpRequest request, OSHttpResponse response)
        {
            if (request.HttpMethod == String.Empty) // Can't handle empty requests, not wasting a thread
            {
                try
                {
                    if(request.InputStream != null && request.InputStream.CanRead) 
                        request.InputStream.Close();
                    byte[] buffer500 = SendHTML500(response);
                    response.OutputStream.Write(buffer500, 0, buffer500.Length);
                    response.Send();
                }
                catch
                {
                }

                return;
            }

            string requestMethod = request.HttpMethod;
            string uriString = request.RawUrl;

            int requestStartTick = Environment.TickCount;

            // Will be adjusted later on.
            int requestEndTick = requestStartTick;

            IRequestHandler requestHandler = null;

            try
            {
                // OpenSim.Framework.WebUtil.OSHeaderRequestID
//                if (request.Headers["opensim-request-id"] != null)
//                    reqnum = String.Format("{0}:{1}",request.RemoteIPEndPoint,request.Headers["opensim-request-id"]);
                 //m_log.DebugFormat("[BASE HTTP SERVER]: <{0}> handle request for {1}",reqnum,request.RawUrl);

                Culture.SetCurrentCulture();

//                //  This is the REST agent interface. We require an agent to properly identify
//                //  itself. If the REST handler recognizes the prefix it will attempt to
//                //  satisfy the request. If it is not recognizable, and no damage has occurred
//                //  the request can be passed through to the other handlers. This is a low
//                //  probability event; if a request is matched it is normally expected to be
//                //  handled
//                IHttpAgentHandler agentHandler;
//
//                if (TryGetAgentHandler(request, response, out agentHandler))
//                {
//                    if (HandleAgentRequest(agentHandler, request, response))
//                    {
//                        requestEndTick = Environment.TickCount;
//                        return;
//                    }
//                }

                string path = request.RawUrl;
                string handlerKey = GetHandlerKey(request.HttpMethod, path);
                byte[] buffer = null;

                if (TryGetStreamHandler(handlerKey, out requestHandler))
                {
                    if (DebugLevel >= 3)
                        LogIncomingToStreamHandler(request, requestHandler);

                    response.ContentType = requestHandler.ContentType; // Lets do this defaulting before in case handler has varying content type.

                    if (requestHandler is IStreamedRequestHandler)
                    {
                        IStreamedRequestHandler streamedRequestHandler = requestHandler as IStreamedRequestHandler;

                        buffer = streamedRequestHandler.Handle(path, request.InputStream, request, response);
                    }
                    else if (requestHandler is IGenericHTTPHandler)
                    {
                        //m_log.Debug("[BASE HTTP SERVER]: Found Caps based HTTP Handler");
                        IGenericHTTPHandler HTTPRequestHandler = requestHandler as IGenericHTTPHandler;

                        string requestBody;
                        Encoding encoding = Encoding.UTF8;
                        using(StreamReader reader = new StreamReader(request.InputStream, encoding))
                            requestBody = reader.ReadToEnd();

                        Hashtable keysvals = new Hashtable();
                        Hashtable headervals = new Hashtable();
                        //string host = String.Empty;

                        string[] querystringkeys = request.QueryString.AllKeys;
                        string[] rHeaders = request.Headers.AllKeys;

                        foreach (string queryname in querystringkeys)
                        {
                            keysvals.Add(queryname, request.QueryString[queryname]);
                        }

                        foreach (string headername in rHeaders)
                        {
                            //m_log.Warn("[HEADER]: " + headername + "=" + request.Headers[headername]);
                            headervals[headername] = request.Headers[headername];
                        }

                        //                        if (headervals.Contains("Host"))
                        //                        {
                        //                            host = (string)headervals["Host"];
                        //                        }

                        keysvals.Add("requestbody", requestBody);
                        keysvals.Add("headers",headervals);
                        if (keysvals.Contains("method"))
                        {
                            //m_log.Warn("[HTTP]: Contains Method");
                            //string method = (string)keysvals["method"];
                            //m_log.Warn("[HTTP]: " + requestBody);

                        }

                        buffer = DoHTTPGruntWork(HTTPRequestHandler.Handle(path, keysvals), response);
                    }
                    else
                    {
                        IStreamHandler streamHandler = (IStreamHandler)requestHandler;
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            streamHandler.Handle(path, request.InputStream, memoryStream, request, response);
                            buffer = memoryStream.ToArray();
                        }
                    }
                }
                else
                {
                    switch (request.ContentType)
                    {
                        case null:
                        case "text/html":
                            if (DebugLevel >= 3)
                                LogIncomingToContentTypeHandler(request);

                            buffer = HandleHTTPRequest(request, response);
                            break;

                        case "application/llsd+xml":
                        case "application/xml+llsd":
                        case "application/llsd+json":
                            if (DebugLevel >= 3)
                                LogIncomingToContentTypeHandler(request);

                            buffer = HandleLLSDRequests(request, response);
                            break;

                        case "application/json-rpc":
                            if (DebugLevel >= 3)
                                LogIncomingToContentTypeHandler(request);

                            buffer = HandleJsonRpcRequests(request, response);
                            break;

                        case "text/xml":
                        case "application/xml":
                        case "application/json":

                        default:
                            //m_log.Info("[Debug BASE HTTP SERVER]: in default handler");
                            // Point of note..  the DoWeHaveA methods check for an EXACT path
                            //                        if (request.RawUrl.Contains("/CAPS/EQG"))
                            //                        {
                            //                            int i = 1;
                            //                        }
                            //m_log.Info("[Debug BASE HTTP SERVER]: Checking for LLSD Handler");
                            if (DoWeHaveALLSDHandler(request.RawUrl))
                            {
                                if (DebugLevel >= 3)
                                    LogIncomingToContentTypeHandler(request);

                                buffer = HandleLLSDRequests(request, response);
                            }
    //                        m_log.DebugFormat("[BASE HTTP SERVER]: Checking for HTTP Handler for request {0}", request.RawUrl);
                            else if (DoWeHaveAHTTPHandler(request.RawUrl))
                            {
                                if (DebugLevel >= 3)
                                    LogIncomingToContentTypeHandler(request);

                                buffer = HandleHTTPRequest(request, response);
                            }
                            else
                            {
                                if (DebugLevel >= 3)
                                    LogIncomingToXmlRpcHandler(request);

                                // generic login request.
                                buffer = HandleXmlRpcRequests(request, response);
                            }

                            break;
                    }
                }

                if(request.InputStream != null && request.InputStream.CanRead)
                    request.InputStream.Dispose();

                if (buffer != null)
                {
                    if (WebUtil.DebugLevel >= 5)
                    {
                        string output = System.Text.Encoding.UTF8.GetString(buffer);

                        if (WebUtil.DebugLevel >= 6)
                        {
                            // Always truncate binary blobs. We don't have a ContentType, so detect them using the request name.
                            if ((requestHandler != null && requestHandler.Name == "GetMesh"))
                            {
                                if (output.Length > WebUtil.MaxRequestDiagLength)
                                    output = output.Substring(0, WebUtil.MaxRequestDiagLength) + "...";
                            }
                        }

                        WebUtil.LogResponseDetail(RequestNumber, output);
                    }

                    if (!response.SendChunked && response.ContentLength64 <= 0)
                        response.ContentLength64 = buffer.LongLength;

                    //response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.RawBufferStart = 0;
                    response.RawBufferLen = buffer.Length;
                    response.RawBuffer = buffer;
                }

                // Do not include the time taken to actually send the response to the caller in the measurement
                // time.  This is to avoid logging when it's the client that is slow to process rather than the
                // server
                requestEndTick = Environment.TickCount;

                buffer = null;
                response.Send();
                response.RawBuffer = null;
            }
            catch (SocketException e)
            {
                // At least on linux, it appears that if the client makes a request without requiring the response,
                // an unconnected socket exception is thrown when we close the response output stream.  There's no
                // obvious way to tell if the client didn't require the response, so instead we'll catch and ignore
                // the exception instead.
                //
                // An alternative may be to turn off all response write exceptions on the HttpListener, but let's go
                // with the minimum first
                m_log.Warn(String.Format("[BASE HTTP SERVER]: HandleRequest threw {0}.\nNOTE: this may be spurious on Linux ", e.Message), e);
            }
            catch (IOException e)
            {
                m_log.Error("[BASE HTTP SERVER]: HandleRequest() threw exception ", e);
            }
            catch (Exception e)
            {
                m_log.Error("[BASE HTTP SERVER]: HandleRequest() threw exception ", e);
                try
                {
                    byte[] buffer500 = SendHTML500(response);
                    response.OutputStream.Write(buffer500, 0, buffer500.Length);
                    response.Send();
                }
                catch
                {
                }
            }
            finally
            {
                if(request.InputStream != null && request.InputStream.CanRead)
                    request.InputStream.Close();

                // Every month or so this will wrap and give bad numbers, not really a problem
                // since its just for reporting
                int tickdiff = requestEndTick - requestStartTick;
                if (tickdiff > 3000 && (requestHandler == null || requestHandler.Name == null || requestHandler.Name != "GetTexture"))
                {
                    m_log.InfoFormat(
                        "[LOGHTTP] Slow handling of {0} {1} {2} {3} {4} from {5} took {6}ms",
                        RequestNumber,
                        requestMethod,
                        uriString,
                        requestHandler != null ? requestHandler.Name : "",
                        requestHandler != null ? requestHandler.Description : "",
                        request.RemoteIPEndPoint,
                        tickdiff);
                }
                else if (DebugLevel >= 4)
                {
                    m_log.DebugFormat(
                        "[LOGHTTP] HTTP IN {0} :{1} took {2}ms",
                        RequestNumber,
                        Port,
                        tickdiff);
                }
            }
        }

        private void LogIncomingToStreamHandler(OSHttpRequest request, IRequestHandler requestHandler)
        {
            m_log.DebugFormat(
                "[LOGHTTP] HTTP IN {0} :{1} stream handler {2} {3} {4} {5} from {6}",
                RequestNumber,
                Port,
                request.HttpMethod,
                request.Url.PathAndQuery,
                requestHandler.Name,
                requestHandler.Description,
                request.RemoteIPEndPoint);

            if (DebugLevel >= 5)
                LogIncomingInDetail(request);
        }

        private void LogIncomingToContentTypeHandler(OSHttpRequest request)
        {
            m_log.DebugFormat(
                "[LOGHTTP] HTTP IN {0} :{1} {2} content type handler {3} {4} from {5}",
                RequestNumber,
                Port,
                string.IsNullOrEmpty(request.ContentType) ? "not set" : request.ContentType,
                request.HttpMethod,
                request.Url.PathAndQuery,
                request.RemoteIPEndPoint);

            if (DebugLevel >= 5)
                LogIncomingInDetail(request);
        }

        private void LogIncomingToXmlRpcHandler(OSHttpRequest request)
        {
            m_log.DebugFormat(
                "[LOGHTTP] HTTP IN {0} :{1} assumed generic XMLRPC request {2} {3} from {4}",
                RequestNumber,
                Port,
                request.HttpMethod,
                request.Url.PathAndQuery,
                request.RemoteIPEndPoint);

            if (DebugLevel >= 5)
                LogIncomingInDetail(request);
        }

        private void LogIncomingInDetail(OSHttpRequest request)
        {
            if (request.ContentType == "application/octet-stream")
                return; // never log these; they're just binary data

            Stream inputStream = Util.Copy(request.InputStream);
            Stream innerStream = null;
            try
            {
                if ((request.Headers["Content-Encoding"] == "gzip") || (request.Headers["X-Content-Encoding"] == "gzip"))
                {
                    innerStream = inputStream;
                    inputStream = new GZipStream(innerStream, System.IO.Compression.CompressionMode.Decompress);
                }

                using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
                {
                    string output;

                    if (DebugLevel == 5)
                    {
                        char[] chars = new char[WebUtil.MaxRequestDiagLength + 1];  // +1 so we know to add "..." only if needed
                        int len = reader.Read(chars, 0, WebUtil.MaxRequestDiagLength + 1);
                        output = new string(chars, 0, Math.Min(len, WebUtil.MaxRequestDiagLength));
                        if (len > WebUtil.MaxRequestDiagLength)
                            output += "...";
                    }
                    else
                    {
                        output = reader.ReadToEnd();
                    }

                    m_log.DebugFormat("[LOGHTTP] {0}", Util.BinaryToASCII(output));
                }
            }
            finally
            {
                if (innerStream != null)
                    innerStream.Dispose();
                inputStream.Dispose();
            }
        }

        private readonly string HANDLER_SEPARATORS = "/?&#-";

        private bool TryGetStreamHandler(string handlerKey, out IRequestHandler streamHandler)
        {
            string bestMatch = null;

            lock (m_streamHandlers)
            {
                foreach (string pattern in m_streamHandlers.Keys)
                {
                    if ((handlerKey == pattern)
                        || (handlerKey.StartsWith(pattern) && (HANDLER_SEPARATORS.IndexOf(handlerKey[pattern.Length]) >= 0)))
                    {
                        if (String.IsNullOrEmpty(bestMatch) || pattern.Length > bestMatch.Length)
                        {
                            bestMatch = pattern;
                        }
                    }
                }

                if (String.IsNullOrEmpty(bestMatch))
                {
                    streamHandler = null;
                    return false;
                }
                else
                {
                    streamHandler = m_streamHandlers[bestMatch];
                    return true;
                }
            }
        }

        private bool TryGetPollServiceHTTPHandler(string handlerKey, out PollServiceEventArgs oServiceEventArgs)
        {
            string bestMatch = null;

            lock (m_pollHandlers)
            {
                foreach (string pattern in m_pollHandlers.Keys)
                {
                    if ((handlerKey == pattern)
                        || (handlerKey.StartsWith(pattern) && (HANDLER_SEPARATORS.IndexOf(handlerKey[pattern.Length]) >= 0)))
                    {
                        if (String.IsNullOrEmpty(bestMatch) || pattern.Length > bestMatch.Length)
                        {
                            bestMatch = pattern;
                        }
                    }
                }

                if (String.IsNullOrEmpty(bestMatch))
                {
                    oServiceEventArgs = null;
                    return false;
                }
                else
                {
                    oServiceEventArgs = m_pollHandlers[bestMatch];
                    return true;
                }
            }
        }

        private bool TryGetHTTPHandler(string handlerKey, out GenericHTTPMethod HTTPHandler)
        {
//            m_log.DebugFormat("[BASE HTTP HANDLER]: Looking for HTTP handler for {0}", handlerKey);

            string bestMatch = null;

            lock (m_HTTPHandlers)
            {
                foreach (string pattern in m_HTTPHandlers.Keys)
                {
                    if ((handlerKey == pattern)
                        || (handlerKey.StartsWith(pattern) && (HANDLER_SEPARATORS.IndexOf(handlerKey[pattern.Length]) >= 0)))
                    {
                        if (String.IsNullOrEmpty(bestMatch) || pattern.Length > bestMatch.Length)
                        {
                            bestMatch = pattern;
                        }
                    }
                }

                if (String.IsNullOrEmpty(bestMatch))
                {
                    HTTPHandler = null;
                    return false;
                }
                else
                {
                    HTTPHandler = m_HTTPHandlers[bestMatch];
                    return true;
                }
            }
        }

//        private bool TryGetAgentHandler(OSHttpRequest request, OSHttpResponse response, out IHttpAgentHandler agentHandler)
//        {
//            agentHandler = null;
//
//            lock (m_agentHandlers)
//            {
//                foreach (IHttpAgentHandler handler in m_agentHandlers.Values)
//                {
//                    if (handler.Match(request, response))
//                    {
//                        agentHandler = handler;
//                        return true;
//                    }
//                }
//            }
//
//            return false;
//        }

        /// <summary>
        /// Try all the registered xmlrpc handlers when an xmlrpc request is received.
        /// Sends back an XMLRPC unknown request response if no handler is registered for the requested method.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private byte[] HandleXmlRpcRequests(OSHttpRequest request, OSHttpResponse response)
        {
            String requestBody;

            Stream requestStream = Util.Copy(request.InputStream);
            Stream innerStream = null;
            try
            {
                if ((request.Headers["Content-Encoding"] == "gzip") || (request.Headers["X-Content-Encoding"] == "gzip"))
                {
                    innerStream = requestStream;
                    requestStream = new GZipStream(innerStream, System.IO.Compression.CompressionMode.Decompress);
                }

                using (StreamReader reader = new StreamReader(requestStream, Encoding.UTF8))
                    requestBody = reader.ReadToEnd();

                }
            finally
            {
                if (innerStream != null && innerStream.CanRead)
                    innerStream.Dispose();
                if (requestStream.CanRead)
                    requestStream.Dispose();
            }

            //m_log.Debug(requestBody);
            requestBody = requestBody.Replace("<base64></base64>", "");

            string responseString = String.Empty;
            XmlRpcRequest xmlRprcRequest = null;

            bool gridproxy = false;
            if (requestBody.Contains("encoding=\"utf-8"))
            {
                int channelindx = -1;
                int optionsindx = requestBody.IndexOf(">options<");
                if(optionsindx >0)
                {
                    channelindx = requestBody.IndexOf(">channel<");
                    if (optionsindx < channelindx)
                        gridproxy = true;
                }
            }

            try
            {
                xmlRprcRequest = (XmlRpcRequest) (new XmlRpcRequestDeserializer()).Deserialize(requestBody);
            }
            catch (XmlException e)
            {
                if (DebugLevel >= 1)
                {
                    if (DebugLevel >= 2)
                        m_log.Warn(
                            string.Format(
                                "[BASE HTTP SERVER]: Got XMLRPC request with invalid XML from {0}.  XML was '{1}'.  Sending blank response.  Exception ",
                                request.RemoteIPEndPoint, requestBody),
                            e);
                    else
                    {
                        m_log.WarnFormat(
                            "[BASE HTTP SERVER]: Got XMLRPC request with invalid XML from {0}, length {1}.  Sending blank response.",
                            request.RemoteIPEndPoint, requestBody.Length);
                    }
                }
            }

            if (xmlRprcRequest != null)
            {
                string methodName = xmlRprcRequest.MethodName;
                if (methodName != null)
                {
                    xmlRprcRequest.Params.Add(request.RemoteIPEndPoint); // Param[1]
                    XmlRpcResponse xmlRpcResponse;

                    XmlRpcMethod method;
                    bool methodWasFound;
                    bool keepAlive = false;
                    lock (m_rpcHandlers)
                    {
                        methodWasFound = m_rpcHandlers.TryGetValue(methodName, out method);
                        if (methodWasFound)
                            keepAlive = m_rpcHandlersKeepAlive[methodName];
                    }

                    if (methodWasFound)
                    {
                        xmlRprcRequest.Params.Add(request.Url); // Param[2]

                        string xff = "X-Forwarded-For";
                        string xfflower = xff.ToLower();
                        foreach (string s in request.Headers.AllKeys)
                        {
                            if (s != null && s.Equals(xfflower))
                            {
                                xff = xfflower;
                                break;
                            }
                        }
                        xmlRprcRequest.Params.Add(request.Headers.Get(xff)); // Param[3]

                        if (gridproxy)
                            xmlRprcRequest.Params.Add("gridproxy");  // Param[4]

                        // reserve this for
                        // ... by Fumi.Iseki for DTLNSLMoneyServer
                        // BUT make its presence possible to detect/parse
                        string rcn = request.IHttpClientContext.SSLCommonName;
                        if(!string.IsNullOrWhiteSpace(rcn))
                        {
                            rcn = "SSLCN:" + rcn;
                            xmlRprcRequest.Params.Add(rcn); // Param[4] or Param[5]
                        }
                
                        try
                        {
                            xmlRpcResponse = method(xmlRprcRequest, request.RemoteIPEndPoint);
                        }
                        catch(Exception e)
                        {
                            string errorMessage
                                = String.Format(
                                    "Requested method [{0}] from {1} threw exception: {2} {3}",
                                    methodName, request.RemoteIPEndPoint.Address, e.Message, e.StackTrace);

                            m_log.ErrorFormat("[BASE HTTP SERVER]: {0}", errorMessage);

                            // if the registered XmlRpc method threw an exception, we pass a fault-code along
                            xmlRpcResponse = new XmlRpcResponse();

                            // Code probably set in accordance with http://xmlrpc-epi.sourceforge.net/specs/rfc.fault_codes.php
                            xmlRpcResponse.SetFault(-32603, errorMessage);
                        }

                        // if the method wasn't found, we can't determine KeepAlive state anyway, so lets do it only here
                        response.KeepAlive = keepAlive;
                        response.AddHeader("Access-Control-Allow-Origin", "*");
                    }
                    else
                    {
                        xmlRpcResponse = new XmlRpcResponse();

                        // Code set in accordance with http://xmlrpc-epi.sourceforge.net/specs/rfc.fault_codes.php
                        xmlRpcResponse.SetFault(
                            XmlRpcErrorCodes.SERVER_ERROR_METHOD,
                            String.Format("Requested method [{0}] not found", methodName));
                    }

                    response.ContentType = "text/xml";
                    using (MemoryStream outs = new MemoryStream())
                    using (XmlTextWriter writer = new XmlTextWriter(outs, UTF8NoBOM))
                    {
                        writer.Formatting = Formatting.None;
                        XmlRpcResponseSerializer.Singleton.Serialize(writer, xmlRpcResponse);
                        writer.Flush();
                        outs.Flush();
                        outs.Position = 0;
                        using (StreamReader sr = new StreamReader(outs))
                        {
                            responseString = sr.ReadToEnd();
                        }
                    }
                }
                else
                {
                    //HandleLLSDRequests(request, response);
                    response.ContentType = "text/plain";
                    response.StatusCode = 404;
                    response.StatusDescription = "Not Found";
                    response.ProtocolVersion = "HTTP/1.0";
                    responseString = "Not found";
                    response.KeepAlive = false;

                    m_log.ErrorFormat(
                        "[BASE HTTP SERVER]: Handler not found for http request {0} {1}",
                        request.HttpMethod, request.Url.PathAndQuery);
                }
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            return buffer;
        }

        // JsonRpc (v2.0 only)
        // Batch requests not yet supported
        private byte[] HandleJsonRpcRequests(OSHttpRequest request, OSHttpResponse response)
        {
            Stream requestStream = request.InputStream;
            JsonRpcResponse jsonRpcResponse = new JsonRpcResponse();
            OSDMap jsonRpcRequest = null;

            try
            {
                jsonRpcRequest = (OSDMap)OSDParser.DeserializeJson(requestStream);
            }
            catch (LitJson.JsonException e)
            {
                jsonRpcResponse.Error.Code = ErrorCode.InternalError;
                jsonRpcResponse.Error.Message = e.Message;
            }

            requestStream.Close();

            if (jsonRpcRequest != null)
            {
                if (jsonRpcRequest.ContainsKey("jsonrpc") || jsonRpcRequest["jsonrpc"].AsString() == "2.0")
                {
                    jsonRpcResponse.JsonRpc = "2.0";

                    // If we have no id, then it's a "notification"
                    if (jsonRpcRequest.ContainsKey("id"))
                    {
                        jsonRpcResponse.Id = jsonRpcRequest["id"].AsString();
                    }

                    string methodname = jsonRpcRequest["method"];
                    JsonRPCMethod method;

                    if (jsonRpcHandlers.ContainsKey(methodname))
                    {
                        lock(jsonRpcHandlers)
                        {
                            jsonRpcHandlers.TryGetValue(methodname, out method);
                        }
                        bool res = false;
                        try
                        {
                            res = method(jsonRpcRequest, ref jsonRpcResponse);
                            if(!res)
                            {
                                // The handler sent back an unspecified error
                                if(jsonRpcResponse.Error.Code == 0)
                                {
                                    jsonRpcResponse.Error.Code = ErrorCode.InternalError;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            string ErrorMessage = string.Format("[BASE HTTP SERVER]: Json-Rpc Handler Error method {0} - {1}", methodname, e.Message);
                            m_log.Error(ErrorMessage);
                            jsonRpcResponse.Error.Code = ErrorCode.InternalError;
                            jsonRpcResponse.Error.Message = ErrorMessage;
                        }
                    }
                    else // Error no hanlder defined for requested method
                    {
                        jsonRpcResponse.Error.Code = ErrorCode.InvalidRequest;
                        jsonRpcResponse.Error.Message = string.Format ("No handler defined for {0}", methodname);
                    }
                }
                else // not json-rpc 2.0 could be v1
                {
                    jsonRpcResponse.Error.Code = ErrorCode.InvalidRequest;
                    jsonRpcResponse.Error.Message = "Must be valid json-rpc 2.0 see: http://www.jsonrpc.org/specification";

                    if (jsonRpcRequest.ContainsKey("id"))
                        jsonRpcResponse.Id = jsonRpcRequest["id"].AsString();
                }
            }

            string responseData = string.Empty;

            responseData = jsonRpcResponse.Serialize();

            byte[] buffer = Encoding.UTF8.GetBytes(responseData);
            return buffer;
        }

        private byte[] HandleLLSDRequests(OSHttpRequest request, OSHttpResponse response)
        {
            //m_log.Warn("[BASE HTTP SERVER]: We've figured out it's a LLSD Request");
            bool notfound = false;

            Stream requestStream = request.InputStream;

            string requestBody;
            Encoding encoding = Encoding.UTF8;
            using(StreamReader reader = new StreamReader(requestStream, encoding))
                requestBody= reader.ReadToEnd();

            //m_log.DebugFormat("[OGP]: {0}:{1}", request.RawUrl, requestBody);

            OSD llsdRequest = null;
            OSD llsdResponse = null;

            bool LegacyLLSDLoginLibOMV = (requestBody.Contains("passwd") && requestBody.Contains("mac") && requestBody.Contains("viewer_digest"));

            if (requestBody.Length == 0)
            // Get Request
            {
                requestBody = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><llsd><map><key>request</key><string>get</string></map></llsd>";
            }
            try
            {
                llsdRequest = OSDParser.Deserialize(requestBody);
            }
            catch (Exception ex)
            {
                m_log.Warn("[BASE HTTP SERVER]: Error - " + ex.Message);
            }

            if (llsdRequest != null)// && m_defaultLlsdHandler != null)
            {
                LLSDMethod llsdhandler = null;

                if (TryGetLLSDHandler(request.RawUrl, out llsdhandler) && !LegacyLLSDLoginLibOMV)
                {
                    // we found a registered llsd handler to service this request
                    llsdResponse = llsdhandler(request.RawUrl, llsdRequest, request.RemoteIPEndPoint.ToString());
                }
                else
                {
                    // we didn't find a registered llsd handler to service this request
                    // check if we have a default llsd handler

                    if (m_defaultLlsdHandler != null)
                    {
                        // LibOMV path
                        llsdResponse = m_defaultLlsdHandler(llsdRequest, request.RemoteIPEndPoint);
                    }
                    else
                    {
                        // Oops, no handler for this..   give em the failed message
                        notfound = true;
                    }
                }
            }
            else
            {
                notfound = true;
            }

            if(notfound)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.StatusDescription = "Not found";
                return null;
            }

            byte[] buffer = new byte[0];

            if (llsdResponse.ToString() == "shutdown404!")
            {
                response.ContentType = "text/plain";
                response.StatusCode = 404;
                response.StatusDescription = "Not Found";
                response.ProtocolVersion = "HTTP/1.0";
                buffer = Encoding.UTF8.GetBytes("Not found");
            }
            else
            {
                // Select an appropriate response format
                buffer = BuildLLSDResponse(request, response, llsdResponse);
            }

            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            return buffer;
        }

        private byte[] BuildLLSDResponse(OSHttpRequest request, OSHttpResponse response, OSD llsdResponse)
        {
            if (request.AcceptTypes != null && request.AcceptTypes.Length > 0)
            {
                foreach (string strAccept in request.AcceptTypes)
                {
                    switch (strAccept)
                    {
                        case "application/llsd+xml":
                        case "application/xml":
                        case "text/xml":
                            response.ContentType = strAccept;
                            return OSDParser.SerializeLLSDXmlBytes(llsdResponse);
                        case "application/llsd+json":
                        case "application/json":
                            response.ContentType = strAccept;
                            return Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(llsdResponse));
                    }
                }
            }

            if (!String.IsNullOrEmpty(request.ContentType))
            {
                switch (request.ContentType)
                {
                    case "application/llsd+xml":
                    case "application/xml":
                    case "text/xml":
                        response.ContentType = request.ContentType;
                        return OSDParser.SerializeLLSDXmlBytes(llsdResponse);
                    case "application/llsd+json":
                    case "application/json":
                        response.ContentType = request.ContentType;
                        return Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(llsdResponse));
                }
            }

            // response.ContentType = "application/llsd+json";
            // return Util.UTF8.GetBytes(OSDParser.SerializeJsonString(llsdResponse));
            response.ContentType = "application/llsd+xml";
            return OSDParser.SerializeLLSDXmlBytes(llsdResponse);
        }

        /// <summary>
        /// Checks if we have an Exact path in the LLSD handlers for the path provided
        /// </summary>
        /// <param name="path">URI of the request</param>
        /// <returns>true if we have one, false if not</returns>
        private bool DoWeHaveALLSDHandler(string path)
        {
            string[] pathbase = path.Split('/');
            string searchquery = "/";

            if (pathbase.Length < 1)
                return false;

            for (int i = 1; i < pathbase.Length; i++)
            {
                searchquery += pathbase[i];
                if (pathbase.Length - 1 != i)
                    searchquery += "/";
            }

            string bestMatch = null;

            lock (m_llsdHandlers)
            {
                foreach (string pattern in m_llsdHandlers.Keys)
                {
                    if (searchquery.StartsWith(pattern) && searchquery.Length >= pattern.Length)
                        bestMatch = pattern;
                }
            }

            // extra kicker to remove the default XMLRPC login case..  just in case..
            if (path != "/" && bestMatch == "/" && searchquery != "/")
                return false;

            if (path == "/")
                return false;

            if (String.IsNullOrEmpty(bestMatch))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Checks if we have an Exact path in the HTTP handlers for the path provided
        /// </summary>
        /// <param name="path">URI of the request</param>
        /// <returns>true if we have one, false if not</returns>
        private bool DoWeHaveAHTTPHandler(string path)
        {
            string[] pathbase = path.Split('/');
            string searchquery = "/";

            if (pathbase.Length < 1)
                return false;

            for (int i = 1; i < pathbase.Length; i++)
            {
                searchquery += pathbase[i];
                if (pathbase.Length - 1 != i)
                    searchquery += "/";
            }

            string bestMatch = null;

            //m_log.DebugFormat("[BASE HTTP HANDLER]: Checking if we have an HTTP handler for {0}", searchquery);

            lock (m_HTTPHandlers)
            {
                foreach (string pattern in m_HTTPHandlers.Keys)
                {
                    if (searchquery.StartsWith(pattern) && searchquery.Length >= pattern.Length)
                    {
                        bestMatch = pattern;
                    }
                }

                // extra kicker to remove the default XMLRPC login case..  just in case..
                if (path == "/")
                    return false;

                if (String.IsNullOrEmpty(bestMatch))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        private bool TryGetLLSDHandler(string path, out LLSDMethod llsdHandler)
        {
            llsdHandler = null;
            // Pull out the first part of the path
            // splitting the path by '/' means we'll get the following return..
            // {0}/{1}/{2}
            // where {0} isn't something we really control 100%

            string[] pathbase = path.Split('/');
            string searchquery = "/";

            if (pathbase.Length < 1)
                return false;

            for (int i=1; i<pathbase.Length; i++)
            {
                searchquery += pathbase[i];
                if (pathbase.Length-1 != i)
                    searchquery += "/";
            }

            // while the matching algorithm below doesn't require it, we're expecting a query in the form
            //
            //   [] = optional
            //   /resource/UUID/action[/action]
            //
            // now try to get the closest match to the reigstered path
            // at least for OGP, registered path would probably only consist of the /resource/

            string bestMatch = null;

            lock (m_llsdHandlers)
            {
                foreach (string pattern in m_llsdHandlers.Keys)
                {
                    if (searchquery.ToLower().StartsWith(pattern.ToLower()))
                    {
                        if (String.IsNullOrEmpty(bestMatch) || searchquery.Length > bestMatch.Length)
                        {
                            // You have to specifically register for '/' and to get it, you must specificaly request it
                            //
                            if (pattern == "/" && searchquery == "/" || pattern != "/")
                                bestMatch = pattern;
                        }
                    }
                }

                if (String.IsNullOrEmpty(bestMatch))
                {
                    llsdHandler = null;
                    return false;
                }
                else
                {
                    llsdHandler = m_llsdHandlers[bestMatch];
                    return true;
                }
            }
        }

        public byte[] HandleHTTPRequest(OSHttpRequest request, OSHttpResponse response)
        {
//            m_log.DebugFormat(
//                "[BASE HTTP SERVER]: HandleHTTPRequest for request to {0}, method {1}",
//                request.RawUrl, request.HttpMethod);

            switch (request.HttpMethod)
            {
                case "OPTIONS":
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.AddHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, PUT, OPTIONS");
                    response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                    response.StatusCode = (int)OSHttpStatusCode.SuccessOk;
                    return null;

                default:
                    return HandleContentVerbs(request, response);
            }
        }

        private byte[] HandleContentVerbs(OSHttpRequest request, OSHttpResponse response)
        {
//            m_log.DebugFormat("[BASE HTTP SERVER]: HandleContentVerbs for request to {0}", request.RawUrl);

            // This is a test.  There's a workable alternative..  as this way sucks.
            // We'd like to put this into a text file parhaps that's easily editable.
            //
            // For this test to work, I used the following secondlife.exe parameters
            // "C:\Program Files\SecondLifeWindLight\SecondLifeWindLight.exe" -settings settings_windlight.xml -channel "Second Life WindLight"  -set SystemLanguage en-us -loginpage http://10.1.1.2:8002/?show_login_form=TRUE -loginuri http://10.1.1.2:8002 -user 10.1.1.2
            //
            // Even after all that, there's still an error, but it's a start.
            //
            // I depend on show_login_form being in the secondlife.exe parameters to figure out
            // to display the form, or process it.
            // a better way would be nifty.

            byte[] buffer;

            Stream requestStream = request.InputStream;
            string requestBody;
            Encoding encoding = Encoding.UTF8;
            using(StreamReader reader = new StreamReader(requestStream, encoding))
                requestBody = reader.ReadToEnd();

            Hashtable keysvals = new Hashtable();
            Hashtable headervals = new Hashtable();

            Hashtable requestVars = new Hashtable();

            string host = String.Empty;

            string[] querystringkeys = request.QueryString.AllKeys;
            string[] rHeaders = request.Headers.AllKeys;

            keysvals.Add("body", requestBody);
            keysvals.Add("uri", request.RawUrl);
            keysvals.Add("content-type", request.ContentType);
            keysvals.Add("http-method", request.HttpMethod);

            foreach (string queryname in querystringkeys)
            {
//                m_log.DebugFormat(
//                    "[BASE HTTP SERVER]: Got query paremeter {0}={1}", queryname, request.QueryString[queryname]);
                keysvals.Add(queryname, request.QueryString[queryname]);
                requestVars.Add(queryname, keysvals[queryname]);
            }

            foreach (string headername in rHeaders)
            {
//                m_log.Debug("[BASE HTTP SERVER]: " + headername + "=" + request.Headers[headername]);
                headervals[headername] = request.Headers[headername];
            }

            if (headervals.Contains("Host"))
            {
                host = (string)headervals["Host"];
            }

            keysvals.Add("headers", headervals);
            keysvals.Add("querystringkeys", querystringkeys);
            keysvals.Add("requestvars", requestVars);
//            keysvals.Add("form", request.Form);

            if (keysvals.Contains("method"))
            {
//                m_log.Debug("[BASE HTTP SERVER]: Contains Method");
                string method = (string) keysvals["method"];
//                m_log.Debug("[BASE HTTP SERVER]: " + requestBody);
                GenericHTTPMethod requestprocessor;
                bool foundHandler = TryGetHTTPHandler(method, out requestprocessor);
                if (foundHandler)
                {
                    Hashtable responsedata1 = requestprocessor(keysvals);
                    buffer = DoHTTPGruntWork(responsedata1,response);

                    //SendHTML500(response);
                }
                else
                {
//                    m_log.Warn("[BASE HTTP SERVER]: Handler Not Found");
                    buffer = SendHTML404(response, host);
                }
            }
            else
            {
                GenericHTTPMethod requestprocessor;
                bool foundHandler = TryGetHTTPHandlerPathBased(request.RawUrl, out requestprocessor);
                if (foundHandler)
                {
                    Hashtable responsedata2 = requestprocessor(keysvals);
                    buffer = DoHTTPGruntWork(responsedata2, response);

                    //SendHTML500(response);
                }
                else
                {
//                    m_log.Warn("[BASE HTTP SERVER]: Handler Not Found2");
                    buffer = SendHTML404(response, host);
                }
            }

            return buffer;
        }

        private bool TryGetHTTPHandlerPathBased(string path, out GenericHTTPMethod httpHandler)
        {
            httpHandler = null;
            // Pull out the first part of the path
            // splitting the path by '/' means we'll get the following return..
            // {0}/{1}/{2}
            // where {0} isn't something we really control 100%

            string[] pathbase = path.Split('/');
            string searchquery = "/";

            if (pathbase.Length < 1)
                return false;

            for (int i = 1; i < pathbase.Length; i++)
            {
                searchquery += pathbase[i];
                if (pathbase.Length - 1 != i)
                    searchquery += "/";
            }

            // while the matching algorithm below doesn't require it, we're expecting a query in the form
            //
            //   [] = optional
            //   /resource/UUID/action[/action]
            //
            // now try to get the closest match to the reigstered path
            // at least for OGP, registered path would probably only consist of the /resource/

            string bestMatch = null;

//            m_log.DebugFormat(
//                "[BASE HTTP HANDLER]: TryGetHTTPHandlerPathBased() looking for HTTP handler to match {0}", searchquery);

            lock (m_HTTPHandlers)
            {
                foreach (string pattern in m_HTTPHandlers.Keys)
                {
                    if (searchquery.ToLower().StartsWith(pattern.ToLower()))
                    {
                        if (String.IsNullOrEmpty(bestMatch) || searchquery.Length > bestMatch.Length)
                        {
                            // You have to specifically register for '/' and to get it, you must specifically request it
                            if (pattern == "/" && searchquery == "/" || pattern != "/")
                                bestMatch = pattern;
                        }
                    }
                }

                if (String.IsNullOrEmpty(bestMatch))
                {
                    httpHandler = null;
                    return false;
                }
                else
                {
                    if (bestMatch == "/" && searchquery != "/")
                        return false;

                    httpHandler =  m_HTTPHandlers[bestMatch];
                    return true;
                }
            }
        }

        internal byte[] DoHTTPGruntWork(Hashtable responsedata, OSHttpResponse response)
        {
            int responsecode;
            string responseString = String.Empty;
            byte[] responseData = null;
            string contentType;

            if (responsedata == null)
            {
                responsecode = 500;
                responseString = "No response could be obtained";
                contentType = "text/plain";
                responsedata = new Hashtable();
            }
            else
            {
                try
                {
                    //m_log.Info("[BASE HTTP SERVER]: Doing HTTP Grunt work with response");
                    responsecode = (int)responsedata["int_response_code"];
                    if (responsedata["bin_response_data"] != null)
                        responseData = (byte[])responsedata["bin_response_data"];
                    else
                        responseString = (string)responsedata["str_response_string"];
                    contentType = (string)responsedata["content_type"];
                    if (responseString == null)
                        responseString = String.Empty;
                }
                catch
                {
                    responsecode = 500;
                    responseString = "No response could be obtained";
                    contentType = "text/plain";
                    responsedata = new Hashtable();
                }
            }

            if (responsedata.ContainsKey("error_status_text"))
            {
                response.StatusDescription = (string)responsedata["error_status_text"];
            }
            if (responsedata.ContainsKey("http_protocol_version"))
            {
                response.ProtocolVersion = (string)responsedata["http_protocol_version"];
            }

            if (responsedata.ContainsKey("keepalive"))
            {
                bool keepalive = (bool)responsedata["keepalive"];
                response.KeepAlive = keepalive;
            }

            // Cross-Origin Resource Sharing with simple requests
            if (responsedata.ContainsKey("access_control_allow_origin"))
                response.AddHeader("Access-Control-Allow-Origin", (string)responsedata["access_control_allow_origin"]);

            //Even though only one other part of the entire code uses HTTPHandlers, we shouldn't expect this
            //and should check for NullReferenceExceptions

            if (string.IsNullOrEmpty(contentType))
            {
                contentType = "text/html";
            }

            // The client ignores anything but 200 here for web login, so ensure that this is 200 for that

            response.StatusCode = responsecode;

            if (responsecode == (int)OSHttpStatusCode.RedirectMovedPermanently)
            {
                response.RedirectLocation = (string)responsedata["str_redirect_location"];
                response.StatusCode = responsecode;
            }

            response.AddHeader("Content-Type", contentType);
            if (responsedata.ContainsKey("headers"))
            {
                Hashtable headerdata = (Hashtable)responsedata["headers"];

                foreach (string header in headerdata.Keys)
                    response.AddHeader(header, headerdata[header].ToString());
            }

            byte[] buffer;

            if (responseData != null)
            {
                buffer = responseData;
            }
            else
            {
                if (!(contentType.Contains("image")
                    || contentType.Contains("x-shockwave-flash")
                    || contentType.Contains("application/x-oar")
                    || contentType.Contains("application/vnd.ll.mesh")))
                {
                    // Text
                    buffer = Encoding.UTF8.GetBytes(responseString);
                }
                else
                {
                    // Binary!
                    buffer = Convert.FromBase64String(responseString);
                }

                response.ContentLength64 = buffer.Length;
                response.ContentEncoding = Encoding.UTF8;
            }

            return buffer;
        }

        public byte[] SendHTML404(OSHttpResponse response, string host)
        {
            // I know this statuscode is dumb, but the client doesn't respond to 404s and 500s
            response.StatusCode = 404;
            response.AddHeader("Content-type", "text/html");

            string responseString = GetHTTP404(host);
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            return buffer;
        }

        public byte[] SendHTML500(OSHttpResponse response)
        {
            // I know this statuscode is dumb, but the client doesn't respond to 404s and 500s
            response.StatusCode = (int)OSHttpStatusCode.SuccessOk;
            response.AddHeader("Content-type", "text/html");

            string responseString = GetHTTP500();
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            return buffer;
        }

        public void Start()
        {
            Start(true, true);
        }

        /// <summary>
        /// Start the http server
        /// </summary>
        /// <param name='processPollRequestsAsync'>
        /// If true then poll responses are performed asynchronsly.
        /// Option exists to allow regression tests to perform processing synchronously.
        /// </param>
        public void Start(bool performPollResponsesAsync, bool runPool)
        {
            m_log.InfoFormat(
                "[BASE HTTP SERVER]: Starting {0} server on port {1}", UseSSL ? "HTTPS" : "HTTP", Port);

            try
            {
                //m_httpListener = new HttpListener();

                NotSocketErrors = 0;
                if (!m_ssl)
                {
                    //m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
                    //m_httpListener.Prefixes.Add("http://10.1.1.5:" + m_port + "/");
                    m_httpListener2 = CoolHTTPListener.Create(m_listenIPAddress, (int)m_port);
                    m_httpListener2.ExceptionThrown += httpServerException;
                    m_httpListener2.LogWriter = httpserverlog;

                    // Uncomment this line in addition to those in HttpServerLogWriter
                    // if you want more detailed trace information from the HttpServer
                    //m_httpListener2.UseTraceLogs = true;

                    //m_httpListener2.DisconnectHandler = httpServerDisconnectMonitor;
                }
                else
                {
                    //m_httpListener.Prefixes.Add("https://+:" + (m_sslport) + "/");
                    //m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
                    m_httpListener2 = CoolHTTPListener.Create(IPAddress.Any, (int)m_port, m_cert);
                    if(m_certificateValidationCallback != null)
                        m_httpListener2.CertificateValidationCallback = m_certificateValidationCallback;
                    m_httpListener2.ExceptionThrown += httpServerException;
                    m_httpListener2.LogWriter = httpserverlog;
                }

                m_httpListener2.RequestReceived += OnRequest;
                //m_httpListener.Start();
                m_httpListener2.Start(64);

                lock(m_generalLock)
                {
                    if (runPool)
                    {
                        if(m_pollServiceManager == null)
                            m_pollServiceManager = new PollServiceRequestManager(performPollResponsesAsync, 2, 25000);
                        m_pollServiceManager.Start();
                    }
                }

                HTTPDRunning = true;

                //HttpListenerContext context;
                //while (true)
                //{
                //    context = m_httpListener.GetContext();
                //    ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(HandleRequest), context);
               // }
            }
            catch (Exception e)
            {
                m_log.Error("[BASE HTTP SERVER]: Error - " + e.Message);
                m_log.Error("[BASE HTTP SERVER]: Tip: Do you have permission to listen on port " + m_port + "?");

                // We want this exception to halt the entire server since in current configurations we aren't too
                // useful without inbound HTTP.
                throw e;
            }

            m_requestsProcessedStat
                = new Stat(
                    "HTTPRequestsServed",
                    "Number of inbound HTTP requests processed",
                    "",
                    "requests",
                    "httpserver",
                    Port.ToString(),
                    StatType.Pull,
                    MeasuresOfInterest.AverageChangeOverTime,
                    stat => stat.Value = RequestNumber,
                    StatVerbosity.Debug);

            StatsManager.RegisterStat(m_requestsProcessedStat);
        }

        public void httpServerDisconnectMonitor(IHttpClientContext source, SocketError err)
        {
            switch (err)
            {
                case SocketError.NotSocket:
                    NotSocketErrors++;

                    break;
            }
        }

        public void httpServerException(object source, Exception exception)
        {
            if (source.ToString() == "HttpServer.HttpListener" && exception.ToString().StartsWith("Mono.Security.Protocol.Tls.TlsException"))
                return;
            m_log.ErrorFormat("[BASE HTTP SERVER]: {0} had an exception {1}", source.ToString(), exception.ToString());
        }

        public void Stop(bool stopPool = false)
        {
            HTTPDRunning = false;

            StatsManager.DeregisterStat(m_requestsProcessedStat);

            try
            {
                lock(m_generalLock)
                {
                    if (stopPool && m_pollServiceManager != null)
                        m_pollServiceManager.Stop();
                }

                m_httpListener2.ExceptionThrown -= httpServerException;
                //m_httpListener2.DisconnectHandler = null;

                m_httpListener2.LogWriter = null;
                m_httpListener2.RequestReceived -= OnRequest;
                m_httpListener2.Stop();
            }
            catch (NullReferenceException)
            {
                m_log.Warn("[BASE HTTP SERVER]: Null Reference when stopping HttpServer.");
            }
        }

        public void RemoveStreamHandler(string httpMethod, string path)
        {
            string handlerKey = GetHandlerKey(httpMethod, path);

            //m_log.DebugFormat("[BASE HTTP SERVER]: Removing handler key {0}", handlerKey);

            lock (m_streamHandlers)
                m_streamHandlers.Remove(handlerKey);
        }

        public void RemoveHTTPHandler(string httpMethod, string path)
        {
            if (path == null) return; // Caps module isn't loaded, tries to remove handler where path = null
            lock (m_HTTPHandlers)
            {
                if (httpMethod != null && httpMethod.Length == 0)
                {
                    m_HTTPHandlers.Remove(path);
                    return;
                }

                m_HTTPHandlers.Remove(GetHandlerKey(httpMethod, path));
            }
        }

        public void RemovePollServiceHTTPHandler(string httpMethod, string path)
        {
            lock (m_pollHandlers)
                m_pollHandlers.Remove(path);
        }

//        public bool RemoveAgentHandler(string agent, IHttpAgentHandler handler)
//        {
//            lock (m_agentHandlers)
//            {
//                IHttpAgentHandler foundHandler;
//
//                if (m_agentHandlers.TryGetValue(agent, out foundHandler) && foundHandler == handler)
//                {
//                    m_agentHandlers.Remove(agent);
//                    return true;
//                }
//            }
//
//            return false;
//        }

        public void RemoveXmlRPCHandler(string method)
        {
            lock (m_rpcHandlers)
                m_rpcHandlers.Remove(method);
        }

        public void RemoveJsonRPCHandler(string method)
        {
            lock(jsonRpcHandlers)
                jsonRpcHandlers.Remove(method);
        }

        public bool RemoveLLSDHandler(string path, LLSDMethod handler)
        {
            lock (m_llsdHandlers)
            {
                LLSDMethod foundHandler;

                if (m_llsdHandlers.TryGetValue(path, out foundHandler) && foundHandler == handler)
                {
                    m_llsdHandlers.Remove(path);
                    return true;
                }
            }

            return false;
        }

        public string GetHTTP404(string host)
        {
            string file = Path.Combine(".", "http_404.html");
            if (!File.Exists(file))
                return getDefaultHTTP404(host);

            StreamReader sr = File.OpenText(file);
            string result = sr.ReadToEnd();
            sr.Close();
            return result;
        }

        public string GetHTTP500()
        {
            string file = Path.Combine(".", "http_500.html");
            if (!File.Exists(file))
                return getDefaultHTTP500();
            string result;
            using(StreamReader sr = File.OpenText(file))
                result = sr.ReadToEnd();
            return result;
        }

        // Fallback HTTP responses in case the HTTP error response files don't exist
        private static string getDefaultHTTP404(string host)
        {
            return "<HTML><HEAD><TITLE>404 Page not found</TITLE><BODY><BR /><H1>Ooops!</H1><P>The page you requested has been obsconded with by knomes. Find hippos quick!</P><P>If you are trying to log-in, your link parameters should have: &quot;-loginpage http://" + host + "/?method=login -loginuri http://" + host + "/&quot; in your link </P></BODY></HTML>";
        }

        private static string getDefaultHTTP500()
        {
            return "<HTML><HEAD><TITLE>500 Internal Server Error</TITLE><BODY><BR /><H1>Ooops!</H1><P>The server you requested is overun by knomes! Find hippos quick!</P></BODY></HTML>";
        }
    }

    public class HttpServerContextObj
    {
        public IHttpClientContext context = null;
        public IHttpRequest req = null;
        public OSHttpRequest oreq = null;
        public OSHttpResponse oresp = null;

        public HttpServerContextObj(IHttpClientContext contxt, IHttpRequest reqs)
        {
            context = contxt;
            req = reqs;
        }

        public HttpServerContextObj(OSHttpRequest osreq, OSHttpResponse osresp)
        {
            oreq = osreq;
            oresp = osresp;
        }
    }

    /// <summary>
    /// Relays HttpServer log messages to our own logging mechanism.
    /// </summary>
    /// To use this you must uncomment the switch section
    ///
    /// You may also be able to get additional trace information from HttpServer if you uncomment the UseTraceLogs
    /// property in StartHttp() for the HttpListener
    public class HttpServerLogWriter : ILogWriter
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void Write(object source, LogPrio priority, string message)
        {
            /*
            switch (priority)
            {
                case LogPrio.Trace:
                    m_log.DebugFormat("[{0}]: {1}", source, message);
                    break;
                case LogPrio.Debug:
                    m_log.DebugFormat("[{0}]: {1}", source, message);
                    break;
                case LogPrio.Error:
                    m_log.ErrorFormat("[{0}]: {1}", source, message);
                    break;
                case LogPrio.Info:
                    m_log.InfoFormat("[{0}]: {1}", source, message);
                    break;
                case LogPrio.Warning:
                    m_log.WarnFormat("[{0}]: {1}", source, message);
                    break;
                case LogPrio.Fatal:
                    m_log.ErrorFormat("[{0}]: FATAL! - {1}", source, message);
                    break;
                default:
                    break;
            }
            */

            return;
        }
    }
}
