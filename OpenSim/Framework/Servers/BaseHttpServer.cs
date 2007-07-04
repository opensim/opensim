/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nwc.XmlRpc;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Servers
{
    public class BaseHttpServer
    {
        protected Thread m_workerThread;
        protected HttpListener m_httpListener;
        protected Dictionary<string, RestMethodEntry> m_restHandlers = new Dictionary<string, RestMethodEntry>();
        protected Dictionary<string, XmlRpcMethod> m_rpcHandlers = new Dictionary<string, XmlRpcMethod>();
        protected Dictionary<string, IStreamHandler> m_streamHandlers = new Dictionary<string, IStreamHandler>();
        protected int m_port;
        protected bool firstcaps = true;

        public BaseHttpServer(int port)
        {
            m_port = port;
        }

        public void AddStreamHandler( string path, IStreamHandler handler)
        {
            string handlerKey = handler.HttpMethod + ":" + path;
            m_streamHandlers.Add(handlerKey, handler);
        }

        public bool AddRestHandler(string method, string path, RestMethod handler)
        {
            //Console.WriteLine("adding new REST handler for path " + path);
            string methodKey = String.Format("{0}: {1}", method, path);

            if (!this.m_restHandlers.ContainsKey(methodKey))
            {
                this.m_restHandlers.Add(methodKey, new RestMethodEntry(path, handler));
                return true;
            }

            //must already have a handler for that path so return false
            return false;
        }

        public bool RemoveRestHandler(string method, string path)
        {
            string methodKey = String.Format("{0}: {1}", method, path);
            if (this.m_restHandlers.ContainsKey(methodKey))
            {
                this.m_restHandlers.Remove(methodKey);
                return true;
            }
            return false;
        }

        public bool AddXmlRPCHandler(string method, XmlRpcMethod handler)
        {
            if (!this.m_rpcHandlers.ContainsKey(method))
            {
                this.m_rpcHandlers.Add(method, handler);
                return true;
            }

            //must already have a handler for that path so return false
            return false;
        }

        protected virtual string ProcessXMLRPCMethod(string methodName, XmlRpcRequest request)
        {
            XmlRpcResponse response;

            XmlRpcMethod method;
            if (this.m_rpcHandlers.TryGetValue(methodName, out method))
            {
                response = method(request);
            }
            else
            {
                response = new XmlRpcResponse();
                Hashtable unknownMethodError = new Hashtable();
                unknownMethodError["reason"] = "XmlRequest"; ;
                unknownMethodError["message"] = "Unknown Rpc request";
                unknownMethodError["login"] = "false";
                response.Value = unknownMethodError;
            }

            return XmlRpcResponseSerializer.Singleton.Serialize(response);
        }

        protected virtual string ParseREST(string request, string path, string method)
        {
            string response;

            string requestKey = String.Format("{0}: {1}", method, path);

            string bestMatch = String.Empty;
            foreach (string currentKey in m_restHandlers.Keys)
            {
                if (requestKey.StartsWith(currentKey))
                {
                    if (currentKey.Length > bestMatch.Length)
                    {
                        bestMatch = currentKey;
                    }
                }
            }

            RestMethodEntry restMethodEntry;
            if (m_restHandlers.TryGetValue(bestMatch, out restMethodEntry))
            {
                RestMethod restMethod = restMethodEntry.RestMethod;

                string param = path.Substring(restMethodEntry.Path.Length);
                response = restMethod(request, path, param);

            }
            else
            {
                response = String.Empty;
            }

            return response;
        }


        protected virtual string ParseXMLRPC(string requestBody)
        {
            string responseString = String.Empty;

            XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);

            string methodName = request.MethodName;

            responseString = ProcessXMLRPCMethod(methodName, request);

            return responseString;
        }

        public virtual void HandleRequest(Object stateinfo)
        {
            HttpListenerContext context = (HttpListenerContext)stateinfo;

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            response.KeepAlive = false;
            response.SendChunked = false;

            string path = request.RawUrl;
            string handlerKey = request.HttpMethod + ":" + path;

            IStreamHandler streamHandler;

            if (TryGetStreamHandler( handlerKey, out streamHandler))
            {
                byte[] buffer = streamHandler.Handle(path, request.InputStream );
                request.InputStream.Close();

                response.ContentType = streamHandler.ContentType;
                response.ContentLength64 = buffer.LongLength;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            else
            {
                HandleLegacyRequests(request, response);
            }
        }

        private bool TryGetStreamHandler(string handlerKey, out IStreamHandler streamHandler)
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

        private void HandleLegacyRequests(HttpListenerRequest request, HttpListenerResponse response)
        {
            Stream body = request.InputStream;

            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(body, encoding);

            string requestBody = reader.ReadToEnd();
            body.Close();
            reader.Close();

            //Console.WriteLine(request.HttpMethod + " " + request.RawUrl + " Http/" + request.ProtocolVersion.ToString() + " content type: " + request.ContentType);
            //Console.WriteLine(requestBody);

            string responseString = "";
            // Console.WriteLine("new request " + request.ContentType +" at "+ request.RawUrl);
            switch (request.ContentType)
            {
                case "text/xml":
                    // must be XML-RPC, so pass to the XML-RPC parser

                    responseString = ParseXMLRPC(requestBody);
                    responseString = Regex.Replace(responseString, "utf-16", "utf-8");

                    response.AddHeader("Content-type", "text/xml");
                    break;

                case "application/xml":
                case "application/octet-stream":
                    // probably LLSD we hope, otherwise it should be ignored by the parser
                    // responseString = ParseLLSDXML(requestBody);
                    responseString = ParseREST(requestBody, request.RawUrl, request.HttpMethod);
                    response.AddHeader("Content-type", "application/xml");
                    break;

                case "application/x-www-form-urlencoded":
                    // a form data POST so send to the REST parser
                    responseString = ParseREST(requestBody, request.RawUrl, request.HttpMethod);
                    response.AddHeader("Content-type", "text/html");
                    break;

                case null:
                    // must be REST or invalid crap, so pass to the REST parser
                    responseString = ParseREST(requestBody, request.RawUrl, request.HttpMethod);
                    response.AddHeader("Content-type", "text/html");
                    break;

            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            Stream output = response.OutputStream;
            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;



            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        public void Start()
        {
            MainLog.Instance.WriteLine(LogPriority.LOW, "BaseHttpServer.cs: Starting up HTTP Server");

            m_workerThread = new Thread(new ThreadStart(StartHTTP));
            m_workerThread.IsBackground = true;
            m_workerThread.Start();
        }

        private void StartHTTP()
        {
            try
            {
                MainLog.Instance.WriteLine(LogPriority.LOW, "BaseHttpServer.cs: StartHTTP() - Spawned main thread OK");
                m_httpListener = new HttpListener();

                m_httpListener.Prefixes.Add("http://+:" + m_port + "/");
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
                MainLog.Instance.WriteLine(LogPriority.MEDIUM, e.Message);
            }
        }

    }
}
