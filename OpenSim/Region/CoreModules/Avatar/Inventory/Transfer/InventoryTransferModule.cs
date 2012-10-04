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
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Transfer
{
    public class InventoryTransferModule : IInventoryTransferModule, ISharedRegionModule
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        private List<Scene> m_Scenelist = new List<Scene>();
        private Dictionary<UUID, Scene> m_AgentRegions =
                new Dictionary<UUID, Scene>();

        private IMessageTransferModule m_TransferModule = null;
        private bool m_Enabled = true;

        #region IRegionModule Members

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

            scene.RegisterModuleInterface<IInventoryTransferModule>(this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += ClientLoggedOut;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnSetRootAgentScene += OnSetRootAgentScene;
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

                    m_Scenelist.Clear();
                    scene.EventManager.OnNewClient -= OnNewClient;
                    scene.EventManager.OnClientClosed -= ClientLoggedOut;
                    scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
                    scene.EventManager.OnSetRootAgentScene -= OnSetRootAgentScene;
                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnClientClosed -= ClientLoggedOut;
            scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            scene.EventManager.OnSetRootAgentScene -= OnSetRootAgentScene;
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
        
        protected void OnSetRootAgentScene(UUID id, Scene scene)
        {
            m_AgentRegions[id] = scene;
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
//                "[INVENTORY TRANSFER]: {0} IM type received from {1}", 
//                (InstantMessageDialog)im.dialog, client.Name);
          
            Scene scene = FindClientScene(client.AgentId);

            if (scene == null) // Something seriously wrong here.
                return;

            if (im.dialog == (byte) InstantMessageDialog.InventoryOffered)
            {
                //m_log.DebugFormat("Asset type {0}", ((AssetType)im.binaryBucket[0]));

                if (im.binaryBucket.Length < 17) // Invalid
                    return;
            
                UUID receipientID = new UUID(im.toAgentID);
                ScenePresence user = scene.GetScenePresence(receipientID);
                UUID copyID;

                // First byte is the asset type
                AssetType assetType = (AssetType)im.binaryBucket[0];
                
                if (AssetType.Folder == assetType)
                {
                    UUID folderID = new UUID(im.binaryBucket, 1);
                    
                    m_log.DebugFormat("[INVENTORY TRANSFER]: Inserting original folder {0} "+
                            "into agent {1}'s inventory",
                            folderID, new UUID(im.toAgentID));
                    
                    InventoryFolderBase folderCopy 
                        = scene.GiveInventoryFolder(receipientID, client.AgentId, folderID, UUID.Zero);
                    
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
                    im.imSessionID = folderID.Guid;
                }
                else
                {
                    // First byte of the array is probably the item type
                    // Next 16 bytes are the UUID

                    UUID itemID = new UUID(im.binaryBucket, 1);

                    m_log.DebugFormat("[INVENTORY TRANSFER]: (giving) Inserting item {0} "+
                            "into agent {1}'s inventory",
                            itemID, new UUID(im.toAgentID));

                    InventoryItemBase itemCopy = scene.GiveInventoryItem(
                            new UUID(im.toAgentID),
                            client.AgentId, itemID);

                    if (itemCopy == null)
                    {
                        client.SendAgentAlertMessage("Can't find item to give. Nothing given.", false);
                        return;
                    }
                    
                    copyID = itemCopy.ID;
                    Array.Copy(copyID.GetBytes(), 0, im.binaryBucket, 1, 16);
                    
                    if (user != null)
                        user.ControllingClient.SendBulkUpdateInventory(itemCopy);

                    // HACK!!
                    im.imSessionID = itemID.Guid;
                }

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
                ScenePresence user = scene.GetScenePresence(new UUID(im.toAgentID));

                if (user != null) // Local
                {
                    user.ControllingClient.SendInstantMessage(im);
                }
                else
                {
                    if (m_TransferModule != null)
                        m_TransferModule.SendInstantMessage(im, delegate(bool success) {

                            // justincc - FIXME: Comment out for now.  This code was added in commit db91044 Mon Aug 22 2011
                            // and is apparently supposed to fix bulk inventory updates after accepting items.  But
                            // instead it appears to cause two copies of an accepted folder for the receiving user in
                            // at least some cases.  Folder/item update is already done when the offer is made (see code above)

//                            // Send BulkUpdateInventory
//                            IInventoryService invService = scene.InventoryService;
//                            UUID inventoryEntityID = new UUID(im.imSessionID); // The inventory item /folder, back from it's trip
//
//                            InventoryFolderBase folder = new InventoryFolderBase(inventoryEntityID, client.AgentId);
//                            folder = invService.GetFolder(folder);
//
//                            ScenePresence fromUser = scene.GetScenePresence(new UUID(im.fromAgentID));
//
//                            // If the user has left the scene by the time the message comes back then we can't send
//                            // them the update.
//                            if (fromUser != null)
//                                fromUser.ControllingClient.SendBulkUpdateInventory(folder);
                        });
                }
            }
            else if (im.dialog == (byte) InstantMessageDialog.TaskInventoryAccepted)
            {
                UUID destinationFolderID = UUID.Zero;

                if (im.binaryBucket != null && im.binaryBucket.Length >= 16)
                {
                    destinationFolderID = new UUID(im.binaryBucket, 0);
                }

                if (destinationFolderID != UUID.Zero)
                {
                    IInventoryService invService = scene.InventoryService;

                    UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip

                    InventoryItemBase item = new InventoryItemBase(inventoryID, client.AgentId);
                    item = invService.GetItem(item);
                    InventoryFolderBase folder = null;

                    if (item != null) // It's an item
                    {
                        item.Folder = destinationFolderID;

                        invService.DeleteItems(item.Owner, new List<UUID>() { item.ID });
                        scene.AddInventoryItem(client, item);
                    }
                    else
                    {
                        folder = new InventoryFolderBase(inventoryID, client.AgentId);
                        folder = invService.GetFolder(folder);

                        if (folder != null) // It's a folder
                        {
                            folder.ParentID = destinationFolderID;
                            invService.MoveFolder(folder);
                        }
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
                    invService.GetFolderForType(client.AgentId, AssetType.TrashFolder);

                UUID inventoryID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip

                InventoryItemBase item = new InventoryItemBase(inventoryID, client.AgentId);
                item = invService.GetItem(item);
                InventoryFolderBase folder = null;
                
                if (item != null && trashFolder != null)
                {
                    item.Folder = trashFolder.ID;

                    // Diva comment: can't we just update this item???
                    List<UUID> uuids = new List<UUID>();
                    uuids.Add(item.ID);
                    invService.DeleteItems(item.Owner, uuids);
                    scene.AddInventoryItem(client, item);
                }
                else
                {
                    folder = new InventoryFolderBase(inventoryID, client.AgentId);
                    folder = invService.GetFolder(folder);
                    
                    if (folder != null & trashFolder != null)
                    {
                        folder.ParentID = trashFolder.ID;
                        invService.MoveFolder(folder);
                    }
                }
                
                if ((null == item && null == folder) | null == trashFolder)
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
        }

        public bool NeedSceneCacheClear(UUID agentID, Scene scene)
        {
            if (!m_AgentRegions.ContainsKey(agentID))
            {
                // Since we can get here two ways, we need to scan
                // the scenes here. This is somewhat more expensive
                // but helps avoid a nasty bug
                //

                foreach (Scene s in m_Scenelist)
                {
                    ScenePresence presence;

                    if (s.TryGetScenePresence(agentID, out presence))
                    {
                        // If the agent is in this scene, then we
                        // are being called twice in a single
                        // teleport. This is wasteful of cycles
                        // but harmless due to this 2nd level check
                        //
                        // If the agent is found in another scene
                        // then the list wasn't current
                        //
                        // If the agent is totally unknown, then what
                        // are we even doing here??
                        //
                        if (s == scene)
                        {
                            //m_log.Debug("[INVTRANSFERMOD]: s == scene. Returning true in " + scene.RegionInfo.RegionName);
                            return true;
                        }
                        else
                        {
                            //m_log.Debug("[INVTRANSFERMOD]: s != scene. Returning false in " + scene.RegionInfo.RegionName);
                            return false;
                        }
                    }
                }
                //m_log.Debug("[INVTRANSFERMOD]: agent not in scene. Returning true in " + scene.RegionInfo.RegionName);
                return true;
            }

            // The agent is left in current Scene, so we must be
            // going to another instance
            //
            if (m_AgentRegions[agentID] == scene)
            {
                //m_log.Debug("[INVTRANSFERMOD]: m_AgentRegions[agentID] == scene. Returning true in " + scene.RegionInfo.RegionName);
                m_AgentRegions.Remove(agentID);
                return true;
            }

            // Another region has claimed the agent
            //
            //m_log.Debug("[INVTRANSFERMOD]: last resort. Returning false in " + scene.RegionInfo.RegionName);
            return false;
        }

        public void ClientLoggedOut(UUID agentID, Scene scene)
        {
            if (m_AgentRegions.ContainsKey(agentID))
                m_AgentRegions.Remove(agentID);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="msg"></param>
        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // Check if this is ours to handle
            //
            Scene scene = FindClientScene(new UUID(msg.toAgentID));

            if (scene == null)
                return;

            // Find agent to deliver to
            //
            ScenePresence user = scene.GetScenePresence(new UUID(msg.toAgentID));

            // Just forward to local handling
            OnInstantMessage(user.ControllingClient, msg);

        }
    }
}
