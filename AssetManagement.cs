/*
 * 
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
*
 */

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
	/// Asset and Image management
	/// </summary>
	public class AssetManagement
	{
		public Dictionary<libsecondlife.LLUUID,AssetInfo> Assets;
		public Dictionary<libsecondlife.LLUUID,TextureImage> Textures;
		
		public ArrayList AssetRequests = new ArrayList();  //should change to a generic
		public ArrayList TextureRequests = new ArrayList(); 	
		//public ArrayList uploads=new ArrayList();
		private Server _server;
		private InventoryManager _inventoryManager;
		private System.Text.Encoding _enc = System.Text.Encoding.ASCII;
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="_server"></param>
		public AssetManagement(Server server, InventoryManager inventoryManager)
		{
			this._server = server;
			this._inventoryManager = inventoryManager;
			Textures = new Dictionary<libsecondlife.LLUUID,TextureImage> ();
			Assets = new Dictionary<libsecondlife.LLUUID,AssetInfo> ();
			this.initialise();
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void initialise()
		{
			//Shape and skin base assets
			AssetInfo Asset = new AssetInfo();
			Asset.filename = "base_shape.dat";
			Asset.FullID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");		
			this.LoadAsset(Asset, false);
			this.Assets.Add(Asset.FullID, Asset);
			
			Asset = new AssetInfo();
			Asset.filename = "base_skin.dat";
			Asset.FullID = new LLUUID("e0ee49b5a4184df8d3c9a65361fe7f49");		
			this.LoadAsset(Asset, false);
			this.Assets.Add(Asset.FullID, Asset);
			
			//our test images
			//Change these filenames to images you want to use. 
			TextureImage Image = new TextureImage();
			Image.filename = "testpic2.jp2";
			Image.FullID = new LLUUID("00000000-0000-0000-5005-000000000005");
			Image.Name = "test Texture";
			this.LoadAsset(Image, true);
			this.Textures.Add(Image.FullID, Image);
			
			Image = new TextureImage();
			Image.filename = "map_base.jp2";
			Image.FullID = new LLUUID("00000000-0000-0000-7007-000000000006");
			this.LoadAsset(Image, true);
			this.Textures.Add(Image.FullID, Image);
			
			Image = new TextureImage();
			Image.filename = "map1.jp2";
			Image.FullID = new LLUUID("00000000-0000-0000-7009-000000000008");
			this.LoadAsset(Image, true);
			this.Textures.Add(Image.FullID, Image);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		/// <param name="AssetID"></param>
		/// <param name="TransferRequest"></param>
		#region AssetRegion
		
		public void AddAssetRequest(UserAgentInfo userInfo, LLUUID assetID, TransferRequestPacket transferRequest)
		{
			
			if(!this.Assets.ContainsKey(assetID))
			{
				//not found asset	
				return;
			}
			AssetInfo info = this.Assets[assetID];
			//for now as it will be only skin or shape request just send back the asset
			TransferInfoPacket Transfer = new TransferInfoPacket();
			Transfer.TransferInfo.ChannelType = 2;
			Transfer.TransferInfo.Status = 0;
			Transfer.TransferInfo.TargetType = 0;
			Transfer.TransferInfo.Params = transferRequest.TransferInfo.Params;
			Transfer.TransferInfo.Size = info.data.Length;
			Transfer.TransferInfo.TransferID = transferRequest.TransferInfo.TransferID;
			
			_server.SendPacket(Transfer, true, userInfo);
			
			TransferPacketPacket TransferPacket = new TransferPacketPacket();
			TransferPacket.TransferData.Packet = 0;
			TransferPacket.TransferData.ChannelType = 2;
			TransferPacket.TransferData.TransferID=transferRequest.TransferInfo.TransferID;
			if(info.data.Length>1000)  //but needs to be less than 2000 at the moment
			{
				byte[] chunk = new byte[1000];
				Array.Copy(info.data,chunk,1000);
				TransferPacket.TransferData.Data = chunk;
				TransferPacket.TransferData.Status = 0;
				_server.SendPacket(TransferPacket,true,userInfo);
				
				TransferPacket = new TransferPacketPacket();
				TransferPacket.TransferData.Packet = 1;
				TransferPacket.TransferData.ChannelType = 2;
				TransferPacket.TransferData.TransferID = transferRequest.TransferInfo.TransferID;
				byte[] chunk1 = new byte[(info.data.Length-1000)];
				Array.Copy(info.data, 1000, chunk1, 0, chunk1.Length);
				TransferPacket.TransferData.Data = chunk1;
				TransferPacket.TransferData.Status = 1;
				_server.SendPacket(TransferPacket, true, userInfo);
			}
			else
			{
				TransferPacket.TransferData.Status = 1;  //last packet? so set to 1
				TransferPacket.TransferData.Data = info.data;
				_server.SendPacket(TransferPacket, true, userInfo);
			}
			
		}
		
		public void CreateNewInventorySet(ref AvatarData Avata,UserAgentInfo UserInfo)
		{
			//Create Folders
			LLUUID BaseFolder = Avata.BaseFolder;
			_inventoryManager.CreateNewFolder(UserInfo, Avata.InventoryFolder);
			_inventoryManager.CreateNewFolder(UserInfo, BaseFolder);
			
			//Give a copy of default shape
			AssetInfo Base = this.Assets[new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73")];
			AssetInfo Shape = this.CloneAsset(UserInfo.AgentID, Base);
			
			Shape.filename = "";
			Shape.Name = "Default Shape";
			Shape.Description = "Default Shape";
			Shape.InvType = 18;
			Shape.Type = libsecondlife.AssetSystem.Asset.ASSET_TYPE_WEARABLE_BODY;
			
			byte[] Agentid = _enc.GetBytes(UserInfo.AgentID.ToStringHyphenated());
			Array.Copy(Agentid, 0, Shape.data, 294, Agentid.Length);
			this.Assets.Add(Shape.FullID, Shape);

			Avata.Wearables[0].ItemID = _inventoryManager.AddToInventory(UserInfo, BaseFolder, Shape);
			Avata.Wearables[0].AssetID = Shape.FullID;
			
			//Give copy of default skin
			Base = this.Assets[new LLUUID("e0ee49b5a4184df8d3c9a65361fe7f49")];
			AssetInfo Skin=this.CloneAsset(UserInfo.AgentID, Base);
			
			Skin.filename = "";
			Skin.Name = "Default Skin";
			Skin.Description = "Default Skin";
			Skin.InvType = 18;
			Skin.Type = libsecondlife.AssetSystem.Asset.ASSET_TYPE_WEARABLE_BODY;
			
			Array.Copy(Agentid,0,Skin.data,238,Agentid.Length);
			this.Assets.Add(Skin.FullID, Skin);
			
			Avata.Wearables[1].ItemID = _inventoryManager.AddToInventory(UserInfo, BaseFolder, Skin);
			Avata.Wearables[1].AssetID = Skin.FullID;
			
			//give a copy of test texture
			TextureImage Texture = this.CloneImage(UserInfo.AgentID, Textures[new LLUUID("00000000-0000-0000-5005-000000000005")]);
			this.Textures.Add(Texture.FullID, Texture);
			_inventoryManager.AddToInventory(UserInfo, BaseFolder, Texture);
			
		}
		

		private void LoadAsset(AssetBase info, bool Image)
		{
			//should request Asset from storage manager
			//but for now read from file
			string folder;
			if(Image)
			{
				folder = @"\textures\";
			}
			else
			{
				folder = @"\assets\";
			}
            string data_path = System.AppDomain.CurrentDomain.BaseDirectory + folder;
			string filename = data_path+@info.filename;
			FileInfo fInfo = new FileInfo(filename);

			long numBytes = fInfo.Length;

			FileStream fStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
			byte[] idata = new byte[numBytes];
			BinaryReader br = new BinaryReader(fStream);
			idata= br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			info.data = idata;
			//info.loaded=true;
		}
		
		public AssetInfo CloneAsset(LLUUID NewOwner, AssetInfo SourceAsset)
		{
			AssetInfo NewAsset = new AssetInfo();
			NewAsset.data = new byte[SourceAsset.data.Length];
			Array.Copy(SourceAsset.data, NewAsset.data, SourceAsset.data.Length);
			NewAsset.FullID = LLUUID.Random();
			NewAsset.Type = SourceAsset.Type;
			NewAsset.InvType = SourceAsset.InvType;
			return(NewAsset);
		}
		#endregion
		
		#region TextureRegion
		public void AddTextureRequest(UserAgentInfo userInfo, LLUUID imageID)
		{
			
			if(!this.Textures.ContainsKey(imageID))
			{
				//not found image so send back image not in data base message
				ImageNotInDatabasePacket im_not = new ImageNotInDatabasePacket();
				im_not.ImageID.ID=imageID;
				_server.SendPacket(im_not, true, userInfo);
				return;
			}
			TextureImage imag = this.Textures[imageID];
			TextureRequest req = new TextureRequest();
			req.RequestUser = userInfo;
			req.RequestImage = imageID;
			req.ImageInfo = imag;
			
			if(imag.data.LongLength>600)  //should be bigger or smaller?
			{
				//over 600 bytes so split up file
				req.NumPackets = 1 + (int)(imag.data.Length-600+999)/1000;
			}
			else
			{
				req.NumPackets = 1;
			}
			
			this.TextureRequests.Add(req);
			
		}
		
		public void AddTexture(LLUUID imageID, string name, byte[] data)
		{
			
		}
		public void DoWork(ulong time)
		{
			if(this.TextureRequests.Count == 0)
			{
				//no requests waiting
				return;
			}
			int num;
			//should be running in its own thread but for now is called by timer
			if(this.TextureRequests.Count < 5)
			{
				//lower than 5 so do all of them
				num = this.TextureRequests.Count;
			}
			else
			{
				num=5;
			}
			TextureRequest req;
			for(int i = 0; i < num; i++)
			{
				req=(TextureRequest)this.TextureRequests[i];
				
				if(req.PacketCounter == 0)
				{
					//first time for this request so send imagedata packet
					if(req.NumPackets == 1)
					{		
						//only one packet so send whole file
						ImageDataPacket im = new ImageDataPacket();
						im.ImageID.Packets = 1;
						im.ImageID.ID = req.ImageInfo.FullID;
						im.ImageID.Size = (uint)req.ImageInfo.data.Length;
						im.ImageData.Data = req.ImageInfo.data;
						im.ImageID.Codec = 2;		
						_server.SendPacket(im, true, req.RequestUser);
						req.PacketCounter++;
						req.ImageInfo.last_used = time;
						//System.Console.WriteLine("sent texture: "+req.image_info.FullID);
					}
					else
					{
						//more than one packet so split file up
						ImageDataPacket im = new ImageDataPacket();
						im.ImageID.Packets = (ushort)req.NumPackets;
						im.ImageID.ID = req.ImageInfo.FullID;
						im.ImageID.Size = (uint)req.ImageInfo.data.Length;
						im.ImageData.Data = new byte[600];
						Array.Copy(req.ImageInfo.data, 0, im.ImageData.Data, 0, 600);
						im.ImageID.Codec = 2;
						_server.SendPacket(im, true, req.RequestUser);
						req.PacketCounter++;
						req.ImageInfo.last_used = time;
						//System.Console.WriteLine("sent first packet of texture:
					}
				}
				else
				{
					//send imagepacket
					//more than one packet so split file up
					ImagePacketPacket im = new ImagePacketPacket();
					im.ImageID.Packet = (ushort)req.PacketCounter;
					im.ImageID.ID = req.ImageInfo.FullID;
					int size = req.ImageInfo.data.Length - 600 - 1000*(req.PacketCounter - 1);
					if(size > 1000) size = 1000;
					im.ImageData.Data = new byte[size];
					Array.Copy(req.ImageInfo.data, 600 + 1000*(req.PacketCounter - 1), im.ImageData.Data, 0, size);
					_server.SendPacket(im, true, req.RequestUser);
					req.PacketCounter++;
					req.ImageInfo.last_used = time;
					//System.Console.WriteLine("sent a packet of texture: "+req.image_info.FullID);	
				}
			}
			
			//remove requests that have been completed
			for(int i = 0; i < num; i++)
			{
				req=(TextureRequest)this.TextureRequests[i];
				if(req.PacketCounter == req.NumPackets)
				{
					this.TextureRequests.Remove(req);
				}
			}
		}
		
		public void RecieveTexture(Packet pack)
		{
			
		}
		
		public TextureImage CloneImage(LLUUID newOwner, TextureImage source)
		{
			TextureImage newImage = new TextureImage();
			newImage.data = new byte[source.data.Length];
			Array.Copy(source.data,newImage.data,source.data.Length);
			newImage.filename = source.filename;
			newImage.FullID = LLUUID.Random();
			newImage.Name = source.Name;
			return(newImage);
		}
		
		#endregion
	}
	
	public class AssetRequest
	{
		public UserAgentInfo RequestUser;
		public LLUUID RequestImage;
		public AssetInfo asset_inf;
		public long data_pointer = 0;
		public int num_packets = 0;
		public int packet_counter = 0;
		
		public AssetRequest()
		{
			
		}
	}
	public class AssetInfo:AssetBase
	{
		//public byte[] data;
		//public LLUUID Full_ID;
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
		public LLUUID FullID;
		public sbyte Type;
		public sbyte InvType;
		public string Name;
		public string Description;
		public string filename;
		
		public AssetBase()
		{
			
		}
	}
	 public class TextureRequest
	{
		public UserAgentInfo RequestUser;
		public LLUUID RequestImage;
		public TextureImage ImageInfo;
		public long DataPointer = 0;
		public int NumPackets = 0;
		public int PacketCounter = 0;
		
		public TextureRequest()
		{
			
		}
	}
	public class TextureImage: AssetBase
	{
		//any need for this class now most has been moved into AssetBase?
		//public byte[] data;
		//public LLUUID Full_ID;
		//public string name;
		public bool loaded;
		public ulong last_used;  //need to add a tick/time counter and keep record
								// of how often images are requested to unload unused ones.
		
		public TextureImage()
		{
			
		}
	}

	
}
