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
using OpenMetaverse;
using OpenSim.Framework;
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
    }
}
