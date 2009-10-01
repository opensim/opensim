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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Transfer
{
    public class InventoryTransferModule : IInventoryTransferModule, IRegionModule
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        private List<Scene> m_Scenelist = new List<Scene>();
        private Dictionary<UUID, Scene> m_AgentRegions =
                new Dictionary<UUID, Scene>();

        private IMessageTransferModule m_TransferModule = null;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (config.Configs["Messaging"] != null)
            {
                // Allow disabling this module in config
                //
                if (config.Configs["Messaging"].GetString(
                        "InventoryTransferModule", "InventoryTransferModule") !=
                        "InventoryTransferModule")
                    return;
            }

            if (!m_Scenelist.Contains(scene))
            {
                m_Scenelist.Add(scene);

                scene.RegisterModuleInterface<IInventoryTransferModule>(this);

                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnClientClosed += ClientLoggedOut;
                scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            }
        }

        public void PostInitialise()
        {
            if (m_Scenelist.Count > 0)
            {
                m_TransferModule = m_Scenelist[0].RequestModuleInterface<IMessageTransferModule>();
                if (m_TransferModule == null)
                    m_log.Error("[INVENTORY TRANSFER] No Message transfer module found, transfers will be local only");
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InventoryModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
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
                    {
                        if (!presence.IsChildAgent)
                            return scene;
                    }
                }
            }
            return null;
        }

        private void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            m_log.InfoFormat("OnInstantMessage {0}", im.dialog);
            Scene scene = FindClientScene(client.AgentId);

            if (scene == null) // Something seriously wrong here.
                return;



            if (im.dialog == (byte) InstantMessageDialog.InventoryOffered)
            {
                //m_log.DebugFormat("Asset type {0}", ((AssetType)im.binaryBucket[0]));
                
                ScenePresence user = scene.GetScenePresence(new UUID(im.toAgentID));
                UUID copyID;

                // First byte is the asset type
                AssetType assetType = (AssetType)im.binaryBucket[0];
                
                if (AssetType.Folder == assetType)
                {
                    UUID folderID = new UUID(im.binaryBucket, 1);
                    
                    m_log.DebugFormat("[AGENT INVENTORY]: Inserting original folder {0} "+
                            "into agent {1}'s inventory",
                            folderID, new UUID(im.toAgentID));
                    
                    InventoryFolderBase folderCopy 
                        = scene.GiveInventoryFolder(new UUID(im.toAgentID), client.AgentId, folderID, UUID.Zero);
                    
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
                    
                    if (user != null && !user.IsChildAgent)
                    {
                        user.ControllingClient.SendBulkUpdateInventory(folderCopy);
                    }
                }
                else
                {
                    // First byte of the array is probably the item type
                    // Next 16 bytes are the UUID
                    m_log.Info("OnInstantMessage - giving item");

                    UUID itemID = new UUID(im.binaryBucket, 1);

                    m_log.DebugFormat("[AGENT INVENTORY]: Inserting item {0} "+
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
                    
                    if (user != null && !user.IsChildAgent)
                    {
                        user.ControllingClient.SendBulkUpdateInventory(itemCopy);
                    }
                }

                // Send the IM to the recipient. The item is already
                // in their inventory, so it will not be lost if
                // they are offline.
                //
                if (user != null && !user.IsChildAgent)
                {
                    // And notify. Transaction ID is the item ID. We get that
                    // same ID back on the reply so we know what to act on
                    //
                    user.ControllingClient.SendInstantMessage(im);

                    return;
                }
                else
                {
                    if (m_TransferModule != null)
                        m_TransferModule.SendInstantMessage(im, delegate(bool success) {});
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
                        m_TransferModule.SendInstantMessage(im, delegate(bool success) {});
                }
            }
            else if (im.dialog == (byte) InstantMessageDialog.InventoryDeclined)
            {
                // Here, the recipient is local and we can assume that the
                // inventory is loaded. Courtesy of the above bulk update,
                // It will have been pushed to the client, too
                //
                
                //CachedUserInfo userInfo =
                //        scene.CommsManager.UserProfileCacheService.
                //        GetUserDetails(client.AgentId);
                IInventoryService invService = scene.InventoryService;

                InventoryFolderBase trashFolder =
                    invService.GetFolderForType(client.AgentId, AssetType.TrashFolder);
                
                UUID inventoryEntityID = new UUID(im.imSessionID); // The inventory item/folder, back from it's trip

                InventoryItemBase item = new InventoryItemBase(inventoryEntityID, client.AgentId);
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
                    folder = new InventoryFolderBase(inventoryEntityID, client.AgentId);
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

        public void SetRootAgentScene(UUID agentID, Scene scene)
        {
            m_AgentRegions[agentID] = scene;
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

                    if (s.TryGetAvatar(agentID, out presence))
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
            m_log.Info("OnFridInstantMessage");
            if (msg.dialog != (byte) InstantMessageDialog.InventoryOffered)
                return;

            if (msg.binaryBucket.Length < 17) // Invalid
                return;

            Scene scene = FindClientScene(new UUID(msg.toAgentID));

            // Find agent to deliver to
            //
            ScenePresence user = scene.GetScenePresence(new UUID(msg.toAgentID));

            if (user == null) // Shouldn't happen
            {
                m_log.Debug("[INVENTORY TRANSFER] Can't find recipient");
                return;
            }

            //CachedUserInfo userInfo =
            //        scene.CommsManager.UserProfileCacheService.
            //        GetUserDetails(user.ControllingClient.AgentId);

            //if (userInfo == null)
            //{
            //    m_log.Debug("[INVENTORY TRANSFER] Can't find user info of recipient");
            //    return;
            //}

            AssetType assetType = (AssetType)msg.binaryBucket[0];
            IInventoryService invService = scene.InventoryService;

            if (AssetType.Folder == assetType)
            {
                UUID folderID = new UUID(msg.binaryBucket, 1);
                InventoryFolderBase folder = new InventoryFolderBase();

                folder.ID = folderID;
                folder.Owner = user.ControllingClient.AgentId;

                // Fetch from service
                //
                folder = invService.GetFolder(folder);
                if (folder == null)
                {
                    m_log.Debug("[INVENTORY TRANSFER] Can't find folder to give");
                    return;
                }

                user.ControllingClient.SendBulkUpdateInventory(folder);

                               //// This unelegant, slow kludge is to reload the folders and
                               //// items. Since a folder give can transfer subfolders and
                               //// items, this is the easiest way to pull that stuff in
                               ////
                               //userInfo.DropInventory();
                               //userInfo.FetchInventory();

                // Deliver message
                //
                user.ControllingClient.SendInstantMessage(msg);
            }
            else
            {
                UUID itemID = new UUID(msg.binaryBucket, 1);
                InventoryItemBase item = new InventoryItemBase(itemID, user.ControllingClient.AgentId);

                // Fetch from service
                //
                item = invService.GetItem(item);
                if (item == null)
                {
                    m_log.Debug("[INVENTORY TRANSFER] Can't find item to give");
                    return;
                }

                // Update item to viewer (makes it appear in proper folder)
                //
                user.ControllingClient.SendBulkUpdateInventory(item);

                // Deliver message
                //
                user.ControllingClient.SendInstantMessage(msg);
            }
        }
    }
}
