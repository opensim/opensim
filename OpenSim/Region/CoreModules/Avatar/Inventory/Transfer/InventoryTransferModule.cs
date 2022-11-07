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

        private Scene FindClientAndScene(UUID agentId, out ScenePresence presence)
        {
            presence = null;
            lock (m_Scenelist)
            {
                foreach (Scene scene in m_Scenelist)
                {
                    presence = scene.GetScenePresence(agentId);
                    if (presence != null && !presence.IsDeleted)
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

            Scene scene = client.Scene as Scene;
            if (scene == null) // Something seriously wrong here.
                return;

            UUID agentID = client.AgentId;

            switch((InstantMessageDialog)im.dialog)
            {
                case InstantMessageDialog.InventoryOffered:
                {
                    if (im.binaryBucket.Length < 17) // Invalid
                        return;

                    UUID recipientID = new UUID(im.toAgentID);
                    ScenePresence recipientAgent = scene.GetScenePresence(recipientID);
                    UUID copyID;

                    // First byte is the asset type
                    AssetType assetType = (AssetType)im.binaryBucket[0];
                    if(assetType == AssetType.LinkFolder || assetType == AssetType.Link)
                    {
                        client.SendAgentAlertMessage("Can't give a link. Nothing given.", false);
                        return;
                    }

                    if (assetType == AssetType.Folder)
                    {
                        UUID folderID = new UUID(im.binaryBucket, 1);
                        if (folderID.IsZero())
                        {
                            client.SendAgentAlertMessage("Can't find folder to give. Nothing given.", false);
                            return;
                        }

                        Dictionary<UUID,AssetType> ids = new Dictionary<UUID, AssetType>();
                        ids[folderID] = assetType;

                        if (im.binaryBucket.Length >= 34 && im.binaryBucket.Length % 17 == 0)
                        {
                            byte[] iddata = im.binaryBucket;
                            for (int i = 17; i <= im.binaryBucket.Length - 17; i += 17)
                                ids[new UUID(iddata, i + 1)] = (AssetType)im.binaryBucket[i];
                        }

                        m_log.DebugFormat("[INVENTORY TRANSFER]: offering folder {0} to agent {1}'s inventory",
                            folderID, recipientID);

                        InventoryFolderBase folderCopy = scene.GiveInventoryFolder(client, recipientID, agentID, folderID, UUID.Zero, ids);

                        if (folderCopy == null)
                        {
                            client.SendAgentAlertMessage("Can't find folder to give. Nothing given.", false);
                            return;
                        }

                        copyID = folderCopy.ID;
                        im.imSessionID = copyID.Guid;

                        if (recipientAgent != null)
                        {
                            recipientAgent.ControllingClient.SendBulkUpdateInventory(folderCopy);
                        }
                    }
                    else
                    {
                        UUID itemID = new UUID(im.binaryBucket, 1);
                        if (itemID.IsZero())
                        {
                            client.SendAgentAlertMessage("Can't find item to give. Nothing given.", false);
                            return;
                        }

                        m_log.DebugFormat("[INVENTORY TRANSFER]: (giving) Inserting item {0} "+
                                "into agent {1}'s inventory",
                                itemID, recipientID);

                        string message;
                        InventoryItemBase itemCopy = scene.GiveInventoryItem(recipientID, agentID, itemID, out message);

                        if (itemCopy == null)
                        {
                            client.SendAgentAlertMessage(message, false);
                            return;
                        }

                        copyID = itemCopy.ID;

                        if (recipientAgent != null)
                            recipientAgent.ControllingClient.SendBulkUpdateInventory(itemCopy);

                        im.imSessionID = copyID.Guid;
                    }

                    // Send the IM to the recipient. The item is already
                    // in their inventory, so it will not be lost if
                    // they are offline.

                    im.binaryBucket = new byte[17];
                    im.binaryBucket[0] = (byte)assetType;
                    copyID.ToBytes(im.binaryBucket, 1);

                    if (recipientAgent != null)
                    {
                        im.offline = 0;
                        recipientAgent.ControllingClient.SendInstantMessage(im);
                        return;
                    }
                    else
                    {
                        im.offline = 0;
                        if (m_TransferModule != null)
                        {
                            m_TransferModule.SendInstantMessage(im, delegate(bool success)
                            {
                                if (!success)
                                    client.SendAlertMessage("User not online. Inventory has been saved");
                            });
                        }
                    }
                    break;
                }
                case InstantMessageDialog.InventoryAccepted:
                {
                    UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip
                    if(inventoryID.IsZero())
                        return;

                    IInventoryService invService = scene.InventoryService;

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
                    break;
                }
                case InstantMessageDialog.TaskInventoryAccepted:
                {
                    if (im.binaryBucket == null || im.binaryBucket.Length < 16)
                        return;

                    UUID destinationFolderID = new UUID(im.binaryBucket, 0);
                    if(destinationFolderID.IsZero()) // uuid-zero is a valid folder ID(?) keeping old code assuming not
                        return;

                    IInventoryService invService = scene.InventoryService;
                    InventoryFolderBase destinationFolder = null;
                    destinationFolder = invService.GetFolder(agentID, destinationFolderID);

                    if(destinationFolder == null)
                        return; // no where to put it

                    UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip
                    if(inventoryID.IsZero())
                        return;

                    InventoryItemBase item = invService.GetItem(agentID, inventoryID);
                    InventoryFolderBase folder = null;
                    //UUID? previousParentFolderID = null;

                    if (item != null) // It's an item
                    {
                        if(item.Folder != destinationFolderID)
                        {
                            //previousParentFolderID = item.Folder;
                            item.Folder = destinationFolderID;
                            invService.MoveItems(item.Owner, new List<InventoryItemBase>() { item });
                            client.SendInventoryItemCreateUpdate(item, 0);
                        }
                    }
                    else
                    {
                        folder = invService.GetFolder(agentID, inventoryID);

                        if (folder != null) // It's a folder
                        {
                            if(folder.ParentID != destinationFolderID)
                            {
                                folder.ParentID = destinationFolderID;
                                invService.MoveFolder(folder);
                            }
                            client.SendBulkUpdateInventory(folder, folder.ID);
                        }
                    }
                    break;
                }
                case InstantMessageDialog.InventoryDeclined:
                case InstantMessageDialog.TaskInventoryDeclined:
                {
                    IInventoryService invService = scene.InventoryService;
                    InventoryFolderBase trashFolder = invService.GetFolderForType(agentID, FolderType.Trash);
                    if(trashFolder == null) //??
                    {
                        client.SendAgentAlertMessage("Trash folder not found", false);
                        return;
                    }

                    UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip
                    if(inventoryID.IsZero())
                    {
                        client.SendAgentAlertMessage("Item or folder not found", false);
                        return;
                    }

                    InventoryItemBase item = invService.GetItem(agentID, inventoryID);
                    InventoryFolderBase folder = null;

                    if (item != null)
                    {
                        if (trashFolder.ID != item.Folder)
                        {
                            item.Folder = trashFolder.ID;
                            invService.MoveItems(item.Owner, new List<InventoryItemBase>() { item });
                            client.SendInventoryItemCreateUpdate(item, 0);
                        }
                    }
                    else
                    {
                        folder = invService.GetFolder(agentID, inventoryID);
                        if (folder != null)
                        {
                            if (trashFolder.ID != folder.ParentID)
                            {
                                folder.ParentID = trashFolder.ID;
                                invService.MoveFolder(folder);
                            }
                            client.SendBulkUpdateInventory(folder, folder.ID);
                        }
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
                    break;
                }
                default:
                    break;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="im"></param>
        private void OnGridInstantMessage(GridInstantMessage im)
        {
            // Check if this is ours to handle
            UUID recipientID = new UUID(im.toAgentID);
            Scene scene = FindClientAndScene(recipientID, out ScenePresence user);

            if (scene == null)
                return;
            if (user == null)
                return;

            switch((InstantMessageDialog)im.dialog)
            {
            // This requires a little bit of processing because we have to make the
            // new item visible in the recipient's inventory here
            //
                case InstantMessageDialog.InventoryOffered:
                {
                    if (im.binaryBucket.Length < 17) // Invalid
                        return;

                    // First byte is the asset type
                    AssetType assetType = (AssetType)im.binaryBucket[0];
                    if (assetType == AssetType.LinkFolder || assetType == AssetType.Link)
                        return;

                    UUID copyID;

                    if (AssetType.Folder == assetType)
                    {
                        UUID folderID = new UUID(im.binaryBucket, 1);

                        InventoryFolderBase folder = scene.InventoryService.GetFolder(recipientID, folderID);

                        if (folder == null)
                            return;

                        copyID = folder.ID;
                        user.ControllingClient.SendBulkUpdateInventory(folder);
                    }
                    else
                    {
                        UUID itemID = new UUID(im.binaryBucket, 1);

                        InventoryItemBase item = scene.InventoryService.GetItem(recipientID, itemID);

                        if (item == null)
                            return;
                        user.ControllingClient.SendBulkUpdateInventory(item);
                        copyID = item.ID;
                    }

                    im.binaryBucket = new byte[17];
                    im.binaryBucket[0] = (byte)assetType;
                    copyID.ToBytes(im.binaryBucket, 1);

                    user.ControllingClient.SendInstantMessage(im);
                    break;
                }
                case InstantMessageDialog.TaskInventoryOffered:
                {
                    if (im.binaryBucket.Length < 1) // Invalid
                        return;

                    // Bucket is the asset type
                    AssetType assetType = (AssetType)im.binaryBucket[0];

                    if (AssetType.Folder == assetType)
                    {
                        UUID folderID = new UUID(im.imSessionID);
                        InventoryFolderBase folder = scene.InventoryService.GetFolder(recipientID, folderID);

                        if (folder != null)
                            user.ControllingClient.SendBulkUpdateInventory(folder);
                    }
                    else
                    {
                        UUID itemID = new UUID(im.imSessionID);
                        InventoryItemBase item = scene.InventoryService.GetItem(recipientID, itemID);

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
                    break;
                }
                case InstantMessageDialog.InventoryAccepted:
                case InstantMessageDialog.InventoryDeclined:
                case InstantMessageDialog.TaskInventoryDeclined:
                case InstantMessageDialog.TaskInventoryAccepted:
                {
                    user.ControllingClient.SendInstantMessage(im);
                    break;
                }
                default:
                    break;
            }
        }
    }
}
