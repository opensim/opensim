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
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Transfer
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "InventoryTransferModule")]
    public class InventoryTransferModule : ISharedRegionModule
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        private List<Scene> m_Scenelist = new List<Scene>();

        private IMessageTransferModule m_TransferModule;
        private bool m_Enabled = true;

        #region Region Module interface

        public void Initialise(IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                // Allow disabling this module in config
                //
                if (config.Configs["Messaging"].GetString(
                        "InventoryTransferModule", "InventoryTransferModule") !=
                        "InventoryTransferModule")
                {
                    m_Enabled = false;
                    return;
                }
            }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scenelist.Add(scene);

//            scene.RegisterModuleInterface<IInventoryTransferModule>(this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_TransferModule == null)
            {
                m_TransferModule = m_Scenelist[0].RequestModuleInterface<IMessageTransferModule>();
                if (m_TransferModule == null)
                {
                    m_log.Error("[INVENTORY TRANSFER]: No Message transfer module found, transfers will be local only");
                    m_Enabled = false;

//                    m_Scenelist.Clear();
//                    scene.EventManager.OnNewClient -= OnNewClient;
                    scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            m_Scenelist.Remove(scene);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InventoryModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            // Inventory giving is conducted via instant message
            client.OnInstantMessage += OnInstantMessage;
        }

        private Scene FindClientScene(UUID agentId)
        {
            lock (m_Scenelist)
            {
                foreach (Scene scene in m_Scenelist)
                {
                    ScenePresence presence = scene.GetScenePresence(agentId);
                    if (presence != null)
                        return scene;
                }
            }
            return null;
        }

        private void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
//            m_log.DebugFormat(
//                "[INVENTORY TRANSFER]: {0} IM type received from client {1}. From={2} ({3}), To={4}",
//                (InstantMessageDialog)im.dialog, client.Name,
//                im.fromAgentID, im.fromAgentName, im.toAgentID);

            Scene scene = FindClientScene(client.AgentId);

            if (scene == null) // Something seriously wrong here.
                return;

            if (im.dialog == (byte) InstantMessageDialog.InventoryOffered)
            {
                //m_log.DebugFormat("Asset type {0}", ((AssetType)im.binaryBucket[0]));

                if (im.binaryBucket.Length < 17) // Invalid
                    return;

                UUID recipientID = new UUID(im.toAgentID);
                ScenePresence user = scene.GetScenePresence(recipientID);
                UUID copyID;

                // First byte is the asset type
                AssetType assetType = (AssetType)im.binaryBucket[0];

                if (AssetType.Folder == assetType)
                {
                    UUID folderID = new UUID(im.binaryBucket, 1);

                    m_log.DebugFormat(
                        "[INVENTORY TRANSFER]: Inserting original folder {0} into agent {1}'s inventory",
                        folderID, new UUID(im.toAgentID));

                    InventoryFolderBase folderCopy
                        = scene.GiveInventoryFolder(client, recipientID, client.AgentId, folderID, UUID.Zero);

                    if (folderCopy == null)
                    {
                        client.SendAgentAlertMessage("Can't find folder to give. Nothing given.", false);
                        return;
                    }

                    // The outgoing binary bucket should contain only the byte which signals an asset folder is
                    // being copied and the following bytes for the copied folder's UUID
                    copyID = folderCopy.ID;
                    byte[] copyIDBytes = copyID.GetBytes();
                    im.binaryBucket = new byte[1 + copyIDBytes.Length];
                    im.binaryBucket[0] = (byte)AssetType.Folder;
                    Array.Copy(copyIDBytes, 0, im.binaryBucket, 1, copyIDBytes.Length);

                    if (user != null)
                        user.ControllingClient.SendBulkUpdateInventory(folderCopy);

                    // HACK!!
                    // Insert the ID of the copied folder into the IM so that we know which item to move to trash if it
                    // is rejected.
                    // XXX: This is probably a misuse of the session ID slot.
                    im.imSessionID = copyID.Guid;
                }
                else
                {
                    // First byte of the array is probably the item type
                    // Next 16 bytes are the UUID

                    UUID itemID = new UUID(im.binaryBucket, 1);

                    m_log.DebugFormat("[INVENTORY TRANSFER]: (giving) Inserting item {0} "+
                            "into agent {1}'s inventory",
                            itemID, new UUID(im.toAgentID));

                    string message;
                    InventoryItemBase itemCopy = scene.GiveInventoryItem(new UUID(im.toAgentID), client.AgentId, itemID, out message);

                    if (itemCopy == null)
                    {
                        client.SendAgentAlertMessage(message, false);
                        return;
                    }

                    copyID = itemCopy.ID;
                    Array.Copy(copyID.GetBytes(), 0, im.binaryBucket, 1, 16);

                    if (user != null)
                        user.ControllingClient.SendBulkUpdateInventory(itemCopy);

                    // HACK!!
                    // Insert the ID of the copied item into the IM so that we know which item to move to trash if it
                    // is rejected.
                    // XXX: This is probably a misuse of the session ID slot.
                    im.imSessionID = copyID.Guid;
                }

                im.offline = 0;

                // Send the IM to the recipient. The item is already
                // in their inventory, so it will not be lost if
                // they are offline.
                //
                if (user != null)
                {
                    user.ControllingClient.SendInstantMessage(im);
                    return;
                }
                else
                {
                    if (m_TransferModule != null)
                        m_TransferModule.SendInstantMessage(im, delegate(bool success)
                        {
                            if (!success)
                                client.SendAlertMessage("User not online. Inventory has been saved");
                        });
                }
            }
            else if (im.dialog == (byte) InstantMessageDialog.InventoryAccepted)
            {
                UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip
                IInventoryService invService = scene.InventoryService;

                // Special case: folder redirect.
                // RLV uses this
                if (im.dialog == (byte) InstantMessageDialog.TaskInventoryAccepted)
                {
                    InventoryFolderBase folder = invService.GetFolder(client.AgentId, inventoryID);

                    if (folder != null)
                    {
                        if (im.binaryBucket.Length >= 16)
                        {
                            UUID destFolderID = new UUID(im.binaryBucket, 0);
                            if (destFolderID != UUID.Zero)
                            {
                                InventoryFolderBase destFolder = invService.GetFolder(client.AgentId, destFolderID);
                                if (destFolder != null)
                                {
                                    if (folder.ParentID != destFolder.ID)
                                    {
                                        folder.ParentID = destFolder.ID;
                                        invService.MoveFolder(folder);
                                        client.SendBulkUpdateInventory(folder);
                                    }
                                }
                            }
                        }
                    }
                }

                ScenePresence user = scene.GetScenePresence(new UUID(im.toAgentID));

                if (user != null) // Local
                {
                    user.ControllingClient.SendInstantMessage(im);
                }
                else
                {
                    if (m_TransferModule != null)
                        m_TransferModule.SendInstantMessage(im, delegate(bool success) {});
                }
            }
            else if (im.dialog == (byte) InstantMessageDialog.TaskInventoryAccepted)
            {
                UUID destinationFolderID = UUID.Zero;

                if (im.binaryBucket != null && im.binaryBucket.Length >= 16)
                    destinationFolderID = new UUID(im.binaryBucket, 0);

                IInventoryService invService = scene.InventoryService;
                InventoryFolderBase destinationFolder = null;
                if (destinationFolderID != UUID.Zero)
                    destinationFolder = invService.GetFolder(client.AgentId, destinationFolderID);

                if(destinationFolder != null)
                {
                    UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip

                    InventoryItemBase item = invService.GetItem(client.AgentId, inventoryID);
                    InventoryFolderBase folder = null;
                    UUID? previousParentFolderID = null;

                    if (item != null) // It's an item
                    {
                        if(item.Folder != destinationFolderID)
                        {
                            previousParentFolderID = item.Folder;
                            item.Folder = destinationFolderID;
                            invService.MoveItems(item.Owner, new List<InventoryItemBase>() { item });
                            client.SendInventoryItemCreateUpdate(item, 0);
                        }
                    }
                    else
                    {
                        folder = invService.GetFolder(client.AgentId, inventoryID);

                        if (folder != null) // It's a folder
                        {
                            if(folder.ParentID != destinationFolderID)
                            {
                                previousParentFolderID = folder.ParentID;
                                folder.ParentID = destinationFolderID;
                                invService.MoveFolder(folder);
                            }
                        }
                    }

                    // Tell client about updates to original parent and new parent (this should probably be factored with existing move item/folder code).
                    if (previousParentFolderID != null)
                    {
                        InventoryFolderBase previousParentFolder = invService.GetFolder(client.AgentId, (UUID)previousParentFolderID);
                        scene.SendInventoryUpdate(client, previousParentFolder, true, true);

                        scene.SendInventoryUpdate(client, destinationFolder, true, true);
                    }
                }
            }
            else if (
                im.dialog == (byte)InstantMessageDialog.InventoryDeclined
                || im.dialog == (byte)InstantMessageDialog.TaskInventoryDeclined)
            {
                // Here, the recipient is local and we can assume that the
                // inventory is loaded. Courtesy of the above bulk update,
                // It will have been pushed to the client, too
                //
                IInventoryService invService = scene.InventoryService;

                InventoryFolderBase trashFolder =
                    invService.GetFolderForType(client.AgentId, FolderType.Trash);

                UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip

                InventoryItemBase item = invService.GetItem(client.AgentId, inventoryID);
                InventoryFolderBase folder = null;
                UUID? previousParentFolderID = null;

                if (item != null && trashFolder != null)
                {
                    previousParentFolderID = item.Folder;
                    item.Folder = trashFolder.ID;

                    // Diva comment: can't we just update this item???
                    List<UUID> uuids = new List<UUID>();
                    uuids.Add(item.ID);
                    invService.DeleteItems(item.Owner, uuids);
                    scene.AddInventoryItem(client, item);
                }
                else
                {
                    folder = invService.GetFolder(client.AgentId, inventoryID);

                    if (folder != null && trashFolder != null)
                    {
                        previousParentFolderID = folder.ParentID;
                        folder.ParentID = trashFolder.ID;
                        invService.MoveFolder(folder);
                        client.SendBulkUpdateInventory(folder);
                    }
                }

                if ((null == item && null == folder) || null == trashFolder)
                {
                    string reason = String.Empty;

                    if (trashFolder == null)
                        reason += " Trash folder not found.";
                    if (item == null)
                        reason += " Item not found.";
                    if (folder == null)
                        reason += " Folder not found.";

                    client.SendAgentAlertMessage("Unable to delete "+
                            "received inventory" + reason, false);
                }
                // Tell client about updates to original parent and new parent (this should probably be factored with existing move item/folder code).
                else if (previousParentFolderID != null)
                {
                    InventoryFolderBase previousParentFolder = invService.GetFolder(client.AgentId, (UUID)previousParentFolderID);
                    scene.SendInventoryUpdate(client, previousParentFolder, true, true);

                    scene.SendInventoryUpdate(client, trashFolder, true, true);
                }

                if (im.dialog == (byte)InstantMessageDialog.InventoryDeclined)
                {
                    ScenePresence user = scene.GetScenePresence(new UUID(im.toAgentID));

                    if (user != null) // Local
                    {
                        user.ControllingClient.SendInstantMessage(im);
                    }
                    else
                    {
                        if (m_TransferModule != null)
                            m_TransferModule.SendInstantMessage(im, delegate(bool success) { });
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="im"></param>
        private void OnGridInstantMessage(GridInstantMessage im)
        {
            // Check if this is ours to handle
            //
            Scene scene = FindClientScene(new UUID(im.toAgentID));

            if (scene == null)
                return;

            // Find agent to deliver to
            //
            ScenePresence user = scene.GetScenePresence(new UUID(im.toAgentID));
            if (user == null)
                return;

            // This requires a little bit of processing because we have to make the
            // new item visible in the recipient's inventory here
            //
            if (im.dialog == (byte) InstantMessageDialog.InventoryOffered)
            {
                if (im.binaryBucket.Length < 17) // Invalid
                    return;

                UUID recipientID = new UUID(im.toAgentID);

                // First byte is the asset type
                AssetType assetType = (AssetType)im.binaryBucket[0];

                if (AssetType.Folder == assetType)
                {
                    UUID folderID = new UUID(im.binaryBucket, 1);

                    InventoryFolderBase folder =
                            scene.InventoryService.GetFolder(recipientID, folderID);

                    if (folder != null)
                        user.ControllingClient.SendBulkUpdateInventory(folder);
                }
                else
                {
                    UUID itemID = new UUID(im.binaryBucket, 1);

                    InventoryItemBase item =
                            scene.InventoryService.GetItem(recipientID, itemID);

                    if (item != null)
                    {
                        user.ControllingClient.SendBulkUpdateInventory(item);
                    }
                }
                user.ControllingClient.SendInstantMessage(im);
            }
            if (im.dialog == (byte) InstantMessageDialog.TaskInventoryOffered)
            {
                if (im.binaryBucket.Length < 1) // Invalid
                    return;

                UUID recipientID = new UUID(im.toAgentID);

                // Bucket is the asset type
                AssetType assetType = (AssetType)im.binaryBucket[0];

                if (AssetType.Folder == assetType)
                {
                    UUID folderID = new UUID(im.imSessionID);

                    InventoryFolderBase folder =
                            scene.InventoryService.GetFolder(recipientID, folderID);

                    if (folder != null)
                        user.ControllingClient.SendBulkUpdateInventory(folder);
                }
                else
                {
                    UUID itemID = new UUID(im.imSessionID);

                    InventoryItemBase item =
                            scene.InventoryService.GetItem(recipientID, itemID);

                    if (item != null)
                    {
                        user.ControllingClient.SendBulkUpdateInventory(item);
                    }
                }

                // Fix up binary bucket since this may be 17 chars long here
                Byte[] bucket = new Byte[1];
                bucket[0] = im.binaryBucket[0];
                im.binaryBucket = bucket;

                user.ControllingClient.SendInstantMessage(im);
            }
            else if (im.dialog == (byte) InstantMessageDialog.InventoryAccepted ||
                     im.dialog == (byte) InstantMessageDialog.InventoryDeclined ||
                     im.dialog == (byte) InstantMessageDialog.TaskInventoryDeclined ||
                     im.dialog == (byte) InstantMessageDialog.TaskInventoryAccepted)
            {
                user.ControllingClient.SendInstantMessage(im);
            }
        }
    }
}
