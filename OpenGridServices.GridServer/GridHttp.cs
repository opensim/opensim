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
using System.Xml;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Console;
using OpenSim.Servers;

namespace OpenGridServices.GridServer
{
	public class GridHTTPServer : BaseHttpServer {
		public Thread HTTPD;
		public HttpListener Listener;
	
		public GridHTTPServer() : base( 8001 ) {
		    Start();
		}

	    public void Start()
	    {
	        MainConsole.Instance.WriteLine("Starting up HTTP Server");
	        HTTPD = new Thread(new ThreadStart(StartHTTP));
	        HTTPD.Start();
	    }

	    public void StartHTTP() {
			MainConsole.Instance.WriteLine("GridHttp.cs:StartHTTP() - Spawned main thread OK");
			Listener = new HttpListener();

			Listener.Prefixes.Add("http://+:8001/");			
			Listener.Prefixes.Add("http://+:8001/sims/");
			Listener.Prefixes.Add("http://+:8001/gods/");
			Listener.Prefixes.Add("http://+:8001/highestuuid/");
			Listener.Prefixes.Add("http://+:8001/uuidblocks/");
			Listener.Start();
			
			MainConsole.Instance.WriteLine("GridHttp.cs:StartHTTP() - Successfully bound to port 8001");

			HttpListenerContext context;
			while(true) {
				context = Listener.GetContext();
				ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
			}
		}

		static string ParseXMLRPC(string requestBody, string referrer) {
			try{
				XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);
		
				Hashtable requestData = (Hashtable)request.Params[0];
				switch(request.MethodName) {
					case "simulator_login":
					
						/*if(!((string)requestData["authkey"]==OpenGrid_Main.thegrid.SimRecvKey)) {
							XmlRpcResponse ErrorResp = new XmlRpcResponse();
							Hashtable ErrorRespData = new Hashtable();
							ErrorRespData["error"]="invalid key";
							ErrorResp.Value=ErrorRespData;
							return(Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(ErrorResp),"utf-16","utf-8"));
						}*/
						SimProfileBase TheSim = null;

						if(requestData.ContainsKey("UUID")) {						
							TheSim = OpenGrid_Main.thegrid._regionmanager.GetProfileByLLUUID(new LLUUID((string)requestData["UUID"]));
						} else if (requestData.ContainsKey("region_handle")){
							TheSim = OpenGrid_Main.thegrid._regionmanager.GetProfileByHandle((ulong)Convert.ToUInt64(requestData["region_handle"]));
						}
						
						if(TheSim==null) {
							XmlRpcResponse ErrorResp = new XmlRpcResponse();
							Hashtable ErrorRespData = new Hashtable();
							ErrorRespData["error"]="sim not found";
							ErrorResp.Value=ErrorRespData;
							return(Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(ErrorResp),"utf-16","utf-8"));
						}

						XmlRpcResponse SimLoginResp = new XmlRpcResponse();
						Hashtable SimLoginRespData = new Hashtable();
						
						ArrayList SimNeighboursData = new ArrayList();
					
						SimProfileBase neighbour;
						Hashtable NeighbourBlock;
						for(int x=-1; x<2; x++) for(int y=-1; y<2; y++) {
							if(OpenGrid_Main.thegrid._regionmanager.GetProfileByHandle(Helpers.UIntsToLong((uint)((TheSim.RegionLocX+x)*256), (uint)(TheSim.RegionLocY+y)*256))!=null) {
								neighbour=OpenGrid_Main.thegrid._regionmanager.GetProfileByHandle(Helpers.UIntsToLong((uint)((TheSim.RegionLocX+x)*256), (uint)(TheSim.RegionLocY+y)*256));
								NeighbourBlock = new Hashtable();
								NeighbourBlock["sim_ip"] = neighbour.sim_ip;
								NeighbourBlock["sim_port"] = neighbour.sim_port.ToString();
								NeighbourBlock["region_locx"] = neighbour.RegionLocX.ToString();
								NeighbourBlock["region_locy"] = neighbour.RegionLocY.ToString();
								NeighbourBlock["UUID"] = neighbour.UUID.ToString();
								SimNeighboursData.Add(NeighbourBlock);
							}
						}

						SimLoginRespData["UUID"]=TheSim.UUID;	
						SimLoginRespData["region_locx"]=TheSim.RegionLocX.ToString();
						SimLoginRespData["region_locy"]=TheSim.RegionLocY.ToString();
						SimLoginRespData["regionname"]=TheSim.regionname;
						SimLoginRespData["estate_id"]="1";
						SimLoginRespData["neighbours"]=SimNeighboursData;
						SimLoginRespData["asset_url"]=OpenGrid_Main.thegrid.DefaultAssetServer;
						SimLoginRespData["asset_sendkey"]=OpenGrid_Main.thegrid.AssetSendKey;
						SimLoginRespData["asset_recvkey"]=OpenGrid_Main.thegrid.AssetRecvKey;
						SimLoginRespData["user_url"]=OpenGrid_Main.thegrid.DefaultUserServer;
						SimLoginRespData["user_sendkey"]=OpenGrid_Main.thegrid.UserSendKey;
						SimLoginRespData["user_recvkey"]=OpenGrid_Main.thegrid.UserRecvKey;
						SimLoginRespData["authkey"]=OpenGrid_Main.thegrid.SimSendKey;
						SimLoginResp.Value=SimLoginRespData;
						return(Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(SimLoginResp),"utf-16","utf-8"));
					break;
				}
			} catch(Exception e) {
				Console.WriteLine(e.ToString());
			}
			return "";
		}
		
		static string ParseREST(string requestURL, string requestBody, string HTTPmethod) {
			char[] splitter  = {'/'};
                        string[] rest_params = requestURL.Split(splitter);
                        string req_type = rest_params[0];       // First part of the URL is the type of request - 
                        string respstring="";
			SimProfileBase TheSim;
			Console.WriteLine(req_type);
			switch(req_type) {
				case "regions":
					// DIRTY HACK ALERT
					Console.WriteLine("/regions/ accessed");					
					TheSim=OpenGrid_Main.thegrid._regionmanager.GetProfileByHandle((ulong)Convert.ToUInt64(rest_params[1]));
					respstring=ParseREST("/regions/" + rest_params[1], requestBody, HTTPmethod);
				break;
                                
				case "sims":
                                        LLUUID UUID = new LLUUID((string)rest_params[1]);
					TheSim = OpenGrid_Main.thegrid._regionmanager.GetProfileByLLUUID(UUID);
					if(!(TheSim==null)) {
						switch(HTTPmethod) {
							case "GET":
								respstring="<authkey>" + OpenGrid_Main.thegrid.SimSendKey + "</authkey>";
								respstring+="<sim>";
								respstring+="<uuid>" + TheSim.UUID.ToString() + "</uuid>";
								respstring+="<regionname>" + TheSim.regionname + "</regionname>";
								respstring+="<sim_ip>" + TheSim.sim_ip + "</sim_ip>";
								respstring+="<sim_port>" + TheSim.sim_port.ToString() + "</sim_port>";
								respstring+="<region_locx>" + TheSim.RegionLocX.ToString() + "</region_locx>";
								respstring+="<region_locy>" + TheSim.RegionLocY.ToString() + "</region_locy>";
								respstring+="<estate_id>1</estate_id>";
								respstring+="</sim>";
							break;
							case "POST":
								Console.WriteLine("Updating sim details.....");								
								XmlDocument doc = new XmlDocument();
								doc.LoadXml(requestBody);
								XmlNode authkeynode = doc.FirstChild;
                						if (authkeynode.Name != "authkey") {
                    							respstring = "ERROR! bad XML - expected authkey tag";
									return respstring;	
								}
								XmlNode simnode = doc.ChildNodes[1];
								if (simnode.Name != "sim") {
									respstring = "ERROR! bad XML - expected sim tag";
									return respstring;
								}
								if (authkeynode.Name != OpenGrid_Main.thegrid.SimRecvKey) {
									respstring = "ERROR! invalid key";
									return respstring;
								}

								if (TheSim==null) {
									respstring="ERROR! sim not found";
									return respstring;
								} else {
									for(int i=0; i<= simnode.ChildNodes.Count; i++) {
										switch(simnode.ChildNodes[i].Name) {
											case "uuid":
												// should a sim be able to update it's own UUID? To be decided
												// watch next week for the exciting conclusion in "the adventures of OpenGridServices.GridServer/GridHttp.cs:ParseREST() at line 190!
											break;			// and line 190's arch-enemy - THE BREAK STATEMENT! OH NOES!!!!! (this code written at 6:57AM, no sleep, lots of caffeine)
											
											case "regionname":
												TheSim.regionname=simnode.ChildNodes[i].InnerText;
											break;

											case "sim_ip":
												TheSim.sim_ip=simnode.ChildNodes[i].InnerText;
											break;
											
											case "sim_port":
												TheSim.sim_port=Convert.ToUInt32(simnode.ChildNodes[i].InnerText);
											break;

											case "region_locx":
												TheSim.RegionLocX=Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
												TheSim.regionhandle=Helpers.UIntsToLong((TheSim.RegionLocX * 256), (TheSim.RegionLocY * 256));
											break;
											
											case "region_locy":
												TheSim.RegionLocY=Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
												TheSim.regionhandle=Helpers.UIntsToLong((TheSim.RegionLocX * 256), (TheSim.RegionLocY * 256));
											break;
										}
									}
									respstring="OK";																	
								}
								
								

							break;
						}
					}
					return respstring;
				break;
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

			// TODO: AUTHENTICATION!!!!!!!!! MUST ADD!!!!!!!!!! SCRIPT KIDDIES LOVE LACK OF IT!!!!!!!!!!

                        string responseString="";
			switch(request.ContentType) {
                                case "text/xml":
                                	// must be XML-RPC, so pass to the XML-RPC parser
					
					responseString=ParseXMLRPC(requestBody,request.Headers["Referer"]);
					response.AddHeader("Content-type","text/xml");	
				break;
                        	
				case "text/plaintext":
					// must be REST
					responseString=ParseREST(request.RawUrl,requestBody,request.HttpMethod);
				break;

				case null:
					// must be REST or invalid crap, so pass to the REST parser
					responseString=ParseREST(request.RawUrl,requestBody,request.HttpMethod);
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
