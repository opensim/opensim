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

namespace OpenSim
{
	/// <summary>
	/// Description of InventoryManager.
	/// </summary>
	public class InventoryManager
	{
		private System.Text.Encoding enc = System.Text.Encoding.ASCII;
		public Dictionary<LLUUID, InventoryFolder> Folders;
		public Dictionary<LLUUID, InventoryItem> Items;
		private Server server;
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="serve"></param>
		public InventoryManager(Server serve)
		{
			server=serve;
			Folders=new Dictionary<LLUUID, InventoryFolder>();
			Items=new Dictionary<LLUUID, InventoryItem>();
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		/// <param name="FolderID"></param>
		/// <param name="Asset"></param>
		/// <returns></returns>
		public LLUUID AddToInventory(User_Agent_info UserInfo, LLUUID FolderID,AssetBase Asset)
		{
			if(this.Folders.ContainsKey(FolderID))
			{
				LLUUID NewItemID=LLUUID.Random();
				
				InventoryItem Item=new InventoryItem();
				Item.FolderID=FolderID;
				Item.OwnerID=UserInfo.AgentID;
				Item.AssetID=Asset.Full_ID;
				Item.ItemID=NewItemID;
				Item.Type=Asset.Type;
				Item.Name=Asset.Name;
				Item.Description=Asset.Description;
				Item.InvType=Asset.InvType;
				this.Items.Add(Item.ItemID,Item);
				InventoryFolder Folder=Folders[Item.FolderID];
				Folder.Items.Add(Item);
				return(Item.ItemID);
			}
			else
			{
				return(null);
			}
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="UserInfo"></param>
		/// <param name="NewFolder"></param>
		/// <returns></returns>
		public bool CreateNewFolder(User_Agent_info UserInfo, LLUUID NewFolder)
		{
			InventoryFolder Folder=new InventoryFolder();
			Folder.FolderID=NewFolder;
			Folder.OwnerID=UserInfo.AgentID;
			this.Folders.Add(Folder.FolderID,Folder);
			
			return(true);
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		/// <param name="FetchDescend"></param>
		public void FetchInventoryDescendents(User_Agent_info User_info,FetchInventoryDescendentsPacket FetchDescend)
		{
			if(FetchDescend.InventoryData.FetchItems)
			{
				if(this.Folders.ContainsKey(FetchDescend.InventoryData.FolderID))
				{
						
					InventoryFolder Folder=this.Folders[FetchDescend.InventoryData.FolderID];
					InventoryDescendentsPacket Descend=new InventoryDescendentsPacket();
					Descend.AgentData.AgentID=User_info.AgentID;
					Descend.AgentData.OwnerID=Folder.OwnerID;//User_info.AgentID;
					Descend.AgentData.FolderID=FetchDescend.InventoryData.FolderID;//Folder.FolderID;//new LLUUID("4fb2dab6-a987-da66-05ee-96ca82bccbf1");
					Descend.AgentData.Descendents=Folder.Items.Count;
					Descend.AgentData.Version=Folder.Items.Count;
					
					Descend.ItemData=new InventoryDescendentsPacket.ItemDataBlock[Folder.Items.Count];
					for(int i=0; i<Folder.Items.Count ; i++)
					{
						
						InventoryItem Item=Folder.Items[i];
						Descend.ItemData[i]=new InventoryDescendentsPacket.ItemDataBlock();
						Descend.ItemData[i].ItemID=Item.ItemID;//new LLUUID("b7878441893b094917f791174bc8401c");
						Descend.ItemData[i].AssetID=Item.AssetID;//new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
						Descend.ItemData[i].CreatorID=Item.CreatorID;//User_info.AgentID;
						Descend.ItemData[i].BaseMask=2147483647;
						Descend.ItemData[i].CreationDate=1000;
						Descend.ItemData[i].Description=enc.GetBytes(Item.Description+"\0");
						Descend.ItemData[i].EveryoneMask=2147483647;;
						Descend.ItemData[i].Flags=1;
						Descend.ItemData[i].FolderID=Item.FolderID;//new LLUUID("4fb2dab6-a987-da66-05ee-96ca82bccbf1");
						Descend.ItemData[i].GroupID=new LLUUID("00000000-0000-0000-0000-000000000000");
						Descend.ItemData[i].GroupMask=2147483647;
						Descend.ItemData[i].InvType=Item.InvType;
						Descend.ItemData[i].Name=enc.GetBytes(Item.Name+"\0");
						Descend.ItemData[i].NextOwnerMask=2147483647;
						Descend.ItemData[i].OwnerID=Item.OwnerID;//User_info.AgentID;
						Descend.ItemData[i].OwnerMask=2147483647;;
						Descend.ItemData[i].SalePrice=100;
						Descend.ItemData[i].SaleType=0;
						Descend.ItemData[i].Type=Item.Type;//libsecondlife.AssetSystem.Asset.ASSET_TYPE_WEARABLE_BODY;
						Descend.ItemData[i].CRC=libsecondlife.Helpers.InventoryCRC(1000,0,Descend.ItemData[i].InvType,Descend.ItemData[i].Type,Descend.ItemData[i].AssetID ,Descend.ItemData[i].GroupID,100,Descend.ItemData[i].OwnerID,Descend.ItemData[i].CreatorID,Descend.ItemData[i].ItemID,Descend.ItemData[i].FolderID,2147483647,1,2147483647,2147483647,2147483647);
					}
					server.SendPacket(Descend,true,User_info);
					
				}
			}
			else
			{
				Console.WriteLine("fetch subfolders");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="User_info"></param>
		public void FetchInventory(User_Agent_info User_info, FetchInventoryPacket FetchItems)
		{
			
			for(int i=0; i<FetchItems.InventoryData.Length; i++)
			{
				if(this.Items.ContainsKey(FetchItems.InventoryData[i].ItemID))
				{
					
					InventoryItem Item=Items[FetchItems.InventoryData[i].ItemID];
					FetchInventoryReplyPacket InventoryReply=new FetchInventoryReplyPacket();
					InventoryReply.AgentData.AgentID=User_info.AgentID;
					InventoryReply.InventoryData=new FetchInventoryReplyPacket.InventoryDataBlock[1];
					InventoryReply.InventoryData[0]=new FetchInventoryReplyPacket.InventoryDataBlock();
					InventoryReply.InventoryData[0].ItemID=Item.ItemID;//new LLUUID("b7878441893b094917f791174bc8401c");
					InventoryReply.InventoryData[0].AssetID=Item.AssetID;//new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
					InventoryReply.InventoryData[0].CreatorID=Item.CreatorID;//User_info.AgentID;
					InventoryReply.InventoryData[0].BaseMask=2147483647;
					InventoryReply.InventoryData[0].CreationDate=1000;
					InventoryReply.InventoryData[0].Description=enc.GetBytes(  Item.Description+"\0");
					InventoryReply.InventoryData[0].EveryoneMask=2147483647;;
					InventoryReply.InventoryData[0].Flags=1;
					InventoryReply.InventoryData[0].FolderID=Item.FolderID;//new LLUUID("4fb2dab6-a987-da66-05ee-96ca82bccbf1");
					InventoryReply.InventoryData[0].GroupID=new LLUUID("00000000-0000-0000-0000-000000000000");
					InventoryReply.InventoryData[0].GroupMask=2147483647;
					InventoryReply.InventoryData[0].InvType=Item.InvType;
					InventoryReply.InventoryData[0].Name=enc.GetBytes(Item.Name+"\0");
					InventoryReply.InventoryData[0].NextOwnerMask=2147483647;
					InventoryReply.InventoryData[0].OwnerID=Item.OwnerID;//User_info.AgentID;
					InventoryReply.InventoryData[0].OwnerMask=2147483647;;
					InventoryReply.InventoryData[0].SalePrice=100;
					InventoryReply.InventoryData[0].SaleType=0;
					InventoryReply.InventoryData[0].Type=Item.Type;//libsecondlife.AssetSystem.Asset.ASSET_TYPE_WEARABLE_BODY;
					InventoryReply.InventoryData[0].CRC=libsecondlife.Helpers.InventoryCRC(1000,0,InventoryReply.InventoryData[0].InvType,InventoryReply.InventoryData[0].Type,InventoryReply.InventoryData[0].AssetID ,InventoryReply.InventoryData[0].GroupID,100,InventoryReply.InventoryData[0].OwnerID,InventoryReply.InventoryData[0].CreatorID,InventoryReply.InventoryData[0].ItemID,InventoryReply.InventoryData[0].FolderID,2147483647,1,2147483647,2147483647,2147483647);
					server.SendPacket(InventoryReply,true,User_info);
				}
			}
		}
	}
	
	public class InventoryFolder
	{
		public List<InventoryItem> Items;
		//public List<InventoryFolder> Subfolders;
		
		public LLUUID FolderID;
		public LLUUID OwnerID;
		public LLUUID ParentID;
		
		
		public InventoryFolder()
		{
			Items=new List<InventoryItem>();
		}
		
	}
	
	public class InventoryItem
	{
		public LLUUID FolderID;
		public LLUUID OwnerID;
		public LLUUID ItemID;
		public LLUUID AssetID;
		public LLUUID CreatorID=LLUUID.Zero;//new LLUUID("3d924400-038e-6ad9-920b-cfbb9b40585c");
		public sbyte InvType;
		public sbyte Type;
		public string Name;
		public string Description;
		
		public InventoryItem()
		{
			
		}
	}
}
