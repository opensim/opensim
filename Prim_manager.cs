/*
Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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

namespace OpenSim
{
	/// <summary>
	/// Description of Prim_manager.
	/// </summary>
	public class PrimManager
	{
		private Server _server;
		private uint _primCount;
		
		public AgentManager AgentManagement;
		public libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock PrimTemplate;
		public Dictionary<libsecondlife.LLUUID,PrimInfo> PrimList;
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="serve"></param>
		public PrimManager(Server server)
		{
			_server = server;
			PrimList = new Dictionary<libsecondlife.LLUUID,PrimInfo> ();
			this.SetupTemplates("objectupate164.dat");
			
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		/// <param name="p1"></param>
		/// <param name="add_pack"></param>
		public void CreatePrim(UserAgentInfo userInfo, libsecondlife.LLVector3 pos1, ObjectAddPacket addPacket)
		{
			ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle = Globals.Instance.RegionHandle;
			objupdate.RegionData.TimeDilation = 64096;
			
			objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			PrimData PData = new PrimData();
			objupdate.ObjectData[0] = this.PrimTemplate;
			PData.OwnerID=objupdate.ObjectData[0].OwnerID = userInfo.AgentID;
			PData.PCode=objupdate.ObjectData[0].PCode = addPacket.ObjectData.PCode;
			PData.PathBegin=objupdate.ObjectData[0].PathBegin = addPacket.ObjectData.PathBegin;
			PData.PathEnd=objupdate.ObjectData[0].PathEnd = addPacket.ObjectData.PathEnd;
			PData.PathScaleX=objupdate.ObjectData[0].PathScaleX = addPacket.ObjectData.PathScaleX;
			PData.PathScaleY=objupdate.ObjectData[0].PathScaleY = addPacket.ObjectData.PathScaleY;
			PData.PathShearX=objupdate.ObjectData[0].PathShearX = addPacket.ObjectData.PathShearX;
			PData.PathShearY=objupdate.ObjectData[0].PathShearY = addPacket.ObjectData.PathShearY;
			PData.PathSkew=objupdate.ObjectData[0].PathSkew = addPacket.ObjectData.PathSkew;
			PData.ProfileBegin=objupdate.ObjectData[0].ProfileBegin = addPacket.ObjectData.ProfileBegin;
			PData.ProfileEnd=objupdate.ObjectData[0].ProfileEnd = addPacket.ObjectData.ProfileEnd;
			PData.Scale=objupdate.ObjectData[0].Scale = addPacket.ObjectData.Scale;
			PData.PathCurve=objupdate.ObjectData[0].PathCurve = addPacket.ObjectData.PathCurve;
			PData.ProfileCurve=objupdate.ObjectData[0].ProfileCurve = addPacket.ObjectData.ProfileCurve;
			PData.ParentID=objupdate.ObjectData[0].ParentID = 0;
			PData.ProfileHollow=objupdate.ObjectData[0].ProfileHollow = addPacket.ObjectData.ProfileHollow;
			//finish off copying rest of shape data
			
			objupdate.ObjectData[0].ID = (uint)(702000 + _primCount);
			objupdate.ObjectData[0].FullID = new LLUUID("edba7151-5857-acc5-b30b-f01efefda"+_primCount.ToString("000"));
			
			//update position
			byte[] pb = pos1.GetBytes();		
			Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 0, pb.Length);
			
			_primCount++;
			_server.SendPacket(objupdate, true, userInfo);
			
			//should send to all users
			foreach (KeyValuePair<libsecondlife.LLUUID, AvatarData> kp in AgentManagement.AgentList)
			{
				if(kp.Value.NetInfo.AgentID != userInfo.AgentID)
				{
					_server.SendPacket(objupdate, true, kp.Value.NetInfo);
				}
            }
			//should store this infomation 
			PrimInfo NewPrim = new PrimInfo();
			NewPrim.FullID = objupdate.ObjectData[0].FullID;
			NewPrim.LocalID = objupdate.ObjectData[0].ID;
			NewPrim.Position = pos1;
			NewPrim.Data = PData;
			
			this.PrimList.Add(NewPrim.FullID, NewPrim);
			
			//store rest of data
			
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User"></param>
		/// <param name="position"></param>
		/// <param name="LocalID"></param>
		/// <param name="setRotation"></param>
		/// <param name="rotation"></param>
		public void UpdatePrimPosition(UserAgentInfo userInfo, LLVector3 position, uint localID, bool setRotation, LLQuaternion rotation)
		{
			PrimInfo pri = null;
			foreach (KeyValuePair<libsecondlife.LLUUID,PrimInfo> kp in this.PrimList)
			{
				if(kp.Value.LocalID == localID)
				{
					pri = kp.Value;
				}
			}
			if(pri == null)
			{
				return;
			}
			uint ID = pri.LocalID;
			libsecondlife.LLVector3 pos2 = new LLVector3(position.X, position.Y, position.Z);
			libsecondlife.LLQuaternion rotation2;
			if(!setRotation)
			{
				pri.Position = pos2;
				rotation2 = new LLQuaternion(pri.Rotation.X, pri.Rotation.Y, pri.Rotation.Z, pri.Rotation.W);
			}
			else
			{
				rotation2=new LLQuaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
				pos2 = pri.Position;
				pri.Rotation = rotation;
			}
			rotation2.W += 1;
			rotation2.X += 1;
			rotation2.Y += 1;
			rotation2.Z += 1;
			
			byte[] bytes = new byte[60];
			
			ImprovedTerseObjectUpdatePacket im = new ImprovedTerseObjectUpdatePacket();
			im.RegionData.RegionHandle = Globals.Instance.RegionHandle;
			im.RegionData.TimeDilation = 64096;
			im.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
			int i = 0;
			ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
			im.ObjectData[0] = dat;
			dat.TextureEntry = PrimTemplate.TextureEntry;
			
			bytes[i++] = (byte)(ID % 256);
			bytes[i++] = (byte)((ID >> 8) % 256);
			bytes[i++] = (byte)((ID >> 16) % 256);
			bytes[i++] = (byte)((ID >> 24) % 256);
			bytes[i++]= 0;
			bytes[i++]= 0;

			byte[] pb = pos2.GetBytes();
			pri.Position = pos2;
			Array.Copy(pb, 0, bytes, i, pb.Length);
			i += 12;
			ushort ac = 32767;

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
			
			ushort rw, rx,ry,rz;
			rw = (ushort)(32768 * rotation2.W);
			rx = (ushort)(32768 * rotation2.X);
			ry = (ushort)(32768 * rotation2.Y);
			rz = (ushort)(32768 * rotation2.Z);
			
			//rot
			bytes[i++] = (byte)(rx % 256);
			bytes[i++] = (byte)((rx >> 8) % 256);
			bytes[i++] = (byte)(ry % 256);
			bytes[i++] = (byte)((ry >> 8) % 256);
			bytes[i++] = (byte)(rz % 256);
			bytes[i++] = (byte)((rz >> 8) % 256);
			bytes[i++] = (byte)(rw % 256);
			bytes[i++] = (byte)((rw >> 8) % 256);
			
			//rotation vel
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			dat.Data=bytes;
			
			foreach (KeyValuePair<libsecondlife.LLUUID,AvatarData> kp in AgentManagement.AgentList)
			{
				if(kp.Value.NetInfo.AgentID!=userInfo.AgentID)
				{
					_server.SendPacket(im, true, kp.Value.NetInfo);
				}
            }
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="user"></param>
		public void SendExistingPrims(UserAgentInfo userInfo)
		{
			//send data for already created prims to a new joining user
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		public void SetupTemplates(string name)
		{
			ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
			objupdate.RegionData.RegionHandle = Globals.Instance.RegionHandle;
			objupdate.RegionData.TimeDilation = 64096;
			objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
				
			int i = 0;
			FileInfo fInfo = new FileInfo(name);
			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fStream);
			byte [] data1 = br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			
			libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1,ref i);
			objupdate.ObjectData[0] = objdata;
			this.PrimTemplate = objdata;
			objdata.UpdateFlags = objdata.UpdateFlags + 12 - 16 + 32 + 256;
			objdata.OwnerID = new LLUUID("00000000-0000-0000-0000-000000000000");
			//test adding a new texture to object , to test image downloading
			LLObject.TextureEntry te = new LLObject.TextureEntry(objdata.TextureEntry, 0, objdata.TextureEntry.Length);
			te.DefaultTexture.TextureID = new LLUUID("00000000-0000-0000-5005-000000000005");
			
			LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
			
			objdata.TextureEntry = ntex.ToBytes();
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="user"></param>
		public void ReadPrimDatabase(string name, UserAgentInfo userInfo)
	    {
			StreamReader SR;
			string line;
			SR=File.OpenText(name);
			string [] comp = new string[10];
			string delimStr = " ,	";
			char [] delimiter = delimStr.ToCharArray();
			
			line=SR.ReadLine();
			while(line != "end")
			{
				comp = line.Split(delimiter);
				if(comp[0] == "ObjPack")
				{
					int num = Convert.ToInt32(comp[2]);
					int start = Convert.ToInt32(comp[1]);
					ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
					objupdate.RegionData.RegionHandle = Globals.Instance.RegionHandle;
					objupdate.RegionData.TimeDilation = 64096;
					objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[num];
					
					//	int count=0;
					string data_path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, @"data");
					for(int cc = 0; cc < num; cc++)
					{
						string filenam = System.IO.Path.Combine(data_path, @"prim_updates"+start+".dat");
						int i = 0;
						//FileInfo fInfo = new FileInfo("objectupate"+start+".dat");
						FileInfo fInfo = new FileInfo(filenam);
						long numBytes = fInfo.Length;
						//FileStream fStream = new FileStream("objectupate"+start+".dat", FileMode.Open, FileAccess.Read);
						FileStream fStream = new FileStream(filenam, FileMode.Open, FileAccess.Read);
						BinaryReader br = new BinaryReader(fStream);
						byte [] data1 = br.ReadBytes((int)numBytes);
						br.Close();
						fStream.Close();
						
						libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);
						objupdate.ObjectData[cc] = objdata;
						start++;
					}
					_server.SendPacket(objupdate, true, userInfo);
					line = SR.ReadLine();
				}
			}
			SR.Close();
	    }
	}
	
	public class PrimInfo
	{
		public LLVector3 Position;
		public LLVector3 Velocity;
		public LLQuaternion Rotation=LLQuaternion.Identity;
		public uint LocalID;
		public LLUUID FullID;
		public PrimData Data;
		
		public PrimInfo()
		{
			Position=new LLVector3(0,0,0);
			Velocity=new LLVector3(0,0,0);
			//data=new PrimData();
		}
	}
	public class PrimData
	{
		public LLUUID OwnerID;
		public byte PCode;
		public byte PathBegin;
		public byte PathEnd;
		public byte PathScaleX;
		public byte PathScaleY;
		public byte PathShearX;
		public byte PathShearY;
		public sbyte PathSkew;
		public byte ProfileBegin;
		public byte ProfileEnd;
		public LLVector3 Scale;
		public byte PathCurve;
		public byte ProfileCurve;
		public uint ParentID=0;
		public byte ProfileHollow;
		
		public bool DataBaseStorage=false;
		
		public PrimData()
		{
			
		}
	}
}
