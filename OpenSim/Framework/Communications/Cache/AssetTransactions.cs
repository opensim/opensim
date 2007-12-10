/*
* Copyright (c) Contributors, http://opensimulator.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
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
using System.IO;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Servers;
using OpenSim.Region.Capabilities;

namespace OpenSim.Framework.Communications.Cache
{
    public class AgentAssetTransactions
    {
        // Fields
        public List<AssetCapsUploader> CapsUploaders = new List<AssetCapsUploader>();
        public List<NoteCardCapsUpdate> NotecardUpdaters = new List<NoteCardCapsUpdate>();
        public LLUUID UserID;
        public Dictionary<LLUUID, AssetXferUploader> XferUploaders = new Dictionary<LLUUID, AssetXferUploader>();
        public AssetTransactionManager Manager;
        private bool m_dumpAssetsToFile;

        // Methods
        public AgentAssetTransactions(LLUUID agentID, AssetTransactionManager manager, bool dumpAssetsToFile)
        {
            UserID = agentID;
            Manager = manager;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        public AssetCapsUploader RequestCapsUploader()
        {
            AssetCapsUploader uploader = new AssetCapsUploader();
            CapsUploaders.Add(uploader);
            return uploader;
        }

        public NoteCardCapsUpdate RequestNoteCardUpdater()
        {
            NoteCardCapsUpdate update = new NoteCardCapsUpdate();
            NotecardUpdaters.Add(update);
            return update;
        }

        public AssetXferUploader RequestXferUploader(LLUUID transactionID)
        {
            if (!XferUploaders.ContainsKey(transactionID))
            {
                AssetXferUploader uploader = new AssetXferUploader(this, m_dumpAssetsToFile);

                XferUploaders.Add(transactionID, uploader);
                return uploader;
            }
            return null;
        }

        public void HandleXfer(ulong xferID, uint packetID, byte[] data)
        {
            foreach (AssetXferUploader uploader in XferUploaders.Values)
            {
                if (uploader.XferID == xferID)
                {
                    uploader.HandleXferPacket(xferID, packetID, data);
                    break;
                }
            }
        }

        public void RequestCreateInventoryItem(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID,
                                               uint callbackID, string description, string name, sbyte invType,
                                               sbyte type, byte wearableType, uint nextOwnerMask)
        {
            if (XferUploaders.ContainsKey(transactionID))
            {
                XferUploaders[transactionID].RequestCreateInventoryItem(remoteClient, transactionID, folderID,
                                                                        callbackID, description, name, invType, type,
                                                                        wearableType, nextOwnerMask);
            }
        }

        public AssetBase GetTransactionAsset(LLUUID transactionID)
        {
            if (XferUploaders.ContainsKey(transactionID))
            {
                return XferUploaders[transactionID].GetAssetData();
            }
            return null;
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
            private bool m_dumpImageToFile;
            private string uploaderPath = "";

            // Events
            public event UpLoadedAsset OnUpLoad;

            // Methods
            public void Initialise(string assetName, string assetDescription, LLUUID assetID, LLUUID inventoryItem,
                                   LLUUID folderID, string path, BaseHttpServer httpServer, bool dumpImageToFile)
            {
                m_assetName = assetName;
                m_assetDescription = assetDescription;
                m_folderID = folderID;
                newAssetID = assetID;
                inventoryItemID = inventoryItem;
                uploaderPath = path;
                httpListener = httpServer;
                m_dumpImageToFile = dumpImageToFile;
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
                complete.new_asset = newAssetID.ToStringHyphenated();
                complete.new_inventory_item = inventoryItemID;
                complete.state = "complete";
                text = LLSDHelpers.SerialiseLLSDReply(complete);
                httpListener.RemoveStreamHandler("POST", uploaderPath);
                if (m_dumpImageToFile)
                {
                    SaveImageToFile(m_assetName + ".jp2", data);
                }
                if (OnUpLoad != null)
                {
                    OnUpLoad(m_assetName, "description", newAssetID, inventoryItemID, LLUUID.Zero, data, "", "");
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
            private bool m_storeLocal;
            private bool m_dumpAssetToFile;

            public AssetXferUploader(AgentAssetTransactions transactions, bool dumpAssetToFile)
            {
                m_userTransactions = transactions;
                m_dumpAssetToFile = dumpAssetToFile;
            }

            // Methods
            public void HandleXferPacket(ulong xferID, uint packetID, byte[] data)
            {
                if (XferID == xferID)
                {
                    if (Asset.Data.Length > 1)
                    {
                        byte[] destinationArray = new byte[Asset.Data.Length + data.Length];
                        Array.Copy(Asset.Data, 0, destinationArray, 0, Asset.Data.Length);
                        Array.Copy(data, 0, destinationArray, Asset.Data.Length, data.Length);
                        Asset.Data = destinationArray;
                    }
                    else
                    {
                        byte[] buffer2 = new byte[data.Length - 4];
                        Array.Copy(data, 4, buffer2, 0, data.Length - 4);
                        Asset.Data = buffer2;
                    }
                    ConfirmXferPacketPacket newPack = new ConfirmXferPacketPacket();
                    newPack.XferID.ID = xferID;
                    newPack.XferID.Packet = packetID;
                    ourClient.OutPacket(newPack, ThrottleOutPacketType.Asset);
                    if ((packetID & 0x80000000) != 0)
                    {
                        SendCompleteMessage();
                    }
                }
            }

            public void Initialise(IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type, byte[] data,
                                   bool storeLocal)
            {
                ourClient = remoteClient;
                Asset = new AssetBase();
                Asset.FullID = assetID;
                Asset.InvType = type;
                Asset.Type = type;
                Asset.Data = data;
                Asset.Name = "blank";
                Asset.Description = "empty";
                TransactionID = transaction;
                m_storeLocal = storeLocal;
                if (Asset.Data.Length > 2)
                {
                    SendCompleteMessage();
                }
                else
                {
                    ReqestStartXfer();
                }
            }

            protected void ReqestStartXfer()
            {
                UploadComplete = false;
                XferID = Util.GetNextXferID();
                RequestXferPacket newPack = new RequestXferPacket();
                newPack.XferID.ID = XferID;
                newPack.XferID.VFileType = Asset.Type;
                newPack.XferID.VFileID = Asset.FullID;
                newPack.XferID.FilePath = 0;
                newPack.XferID.Filename = new byte[0];
                ourClient.OutPacket(newPack, ThrottleOutPacketType.Asset);
            }

            protected void SendCompleteMessage()
            {
                UploadComplete = true;
                AssetUploadCompletePacket newPack = new AssetUploadCompletePacket();
                newPack.AssetBlock.Type = Asset.Type;
                newPack.AssetBlock.Success = true;
                newPack.AssetBlock.UUID = Asset.FullID;
                ourClient.OutPacket(newPack, ThrottleOutPacketType.Asset);
                m_finished = true;
                if (m_createItem)
                {
                    DoCreateItem();
                }
                else if (m_storeLocal)
                {
                    m_userTransactions.Manager.CommsManager.AssetCache.AddAsset(Asset);
                }

                // Console.WriteLine("upload complete "+ this.TransactionID);

                if (m_dumpAssetToFile)
                {
                    DateTime now = DateTime.Now;
                    string filename =
                        String.Format("{6}_{7}_{0:d2}{1:d2}{2:d2}_{3:d2}{4:d2}{5:d2}.dat", now.Year, now.Month, now.Day,
                                      now.Hour, now.Minute, now.Second, Asset.Name, Asset.Type);
                    SaveAssetToFile(filename, Asset.Data);
                }
            }

            private void SaveAssetToFile(string filename, byte[] data)
            {
                FileStream fs = File.Create(filename);
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();
            }

            public void RequestCreateInventoryItem(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID,
                                                   uint callbackID, string description, string name, sbyte invType,
                                                   sbyte type, byte wearableType, uint nextOwnerMask)
            {
                if (TransactionID == transactionID)
                {
                    InventFolder = folderID;
                    m_name = name;
                    m_description = description;
                    this.type = type;
                    this.invType = invType;
                    nextPerm = nextOwnerMask;
                    Asset.Name = name;
                    Asset.Description = description;
                    Asset.Type = type;
                    Asset.InvType = invType;
                    m_createItem = true;
                    if (m_finished)
                    {
                        DoCreateItem();
                    }
                }
            }

            private void DoCreateItem()
            {
                //really need to fix this call, if lbsa71 saw this he would die. 
                m_userTransactions.Manager.CommsManager.AssetCache.AddAsset(Asset);
                CachedUserInfo userInfo =
                    m_userTransactions.Manager.CommsManager.UserProfileCacheService.GetUserDetails(ourClient.AgentId);
                if (userInfo != null)
                {
                    InventoryItemBase item = new InventoryItemBase();
                    item.avatarID = ourClient.AgentId;
                    item.creatorsID = ourClient.AgentId;
                    item.inventoryID = LLUUID.Random();
                    item.assetID = Asset.FullID;
                    item.inventoryDescription = m_description;
                    item.inventoryName = m_name;
                    item.assetType = type;
                    item.invType = invType;
                    item.parentFolderID = InventFolder;
                    item.inventoryCurrentPermissions = 2147483647;
                    item.inventoryNextPermissions = nextPerm;

                    userInfo.AddItem(ourClient.AgentId, item);
                    ourClient.SendInventoryItemCreateUpdate(item);
                }
            }

            public void UpdateInventoryItem(LLUUID itemID)
            {
            }

            public AssetBase GetAssetData()
            {
                if (m_finished)
                {
                    return Asset;
                }
                return null;
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
                inventoryItemID = inventoryItem;
                uploaderPath = path;
                httpListener = httpServer;
                newAssetID = LLUUID.Random();
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
                complete.new_asset = newAssetID.ToStringHyphenated();
                complete.new_inventory_item = inventoryItemID;
                complete.state = "complete";
                text = LLSDHelpers.SerialiseLLSDReply(complete);
                httpListener.RemoveStreamHandler("POST", uploaderPath);
                if (SaveImages)
                {
                    SaveImageToFile(m_assetName + "notecard.txt", data);
                }
                if (OnUpLoad != null)
                {
                    OnUpLoad(m_assetName, "description", newAssetID, inventoryItemID, LLUUID.Zero, data, "", "");
                }
                return text;
            }
        }
    }
}
