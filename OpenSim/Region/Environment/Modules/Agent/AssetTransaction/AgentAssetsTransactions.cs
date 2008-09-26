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
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Agent.AssetTransaction
{
    /// <summary>
    /// Manage asset transactions for a single agent.
    /// </summary>
    public class AgentAssetTransactions
    {
        //private static readonly log4net.ILog m_log
        //   = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Fields
        private bool m_dumpAssetsToFile;
        public AgentAssetTransactionsManager Manager;
        public UUID UserID;
        public Dictionary<UUID, AssetXferUploader> XferUploaders = new Dictionary<UUID, AssetXferUploader>();

        // Methods
        public AgentAssetTransactions(UUID agentID, AgentAssetTransactionsManager manager, bool dumpAssetsToFile)
        {
            UserID = agentID;
            Manager = manager;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        public AssetXferUploader RequestXferUploader(UUID transactionID)
        {
            if (!XferUploaders.ContainsKey(transactionID))
            {
                AssetXferUploader uploader = new AssetXferUploader(this, m_dumpAssetsToFile);

                lock (XferUploaders)
                {
                    XferUploaders.Add(transactionID, uploader);
                }

                return uploader;
            }
            return null;
        }

        public void HandleXfer(ulong xferID, uint packetID, byte[] data)
        {         
            lock (XferUploaders)
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
        }

        public void RequestCreateInventoryItem(IClientAPI remoteClient, UUID transactionID, UUID folderID,
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

        public void RequestUpdateInventoryItem(IClientAPI remoteClient, UUID transactionID,
                                               InventoryItemBase item)
        {
            if (XferUploaders.ContainsKey(transactionID))
            {
                XferUploaders[transactionID].RequestUpdateInventoryItem(remoteClient, transactionID, item);
            }
        }
        
        public void RequestUpdateTaskInventoryItem(
            IClientAPI remoteClient, SceneObjectPart part, UUID transactionID, TaskInventoryItem item)
        {      
            if (XferUploaders.ContainsKey(transactionID))
            {
                XferUploaders[transactionID].RequestUpdateTaskInventoryItem(remoteClient, part, transactionID, item);
            } 
        }

        /// <summary>
        /// Get an uploaded asset.  If the data is successfully retrieved, the transaction will be removed.
        /// </summary>
        /// <param name="transactionID"></param>
        /// <returns>The asset if the upload has completed, null if it has not.</returns>
        public AssetBase GetTransactionAsset(UUID transactionID)
        {
            if (XferUploaders.ContainsKey(transactionID))
            {
                AssetXferUploader uploader = XferUploaders[transactionID];
                AssetBase asset = uploader.GetAssetData();

                lock (XferUploaders)
                {
                    XferUploaders.Remove(transactionID);
                }

                return asset;
            }

            return null;
        }

        // Nested Types

        #region Nested type: AssetXferUploader

        public class AssetXferUploader
        {
            private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            
            // Fields
            public bool AddToInventory;
            public AssetBase Asset;
            public UUID InventFolder = UUID.Zero;
            private sbyte invType = 0;
            private bool m_createItem = false;
            private string m_description = String.Empty;
            private bool m_dumpAssetToFile;
            private bool m_finished = false;
            private string m_name = String.Empty;
            private bool m_storeLocal;
            private AgentAssetTransactions m_userTransactions;
            private uint nextPerm = 0;
            private IClientAPI ourClient;
            public UUID TransactionID = UUID.Zero;
            private sbyte type = 0;
            public bool UploadComplete;
            private byte wearableType = 0;
            public ulong XferID;

            public AssetXferUploader(AgentAssetTransactions transactions, bool dumpAssetToFile)
            {
                m_userTransactions = transactions;
                m_dumpAssetToFile = dumpAssetToFile;
            }

            /// <summary>
            /// Process transfer data received from the client.
            /// </summary>
            /// <param name="xferID"></param>
            /// <param name="packetID"></param>
            /// <param name="data"></param>
            /// <returns>True if the transfer is complete, false otherwise or if the xferID was not valid</returns>
            public bool HandleXferPacket(ulong xferID, uint packetID, byte[] data)
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

                    ourClient.SendConfirmXfer(xferID, packetID);

                    if ((packetID & 0x80000000) != 0)
                    {
                        SendCompleteMessage();
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Initialise asset transfer from the client
            /// </summary>
            /// <param name="xferID"></param>
            /// <param name="packetID"></param>
            /// <param name="data"></param>
            /// <returns>True if the transfer is complete, false otherwise</returns>
            public bool Initialise(IClientAPI remoteClient, UUID assetID, UUID transaction, sbyte type, byte[] data,
                                   bool storeLocal, bool tempFile)
            {
                ourClient = remoteClient;
                Asset = new AssetBase();
                Asset.FullID = assetID;
                Asset.Type = type;
                Asset.Data = data;
                Asset.Name = "blank";
                Asset.Description = "empty";
                Asset.Local = storeLocal;
                Asset.Temporary = tempFile;

                TransactionID = transaction;
                m_storeLocal = storeLocal;
                
                if (Asset.Data.Length > 2)
                {
                    SendCompleteMessage();
                    return true;
                }
                else
                {
                    RequestStartXfer();
                }

                return false;
            }

            protected void RequestStartXfer()
            {
                UploadComplete = false;
                XferID = Util.GetNextXferID();
                ourClient.SendXferRequest(XferID, Asset.Type, Asset.FullID, 0, new byte[0]);
            }

            protected void SendCompleteMessage()
            {
                UploadComplete = true;

                ourClient.SendAssetUploadCompleteMessage(Asset.Type, true, Asset.FullID);

                m_finished = true;
                if (m_createItem)
                {
                    DoCreateItem();
                }
                else if (m_storeLocal)
                {
                    m_userTransactions.Manager.MyScene.CommsManager.AssetCache.AddAsset(Asset);
                }

                m_log.DebugFormat("[ASSET TRANSACTIONS]: Uploaded asset data for transaction {0}", TransactionID);

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
                string assetPath = "UserAssets";
                if (!Directory.Exists(assetPath))
                {
                    Directory.CreateDirectory(assetPath);
                }
                FileStream fs = File.Create(Path.Combine(assetPath, filename));
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(data);
                bw.Close();
                fs.Close();
            }

            public void RequestCreateInventoryItem(IClientAPI remoteClient, UUID transactionID, UUID folderID,
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
                    this.wearableType = wearableType;
                    nextPerm = nextOwnerMask;
                    Asset.Name = name;
                    Asset.Description = description;
                    Asset.Type = type;
                    m_createItem = true;
                    
                    if (m_finished)
                    {
                        DoCreateItem();
                    }
                }
            }

            public void RequestUpdateInventoryItem(IClientAPI remoteClient, UUID transactionID,
                                                   InventoryItemBase item)
            {
                if (TransactionID == transactionID)
                {
                    CachedUserInfo userInfo =
                        m_userTransactions.Manager.MyScene.CommsManager.UserProfileCacheService.GetUserDetails(
                            remoteClient.AgentId);

                    if (userInfo != null)
                    {
                        UUID assetID = UUID.Combine(transactionID, remoteClient.SecureSessionId);

                        AssetBase asset
                            = m_userTransactions.Manager.MyScene.CommsManager.AssetCache.GetAsset(
                                assetID, (item.AssetType == (int) AssetType.Texture ? true : false));

                        if (asset == null)
                        {
                            asset = m_userTransactions.GetTransactionAsset(transactionID);
                        }

                        if (asset != null && asset.FullID == assetID)
                        {
                            // Assets never get updated, new ones get created
                            asset.FullID = UUID.Random();
                            asset.Name = item.Name;
                            asset.Description = item.Description;
                            asset.Type = (sbyte) item.AssetType;
                            item.AssetID = asset.FullID;

                            m_userTransactions.Manager.MyScene.CommsManager.AssetCache.AddAsset(Asset);
                        }

                        userInfo.UpdateItem(item);
                    }
                }
            }
            
            public void RequestUpdateTaskInventoryItem(
                IClientAPI remoteClient, SceneObjectPart part, UUID transactionID, TaskInventoryItem item)
            {
                m_log.DebugFormat(
                    "[ASSET TRANSACTIONS]: Updating task item {0} in {1} with asset in transaction {2}", 
                    item.Name, part.Name, transactionID);
                
                Asset.Name = item.Name;
                Asset.Description = item.Description;
                Asset.Type = (sbyte) item.Type;
                item.AssetID = Asset.FullID;
                
                m_userTransactions.Manager.MyScene.CommsManager.AssetCache.AddAsset(Asset);
                
                if (part.UpdateInventoryItem(item))
                    part.GetProperties(remoteClient);                 
            }              
                        
            private void DoCreateItem()
            {
                //really need to fix this call, if lbsa71 saw this he would die.
                m_userTransactions.Manager.MyScene.CommsManager.AssetCache.AddAsset(Asset);
                CachedUserInfo userInfo =
                    m_userTransactions.Manager.MyScene.CommsManager.UserProfileCacheService.GetUserDetails(ourClient.AgentId);
                if (userInfo != null)
                {
                    InventoryItemBase item = new InventoryItemBase();
                    item.Owner = ourClient.AgentId;
                    item.Creator = ourClient.AgentId;
                    item.ID = UUID.Random();
                    item.AssetID = Asset.FullID;
                    item.Description = m_description;
                    item.Name = m_name;
                    item.AssetType = type;
                    item.InvType = invType;
                    item.Folder = InventFolder;
                    item.BasePermissions = 0x7fffffff;
                    item.CurrentPermissions = 0x7fffffff;
                    item.EveryOnePermissions=0;
                    item.NextPermissions = nextPerm;
                    item.Flags = (uint) wearableType;

                    userInfo.AddItem(item);
                    ourClient.SendInventoryItemCreateUpdate(item);
                }
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

        #endregion
    }
}
