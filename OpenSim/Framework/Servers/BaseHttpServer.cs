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
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using Nwc.XmlRpc;
using libsecondlife.StructuredData;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Servers
{
    public class BaseHttpServer
    {
        protected Thread m_workerThread;
        protected HttpListener m_httpListener;
        protected Dictionary<string, XmlRpcMethod> m_rpcHandlers = new Dictionary<string, XmlRpcMethod>();
        protected LLSDMethod m_llsdHandler = null;
        protected Dictionary<string, IRequestHandler> m_streamHandlers = new Dictionary<string, IRequestHandler>();
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

        public void AddStreamHandler(IRequestHandler handler)
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
            if (!m_rpcHandlers.ContainsKey(method))
            {
                m_rpcHandlers.Add(method, handler);
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

        public virtual void HandleRequest(Object stateinfo)
        {
            HttpListenerContext context = (HttpListenerContext) stateinfo;

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;


            response.KeepAlive = false;
            response.SendChunked = false;

            string path = request.RawUrl;
            string handlerKey = GetHandlerKey(request.HttpMethod, path);

            IRequestHandler requestHandler;

            if (TryGetStreamHandler(handlerKey, out requestHandler))
            {
                // Okay, so this is bad, but should be considered temporary until everything is IStreamHandler.
                byte[] buffer;
                if (requestHandler is IStreamedRequestHandler)
                {
                    IStreamedRequestHandler streamedRequestHandler = requestHandler as IStreamedRequestHandler;
                    buffer = streamedRequestHandler.Handle(path, request.InputStream);
                }
                else
                {
                    IStreamHandler streamHandler = (IStreamHandler) requestHandler;

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        streamHandler.Handle(path, request.InputStream, memoryStream);
                        memoryStream.Flush();
                        buffer = memoryStream.ToArray();
                    }
                }

                request.InputStream.Close();
                response.ContentType = requestHandler.ContentType;
                response.ContentLength64 = buffer.LongLength;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            else
            {
                switch (request.ContentType)
                {
                    //case "application/xml+llsd":
                        //HandleLLSDRequests(request, response);
                        //break;
                    case "text/xml":
                    case "application/xml":
                    default:
                        HandleXmlRpcRequests(request, response);
                        break;
                }
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
                xmlRprcRequest = (XmlRpcRequest) (new XmlRpcRequestDeserializer()).Deserialize(requestBody);
            }
            catch (XmlException e)
            {
                Hashtable keysvals = new Hashtable();
                responseString = String.Format("XmlException:\n{0}", e.Message);
                MainLog.Instance.Error("XML", responseString);
                string[] querystringkeys = request.QueryString.AllKeys;
                string[] rHeaders = request.Headers.AllKeys;


                foreach (string queryname in querystringkeys)
                {
                    keysvals.Add(queryname, request.QueryString[queryname]);
                    MainLog.Instance.Warn("HTTP", queryname + "=" + request.QueryString[queryname]);
                }
                foreach (string headername in rHeaders)
                {
                    MainLog.Instance.Warn("HEADER", headername + "=" + request.Headers[headername]);
                }
                if (keysvals.ContainsKey("show_login_form"))
                {
                    HandleHTTPRequest(keysvals, request, response);
                    return;
                }
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
                        Hashtable unknownMethodError = new Hashtable();
                        unknownMethodError["reason"] = "XmlRequest";
                        ;
                        unknownMethodError["message"] = "Unknown Rpc Request [" + methodName + "]";
                        unknownMethodError["login"] = "false";
                        xmlRpcResponse.Value = unknownMethodError;
                    }

                    responseString = XmlRpcResponseSerializer.Singleton.Serialize(xmlRpcResponse);
                }
                else
                {
                    System.Console.WriteLine("Handler not found for http request " + request.RawUrl);
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
                MainLog.Instance.Warn("HTTPD", "Error - " + ex.Message);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        private void HandleLLSDRequests(HttpListenerRequest request, HttpListenerResponse response)
        {
            Stream requestStream = request.InputStream;

            Encoding encoding = Encoding.UTF8;
            StreamReader reader = new StreamReader(requestStream, encoding);

            string requestBody = reader.ReadToEnd();
            reader.Close();
            requestStream.Close();

            LLSD llsdRequest = null;
            LLSD llsdResponse = null;

            try { llsdRequest = LLSDParser.DeserializeXml(requestBody); }
            catch (Exception ex) { MainLog.Instance.Warn("HTTPD", "Error - " + ex.Message); }

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
                MainLog.Instance.Warn("HTTPD", "Error - " + ex.Message);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        public void HandleHTTPRequest(Hashtable keysvals, HttpListenerRequest request, HttpListenerResponse response)
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

            if ((string) keysvals["show_login_form"] == "TRUE")
            {
                string responseString =
                    "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">";
                responseString = responseString + "<html xmlns=\"http://www.w3.org/1999/xhtml\">";
                responseString = responseString + "<head>";
                responseString = responseString +
                                 "<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />";
                responseString = responseString + "<meta http-equiv=\"cache-control\" content=\"no-cache\">";
                responseString = responseString + "<meta http-equiv=\"Pragma\" content=\"no-cache\">";
                responseString = responseString + "<title>Second Life Login</title>";
                responseString = responseString + "<body>";
                responseString = responseString + "<div id=\"login_box\">";
                // Linden Grid Form Post
                //responseString = responseString + "<form action=\"https://secure-web16.secondlife.com/app/login/go.php?show_login_form=True&show_grid=&show_start_location=\" method=\"POST\" id=\"login-form\">";
                responseString = responseString + "<form action=\"/\" method=\"GET\" id=\"login-form\">";

                responseString = responseString + "<div id=\"message\">";
                responseString = responseString + "</div>";
                responseString = responseString + "<fieldset id=\"firstname\">";
                responseString = responseString + "<legend>First Name:</legend>";
                responseString = responseString +
                                 "<input type=\"text\" id=\"firstname_input\" size=\"15\" maxlength=\"100\" name=\"username\" value=\"" +
                                 keysvals["username"] + "\" />";
                responseString = responseString + "</fieldset>";
                responseString = responseString + "<fieldset id=\"lastname\">";
                responseString = responseString + "<legend>Last Name:</legend>";
                responseString = responseString +
                                 "<input type=\"text\" size=\"15\" maxlength=\"100\" name=\"lastname\" value=\"" +
                                 keysvals["lastname"] + "\" />";
                responseString = responseString + "</fieldset>";
                responseString = responseString + "<fieldset id=\"password\">";
                responseString = responseString + "<legend>Password:</legend>";
                responseString = responseString + "<table cellspacing=\"0\" cellpadding=\"0\" border=\"0\">";
                responseString = responseString + "<tr>";
                responseString = responseString +
                                 "<td colspan=\"2\"><input type=\"password\" size=\"15\" maxlength=\"100\" name=\"password\" value=\"" +
                                 keysvals["password"] + "\" /></td>";
                responseString = responseString + "</tr>";
                responseString = responseString + "<tr>";
                responseString = responseString +
                                 "<td valign=\"middle\"><input type=\"checkbox\" name=\"remember_password\" id=\"remember_password\" value=\"" +
                                 keysvals["remember_password"] + "\" checked style=\"margin-left:0px;\"/></td>";
                responseString = responseString + "<td><label for=\"remember_password\">Remember password</label></td>";
                responseString = responseString + "</tr>";
                responseString = responseString + "</table>";
                responseString = responseString + "</fieldset>";
                responseString = responseString + "<input type=\"hidden\" name=\"show_login_form\" value=\"FALSE\" />";
                responseString = responseString + "<input type=\"hidden\" id=\"grid\" name=\"grid\" value=\"" +
                                 keysvals["grid"] + "\" />";
                responseString = responseString + "<div id=\"submitbtn\">";
                responseString = responseString + "<input class=\"input_over\" type=\"submit\" value=\"Connect\" />";
                responseString = responseString + "</div>";
                responseString = responseString +
                                 "<div id=\"connecting\" style=\"visibility:hidden\"><img src=\"/_img/sl_logo_rotate_black.gif\" align=\"absmiddle\"> Connecting...</div>";

                responseString = responseString + "<div id=\"helplinks\">";
                responseString = responseString +
                                 "<a href=\"http://www.secondlife.com/join/index.php\" target=\"_blank\">Create new account</a> | ";
                responseString = responseString +
                                 "<a href=\"http://www.secondlife.com/account/request.php\" target=\"_blank\">Forgot password?</a>";
                responseString = responseString + "</div>";

                responseString = responseString + "<div id=\"channelinfo\"> " + keysvals["channel"] + " | " +
                                 keysvals["version"] + "=" + keysvals["lang"] + "</div>";
                responseString = responseString + "</form>";
                responseString = responseString + "<script language=\"JavaScript\">";
                responseString = responseString + "document.getElementById('firstname_input').focus();";
                responseString = responseString + "</script>";
                responseString = responseString + "</div>";
                responseString = responseString + "</div>";
                responseString = responseString + "</body>";
                responseString = responseString + "</html>";
                response.AddHeader("Content-type", "text/html");

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
            } // show_login_form == "TRUE"
            else
            {
                // show_login_form is present but FALSE
                //
                // The idea here is that we're telling the client to log in immediately here using the following information
                // For my testing, I'm hard coding  the web_login_key temporarily.
                // Telling the client to go to the new improved SLURL for immediate logins

                // The fact that it says grid=Other is important

                // 

                response.StatusCode = 301;
                response.RedirectLocation = "secondlife:///app/login?first_name=" + keysvals["username"] + "&last_name=" +
                                            keysvals["lastname"] +
                                            "&location=home&grid=other&web_login_key=796f2b2a-0131-41e4-af12-00f60c24c458";

                response.OutputStream.Close();
            } // show_login_form == "FALSE"
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
                MainLog.Instance.Warn("HTTPD", "Error - " + e.Message);
            }
        }


        public void RemoveStreamHandler(string httpMethod, string path)
        {
            m_streamHandlers.Remove(GetHandlerKey(httpMethod, path));
        }
    }
}
