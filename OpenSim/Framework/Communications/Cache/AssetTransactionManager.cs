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
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Data;

namespace OpenSim.Framework.Communications.Caches
{
    public class AssetTransactionManager
    {
        // Fields
        public CommunicationsManager CommsManager;
        public Dictionary<LLUUID, AgentAssetTransactions> AgentTransactions = new Dictionary<LLUUID, AgentAssetTransactions>();

        public AssetTransactionManager(CommunicationsManager commsManager)
        {
            CommsManager = commsManager;
        }

        // Methods
        public AgentAssetTransactions AddUser(LLUUID userID)
        {
            if (!this.AgentTransactions.ContainsKey(userID))
            {
                AgentAssetTransactions transactions = new AgentAssetTransactions(userID, this);
                this.AgentTransactions.Add(userID, transactions);
                return transactions;
            }
            return null;
        }

        public AgentAssetTransactions GetUserTransActions(LLUUID userID)
        {
            if (this.AgentTransactions.ContainsKey(userID))
            {
                return this.AgentTransactions[userID];
            }
            return null;
        }

        public void HandleInventoryFromTransaction(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID, uint callbackID, string description, string name, sbyte invType, sbyte type, byte wearableType, uint nextOwnerMask)
        {
            AgentAssetTransactions transactions = this.GetUserTransActions(remoteClient.AgentId);
            if (transactions != null)
            {
                transactions.RequestCreateInventoryItem(remoteClient, transactionID, folderID, callbackID, description, name, invType, type, wearableType, nextOwnerMask);
            }

        }

        public void HandleUDPUploadRequest(IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type, byte[] data, bool storeLocal)
        {
           // Console.WriteLine("asset upload of " + assetID);
            AgentAssetTransactions transactions = this.GetUserTransActions(remoteClient.AgentId);
            if (transactions != null)
            {
                AgentAssetTransactions.AssetXferUploader uploader = transactions.RequestXferUploader(transaction);
                if (uploader != null)
                {
                    uploader.Initialise(remoteClient, assetID, transaction, type, data, storeLocal);
                }
            }
        }

        public void HandleXfer(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            AgentAssetTransactions transactions = this.GetUserTransActions(remoteClient.AgentId);
            if (transactions != null)
            {
                transactions.HandleXfer(xferID, packetID, data);
            }
        }
    }
}


