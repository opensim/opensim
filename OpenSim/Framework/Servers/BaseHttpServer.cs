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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using libsecondlife.StructuredData;
using log4net;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Servers
{
    public class BaseHttpServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Thread m_workerThread;
        protected HttpListener m_httpListener;
        protected Dictionary<string, XmlRpcMethod> m_rpcHandlers = new Dictionary<string, XmlRpcMethod>();
        protected LLSDMethod m_llsdHandler = null;
        protected Dictionary<string, IRequestHandler> m_streamHandlers = new Dictionary<string, IRequestHandler>();
        protected Dictionary<string, GenericHTTPMethod> m_HTTPHandlers = new Dictionary<string, GenericHTTPMethod>();

        protected uint m_port;
        protected bool m_ssl = false;
        protected bool m_firstcaps = true;

        public uint Port
        {
            get { return m_port; }
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

        /// <summary>
        /// Add a stream handler to the http server.  If the handler already exists, then nothing happens.
        /// </summary>
        /// <param name="handler"></param>
        public void AddStreamHandler(IRequestHandler handler)
        {
            string httpMethod = handler.HttpMethod;
            string path = handler.Path;

            string handlerKey = GetHandlerKey(httpMethod, path);

            if (!m_streamHandlers.ContainsKey(handlerKey))
            {
                //m_log.DebugFormat("[BASE HTTP SERVER]: Adding handler key {0}", handlerKey);
                m_streamHandlers.Add(handlerKey, handler);
            }
        }

        private static string GetHandlerKey(string httpMethod, string path)
        {
            return httpMethod + ":" + path;
        }

        public bool AddXmlRPCHandler(string method, XmlRpcMethod handler)
        {
            if (!m_rpcHandlers.ContainsKey(method))
            {
                m_rpcHandlers.Add(method, handler);
                return true;
            }

            //must already have a handler for that path so return false
            return false;
        }

        public bool AddHTTPHandler(string method, GenericHTTPMethod handler)
        {
            if (!m_HTTPHandlers.ContainsKey(method))
            {
                m_HTTPHandlers.Add(method, handler);
                return true;
            }

            //must already have a handler for that path so return false
            return false;
        }

        public bool SetLLSDHandler(LLSDMethod handler)
        {
            m_llsdHandler = handler;
            return true;
        }

        /// <summary>
        /// Handle an individual http request.  This method is given to a worker in the thread pool.
        /// </summary>
        /// <param name="stateinfo"></param>
        public virtual void HandleRequest(Object stateinfo)
        {
            // If we don't catch the exception here it will just disappear into the thread pool and we'll be none the wiser
            try
            {
                HttpListenerContext context = (HttpListenerContext) stateinfo;

                OSHttpRequest request = new OSHttpRequest(context.Request);
                OSHttpResponse response = new OSHttpResponse(context.Response);

                response.KeepAlive = false;
                response.SendChunked = false;

                string path = request.RawUrl;
                string handlerKey = GetHandlerKey(request.HttpMethod, path);

                //m_log.DebugFormat("[BASE HTTP SERVER]: Handling {0} request for {1}", request.HttpMethod, path);

                IRequestHandler requestHandler;

                if (TryGetStreamHandler(handlerKey, out requestHandler))
                {
                    // Okay, so this is bad, but should be considered temporary until everything is IStreamHandler.
                    byte[] buffer;
                    if (requestHandler is IStreamedRequestHandler)
                    {
                        IStreamedRequestHandler streamedRequestHandler = requestHandler as IStreamedRequestHandler;

                        buffer = streamedRequestHandler.Handle(path, request.InputStream, request, response);
                    }
                    else
                    {
                        IStreamHandler streamHandler = (IStreamHandler) requestHandler;

                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            streamHandler.Handle(path, request.InputStream, memoryStream, request, response);
                            memoryStream.Flush();
                            buffer = memoryStream.ToArray();
                        }
                    }

                    request.InputStream.Close();
                    if (!response.IsContentTypeSet) response.ContentType = requestHandler.ContentType;
                    response.ContentLength64 = buffer.LongLength;

                    try
                    {
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                    catch (HttpListenerException)
                    {
                        m_log.WarnFormat("[BASE HTTP SERVER]: HTTP request abnormally terminated.");
                    }
                }
                else
                {
                    switch (request.ContentType)
                    {
                        case null:
                        case "text/html":
                            HandleHTTPRequest(request, response);
                            break;
                        case "application/xml+llsd":
                            HandleLLSDRequests(request, response);
                            break;
                        case "text/xml":
                        case "application/xml":
                        default:
                            HandleXmlRpcRequests(request, response);
                            break;
                    }
                }
            }
            catch (SocketException)
            {
                // At least on linux, it appears that if the client makes a request without requiring the response,
                // an unconnected socket exception is thrown when we close the response output stream.  There's no
                // obvious way to tell if the client didn't require the response, so instead we'll catch and ignore
                // the exception instead.
                //
                // An alternative may be to turn off all response write exceptions on the HttpListener, but let's go
                // with the minimum first
            }
            catch (Exception e)
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
                    response.OutputStream.Close();
                }
                catch (SocketException)
                {
                    // This has to be here to prevent a Linux/Mono crash
                }
            }
        }

        private void HandleLLSDRequests(OSHttpRequest request, OSHttpResponse response)
        {
            Stream requestStream = request.InputStream;

            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(requestStream, encoding);

            string requestBody = reader.ReadToEnd();
            reader.Close();
            requestStream.Close();

            LLSD llsdRequest = null;
            LLSD llsdResponse = null;

            try
            {
                llsdRequest = LLSDParser.DeserializeXml(requestBody);
            }
            catch (Exception ex)
            {
                m_log.Warn("[HTTPD]: Error - " + ex.Message);
            }

            if (llsdRequest != null && m_llsdHandler != null)
            {
                llsdResponse = m_llsdHandler(llsdRequest);
            }
            else
            {
                LLSDMap map = new LLSDMap();
                map["reason"] = LLSD.FromString("LLSDRequest");
                map["message"] = LLSD.FromString("No handler registered for LLSD Requests");
                map["login"] = LLSD.FromString("false");
                llsdResponse = map;
            }

            response.ContentType = "application/xml+llsd";

            byte[] buffer = LLSDParser.SerializeXmlBytes(llsdResponse);

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
                response.OutputStream.Close();
            }
        }

        public void HandleHTTPRequest(OSHttpRequest request, OSHttpResponse response)
        {
            switch (request.HttpMethod)
            {
                case "OPTIONS":
                    response.StatusCode = 200;
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
            reader.Close();
            requestStream.Close();

            Hashtable keysvals = new Hashtable();
            Hashtable headervals = new Hashtable();
            string host = String.Empty;

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
                    Hashtable responsedata = requestprocessor(keysvals);
                    DoHTTPGruntWork(responsedata,response);

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
                //m_log.Warn("[HTTP]: No Method specified");
                SendHTML404(response, host);
            }
        }

        private static void DoHTTPGruntWork(Hashtable responsedata, OSHttpResponse response)
        {
            int responsecode = (int)responsedata["int_response_code"];
            string responseString = (string)responsedata["str_response_string"];
            string contentType = (string)responsedata["content_type"];

            //Even though only one other part of the entire code uses HTTPHandlers, we shouldn't expect this
            //and should check for NullReferenceExceptions

            if (string.IsNullOrEmpty(contentType))
            {
                contentType = "text/html";
            }

            // We're forgoing the usual error status codes here because the client
            // ignores anything but 200 and 301

            response.StatusCode = 200;

            if (responsecode == 301)
            {
                response.RedirectLocation = (string)responsedata["str_redirect_location"];
                response.StatusCode = responsecode;
            }

            response.AddHeader("Content-type", contentType);

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
                response.OutputStream.Close();
            }
        }

        public void SendHTML404(OSHttpResponse response, string host)
        {
            // I know this statuscode is dumb, but the client doesn't respond to 404s and 500s
            response.StatusCode = 200;
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
                response.OutputStream.Close();
            }
        }

        public void SendHTML500(OSHttpResponse response)
        {
            // I know this statuscode is dumb, but the client doesn't respond to 404s and 500s
            response.StatusCode = 200;
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
                response.OutputStream.Close();
            }
        }

        public void Start()
        {
            m_log.Info("[HTTPD]: Starting up HTTP Server");

            m_workerThread = new Thread(new ThreadStart(StartHTTP));
            m_workerThread.Name = "HttpThread";
            m_workerThread.IsBackground = true;
            m_workerThread.Start();
            ThreadTracker.Add(m_workerThread);
        }

        private void StartHTTP()
        {
            try
            {
                m_log.Info("[HTTPD]: Spawned main thread OK");
                m_httpListener = new HttpListener();

                if (!m_ssl)
                {
                    m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
                }
                else
                {
                    m_httpListener.Prefixes.Add("https://+:" + m_port + "/");
                }
                m_httpListener.Start();

                HttpListenerContext context;
                while (true)
                {
                    context = m_httpListener.GetContext();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
                }
            }
            catch (Exception e)
            {
                m_log.Warn("[HTTPD]: Error - " + e.Message);
                m_log.Warn("Tip: Do you have permission to listen on port " + m_port + "?");
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
            m_HTTPHandlers.Remove(GetHandlerKey(httpMethod, path));
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
}
