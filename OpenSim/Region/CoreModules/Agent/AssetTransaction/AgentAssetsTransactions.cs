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

using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Agent.AssetTransaction
{
    /// <summary>
    /// Manage asset transactions for a single agent.
    /// </summary>
    public class AgentAssetTransactions
    {
//        private static readonly ILog m_log = LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        // Fields
        private bool m_dumpAssetsToFile;
        private Scene m_Scene;
        public UUID UserID;
        public Dictionary<UUID, AssetXferUploader> XferUploaders =
                new Dictionary<UUID, AssetXferUploader>();

        // Methods
        public AgentAssetTransactions(UUID agentID, Scene scene,
                bool dumpAssetsToFile)
        {
            m_Scene = scene;
            UserID = agentID;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        public AssetXferUploader RequestXferUploader(UUID transactionID)
        {
            if (!XferUploaders.ContainsKey(transactionID))
            {
                AssetXferUploader uploader = new AssetXferUploader(m_Scene,
                        m_dumpAssetsToFile);

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

        public void RequestCreateInventoryItem(IClientAPI remoteClient,
                UUID transactionID, UUID folderID, uint callbackID,
                string description, string name, sbyte invType,
               sbyte type, byte wearableType, uint nextOwnerMask)
        {
            if (XferUploaders.ContainsKey(transactionID))
            {
                XferUploaders[transactionID].RequestCreateInventoryItem(
                        remoteClient, transactionID, folderID,
                        callbackID, description, name, invType, type,
                        wearableType, nextOwnerMask);
            }
        }



        /// <summary>
        /// Get an uploaded asset. If the data is successfully retrieved,
        /// the transaction will be removed.
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

        public void RequestUpdateTaskInventoryItem(IClientAPI remoteClient,
                SceneObjectPart part, UUID transactionID,
                TaskInventoryItem item)
        {
            if (XferUploaders.ContainsKey(transactionID))
            {
                AssetBase asset = GetTransactionAsset(transactionID);

                // Only legacy viewers use this, and they prefer CAPS, which 
                // we have, so this really never runs.
                // Allow it, but only for "safe" types.
                if ((InventoryType)item.InvType != InventoryType.Notecard &&
                    (InventoryType)item.InvType != InventoryType.LSL)
                    return;

                if (asset != null)
                {
                    asset.FullID = UUID.Random();
                    asset.Name = item.Name;
                    asset.Description = item.Description;
                    asset.Type = (sbyte)item.Type;
                    item.AssetID = asset.FullID;

                    m_Scene.AssetService.Store(asset);

                    part.Inventory.UpdateInventoryItem(item);
                }
            }
        }

        public void RequestUpdateInventoryItem(IClientAPI remoteClient,
                UUID transactionID, InventoryItemBase item)
        {
            if (XferUploaders.ContainsKey(transactionID))
            {
//                m_log.DebugFormat("[XFER]: Asked to update item {0} ({1})",
//                        item.Name, item.ID);

                // Here we need to get the old asset to extract the
                // texture UUIDs if it's a wearable.
                if (item.AssetType == (int)AssetType.Bodypart ||
                    item.AssetType == (int)AssetType.Clothing)
                {
                    AssetBase oldAsset = m_Scene.AssetService.Get(item.AssetID.ToString());
                    if (oldAsset != null)
                        XferUploaders[transactionID].SetOldData(oldAsset.Data);
                }

                AssetBase asset = GetTransactionAsset(transactionID);

                if (asset != null)
                {
                    asset.FullID = UUID.Random();
                    asset.Name = item.Name;
                    asset.Description = item.Description;
                    asset.Type = (sbyte)item.AssetType;
                    item.AssetID = asset.FullID;

                    m_Scene.AssetService.Store(asset);

                    IInventoryService invService = m_Scene.InventoryService;
                    invService.UpdateItem(item);

//                    m_log.DebugFormat("[XFER]: Updated item {0} ({1}) with asset {2}",
//                            item.Name, item.ID, asset.FullID);
                }
            }
        }
    }
}
