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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Data;
using OpenSim.Region.Capabilities;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Communications.Caches
{
    public class AgentAssetTransactions
    {
        // Fields
        public List<AssetCapsUploader> CapsUploaders = new List<AssetCapsUploader>();
        public List<NoteCardCapsUpdate> NotecardUpdaters = new List<NoteCardCapsUpdate>();
        public LLUUID UserID;
        public Dictionary<LLUUID, AssetXferUploader> XferUploaders = new Dictionary<LLUUID, AssetXferUploader>();
        public AssetTransactionManager Manager;

        // Methods
        public AgentAssetTransactions(LLUUID agentID, AssetTransactionManager manager)
        {
            this.UserID = agentID;
            Manager = manager;
        }

        public AssetCapsUploader RequestCapsUploader()
        {
            AssetCapsUploader uploader = new AssetCapsUploader();
            this.CapsUploaders.Add(uploader);
            return uploader;
        }

        public NoteCardCapsUpdate RequestNoteCardUpdater()
        {
            NoteCardCapsUpdate update = new NoteCardCapsUpdate();
            this.NotecardUpdaters.Add(update);
            return update;
        }

        public AssetXferUploader RequestXferUploader(LLUUID transactionID)
        {
            if (!this.XferUploaders.ContainsKey(transactionID))
            {
                AssetXferUploader uploader = new AssetXferUploader(this);

                this.XferUploaders.Add(transactionID, uploader);
                return uploader;
            }
            return null;
        }

        public void HandleXfer(ulong xferID, uint packetID, byte[] data)
        {
            foreach (AssetXferUploader uploader in this.XferUploaders.Values)
            {
                if (uploader.XferID == xferID)
                {
                    uploader.HandleXferPacket(xferID, packetID, data);
                    break;
                }
            }
        }

        public void RequestCreateInventoryItem(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID, uint callbackID, string description, string name, sbyte invType, sbyte type, byte wearableType, uint nextOwnerMask)
        {
            if (this.XferUploaders.ContainsKey(transactionID))
            {
                this.XferUploaders[transactionID].RequestCreateInventoryItem(remoteClient, transactionID, folderID, callbackID, description, name, invType, type, wearableType, nextOwnerMask);
            }
        }

        // Nested Types
        public class AssetCapsUploader
        {
            // Fields
            private BaseHttpServer httpListener;
            private LLUUID inventoryItemID;
            private string m_assetDescription = "";
            private string m_assetName = "";
            private LLUUID m_folderID;
            private LLUUID newAssetID;
            private bool SaveImages = false;
            private string uploaderPath = "";

            // Events
            public event UpLoadedAsset OnUpLoad;

            // Methods
            public void Initialise(string assetName, string assetDescription, LLUUID assetID, LLUUID inventoryItem, LLUUID folderID, string path, BaseHttpServer httpServer)
            {
                this.m_assetName = assetName;
                this.m_assetDescription = assetDescription;
                this.m_folderID = folderID;
                this.newAssetID = assetID;
                this.inventoryItemID = inventoryItem;
                this.uploaderPath = path;
                this.httpListener = httpServer;
            }

            private void SaveImageToFile(string filename, byte[] data)
            {
                FileStream output = File.Create(filename);
                BinaryWriter writer = new BinaryWriter(output);
                writer.Write(data);
                writer.Close();
                output.Close();
            }

            public string uploaderCaps(byte[] data, string path, string param)
            {
                LLUUID inventoryItemID = this.inventoryItemID;
                string text = "";
                LLSDAssetUploadComplete complete = new LLSDAssetUploadComplete();
                complete.new_asset = this.newAssetID.ToStringHyphenated();
                complete.new_inventory_item = inventoryItemID;
                complete.state = "complete";
                text = LLSDHelpers.SerialiseLLSDReply(complete);
                this.httpListener.RemoveStreamHandler("POST", this.uploaderPath);
                if (this.SaveImages)
                {
                    this.SaveImageToFile(this.m_assetName + ".jp2", data);
                }
                if (this.OnUpLoad != null)
                {
                    this.OnUpLoad(this.m_assetName, "description", this.newAssetID, inventoryItemID, LLUUID.Zero, data, "" , "");
                }
                return text;
            }
        }

        public class AssetXferUploader
        {
            // Fields
            public bool AddToInventory;
            public AssetBase Asset;
            public LLUUID InventFolder = LLUUID.Zero;
            private IClientAPI ourClient;
            public LLUUID TransactionID = LLUUID.Zero;
            public bool UploadComplete;
            public ulong XferID;
            private string m_name = "";
            private string m_description = "";
            private sbyte type = 0;
            private sbyte invType = 0;
            private uint nextPerm = 0;
            private bool m_finished = false;
            private bool m_createItem = false;
            private AgentAssetTransactions m_userTransactions;

            public AssetXferUploader(AgentAssetTransactions transactions)
            {
                this.m_userTransactions = transactions;
            }

            // Methods
            public void HandleXferPacket(ulong xferID, uint packetID, byte[] data)
            {
                if (this.XferID == xferID)
                {
                    if (this.Asset.Data.Length > 1)
                    {
                        byte[] destinationArray = new byte[this.Asset.Data.Length + data.Length];
                        Array.Copy(this.Asset.Data, 0, destinationArray, 0, this.Asset.Data.Length);
                        Array.Copy(data, 0, destinationArray, this.Asset.Data.Length, data.Length);
                        this.Asset.Data = destinationArray;
                    }
                    else
                    {
                        byte[] buffer2 = new byte[data.Length - 4];
                        Array.Copy(data, 4, buffer2, 0, data.Length - 4);
                        this.Asset.Data = buffer2;
                    }
                    ConfirmXferPacketPacket newPack = new ConfirmXferPacketPacket();
                    newPack.XferID.ID = xferID;
                    newPack.XferID.Packet = packetID;
                    this.ourClient.OutPacket(newPack);
                    if ((packetID & 0x80000000) != 0)
                    {
                        this.SendCompleteMessage();
                    }
                }
            }

            public void Initialise(IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type, byte[] data)
            {
                this.ourClient = remoteClient;
                this.Asset = new AssetBase();
                this.Asset.FullID = assetID;
                this.Asset.InvType = type;
                this.Asset.Type = type;
                this.Asset.Data = data;
                this.Asset.Name = "blank";
                this.Asset.Description = "empty";
                this.TransactionID = transaction;
                if (this.Asset.Data.Length > 2)
                {
                    this.SendCompleteMessage();
                }
                else
                {
                    this.ReqestStartXfer();
                }
            }

            protected void ReqestStartXfer()
            {
                this.UploadComplete = false;
                this.XferID = Util.GetNextXferID();
                RequestXferPacket newPack = new RequestXferPacket();
                newPack.XferID.ID = this.XferID;
                newPack.XferID.VFileType = this.Asset.Type;
                newPack.XferID.VFileID = this.Asset.FullID;
                newPack.XferID.FilePath = 0;
                newPack.XferID.Filename = new byte[0];
                this.ourClient.OutPacket(newPack);
            }

            protected void SendCompleteMessage()
            {
                this.UploadComplete = true;
                AssetUploadCompletePacket newPack = new AssetUploadCompletePacket();
                newPack.AssetBlock.Type = this.Asset.Type;
                newPack.AssetBlock.Success = true;
                newPack.AssetBlock.UUID = this.Asset.FullID;
                this.ourClient.OutPacket(newPack);
                this.m_finished = true;
                if (m_createItem)
                {
                    DoCreateItem();
                }
              // Console.WriteLine("upload complete "+ this.TransactionID);
                //SaveAssetToFile("testudpupload" + Util.RandomClass.Next(1, 1000) + ".dat", this.Asset.Data);
            }
            private void SaveAssetToFile(string filename, byte[] data)
            {
                FileStream fs = File.Create(filename);
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();
            }

            public void RequestCreateInventoryItem(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID, uint callbackID, string description, string name, sbyte invType, sbyte type, byte wearableType, uint nextOwnerMask)
            {
                if (this.TransactionID == transactionID)
                {
                    this.InventFolder = folderID;
                    this.m_name = name;
                    this.m_description = description;
                    this.type = type;
                    this.invType = invType;
                    this.nextPerm = nextOwnerMask;
                    this.Asset.Name = name;
                    this.Asset.Description = description;
                    this.Asset.Type = type;
                    this.Asset.InvType = invType;
                    m_createItem = true;
                    if (m_finished)
                    {
                        DoCreateItem();
                    }
                }
            }

            private void DoCreateItem()
            {
                this.m_userTransactions.Manager.CommsManager.AssetCache.AddAsset(this.Asset);
                CachedUserInfo userInfo = m_userTransactions.Manager.CommsManager.UserProfiles.GetUserDetails(ourClient.AgentId);
                if (userInfo != null)
                {
                    InventoryItemBase item = new InventoryItemBase();
                    item.avatarID = this.ourClient.AgentId;
                    item.creatorsID = ourClient.AgentId;
                    item.inventoryID = LLUUID.Random();
                    item.assetID = Asset.FullID;
                    item.inventoryDescription = this.m_description;
                    item.inventoryName = m_name;
                    item.assetType = type;
                    item.invType = this.invType;
                    item.parentFolderID = this.InventFolder;
                    item.inventoryCurrentPermissions = 2147483647;
                    item.inventoryNextPermissions = this.nextPerm;

                    userInfo.AddItem(ourClient.AgentId, item);
                    ourClient.SendInventoryItemUpdate(item);
                }
            }

            public void UpdateInventoryItem(LLUUID itemID)
            {

            }
        }

        public class NoteCardCapsUpdate
        {
            // Fields
            private BaseHttpServer httpListener;
            private LLUUID inventoryItemID;
            private string m_assetName = "";
            private LLUUID newAssetID;
            private bool SaveImages = false;
            private string uploaderPath = "";

            // Events
            public event UpLoadedAsset OnUpLoad;

            // Methods
            public void Initialise(LLUUID inventoryItem, string path, BaseHttpServer httpServer)
            {
                this.inventoryItemID = inventoryItem;
                this.uploaderPath = path;
                this.httpListener = httpServer;
                this.newAssetID = LLUUID.Random();
            }

            private void SaveImageToFile(string filename, byte[] data)
            {
                FileStream output = File.Create(filename);
                BinaryWriter writer = new BinaryWriter(output);
                writer.Write(data);
                writer.Close();
                output.Close();
            }

            public string uploaderCaps(byte[] data, string path, string param)
            {
                LLUUID inventoryItemID = this.inventoryItemID;
                string text = "";
                LLSDAssetUploadComplete complete = new LLSDAssetUploadComplete();
                complete.new_asset = this.newAssetID.ToStringHyphenated();
                complete.new_inventory_item = inventoryItemID;
                complete.state = "complete";
                text = LLSDHelpers.SerialiseLLSDReply(complete);
                this.httpListener.RemoveStreamHandler("POST", this.uploaderPath);
                if (this.SaveImages)
                {
                    this.SaveImageToFile(this.m_assetName + "notecard.txt", data);
                }
                if (this.OnUpLoad != null)
                {
                    this.OnUpLoad(this.m_assetName, "description", this.newAssetID, inventoryItemID, LLUUID.Zero, data, "" , "" );
                }
                return text;
            }
        }
    }
}