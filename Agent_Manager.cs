/*
Copyright (c) 2007 Michael Wright
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

namespace Second_server
{
	/// <summary>
	/// Description of Agent_Manager.
	/// </summary>
	public class Agent_Manager
	{
		public Dictionary<libsecondlife.LLUUID,Avatar_data> Agent_list;
		//public uint number_agents=0;
		private uint local_numer=0;
		private Server server;
		public  Prim_manager prim_man;
		private byte [] data1;
		
		private libsecondlife.Packets.RegionHandshakePacket reg;
		private  System.Text.Encoding enc = System.Text.Encoding.ASCII;
		public libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock avatar_template;
		//private int appc=0;
		
		public Agent_Manager(Server serve)
		{
			Agent_list=new Dictionary<libsecondlife.LLUUID,Avatar_data>();
			server=serve;
			this.initialise();
		}
		//***************************************************
		public Avatar_data Get_Agent(LLUUID id)
		{
			
			
			if(!this.Agent_list.ContainsKey(id))
			{
				return null;
			}
			else
			{
				Avatar_data ad=this.Agent_list[id];
				return ad;
			}
		}
		
		public void Add_Agent(Avatar_data agent)
		{
			this.Agent_list.Add(agent.Full_ID,agent);
		}
		
		public bool New_Agent(User_Agent_info User_info)
		{
			Avatar_data ad=new Avatar_data();
			ad.Full_ID=User_info.AgentID;
			ad.Net_info=User_info;
			ad.pos=new LLVector3(100,100,22);
			this.Agent_list.Add(ad.Full_ID,ad);
			return(true);
		}
		
		public void Agent_join(User_Agent_info User_info)
		{
			//send region data 
			server.SendPacket(reg,true,User_info);
			
			//inform client of join comlete
			libsecondlife.Packets.AgentMovementCompletePacket mov=new AgentMovementCompletePacket();
			mov.AgentData.SessionID=User_info.SessionID;
			mov.AgentData.AgentID=User_info.AgentID;
			mov.Data.RegionHandle=1096213093147648;
			mov.Data.Timestamp=1169838966;
			mov.Data.Position=new LLVector3(100f,100f,22f);
			mov.Data.LookAt=new LLVector3(0.99f,0.042f,0);
			server.SendPacket(mov,true,User_info);
		}
		public void tick()
		{
			//update positions
			foreach (KeyValuePair<libsecondlife.LLUUID,Avatar_data> kp in this.Agent_list)
			{
				
				kp.Value.pos.X+=(kp.Value.vel.X*0.2f);
				kp.Value.pos.Y+=(kp.Value.vel.Y*0.2f);
				kp.Value.pos.Z+=(kp.Value.vel.Z*0.2f);
			}
		}
		//**************************************************************
		private void initialise()
		{
				//Region data
				reg=new RegionHandshakePacket();
				reg.RegionInfo.BillableFactor=0;
				reg.RegionInfo.IsEstateManager=false;
				reg.RegionInfo.TerrainHeightRange00=60;
				reg.RegionInfo.TerrainHeightRange01=60;
				reg.RegionInfo.TerrainHeightRange10=60;
				reg.RegionInfo.TerrainHeightRange11=60;
				reg.RegionInfo.TerrainStartHeight00=20;
				reg.RegionInfo.TerrainStartHeight01=20;
				reg.RegionInfo.TerrainStartHeight10=20;
				reg.RegionInfo.TerrainStartHeight11=20;
				reg.RegionInfo.SimAccess=13;
				reg.RegionInfo.WaterHeight=5;
				reg.RegionInfo.RegionFlags=72458694;
				reg.RegionInfo.SimName=enc.GetBytes( "Test Sandbox\0");
				reg.RegionInfo.SimOwner=new LLUUID("00000000-0000-0000-0000-000000000000");
				reg.RegionInfo.TerrainBase0=new LLUUID("b8d3965a-ad78-bf43-699b-bff8eca6c975");
				reg.RegionInfo.TerrainBase1=new LLUUID("abb783e6-3e93-26c0-248a-247666855da3");
				reg.RegionInfo.TerrainBase2=new LLUUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
				reg.RegionInfo.TerrainBase3=new LLUUID("beb169c7-11ea-fff2-efe5-0f24dc881df2");
				reg.RegionInfo.TerrainDetail0=new LLUUID("00000000-0000-0000-0000-000000000000");
				reg.RegionInfo.TerrainDetail1=new LLUUID("00000000-0000-0000-0000-000000000000");
				reg.RegionInfo.TerrainDetail2=new LLUUID("00000000-0000-0000-0000-000000000000");
				reg.RegionInfo.TerrainDetail3=new LLUUID("00000000-0000-0000-0000-000000000000");
				reg.RegionInfo.CacheID=new LLUUID("545ec0a5-5751-1026-8a0b-216e38a7ab33");
		
				this.setuptemplate("objectupate168.dat");
		}
		
		public void setuptemplate(string name)
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
			
			avatar_template=objdata;
				
		}
		//**********************************************************
		public void send_intial_data(User_Agent_info User_info)
		{
			
			//shouldn't really have to read all this in from disk for every new client?
				 string data_path=System.AppDomain.CurrentDomain.BaseDirectory + @"\layer_data\";
			
				//send layerdata
				LayerDataPacket layerpack=new LayerDataPacket();
				layerpack.LayerID.Type=76;
				//layerpack.LayerData.ReadfromFile(data_path+@"layerdata0.dat");
				this.read_layerdata(User_info,ref layerpack,data_path+@"layerdata0.dat");
				
				//server.SendPacket(layerpack,true,User_info);
				
				LayerDataPacket layerpack1=new LayerDataPacket();
				layerpack1.LayerID.Type=76;
				//layerpack1.LayerData.ReadfromFile(data_path+@"layerdata1.dat");
				this.read_layerdata(User_info,ref layerpack,data_path+@"layerdata1.dat");
				//server.SendPacket(layerpack1,true,User_info);
				
				LayerDataPacket layerpack2=new LayerDataPacket();
				layerpack2.LayerID.Type=56;
				//layerpack2.LayerData.ReadfromFile(data_path+@"layerdata2.dat");
				this.read_layerdata(User_info,ref layerpack,data_path+@"layerdata2.dat");
				//server.SendPacket(layerpack2,true,User_info);
				
				LayerDataPacket layerpack3=new LayerDataPacket();
				layerpack3.LayerID.Type=55;
				//layerpack3.LayerData.ReadfromFile(data_path+@"layerdata3.dat");
				this.read_layerdata(User_info,ref layerpack,data_path+@"layerdata3.dat");
				//server.SendPacket(layerpack3,true,User_info);
				
				LayerDataPacket layerpack4=new LayerDataPacket();
				layerpack4.LayerID.Type=56;
				//layerpack4.LayerData.ReadfromFile(data_path+@"layerdata4.dat");
				this.read_layerdata(User_info,ref layerpack,data_path+@"layerdata4.dat");
				//server.SendPacket(layerpack4,true,User_info);
				
				LayerDataPacket layerpack5=new LayerDataPacket();
				layerpack5.LayerID.Type=55;
				//layerpack5.LayerData.ReadfromFile(data_path+@"layerdata5.dat");
				this.read_layerdata(User_info,ref layerpack,data_path+@"layerdata5.dat");
				//server.SendPacket(layerpack5,true,User_info);
				
				
				//send intial set of captured prims data?
				this.prim_man.Read_Prim_database( "objectdatabase.ini",User_info);			
				
				//send prims that have been created by users
				//prim_man.send_existing_prims(User_info);
				
				//send update about clients avatar
				this.send_intial_avatar_position(User_info);
				
				//send updates about all other users
				//this.send_test_avatar_position(User_info);
				foreach (KeyValuePair<libsecondlife.LLUUID,Avatar_data> kp in this.Agent_list)
				{
					if(kp.Value.Net_info.AgentID!=User_info.AgentID)
					{
						this.send_other_avatar_position(User_info,kp.Value);
					}
				}
				
				
		}
		public void send_intial_avatar_position(User_Agent_info User_info)
		{
			//send a objectupdate packet with information about the clients avatar
			ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=1096213093147648;
			objupdate.RegionData.TimeDilation=64096;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0]=avatar_template;
			//give this avatar object a local id and assign the user a name
			objupdate.ObjectData[0].ID=8880000+this.local_numer;
			User_info.localID=objupdate.ObjectData[0].ID;
			//User_info.name="Test"+this.local_numer+" User";
			this.Get_Agent(User_info.AgentID).started=true;
			objupdate.ObjectData[0].FullID=User_info.AgentID;
			objupdate.ObjectData[0].NameValue=enc.GetBytes("FirstName STRING RW SV Test"+ this.local_numer+"\nLastName STRING RW SV User \0");
			User_info.name="FirstName STRING RW SV Test"+ this.local_numer+"\nLastName STRING RW SV User \0";
			User_info.last_name="User";
			User_info.first_name="Test"+this.local_numer;
			libsecondlife.LLVector3 pos2=new LLVector3(100f,100.0f,22.0f);
			
			byte[] pb=pos2.GetBytes();
						
			Array.Copy(pb,0,objupdate.ObjectData[0].ObjectData,16,pb.Length);
			this.local_numer++;
			
			server.SendPacket(objupdate,true,User_info);
			
			//send this info to other existing clients
			foreach (KeyValuePair<libsecondlife.LLUUID,Avatar_data> kp in this.Agent_list)
			{
					if(kp.Value.Net_info.AgentID!=User_info.AgentID)
					{
						server.SendPacket(objupdate,true,kp.Value.Net_info);
						this.send_other_apper(kp.Value.Net_info,objupdate.ObjectData[0].FullID);
					}
			}
		
		}
		public void send_intial_avatar_apper(User_Agent_info user)
		{
			
			//seems that we don't send a avatarapperance for ourself.
			/*AvatarAppearancePacket avp=new AvatarAppearancePacket();
			
			avp.VisualParam=new AvatarAppearancePacket.VisualParamBlock[218];
			avp.ObjectData.TextureEntry=this.avatar_template.TextureEntry;// br.ReadBytes((int)numBytes);
			
			AvatarAppearancePacket.VisualParamBlock avblock=null;
			for(int i=0; i<218; i++)
			{
				avblock=new AvatarAppearancePacket.VisualParamBlock();
				avblock.ParamValue=(byte)100;
				avp.VisualParam[i]=avblock;
			}
			
			avp.Sender.IsTrial=false;
			avp.Sender.ID=user.AgentID;
			*/
	
			AgentWearablesUpdatePacket aw=new AgentWearablesUpdatePacket();
			aw.AgentData.AgentID=user.AgentID;
			aw.AgentData.SerialNum=0;//(uint)appc;
			//appc++;
			aw.AgentData.SessionID=user.SessionID;//new LLUUID("00000000-0000-0000-0000-000000000000");//user.SessionID;
			
			aw.WearableData= new AgentWearablesUpdatePacket.WearableDataBlock[13];
			AgentWearablesUpdatePacket.WearableDataBlock awb=null;
			awb=new AgentWearablesUpdatePacket.WearableDataBlock();
				awb.WearableType=(byte)0;
				awb.AssetID=new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
				//awb.ItemID=new LLUUID("b7878000-0000-0000-0000-000000000000");
				awb.ItemID=new LLUUID("b7878441893b094917f791174bc8401c");
				//awb.ItemID=new LLUUID("00000000-0000-0000-0000-000000000000");
				aw.WearableData[0]=awb;
				
				
				/*awb=new AgentWearablesUpdatePacket.WearableDataBlock();
				awb.WearableType=(byte)1;
				awb.AssetID=new LLUUID("e0ee49b5a4184df8d3c9a65361fe7f49");
				awb.ItemID=new LLUUID("193f0876fc11d143797454352f9c9c26");
				//awb.ItemID=new LLUUID("00000000-0000-0000-0000-000000000000");
				aw.WearableData[1]=awb;*/
				
				
			for(int i=1; i<13; i++)
			{
				awb=new AgentWearablesUpdatePacket.WearableDataBlock();
				awb.WearableType=(byte)i;
				awb.AssetID=new LLUUID("00000000-0000-0000-0000-000000000000");
				awb.ItemID=new LLUUID("00000000-0000-0000-0000-000000000000");
				aw.WearableData[i]=awb;
			}
			
			//server.SendPacket(avp,true,user); 
			server.SendPacket(aw,true,user);
			//System.Console.WriteLine(avp);
			
			
		}
		public void send_other_apper(User_Agent_info user,LLUUID id)
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
		
		public void send_test_avatar_position(User_Agent_info User_info)
		{
			//send a objectupdate packet with information about the clients avatar
			ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=1096213093147648;
			objupdate.RegionData.TimeDilation=64500;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0]=avatar_template;
			//give this avatar object a local id and assign the user a name
			objupdate.ObjectData[0].ID=8880000+this.local_numer;
			objupdate.ObjectData[0].FullID=new LLUUID("00000000-0000-0000-5665-000000000034");
			objupdate.ObjectData[0].NameValue=enc.GetBytes("FirstName STRING RW SV Test"+ this.local_numer+"\nLastName STRING RW SV User \0");
			libsecondlife.LLVector3 pos2=new LLVector3(120f,120.0f,22.0f);
			
			byte[] pb=pos2.GetBytes();
						
			Array.Copy(pb,0,objupdate.ObjectData[0].ObjectData,16,pb.Length);
			this.local_numer++;
			
			server.SendPacket(objupdate,true,User_info);
			
			this.send_other_apper(User_info,new LLUUID("00000000-0000-0000-5665-000000000034"));
			
		}
		
		public void send_other_avatar_position(User_Agent_info User_info, Avatar_data avd)
		{
			//send a objectupdate packet with information about the clients avatar
			ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=1096213093147648;
			objupdate.RegionData.TimeDilation=64500;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0]=avatar_template;
			//give this avatar object a local id and assign the user a name
			objupdate.ObjectData[0].ID=avd.Net_info.localID;
			objupdate.ObjectData[0].FullID=avd.Net_info.AgentID;//new LLUUID("00000000-0000-0000-5665-000000000034");
			objupdate.ObjectData[0].NameValue=enc.GetBytes(avd.Net_info.name);//enc.GetBytes("FirstName STRING RW SV Test"+ this.local_numer+"\nLastName STRING RW SV User \0");
			libsecondlife.LLVector3 pos2=new LLVector3(avd.pos.X,avd.pos.Y,avd.pos.Z);
			
			byte[] pb=pos2.GetBytes();
						
			Array.Copy(pb,0,objupdate.ObjectData[0].ObjectData,16,pb.Length);
			this.local_numer++;
			
			server.SendPacket(objupdate,true,User_info);
			
			this.send_other_apper(User_info,avd.Net_info.AgentID);//new LLUUID("00000000-0000-0000-5665-000000000034"));
			
		}
		//*************************************************************
		public void send_chat_message(User_Agent_info User_info, string line)
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
              			foreach (KeyValuePair<libsecondlife.LLUUID,Avatar_data> kp in this.Agent_list)
						{
								if(kp.Value.Net_info.AgentID!=User_info.AgentID)
								{
									server.SendPacket(reply,true,kp.Value.Net_info);
								}
              			}
		}
		//*************************************************************
		public void send_move_command(User_Agent_info user, bool stop,float x, float y, float z, uint av_id, libsecondlife.LLQuaternion body)
		{
						uint ID=user.localID;
						//ID=av_id;
              			byte[] bytes=new byte[60];
						
              			ImprovedTerseObjectUpdatePacket im=new ImprovedTerseObjectUpdatePacket();
              			im.RegionData.RegionHandle=1096213093147648;
						im.RegionData.TimeDilation=64096;
              			
              			im.ObjectData=new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
              			int i=0;
              			ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat=new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
              			
              			im.ObjectData[0]=dat;
              			
              			dat.TextureEntry=avatar_template.TextureEntry;
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
                		foreach (KeyValuePair<libsecondlife.LLUUID,Avatar_data> kp in this.Agent_list)
						{
							if(kp.Value.Net_info.AgentID!=user.AgentID)
							{
								server.SendPacket(im,true,kp.Value.Net_info);
							}
                		}
		}
		//*************************************************************
		public void read_layerdata(User_Agent_info User_info,ref LayerDataPacket lay,string name)
		{
			FileInfo fInfo = new FileInfo(name);

			long numBytes = fInfo.Length;

			FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
			
			BinaryReader br = new BinaryReader(fStream);

			 data1 = br.ReadBytes((int)numBytes);

			br.Close();
			
			fStream.Close();
			lay.LayerData.Data=data1;
			server.SendPacket(lay,true,User_info);
			//System.Console.WriteLine("sent");
		}
	}
	
	public class Avatar_data
	{
		public User_Agent_info Net_info;
		public LLUUID Full_ID;
		public LLVector3 pos;
		public LLVector3 vel=new LLVector3(0,0,0);
		public bool walk=false;
		public bool started=false;
		
		public Avatar_data()
		{
			
		}
	}
}
