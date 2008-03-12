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
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using System.IO;
using System.Text;
using System.Xml;
using OpenSim.Region.Environment.Interfaces;


namespace OpenSim.Region.Environment.Scenes
{
    public partial class Scene
    {
        private static readonly log4net.ILog m_log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Start all the scripts in the scene which should be started.
        /// </summary>
        public void StartScripts()
        {
            m_log.Info("[PRIM INVENTORY]: Starting scripts in scene");
            
            foreach (SceneObjectGroup group in Entities.Values)
            {
                group.StartScripts();
            }            
        }

        /// <summary>
        /// Add an inventory item to an avatar's inventory.
        /// </summary>
        /// <param name="remoteClient">The remote client controlling the avatar</param>
        /// <param name="item">The item.  This structure contains all the item metadata, including the folder
        /// in which the item is to be placed.</param>
        public void AddInventoryItem(IClientAPI remoteClient, InventoryItemBase item)
        {
            CachedUserInfo userInfo 
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            
            if (userInfo != null)
            {
                userInfo.AddItem(remoteClient.AgentId, item);
                remoteClient.SendInventoryItemCreateUpdate(item);
            }
        }

        /// <summary> 
        /// <see>AddInventoryItem(LLUUID, InventoryItemBase)</see>
        /// </summary>
        /// <param name="avatarId">The ID of the avatar</param>
        /// <param name="item">The item.  This structure contains all the item metadata, including the folder
        /// in which the item is to be placed.</param>        
        public void AddInventoryItem(LLUUID avatarId, InventoryItemBase item)
        {
            ScenePresence avatar;

            if (!TryGetAvatar(avatarId, out avatar))
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not find avatar {0} to add inventory item", avatarId);
                return;
            }

            AddInventoryItem(avatar.ControllingClient, item);
        }

        /// <summary>
        /// Capability originating call to update the asset of an item in an agent's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public LLUUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, LLUUID itemID, byte[] data)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AssetBase asset =
                            CreateAsset(item.inventoryName, item.inventoryDescription, (sbyte) item.invType,
                                        (sbyte) item.assetType, data);
                        AssetCache.AddAsset(asset);

                        item.assetID = asset.FullID;
                        userInfo.UpdateItem(remoteClient.AgentId, item);

                        // remoteClient.SendInventoryItemCreateUpdate(item);
                        if ((InventoryType) item.invType == InventoryType.Notecard)
                        {
                            //do we want to know about updated note cards?
                        }
                        else if ((InventoryType) item.invType == InventoryType.LSL)
                        {
                            // do we want to know about updated scripts
                        }

                        return (asset.FullID);
                    }
                }
            }
            return LLUUID.Zero;
        }

        /// <summary>
        /// <see>CapsUpdatedInventoryItemAsset(IClientAPI, LLUUID, byte[])</see>
        /// </summary>
        private LLUUID CapsUpdateInventoryItemAsset(LLUUID avatarId, LLUUID itemID, byte[] data)
        {
            ScenePresence avatar;

            if (TryGetAvatar(avatarId, out avatar))
            {
                return CapsUpdateInventoryItemAsset(avatar.ControllingClient, itemID, data);
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: " +
                    "Avatar {0} cannot be found to update its inventory item asset",
                    avatarId);
            }

            return LLUUID.Zero;
        }

        /// <summary>
        /// Capability originating call to update the asset of a script in a prim's (task's) inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="primID">The prim which contains the item to update</param>
        /// <param name="isScriptRunning">Indicates whether the script to update is currently running</param>
        /// <param name="data"></param>       
        public void CapsUpdateTaskInventoryScriptAsset(IClientAPI remoteClient, LLUUID itemId,
                                                       LLUUID primId, bool isScriptRunning, byte[] data)
        {
            // Retrieve group
            SceneObjectPart part = GetSceneObjectPart(primId);
            SceneObjectGroup group = part.ParentGroup;            
            if (null == group)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Prim inventory update requested for item ID {0} in prim ID {1} but this prim does not exist",
                    itemId, primId);

                return;
            }
                        
            // Retrieve item
            TaskInventoryItem item = group.GetInventoryItem(part.LocalId, itemId);
            if (null == item)
            {
                return;
            }
            
            // Create new asset
            // XXX Hardcoding the numbers is a temporary measure - need an enumeration for this 
            // There may well be one in libsecondlife
            AssetBase asset = CreateAsset(item.Name, item.Description, 10, 10, data);
            AssetCache.AddAsset(asset);
                        
            // Update item with new asset
            item.AssetID = asset.FullID;
            group.UpdateInventoryItem(item);
            group.GetProperties(remoteClient);
            
            // Trigger rerunning of script (use TriggerRezScript event, see RezScript)           
            if (isScriptRunning)
            {
                group.StopScript(part.LocalId, item.ItemID);
                group.StartScript(part.LocalId, item.ItemID);            
            }
        }

        /// <summary>
        /// <see>CapsUpdateTaskInventoryScriptAsset(IClientAPI, LLUUID, LLUUID, bool, byte[])</see>
        /// </summary>      
        private void CapsUpdateTaskInventoryScriptAsset(LLUUID avatarId, LLUUID itemId,
                                                        LLUUID primId, bool isScriptRunning, byte[] data)
        {
            ScenePresence avatar;

            if (TryGetAvatar(avatarId, out avatar))
            {
                CapsUpdateTaskInventoryScriptAsset(
                    avatar.ControllingClient, itemId, primId, isScriptRunning, data);
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Avatar {0} cannot be found to update its prim item asset",
                    avatarId);
            }
        }

        /// <summary>
        /// Update an item which is either already in the client's inventory or is within
        /// a transaction
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID">The transaction ID.  If this is LLUUID.Zero we will
        /// assume that we are not in a transaction</param>
        /// <param name="itemID">The ID of the updated item</param>
        /// <param name="name">The name of the updated item</param>
        /// <param name="description">The description of the updated item</param>
        /// <param name="nextOwnerMask">The permissions of the updated item</param>
        public void UpdateInventoryItemAsset(IClientAPI remoteClient, LLUUID transactionID,
                                             LLUUID itemID, string name, string description,
                                             uint nextOwnerMask)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

            if (userInfo != null && userInfo.RootFolder != null)
            {
                InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                
                if (item != null)
                {
                    if (LLUUID.Zero == transactionID)
                    {
                        item.inventoryName = name;
                        item.inventoryDescription = description;
                        item.inventoryNextPermissions = nextOwnerMask;

                        userInfo.UpdateItem(remoteClient.AgentId, item);
                    }
                    else
                    {
                        IAgentAssetTransactions agentTransactions = this.RequestModuleInterface<IAgentAssetTransactions>();
                        if (agentTransactions != null)
                        {
                            agentTransactions.HandleItemUpdateFromTransaction(
                                         remoteClient, transactionID, item);
                        }
                    }
                }
                else
                {
                    m_log.Error(
                        "[AGENTINVENTORY]: Item ID " + itemID + " not found for an inventory item update.");
                }
            }
            else
            {
                m_log.Error(
                    "[AGENT INVENTORY]: Agent ID " + remoteClient.AgentId + " not found for an inventory item update.");
            }
        }

        public void CopyInventoryItem(IClientAPI remoteClient, uint callbackID, LLUUID oldAgentID, LLUUID oldItemID,
                                      LLUUID newFolderID, string newName)
        {
            InventoryItemBase item = CommsManager.UserProfileCacheService.libraryRoot.HasItem(oldItemID);
            if (item == null)
            {
                CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(oldAgentID);
                if (userInfo == null)
                {
                    m_log.Error("[AGENT INVENTORY]: Failed to find user " + oldAgentID.ToString());
                    return;
                }

                if (userInfo.RootFolder != null)
                {
                    item = userInfo.RootFolder.HasItem(oldItemID);
                    if (item == null)
                    {
                        m_log.Error("[AGENT INVENTORY]: Failed to find item " + oldItemID.ToString());
                        return;
                    }
                }
                else
                {
                    m_log.Error("[AGENT INVENTORY]: Failed to find item " + oldItemID.ToString());
                    return;
                }
            }
            
            AssetBase asset 
                = AssetCache.GetAsset(
                    item.assetID, (item.assetType == (int)AssetType.Texture ? true : false));

            if (asset != null)
            {
                // TODO: preserve current permissions?
                CreateNewInventoryItem(
                    remoteClient, newFolderID, callbackID, asset, item.inventoryNextPermissions);
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not copy item {0} since asset {1} could not be found",
                    item.inventoryName, item.assetID);                
            }
        }

        private AssetBase CreateAsset(string name, string description, sbyte invType, sbyte assetType, byte[] data)
        {
            AssetBase asset = new AssetBase();
            asset.Name = name;
            asset.Description = description;
            asset.InvType = invType;
            asset.Type = assetType;
            asset.FullID = LLUUID.Random(); // TODO: check for conflicts
            asset.Data = (data == null) ? new byte[1] : data;
            return asset;
        }

        public void MoveInventoryItem(IClientAPI remoteClient, LLUUID folderID, LLUUID itemID, int length,
                                      string newName)
        {
            m_log.Info(
                "[AGENT INVENTORY]: " +
                "Moving item for " + remoteClient.AgentId.ToString());

            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo == null)
            {
                m_log.Error("[AGENT INVENTORY]: Failed to find user " + remoteClient.AgentId.ToString());
                return;
            }

            if (userInfo.RootFolder != null)
            {
                InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                if (item != null)
                {
                    if (newName != System.String.Empty)
                    {
                        item.inventoryName = newName;
                    }
                    item.parentFolderID = folderID;
                    userInfo.DeleteItem(remoteClient.AgentId, item);

                    // TODO: preserve current permissions?
                    AddInventoryItem(remoteClient, item);
                }
                else
                {
                    m_log.Error("[AGENT INVENTORY]: Failed to find item " + itemID.ToString());
                    return;
                }
            }
            else
            {
                m_log.Error("[AGENT INVENTORY]: Failed to find item " + itemID.ToString() + ", no root folder");
                return;
            }
        }

        /// <summary>
        /// Create a new inventory item.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="asset"></param>
        /// <param name="nextOwnerMask"></param>
        private void CreateNewInventoryItem(IClientAPI remoteClient, LLUUID folderID, uint callbackID,
                                            AssetBase asset, uint nextOwnerMask)
        {
            CachedUserInfo userInfo 
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            
            if (userInfo != null)
            {
                InventoryItemBase item = new InventoryItemBase();
                item.avatarID = remoteClient.AgentId;
                item.creatorsID = remoteClient.AgentId;
                item.inventoryID = LLUUID.Random();
                item.assetID = asset.FullID;
                item.inventoryDescription = asset.Description;
                item.inventoryName = asset.Name;
                item.assetType = asset.Type;
                item.invType = asset.InvType;
                item.parentFolderID = folderID;
                item.inventoryCurrentPermissions = 2147483647;
                item.inventoryNextPermissions = nextOwnerMask;

                userInfo.AddItem(remoteClient.AgentId, item);
                remoteClient.SendInventoryItemCreateUpdate(item);
            }
            else
            {
                m_log.WarnFormat(
                    "No user details associated with client {0} uuid {1} in CreateNewInventoryItem!", 
                     remoteClient.Name, remoteClient.AgentId);
            }
        }

        /// <summary>
        /// Create a new inventory item.
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
        public void CreateNewInventoryItem(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID,
                                           uint callbackID, string description, string name, sbyte invType,
                                           sbyte assetType,
                                           byte wearableType, uint nextOwnerMask)
        {
            if (transactionID == LLUUID.Zero)
            {
                CachedUserInfo userInfo 
                    = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
                
                if (userInfo != null)
                {
                    AssetBase asset = CreateAsset(name, description, invType, assetType, null);
                    AssetCache.AddAsset(asset);

                    CreateNewInventoryItem(remoteClient, folderID, callbackID, asset, nextOwnerMask);
                }
                else
                {
                    m_log.ErrorFormat(
                        "userInfo for agent uuid {0} unexpectedly null in CreateNewInventoryItem", 
                        remoteClient.AgentId);
                }
            }
            else
            {
                IAgentAssetTransactions agentTransactions = this.RequestModuleInterface<IAgentAssetTransactions>();
                if (agentTransactions != null)
                {
                    agentTransactions.HandleItemCreationFromTransaction(
                    remoteClient, transactionID, folderID, callbackID, description,
                    name, invType, assetType, wearableType, nextOwnerMask); 
                }

                                
            }
        }

        private void RemoveInventoryItem(IClientAPI remoteClient, LLUUID itemID)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo == null)
            {
                m_log.Error("[AGENT INVENTORY]: Failed to find user " + remoteClient.AgentId.ToString());
                return;
            }

            // is going through the root folder really the best way? 
            // this triggers a tree walk to find and remove the item. 8-(
            // since this only happens in Trash (in theory) shouldn't we grab 
            // the trash folder directly instead of RootFolder?
            if (userInfo.RootFolder != null)
            {
                InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                if (item != null)
                {
                    userInfo.DeleteItem(remoteClient.AgentId, item);
                }
            }
        }

        private void RemoveInventoryFolder(IClientAPI remoteClient, LLUUID folderID)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo == null)
            {
                m_log.Error("[AGENT INVENTORY]: Failed to find user " + remoteClient.AgentId.ToString());
                return;
            }

            if (userInfo.RootFolder != null)
            {
                InventoryItemBase folder = userInfo.RootFolder.HasItem(folderID);
                if (folder != null)
                {
                    // doesn't work just yet, commented out. will fix in next patch.
                    // userInfo.DeleteItem(remoteClient.AgentId, folder);
                }
            }
        }

        private SceneObjectGroup GetGroupByPrim(uint localID)
        {
            List<EntityBase> EntitieList = GetEntities();

            foreach (EntityBase ent in EntitieList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup) ent).HasChildPrim(localID))
                        return (SceneObjectGroup) ent;
                }
            }
            return null;
        }

        /// <summary>
        /// Request a prim (task) inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="primLocalID"></param>
        public void RequestTaskInventory(IClientAPI remoteClient, uint primLocalID)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                bool fileChange = group.GetPartInventoryFileName(remoteClient, primLocalID);
                if (fileChange)
                {
                    if (XferManager != null)
                    {
                        group.RequestInventoryFile(primLocalID, XferManager);
                    }
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Inventory requested of prim {0} which doesn't exist", primLocalID);
            }
        }

        /// <summary>
        /// Remove an item from a prim (task) inventory
        /// </summary>
        /// <param name="remoteClient">Unused at the moment but retained since the avatar ID might
        /// be necessary for a permissions check at some stage.</param>
        /// <param name="itemID"></param>
        /// <param name="localID"></param>
        public void RemoveTaskInventory(IClientAPI remoteClient, LLUUID itemID, uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                int type = group.RemoveInventoryItem(localID, itemID);
                group.GetProperties(remoteClient);
                if (type == 10)
                {
                    EventManager.TriggerRemoveScript(localID, itemID);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Removal of item {0} requested of prim {1} but this prim does not exist",
                    itemID,
                    localID);
            }
        }

        /// <summary>
        /// Update an item in a prim (task) inventory.  
        /// This method does not handle scripts, <see>RezScript(IClientAPI, LLUUID, unit)</see>
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="folderID"></param>
        /// <param name="primLocalID"></param>
        public void UpdateTaskInventory(IClientAPI remoteClient, LLUUID itemID, LLUUID folderID,
                                        uint primLocalID)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);

            if (group != null)
            {
                LLUUID copyID = LLUUID.Random();
                if (itemID != LLUUID.Zero)
                {
                    CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

                    if (userInfo != null && userInfo.RootFolder != null)
                    {
                        InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);

                        // Try library
                        // XXX clumsy, possibly should be one call
                        if (null == item)
                        {
                            item = CommsManager.UserProfileCacheService.libraryRoot.HasItem(itemID);
                        }

                        if (item != null)
                        {
                            if (item.assetType == 0 || item.assetType == 1 || item.assetType == 10)
                            {
                                group.AddInventoryItem(remoteClient, primLocalID, item, copyID);
                                m_log.InfoFormat(
                                    "[PRIM INVENTORY]: Update with item {0} requested of prim {1} for {2}", 
                                    item.inventoryName, primLocalID, remoteClient.Name);
                                group.GetProperties(remoteClient);
                            }
                            else
                            { 
                                // XXX Nasty temporary way of stopping things other than sounds, textures and scripts
                                // from going in a prim's inventory, since other things will not currently work
                                // See http://opensimulator.org/mantis/view.php?id=711 for the error caused later on
                                // - to implement requires changes to TaskInventoryItem (which really requires the current
                                // nasty way it is done to be changed).
                                m_log.WarnFormat(
                                    "[PRIM INVENTORY]: Sorry, prim inventory storage of asset type {0} is not yet supported", 
                                    item.assetType);
                            }
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[PRIM INVENTORY]: Could not find inventory item {0} to update for {1}!",
                                itemID, remoteClient.Name);
                        }
                    }
                }
            }
            else
            {
                m_log.WarnFormat(
                    "[PRIM INVENTORY]: " +
                    "Update with item {0} requested of prim {1} for {2} but this prim does not exist",
                    itemID, primLocalID, remoteClient.Name);
            }
        }

        /// <summary>
        /// Rez a script into a prim's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"> </param>
        /// <param name="localID"></param>
        public void RezScript(IClientAPI remoteClient, LLUUID itemID, uint localID)
        {
            LLUUID copyID = LLUUID.Random();
            
            if (itemID != LLUUID.Zero)
            {
                CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            
                if (userInfo != null && userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    
                    // Try library
                    // XXX clumsy, possibly should be one call
                    if (null == item)
                    {
                        item = CommsManager.UserProfileCacheService.libraryRoot.HasItem(itemID);
                    }
                        
                    if (item != null)
                    {
                        SceneObjectGroup group = GetGroupByPrim(localID);
                        if (group != null)
                        {
                            group.AddInventoryItem(remoteClient, localID, item, copyID);
                            group.StartScript(localID, copyID);
                            group.GetProperties(remoteClient);
    
    //                        m_log.InfoFormat("[PRIMINVENTORY]: " +
    //                                         "Rezzed script {0} into prim local ID {1} for user {2}",
    //                                         item.inventoryName, localID, remoteClient.Name);
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[PRIM INVENTORY]: " +
                                "Could not rez script {0} into prim local ID {1} for user {2}"
                                + " because the prim could not be found in the region!",
                                item.inventoryName, localID, remoteClient.Name);
                        }
                    }
                    else
                    {
                        m_log.ErrorFormat(
                            "[PRIM INVENTORY]: Could not find script inventory item {0} to rez for {1}!",
                            itemID, remoteClient.Name);
                    }
                }
            }
            else  // If the itemID is zero then the script has been rezzed directly in an object's inventory
            {
                // not yet implemented
                // TODO Need to get more details from original RezScript packet                
                // XXX jc tmp
//                AssetBase asset = CreateAsset("chimney sweep", "sailor.lsl", 10, 10, null);
//                AssetCache.AddAsset(asset);            
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="simClient"></param>
        public virtual void DeRezObject(Packet packet, IClientAPI remoteClient)
        {
            DeRezObjectPacket DeRezPacket = (DeRezObjectPacket) packet;

            if (DeRezPacket.AgentBlock.DestinationID == LLUUID.Zero)
            {
                //currently following code not used (or don't know of any case of destination being zero
            }
            else
            {
                foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
                {
                    EntityBase selectedEnt = null;
                    //m_log.Info("[CLIENT]: LocalID:" + Data.ObjectLocalID.ToString());

                    List<EntityBase> EntitieList = GetEntities();

                    foreach (EntityBase ent in EntitieList)
                    {
                        if (ent.LocalId == Data.ObjectLocalID)
                        {
                            selectedEnt = ent;
                            break;
                        }
                    }
                    if (selectedEnt != null)
                    {
                        if (PermissionsMngr.CanDeRezObject(remoteClient.AgentId, ((SceneObjectGroup) selectedEnt).UUID))
                        {
                            string sceneObjectXml = ((SceneObjectGroup) selectedEnt).ToXmlString();
                            CachedUserInfo userInfo =
                                CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
                            if (userInfo != null)
                            {
                                AssetBase asset = CreateAsset(
                                    ((SceneObjectGroup) selectedEnt).GetPartName(selectedEnt.LocalId),
                                    ((SceneObjectGroup) selectedEnt).GetPartDescription(selectedEnt.LocalId),
                                    (sbyte) InventoryType.Object,
                                    (sbyte) AssetType.Primitive,
                                    Helpers.StringToField(sceneObjectXml));
                                AssetCache.AddAsset(asset);

                                InventoryItemBase item = new InventoryItemBase();
                                item.avatarID = remoteClient.AgentId;
                                item.creatorsID = remoteClient.AgentId;
                                item.inventoryID = LLUUID.Random(); // TODO: check for conflicts
                                item.assetID = asset.FullID;
                                item.inventoryDescription = asset.Description;
                                item.inventoryName = asset.Name;
                                item.assetType = asset.Type;
                                item.invType = asset.InvType;
                                item.parentFolderID = DeRezPacket.AgentBlock.DestinationID;
                                item.inventoryCurrentPermissions = 2147483647;
                                item.inventoryNextPermissions = 2147483647;
                                item.inventoryEveryOnePermissions =
                                    ((SceneObjectGroup) selectedEnt).RootPart.EveryoneMask;
                                item.inventoryBasePermissions = ((SceneObjectGroup) selectedEnt).RootPart.BaseMask;
                                item.inventoryCurrentPermissions = ((SceneObjectGroup) selectedEnt).RootPart.OwnerMask;

                                userInfo.AddItem(remoteClient.AgentId, item);
                                remoteClient.SendInventoryItemCreateUpdate(item);
                            }

                            DeleteSceneObjectGroup((SceneObjectGroup) selectedEnt);
                        }
                    }
                }
            }
        }

        public virtual void RezObject(IClientAPI remoteClient, LLUUID itemID, LLVector3 RayEnd, LLVector3 RayStart,
                                    LLUUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    uint EveryoneMask, uint GroupMask, uint NextOwnerMask, uint ItemFlags,
                                    bool RezSelected, bool RemoveItem, LLUUID fromTaskID)
        {
            byte bRayEndIsIntersection = (byte)0;

            if (RayEndIsIntersection)
            {
                bRayEndIsIntersection = (byte)1;
            }
            else
            {
                bRayEndIsIntersection = (byte)0;
            }

            LLVector3 pos = GetNewRezLocation(RayStart, RayEnd, RayTargetID, new LLQuaternion(0, 0, 0, 1), BypassRayCast, bRayEndIsIntersection);
            RezObject(remoteClient, itemID, pos);
        }

        public virtual void RezObject(IClientAPI remoteClient, LLUUID itemID, LLVector3 pos)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AssetBase rezAsset = AssetCache.GetAsset(item.assetID, false);

                        if (rezAsset != null)
                        {
                            AddRezObject(Helpers.FieldToUTF8String(rezAsset.Data), pos);
                            //userInfo.DeleteItem(remoteClient.AgentId, item);
                            //remoteClient.SendRemoveInventoryItem(itemID);
                        }
                    }
                }
            }
        }
        
        public void RezSingleAttachment(IClientAPI remoteClient, LLUUID itemID, uint AttachmentPt,
                                    uint ItemFlags, uint NextOwnerMask)
        {
            System.Console.WriteLine("RezSingleAttachment: unimplemented yet");
        }


        private void AddRezObject(string xmlData, LLVector3 pos)
        {
            SceneObjectGroup group = new SceneObjectGroup(this, m_regionHandle, xmlData);
            group.ResetIDs();
            AddEntity(group);
            group.AbsolutePosition = pos;
            SceneObjectPart rootPart = group.GetChildPart(group.UUID);
            rootPart.TrimPermissions();
            group.ApplyPhysics(m_physicalPrim);
            group.StartScripts();

            //bool UsePhysics = (((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) > 0)&& m_physicalPrim);
            //if ((rootPart.ObjectFlags & (uint) LLObject.ObjectFlags.Phantom) == 0)
            //{
            //PrimitiveBaseShape pbs = rootPart.Shape;
            //rootPart.PhysActor = PhysicsScene.AddPrimShape(
            //rootPart.Name,
            //pbs,
            //new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
            //                  rootPart.AbsolutePosition.Z),
            //new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
            //new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
            //               rootPart.RotationOffset.Y, rootPart.RotationOffset.Z), UsePhysics);

            // rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);

            // }
            //
            rootPart.ScheduleFullUpdate();
        }
    }
}
