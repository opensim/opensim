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
using OpenSim.Framework.Sims;
using OpenSim.Framework.Console;

namespace OpenGridServices.GridServer
{
	public class GridHTTPServer {
		public Thread HTTPD;
		public HttpListener Listener;
	
		public GridHTTPServer() {
	 		MainConsole.Instance.WriteLine("Starting up HTTP Server");
			HTTPD = new Thread(new ThreadStart(StartHTTP));
			HTTPD.Start();
		}

		public void StartHTTP() {
			MainConsole.Instance.WriteLine("GridHttp.cs:StartHTTP() - Spawned main thread OK");
			Listener = new HttpListener();

			Listener.Prefixes.Add("http://+:8001/gridserver/");
			Listener.Start();

			HttpListenerContext context;
			while(true) {
				context = Listener.GetContext();
				ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
			}
		}

		static string ParseXMLRPC(string requestBody) {
			try{
			XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);
		
			Hashtable requestData = (Hashtable)request.Params[0];
			switch(request.MethodName) {
				case "get_sim_info":
					ulong req_handle=(ulong)Convert.ToInt64(requestData["region_handle"]);
					SimProfileBase TheSim = OpenGrid_Main.thegrid._regionmanager.GetProfileByHandle(req_handle);
					string RecvKey="";
					string caller=(string)requestData["caller"];
					switch(caller) {
						case "userserver":
							RecvKey=OpenGrid_Main.thegrid.UserRecvKey;
						break;
						case "assetserver":
							RecvKey=OpenGrid_Main.thegrid.AssetRecvKey;
						break;
					}
					if((TheSim!=null) && (string)requestData["authkey"]==RecvKey) {
						XmlRpcResponse SimInfoResp = new XmlRpcResponse();
						Hashtable SimInfoData = new Hashtable();
						SimInfoData["UUID"]=TheSim.UUID.ToString();
						SimInfoData["regionhandle"]=TheSim.regionhandle.ToString();
						SimInfoData["regionname"]=TheSim.regionname;
						SimInfoData["sim_ip"]=TheSim.sim_ip;
						SimInfoData["sim_port"]=TheSim.sim_port.ToString();
						SimInfoData["caps_url"]=TheSim.caps_url;
						SimInfoData["RegionLocX"]=TheSim.RegionLocX.ToString();
						SimInfoData["RegionLocY"]=TheSim.RegionLocY.ToString();
						SimInfoData["sendkey"]=TheSim.sendkey;
						SimInfoData["recvkey"]=TheSim.recvkey;
						SimInfoResp.Value=SimInfoData;
						return(Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(SimInfoResp),"utf-16","utf-8"));
					} else {
						XmlRpcResponse SimErrorResp = new XmlRpcResponse();
                                                Hashtable SimErrorData = new Hashtable();
						SimErrorData["error"]="sim not found";
						SimErrorResp.Value=SimErrorData;
                                                return(XmlRpcResponseSerializer.Singleton.Serialize(SimErrorResp));
					}
				break;
			}
			} catch(Exception e) {
				Console.WriteLine(e.ToString());
			}
			return "";
		}
		
		static string ParseREST(string requestBody, string requestURL) {
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
                        	
				case null:
					// must be REST or invalid crap, so pass to the REST parser
					responseString=ParseREST(request.Url.OriginalString,requestBody);
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
