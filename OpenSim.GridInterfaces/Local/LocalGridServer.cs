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
using System.IO;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Assets;
using libsecondlife;
using Db4objects.Db4o;
using Db4objects.Db4o.Query;

namespace OpenSim.GridInterfaces.Local
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
	
	public class LocalGridServer : LocalGridBase
	{
		public List<Login> Sessions = new List<Login>();  
		
		public LocalGridServer()
		{
			Sessions = new List<Login>();
			OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Local Grid Server class created");
		}
		
		public override bool RequestConnection(LLUUID SimUUID, string sim_ip, uint sim_port)
		{
			return true;
		}

        public override string GetName()
        {
            return "Local";
        }

		public override AuthenticateResponse AuthenticateSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
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
		
		public override bool LogoutSession(LLUUID sessionID, LLUUID agentID, uint circuitCode)
		{
			return(true);
		}
		
		public override UUIDBlock RequestUUIDBlock()
		{
			UUIDBlock uuidBlock = new UUIDBlock();
			return(uuidBlock);
		}

        public override NeighbourInfo[] RequestNeighbours()
		{
			return null;
		}

        public override void SetServerInfo(string ServerUrl, string SendKey, string RecvKey)
		{
			
		}
		
		public override void Close()
		{
			
		}

		/// <summary>
		/// used by the local login server to inform us of new sessions
		/// </summary>
		/// <param name="session"></param>
		public override void AddNewSession(Login session)
		{
			lock(this.Sessions)
			{
				this.Sessions.Add(session);
			}
		}
	}

	public class AssetUUIDQuery : Predicate
	{
		private LLUUID _findID;
		
		public AssetUUIDQuery(LLUUID find)
		{
			_findID = find;
		}
		public bool Match(AssetStorage asset)
		{
			return (asset.UUID == _findID);
		}
	}
	
	public class AssetStorage
	{
		public byte[] Data;
		public sbyte Type;
		public string Name;
		public LLUUID UUID;
	}
}
