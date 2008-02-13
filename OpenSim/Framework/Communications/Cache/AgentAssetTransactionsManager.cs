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

using libsecondlife;

namespace OpenSim.Framework.Communications.Cache
{
    /// <summary>
    /// Provider handlers for processing asset transactions originating from the agent.  This encompasses
    /// clothing creation and update as well as asset uploads.
    /// </summary>
    public class AgentAssetTransactionsManager
    {
        private static readonly log4net.ILog m_log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        // Fields
        public CommunicationsManager CommsManager;

        /// <summary>
        /// Each agent has its own singleton collection of transactions
        /// </summary>
        private Dictionary<LLUUID, AgentAssetTransactions> AgentTransactions =
            new Dictionary<LLUUID, AgentAssetTransactions>();

        /// <summary>
        /// Should we dump uploaded assets to the filesystem?
        /// </summary>
        private bool m_dumpAssetsToFile;

        public AgentAssetTransactionsManager(CommunicationsManager commsManager, bool dumpAssetsToFile)
        {
            CommsManager = commsManager;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        /// <summary>
        /// Get the collection of asset transactions for the given user.  If one does not already exist, it
        /// is created.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        private AgentAssetTransactions GetUserTransactions(LLUUID userID)
        {
            lock (AgentTransactions)
            {
                if (!AgentTransactions.ContainsKey(userID))
                {
                    AgentAssetTransactions transactions 
                        = new AgentAssetTransactions(userID, this, m_dumpAssetsToFile);
                    AgentTransactions.Add(userID, transactions);  
                }
                
                return AgentTransactions[userID];                
            }            
        }
        
        /// <summary>
        /// Remove the given agent asset transactions.  This should be called when a client is departing
        /// from a scene (and hence won't be making any more transactions here).
        /// </summary>
        /// <param name="userID"></param>
        public void RemoveAgentAssetTransactions(LLUUID userID)
        {
            m_log.DebugFormat("Removing agent asset transactions structure for agent {0}", userID);
            
            lock (AgentTransactions)
            {
                AgentTransactions.Remove(userID);
            }
        }
        
        /// <summary>
        /// Create an inventory item from data that has been received through a transaction.
        /// 
        /// This is called when new clothing or body parts are created.  It may also be called in other
        /// situations.
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
        public void HandleItemCreationFromTransaction(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID,
                                                      uint callbackID, string description, string name, sbyte invType,
                                                      sbyte type, byte wearableType, uint nextOwnerMask)
        {
            m_log.DebugFormat(
                "[TRANSACTIONS MANAGER] Called HandleItemCreationFromTransaction with item {0}", name);
            
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);
            
            transactions.RequestCreateInventoryItem(
                remoteClient, transactionID, folderID, callbackID, description,
                name, invType, type, wearableType, nextOwnerMask);
        }  
           
        /// <summary>
        /// Update an inventory item with data that has been received through a transaction.
        /// 
        /// This is called when clothing or body parts are updated (for instance, with new textures or 
        /// colours).  It may also be called in other situations.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID"></param>
        /// <param name="item"></param>
        public void HandleItemUpdateFromTransaction(IClientAPI remoteClient, LLUUID transactionID, 
                                                    InventoryItemBase item)
        {
            m_log.DebugFormat(
                "[TRANSACTIONS MANAGER] Called HandleItemUpdateFromTransaction with item {0}", 
                item.inventoryName);
            
            AgentAssetTransactions transactions
                = CommsManager.TransactionsManager.GetUserTransactions(remoteClient.AgentId);

            transactions.RequestUpdateInventoryItem(remoteClient, transactionID, item);        
        }

        /// <summary>
        /// Request that a client (agent) begin an asset transfer.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="assetID"></param>
        /// <param name="transaction"></param>
        /// <param name="type"></param>
        /// <param name="data"></param></param>
        /// <param name="tempFile"></param>
        public void HandleUDPUploadRequest(IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type,
                                           byte[] data, bool storeLocal, bool tempFile)
        {
            // Console.WriteLine("asset upload of " + assetID);
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);

            AgentAssetTransactions.AssetXferUploader uploader = transactions.RequestXferUploader(transaction);
            if (uploader != null)
            {
                // Upload has already compelted uploading...
                
                if (uploader.Initialise(remoteClient, assetID, transaction, type, data, storeLocal, tempFile))
                {
                    //[commenting out as this removal breaks uploads]
                   /*lock (transactions.XferUploaders)
                    {
                      
                        // XXX Weak ass way of doing this by directly manipulating this public dictionary, purely temporary
                        transactions.XferUploaders.Remove(uploader.TransactionID);
                        
                        //m_log.InfoFormat("[ASSET TRANSACTIONS] Current uploaders: {0}", transactions.XferUploaders.Count);                        
                    }*/
                }
            }
        }

        /// <summary>
        /// Handle asset transfer data packets received in response to the asset upload request in
        /// HandleUDPUploadRequest()
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="xferID"></param>
        /// <param name="packetID"></param>
        /// <param name="data"></param>
        public void HandleXfer(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);
            
            transactions.HandleXfer(xferID, packetID, data);
        }
    }
}
