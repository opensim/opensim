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
using System.Collections.Generic;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.CoreModules.Agent.AssetTransaction
{
    public class AssetXferUploader
    {
        // Viewer's notion of the default texture
        private List<UUID> defaultIDs = new List<UUID> {
                new UUID("5748decc-f629-461c-9a36-a35a221fe21f"),
                new UUID("7ca39b4c-bd19-4699-aff7-f93fd03d3e7b"),
                new UUID("6522e74d-1660-4e7f-b601-6f48c1659a77"),
                new UUID("c228d1cf-4b5d-4ba8-84f4-899a0796aa97")
                };
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Upload state.
        /// </summary>
        /// <remarks>
        /// New -> Uploading -> Complete
        /// </remarks>
        private enum UploadState
        {
            New,
            Uploading,
            Complete
        }

        /// <summary>
        /// Reference to the object that holds this uploader.  Used to remove ourselves from it's list if we
        /// are performing a delayed update.
        /// </summary>
        AgentAssetTransactions m_transactions;

        private UploadState m_uploadState = UploadState.New;

        private AssetBase m_asset;
        private UUID InventFolder = UUID.Zero;
        private sbyte invType = 0;

        private bool m_createItem;
        private uint m_createItemCallback;

        private bool m_updateItem;
        private InventoryItemBase m_updateItemData;

        private bool m_updateTaskItem;
        private TaskInventoryItem m_updateTaskItemData;

        private string m_description = String.Empty;
        private bool m_dumpAssetToFile;
        private string m_name = String.Empty;
//        private bool m_storeLocal;
        private uint nextPerm = 0;
        private IClientAPI ourClient;

        private UUID m_transactionID;

        private sbyte type = 0;
        private byte wearableType = 0;
        private byte[] m_oldData = null;
        public ulong XferID;
        private Scene m_Scene;

        /// <summary>
        /// AssetXferUploader constructor
        /// </summary>
        /// <param name='transactions'>/param>
        /// <param name='scene'></param>
        /// <param name='transactionID'></param>
        /// <param name='dumpAssetToFile'>
        /// If true then when the asset is uploaded it is dumped to a file with the format
        /// String.Format("{6}_{7}_{0:d2}{1:d2}{2:d2}_{3:d2}{4:d2}{5:d2}.dat",
        ///   now.Year, now.Month, now.Day, now.Hour, now.Minute,
        ///   now.Second, m_asset.Name, m_asset.Type);
        /// for debugging purposes.
        /// </param>
        public AssetXferUploader(
            AgentAssetTransactions transactions, Scene scene, UUID transactionID, bool dumpAssetToFile)
        {
            m_asset = new AssetBase();

            m_transactions = transactions;
            m_transactionID = transactionID;
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
//            m_log.DebugFormat(
//                "[ASSET XFER UPLOADER]: Received packet {0} for xfer {1} (data length {2})",
//                packetID, xferID, data.Length);

            if (XferID == xferID)
            {
                lock (this)
                {
                    int assetLength = m_asset.Data.Length;
                    int dataLength = data.Length;

                    if (m_asset.Data.Length > 1)
                    {
                        byte[] destinationArray = new byte[assetLength + dataLength];
                        Array.Copy(m_asset.Data, 0, destinationArray, 0, assetLength);
                        Array.Copy(data, 0, destinationArray, assetLength, dataLength);
                        m_asset.Data = destinationArray;
                    }
                    else
                    {
                        if (dataLength > 4)
                        {
                            byte[] buffer2 = new byte[dataLength - 4];
                            Array.Copy(data, 4, buffer2, 0, dataLength - 4);
                            m_asset.Data = buffer2;
                        }
                    }
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
        /// Start asset transfer from the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="assetID"></param>
        /// <param name="transaction"></param>
        /// <param name="type"></param>
        /// <param name="data">
        /// Optional data.  If present then the asset is created immediately with this data
        /// rather than requesting an upload from the client.  The data must be longer than 2 bytes.
        /// </param>
        /// <param name="storeLocal"></param>
        /// <param name="tempFile"></param>
        public void StartUpload(
            IClientAPI remoteClient, UUID assetID, UUID transaction, sbyte type, byte[] data, bool storeLocal,
            bool tempFile)
        {
//            m_log.DebugFormat(
//                "[ASSET XFER UPLOADER]: Initialised xfer from {0}, asset {1}, transaction {2}, type {3}, storeLocal {4}, tempFile {5}, already received data length {6}",
//                remoteClient.Name, assetID, transaction, type, storeLocal, tempFile, data.Length);

            lock (this)
            {
                if (m_uploadState != UploadState.New)
                {
                    m_log.WarnFormat(
                        "[ASSET XFER UPLOADER]: Tried to start upload of asset {0}, transaction {1} for {2} but this is already in state {3}.  Aborting.",
                        assetID, transaction, remoteClient.Name, m_uploadState);

                    return;
                }

                m_uploadState = UploadState.Uploading;
            }

            ourClient = remoteClient;

            m_asset.FullID = assetID;
            m_asset.Type = type;
            m_asset.CreatorID = remoteClient.AgentId.ToString();
            m_asset.Data = data;
            m_asset.Local = storeLocal;
            m_asset.Temporary = tempFile;

//            m_storeLocal = storeLocal;

            if (m_asset.Data.Length > 2)
            {
                SendCompleteMessage();
            }
            else
            {
                RequestStartXfer();
            }
        }

        protected void RequestStartXfer()
        {
            XferID = Util.GetNextXferID();

//            m_log.DebugFormat(
//                "[ASSET XFER UPLOADER]: Requesting Xfer of asset {0}, type {1}, transfer id {2} from {3}",
//                m_asset.FullID, m_asset.Type, XferID, ourClient.Name);

            ourClient.SendXferRequest(XferID, m_asset.Type, m_asset.FullID, 0, new byte[0]);
        }

        protected void SendCompleteMessage()
        {
            // We must lock in order to avoid a race with a separate thread dealing with an inventory item or create
            // message from other client UDP.
            lock (this)
            {
                m_uploadState = UploadState.Complete;

                ourClient.SendAssetUploadCompleteMessage(m_asset.Type, true, m_asset.FullID);

                if (m_createItem)
                {
                    CompleteCreateItem(m_createItemCallback);
                }
                else if (m_updateItem)
                {
                    CompleteItemUpdate(m_updateItemData);
                }
                else if (m_updateTaskItem)
                {
                    CompleteTaskItemUpdate(m_updateTaskItemData);
                }
                else if (m_asset.Local)
                {
                    m_Scene.AssetService.Store(m_asset);
                }
            }

            m_log.DebugFormat(
                "[ASSET XFER UPLOADER]: Uploaded asset {0} for transaction {1}",
                m_asset.FullID, m_transactionID);

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
                UUID folderID, uint callbackID,
                string description, string name, sbyte invType,
                sbyte type, byte wearableType, uint nextOwnerMask)
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

            // We must lock to avoid a race with a separate thread uploading the asset.
            lock (this)
            {
                if (m_uploadState == UploadState.Complete)
                {
                    CompleteCreateItem(callbackID);
                }
                else
                {
                    m_createItem = true; //set flag so the inventory item is created when upload is complete
                    m_createItemCallback = callbackID;
                }
            }
        }

        public void RequestUpdateInventoryItem(IClientAPI remoteClient, InventoryItemBase item)
        {
            // We must lock to avoid a race with a separate thread uploading the asset.
            lock (this)
            {
                m_asset.Name = item.Name;
                m_asset.Description = item.Description;
                m_asset.Type = (sbyte)item.AssetType;

                // remove redundante m_Scene.InventoryService.UpdateItem
                // if uploadState == UploadState.Complete)
//                if (m_asset.FullID != UUID.Zero)
//                {
                    // We must always store the item at this point even if the asset hasn't finished uploading, in order
                    // to avoid a race condition when the appearance module retrieves the item to set the asset id in
                    // the AvatarAppearance structure.
//                    item.AssetID = m_asset.FullID;
//                    m_Scene.InventoryService.UpdateItem(item);
//                }

                if (m_uploadState == UploadState.Complete)
                {
                    CompleteItemUpdate(item);
                }
                else
                {
                    // do it here to avoid the eventual race condition
                    if (m_asset.FullID != UUID.Zero)
                    {
                        // We must always store the item at this point even if the asset hasn't finished uploading, in order
                        // to avoid a race condition when the appearance module retrieves the item to set the asset id in
                        // the AvatarAppearance structure.
                        item.AssetID = m_asset.FullID;
                        m_Scene.InventoryService.UpdateItem(item);
                    }


                    //                    m_log.DebugFormat(
                    //                        "[ASSET XFER UPLOADER]: Holding update inventory item request {0} for {1} pending completion of asset xfer for transaction {2}",
                    //                        item.Name, remoteClient.Name, transactionID);

                    m_updateItem = true;
                    m_updateItemData = item;
                }
            }
        }

        public void RequestUpdateTaskInventoryItem(IClientAPI remoteClient, TaskInventoryItem taskItem)
        {
            // We must lock to avoid a race with a separate thread uploading the asset.
            lock (this)
            {
                m_asset.Name = taskItem.Name;
                m_asset.Description = taskItem.Description;
                m_asset.Type = (sbyte)taskItem.Type;
                taskItem.AssetID = m_asset.FullID;

                if (m_uploadState == UploadState.Complete)
                {
                    CompleteTaskItemUpdate(taskItem);
                }
                else
                {
                    m_updateTaskItem = true;
                    m_updateTaskItemData = taskItem;
                }
            }
        }

        /// <summary>
        /// Store the asset for the given item when it has been uploaded.
        /// </summary>
        /// <param name="item"></param>
        private void CompleteItemUpdate(InventoryItemBase item)
        {
//            m_log.DebugFormat(
//                "[ASSET XFER UPLOADER]: Storing asset {0} for earlier item update for {1} for {2}",
//                m_asset.FullID, item.Name, ourClient.Name);

            ValidateAssets();
            m_Scene.AssetService.Store(m_asset);
            if (m_asset.FullID != UUID.Zero)
            {
                item.AssetID = m_asset.FullID;
                m_Scene.InventoryService.UpdateItem(item);
            }

            ourClient.SendInventoryItemCreateUpdate(item, m_transactionID, 0);

            m_transactions.RemoveXferUploader(m_transactionID);

            m_Scene.EventManager.TriggerOnNewInventoryItemUploadComplete(ourClient.AgentId, (AssetType)type, m_asset.FullID, m_asset.Name, 0);
        }

        /// <summary>
        /// Store the asset for the given task item when it has been uploaded.
        /// </summary>
        /// <param name="taskItem"></param>
        private void CompleteTaskItemUpdate(TaskInventoryItem taskItem)
        {
//            m_log.DebugFormat(
//                "[ASSET XFER UPLOADER]: Storing asset {0} for earlier task item update for {1} for {2}",
//                m_asset.FullID, taskItem.Name, ourClient.Name);

            ValidateAssets();
            m_Scene.AssetService.Store(m_asset);

            m_transactions.RemoveXferUploader(m_transactionID);
        }

        private void CompleteCreateItem(uint callbackID)
        {
            ValidateAssets();
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
            item.BasePermissions = (uint)(PermissionMask.All | PermissionMask.Export);
            item.CurrentPermissions = item.BasePermissions;
            item.GroupPermissions=0;
            item.EveryOnePermissions=0;
            item.NextPermissions = nextPerm;
            item.Flags = (uint) wearableType;
            item.CreationDate = Util.UnixTimeSinceEpoch();

            m_log.DebugFormat("[XFER]: Created item {0} with asset {1}",
                    item.ID, item.AssetID);

            if (m_Scene.AddInventoryItem(item))
                ourClient.SendInventoryItemCreateUpdate(item, m_transactionID, callbackID);
            else
                ourClient.SendAlertMessage("Unable to create inventory item");

            m_transactions.RemoveXferUploader(m_transactionID);
        }


        private void ValidateAssets()
        {
            if (m_asset.Type == (sbyte)CustomAssetType.AnimationSet)
            {
                AnimationSet animSet = new AnimationSet(m_asset.Data);

                bool allOk = animSet.Validate(x => {
                    int perms = m_Scene.InventoryService.GetAssetPermissions(ourClient.AgentId, x);
                    int required = (int)(PermissionMask.Transfer | PermissionMask.Copy);
                    if ((perms & required) != required)
                        return false;
                    return true;
                    });

                if (!allOk)
                    m_asset.Data = animSet.ToBytes();
            }

            if (m_asset.Type == (sbyte)AssetType.Clothing ||
                m_asset.Type == (sbyte)AssetType.Bodypart)
            {
                string content = System.Text.Encoding.ASCII.GetString(m_asset.Data);
                string[] lines = content.Split(new char[] {'\n'});

                List<string> validated = new List<string>();

                Dictionary<int, UUID> allowed = ExtractTexturesFromOldData();

                int textures = 0;

                foreach (string line in lines)
                {
                    try
                    {
                        if (line.StartsWith("textures "))
                        {
                            textures = Convert.ToInt32(line.Substring(9));
                            validated.Add(line);
                        }
                        else if (textures > 0)
                        {
                            string[] parts = line.Split(new char[] {' '});

                            UUID tx = new UUID(parts[1]);
                            int id = Convert.ToInt32(parts[0]);

                            if (defaultIDs.Contains(tx) || tx == UUID.Zero ||
                                (allowed.ContainsKey(id) && allowed[id] == tx))
                            {
                                validated.Add(parts[0] + " " + tx.ToString());
                            }
                            else
                            {
                                int perms = m_Scene.InventoryService.GetAssetPermissions(ourClient.AgentId, tx);
                                int full = (int)(PermissionMask.Modify | PermissionMask.Transfer | PermissionMask.Copy);

                                if ((perms & full) != full)
                                {
                                    m_log.ErrorFormat("[ASSET UPLOADER]: REJECTED update with texture {0} from {1} because they do not own the texture", tx, ourClient.AgentId);
                                    validated.Add(parts[0] + " " + UUID.Zero.ToString());
                                }
                                else
                                {
                                    validated.Add(line);
                                }
                            }
                            textures--;
                        }
                        else
                        {
                            validated.Add(line);
                        }
                    }
                    catch
                    {
                        // If it's malformed, skip it
                    }
                }

                string final = String.Join("\n", validated.ToArray());

                m_asset.Data = System.Text.Encoding.ASCII.GetBytes(final);
            }
        }

        /// <summary>
        /// Get the asset data uploaded in this transfer.
        /// </summary>
        /// <returns>null if the asset has not finished uploading</returns>
        public AssetBase GetAssetData()
        {
            if (m_uploadState == UploadState.Complete)
            {
                ValidateAssets();
                return m_asset;
            }

            return null;
        }

        public void SetOldData(byte[] d)
        {
            m_oldData = d;
        }

        private Dictionary<int,UUID> ExtractTexturesFromOldData()
        {
            Dictionary<int,UUID> result = new Dictionary<int,UUID>();
            if (m_oldData == null)
                return result;

            string content = System.Text.Encoding.ASCII.GetString(m_oldData);
            string[] lines = content.Split(new char[] {'\n'});

            int textures = 0;

            foreach (string line in lines)
            {
                try
                {
                    if (line.StartsWith("textures "))
                    {
                        textures = Convert.ToInt32(line.Substring(9));
                    }
                    else if (textures > 0)
                    {
                        string[] parts = line.Split(new char[] {' '});

                        UUID tx = new UUID(parts[1]);
                        int id = Convert.ToInt32(parts[0]);
                        result[id] = tx;
                        textures--;
                    }
                }
                catch
                {
                    // If it's malformed, skip it
                }
            }

            return result;
        }
    }
}
