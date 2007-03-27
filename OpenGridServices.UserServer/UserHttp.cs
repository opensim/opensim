/*
Copyright (c) OpenGrid project, http://osgrid.org/


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
using OpenSim.Framework.User;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Console;

namespace OpenGridServices.UserServer
{
	public class UserHTTPServer {
		public Thread HTTPD;
		public HttpListener Listener;
	
		public UserHTTPServer() {
	 		MainConsole.Instance.WriteLine("Starting up HTTP Server");
			HTTPD = new Thread(new ThreadStart(StartHTTP));
			HTTPD.Start();
		}

		public void StartHTTP() {
			MainConsole.Instance.WriteLine("UserHttp.cs:StartHTTP() - Spawned main thread OK");
			Listener = new HttpListener();

			Listener.Prefixes.Add("http://+:8002/userserver/");
			Listener.Prefixes.Add("http://+:8002/usersessions/");
			Listener.Start();

			HttpListenerContext context;
			while(true) {
				context = Listener.GetContext();
				ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
			}
		}

		static string ParseXMLRPC(string requestBody) {
            return OpenUser_Main.userserver._profilemanager.ParseXMLRPC(requestBody);
		}
		
		static string ParseREST(HttpListenerRequest www_req) {
			Console.WriteLine("INCOMING REST - " + www_req.RawUrl);
	
			char[] splitter  = {'/'};
			string[] rest_params = www_req.RawUrl.Split(splitter);
			string req_type = rest_params[1];	// First part of the URL is the type of request - usersessions/userprofiles/inventory/blabla
			switch(req_type) {
				case "usersessions":
					LLUUID sessionid = new LLUUID(rest_params[2]);	// get usersessions/sessionid
					if(www_req.HttpMethod=="DELETE") {
			                        foreach (libsecondlife.LLUUID UUID in OpenUser_Main.userserver._profilemanager.UserProfiles.Keys) {
							if(OpenUser_Main.userserver._profilemanager.UserProfiles[UUID].CurrentSessionID==sessionid) {
								OpenUser_Main.userserver._profilemanager.UserProfiles[UUID].CurrentSessionID=null;
								OpenUser_Main.userserver._profilemanager.UserProfiles[UUID].CurrentSecureSessionID=null;
								OpenUser_Main.userserver._profilemanager.UserProfiles[UUID].Circuits.Clear();
							}
			                        }

					}
					return "OK";
			}

			return "";
		}


		static void HandleRequest(Object  stateinfo) {
			HttpListenerContext context=(HttpListenerContext)stateinfo;
		
                	HttpListenerRequest request = context.Request;
                	HttpListenerResponse response = context.Response;

			response.KeepAlive=false;
			response.SendChunked=false;

			System.IO.Stream body = request.InputStream;
			System.Text.Encoding encoding = System.Text.Encoding.UTF8;
			System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);
   
	    		string requestBody = reader.ReadToEnd();
			body.Close();
    			reader.Close();

                        string responseString="";
			switch(request.ContentType) {
                                case "text/xml":
                                	// must be XML-RPC, so pass to the XML-RPC parser
					
					responseString=ParseXMLRPC(requestBody);
					response.AddHeader("Content-type","text/xml");	
				break;
                        	
				case "text/plaintext":
					responseString=ParseREST(request);
					response.AddHeader("Content-type","text/plaintext");
				break;
			}
	
	
	                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        	        System.IO.Stream output = response.OutputStream;
    	        	response.SendChunked=false;
			response.ContentLength64=buffer.Length;
			output.Write(buffer,0,buffer.Length);
        	        output.Close();
		}
	}


}
