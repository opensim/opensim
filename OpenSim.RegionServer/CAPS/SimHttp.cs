/*
Copyright (c) OpenSimCAPS project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Text;
using Nwc.XmlRpc;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Servers;

namespace OpenSim.CAPS
{
    // Dummy HTTP server, does nothing useful for now

    public class SimCAPSHTTPServer : BaseHttpServer
    {
        private Thread m_workerThread;
        private HttpListener m_httpListener;
        private Dictionary<string, IRestHandler> m_restHandlers = new Dictionary<string, IRestHandler>();
        private IGridServer m_gridServer;
        private int m_port;

        public SimCAPSHTTPServer(IGridServer gridServer, int port)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Starting up HTTP Server");
            m_workerThread = new Thread(new ThreadStart(StartHTTP));
            m_workerThread.Start();
            m_gridServer = gridServer;
            m_port = port;
        }

        public void StartHTTP()
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("SimHttp.cs:StartHTTP() - Spawned main thread OK");
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(e.Message);
            }
        }

        public bool AddRestHandler(string path, IRestHandler handler)
        {
            if (!this.m_restHandlers.ContainsKey(path))
            {
                this.m_restHandlers.Add(path, handler);
                return true;
            }

            //must already have a handler for that path so return false
            return false;
        }
        protected virtual string ParseXMLRPC(string requestBody)
        {
            try
            {
                XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);

                Hashtable requestData = (Hashtable)request.Params[0];
                switch (request.MethodName)
                {
                    case "expect_user":
                        AgentCircuitData agent_data = new AgentCircuitData();
                        agent_data.SessionID = new LLUUID((string)requestData["session_id"]);
                        agent_data.SecureSessionID = new LLUUID((string)requestData["secure_session_id"]);
                        agent_data.firstname = (string)requestData["firstname"];
                        agent_data.lastname = (string)requestData["lastname"];
                        agent_data.AgentID = new LLUUID((string)requestData["agent_id"]);
                        agent_data.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
                        if (m_gridServer.GetName() == "Remote")
                        {
                            
                            ((RemoteGridBase)m_gridServer).agentcircuits.Add((uint)agent_data.circuitcode, agent_data);
                        }
                        return "<?xml version=\"1.0\"?><methodResponse><params /></methodResponse>";
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return "";
        }

        protected virtual string ParseREST(string requestBody, string requestURL, string requestMethod)
        {
            string[] path;
            string pathDelimStr = "/";
            char[] pathDelimiter = pathDelimStr.ToCharArray();
            path = requestURL.Split(pathDelimiter);

            string responseString = "";
            
            //path[0] should be empty so we are interested in path[1]
            if (path.Length > 1)
            {
                if ((path[1] != "") && (this.m_restHandlers.ContainsKey(path[1])))
                {
                    responseString = this.m_restHandlers[path[1]].HandleREST(requestBody, requestURL, requestMethod);
                }
            }
           
            return responseString;
        }

        protected virtual string ParseLLSDXML(string requestBody)
        {
            // dummy function for now - IMPLEMENT ME!
            return "";
        }

        public virtual void HandleRequest(Object stateinfo)
        {
            // Console.WriteLine("new http incoming");
            HttpListenerContext context = (HttpListenerContext)stateinfo;

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            response.KeepAlive = false;
            response.SendChunked = false;

            System.IO.Stream body = request.InputStream;
            System.Text.Encoding encoding = System.Text.Encoding.UTF8;
            System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);

            string requestBody = reader.ReadToEnd();
            body.Close();
            reader.Close();

            //Console.WriteLine(request.HttpMethod + " " + request.RawUrl + " Http/" + request.ProtocolVersion.ToString() + " content type: " + request.ContentType);
            //Console.WriteLine(requestBody);

            string responseString = "";
            switch (request.ContentType)
            {
                case "text/xml":
                    // must be XML-RPC, so pass to the XML-RPC parser

                    responseString = ParseXMLRPC(requestBody);
                    response.AddHeader("Content-type", "text/xml");
                    break;

                case "application/xml":
                    // probably LLSD we hope, otherwise it should be ignored by the parser
                    responseString = ParseLLSDXML(requestBody);
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

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            System.IO.Stream output = response.OutputStream;
            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }
    }


}
