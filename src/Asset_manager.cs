/*
Copyright (c) OpenSim project, http://osgrid.org/

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
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. */

using System;
using System.Collections.Generic;
using libsecondlife;
using System.Collections;
using libsecondlife.Packets;
using libsecondlife.AssetSystem;
using System.IO;

namespace OpenSim
{
	/// <summary>
	/// Description of Asset_manager.
	/// </summary>
	public class AssetManager
	{
		public Dictionary<libsecondlife.LLUUID,AssetInfo> Assets;
		public ArrayList requests=new ArrayList();  //should change to a generic
	//	public ArrayList uploads=new ArrayList();
		private Server server;
		public TextureManager TextureMan;
		public InventoryManager InventoryManager;
		private  System.Text.Encoding enc = System.Text.Encoding.ASCII;
		
		public AssetManager(Server serve)
		{
			server=serve;
			Assets=new Dictionary<libsecondlife.LLUUID,AssetInfo> ();
			this.initialise();
		}
		
		public void AddRequest(User_Agent_info user, LLUUID asset_id, TransferRequestPacket tran_req)
		{
			Console.WriteLine("Asset Request "+ asset_id);
			if(!this.Assets.ContainsKey(asset_id))
			{
				//not found asset	
				return;
			}
			AssetInfo info=this.Assets[asset_id];
			System.Console.WriteLine("send asset : "+asset_id);
			//for now as it will be only skin or shape request just send back the asset
			TransferInfoPacket Transfer=new TransferInfoPacket();
			Transfer.TransferInfo.ChannelType=2;
			Transfer.TransferInfo.Status=0;
			Transfer.TransferInfo.TargetType=0;
			Transfer.TransferInfo.Params=tran_req.TransferInfo.Params;
			Transfer.TransferInfo.Size=info.data.Length;
			Transfer.TransferInfo.TransferID=tran_req.TransferInfo.TransferID;
			
			server.SendPacket(Transfer,true,user);
			
			TransferPacketPacket tran_p=new TransferPacketPacket();
			tran_p.TransferData.Packet=0;
			tran_p.TransferData.ChannelType=2;
			tran_p.TransferData.TransferID=tran_req.TransferInfo.TransferID;
			if(info.data.Length>1000)  //but needs to be less than 2000 at the moment
			{
				byte[] chunk=new byte[1000];
				Array.Copy(info.data,chunk,1000);
				tran_p.TransferData.Data=chunk;
				tran_p.TransferData.Status=0;
				server.SendPacket(tran_p,true,user);
				
				tran_p=new TransferPacketPacket();
				tran_p.TransferData.Packet=1;
				tran_p.TransferData.ChannelType=2;
				tran_p.TransferData.TransferID=tran_req.TransferInfo.TransferID;
				byte[] chunk1=new byte[(info.data.Length-1000)];
				Array.Copy(info.data,1000,chunk1,0,chunk1.Length);
				tran_p.TransferData.Data=chunk1;
				tran_p.TransferData.Status=1;
				server.SendPacket(tran_p,true,user);
			}
			else
			{
				tran_p.TransferData.Status=1;  //last packet? so set to 1
				tran_p.TransferData.Data=info.data;
				server.SendPacket(tran_p,true,user);
			}
			
		}
		public void CreateNewBaseSet(ref AvatarData Avata,User_Agent_info UserInfo)
		{
			//LLUUID BaseFolder=new LLUUID("4f5f559e-77a0-a4b9-84f9-8c74c07f7cfc");//*/"4fb2dab6-a987-da66-05ee-96ca82bccbf1");
			//LLUUID BaseFolder=new LLUUID("480e2d92-61f6-9f16-f4f5-0f77cfa4f8f9");
			LLUUID BaseFolder=Avata.BaseFolder;
			InventoryManager.CreateNewFolder(UserInfo,Avata.InventoryFolder);
			InventoryManager.CreateNewFolder(UserInfo, BaseFolder);
			
			AssetInfo Base=this.Assets[new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73")];
			AssetInfo Shape=new AssetInfo();
			
			Shape.filename="";
			Shape.data=new byte[Base.data.Length];
			Array.Copy(Base.data,Shape.data,Base.data.Length);
			Shape.Full_ID=LLUUID.Random();
			Shape.Name="Default Skin";
			Shape.Description="Default";
			Shape.InvType=18;
			
			Shape.Type=libsecondlife.AssetSystem.ASSET_TYPE_WEARABLE_BODY;
			byte[] Agentid=enc.GetBytes(UserInfo.AgentID.ToStringHyphenated());
			Array.Copy(Agentid,0,Shape.data,294,Agentid.Length);
			this.Assets.Add(Shape.Full_ID,Shape);
			/*FileStream fStream = new FileStream("Assetshape.dat", FileMode.CreateNew);
			BinaryWriter bw = new BinaryWriter(fStream);
			bw.Write(Shape.data);
			bw.Close();
			fStream.Close();*/
			
			Avata.Wearables[0].ItemID=InventoryManager.AddToInventory(UserInfo,BaseFolder,Shape);
			Avata.Wearables[0].AssetID=Shape.Full_ID;
			//Avata.RootFolder=BaseFolder;
			
			//give test texture
			
			TextureImage Texture=TextureMan.textures[new LLUUID("00000000-0000-0000-5005-000000000005")];
			InventoryManager.AddToInventory(UserInfo,BaseFolder,Texture);
			
		}
		
		private void initialise()
		{
			//for now read in our test image 
			AssetInfo im=new AssetInfo();
			im.filename="base_shape.dat";
			im.Full_ID=new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");		
			this.loadAsset(im);
			this.Assets.Add(im.Full_ID,im);
			
			
			im=new AssetInfo();
			im.filename="base_skin.dat";
			im.Full_ID=new LLUUID("e0ee49b5a4184df8d3c9a65361fe7f49");		
			this.loadAsset(im);
			this.Assets.Add(im.Full_ID,im);
		}
		private void loadAsset(AssetInfo info)
		{
			//should request Asset from storage manager
			//but for now read from file
			
            string data_path = System.AppDomain.CurrentDomain.BaseDirectory + @"\assets\";
			string filename=data_path+@info.filename;
			FileInfo fInfo = new FileInfo(filename);

			long numBytes = fInfo.Length;

			FileStream fStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
			byte[] idata=new byte[numBytes];
			BinaryReader br = new BinaryReader(fStream);
			idata= br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			info.data=idata;
			info.loaded=true;
		}
	}
	
	public class AssetRequest
	{
		public User_Agent_info RequestUser;
		public LLUUID RequestImage;
		public AssetInfo asset_inf;
		public long data_pointer=0;
		public int num_packets=0;
		public int packet_counter=0;
		
		public AssetRequest()
		{
			
		}
	}
	public class AssetInfo:AssetBase
	{
		//public byte[] data;
		//public LLUUID Full_ID;
		public string filename;
		public bool loaded;
		public ulong last_used;  //need to add a tick/time counter and keep record
								// of how often images are requested to unload unused ones.
		
		public AssetInfo()
		{
			
		}
	}
	
	public class AssetBase
	{
		public byte[] data;
		public LLUUID Full_ID;
		public sbyte Type;
		public sbyte InvType;
		public string Name;
		public string Description;
		
		public AssetBase()
		{
			
		}
	}
}
