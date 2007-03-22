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
using libsecondlife;
using OpenSim;
using libsecondlife.Packets;
//using OpenSim.GridServers;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Assets;

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
            this._agentsInventory.Add(agentInventory.AgentID, agentInventory);
        }

        public void ClientLeaving(LLUUID clientID)
        {
            if (this._agentsInventory.ContainsKey(clientID))
            {
                this._agentsInventory.Remove(clientID);
            }

        }
        public bool CreateNewInventoryFolder(SimClient remoteClient, LLUUID folderID)
        {
            bool res = false;
            if (folderID != LLUUID.Zero)  //don't create a folder with a zero id
            {
                if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
                {
                    res = this._agentsInventory[remoteClient.AgentID].CreateNewFolder(folderID);
                }
            }
            return res;
        }

        public LLUUID AddNewInventoryItem(SimClient remoteClient, LLUUID folderID, OpenSim.Framework.Assets.AssetBase asset)
        {
            LLUUID newItem = null;
            if (this._agentsInventory.ContainsKey(remoteClient.AgentID))
            {
                newItem = this._agentsInventory[remoteClient.AgentID].AddToInventory(folderID, asset);
            }

            return newItem;
        }

        public void FetchInventoryDescendents(SimClient userInfo, FetchInventoryDescendentsPacket FetchDescend)
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

        public void FetchInventory(SimClient userInfo, FetchInventoryPacket FetchItems)
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
                        InventoryReply.InventoryData[0].CreationDate = 1000;
                        InventoryReply.InventoryData[0].Description = _enc.GetBytes(Item.Description + "\0");
                        InventoryReply.InventoryData[0].EveryoneMask = FULL_MASK_PERMISSIONS;
                        InventoryReply.InventoryData[0].Flags = 1;
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
    }

    

    public class UserServerRequest
    {
        public UserServerRequest()
        {

        }
    }
}
