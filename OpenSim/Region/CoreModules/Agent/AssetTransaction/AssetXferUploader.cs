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
using System.IO;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Agent.AssetTransaction
{
    public class AssetXferUploader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private AssetBase m_asset;
        private UUID InventFolder = UUID.Zero;
        private sbyte invType = 0;
        private bool m_createItem = false;
        private uint m_createItemCallback = 0;
        private string m_description = String.Empty;
        private bool m_dumpAssetToFile;
        private bool m_finished = false;
        private string m_name = String.Empty;
        private bool m_storeLocal;
        private uint nextPerm = 0;
        private IClientAPI ourClient;
        private UUID TransactionID = UUID.Zero;
        private sbyte type = 0;
        private byte wearableType = 0;
        public ulong XferID;
        private Scene m_Scene;

        public AssetXferUploader(Scene scene, bool dumpAssetToFile)
        {
            m_Scene = scene;
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
                if (m_asset.Data.Length > 1)
                {
                    byte[] destinationArray = new byte[m_asset.Data.Length + data.Length];
                    Array.Copy(m_asset.Data, 0, destinationArray, 0, m_asset.Data.Length);
                    Array.Copy(data, 0, destinationArray, m_asset.Data.Length, data.Length);
                    m_asset.Data = destinationArray;
                }
                else
                {
                    byte[] buffer2 = new byte[data.Length - 4];
                    Array.Copy(data, 4, buffer2, 0, data.Length - 4);
                    m_asset.Data = buffer2;
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
        public bool Initialise(IClientAPI remoteClient, UUID assetID,
                UUID transaction, sbyte type, byte[] data, bool storeLocal,
                bool tempFile)
        {
            ourClient = remoteClient;
            m_asset = new AssetBase(assetID, "blank", type,
                    remoteClient.AgentId.ToString());
            m_asset.Data = data;
            m_asset.Description = "empty";
            m_asset.Local = storeLocal;
            m_asset.Temporary = tempFile;

            TransactionID = transaction;
            m_storeLocal = storeLocal;

            if (m_asset.Data.Length > 2)
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
            XferID = Util.GetNextXferID();
            ourClient.SendXferRequest(XferID, m_asset.Type, m_asset.FullID,
                    0, new byte[0]);
        }

        protected void SendCompleteMessage()
        {
            ourClient.SendAssetUploadCompleteMessage(m_asset.Type, true,
                    m_asset.FullID);

            m_finished = true;
            if (m_createItem)
            {
                DoCreateItem(m_createItemCallback);
            }
            else if (m_storeLocal)
            {
                m_Scene.AssetService.Store(m_asset);
            }

            m_log.DebugFormat(
                "[ASSET TRANSACTIONS]: Uploaded asset {0} for transaction {1}",
                m_asset.FullID, TransactionID);

            if (m_dumpAssetToFile)
            {
                DateTime now = DateTime.Now;
                string filename =
                        String.Format("{6}_{7}_{0:d2}{1:d2}{2:d2}_{3:d2}{4:d2}{5:d2}.dat",
                        now.Year, now.Month, now.Day, now.Hour, now.Minute,
                        now.Second, m_asset.Name, m_asset.Type);
                SaveAssetToFile(filename, m_asset.Data);
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

        public void RequestCreateInventoryItem(IClientAPI remoteClient,
                UUID transactionID, UUID folderID, uint callbackID,
                string description, string name, sbyte invType,
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
                m_asset.Name = name;
                m_asset.Description = description;
                m_asset.Type = type;

                if (m_finished)
                {
                    DoCreateItem(callbackID);
                }
                else
                {
                    m_createItem = true; //set flag so the inventory item is created when upload is complete
                    m_createItemCallback = callbackID;
                }
            }
        }

        private void DoCreateItem(uint callbackID)
        {
            m_Scene.AssetService.Store(m_asset);

            InventoryItemBase item = new InventoryItemBase();
            item.Owner = ourClient.AgentId;
            item.CreatorId = ourClient.AgentId.ToString();
            item.ID = UUID.Random();
            item.AssetID = m_asset.FullID;
            item.Description = m_description;
            item.Name = m_name;
            item.AssetType = type;
            item.InvType = invType;
            item.Folder = InventFolder;
            item.BasePermissions = 0x7fffffff;
            item.CurrentPermissions = 0x7fffffff;
            item.GroupPermissions=0;
            item.EveryOnePermissions=0;
            item.NextPermissions = nextPerm;
            item.Flags = (uint) wearableType;
            item.CreationDate = Util.UnixTimeSinceEpoch();

            if (m_Scene.AddInventoryItem(item))
                ourClient.SendInventoryItemCreateUpdate(item, callbackID);
            else
                ourClient.SendAlertMessage("Unable to create inventory item");
        }

        /// <summary>
        /// Get the asset data uploaded in this transfer.
        /// </summary>
        /// <returns>null if the asset has not finished uploading</returns>
        public AssetBase GetAssetData()
        {
            if (m_finished)
            {
                return m_asset;
            }

            return null;
        }
    }
}
