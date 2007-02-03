/*
Copyright (c) OpenSim project, http://sim.opensecondlife.org/

* Copyright (c) <year>, <copyright holder>
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
using System.Collections.Generic;
using libsecondlife;
using System.Collections;
using libsecondlife.Packets;
using libsecondlife.AssetSystem;
using System.IO;
using System.Xml;


namespace OpenSim 
{
	/// <summary>
	/// Description of GridManager.
	/// </summary>
	public class GridManager
	{
		private Server server;
		private System.Text.Encoding enc = System.Text.Encoding.ASCII;
		private AgentManager AgentManager;
		private Dictionary<ulong,RegionInfo> Grid;
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="serve"></param>
		/// <param name="agentManager"></param>
		public GridManager(Server serve, AgentManager agentManager)
		{
			Grid=new Dictionary<ulong, RegionInfo>();
			server=serve;
			AgentManager=agentManager;
			LoadGrid();
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		public void RequestMapLayer(User_Agent_info UserInfo)
		{
			//send a layer covering the 800,800 - 1200,1200 area
				MapLayerReplyPacket MapReply=new MapLayerReplyPacket();
        		MapReply.AgentData.AgentID=UserInfo.AgentID;
        		MapReply.AgentData.Flags=0;
        		MapReply.LayerData=new MapLayerReplyPacket.LayerDataBlock[1];
        		MapReply.LayerData[0]=new MapLayerReplyPacket.LayerDataBlock();
        		MapReply.LayerData[0].Bottom=800;
        		MapReply.LayerData[0].Left=800;
        		MapReply.LayerData[0].Top=1200;
        		MapReply.LayerData[0].Right=1200;
        		MapReply.LayerData[0].ImageID=new LLUUID("00000000-0000-0000-7007-000000000006");
        		server.SendPacket(MapReply,true,UserInfo);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		/// <param name="MinX"></param>
		/// <param name="MinY"></param>
		/// <param name="MaxX"></param>
		/// <param name="MaxY"></param>
		public void RequestMapBlock(User_Agent_info UserInfo, int MinX, int MinY,int MaxX,int MaxY)
		{		
        		foreach (KeyValuePair<ulong,RegionInfo> RegionPair in this.Grid)
				{
					//check Region is inside the requested area		
					RegionInfo Region=RegionPair.Value;
					if(((Region.X>MinX) && (Region.X<MaxX)) && ((Region.Y>MinY) && (Region.Y<MaxY)))
					{
						MapBlockReplyPacket MapReply=new MapBlockReplyPacket();
		        		MapReply.AgentData.AgentID=UserInfo.AgentID;
		        		MapReply.AgentData.Flags=0;
		        		MapReply.Data=new MapBlockReplyPacket.DataBlock[1];
		        		MapReply.Data[0]=new MapBlockReplyPacket.DataBlock();
		        		MapReply.Data[0].MapImageID=Region.ImageID;
		        		MapReply.Data[0].X=Region.X;
		        		MapReply.Data[0].Y=Region.Y;
		        		MapReply.Data[0].WaterHeight=Region.WaterHeight;
		        		MapReply.Data[0].Name=enc.GetBytes( Region.Name);
		        		MapReply.Data[0].RegionFlags=72458694;
		        		MapReply.Data[0].Access=13;
		        		MapReply.Data[0].Agents=1;
		        		server.SendPacket(MapReply,true,UserInfo);
					}
        		}
        	
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		/// <param name="Request"></param>
		public void RequestTeleport(User_Agent_info UserInfo, TeleportLocationRequestPacket Request)
		{
			if(Grid.ContainsKey(Request.Info.RegionHandle))
			{
				RegionInfo Region=Grid[Request.Info.RegionHandle];
				libsecondlife.Packets.TeleportStartPacket TeleportStart=new TeleportStartPacket();
        		TeleportStart.Info.TeleportFlags=16;
        		server.SendPacket(TeleportStart,true,UserInfo);
        		
        		libsecondlife.Packets.TeleportFinishPacket Teleport=new TeleportFinishPacket();
        		Teleport.Info.AgentID=UserInfo.AgentID;
        		Teleport.Info.RegionHandle=Request.Info.RegionHandle;
        		Teleport.Info.SimAccess=13;
        		Teleport.Info.SeedCapability=new byte[0];
        		
        		System.Net.IPAddress oIP=System.Net.IPAddress.Parse(Region.IPAddress.Address);
         		byte[] byteIP=oIP.GetAddressBytes();
         		uint ip=(uint)byteIP[3]<<24;
         		ip+=(uint)byteIP[2]<<16;
         		ip+=(uint)byteIP[1]<<8;
         		ip+=(uint)byteIP[0];
         		
        		Teleport.Info.SimIP=ip;
        		Teleport.Info.SimPort=Region.IPAddress.Port;
        		Teleport.Info.LocationID=4;
        		Teleport.Info.TeleportFlags= 1 << 4;;
        		server.SendPacket(Teleport,true,UserInfo);
        		
        		this.AgentManager.RemoveAgent(UserInfo);
			}
        	
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void LoadGrid()
		{
			 //should connect to a space server to see what grids there are 
			 //but for now we read static xml files
			 ulong CurrentHandle=0;
			 bool Login=true;
			 
			 XmlDocument doc = new XmlDocument();
         
	         try {
	           	doc.Load(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"Grid.ini" ));
	         }
	         catch ( Exception e)
	         {
	         	Console.WriteLine(e.Message);
	            return;
	         }
	         
	         try
	         {
		            XmlNode root = doc.FirstChild;
		            if (root.Name != "Root")
		               throw new Exception("Error: Invalid File. Missing <Root>");
		            
		            XmlNode nodes = root.FirstChild;
		            if (nodes.Name != "Grid")
		               throw new Exception("Error: Invalid File. <project> first child should be <Grid>");
		            
		            if (nodes.HasChildNodes)   {
			        	foreach( XmlNode xmlnc in nodes.ChildNodes)   
			            {	
			            	if(xmlnc.Name=="Region")
			            	{
			            		string xmlAttri;
			            		RegionInfo Region=new RegionInfo();
			            		if(xmlnc.Attributes["Name"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("Name")).Value;
			            			Region.Name=xmlAttri+" \0";
			            		}
			            		if(xmlnc.Attributes["ImageID"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("ImageID")).Value;
									Region.ImageID=new LLUUID(xmlAttri);
			            		}
			            		if(xmlnc.Attributes["IP_Address"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("IP_Address")).Value;
			            			Region.IPAddress.Address=xmlAttri;
			            		}
			            		if(xmlnc.Attributes["IP_Port"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("IP_Port")).Value;
									Region.IPAddress.Port=Convert.ToUInt16(xmlAttri);
			            		}
			            		if(xmlnc.Attributes["Location_X"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("Location_X")).Value;
									Region.X=Convert.ToUInt16(xmlAttri);
			            		}
			            		if(xmlnc.Attributes["Location_Y"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("Location_Y")).Value;
									Region.Y=Convert.ToUInt16(xmlAttri);
			            		}
			            		
			            		this.Grid.Add(Region.Handle,Region);
			            		
			            	}
			            	if(xmlnc.Name=="CurrentRegion")
			            	{
			            		
			            		string xmlAttri;
			            		uint Rx=0,Ry=0;
			            		if(xmlnc.Attributes["RegionHandle"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("RegionHandle")).Value;
									CurrentHandle=Convert.ToUInt64(xmlAttri);
									
			            		}
			            		else
			            		{
			            			if(xmlnc.Attributes["Region_X"]!=null)
									{
										xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("Region_X")).Value;
										Rx=Convert.ToUInt32(xmlAttri);
			            			}
			            			if(xmlnc.Attributes["Region_Y"]!=null)
									{
										xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("Region_Y")).Value;
										Ry=Convert.ToUInt32(xmlAttri);
			            			}
			            		}
			            		if(xmlnc.Attributes["LoginServer"]!=null)
								{
									xmlAttri=((XmlAttribute)xmlnc.Attributes.GetNamedItem("LoginServer")).Value;
									Login=Convert.ToBoolean(xmlAttri);
									
			            		}
			            		if(CurrentHandle==0)
			            		{
			            			//no RegionHandle set
			            			//so check for Region X and Y
			            			if((Rx >0) && (Ry>0))
			            			{
			            				CurrentHandle=Helpers.UIntsToLong((Rx*256),(Ry*256));
			            			}
			            			else
			            			{
			            				//seems to be no Region location set
			            				// so set default
			            				CurrentHandle=1096213093147648;
			            			}
			            		}
			            	}
			            }
			        	
			            //finished loading grid, now set Globals to current region
			            if(CurrentHandle!=0)
			            {
			            	if(Grid.ContainsKey(CurrentHandle))
			            	{
			            		RegionInfo Region=Grid[CurrentHandle];
			            		Globals.Instance.RegionHandle=Region.Handle;
			            		Globals.Instance.RegionName=Region.Name;
			            		Globals.Instance.IpPort=Region.IPAddress.Port;
			            		Globals.Instance.LoginSever=Login;
			            	}
			            }
			            	
		            }
	       	}
	        catch ( Exception e)
	        {
	            Console.WriteLine(e.Message);
	            return;
	        }
		}
	}
	
	public class RegionInfo
	{
		public RegionIP IPAddress;
		public string Name;
		public ushort x;
		public ushort y;
		public ulong handle;
		public LLUUID ImageID;
		public uint Flags;
		public byte WaterHeight;
		
		public ushort X
		{
			get
			{
				return(x);
			}
			set
			{
				x=value;
				Handle=Helpers.UIntsToLong((((uint)x)*256),(((uint)y)*256));
			}
		}
		public ushort Y
		{
			get
			{
				return(y);
			}
			set
			{
				y=value;
				Handle=Helpers.UIntsToLong((((uint)x)*256),(((uint)y)*256));
			}
		}
		public ulong Handle
		{
			get
			{
					if(handle>0)
					{
						return(handle);
					}
					else
					{
						return(Helpers.UIntsToLong((((uint)x)*256),(((uint)y)*256)));
					}
			}
			set
			{
				handle=value;
			}
				
		}
		
		public RegionInfo()
		{
			this.IPAddress=new RegionIP();
		}
	}
	public class RegionIP
	{
		public string Address;
		public ushort Port;
		
		public RegionIP()
		{
			
		}
	
	}
}
