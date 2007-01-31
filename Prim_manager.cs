/*
Copyright (c) 2007 Michael Wright

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
using libsecondlife.Packets;
using libsecondlife.AssetSystem;
using System.IO;
using Axiom.MathLib;

namespace Second_server
{
	/// <summary>
	/// Description of Prim_manager.
	/// </summary>
	public class Prim_manager
	{
		private Server server;
		public Agent_Manager agent_man;
		
		private uint prim_count;
		
		public libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock object_template;
		public Dictionary<libsecondlife.LLUUID,prim_info> Prim_list;
		public Prim_manager(Server serve)
		{
			server=serve;
			Prim_list=new Dictionary<libsecondlife.LLUUID,prim_info> ();
			this.setuptemplates("objectupate164.dat");
		}
		
		
		//*********************************************************************
		public void create_prim(User_Agent_info User_info, libsecondlife.LLVector3 p1, ObjectAddPacket add_pack)
		{
			ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=1096213093147648;
			objupdate.RegionData.TimeDilation=64096;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
			objupdate.ObjectData[0]=this.object_template;
			objupdate.ObjectData[0].OwnerID=User_info.AgentID;
			objupdate.ObjectData[0].PCode=add_pack.ObjectData.PCode;
			objupdate.ObjectData[0].PathBegin=add_pack.ObjectData.PathBegin;
			objupdate.ObjectData[0].PathEnd=add_pack.ObjectData.PathEnd;
			objupdate.ObjectData[0].PathScaleX=add_pack.ObjectData.PathScaleX;
			objupdate.ObjectData[0].PathScaleY=add_pack.ObjectData.PathScaleY;
			objupdate.ObjectData[0].PathShearX=add_pack.ObjectData.PathShearX;
			objupdate.ObjectData[0].PathShearY=add_pack.ObjectData.PathShearY;
			objupdate.ObjectData[0].PathSkew=add_pack.ObjectData.PathSkew;
			objupdate.ObjectData[0].ProfileBegin=add_pack.ObjectData.ProfileBegin;
			objupdate.ObjectData[0].ProfileEnd=add_pack.ObjectData.ProfileEnd;
			objupdate.ObjectData[0].Scale=add_pack.ObjectData.Scale;//new LLVector3(1,1,1);
			objupdate.ObjectData[0].PathCurve=add_pack.ObjectData.PathCurve;
			objupdate.ObjectData[0].ProfileCurve=add_pack.ObjectData.ProfileCurve;
			objupdate.ObjectData[0].ParentID=0;
			objupdate.ObjectData[0].ProfileHollow=add_pack.ObjectData.ProfileHollow;
			//finish off copying rest of shape data
			
			objupdate.ObjectData[0].ID=(uint)(702000+prim_count);
			objupdate.ObjectData[0].FullID=new LLUUID("edba7151-5857-acc5-b30b-f01efefda"+prim_count.ToString("000"));
			
			//update position
			byte[] pb=p1.GetBytes();		
			Array.Copy(pb,0,objupdate.ObjectData[0].ObjectData,0,pb.Length);
			
			prim_count++;
			server.SendPacket(objupdate,true,User_info);
			
			//should send to all users
			foreach (KeyValuePair<libsecondlife.LLUUID,Avatar_data> kp in agent_man.Agent_list)
			{
				if(kp.Value.Net_info.AgentID!=User_info.AgentID)
				{
					server.SendPacket(objupdate,true,kp.Value.Net_info);
				}
            }
			//should store this infomation 
			prim_info n_prim=new prim_info();
			n_prim.full_ID=objupdate.ObjectData[0].FullID;
			n_prim.local_ID=objupdate.ObjectData[0].ID;
			n_prim.pos=p1;
			
			this.Prim_list.Add(n_prim.full_ID,n_prim);
			
			//store rest of data
			
		}
		public void update_prim_position(User_Agent_info user,float x, float y, float z,uint l_id)
		{
						prim_info pri=null;
						foreach (KeyValuePair<libsecondlife.LLUUID,prim_info> kp in this.Prim_list)
						{
							if(kp.Value.local_ID==l_id)
							{
								pri=kp.Value;
							}
						}
						if(pri==null)
						{
							return;
						}
						uint ID=pri.local_ID;
              			byte[] bytes=new byte[60];
						
              			ImprovedTerseObjectUpdatePacket im=new ImprovedTerseObjectUpdatePacket();
              			im.RegionData.RegionHandle=1096213093147648;
						im.RegionData.TimeDilation=64096;
              			im.ObjectData=new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
              			int i=0;
              			ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat=new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
              			im.ObjectData[0]=dat;
              			dat.TextureEntry=object_template.TextureEntry;
              			libsecondlife.LLVector3 pos2=new LLVector3(x,y,z);
              			
              			bytes[i++] = (byte)(ID % 256);
                		bytes[i++] = (byte)((ID >> 8) % 256);
               			bytes[i++] = (byte)((ID >> 16) % 256);
                		bytes[i++] = (byte)((ID >> 24) % 256);
                		bytes[i++]=0;
                		bytes[i++]=0;//1;

                	//	i+=14;
                	//	bytes[i++]=128;
                	//	bytes[i++]=63;
                		byte[] pb=pos2.GetBytes();
                		pri.pos=pos2;
						Array.Copy(pb,0,bytes,i,pb.Length);
						i+=12;
						ushort ac=32767;

						//vel
						bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
                		bytes[i++] = (byte)(ac % 256);
                		bytes[i++] = (byte)((ac >> 8) % 256);
						
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
                		//server.SendPacket(im,true,user);
                		//should send to all users.
                		foreach (KeyValuePair<libsecondlife.LLUUID,Avatar_data> kp in agent_man.Agent_list)
						{
							if(kp.Value.Net_info.AgentID!=user.AgentID)
							{
								server.SendPacket(im,true,kp.Value.Net_info);
							}
                		}
		}
		public void send_existing_prims(User_Agent_info user)
		{
			//send data for already created prims to a new joining user
		}
		//**************************************************************
		public void setuptemplates(string name)
		{
			ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle=1096213093147648;
			objupdate.RegionData.TimeDilation=64096;
			objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
				
			int i=0;
			FileInfo fInfo = new FileInfo(name);
			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fStream);
			byte [] data1 = br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			
			libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1,ref i);
			objupdate.ObjectData[0]=objdata;
			this.object_template=objdata;
			objdata.UpdateFlags=objdata.UpdateFlags+12-16+32+256;
			objdata.OwnerID=new LLUUID("00000000-0000-0000-0000-000000000000");
			//test adding a new texture to object , to test image downloading
			LLObject.TextureEntry te=new LLObject.TextureEntry(objdata.TextureEntry,0,objdata.TextureEntry.Length);
			te.DefaultTexture.TextureID=new LLUUID("00000000-0000-0000-5005-000000000005");
			
			LLObject.TextureEntry ntex=new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
			
			objdata.TextureEntry=ntex.ToBytes();
		}
		//********************************************************************
		public void Read_Prim_database(string name,User_Agent_info user)
	    {
			StreamReader SR;
    		string line;
    		SR=File.OpenText(name);
			string [] comp= new string[10];
			string delimStr = " ,	";		
            char [] delimiter = delimStr.ToCharArray();
            
            line=SR.ReadLine();
            while(line!="end")
            {
              comp=line.Split(delimiter);  
              if(comp[0]=="ObjPack"){
              	int num=Convert.ToInt32(comp[2]);
              	int start=Convert.ToInt32(comp[1]);
				ObjectUpdatePacket objupdate=new ObjectUpdatePacket();
				objupdate.RegionData.RegionHandle=1096213093147648;
				objupdate.RegionData.TimeDilation=64096;
				objupdate.ObjectData=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[num];
				
			 //	int count=0;
			 string data_path=System.Windows.Forms.Application.StartupPath + @"\data\";
			 for(int cc=0; cc<num; cc++)
			 {
			 	string filenam=data_path+@"prim_updates"+start+".dat";
				int i=0;
				//FileInfo fInfo = new FileInfo("objectupate"+start+".dat");
				FileInfo fInfo = new FileInfo(filenam);
				long numBytes = fInfo.Length;
				//FileStream fStream = new FileStream("objectupate"+start+".dat", FileMode.Open, FileAccess.Read);
				FileStream fStream = new FileStream(filenam, FileMode.Open, FileAccess.Read);
				BinaryReader br = new BinaryReader(fStream);
				byte [] data1 = br.ReadBytes((int)numBytes);
				br.Close();
				fStream.Close();
			
				libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata=new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1,ref i);
				objupdate.ObjectData[cc]=objdata;
				start++;		
			 }
			server.SendPacket(objupdate,true,user);
			line=SR.ReadLine();
            }
            }
            SR.Close();
	    }
	}
	
	public class prim_info
	{
		public LLVector3 pos;
		public LLVector3 vel;
		public uint local_ID;
		public LLUUID full_ID;
		public prim_data data;
		
		public prim_info()
		{
			pos=new LLVector3(0,0,0);
			vel=new LLVector3(0,0,0);
			data=new prim_data();
		}
	}
	public class prim_data
	{
		public prim_data()
		{
			
		}
	}
}
