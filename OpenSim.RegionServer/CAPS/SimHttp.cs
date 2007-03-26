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
        private string AdminPage;
        private string NewAccountForm;
        private string LoginForm;
        private string passWord = "Admin";

        public SimCAPSHTTPServer()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Starting up HTTP Server");
            HTTPD = new Thread(new ThreadStart(StartHTTP));
            HTTPD.Start();
            LoadAdminPage();
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

        private string ParseXMLRPC(string requestBody)
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

        private string ParseREST(string requestBody, string requestURL, string requestMethod)
        {
            string responseString = "";
            switch (requestURL)
            {
                case "/Admin/Accounts":
                    if (requestMethod == "GET")
                    {
                        responseString = "<p> Account management </p>";
                        responseString += "<br> ";
                        responseString += "<p> Create New Account </p>";
                        responseString += NewAccountForm;
                    }
                    break;
                case "/Admin/Clients":
                    if (requestMethod == "GET")
                    {
                        responseString = " <p> Listing connected Clients </p>" ;
                        OpenSim.world.Avatar TempAv;
                        foreach (libsecondlife.LLUUID UUID in OpenSimRoot.Instance.LocalWorld.Entities.Keys)
                        {
                            if (OpenSimRoot.Instance.LocalWorld.Entities[UUID].ToString() == "OpenSim.world.Avatar")
                            {
                                TempAv = (OpenSim.world.Avatar)OpenSimRoot.Instance.LocalWorld.Entities[UUID];
                                responseString += "<p>";
                                responseString += String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}", TempAv.firstname, TempAv.lastname, UUID, TempAv.ControllingClient.SessionID, TempAv.ControllingClient.CircuitCode, TempAv.ControllingClient.userEP.ToString());
                                responseString += "</p>";
                            }
                        }
                    }
                    break;
                case "/Admin/NewAccount": 
                    if (requestMethod == "POST")
                    {
                        string[] comp = new string[10];
                        string[] passw = new string[3];
                        string delimStr = "&";
                        char[] delimiter = delimStr.ToCharArray();
                        string delimStr2 = "=";
                        char[] delimiter2 = delimStr2.ToCharArray();

                        //Console.WriteLine(requestBody);
                        comp = requestBody.Split(delimiter);
                        passw = comp[3].Split(delimiter2);
                        if (passw[1] == passWord)
                        {
                            responseString = "<p> New Account created </p>";
                        }
                        else
                        {
                            responseString = "<p> Admin password is incorrect, please login with the correct password</p>";
                            responseString += "<br><br>" + LoginForm;
                        }
                       
                        
                    }
                    break;
                case "/Admin/Login":
                    if (requestMethod == "POST")
                    {
                        Console.WriteLine(requestBody);
                        if (requestBody == passWord)
                        {
                            responseString = "<p> Login Successful </p>";
                        }
                        else
                        {
                            responseString = "<p> PassWord Error </p>";
                            responseString += "<p> Please Login with the correct password </p>";
                            responseString += "<br><br> " + LoginForm;
                        }
                    }
                    break;
                case "/Admin/Welcome":
                    if (requestMethod == "GET")
                    {
                        responseString = "Welcome to the OpenSim Admin Page";
                        responseString += "<br><br><br> " + LoginForm;

                    }
                    break;
            }

            return responseString;
        }

        private string ParseLLSDXML(string requestBody)
        {
            // dummy function for now - IMPLEMENT ME!
            return "";
        }

        public void HandleRequest(Object stateinfo)
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
                    if ((request.HttpMethod == "GET") && (request.RawUrl == "/Admin"))
                    {
                        responseString = AdminPage;
                        response.AddHeader("Content-type", "text/html");
                    }
                    else
                    {
                        // must be REST or invalid crap, so pass to the REST parser
                        responseString = ParseREST(requestBody, request.RawUrl, request.HttpMethod);
                        response.AddHeader("Content-type", "text/html");
                    }
                    break;
               
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            System.IO.Stream output = response.OutputStream;
            response.SendChunked = false;
            response.ContentLength64 = buffer.Length;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        private void LoadAdminPage()
        {
            try
            {
                StreamReader SR;
                string lines;
                AdminPage = "";
                NewAccountForm = "";
                LoginForm = "";
                SR = File.OpenText("testadmin.htm");

                while (!SR.EndOfStream)
                {
                    lines = SR.ReadLine();
                    AdminPage += lines + "\n";

                }
                SR.Close();

                SR = File.OpenText("newaccountform.htm");

                while (!SR.EndOfStream)
                {
                    lines = SR.ReadLine();
                    NewAccountForm += lines + "\n";

                }
                SR.Close();

                SR = File.OpenText("login.htm");

                while (!SR.EndOfStream)
                {
                    lines = SR.ReadLine();
                    LoginForm += lines + "\n";

                }
                SR.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }
    }


}
