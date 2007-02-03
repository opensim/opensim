/*
Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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
using libsecondlife.Packets;
using libsecondlife.AssetSystem;
using System.IO;
using Axiom.MathLib;

namespace OpenSim
{
	/// <summary>
	/// Description of Agent_Manager.
	/// </summary>
	public class AgentManager
	{
		public Dictionary<libsecondlife.LLUUID,AvatarData> AgentList;
		
		private uint local_numer=0;
		private Server server;
		public  PrimManager Prim_Manager;
		public AssetManagement Asset_Manager;
		
		private libsecondlife.Packets.RegionHandshakePacket RegionPacket;
		private  System.Text.Encoding enc = System.Text.Encoding.ASCII;
		public libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock AvatarTemplate;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="serve"></param>
		public AgentManager(Server serve)
		{
			AgentList=new Dictionary<libsecondlife.LLUUID,AvatarData>();
			server=serve;
			this.initialise();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public AvatarData GetAgent(LLUUID id)
		{
			if(!this.AgentList.ContainsKey(id))
			{
				return null;
			}
			else
			{
				AvatarData ad=this.AgentList[id];
				return ad;
			}
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="agent"></param>
		public void AddAgent(AvatarData agent)
		{
			this.AgentList.Add(agent.FullID,agent);
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <returns></returns>
		/// 
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		/// <param name="first"></param>
		/// <param name="last"></param>
		/// <returns></returns>
		public bool NewAgent(User_Agent_info User_info, string first, string last ,LLUUID BaseFolder,LLUUID InventoryFolder)
		{
			AvatarData agent=new AvatarData();
			agent.FullID=User_info.AgentID;
			agent.NetInfo=User_info;
			agent.NetInfo.first_name=first;
			agent.NetInfo.last_name=last;
			agent.Position=new LLVector3(100,100,22);
			agent.BaseFolder=BaseFolder;
			agent.InventoryFolder=InventoryFolder;
			this.AgentList.Add(agent.FullID,agent);
			
			//Create new Wearable Assets and place in Inventory
			this.Asset_Manager.CreateNewInventorySet(ref agent,User_info);
			
			return(true);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		public void RemoveAgent(User_Agent_info UserInfo)
		{
			this.AgentList.Remove(UserInfo.AgentID);
			
			//tell other clients to delete this avatar
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		public void AgentJoin(User_Agent_info User_info)
		{
			//send region data 
			server.SendPacket(RegionPacket,true,User_info);
			
			//inform client of join comlete
			libsecondlife.Packets.AgentMovementCompletePacket mov=new AgentMovementCompletePacket();
			mov.AgentData.SessionID=User_info.SessionID;
			mov.AgentData.AgentID=User_info.AgentID;
			mov.Data.RegionHandle=Globals.Instance.RegionHandle;
			mov.Data.Timestamp=1169838966;
			mov.Data.Position=new LLVector3(100f,100f,22f);
			mov.Data.LookAt=new LLVector3(0.99f,0.042f,0);
			server.SendPacket(mov,true,User_info);
		}
		
		/// <summary>
		/// 
		/// </summary>
		public void UpdatePositions()
		{
			//update positions
			foreach (KeyValuePair<libsecondlife.LLUUID,AvatarData> kp in this.AgentList)
			{
				
				kp.Value.Position.X+=(kp.Value.Velocity.X*0.2f);
				kp.Value.Position.Y+=(kp.Value.Velocity.Y*0.2f);
				kp.Value.Position.Z+=(kp.Value.Velocity.Z*0.2f);
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void initialise()
		{
				//Region data
				RegionPacket=new RegionHandshakePacket();
				RegionPacket.RegionInfo.BillableFactor=0;
				RegionPacket.RegionInfo.IsEstateManager=false;
				RegionPacket.RegionInfo.TerrainHeightRange00=60;
				RegionPacket.RegionInfo.TerrainHeightRange01=60;
				RegionPacket.RegionInfo.TerrainHeightRange10=60;
				RegionPacket.RegionInfo.TerrainHeightRange11=60;
				RegionPacket.RegionInfo.TerrainStartHeight00=20;
				RegionPacket.RegionInfo.TerrainStartHeight01=20;
				RegionPacket.RegionInfo.TerrainStartHeight10=20;
				RegionPacket.RegionInfo.TerrainStartHeight11=20;
				RegionPacket.RegionInfo.SimAccess=13;
				RegionPacket.RegionInfo.WaterHeight=5;
				RegionPacket.RegionInfo.RegionFlags=72458694;
				RegionPacket.RegionInfo.SimName=enc.GetBytes( Globals.Instance.RegionName);
				RegionPacket.RegionInfo.SimOwner=new LLUUID("00000000-0000-0000-0000-000000000000");
				RegionPacket.RegionInfo.TerrainBase0=new LLUUID("b8d3965a-ad78-bf43-699b-bff8eca6c975");
				RegionPacket.RegionInfo.TerrainBase1=new LLUUID("abb783e6-3e93-26c0-248a-247666855da3");
				RegionPacket.RegionInfo.TerrainBase2=new LLUUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
				RegionPacket.RegionInfo.TerrainBase3=new LLUUID("beb169c7-11ea-fff2-efe5-0f24dc881df2");
				RegionPacket.RegionInfo.TerrainDetail0=new LLUUID("00000000-0000-0000-0000-000000000000");
				RegionPacket.RegionInfo.TerrainDetail1=new LLUUID("00000000-0000-0000-0000-000000000000");
				RegionPacket.RegionInfo.TerrainDetail2=new LLUUID("00000000-0000-0000-0000-000000000000");
				RegionPacket.RegionInfo.TerrainDetail3=new LLUUID("00000000-0000-0000-0000-000000000000");
				RegionPacket.RegionInfo.CacheID=new LLUUID("545ec0a5-5751-1026-8a0b-216e38a7ab37");
				
				this.SetupTemplate("objectupate168.dat");
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		private void SetupTemplate(string name)
		{
			/*ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=1096213093147648;
			objupdate.RegionData.TimeDilation=64096;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			*/	
			int i=0;
			FileInfo fInfo = new FileInfo(name);

			long numBytes = fInfo.Length;

			FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
			
			BinaryReader br = new BinaryReader(fStream);

			byte [] data1 = br.ReadBytes((int)numBytes);

			br.Close();

			fStream.Close();
			
			libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1,ref i);
			
			//objupdate.ObjectData[0]=objdata;
			
			System.Text.Encoding enc = System.Text.Encoding.ASCII;
			libsecondlife.LLVector3 pos=new LLVector3(objdata.ObjectData, 16);
			pos.X=100f;
			objdata.ID=8880000;
			objdata.NameValue=enc.GetBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User \0");
			libsecondlife.LLVector3 pos2=new LLVector3(13.981f,100.0f,20.0f);
			//objdata.FullID=user.AgentID;
			byte[] pb=pos.GetBytes();
						
			Array.Copy(pb,0,objdata.ObjectData,16,pb.Length);
			
			AvatarTemplate=objdata;
				
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		public void SendInitialData(User_Agent_info User_info)
		{
			
			//shouldn't have to read all this in from disk for every new client
				 string data_path=System.AppDomain.CurrentDomain.BaseDirectory + @"\layer_data\";
			
				//send layerdata
				LayerDataPacket layerpack=new LayerDataPacket();
				layerpack.LayerID.Type=76;
				this.SendLayerData(User_info,ref layerpack,data_path+@"layerdata0.dat");
	
				LayerDataPacket layerpack1=new LayerDataPacket();
				layerpack1.LayerID.Type=76;
				this.SendLayerData(User_info,ref layerpack,data_path+@"layerdata1.dat");
				
				LayerDataPacket layerpack2=new LayerDataPacket();
				layerpack2.LayerID.Type=56;
				this.SendLayerData(User_info,ref layerpack,data_path+@"layerdata2.dat");
				
				LayerDataPacket layerpack3=new LayerDataPacket();
				layerpack3.LayerID.Type=55;
				this.SendLayerData(User_info,ref layerpack,data_path+@"layerdata3.dat");
				
				LayerDataPacket layerpack4=new LayerDataPacket();
				layerpack4.LayerID.Type=56;
				this.SendLayerData(User_info,ref layerpack,data_path+@"layerdata4.dat");
				
				LayerDataPacket layerpack5=new LayerDataPacket();
				layerpack5.LayerID.Type=55;
				this.SendLayerData(User_info,ref layerpack,data_path+@"layerdata5.dat");
				
				//send intial set of captured prims data?
				this.Prim_Manager.ReadPrimDatabase( "objectdatabase.ini",User_info);			
				
				//send prims that have been created by users
				//prim_man.send_existing_prims(User_info);
				
				//send update about clients avatar
				this.SendInitialAvatarPosition(User_info);
				
				//send updates about all other users
				foreach (KeyValuePair<libsecondlife.LLUUID,AvatarData> kp in this.AgentList)
				{
					if(kp.Value.NetInfo.AgentID!=User_info.AgentID)
					{
						this.SendOtherAvatarPosition(User_info,kp.Value);
					}
				}	
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		public void SendInitialAvatarPosition(User_Agent_info User_info)
		{
			//send a objectupdate packet with information about the clients avatar
			ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=Globals.Instance.RegionHandle;
			objupdate.RegionData.TimeDilation=64096;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0]=AvatarTemplate;
			//give this avatar object a local id and assign the user a name
			objupdate.ObjectData[0].ID=8880000+this.local_numer;
			User_info.localID=objupdate.ObjectData[0].ID;
			//User_info.name="Test"+this.local_numer+" User";
			this.GetAgent(User_info.AgentID).Started=true;
			objupdate.ObjectData[0].FullID=User_info.AgentID;
			objupdate.ObjectData[0].NameValue=enc.GetBytes("FirstName STRING RW SV "+User_info.first_name+"\nLastName STRING RW SV "+User_info.last_name+" \0");
			User_info.name="FirstName STRING RW SV "+User_info.first_name+"\nLastName STRING RW SV "+User_info.last_name+" \0";
			//User_info.last_name="User";
			//User_info.first_name="Test"+this.local_numer;
			libsecondlife.LLVector3 pos2=new LLVector3(100f,100.0f,22.0f);
			
			byte[] pb=pos2.GetBytes();
						
			Array.Copy(pb,0,objupdate.ObjectData[0].ObjectData,16,pb.Length);
			this.local_numer++;
			
			server.SendPacket(objupdate,true,User_info);
			
			//send this info to other existing clients
			foreach (KeyValuePair<libsecondlife.LLUUID,AvatarData> kp in this.AgentList)
			{
					if(kp.Value.NetInfo.AgentID!=User_info.AgentID)
					{
						server.SendPacket(objupdate,true,kp.Value.NetInfo);
						this.SendOtherAppearance(kp.Value.NetInfo,objupdate.ObjectData[0].FullID);
					}
			}
		
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="user"></param>
		public void SendIntialAvatarAppearance(User_Agent_info user)
		{
			AvatarData Agent=this.AgentList[user.AgentID];
			AgentWearablesUpdatePacket aw=new AgentWearablesUpdatePacket();
			aw.AgentData.AgentID=user.AgentID;
			aw.AgentData.SerialNum=0;
			aw.AgentData.SessionID=user.SessionID;
			
			aw.WearableData= new AgentWearablesUpdatePacket.WearableDataBlock[13];
			AgentWearablesUpdatePacket.WearableDataBlock awb=null;
			awb=new AgentWearablesUpdatePacket.WearableDataBlock();
			awb.WearableType=(byte)0;
			awb.AssetID=Agent.Wearables[0].AssetID;//new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
			awb.ItemID=Agent.Wearables[0].ItemID;//new LLUUID("b7878441893b094917f791174bc8401c");
			aw.WearableData[0]=awb;
	
			awb=new AgentWearablesUpdatePacket.WearableDataBlock();
			awb.WearableType=(byte)1;
			awb.AssetID=Agent.Wearables[1].AssetID;//new LLUUID("e0ee49b5a4184df8d3c9a65361fe7f49");
			awb.ItemID=Agent.Wearables[1].ItemID;//new LLUUID("193f0876fc11d143797454352f9c9c26");
			aw.WearableData[1]=awb;
	
			for(int i=2; i<13; i++)
			{
				awb=new AgentWearablesUpdatePacket.WearableDataBlock();
				awb.WearableType=(byte)i;
				awb.AssetID=new LLUUID("00000000-0000-0000-0000-000000000000");
				awb.ItemID=new LLUUID("00000000-0000-0000-0000-000000000000");
				aw.WearableData[i]=awb;
			}
			
			server.SendPacket(aw,true,user);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="user"></param>
		/// <param name="id"></param>
		public void SendOtherAppearance(User_Agent_info user,LLUUID id)
		{
			AvatarAppearancePacket avp=new AvatarAppearancePacket();
		
			
			avp.VisualParam=new AvatarAppearancePacket.VisualParamBlock[218];
			//avp.ObjectData.TextureEntry=this.avatar_template.TextureEntry;// br.ReadBytes((int)numBytes);
			
			FileInfo fInfo = new FileInfo("Avatar_texture3.dat");

			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream("Avatar_texture3.dat", FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fStream);
			avp.ObjectData.TextureEntry= br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			
			AvatarAppearancePacket.VisualParamBlock avblock=null;
			for(int i=0; i<218; i++)
			{
				avblock=new AvatarAppearancePacket.VisualParamBlock();
				avblock.ParamValue=(byte)100;
				avp.VisualParam[i]=avblock;
			}
			
			avp.Sender.IsTrial=false;
			avp.Sender.ID=id;
			server.SendPacket(avp,true,user);
			
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		/// <param name="avd"></param>
		public void SendOtherAvatarPosition(User_Agent_info User_info, AvatarData avd)
		{
			//send a objectupdate packet with information about the clients avatar
			ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=Globals.Instance.RegionHandle;
			objupdate.RegionData.TimeDilation=64500;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0]=AvatarTemplate;
			//give this avatar object a local id and assign the user a name
			objupdate.ObjectData[0].ID=avd.NetInfo.localID;
			objupdate.ObjectData[0].FullID=avd.NetInfo.AgentID;//new LLUUID("00000000-0000-0000-5665-000000000034");
			objupdate.ObjectData[0].NameValue=enc.GetBytes(avd.NetInfo.name);//enc.GetBytes("FirstName STRING RW SV Test"+ this.local_numer+"\nLastName STRING RW SV User \0");
			libsecondlife.LLVector3 pos2=new LLVector3(avd.Position.X,avd.Position.Y,avd.Position.Z);
			
			byte[] pb=pos2.GetBytes();
						
			Array.Copy(pb,0,objupdate.ObjectData[0].ObjectData,16,pb.Length);
			this.local_numer++;
			
			server.SendPacket(objupdate,true,User_info);
			
			this.SendOtherAppearance(User_info,avd.NetInfo.AgentID);//new LLUUID("00000000-0000-0000-5665-000000000034"));
			
		}
	
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		/// <param name="line"></param>
		public void SendChatMessage(User_Agent_info User_info, string line)
		{
						libsecondlife.Packets.ChatFromSimulatorPacket reply=new ChatFromSimulatorPacket();
              			reply.ChatData.Audible=1;
              			reply.ChatData.Message=enc.GetBytes(line);
              			reply.ChatData.ChatType=1;
              			reply.ChatData.SourceType=1;
              			reply.ChatData.Position=new LLVector3(120,100,21); //should set to actual position
              			reply.ChatData.FromName=enc.GetBytes(User_info.first_name +" "+User_info.last_name +"\0");  //enc.GetBytes("Echo: \0"); //and actual name
              			reply.ChatData.OwnerID=User_info.AgentID;
              			reply.ChatData.SourceID=User_info.AgentID;
              			//echo to sender
              			server.SendPacket(reply,true,User_info);  
              			
              			//send to all users
              			foreach (KeyValuePair<libsecondlife.LLUUID,AvatarData> kp in this.AgentList)
						{
								if(kp.Value.NetInfo.AgentID!=User_info.AgentID)
								{
									server.SendPacket(reply,true,kp.Value.NetInfo);
								}
              			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="user"></param>
		/// <param name="stop"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <param name="av_id"></param>
		/// <param name="body"></param>
		public void SendMoveCommand(User_Agent_info user, bool stop,float x, float y, float z, uint av_id, libsecondlife.LLQuaternion body)
		{
						uint ID=user.localID;
						//ID=av_id;
              			byte[] bytes=new byte[60];
						
              			ImprovedTerseObjectUpdatePacket im=new ImprovedTerseObjectUpdatePacket();
              			im.RegionData.RegionHandle=Globals.Instance.RegionHandle;;
						im.RegionData.TimeDilation=64096;
              			
              			im.ObjectData=new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
              			int i=0;
              			ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat=new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
              			
              			im.ObjectData[0]=dat;
              			
              			dat.TextureEntry=AvatarTemplate.TextureEntry;
              			libsecondlife.LLVector3 pos2=new LLVector3(x,y,z);
              			
              			bytes[i++] = (byte)(ID % 256);
                		bytes[i++] = (byte)((ID >> 8) % 256);
               			bytes[i++] = (byte)((ID >> 16) % 256);
                		bytes[i++] = (byte)((ID >> 24) % 256);
                		
                		bytes[i++]=0;
                		bytes[i++]=1;

                		i+=14;
                		bytes[i++]=128;
                		bytes[i++]=63;
                		byte[] pb=pos2.GetBytes();
							
						Array.Copy(pb,0,bytes,i,pb.Length);
						i+=12;
						ushort ac=32767;
						Axiom.MathLib.Vector3 v3=new Axiom.MathLib.Vector3(1,0,0);
						Axiom.MathLib.Quaternion q=new Axiom.MathLib.Quaternion(body.W,body.X,body.Y,body.Z);
						Axiom.MathLib.Vector3 direc=q*v3;
						direc.Normalize();
						
						direc=direc*(0.03f);
						direc.x+=1;
						direc.y+=1;
						direc.z+=1;
						ushort dx,dy,dz;
						dx=(ushort)(32768*direc.x);
						dy=(ushort)(32768*direc.y);
						dz=(ushort)(32768*direc.z);

						//vel
						if(!stop)
						{
						bytes[i++] = (byte)(dx % 256);
                		bytes[i++] = (byte)((dx >> 8) % 256);
                		
                		bytes[i++] = (byte)(dy % 256);
                		bytes[i++] = (byte)((dy >> 8) % 256);
                		
                		bytes[i++] = (byte)(dz % 256);
                		bytes[i++] = (byte)((dz >> 8) % 256);
						}
						else
						{
						bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
						
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
						}
                		//accel
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		//rot
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		//rotation vel
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		
                		dat.Data=bytes;
                		
                		server.SendPacket(im,true,user);
                		
                		//should send to all users.
                		foreach (KeyValuePair<libsecondlife.LLUUID,AvatarData> kp in this.AgentList)
						{
							if(kp.Value.NetInfo.AgentID!=user.AgentID)
							{
								server.SendPacket(im,true,kp.Value.NetInfo);
							}
                		}
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		/// <param name="lay"></param>
		/// <param name="name"></param>
		public void SendLayerData(User_Agent_info User_info,ref LayerDataPacket lay,string name)
		{
			FileInfo fInfo = new FileInfo(name);

			long numBytes = fInfo.Length;

			FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
			
			BinaryReader br = new BinaryReader(fStream);

			byte [] data1 = br.ReadBytes((int)numBytes);

			br.Close();
			
			fStream.Close();
			lay.LayerData.Data=data1;
			server.SendPacket(lay,true,User_info);
			//System.Console.WriteLine("sent");
		}
	}
	
	public class AvatarData
	{
		public User_Agent_info NetInfo;
		public LLUUID FullID;
		public LLVector3 Position;
		public LLVector3 Velocity=new LLVector3(0,0,0);
		//public LLQuaternion Rotation;
		public bool Walk=false;
		public bool Started=false;
		//public TextureEntry TextureEntry;
		public AvatarWearable[] Wearables; 
		public LLUUID InventoryFolder;
    	public LLUUID BaseFolder;
		
		public AvatarData()
		{
			Wearables=new AvatarWearable[2]; //should be 13
			for(int i=0; i<2; i++)
			{
				Wearables[i]=new AvatarWearable();
			}
		}
	}
	
	public class AvatarWearable
	{
		public LLUUID AssetID;
		public LLUUID ItemID;
		
		public AvatarWearable()
		{
			
		}
	}
	/*
	public class AvatarParams
	{
		public byte[] Params;
		
		public AvatarParams()
		{
		
		}
		
	}
	*/
}
