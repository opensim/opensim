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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Agent.AssetTransaction
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AssetTransactionModule")]
    public class AssetTransactionModule : INonSharedRegionModule,
            IAgentAssetTransactions
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Scene m_Scene;
        private bool m_dumpAssetsToFile = false;
        private int  m_levelUpload = 0;

        /// <summary>
        /// Each agent has its own singleton collection of transactions
        /// </summary>
        private Dictionary<UUID, AgentAssetTransactions> AgentTransactions =
            new Dictionary<UUID, AgentAssetTransactions>();
        
        #region Region Module interface

        public void Initialise(IConfigSource source)
        {
            IConfig sconfig = source.Configs["Startup"];
            if (sconfig != null)
            {
                m_levelUpload = sconfig.GetInt("LevelUpload", 0);
            }
        }

        public void AddRegion(Scene scene)
        {
            m_Scene = scene;
            scene.RegisterModuleInterface<IAgentAssetTransactions>(this);
            scene.EventManager.OnNewClient += NewClient;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "AgentTransactionModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return typeof(IAgentAssetTransactions); }
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            client.OnAssetUploadRequest += HandleUDPUploadRequest;
            client.OnXferReceive += HandleXfer;
        }

        #region AgentAssetTransactions
        /// <summary>
        /// Get the collection of asset transactions for the given user.
        /// If one does not already exist, it is created.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        private AgentAssetTransactions GetUserTransactions(UUID userID)
        {
            lock (AgentTransactions)
            {
                if (!AgentTransactions.ContainsKey(userID))
                {
                    AgentAssetTransactions transactions =
                            new AgentAssetTransactions(userID, m_Scene,
                            m_dumpAssetsToFile);

                    AgentTransactions.Add(userID, transactions);
                }

                return AgentTransactions[userID];
            }
        }

        /// <summary>
        /// Remove the given agent asset transactions. This should be called
        /// when a client is departing from a scene (and hence won't be making
        /// any more transactions here).
        /// </summary>
        /// <param name="userID"></param>
        public void RemoveAgentAssetTransactions(UUID userID)
        {
            // m_log.DebugFormat("Removing agent asset transactions structure for agent {0}", userID);

            lock (AgentTransactions)
            {
                AgentTransactions.Remove(userID);
            }
        }

        /// <summary>
        /// Create an inventory item from data that has been received through
        /// a transaction.
        /// This is called when new clothing or body parts are created.
        /// It may also be called in other situations.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="description"></param>
        /// <param name="name"></param>
        /// <param name="invType"></param>
        /// <param name="type"></param>
        /// <param name="wearableType"></param>
        /// <param name="nextOwnerMask"></param>
        public bool HandleItemCreationFromTransaction(IClientAPI remoteClient,
                UUID transactionID, UUID folderID, uint callbackID,
                string description, string name, sbyte invType,
                sbyte type, byte wearableType, uint nextOwnerMask)
        {
//            m_log.DebugFormat(
//                "[TRANSACTIONS MANAGER] Called HandleItemCreationFromTransaction with item {0}", name);

            AgentAssetTransactions transactions =
                    GetUserTransactions(remoteClient.AgentId);

            return transactions.RequestCreateInventoryItem(remoteClient, transactionID,
                    folderID, callbackID, description, name, invType, type,
                    wearableType, nextOwnerMask);
        }

        /// <summary>
        /// Update an inventory item with data that has been received through a
        /// transaction.
        /// </summary>
        /// <remarks>
        /// This is called when clothing or body parts are updated (for
        /// instance, with new textures or colours). It may also be called in
        /// other situations.
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID"></param>
        /// <param name="item"></param>
        public void HandleItemUpdateFromTransaction(IClientAPI remoteClient,
                UUID transactionID, InventoryItemBase item)
        {
//            m_log.DebugFormat(
//                "[ASSET TRANSACTION MODULE]: Called HandleItemUpdateFromTransaction with item {0}",
//                item.Name);

            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);

            transactions.RequestUpdateInventoryItem(remoteClient, transactionID, item);
        }

        /// <summary>
        /// Update a task inventory item with data that has been received
        /// through a transaction.
        ///
        /// This is currently called when, for instance, a notecard in a prim
        /// is saved. The data is sent up through a single AssetUploadRequest.
        /// A subsequent UpdateTaskInventory then references the transaction
        /// and comes through this method.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="part"></param>
        /// <param name="transactionID"></param>
        /// <param name="item"></param>
        public void HandleTaskItemUpdateFromTransaction(
            IClientAPI remoteClient, SceneObjectPart part, UUID transactionID, TaskInventoryItem item)
        {
//            m_log.DebugFormat(
//                "[ASSET TRANSACTION MODULE]: Called HandleTaskItemUpdateFromTransaction with item {0} in {1} for {2} in {3}",
//                item.Name, part.Name, remoteClient.Name, m_Scene.RegionInfo.RegionName);

            AgentAssetTransactions transactions =
                    GetUserTransactions(remoteClient.AgentId);

            transactions.RequestUpdateTaskInventoryItem(remoteClient, part,
                    transactionID, item);
        }

        /// <summary>
        /// Request that a client (agent) begin an asset transfer.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="assetID"></param>
        /// <param name="transactionID"></param>
        /// <param name="type"></param>
        /// <param name="data"></param></param>
        /// <param name="tempFile"></param>
        public void HandleUDPUploadRequest(IClientAPI remoteClient,
                UUID assetID, UUID transactionID, sbyte type, byte[] data,
                bool storeLocal, bool tempFile)
        {
//            m_log.DebugFormat(
//                "[ASSET TRANSACTION MODULE]: HandleUDPUploadRequest - assetID: {0}, transaction {1}, type {2}, storeLocal {3}, tempFile {4}, data.Length {5}",
//                assetID, transactionID, type, storeLocal, tempFile, data.Length);
            
            if (((AssetType)type == AssetType.Texture ||
                (AssetType)type == AssetType.Sound ||
                (AssetType)type == AssetType.TextureTGA ||
                (AssetType)type == AssetType.Animation) &&
                tempFile == false)
            {
                ScenePresence avatar = null;
                Scene scene = (Scene)remoteClient.Scene;
                scene.TryGetScenePresence(remoteClient.AgentId, out avatar);

                // check user level
                if (avatar != null)
                {
                    if (avatar.UserLevel < m_levelUpload)
                    {
                        remoteClient.SendAgentAlertMessage("Unable to upload asset. Insufficient permissions.", false);
                        return;
                    }
                }

                // check funds
                IMoneyModule mm = scene.RequestModuleInterface<IMoneyModule>();

                if (mm != null)
                {
                    if (!mm.UploadCovered(remoteClient.AgentId, mm.UploadCharge))
                    {
                        remoteClient.SendAgentAlertMessage("Unable to upload asset. Insufficient funds.", false);
                        return;
                    }
                }
            }

            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);
            AssetXferUploader uploader = transactions.RequestXferUploader(transactionID);
            uploader.StartUpload(remoteClient, assetID, transactionID, type, data, storeLocal, tempFile);
        }

        /// <summary>
        /// Handle asset transfer data packets received in response to the
        /// asset upload request in HandleUDPUploadRequest()
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="xferID"></param>
        /// <param name="packetID"></param>
        /// <param name="data"></param>
        public void HandleXfer(IClientAPI remoteClient, ulong xferID,
                uint packetID, byte[] data)
        {
//            m_log.Debug("xferID: " + xferID + "  packetID: " + packetID + "  data length " + data.Length);
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);

            transactions.HandleXfer(xferID, packetID, data);
        }

        #endregion
    }
}
