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
using System.Xml;

namespace OpenSim.Framework.Servers
{
    public class BaseHttpServer
    {
        protected Thread m_workerThread;
        protected HttpListener m_httpListener;
        protected Dictionary<string, XmlRpcMethod> m_rpcHandlers = new Dictionary<string, XmlRpcMethod>();
        protected Dictionary<string, IStreamHandler> m_streamHandlers = new Dictionary<string, IStreamHandler>();
        protected int m_port;
        protected bool m_firstcaps = true;

        public int Port
        {
            get { return m_port; } 
        }

        public BaseHttpServer(int port)
        {
            m_port = port;
        }

        public void AddStreamHandler( IStreamHandler handler)
        {
            string httpMethod = handler.HttpMethod;
            string path = handler.Path;
            
            string handlerKey = GetHandlerKey(httpMethod, path);
            m_streamHandlers.Add(handlerKey, handler);
        }

        private static string GetHandlerKey(string httpMethod, string path)
        {
            return httpMethod + ":" + path;
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


        public virtual void HandleRequest(Object stateinfo)
        {
            HttpListenerContext context = (HttpListenerContext)stateinfo;

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            response.KeepAlive = false;
            response.SendChunked = false;

            string path = request.RawUrl;
            string handlerKey = GetHandlerKey( request.HttpMethod, path );

            IStreamHandler streamHandler;

            if (TryGetStreamHandler( handlerKey, out streamHandler))
            {
                byte[] buffer = streamHandler.Handle(path, request.InputStream);
                request.InputStream.Close();

                response.ContentType = streamHandler.ContentType;
                response.ContentLength64 = buffer.LongLength;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            else
            {
                HandleXmlRpcRequests(request, response);
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

        private void HandleXmlRpcRequests(HttpListenerRequest request, HttpListenerResponse response)
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
                xmlRprcRequest = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);
            }
            catch ( XmlException e )
            {            
                responseString = String.Format( "XmlException:\n{0}",e.Message );
            }

            if (xmlRprcRequest != null)
            {
                string methodName = xmlRprcRequest.MethodName;

                XmlRpcResponse xmlRpcResponse;

                XmlRpcMethod method;
                if (this.m_rpcHandlers.TryGetValue(methodName, out method))
                {
                    xmlRpcResponse = method(xmlRprcRequest);
                }
                else
                {
                    xmlRpcResponse = new XmlRpcResponse();
                    Hashtable unknownMethodError = new Hashtable();
                    unknownMethodError["reason"] = "XmlRequest"; ;
                    unknownMethodError["message"] = "Unknown Rpc Request [" + methodName + "]";
                    unknownMethodError["login"] = "false";
                    xmlRpcResponse.Value = unknownMethodError;
                }

                responseString = XmlRpcResponseSerializer.Singleton.Serialize(xmlRpcResponse);
            }

            response.AddHeader("Content-type", "text/xml");

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
                MainLog.Instance.Warn("HTTPD", "Error - " + ex.Message);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        public void Start()
        {
            MainLog.Instance.Verbose("HTTPD", "Starting up HTTP Server");

            m_workerThread = new Thread(new ThreadStart(StartHTTP));
            m_workerThread.IsBackground = true;
            m_workerThread.Start();
        }

        private void StartHTTP()
        {
            try
            {
                MainLog.Instance.Verbose("HTTPD", "Spawned main thread OK");
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
                MainLog.Instance.Warn("HTTPD", "Error - " + e.Message);
            }
        }


        public void RemoveStreamHandler(string httpMethod, string path)
        {
            m_streamHandlers.Remove(GetHandlerKey(httpMethod, path));
        }
    }
}
