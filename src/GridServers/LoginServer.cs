/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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
* 
*/

using Nwc.XmlRpc;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections;
using System.Security.Cryptography;
using System.Xml;
using libsecondlife;
using OpenSim;

namespace OpenSim.GridServers
{
	
	/// <summary>
	/// When running in local (default) mode , handles client logins.
	/// </summary>
	public class LoginServer
	{
		public LoginServer(IGridServer gridServer)
		{
			_gridServer = gridServer;
		}
		private Login _login;
		private IGridServer _gridServer;
		private ushort _loginPort = 8080;
		public IPAddress clientAddress = IPAddress.Loopback;
		public IPAddress remoteAddress = IPAddress.Any;
		private Socket loginServer;
		private Random RandomClass = new Random();
		private int NumClients;
		private string _defaultResponse;
		
		private string _mpasswd;
		private bool _needPasswd=false;

		// InitializeLogin: initialize the login 
		private void InitializeLogin() {
			loginServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			loginServer.Bind(new IPEndPoint(remoteAddress, _loginPort));
			loginServer.Listen(1);
			
			this._needPasswd=false;
			//read in default response string
			StreamReader SR;
    		string lines;
    		SR=File.OpenText("new-login.dat");
    		
    		//lines=SR.ReadLine();
    		
    		while(!SR.EndOfStream)
    		{
    			lines = SR.ReadLine();
    			_defaultResponse += lines;
    			//lines = SR.ReadLine();
    		}
    		SR.Close();
    		this._mpasswd = EncodePassword("testpass");
		}
		
		public void Startup()
		{
			this.InitializeLogin();
			Thread runLoginProxy = new Thread(new ThreadStart(RunLogin));
			runLoginProxy.IsBackground = true;
			runLoginProxy.Start();
		}
		
		private void RunLogin() 
		{
			Console.WriteLine("Starting Login Server");
		    try 
		    {
		    	for (;;) 
		    	{
		    		Socket client = loginServer.Accept();
		    		IPEndPoint clientEndPoint = (IPEndPoint)client.RemoteEndPoint;

		    		
		    		NetworkStream networkStream = new NetworkStream(client);
		    		StreamReader networkReader = new StreamReader(networkStream);
		    		StreamWriter networkWriter = new StreamWriter(networkStream);

		    		try
		    		{
		    			LoginRequest(networkReader, networkWriter);
		    		}
		    		catch (Exception e)
		    		{
		    			Console.WriteLine(e.Message);
		    		}

		    		networkWriter.Close();
		    		networkReader.Close();
		    		networkStream.Close();

		    		client.Close();

		    		// send any packets queued for injection
		    		
				}
		    } 
		    catch (Exception e)
		    {
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
		    }
		}

		// ProxyLogin: proxy a login request
		private void LoginRequest(StreamReader reader, StreamWriter writer) 
		{ 
			lock(this)
			{
				string line;
				int contentLength = 0;
				// read HTTP header
				do
				{
					// read one line of the header
					line = reader.ReadLine();
				
					// check for premature EOF
					if (line == null)
						throw new Exception("EOF in client HTTP header");

					// look for Content-Length
					Match match = (new Regex(@"Content-Length: (\d+)$")).Match(line);
					if (match.Success)
						contentLength = Convert.ToInt32(match.Groups[1].Captures[0].ToString());
				} while (line != "");
				
				// read the HTTP body into a buffer
				char[] content = new char[contentLength];
				reader.Read(content, 0, contentLength);
				
				XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(new String(content));
				if(request.MethodName == "login_to_simulator")
				{
					Hashtable requestData = (Hashtable)request.Params[0];
					string first;
					string last;
					string passwd;
					LLUUID Agent;
					LLUUID Session;
					
					//get login name
					if(requestData.Contains("first"))
					{
						first = (string)requestData["first"];
					}
					else
					{
						first = "test";
					}
					
					if(requestData.Contains("last"))
					{
						last = (string)requestData["last"];
					}
					else
					{
						last = "User"+NumClients.ToString();
					}
					
					if(requestData.Contains("passwd"))
					{
						passwd = (string)requestData["passwd"];
					}
					else
					{
						passwd = "notfound";
					}
					
					if( !Authenticate(first, last, passwd))
					{
						// Fail miserably
						writer.WriteLine("HTTP/1.0 403 Authentication Forbidden");
						writer.WriteLine();
						return;
					}
					NumClients++;
					
					//create a agent and session LLUUID
					Agent = GetAgentId( first, last );
					int SessionRand = this.RandomClass.Next(1,999);
					Session = new LLUUID("aaaabbbb-0200-"+SessionRand.ToString("0000")+"-8664-58f53e442797");
					
					
					XmlRpcResponse response =(XmlRpcResponse)(new XmlRpcResponseDeserializer()).Deserialize(this._defaultResponse);
					Hashtable responseData = (Hashtable)response.Value;
					
					responseData["sim_port"] = OpenSim_Main.cfg.IPListenPort;
					responseData["sim_ip"] = OpenSim_Main.cfg.IPListenAddr;
					responseData["agent_id"] = Agent.ToStringHyphenated();
					responseData["session_id"] = Session.ToStringHyphenated();
					ArrayList InventoryList = (ArrayList) responseData["inventory-skeleton"];
					Hashtable Inventory1 = (Hashtable)InventoryList[0];
					Hashtable Inventory2 = (Hashtable)InventoryList[1];
					LLUUID BaseFolderID = LLUUID.Random();
					LLUUID InventoryFolderID = LLUUID.Random();
					Inventory2["name"] = "Base";
					Inventory2["folder_id"] = BaseFolderID.ToStringHyphenated();
					Inventory2["type_default"] =6;
					Inventory1["folder_id"] = InventoryFolderID.ToStringHyphenated();
					
					ArrayList InventoryRoot = (ArrayList) responseData["inventory-root"];
					Hashtable Inventoryroot = (Hashtable)InventoryRoot[0];
					Inventoryroot["folder_id"] = InventoryFolderID.ToStringHyphenated();
					
					CustomiseLoginResponse( responseData, first, last );
					
					this._login = new Login();
					//copy data to login object
					_login.First = first;
					_login.Last = last;
					_login.Agent = Agent;
					_login.Session = Session;
					_login.BaseFolder = BaseFolderID;
					_login.InventoryFolder = InventoryFolderID;
					
					//working on local computer so lets add to the gridserver's list of sessions?
					if(OpenSim_Main.gridServers.GridServer.GetName() == "Local")
					{
						((LocalGridBase)this._gridServer).AddNewSession(_login);
					}
					
					// forward the XML-RPC response to the client
					writer.WriteLine("HTTP/1.0 200 OK");
					writer.WriteLine("Content-type: text/xml");
					writer.WriteLine();
					
					XmlTextWriter responseWriter = new XmlTextWriter(writer);
					XmlRpcResponseSerializer.Singleton.Serialize(responseWriter, response);
					responseWriter.Close();
				}
				else
				{
					writer.WriteLine("HTTP/1.0 403 Authentication Forbidden");
					writer.WriteLine();
				}
			}
		}
		
		protected virtual void CustomiseLoginResponse( Hashtable responseData, string first, string last )
		{
		}

		protected virtual LLUUID GetAgentId(string firstName, string lastName)
		{
			LLUUID Agent;
			int AgentRand = this.RandomClass.Next(1,9999);
			Agent = new LLUUID("99998888-0100-"+AgentRand.ToString("0000")+"-8ec1-0b1d5cd6aead");
			return Agent;
		}

		protected virtual bool Authenticate(string first, string last, string  passwd)
		{
			if(this._needPasswd)
			{
				//every user needs the password to login
				string encodedPass = passwd.Remove(0,3); //remove $1$
				if(encodedPass == this._mpasswd)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				//do not need password to login
				return true;
			}
		}
		
		private static string EncodePassword(string passwd)
		{
			Byte[] originalBytes;
			Byte[] encodedBytes;
			MD5 md5;
			
			md5 = new MD5CryptoServiceProvider();
			originalBytes = ASCIIEncoding.Default.GetBytes(passwd);
			encodedBytes = md5.ComputeHash(originalBytes);

			return Regex.Replace(BitConverter.ToString(encodedBytes), "-", "").ToLower();
		}
		
	}

	
}
