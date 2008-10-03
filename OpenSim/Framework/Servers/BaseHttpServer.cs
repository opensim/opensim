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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Xml;
using OpenMetaverse.StructuredData;
using log4net;
using Nwc.XmlRpc;
using CoolHTTPListener = HttpServer.HttpListener;
using IHttpClientContext = HttpServer.IHttpClientContext;
using IHttpRequest = HttpServer.IHttpRequest;

namespace OpenSim.Framework.Servers
{
    public class BaseHttpServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Thread m_workerThread;
        protected HttpListener m_httpListener;
        protected CoolHTTPListener m_httpListener2;
        protected Dictionary<string, XmlRpcMethod> m_rpcHandlers        = new Dictionary<string, XmlRpcMethod>();
        protected DefaultLLSDMethod m_defaultLlsdHandler = null; // <--   Moving away from the monolithic..  and going to /registered/
        protected Dictionary<string, LLSDMethod> m_llsdHandlers         = new Dictionary<string, LLSDMethod>();
        protected Dictionary<string, IRequestHandler> m_streamHandlers  = new Dictionary<string, IRequestHandler>();
        protected Dictionary<string, GenericHTTPMethod> m_HTTPHandlers  = new Dictionary<string, GenericHTTPMethod>();
        protected Dictionary<string, IHttpAgentHandler> m_agentHandlers = new Dictionary<string, IHttpAgentHandler>();

        protected uint m_port;
        protected uint m_sslport;
        protected bool m_ssl = false;
        protected bool m_firstcaps = true;
        protected string m_SSLCommonName = "";

        public uint SSLPort
        {
            get { return m_sslport; }
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

        public BaseHttpServer(uint port)
        {
            m_port = port;
        }

        public BaseHttpServer(uint port, bool ssl)
        {
            m_ssl = ssl;
            m_port = port;
            
        }


        public BaseHttpServer(uint port, bool ssl, uint sslport, string CN)
        {
            m_ssl = ssl;
            m_port = port;
            if (m_ssl)
            {
                //SetupSsl((int)sslport, CN);
                m_sslport = sslport;
            }
        }

        
        /*
         * 
        public bool SetupSsl(int port, string CN)
        {
            string searchCN = Environment.MachineName.ToUpper();
            
            if (CN.Length > 0)
                searchCN = CN.ToUpper();

            m_SSLCommonName = searchCN;

            Type t = Type.GetType("Mono.Runtime");
            if (t != null)
            {
                // TODO Mono User Friendly HTTPS setup
                // if this doesn't exist, then mono people can still manually use httpcfg
            }
            else
            {
                // Windows.
                // Search through the store for a certificate with a Common name specified in OpenSim.ini.
                // We need to find it's hash so we can pass it to httpcfg
                X509Store store = new X509Store(StoreLocation.LocalMachine);
                //Use the first cert to configure Ssl
                store.Open(OpenFlags.ReadOnly);
                //Assumption is we have certs. If not then this call will fail :(
                try
                {
                    bool found = false;
                    //X509Certificate2.CreateFromCertFile("testCert.cer");

                    foreach (X509Certificate2 cert in store.Certificates)
                    {
                        String certHash = cert.GetCertHashString();
                        //Only install certs issued for the machine and has the name as the machine name
                        if (cert.Subject.ToUpper().IndexOf(searchCN) >= 0)
                        {
                            string httpcfgparams = String.Format("set ssl -i 0.0.0.0:{1} -c \"MY\" -h {0}", certHash, port);
                            try
                            {
                                found = true;

                                ExecuteHttpcfgCommand(httpcfgparams);
 
                                break;
                            }
                            catch (Exception e)
                            {
                                m_log.WarnFormat("[HTTPS]: Automatic HTTPS setup failed.  Do you have httpcfg.exe in your path?  If not, you can download it in the windowsXP Service Pack 2 Support Tools, here: http://www.microsoft.com/downloads/details.aspx?FamilyID=49ae8576-9bb9-4126-9761-ba8011fabf38&displaylang=en.  When you get it installed type, httpcfg {0} - {1}", httpcfgparams, e);
                                return false;
                            }
                        }
                    }

                    if (!found)
                    {
                        m_log.WarnFormat("[HTTPS]: We didn't find a certificate that matched the common name {0}.  Automatic HTTPS setup failed, you may have certificate errors.  To fix this, make sure you generate a certificate request(CSR) using OpenSSL or the IIS snap-in with the common name you specified in opensim.ini. Then get it signed by a certification authority or sign it yourself with OpenSSL and the junkCA.  Finally, be sure to import the cert to the 'MY' store(StoreLocation.LocalMachine)", searchCN);
                        return false;
                    }

                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[HTTPS]: We didn't any certificates in your LocalMachine certificate store.  Automatic HTTPS setup failed, you may have certificate errors.  To fix this, make sure you generate a certificate request(CSR) using OpenSSL or the IIS snap-inwith the common name you specified in opensim.ini. Then get it signed by a certification authority or sign it yourself with OpenSSL and the junkCA.  Finally, be sure to import the cert to the 'MY' store(StoreLocation.LocalMachine). The configured common name is {0} - {1}", searchCN, e);
                    return false;
                }
                finally
                {
                    if (store != null)
                    {
                        store.Close();
                    }
                }
            }
            return true;
        }

        private void ExecuteHttpcfgCommand(string p)
        {
            
            string file = "httpcfg";

            ProcessStartInfo info = new ProcessStartInfo(file, p);
            // Redirect output so we can read it.
            info.RedirectStandardOutput = true;
            // To redirect, we must not use shell execute.
            info.UseShellExecute = false;

            // Create and execute the process.
            Process httpcfgprocess = Process.Start(info);
            httpcfgprocess.Start();
            string result = httpcfgprocess.StandardOutput.ReadToEnd();
            if (result.Contains("HttpSetServiceConfiguration completed with"))
            {
                //success
            
            }
            else
            {
                //fail
                m_log.WarnFormat("[HTTPS]:Error binding certificate with the requested port.  Message:{0}", result);
            }
            
        }
        */

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

        private static string GetHandlerKey(string httpMethod, string path)
        {
            return httpMethod + ":" + path;
        }

        public bool AddXmlRPCHandler(string method, XmlRpcMethod handler)
        {
            lock (m_rpcHandlers)
            {
                m_rpcHandlers[method] = handler;
                return true;
            }
        }

        public bool AddHTTPHandler(string method, GenericHTTPMethod handler)
        {
            lock (m_HTTPHandlers)
            {
                if (!m_HTTPHandlers.ContainsKey(method))
                {
                    m_HTTPHandlers.Add(method, handler);
                    return true;
                }
            }

            //must already have a handler for that path so return false
            return false;
        }

        // Note that the agent string is provided simply to differentiate
        // the handlers - it is NOT required to be an actual agent header
        // value.

        public bool AddAgentHandler(string agent, IHttpAgentHandler handler)
        {
            lock (m_agentHandlers)
            {
                if (!m_agentHandlers.ContainsKey(agent))
                {
                    m_agentHandlers.Add(agent, handler);
                    return true;
                }
            }

            //must already have a handler for that path so return false
            return false;
        }

        /// <summary>
        /// Adds a LLSD handler, yay.
        /// </summary>
        /// <param name="path">/resource/ path</param>
        /// <param name="handler">handle the LLSD response</param>
        /// <returns></returns>
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

        public bool SetDefaultLLSDHandler(DefaultLLSDMethod handler)
        {
            m_defaultLlsdHandler = handler;
            return true;
        }

        /// <summary>
        /// HttpListener Handle an individual http request.  This method is given to a worker in the thread pool.
        /// </summary>
        /// <param name="stateinfo"></param>
        public virtual void HandleRequest(Object stateinfo)
        {
            // force the culture to en-US
            

            // If we don't catch the exception here it will just disappear into the thread pool and we'll be none the wiser
            try
            {
                HttpListenerContext context = (HttpListenerContext)stateinfo;

                OSHttpRequest request = new OSHttpRequest(context.Request);
                OSHttpResponse response = new OSHttpResponse(context.Response);
                
                HandleRequest(request, response);
            
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
                m_log.WarnFormat("[BASE HTTP SERVER]: HandleRequest threw {0}.\nNOTE: this may be spurious on Linux", e);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[BASE HTTP SERVER]: HandleRequest() threw {0}", e);
            }
        }

        /*
        /// <summary>
        /// HttpListener Handle an individual http request.  This method is given to a worker in the thread pool.
        /// </summary>
        /// <param name="stateinfo"></param>
        public virtual void HandleRequestHttpServer(Object stateinfo)
        {
            // force the culture to en-US


            // If we don't catch the exception here it will just disappear into the thread pool and we'll be none the wiser
            try
            {
                HttpServerContextObj context = (HttpServerContextObj)stateinfo;

                OSHttpRequest request = new OSHttpRequest(context.Request);
                OSHttpResponse response = new OSHttpResponse(context.Response);

                HandleRequest(request, response);

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
                m_log.WarnFormat("[BASE HTTP SERVER]: HandleRequest threw {0}.\nNOTE: this may be spurious on Linux", e);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[BASE HTTP SERVER]: HandleRequest() threw {0}", e);
            }
        }
      */ 
        public void OnHandleRequestIOThread(IHttpClientContext context, IHttpRequest request)
        {
            OSHttpRequest req = new OSHttpRequest(context, request);
            OSHttpResponse resp = new OSHttpResponse(new HttpServer.HttpResponse(context, request));
            //m_log.Info("[Debug BASE HTTP SERVER]: Got Request");
            //HttpServerContextObj objstate= new HttpServerContextObj(req,resp);
            //ThreadPool.QueueUserWorkItem(new WaitCallback(ConvertIHttpClientContextToOSHttp), (object)objstate);
            HandleRequest(req, resp);
        }

        public void ConvertIHttpClientContextToOSHttp(object stateinfo)
        {
            HttpServerContextObj objstate = (HttpServerContextObj)stateinfo;
            //OSHttpRequest request = new OSHttpRequest(objstate.context,objstate.req);
            //OSHttpResponse resp = new OSHttpResponse(new HttpServer.HttpResponse(objstate.context, objstate.req));

            OSHttpRequest request = objstate.oreq;
            OSHttpResponse resp = objstate.oresp;
            //OSHttpResponse resp = new OSHttpResponse(new HttpServer.HttpResponse(objstate.context, objstate.req));

            /*
            request.AcceptTypes = objstate.req.AcceptTypes;
            request.ContentLength = (long)objstate.req.ContentLength;
            request.Headers = objstate.req.Headers;
            request.HttpMethod = objstate.req.Method;
            request.InputStream = objstate.req.Body;
            foreach (string str in request.Headers)
            {
                if (str.ToLower().Contains("content-type: "))
                {
                    request.ContentType = str.Substring(13, str.Length - 13);
                    break;
                }
            }
            //request.KeepAlive = objstate.req.
            foreach (HttpServer.HttpInput httpinput in objstate.req.QueryString)
            {
                request.QueryString.Add(httpinput.Name, httpinput[httpinput.Name]);
            }
            
            //request.Query = objstate.req.//objstate.req.QueryString;
            //foreach (
            //request.QueryString = objstate.req.QueryString;

             */
            HandleRequest(request,resp);
            

        }

        public virtual void HandleRequest(OSHttpRequest request, OSHttpResponse response)
        {
            try 
            {
                Culture.SetCurrentCulture();
                //  This is the REST agent interface. We require an agent to properly identify
                //  itself. If the REST handler recognizes the prefix it will attempt to
                //  satisfy the request. If it is not recognizable, and no damage has occurred
                //  the request can be passed through to the other handlers. This is a low
                //  probability event; if a request is matched it is normally expected to be
                //  handled
                //m_log.Info("[Debug BASE HTTP SERVER]: Handling Request" + request.RawUrl);
                IHttpAgentHandler agentHandler;

                if (TryGetAgentHandler(request, response, out agentHandler))
                {
                    if (HandleAgentRequest(agentHandler, request, response))
                    {
                        return;
                    }
                }

                IRequestHandler requestHandler;
                response.KeepAlive   = false;
                response.SendChunked = false;

                string path = request.RawUrl;
                string handlerKey = GetHandlerKey(request.HttpMethod, path);

                //m_log.DebugFormat("[BASE HTTP SERVER]: Handling {0} request for {1}", request.HttpMethod, path);

                if (TryGetStreamHandler(handlerKey, out requestHandler))
                {
                    //m_log.Info("[Debug BASE HTTP SERVER]: Found Stream Handler");
                    // Okay, so this is bad, but should be considered temporary until everything is IStreamHandler.
                    byte[] buffer;
                    if (requestHandler is IStreamedRequestHandler)
                    {
                        IStreamedRequestHandler streamedRequestHandler = requestHandler as IStreamedRequestHandler;

                        buffer = streamedRequestHandler.Handle(path, request.InputStream, request, response);
                    }
                    else if (requestHandler is IGenericHTTPHandler)
                    {
                        //m_log.Info("[Debug BASE HTTP SERVER]: Found Caps based HTTP Handler");
                        IGenericHTTPHandler HTTPRequestHandler = requestHandler as IGenericHTTPHandler;
                        Stream requestStream = request.InputStream;

                        Encoding encoding = Encoding.UTF8;
                        StreamReader reader = new StreamReader(requestStream, encoding);

                        string requestBody = reader.ReadToEnd();
                        
                        
                        reader.Close();
                        requestStream.Close();

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
                        
                        keysvals.Add("requestbody",requestBody);
                        if (keysvals.Contains("method"))
                        {
                            //m_log.Warn("[HTTP]: Contains Method");
                            //string method = (string)keysvals["method"];
                            //m_log.Warn("[HTTP]: " + requestBody);
                   
                        } 
                        DoHTTPGruntWork(HTTPRequestHandler.Handle(path,keysvals), response);
                        return;                        
                    }
                    else
                    {
                        IStreamHandler streamHandler = (IStreamHandler)requestHandler;

                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            streamHandler.Handle(path, request.InputStream, memoryStream, request, response);
                            memoryStream.Flush();
                            buffer = memoryStream.ToArray();
                        }
                    }

                    request.InputStream.Close();
                    if (!response.IsContentTypeSet) response.ContentType = requestHandler.ContentType;
                    if (!response.SendChunked)
                        response.ContentLength64 = buffer.LongLength;

                    try
                    {
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        //response.OutputStream.Close();
                    }
                    catch (HttpListenerException)
                    {
                        m_log.WarnFormat("[BASE HTTP SERVER]: HTTP request abnormally terminated.");
                    }
                    //response.OutputStream.Close();
                    try
                    {
                        response.Send();
                    }
                    catch (SocketException e)
                    {
                        // This has to be here to prevent a Linux/Mono crash
                        m_log.WarnFormat("[BASE HTTP SERVER] XmlRpcRequest issue {0}.\nNOTE: this may be spurious on Linux.", e);
                    }
                    return;
                }

                if (request.AcceptTypes != null && request.AcceptTypes.Length > 0)
                {
                    foreach (string strAccept in request.AcceptTypes)
                    {
                        if (strAccept.Contains("application/llsd+xml"))
                        {
                            //m_log.Info("[Debug BASE HTTP SERVER]: Found an application/llsd+xml accept header");
                            HandleLLSDRequests(request, response);
                            return;
                        }
                    }
                }

                switch (request.ContentType)
                {
                    case null:
                    case "text/html":
                        //m_log.Info("[Debug BASE HTTP SERVER]: found a text/html content type");
                        HandleHTTPRequest(request, response);
                        return;

                    case "application/llsd+xml":
                    case "application/xml+llsd":
                        //m_log.Info("[Debug BASE HTTP SERVER]: found a application/llsd+xml content type");
                        HandleLLSDRequests(request, response);
                        return;

                    case "text/xml":
                    case "application/xml":
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
                            //m_log.Info("[Debug BASE HTTP SERVER]: Found LLSD Handler");
                            HandleLLSDRequests(request, response);
                            return;
                        }
                        //m_log.Info("[Debug BASE HTTP SERVER]: Checking for HTTP Handler");
                        if (DoWeHaveAHTTPHandler(request.RawUrl))
                        {
                            //m_log.Info("[Debug BASE HTTP SERVER]: found HTTP Handler");
                            HandleHTTPRequest(request, response);
                            return;
                        }

                        //m_log.Info("[Debug BASE HTTP SERVER]: Generic XMLRPC");
                        // generic login request.
                        HandleXmlRpcRequests(request, response);
                        
                        return;
                }
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
                m_log.WarnFormat("[BASE HTTP SERVER]: HandleRequest threw {0}.\nNOTE: this may be spurious on Linux", e);
            }
            catch (EndOfStreamException e)
            {
                m_log.ErrorFormat("[BASE HTTP SERVER]: HandleRequest() threw {0}", e);
            }
        }

        private bool TryGetStreamHandler(string handlerKey, out IRequestHandler streamHandler)
        {
            string bestMatch = null;

            foreach (string pattern in m_streamHandlers.Keys)
            {
                if (handlerKey.StartsWith(pattern))
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

        private bool TryGetHTTPHandler(string handlerKey, out GenericHTTPMethod HTTPHandler)
        {
            string bestMatch = null;

            foreach (string pattern in m_HTTPHandlers.Keys)
            {
                if (handlerKey.StartsWith(pattern))
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

        private bool TryGetAgentHandler(OSHttpRequest request, OSHttpResponse response, out IHttpAgentHandler agentHandler)
        {
            agentHandler = null;
            try
            {
                foreach (IHttpAgentHandler handler in m_agentHandlers.Values)
                {
                    if (handler.Match(request, response))
                    {
                        agentHandler = handler;
                        return true;
                    }
                }
            }
            catch(KeyNotFoundException)
            {
            }

            return false;
        }

        /// <summary>
        /// Try all the registered xmlrpc handlers when an xmlrpc request is received.
        /// Sends back an XMLRPC unknown request response if no handler is registered for the requested method.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        private void HandleXmlRpcRequests(OSHttpRequest request, OSHttpResponse response)
        {
            Stream requestStream = request.InputStream;

            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(requestStream, encoding);

            string requestBody = reader.ReadToEnd();
            reader.Close();
            requestStream.Close();

            string responseString = String.Empty;
            XmlRpcRequest xmlRprcRequest = null;

            try
            {
                xmlRprcRequest = (XmlRpcRequest) (new XmlRpcRequestDeserializer()).Deserialize(requestBody);
            }
            catch (XmlException)
            {
            }

            if (xmlRprcRequest != null)
            {
                string methodName = xmlRprcRequest.MethodName;
                if (methodName != null)
                {
                    xmlRprcRequest.Params.Add(request.RemoteIPEndPoint);
                    XmlRpcResponse xmlRpcResponse;

                    XmlRpcMethod method;
                    if (m_rpcHandlers.TryGetValue(methodName, out method))
                    {
                        xmlRpcResponse = method(xmlRprcRequest);
                    }
                    else
                    {
                        xmlRpcResponse = new XmlRpcResponse();
                        // Code set in accordance with http://xmlrpc-epi.sourceforge.net/specs/rfc.fault_codes.php
                        xmlRpcResponse.SetFault(-32601, String.Format("Requested method [{0}] not found", methodName));
                    }

                    responseString = XmlRpcResponseSerializer.Singleton.Serialize(xmlRpcResponse);
                }
                else
                {
                    m_log.ErrorFormat("[BASE HTTP SERVER] Handler not found for http request {0}", request.RawUrl);
                    responseString = "Error";
                }
            }

            response.ContentType = "text/xml";

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;
            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                m_log.Warn("[HTTPD]: Error - " + ex.Message);
            }
            finally
            {
                try
                {
                    response.Send();
                }
                catch (SocketException e)
                {
                    // This has to be here to prevent a Linux/Mono crash
                    m_log.WarnFormat("[BASE HTTP SERVER] XmlRpcRequest issue {0}.\nNOTE: this may be spurious on Linux.", e);
                }
            }
        }

        private void HandleLLSDRequests(OSHttpRequest request, OSHttpResponse response)
        {
            //m_log.Warn("[BASE HTTP SERVER]: We've figured out it's a LLSD Request");
            Stream requestStream = request.InputStream;

            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(requestStream, encoding);

            string requestBody = reader.ReadToEnd();
            reader.Close();
            requestStream.Close();

            //m_log.DebugFormat("[OGP]: {0}:{1}", request.RawUrl, requestBody);
            response.KeepAlive = false;

            LLSD llsdRequest = null;
            LLSD llsdResponse = null;
            
            bool LegacyLLSDLoginLibOMV = (requestBody.Contains("passwd") && requestBody.Contains("mac") && requestBody.Contains("viewer_digest"));
            
            if (requestBody.Length == 0)
            // Get Request
            {
                requestBody = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><llsd><map><key>request</key><string>get</string></map></llsd>"; 
            }
            try
            {
                llsdRequest = LLSDParser.DeserializeXml(requestBody);
            }
            catch (Exception ex)
            {
                m_log.Warn("[HTTPD]: Error - " + ex.Message);
            }

            if (llsdRequest != null)// && m_defaultLlsdHandler != null)
            {

                LLSDMethod llsdhandler = null;

                if (TryGetLLSDHandler(request.RawUrl, out llsdhandler) && !LegacyLLSDLoginLibOMV)
                {
                    // we found a registered llsd handler to service this request
                    llsdResponse = llsdhandler(request.RawUrl, llsdRequest, (request.RemoteIPEndPoint == null)? "" : request.RemoteIPEndPoint.ToString());
                }
                else
                {
                    // we didn't find a registered llsd handler to service this request
                    // check if we have a default llsd handler

                    if (m_defaultLlsdHandler != null)
                    {
                        // LibOMV path
                        llsdResponse = m_defaultLlsdHandler(llsdRequest);
                    }
                    else
                    {
                        // Oops, no handler for this..   give em the failed message
                        llsdResponse = GenerateNoLLSDHandlerResponse();
                    }
                }

            }
            else
            {
                llsdResponse = GenerateNoLLSDHandlerResponse();
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
                response.ContentType = "application/llsd+xml";
                //m_log.Info("[Debug BASE HTTP SERVER]: Response: " + llsdResponse.ToString());
                buffer = LLSDParser.SerializeXmlBytes(llsdResponse);
            }
            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;
            response.KeepAlive = false;

            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                m_log.Warn("[HTTPD]: Error - " + ex.Message);
            }
            finally
            {
                //response.OutputStream.Close();
                try
                {
                    response.Send();
                    response.OutputStream.Flush();
                    response.OutputStream.Close();
                }
                catch (SocketException e)
                {
                    // This has to be here to prevent a Linux/Mono crash
                    m_log.WarnFormat("[BASE HTTP SERVER] XmlRpcRequest issue {0}.\nNOTE: this may be spurious on Linux.", e);
                }
            }
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

            foreach (string pattern in m_llsdHandlers.Keys)
            {

                if (searchquery.StartsWith(pattern) && searchquery.Length >= pattern.Length)
                {

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

        private LLSDMap GenerateNoLLSDHandlerResponse()
        {
            LLSDMap map = new LLSDMap();
            map["reason"] = LLSD.FromString("LLSDRequest");
            map["message"] = LLSD.FromString("No handler registered for LLSD Requests");
            map["login"] = LLSD.FromString("false");
            return map;
        }
        /// <summary>
        /// A specific agent handler was provided. Such a handler is expecetd to have an
        /// intimate, and highly specific relationship with the client. Consequently,
        /// nothing is done here.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="request"></param>
        /// <param name="response"></param>

        private bool HandleAgentRequest(IHttpAgentHandler handler, OSHttpRequest request, OSHttpResponse response)
        {
            // In the case of REST, then handler is responsible for ALL aspects of
            // the request/response handling. Nothing is done here, not even encoding.

            try
            {
                return handler.Handle(request, response);
            }
            catch (Exception e)
            {
                // If the handler did in fact close the stream, then this will blow
                // chunks. So that that doesn't disturb anybody we throw away any
                // and all exceptions raised. We've done our best to release the
                // client.
                try
                {
                    m_log.Warn("[HTTP-AGENT]: Error - " + e.Message);
                    response.SendChunked   = false;
                    response.KeepAlive     = false;
                    response.StatusCode    = (int)OSHttpStatusCode.ServerErrorInternalError;
                    //response.OutputStream.Close();
                    try
                    {
                        response.Send();
                    }
                    catch (SocketException f)
                    {
                        // This has to be here to prevent a Linux/Mono crash
                        m_log.WarnFormat("[BASE HTTP SERVER] XmlRpcRequest issue {0}.\nNOTE: this may be spurious on Linux.", f);
                    }
                }
                catch(Exception)
                {
                }
            }

            // Indicate that the request has been "handled"

            return true;

        }

        public void HandleHTTPRequest(OSHttpRequest request, OSHttpResponse response)
        {
            switch (request.HttpMethod)
            {
                case "OPTIONS":
                    response.StatusCode = (int)OSHttpStatusCode.SuccessOk;
                    return;

                default:
                    HandleContentVerbs(request, response);
                    return;
            }
        }

        private void HandleContentVerbs(OSHttpRequest request, OSHttpResponse response)
        {
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


            Stream requestStream = request.InputStream;

            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(requestStream, encoding);

            string requestBody = reader.ReadToEnd();
            // avoid warning for now
            reader.ReadToEnd();
            reader.Close();
            requestStream.Close();

            Hashtable keysvals = new Hashtable();
            Hashtable headervals = new Hashtable();
            string host = String.Empty;

            string[] querystringkeys = request.QueryString.AllKeys;
            string[] rHeaders = request.Headers.AllKeys;

            keysvals.Add("body", requestBody);
            keysvals.Add("uri", request.RawUrl);
            keysvals.Add("content-type", request.ContentType);


            foreach (string queryname in querystringkeys)
            {
                keysvals.Add(queryname, request.QueryString[queryname]);
            }

            foreach (string headername in rHeaders)
            {
                //m_log.Warn("[HEADER]: " + headername + "=" + request.Headers[headername]);
                headervals[headername] = request.Headers[headername];
            }

            if (headervals.Contains("Host"))
            {
                host = (string)headervals["Host"];
            }

            if (keysvals.Contains("method"))
            {
                //m_log.Warn("[HTTP]: Contains Method");
                string method = (string) keysvals["method"];
                //m_log.Warn("[HTTP]: " + requestBody);
                GenericHTTPMethod requestprocessor;
                bool foundHandler = TryGetHTTPHandler(method, out requestprocessor);
                if (foundHandler)
                {
                    Hashtable responsedata1 = requestprocessor(keysvals);
                    DoHTTPGruntWork(responsedata1,response);

                    //SendHTML500(response);
                }
                else
                {
                    //m_log.Warn("[HTTP]: Handler Not Found");
                    SendHTML404(response, host);
                }
            }
            else
            {

                GenericHTTPMethod requestprocessor;
                bool foundHandler = TryGetHTTPHandlerPathBased(request.RawUrl, out requestprocessor);
                if (foundHandler)
                {
                    Hashtable responsedata2 = requestprocessor(keysvals);
                    DoHTTPGruntWork(responsedata2, response);

                    //SendHTML500(response);
                }
                else
                {
                    //m_log.Warn("[HTTP]: Handler Not Found");
                    SendHTML404(response, host);
                }
            }
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

            foreach (string pattern in m_HTTPHandlers.Keys)
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

        private static void DoHTTPGruntWork(Hashtable responsedata, OSHttpResponse response)
        {
            //m_log.Info("[BASE HTTP SERVER]: Doing HTTP Grunt work with response");
            int responsecode = (int)responsedata["int_response_code"];
            string responseString = (string)responsedata["str_response_string"];
            string contentType = (string)responsedata["content_type"];
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

            byte[] buffer;

            if (!contentType.Contains("image"))
            {
                buffer = Encoding.UTF8.GetBytes(responseString);
            }
            else
            {
                buffer = Convert.FromBase64String(responseString);
            }

            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;


            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                m_log.Warn("[HTTPD]: Error - " + ex.Message);
            }
            finally
            {
                //response.OutputStream.Close();
                try
                {
                    response.Send();
                }
                catch (SocketException e)
                {
                    // This has to be here to prevent a Linux/Mono crash
                    m_log.WarnFormat("[BASE HTTP SERVER] XmlRpcRequest issue {0}.\nNOTE: this may be spurious on Linux.", e);
                }
            }
            
        }

        public void SendHTML404(OSHttpResponse response, string host)
        {
            // I know this statuscode is dumb, but the client doesn't respond to 404s and 500s
            response.StatusCode = (int)OSHttpStatusCode.SuccessOk;
            response.AddHeader("Content-type", "text/html");

            string responseString = GetHTTP404(host);
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;

            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                m_log.Warn("[HTTPD]: Error - " + ex.Message);
            }
            finally
            {
                //response.OutputStream.Close();
                try
                {
                    response.Send();
                }
                catch (SocketException e)
                {
                    // This has to be here to prevent a Linux/Mono crash
                    m_log.WarnFormat("[BASE HTTP SERVER] XmlRpcRequest issue {0}.\nNOTE: this may be spurious on Linux.", e);
                }
            }
        }

        public void SendHTML500(OSHttpResponse response)
        {
            // I know this statuscode is dumb, but the client doesn't respond to 404s and 500s
            response.StatusCode = (int)OSHttpStatusCode.SuccessOk;
            response.AddHeader("Content-type", "text/html");

            string responseString = GetHTTP500();
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            response.ContentEncoding = Encoding.UTF8;
            try
            {
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                m_log.Warn("[HTTPD]: Error - " + ex.Message);
            }
            finally
            {
                //response.OutputStream.Close();
                try
                {
                    response.Send();
                }
                catch (SocketException e)
                {
                    // This has to be here to prevent a Linux/Mono crash
                    m_log.WarnFormat("[BASE HTTP SERVER] XmlRpcRequest issue {0}.\nNOTE: this may be spurious on Linux.", e);
                }
            }
        }

        public void Start()
        {
            m_log.Info("[HTTPD]: Starting up HTTP Server");

            //m_workerThread = new Thread(new ThreadStart(StartHTTP));
            //m_workerThread.Name = "HttpThread";
            //m_workerThread.IsBackground = true;
            //m_workerThread.Start();
            //ThreadTracker.Add(m_workerThread);
            StartHTTP();
        }

        private void StartHTTP()
        {
            try
            {
                m_log.Info("[HTTPD]: Spawned main thread OK");
                //m_httpListener = new HttpListener();
                
                if (!m_ssl)
                {
                    //m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
                    //m_httpListener.Prefixes.Add("http://10.1.1.5:" + m_port + "/");
                    m_httpListener2 = new HttpServer.HttpListener(IPAddress.Any, (int)m_port);
                }
                else
                {
                    //m_httpListener.Prefixes.Add("https://+:" + (m_sslport) + "/");
                    //m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
                }

                m_httpListener2.RequestHandler += OnHandleRequestIOThread;
                //m_httpListener.Start();
                m_httpListener2.Start(5);

                //HttpListenerContext context;
                //while (true)
                //{
                //    context = m_httpListener.GetContext();
                //    ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
               // }
            }
            catch (Exception e)
            {
                m_log.Warn("[HTTPD]: Error - " + e.Message);
                m_log.Warn("Tip: Do you have permission to listen on port " + m_port + "," + m_sslport + "?");
            }
        }

        public void RemoveStreamHandler(string httpMethod, string path)
        {
            string handlerKey = GetHandlerKey(httpMethod, path);

            //m_log.DebugFormat("[BASE HTTP SERVER]: Removing handler key {0}", handlerKey);

            m_streamHandlers.Remove(handlerKey);
        }

        public void RemoveHTTPHandler(string httpMethod, string path)
        {
            if (httpMethod != null && httpMethod.Length == 0)
            {
                m_HTTPHandlers.Remove(path);
                return;
            }
            m_HTTPHandlers.Remove(GetHandlerKey(httpMethod, path));
        }

        // Remove the agent IF it is registered. Intercept the possible
        // exception.

        public bool RemoveAgentHandler(string agent, IHttpAgentHandler handler)
        {
            try
            {
                if (handler == m_agentHandlers[agent])
                {
                    m_agentHandlers.Remove(agent);
                    return true;
                }
            }
            catch(KeyNotFoundException)
            {
            }

            return false;
        }
        public bool RemoveLLSDHandler(string path, LLSDMethod handler)
        {

            try
            {
                if (handler == m_llsdHandlers[path])
                {
                    m_llsdHandlers.Remove(path);
                    return true;
                }
            }
            catch (KeyNotFoundException)
            {
                // This is an exception to prevent crashing because of invalid code
            }

            return false;
        }


        public string GetHTTP404(string host)
        {
            string file = Path.Combine(Util.configDir(), "http_404.html");
            if (!File.Exists(file))
                return getDefaultHTTP404(host);

            StreamReader sr = File.OpenText(file);
            string result = sr.ReadToEnd();
            sr.Close();
            return result;
        }

        public string GetHTTP500()
        {
            string file = Path.Combine(Util.configDir(), "http_500.html");
            if (!File.Exists(file))
                return getDefaultHTTP500();

            StreamReader sr = File.OpenText(file);
            string result = sr.ReadToEnd();
            sr.Close();
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
}
