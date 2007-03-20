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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Xml;
using libsecondlife;
using OpenSim.GridServers;

namespace RemoteGridServers
{
	/// <summary>
	/// 
	/// </summary>
	/// 
	
	public class RemoteGridPlugin : IGridPlugin
	{
		public RemoteGridPlugin()
		{
			
		}
		
		public IGridServer GetGridServer()
		{
			return(new RemoteGridServer());
		}
	}
	
	public class RemoteAssetPlugin : IAssetPlugin
	{
		public RemoteAssetPlugin()
		{
			
		}
		
		public IAssetServer GetAssetServer()
		{
			return(new RemoteAssetServer());
		}
	}
	public class RemoteGridServer : RemoteGridBase
	{
		private string GridServerUrl;
		private string GridSendKey;
		private string GridRecvKey;
		private string UserServerUrl;
		private string UserSendKey;
		private string UserRecvKey;

		private Dictionary<uint, agentcircuitdata> AgentCircuits = new Dictionary<uint, agentcircuitdata>(); 

		public override Dictionary<uint, agentcircuitdata> agentcircuits {
                        get {return AgentCircuits;} 
                        set {AgentCircuits=value;}
                }
	
		public RemoteGridServer()
		{
			ServerConsole.MainConsole.Instance.WriteLine("Remote Grid Server class created");
		}
		
		public override bool RequestConnection()
		{
			return true;
		}
	
		public override AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitcode)
		{
			agentcircuitdata validcircuit=this.AgentCircuits[circuitcode];
			AuthenticateResponse user = new AuthenticateResponse();
			if((sessionID==validcircuit.SessionID) && (agentID==validcircuit.AgentID)) 
			{
				// YAY! Valid login
				user.Authorised = true;
				user.LoginInfo = new Login();
				user.LoginInfo.Agent = agentID;
				user.LoginInfo.Session = sessionID;
				user.LoginInfo.First = validcircuit.firstname;
				user.LoginInfo.Last = validcircuit.lastname;
			}
			else 
			{
				// Invalid
				user.Authorised = false;	
			}
			
			return(user);
		}
		
		public override bool LogoutSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			WebRequest DeleteSession = WebRequest.Create(UserServerUrl + "/usersessions/" + sessionID.ToString());
			DeleteSession.Method="DELETE";
			DeleteSession.ContentType="text/plaintext";
			DeleteSession.ContentLength=0;

			StreamWriter stOut = new StreamWriter (DeleteSession.GetRequestStream(), System.Text.Encoding.ASCII);
			stOut.Write("");
			stOut.Close();

			StreamReader stIn = new StreamReader(DeleteSession.GetResponse().GetResponseStream());
			string GridResponse = stIn.ReadToEnd();
			stIn.Close(); 
			return(true);
		}
		
		public override UUIDBlock RequestUUIDBlock()
		{
			UUIDBlock uuidBlock = new UUIDBlock();
			return(uuidBlock);
		}
		
		public override neighbourinfo[] RequestNeighbours(ulong regionhandle)
		{
			ArrayList neighbourlist = new ArrayList();
			
			WebRequest FindNeighbours = WebRequest.Create(GridServerUrl + "/regions/" + regionhandle.ToString() + "/neighbours");
                        FindNeighbours.ContentType="text/plaintext";
                        FindNeighbours.ContentLength=0;

                        StreamWriter stOut = new StreamWriter (FindNeighbours.GetRequestStream(), System.Text.Encoding.ASCII);
                        stOut.Write("");
                        stOut.Close();

                        
			XmlDocument GridRespXml = new XmlDocument();
			GridRespXml.Load(FindNeighbours.GetResponse().GetResponseStream());
			

			XmlNode NeighboursRoot = GridRespXml.FirstChild;
			if(NeighboursRoot.Name != "neighbours") {
				return new neighbourinfo[0];
			}

			FindNeighbours.GetResponse().GetResponseStream().Close();	
			
			return new neighbourinfo[0];
		}
		
		public override void SetServerInfo(string UserServerUrl, string UserSendKey, string UserRecvKey, string GridServerUrl, string GridSendKey, string GridRecvKey)
		{
			this.UserServerUrl = UserServerUrl;
			this.UserSendKey = UserSendKey;			
			this.UserRecvKey = UserRecvKey;
			this.GridServerUrl = GridServerUrl;
			this.GridSendKey = GridSendKey;
			this.GridRecvKey = GridRecvKey;
		}
		
		public override string GetName()
		{ 
			return "Remote";
		}
	}
	
	
	public class RemoteAssetServer : IAssetServer
	{
		private IAssetReceiver _receiver;
		private BlockingQueue<ARequest> _assetRequests;
		private Thread _remoteAssetServerThread;
		private string AssetServerUrl;
		private string AssetSendKey;
		
		public RemoteAssetServer()
		{
			this._assetRequests = new BlockingQueue<ARequest>();
			this._remoteAssetServerThread = new Thread(new ThreadStart(RunRequests));
			this._remoteAssetServerThread.IsBackground = true;
			this._remoteAssetServerThread.Start();
			ServerConsole.MainConsole.Instance.WriteLine("Remote Asset Server class created");
		}
		
		public void SetReceiver(IAssetReceiver receiver)
		{
			this._receiver = receiver;
		}
		
		public void RequestAsset(LLUUID assetID, bool isTexture)
		{
			ARequest req = new ARequest();
			req.AssetID = assetID;
			req.IsTexture = isTexture;
			this._assetRequests.Enqueue(req);
		}
		
		public void UpdateAsset(AssetBase asset)
		{
			
		}
		
		public void UploadNewAsset(AssetBase asset)
		{
			
		}
		
		public void SetServerInfo(string ServerUrl, string ServerKey)
		{
			this.AssetServerUrl = ServerUrl;
			this.AssetSendKey = ServerKey;
		}
		
		private void RunRequests()
		{
			while(true)
			{
				//we need to add support for the asset server not knowing about a requested asset
				ARequest req = this._assetRequests.Dequeue();
				LLUUID assetID = req.AssetID;
				ServerConsole.MainConsole.Instance.WriteLine(" RemoteAssetServer- Got a AssetServer request, processing it");
				WebRequest AssetLoad = WebRequest.Create(this.AssetServerUrl + "getasset/" + AssetSendKey + "/" + assetID + "/data");
				WebResponse AssetResponse = AssetLoad.GetResponse();
				byte[] idata = new byte[(int)AssetResponse.ContentLength];
				BinaryReader br = new BinaryReader(AssetResponse.GetResponseStream());
				idata = br.ReadBytes((int)AssetResponse.ContentLength);
				br.Close();
				
				AssetBase asset = new AssetBase();
				asset.FullID = assetID;
				asset.Data = idata;
				_receiver.AssetReceived(asset, req.IsTexture );
			}
		}
	}

	public class BlockingQueue< T > {
		private Queue< T > _queue = new Queue< T >();
		private object _queueSync = new object();

		public void Enqueue(T value)
		{
			lock(_queueSync)
			{
				_queue.Enqueue(value);
				Monitor.Pulse(_queueSync);
			}
		}

		public T Dequeue()
		{
			lock(_queueSync)
			{
				if( _queue.Count < 1)
					Monitor.Wait(_queueSync);

				return _queue.Dequeue();
			}
		}
	}
}
