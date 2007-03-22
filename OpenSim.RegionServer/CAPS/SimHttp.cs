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

namespace OpenSim.CAPS
{
    // Dummy HTTP server, does nothing useful for now

    public class SimCAPSHTTPServer
    {
        public Thread HTTPD;
        public HttpListener Listener;

        public SimCAPSHTTPServer()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Starting up HTTP Server");
            HTTPD = new Thread(new ThreadStart(StartHTTP));
            HTTPD.Start();
        }

        public void StartHTTP()
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("SimHttp.cs:StartHTTP() - Spawned main thread OK");
                Listener = new HttpListener();

                Listener.Prefixes.Add("http://+:" + OpenSimRoot.Instance.Cfg.IPListenPort + "/");
                Listener.Start();

                HttpListenerContext context;
                while (true)
                {
                    context = Listener.GetContext();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(e.Message);
            }
        }

        static string ParseXMLRPC(string requestBody)
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
                        if (OpenSimRoot.Instance.GridServers.GridServer.GetName() == "Remote")
                        {
                            ((RemoteGridBase)OpenSimRoot.Instance.GridServers.GridServer).agentcircuits.Add((uint)agent_data.circuitcode, agent_data);
                        }
                        return "<?xml version=\"1.0\"?><methodResponse><params /></methodResponse>";
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return "";
        }

        static string ParseREST(string requestBody, string requestURL)
        {
            return "";
        }

        static string ParseLLSDXML(string requestBody)
        {
            // dummy function for now - IMPLEMENT ME!
            return "";
        }

        static void HandleRequest(Object stateinfo)
        {
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

                case null:
                    // must be REST or invalid crap, so pass to the REST parser
                    responseString = ParseREST(request.Url.OriginalString, requestBody);
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
