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
using System.Collections.Generic;
using System.Threading;
using OpenSim.GridServers;
using libsecondlife;

namespace LocalGridServers
{
	/// <summary>
	/// 
	/// </summary>
	/// 
	public class LocalGridPlugin : IGridPlugin
	{
		public LocalGridPlugin()
		{
			
		}
		
		public IGridServer GetGridServer()
		{
			return(new LocalGridServer());
		}
	}
	
	public class LocalAssetPlugin : IAssetPlugin
	{
		public LocalAssetPlugin()
		{
			
		}
		
		public IAssetServer GetAssetServer()
		{
			return(new LocalAssetServer());
		}
	}
	
	public class LocalAssetServer : IAssetServer
	{
		private IAssetReceiver _receiver;
		private BlockingQueue<ARequest> _assetRequests;
		
		public LocalAssetServer()
		{
			this._assetRequests = new BlockingQueue<ARequest>();
			ServerConsole.MainConsole.Instance.WriteLine("Local Asset Server class created");
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
			//this._assetRequests.Enqueue(req);
		}
		
		public void UpdateAsset(AssetBase asset)
		{
			
		}
		
		public void UploadNewAsset(AssetBase asset)
		{
			
		}
		
		public void SetServerInfo(string ServerUrl, string ServerKey)
		{
			
		}
		
		private void RunRequests()
		{
			while(true)
			{
				
			}
		}
	}
	
	public class LocalGridServer :IGridServer
	{
		public List<Login> Sessions = new List<Login>();  
		
		public LocalGridServer()
		{
			Sessions = new List<Login>();
			ServerConsole.MainConsole.Instance.WriteLine("Local Grid Server class created");
		}
		
		public bool RequestConnection()
		{
			return true;
		}
		public AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			//we are running local
			AuthenticateResponse user = new AuthenticateResponse();
			
			lock(this.Sessions)
			{
				
				for(int i = 0; i < Sessions.Count; i++)
				{
					if((Sessions[i].Agent == agentID) && (Sessions[i].Session == sessionID))
					{
						user.Authorised = true;
						user.LoginInfo = Sessions[i];
					}
				}
			}
			return(user);
		}
		
		public bool LogoutSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			return(true);
		}
		
		public UUIDBlock RequestUUIDBlock()
		{
			UUIDBlock uuidBlock = new UUIDBlock();
			return(uuidBlock);
		}
		
		public void RequestNeighbours()
		{
			return;
		}
		
		public void SetServerInfo(string ServerUrl, string ServerKey)
		{
			
		}
		/// <summary>
		/// used by the local login server to inform us of new sessions
		/// </summary>
		/// <param name="session"></param>
		public void AddNewSession(Login session)
		{
			lock(this.Sessions)
			{
				this.Sessions.Add(session);
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
