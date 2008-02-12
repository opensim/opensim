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
    /// Manage the collection of agent asset transaction collections.  Each agent has its own transaction collection
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
        /// Add a collection of asset transactions for the given user
        /// </summary>
        /// <param name="userID"></param>
        public void AddUser(LLUUID userID)
        {
            lock (AgentTransactions)
            {
                if (!AgentTransactions.ContainsKey(userID))
                {
                    AgentAssetTransactions transactions = new AgentAssetTransactions(userID, this, m_dumpAssetsToFile);
                    AgentTransactions.Add(userID, transactions);
                }
            }
        }

        /// <summary>
        /// Get the collection of asset transactions for the given user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if this agent does not have an asset transactions collection</returns>
        public AgentAssetTransactions GetUserTransactions(LLUUID userID)
        {
            if (AgentTransactions.ContainsKey(userID))
            {
                return AgentTransactions[userID];
            }
            
            return null;
        }

        public void HandleUDPUploadRequest(IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type,
                                           byte[] data, bool storeLocal, bool tempFile)
        {
            // Console.WriteLine("asset upload of " + assetID);
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);
            if (transactions != null)
            {
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
        }

        public void HandleXfer(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);
            if (transactions != null)
            {
                transactions.HandleXfer(xferID, packetID, data);
            }
        }
    }
}
