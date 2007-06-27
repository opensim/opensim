/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
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
using OpenSim;
using libsecondlife.Packets;
//using OpenSim.GridServers;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Types;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Assets
{
    /// <summary>
    /// Description of InventoryManager.
    /// </summary>
    public class InventoryCache
    {
        private Dictionary<LLUUID, AgentInventory> _agentsInventory;
        private List<UserServerRequest> _serverRequests; //list of requests made to user server.
        private System.Text.Encoding _enc = System.Text.Encoding.ASCII;
        private const uint FULL_MASK_PERMISSIONS = 2147483647;

        public InventoryCache()
        {
            _agentsInventory = new Dictionary<LLUUID, AgentInventory>();
            _serverRequests = new List<UserServerRequest>();
        }

        public void AddNewAgentsInventory(AgentInventory agentInventory)
        {
            if (!this._agentsInventory.ContainsKey(agentInventory.AgentID))
            {
                this._agentsInventory.Add(agentInventory.AgentID, agentInventory);
            }
        }

        public AgentInventory FetchAgentsInventory(LLUUID agentID, IUserServer userserver)
        {
            AgentInventory res = null;
            if (!this._agentsInventory.ContainsKey(agentID))
            {
                res = userserver.RequestAgentsInventory(agentID);
                this._agentsInventory.Add(agentID,res);
            }
            return res;
        }

        public AgentInventory GetAgentsInventory(LLUUID agentID)
        {
            if (this._agentsInventory.ContainsKey(agentID))
            {
                return this._agentsInventory[agentID];
            }

            return null;
        }

        public void ClientLeaving(LLUUID clientID, IUserServer userserver)
        { 
            if (this._agentsInventory.ContainsKey(clientID))
            {
                if (userserver != null)
                {
                    userserver.UpdateAgentsInventory(clientID, this._agentsInventory[clientID]);
                }
                this._agentsInventory.Remove(clientID);
            }
        }

        public bool CreateNewInventoryFolder(ClientView remoteClient, LLUUID folderID)
        {
            return this.CreateNewInventoryFolder(remoteClient, folderID, 0);
        }

        public bool CreateNewInventoryFolder(ClientView remoteClient, LLUUID folderID, ushort type)
        {
            bool res = false;
            if (folderID != LLUUID.Zero)  //don't create a folder with a zero id
            {
                if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
                {
                    res = this._agentsInventory[remoteClient.AgentID].CreateNewFolder(folderID, type);
                }
            }
            return res;
        }

        public bool CreateNewInventoryFolder(ClientView remoteClient, LLUUID folderID, ushort type, string folderName, LLUUID parent)
        {
            bool res = false;
            if (folderID != LLUUID.Zero)  //don't create a folder with a zero id
            {
                if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
                {
                    res = this._agentsInventory[remoteClient.AgentID].CreateNewFolder(folderID, type, folderName, parent);
                }
            }
            return res;
        }

        public LLUUID AddNewInventoryItem(ClientView remoteClient, LLUUID folderID, OpenSim.Framework.Types.AssetBase asset)
        {
            LLUUID newItem = null;
            if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
            {
                newItem = this._agentsInventory[remoteClient.AgentID].AddToInventory(folderID, asset);
                if (newItem != null)
                {
                    InventoryItem Item = this._agentsInventory[remoteClient.AgentID].InventoryItems[newItem];
                    this.SendItemUpdateCreate(remoteClient, Item);
                }
            }

            return newItem;
        }
        public bool DeleteInventoryItem(ClientView remoteClient, LLUUID itemID)
        {
            bool res = false;
            if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
            {
                res = this._agentsInventory[remoteClient.AgentID].DeleteFromInventory(itemID);
                if (res)
                {
                    RemoveInventoryItemPacket remove = new RemoveInventoryItemPacket();
                    remove.AgentData.AgentID = remoteClient.AgentID;
                    remove.AgentData.SessionID = remoteClient.SessionID;
                    remove.InventoryData = new RemoveInventoryItemPacket.InventoryDataBlock[1];
                    remove.InventoryData[0] = new RemoveInventoryItemPacket.InventoryDataBlock();
                    remove.InventoryData[0].ItemID = itemID;
                    remoteClient.OutPacket(remove);
                }
            }

            return res;
        }

        public bool UpdateInventoryItemAsset(ClientView remoteClient, LLUUID itemID, OpenSim.Framework.Types.AssetBase asset)
        {
            if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
            {
                bool res = _agentsInventory[remoteClient.AgentID].UpdateItemAsset(itemID, asset);
                if (res)
                {
                    InventoryItem Item = this._agentsInventory[remoteClient.AgentID].InventoryItems[itemID];
                    this.SendItemUpdateCreate(remoteClient, Item);
                }
                return res;
            }

            return false;
        }

        public bool UpdateInventoryItemDetails(ClientView remoteClient, LLUUID itemID, UpdateInventoryItemPacket.InventoryDataBlock packet)
        {
            if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
            {
                bool res = _agentsInventory[remoteClient.AgentID].UpdateItemDetails(itemID, packet);
                if (res)
                {
                    InventoryItem Item = this._agentsInventory[remoteClient.AgentID].InventoryItems[itemID];
                    this.SendItemUpdateCreate(remoteClient, Item);
                }
                return res;
            }

            return false;
        }

        public void FetchInventoryDescendents(ClientView userInfo, FetchInventoryDescendentsPacket FetchDescend)
        {
            if (this._agentsInventory.ContainsKey(userInfo.AgentID))
            {
                AgentInventory agentInventory = this._agentsInventory[userInfo.AgentID];
                if (FetchDescend.InventoryData.FetchItems)
                {
                    if (agentInventory.InventoryFolders.ContainsKey(FetchDescend.InventoryData.FolderID))
                    {
                        InventoryFolder Folder = agentInventory.InventoryFolders[FetchDescend.InventoryData.FolderID];
                        InventoryDescendentsPacket Descend = new InventoryDescendentsPacket();
                        Descend.AgentData.AgentID = userInfo.AgentID;
                        Descend.AgentData.OwnerID = Folder.OwnerID;
                        Descend.AgentData.FolderID = FetchDescend.InventoryData.FolderID;
                        Descend.AgentData.Descendents = Folder.Items.Count;
                        Descend.AgentData.Version = Folder.Items.Count;


                        Descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[Folder.Items.Count];
                        for (int i = 0; i < Folder.Items.Count; i++)
                        {

                            InventoryItem Item = Folder.Items[i];
                            Descend.ItemData[i] = new InventoryDescendentsPacket.ItemDataBlock();
                            Descend.ItemData[i].ItemID = Item.ItemID;
                            Descend.ItemData[i].AssetID = Item.AssetID;
                            Descend.ItemData[i].CreatorID = Item.CreatorID;
                            Descend.ItemData[i].BaseMask = FULL_MASK_PERMISSIONS;
                            Descend.ItemData[i].CreationDate = 1000;
                            Descend.ItemData[i].Description = _enc.GetBytes(Item.Description + "\0");
                            Descend.ItemData[i].EveryoneMask = FULL_MASK_PERMISSIONS;
                            Descend.ItemData[i].Flags = 1;
                            Descend.ItemData[i].FolderID = Item.FolderID;
                            Descend.ItemData[i].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
                            Descend.ItemData[i].GroupMask = FULL_MASK_PERMISSIONS;
                            Descend.ItemData[i].InvType = Item.InvType;
                            Descend.ItemData[i].Name = _enc.GetBytes(Item.Name + "\0");
                            Descend.ItemData[i].NextOwnerMask = FULL_MASK_PERMISSIONS;
                            Descend.ItemData[i].OwnerID = Item.OwnerID;
                            Descend.ItemData[i].OwnerMask = FULL_MASK_PERMISSIONS;
                            Descend.ItemData[i].SalePrice = 100;
                            Descend.ItemData[i].SaleType = 0;
                            Descend.ItemData[i].Type = Item.Type;
                            Descend.ItemData[i].CRC = libsecondlife.Helpers.InventoryCRC(1000, 0, Descend.ItemData[i].InvType, Descend.ItemData[i].Type, Descend.ItemData[i].AssetID, Descend.ItemData[i].GroupID, 100, Descend.ItemData[i].OwnerID, Descend.ItemData[i].CreatorID, Descend.ItemData[i].ItemID, Descend.ItemData[i].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);
                        }
                        
                        userInfo.OutPacket(Descend);

                    }
                }
                else
                {
                    Console.WriteLine("fetch subfolders");
                }
            }
        }

        public void FetchInventory(ClientView userInfo, FetchInventoryPacket FetchItems)
        {
            if (this._agentsInventory.ContainsKey(userInfo.AgentID))
            {
                AgentInventory agentInventory = this._agentsInventory[userInfo.AgentID];

                for (int i = 0; i < FetchItems.InventoryData.Length; i++)
                {
                    if (agentInventory.InventoryItems.ContainsKey(FetchItems.InventoryData[i].ItemID))
                    {
                        InventoryItem Item = agentInventory.InventoryItems[FetchItems.InventoryData[i].ItemID];
                        FetchInventoryReplyPacket InventoryReply = new FetchInventoryReplyPacket();
                        InventoryReply.AgentData.AgentID = userInfo.AgentID;
                        InventoryReply.InventoryData = new FetchInventoryReplyPacket.InventoryDataBlock[1];
                        InventoryReply.InventoryData[0] = new FetchInventoryReplyPacket.InventoryDataBlock();
                        InventoryReply.InventoryData[0].ItemID = Item.ItemID;
                        InventoryReply.InventoryData[0].AssetID = Item.AssetID;
                        InventoryReply.InventoryData[0].CreatorID = Item.CreatorID;
                        InventoryReply.InventoryData[0].BaseMask = FULL_MASK_PERMISSIONS;
                        InventoryReply.InventoryData[0].CreationDate = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                        InventoryReply.InventoryData[0].Description = _enc.GetBytes(Item.Description + "\0");
                        InventoryReply.InventoryData[0].EveryoneMask = FULL_MASK_PERMISSIONS;
                        InventoryReply.InventoryData[0].Flags = 0;
                        InventoryReply.InventoryData[0].FolderID = Item.FolderID;
                        InventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
                        InventoryReply.InventoryData[0].GroupMask = FULL_MASK_PERMISSIONS;
                        InventoryReply.InventoryData[0].InvType = Item.InvType;
                        InventoryReply.InventoryData[0].Name = _enc.GetBytes(Item.Name + "\0");
                        InventoryReply.InventoryData[0].NextOwnerMask = FULL_MASK_PERMISSIONS;
                        InventoryReply.InventoryData[0].OwnerID = Item.OwnerID;
                        InventoryReply.InventoryData[0].OwnerMask = FULL_MASK_PERMISSIONS;
                        InventoryReply.InventoryData[0].SalePrice = 100;
                        InventoryReply.InventoryData[0].SaleType = 0;
                        InventoryReply.InventoryData[0].Type = Item.Type;
                        InventoryReply.InventoryData[0].CRC = libsecondlife.Helpers.InventoryCRC(1000, 0, InventoryReply.InventoryData[0].InvType, InventoryReply.InventoryData[0].Type, InventoryReply.InventoryData[0].AssetID, InventoryReply.InventoryData[0].GroupID, 100, InventoryReply.InventoryData[0].OwnerID, InventoryReply.InventoryData[0].CreatorID, InventoryReply.InventoryData[0].ItemID, InventoryReply.InventoryData[0].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);
                        userInfo.OutPacket(InventoryReply);
                    }
                }
            }
        }

        private void SendItemUpdateCreate(ClientView remoteClient, InventoryItem Item)
        {

            UpdateCreateInventoryItemPacket InventoryReply = new UpdateCreateInventoryItemPacket();
            InventoryReply.AgentData.AgentID = remoteClient.AgentID;
            InventoryReply.AgentData.SimApproved = true;
            InventoryReply.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[1];
            InventoryReply.InventoryData[0] = new UpdateCreateInventoryItemPacket.InventoryDataBlock();
            InventoryReply.InventoryData[0].ItemID = Item.ItemID;
            InventoryReply.InventoryData[0].AssetID = Item.AssetID;
            InventoryReply.InventoryData[0].CreatorID = Item.CreatorID;
            InventoryReply.InventoryData[0].BaseMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].CreationDate = 1000;
            InventoryReply.InventoryData[0].Description = _enc.GetBytes(Item.Description + "\0");
            InventoryReply.InventoryData[0].EveryoneMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].Flags = 0;
            InventoryReply.InventoryData[0].FolderID = Item.FolderID;
            InventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
            InventoryReply.InventoryData[0].GroupMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].InvType = Item.InvType;
            InventoryReply.InventoryData[0].Name = _enc.GetBytes(Item.Name + "\0");
            InventoryReply.InventoryData[0].NextOwnerMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].OwnerID = Item.OwnerID;
            InventoryReply.InventoryData[0].OwnerMask = FULL_MASK_PERMISSIONS;
            InventoryReply.InventoryData[0].SalePrice = 100;
            InventoryReply.InventoryData[0].SaleType = 0;
            InventoryReply.InventoryData[0].Type = Item.Type;
            InventoryReply.InventoryData[0].CRC = libsecondlife.Helpers.InventoryCRC(1000, 0, InventoryReply.InventoryData[0].InvType, InventoryReply.InventoryData[0].Type, InventoryReply.InventoryData[0].AssetID, InventoryReply.InventoryData[0].GroupID, 100, InventoryReply.InventoryData[0].OwnerID, InventoryReply.InventoryData[0].CreatorID, InventoryReply.InventoryData[0].ItemID, InventoryReply.InventoryData[0].FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS);

            remoteClient.OutPacket(InventoryReply);
        }
    }



    public class UserServerRequest
    {
        public UserServerRequest()
        {

        }
    }
}
