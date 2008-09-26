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
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Timers;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Scenes
{
    class DeleteToInventoryHolder
    {
        public DeRezObjectPacket DeRezPacket;
        public EntityBase selectedEnt;
        public IClientAPI remoteClient;
        public SceneObjectGroup objectGroup;
        public UUID folderID;
        public bool permissionToDelete;
    }

    public partial class Scene
    {
        private Timer m_inventoryTicker;
        private readonly Queue<DeleteToInventoryHolder> m_inventoryDeletes = new Queue<DeleteToInventoryHolder>();

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Start all the scripts in the scene which should be started.
        /// </summary>
        public void CreateScriptInstances()
        {
            m_log.Info("[PRIM INVENTORY]: Starting scripts in scene");

            foreach (EntityBase group in Entities.Values)
            {
                if (group is SceneObjectGroup)
                {
                    ((SceneObjectGroup) group).CreateScriptInstances(0, false, DefaultScriptEngine);
                }
            }
        }

        public void AddUploadedInventoryItem(UUID agentID, InventoryItemBase item)
        {
            IMoneyModule money=RequestModuleInterface<IMoneyModule>();
            if (money != null)
            {
                money.ApplyUploadCharge(agentID);
            }

            AddInventoryItem(agentID, item);
        }

        public bool AddInventoryItemReturned(UUID AgentId, InventoryItemBase item)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(AgentId);
            if (userInfo != null)
            {
                userInfo.AddItem(item);
                return true;
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Agent was not found for add of item {1} {2}", item.Name, item.ID);

                return false;
            }
        }

        public void AddInventoryItem(UUID AgentID, InventoryItemBase item)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(AgentID);

            if (userInfo != null)
            {
                userInfo.AddItem(item);

                int userlevel = 0;
                if (ExternalChecks.ExternalChecksCanBeGodLike(AgentID))
                {
                    userlevel = 1;
                }
                // TODO: remove this cruft once MasterAvatar is fully deprecated
                //
                if (m_regInfo.MasterAvatarAssignedUUID == AgentID)
                {
                    userlevel = 2;
                }
                EventManager.TriggerOnNewInventoryItemUploadComplete(AgentID, item.AssetID, item.Name, userlevel);
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Agent {1} was not found for add of item {2} {3}",
                    AgentID, item.Name, item.ID);

                return;
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
                AddInventoryItem(remoteClient.AgentId, item);
                remoteClient.SendInventoryItemCreateUpdate(item);
            }
        }

        /// <summary>
        /// Capability originating call to update the asset of an item in an agent's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public UUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.FindItem(itemID);

                    if (item != null)
                    {
                        if ((InventoryType) item.InvType == InventoryType.Notecard)
                        {
                            if (!ExternalChecks.ExternalChecksCanEditNotecard(itemID, UUID.Zero, remoteClient.AgentId))
                            {
                                remoteClient.SendAgentAlertMessage("Insufficient permissions to edit notecard", false);
                                return UUID.Zero;
                            }
                            remoteClient.SendAgentAlertMessage("Notecard saved", false);
                        }
                        else if ((InventoryType) item.InvType == InventoryType.LSL)
                        {
                            if (!ExternalChecks.ExternalChecksCanEditScript(itemID, UUID.Zero, remoteClient.AgentId))
                            {
                                remoteClient.SendAgentAlertMessage("Insufficient permissions to edit script", false);
                                return UUID.Zero;
                            }
                            remoteClient.SendAgentAlertMessage("Script saved", false);
                        }

                        AssetBase asset =
                            CreateAsset(item.Name, item.Description, (sbyte)item.AssetType, data);
                        AssetCache.AddAsset(asset);

                        item.AssetID = asset.FullID;
                        userInfo.UpdateItem(item);

                        // remoteClient.SendInventoryItemCreateUpdate(item);
                        return (asset.FullID);
                    }
                }
            }
            return UUID.Zero;
        }

        /// <summary>
        /// <see>CapsUpdatedInventoryItemAsset(IClientAPI, UUID, byte[])</see>
        /// </summary>
        private UUID CapsUpdateInventoryItemAsset(UUID avatarId, UUID itemID, byte[] data)
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

            return UUID.Zero;
        }

        /// <summary>
        /// Capability originating call to update the asset of a script in a prim's (task's) inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="primID">The prim which contains the item to update</param>
        /// <param name="isScriptRunning">Indicates whether the script to update is currently running</param>
        /// <param name="data"></param>
        public void CapsUpdateTaskInventoryScriptAsset(IClientAPI remoteClient, UUID itemId,
                                                       UUID primId, bool isScriptRunning, byte[] data)
        {
            if (!ExternalChecks.ExternalChecksCanEditScript(itemId, primId, remoteClient.AgentId))
            {
                remoteClient.SendAgentAlertMessage("Insufficient permissions to edit script", false);
                return;
            }

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
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for caps script update "
                        + " but the item does not exist in this inventory",
                    itemId, part.Name, part.UUID);

                return;
            }

            AssetBase asset = CreateAsset(item.Name, item.Description, (sbyte)AssetType.LSLText, data);
            AssetCache.AddAsset(asset);

            if (isScriptRunning)
            {
                part.RemoveScriptInstance(item.ItemID);
            }
            // Update item with new asset
            item.AssetID = asset.FullID;
            group.UpdateInventoryItem(item);
            part.GetProperties(remoteClient);

            // Trigger rerunning of script (use TriggerRezScript event, see RezScript)
            if (isScriptRunning)
            {
                // Needs to determine which engine was running it and use that
                //
                part.CreateScriptInstance(item.ItemID, 0, false, DefaultScriptEngine);
            }
            else
            {
                remoteClient.SendAgentAlertMessage("Script saved", false);
            }
        }

        /// <summary>
        /// <see>CapsUpdateTaskInventoryScriptAsset(IClientAPI, UUID, UUID, bool, byte[])</see>
        /// </summary>
        private void CapsUpdateTaskInventoryScriptAsset(UUID avatarId, UUID itemId,
                                                        UUID primId, bool isScriptRunning, byte[] data)
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
        /// <param name="transactionID">The transaction ID.  If this is UUID.Zero we will
        /// assume that we are not in a transaction</param>
        /// <param name="itemID">The ID of the updated item</param>
        /// <param name="name">The name of the updated item</param>
        /// <param name="description">The description of the updated item</param>
        /// <param name="nextOwnerMask">The permissions of the updated item</param>
/*        public void UpdateInventoryItemAsset(IClientAPI remoteClient, UUID transactionID,
                                             UUID itemID, string name, string description,
                                             uint nextOwnerMask)*/
        public void UpdateInventoryItemAsset(IClientAPI remoteClient, UUID transactionID,
                                             UUID itemID, InventoryItemBase itemUpd)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

            if (userInfo != null && userInfo.RootFolder != null)
            {
                InventoryItemBase item = userInfo.RootFolder.FindItem(itemID);

                if (item != null)
                {
                    if (UUID.Zero == transactionID)
                    {
                        item.Name = itemUpd.Name;
                        item.Description = itemUpd.Description;
                        item.NextPermissions = itemUpd.NextPermissions;
                        item.CurrentPermissions |= 8; // Slam!
                        item.EveryOnePermissions = itemUpd.EveryOnePermissions;

                        // TODO: Requires sanity checks
                        //item.GroupID = itemUpd.GroupID;
                        //item.GroupOwned = itemUpd.GroupOwned;
                        //item.CreationDate = itemUpd.CreationDate;
                        // The client sends zero if its newly created?

                        if (itemUpd.CreationDate == 0)
                            item.CreationDate = Util.UnixTimeSinceEpoch();
                        else
                            item.CreationDate = itemUpd.CreationDate;

                        // TODO: Check if folder changed and move item
                        //item.NextPermissions = itemUpd.Folder;
                        item.InvType = itemUpd.InvType;
                        item.SalePrice = itemUpd.SalePrice;
                        item.SaleType = itemUpd.SaleType;
                        item.Flags = itemUpd.Flags;

                        userInfo.UpdateItem(item);
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

        /// <summary>
        /// Give an inventory item from one avatar to another
        /// </summary>
        /// <param name="recipientClient"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="itemId"></param>
        public void GiveInventoryItem(IClientAPI recipientClient, UUID senderId, UUID itemId)
        {
            // Retrieve the item from the sender
            CachedUserInfo senderUserInfo = CommsManager.UserProfileCacheService.GetUserDetails(senderId);

            if (senderUserInfo == null)
            {
                m_log.ErrorFormat(
                     "[AGENT INVENTORY]: Failed to find sending user {0} for item {1}", senderId, itemId);

                return;
            }

            if (senderUserInfo.RootFolder != null)
            {
                InventoryItemBase item = senderUserInfo.RootFolder.FindItem(itemId);

                if (item != null)
                {
                    if (!ExternalChecks.ExternalChecksBypassPermissions())
                    {
                        if ((item.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                            return;
                    }

                    // TODO get recipient's root folder
                    CachedUserInfo recipientUserInfo
                        = CommsManager.UserProfileCacheService.GetUserDetails(recipientClient.AgentId);

                    if (recipientUserInfo != null)
                    {
                        // Insert a copy of the item into the recipient
                        InventoryItemBase itemCopy = new InventoryItemBase();
                        itemCopy.Owner = recipientClient.AgentId;
                        itemCopy.Creator = senderId;
                        itemCopy.ID = UUID.Random();
                        itemCopy.AssetID = item.AssetID;
                        itemCopy.Description = item.Description;
                        itemCopy.Name = item.Name;
                        itemCopy.AssetType = item.AssetType;
                        itemCopy.InvType = item.InvType;
                        itemCopy.Folder = UUID.Zero;
                        if (ExternalChecks.ExternalChecksPropagatePermissions())
                        {
                            if (item.InvType == 6)
                            {
                                itemCopy.BasePermissions &= ~(uint)(PermissionMask.Copy | PermissionMask.Modify | PermissionMask.Transfer);
                                itemCopy.BasePermissions |= (item.CurrentPermissions & 7) << 13;
                            }
                            else
                            {
                                itemCopy.BasePermissions = item.BasePermissions & item.NextPermissions;
                            }

                            itemCopy.CurrentPermissions = itemCopy.BasePermissions;
                            if ((item.CurrentPermissions & 8) != 0) // Propagate slam bit
                            {
                                itemCopy.CurrentPermissions = item.NextPermissions;
                                itemCopy.BasePermissions=itemCopy.CurrentPermissions;
                                itemCopy.CurrentPermissions |= 8;
                            }

                            itemCopy.NextPermissions = item.NextPermissions;
                            itemCopy.EveryOnePermissions = item.EveryOnePermissions & item.NextPermissions;
                        }
                        else
                        {
                            itemCopy.CurrentPermissions = item.CurrentPermissions;
                            itemCopy.NextPermissions = item.NextPermissions;
                            itemCopy.EveryOnePermissions = item.EveryOnePermissions & item.NextPermissions;
                            itemCopy.BasePermissions = item.BasePermissions;
                        }
                        itemCopy.GroupID = item.GroupID;
                        itemCopy.GroupOwned = item.GroupOwned;
                        itemCopy.Flags = item.Flags;
                        itemCopy.SalePrice = item.SalePrice;
                        itemCopy.SaleType = item.SaleType;

                        itemCopy.CreationDate = item.CreationDate;

                        recipientUserInfo.AddItem(itemCopy);

                        if (!ExternalChecks.ExternalChecksBypassPermissions())
                        {
                            if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                                senderUserInfo.DeleteItem(itemId);
                        }

                        // Let the recipient client know about this new item
                        recipientClient.SendBulkUpdateInventory(itemCopy);
                    }
                    else
                    {
                        m_log.ErrorFormat(
                            "[AGENT INVENTORY]: Could not find userinfo for recipient user {0}, {1} of item {2}, {3} from {4}",
                            recipientClient.Name, recipientClient.AgentId, item.Name,
                            item.ID, senderId);
                    }
                }
                else
                {
                    m_log.ErrorFormat(
                        "[AGENT INVENTORY]: Failed to find item {0} to give to {1}", itemId, senderId);

                    return;
                }
            }
            else
            {
                m_log.Error("[AGENT INVENTORY]: Failed to find item " + itemId.ToString() + ", no root folder");
                return;
            }
        }

        public void CopyInventoryItem(IClientAPI remoteClient, uint callbackID, UUID oldAgentID, UUID oldItemID,
                                      UUID newFolderID, string newName)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: CopyInventoryItem received by {0} with oldAgentID {1}, oldItemID {2}, new FolderID {3}, newName {4}",
                remoteClient.AgentId, oldAgentID, oldItemID, newFolderID, newName);

            InventoryItemBase item = CommsManager.UserProfileCacheService.LibraryRoot.FindItem(oldItemID);

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
                    item = userInfo.RootFolder.FindItem(oldItemID);

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
                    item.AssetID, (item.AssetType == (int)AssetType.Texture ? true : false));

            if (asset != null)
            {
                if (newName != String.Empty)
                {
                    asset.Name = newName;
                }
                else
                {
                    newName = item.Name;
                }

                if (remoteClient.AgentId == oldAgentID)
                {
                    CreateNewInventoryItem(
                        remoteClient, newFolderID, newName, item.Flags, callbackID, asset, (sbyte)item.InvType,
                        item.BasePermissions, item.CurrentPermissions, item.EveryOnePermissions, item.NextPermissions, Util.UnixTimeSinceEpoch());
                }
                else
                {
                    CreateNewInventoryItem(
                        remoteClient, newFolderID, newName, item.Flags, callbackID, asset, (sbyte)item.InvType,
                        item.NextPermissions, item.NextPermissions, item.EveryOnePermissions & item.NextPermissions, item.NextPermissions, Util.UnixTimeSinceEpoch());
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Could not copy item {0} since asset {1} could not be found",
                    item.Name, item.AssetID);
            }
        }

        /// <summary>
        /// Create a new asset data structure.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="invType"></param>
        /// <param name="assetType"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private AssetBase CreateAsset(string name, string description, sbyte assetType, byte[] data)
        {
            AssetBase asset = new AssetBase();
            asset.Name = name;
            asset.Description = description;
            asset.Type = assetType;
            asset.FullID = UUID.Random();
            asset.Data = (data == null) ? new byte[1] : data;

            return asset;
        }

        /// <summary>
        /// Move an item within the agent's inventory.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="itemID"></param>
        /// <param name="length"></param>
        /// <param name="newName"></param>
        public void MoveInventoryItem(IClientAPI remoteClient, UUID folderID, UUID itemID, int length,
                                      string newName)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Moving item {0} to {1} for {2}", itemID, folderID, remoteClient.AgentId);

            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

            if (userInfo == null)
            {
                m_log.Error("[AGENT INVENTORY]: Failed to find user " + remoteClient.AgentId.ToString());

                return;
            }

            if (userInfo.RootFolder != null)
            {
                InventoryItemBase item = userInfo.RootFolder.FindItem(itemID);

                if (item != null)
                {
                    if (newName != String.Empty)
                    {
                        item.Name = newName;
                    }
                    item.Folder = folderID;

                    userInfo.DeleteItem(item.ID);

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
        /// <param name="invType"></param>
        /// <param name="nextOwnerMask"></param>
        private void CreateNewInventoryItem(IClientAPI remoteClient, UUID folderID, string name, uint flags, uint callbackID,
                                            AssetBase asset, sbyte invType, uint nextOwnerMask, int creationDate)
        {
            CreateNewInventoryItem(
                remoteClient, folderID, name, flags, callbackID, asset, invType,
                (uint)PermissionMask.All, (uint)PermissionMask.All, 0, nextOwnerMask, creationDate);
        }

        /// <summary>
        /// Create a new Inventory Item
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="asset"></param>
        /// <param name="invType"></param>
        /// <param name="nextOwnerMask"></param>
        /// <param name="creationDate"></param>
        private void CreateNewInventoryItem(
            IClientAPI remoteClient, UUID folderID, string name, uint flags, uint callbackID, AssetBase asset, sbyte invType,
            uint baseMask, uint currentMask, uint everyoneMask, uint nextOwnerMask, int creationDate)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

            if (userInfo != null)
            {
                InventoryItemBase item = new InventoryItemBase();
                item.Owner = remoteClient.AgentId;
                item.Creator = remoteClient.AgentId;
                item.ID = UUID.Random();
                item.AssetID = asset.FullID;
                item.Description = asset.Description;
                item.Name = name;
                item.Flags = flags;
                item.AssetType = asset.Type;
                item.InvType = invType;
                item.Folder = folderID;
                item.CurrentPermissions = currentMask;
                item.NextPermissions = nextOwnerMask;
                item.EveryOnePermissions = everyoneMask;
                item.BasePermissions = baseMask;
                item.CreationDate = creationDate;

                userInfo.AddItem(item);
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
        /// Create a new inventory item.  Called when the client creates a new item directly within their
        /// inventory (e.g. by selecting a context inventory menu option).
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
        public void CreateNewInventoryItem(IClientAPI remoteClient, UUID transactionID, UUID folderID,
                                           uint callbackID, string description, string name, sbyte invType,
                                           sbyte assetType,
                                           byte wearableType, uint nextOwnerMask, int creationDate)
        {
//            m_log.DebugFormat("[AGENT INVENTORY]: Received request to create inventory item {0} in folder {1}", name, folderID);

            if (transactionID == UUID.Zero)
            {
                CachedUserInfo userInfo
                    = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

                if (userInfo != null)
                {
                    ScenePresence presence;
                    TryGetAvatar(remoteClient.AgentId, out presence);
                    byte[] data = null;
                    if (invType == 3 && presence != null) // OpenMetaverse.asset.assettype.landmark = 3 - needs to be turned into an enum
                    {
                        Vector3 pos=presence.AbsolutePosition;
                        string strdata=String.Format("Landmark version 2\nregion_id {0}\nlocal_pos {1} {2} {3}\nregion_handle {4}\n",
                            presence.Scene.RegionInfo.RegionID,
                            pos.X, pos.Y, pos.Z,
                            presence.RegionHandle);
                        data=Encoding.ASCII.GetBytes(strdata);
                    }

                    AssetBase asset = CreateAsset(name, description, assetType, data);
                    AssetCache.AddAsset(asset);

                    CreateNewInventoryItem(remoteClient, folderID, asset.Name, 0, callbackID, asset, invType, nextOwnerMask, creationDate);
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

        /// <summary>
        /// Remove an inventory item for the client's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        private void RemoveInventoryItem(IClientAPI remoteClient, UUID itemID)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

            if (userInfo == null)
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Failed to find user {0} {1} to delete inventory item {2}",
                    remoteClient.Name, remoteClient.AgentId, itemID);

                return;
            }

            userInfo.DeleteItem(itemID);
        }

        /// <summary>
        /// Removes an inventory folder.  Although there is a packet in the Linden protocol for this, it may be
        /// legacy and not currently used (purge folder is used to remove folders from trash instead).
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        private void RemoveInventoryFolder(IClientAPI remoteClient, UUID folderID)
        {
            CachedUserInfo userInfo
                = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

            if (userInfo == null)
            {
                m_log.Warn("[AGENT INVENTORY]: Failed to find user " + remoteClient.AgentId.ToString());
                return;
            }

            if (userInfo.RootFolder != null)
            {
                InventoryItemBase folder = userInfo.RootFolder.FindItem(folderID);

                if (folder != null)
                {
                    m_log.WarnFormat(
                         "[AGENT INVENTORY]: Remove folder not implemented in request by {0} {1} for {2}",
                         remoteClient.Name, remoteClient.AgentId, folderID);

                    // doesn't work just yet, commented out. will fix in next patch.
                    // userInfo.DeleteItem(folder);
                }
            }
        }

        private SceneObjectGroup GetGroupByPrim(uint localID)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
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
        /// Send the details of a prim's inventory to the client.
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
                        group.RequestInventoryFile(remoteClient, primLocalID, XferManager);
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
        public void RemoveTaskInventory(IClientAPI remoteClient, UUID itemID, uint localID)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            SceneObjectGroup group = part.ParentGroup;
            if (group != null)
            {
                TaskInventoryItem item = group.GetInventoryItem(localID, itemID);
                if (item == null)
                    return;

                if (item.Type == 10)
                {
                    EventManager.TriggerRemoveScript(localID, itemID);
                }
                group.RemoveInventoryItem(localID, itemID);
                part.GetProperties(remoteClient);
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

        private InventoryItemBase CreateAgentInventoryItemFromTask(UUID destAgent, SceneObjectPart part, UUID itemId)
        {
            TaskInventoryItem taskItem = part.GetInventoryItem(itemId);

            if (null == taskItem)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for creating an avatar"
                        + " inventory item from a prim's inventory item "
                        + " but the required item does not exist in the prim's inventory",
                    itemId, part.Name, part.UUID);

                return null;
            }

            InventoryItemBase agentItem = new InventoryItemBase();

            agentItem.ID = UUID.Random();
            agentItem.Creator = taskItem.CreatorID;
            agentItem.Owner = destAgent;
            agentItem.AssetID = taskItem.AssetID;
            agentItem.Description = taskItem.Description;
            agentItem.Name = taskItem.Name;
            agentItem.AssetType = taskItem.Type;
            agentItem.InvType = taskItem.InvType;
            agentItem.Flags = taskItem.Flags;

            if ((destAgent != taskItem.OwnerID) && ExternalChecks.ExternalChecksPropagatePermissions())
            {
                agentItem.BasePermissions = taskItem.NextPermissions;
                agentItem.CurrentPermissions = taskItem.NextPermissions;
                agentItem.NextPermissions = taskItem.NextPermissions;
                agentItem.EveryOnePermissions = taskItem.EveryonePermissions & taskItem.NextPermissions;
            }
            else
            {
                agentItem.BasePermissions = taskItem.BasePermissions;
                agentItem.CurrentPermissions = taskItem.CurrentPermissions;
                agentItem.NextPermissions = taskItem.NextPermissions;
                agentItem.EveryOnePermissions = taskItem.EveryonePermissions;
            }

            if (!ExternalChecks.ExternalChecksBypassPermissions())
            {
                if ((taskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    part.RemoveInventoryItem(itemId);
            }

            return agentItem;
        }

        /// <summary>
        /// Move the given item in the given prim to a folder in the client's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="part"></param>
        /// <param name="itemID"></param>
        public void MoveTaskInventoryItem(IClientAPI remoteClient, UUID folderId, SceneObjectPart part, UUID itemId)
        {

            InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(remoteClient.AgentId, part, itemId);

            agentItem.Folder = folderId;
            AddInventoryItem(remoteClient, agentItem);
        }

        /// <summary>
        /// <see>ClientMoveTaskInventoryItem</see>
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="primLocalID"></param>
        /// <param name="itemID"></param>
        public void ClientMoveTaskInventoryItem(IClientAPI remoteClient, UUID folderId, uint primLocalId, UUID itemId)
        {
            SceneObjectPart part = GetSceneObjectPart(primLocalId);

            if (null == part)
            {
                m_log.WarnFormat(
                    "[PRIM INVENTORY]: " +
                    "Move of inventory item {0} from prim with local id {1} failed because the prim could not be found",
                    itemId, primLocalId);

                return;
            }

            TaskInventoryItem taskItem = part.GetInventoryItem(itemId);

            if (null == taskItem)
            {
                m_log.WarnFormat("[PRIM INVENTORY]: Move of inventory item {0} from prim with local id {1} failed"
                    + " because the inventory item could not be found",
                    itemId, primLocalId);

                return;
            }

            // Only owner can copy
            if (remoteClient.AgentId != taskItem.OwnerID)
                return;

            MoveTaskInventoryItem(remoteClient, folderId, part, itemId);
        }

        /// <summary>
        /// <see>MoveTaskInventoryItem</see>
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="part"></param>
        /// <param name="itemID"></param>
        public void MoveTaskInventoryItem(UUID avatarId, UUID folderId, SceneObjectPart part, UUID itemId)
        {
            ScenePresence avatar;

            if (TryGetAvatar(avatarId, out avatar))
            {
                MoveTaskInventoryItem(avatar.ControllingClient, folderId, part, itemId);
            }
            else
            {
                CachedUserInfo profile = CommsManager.UserProfileCacheService.GetUserDetails(avatarId);
                if (profile == null || profile.RootFolder == null)
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Avatar {0} cannot be found to add item",
                        avatarId);
                }
                InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(avatarId, part, itemId);
                agentItem.Folder = folderId;

                AddInventoryItem(avatarId, agentItem);
            }
        }

        /// <summary>
        /// Copy a task (prim) inventory item to another task (prim)
        /// </summary>
        /// <param name="destId"></param>
        /// <param name="part"></param>
        /// <param name="itemId"></param>
        public void MoveTaskInventoryItem(UUID destId, SceneObjectPart part, UUID itemId)
        {
            TaskInventoryItem srcTaskItem = part.GetInventoryItem(itemId);

            if (srcTaskItem == null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for moving"
                        + " but the item does not exist in this inventory",
                    itemId, part.Name, part.UUID);

                return;
            }

            SceneObjectPart destPart = GetSceneObjectPart(destId);

            if (destPart == null)
            {
                m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Could not find prim for ID {0}",
                        destId);
                return;
            }

            if (part.OwnerID != destPart.OwnerID && (part.GetEffectiveObjectFlags() & (uint)PrimFlags.AllowInventoryDrop) == 0)
            {
                // object cannot copy items to an object owned by a different owner
                // unless llAllowInventoryDrop has been called

                return;
            }

            // must have both move and modify permission to put an item in an object
            if ((part.OwnerMask & ((uint)PermissionMask.Move | (uint)PermissionMask.Modify)) == 0)
            {
                return;
            }

            TaskInventoryItem destTaskItem = new TaskInventoryItem();

            destTaskItem.ItemID = UUID.Random();
            destTaskItem.CreatorID = srcTaskItem.CreatorID;
            destTaskItem.AssetID = srcTaskItem.AssetID;
            destTaskItem.GroupID = destPart.GroupID;
            destTaskItem.OwnerID = destPart.OwnerID;
            destTaskItem.ParentID = destPart.UUID;
            destTaskItem.ParentPartID = destPart.UUID;

            destTaskItem.BasePermissions = srcTaskItem.BasePermissions;
            destTaskItem.EveryonePermissions = srcTaskItem.EveryonePermissions;
            destTaskItem.GroupPermissions = srcTaskItem.GroupPermissions;
            destTaskItem.CurrentPermissions = srcTaskItem.CurrentPermissions;
            destTaskItem.NextPermissions = srcTaskItem.NextPermissions;
            destTaskItem.Flags = srcTaskItem.Flags;

            if (destPart.OwnerID != part.OwnerID)
            {
                if (ExternalChecks.ExternalChecksPropagatePermissions())
                {
                    destTaskItem.CurrentPermissions = srcTaskItem.CurrentPermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.GroupPermissions = srcTaskItem.GroupPermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.EveryonePermissions = srcTaskItem.EveryonePermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.BasePermissions = srcTaskItem.BasePermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.CurrentPermissions |= 8; // Slam!
                }
            }

            destTaskItem.Description = srcTaskItem.Description;
            destTaskItem.Name = srcTaskItem.Name;
            destTaskItem.InvType = srcTaskItem.InvType;
            destTaskItem.Type = srcTaskItem.Type;

            destPart.AddInventoryItem(destTaskItem);

            if ((srcTaskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                part.RemoveInventoryItem(itemId);

            ScenePresence avatar;

            if (TryGetAvatar(srcTaskItem.OwnerID, out avatar))
            {
                destPart.GetProperties(avatar.ControllingClient);
            }
        }

        public void MoveTaskInventoryItems(UUID destID, string category, SceneObjectPart host, List<UUID> items)
        {
            CachedUserInfo profile = CommsManager.UserProfileCacheService.GetUserDetails(destID);
            if (profile == null || profile.RootFolder == null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Avatar {0} cannot be found to add items",
                    destID);
                return;
            }

            UUID newFolderID = UUID.Random();

            profile.CreateFolder(category, newFolderID, 0xffff, profile.RootFolder.ID);

            foreach (UUID itemID in items)
            {
                InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(destID, host, itemID);
                agentItem.Folder = newFolderID;

                AddInventoryItem(destID, agentItem);
            }

            ScenePresence avatar;

            if (TryGetAvatar(destID, out avatar))
            {
                profile.SendInventoryDecendents(avatar.ControllingClient,
                        profile.RootFolder.ID, true, false);
                profile.SendInventoryDecendents(avatar.ControllingClient,
                        newFolderID, false, true);
            }
        }

        /// <summary>
        /// Update an item in a prim (task) inventory.
        /// This method does not handle scripts, <see>RezScript(IClientAPI, UUID, unit)</see>
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID"></param>
        /// <param name="itemInfo"></param>
        /// <param name="primLocalID"></param>
        public void UpdateTaskInventory(IClientAPI remoteClient, UUID transactionID, TaskInventoryItem itemInfo,
                                        uint primLocalID)
        {
            UUID itemID = itemInfo.ItemID;

            // Find the prim we're dealing with
            SceneObjectPart part = GetSceneObjectPart(primLocalID);

            if (part != null)
            {
                if (!ExternalChecks.ExternalChecksCanEditObjectInventory(part.UUID, remoteClient.AgentId))
                {
                    return;
                }

                TaskInventoryItem currentItem = part.GetInventoryItem(itemID);

                if (currentItem == null)
                {
                    UUID copyID = UUID.Random();
                    if (itemID != UUID.Zero)
                    {
                        CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

                        if (userInfo != null && userInfo.RootFolder != null)
                        {
                            InventoryItemBase item = userInfo.RootFolder.FindItem(itemID);

                            // Try library
                            // XXX clumsy, possibly should be one call
                            if (null == item)
                            {
                                item = CommsManager.UserProfileCacheService.LibraryRoot.FindItem(itemID);
                            }

                            if (item != null)
                            {
                                part.ParentGroup.AddInventoryItem(remoteClient, primLocalID, item, copyID);
                                m_log.InfoFormat(
                                    "[PRIM INVENTORY]: Update with item {0} requested of prim {1} for {2}",
                                    item.Name, primLocalID, remoteClient.Name);
                                part.GetProperties(remoteClient);
                                if (!ExternalChecks.ExternalChecksBypassPermissions())
                                {
                                    if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                                        RemoveInventoryItem(remoteClient, itemID);
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
                else // Updating existing item with new perms etc
                {                    
                    IAgentAssetTransactions agentTransactions = this.RequestModuleInterface<IAgentAssetTransactions>();
                    if (agentTransactions != null)
                    {
                        agentTransactions.HandleTaskItemUpdateFromTransaction(
                            remoteClient, part, transactionID, currentItem);
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
        public void RezScript(IClientAPI remoteClient, InventoryItemBase itemBase, UUID transactionID, uint localID)
        {
            UUID itemID=itemBase.ID;
            UUID copyID = UUID.Random();

            if (itemID != UUID.Zero)
            {
                CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);

                if (userInfo != null && userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.FindItem(itemID);

                    // Try library
                    // XXX clumsy, possibly should be one call
                    if (null == item)
                    {
                        item = CommsManager.UserProfileCacheService.LibraryRoot.FindItem(itemID);
                    }

                    if (item != null)
                    {
                        SceneObjectPart part = GetSceneObjectPart(localID);
                        if (part != null)
                        {
                            if (!ExternalChecks.ExternalChecksCanEditObjectInventory(part.UUID, remoteClient.AgentId))
                            {
                                return;
                            }

                            part.ParentGroup.AddInventoryItem(remoteClient, localID, item, copyID);
                            // TODO: set this to "true" when scripts in inventory have persistent state to fire on_rez
                            part.CreateScriptInstance(copyID, 0, false, DefaultScriptEngine);

                            //                        m_log.InfoFormat("[PRIMINVENTORY]: " +
                            //                                         "Rezzed script {0} into prim local ID {1} for user {2}",
                            //                                         item.inventoryName, localID, remoteClient.Name);
                            part.GetProperties(remoteClient);
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[PRIM INVENTORY]: " +
                                "Could not rez script {0} into prim local ID {1} for user {2}"
                                + " because the prim could not be found in the region!",
                                item.Name, localID, remoteClient.Name);
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
                SceneObjectPart part=GetSceneObjectPart(itemBase.Folder);
                if (part == null)
                    return;

                if (part.OwnerID != remoteClient.AgentId)
                    return;

                if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return;

                AssetBase asset = CreateAsset(itemBase.Name, itemBase.Description, (sbyte)itemBase.AssetType, Encoding.ASCII.GetBytes("default\n{\n    state_entry()\n    {\n        llSay(0, \"Script running\");\n    }\n}"));
                AssetCache.AddAsset(asset);

                TaskInventoryItem taskItem=new TaskInventoryItem();

                taskItem.ResetIDs(itemBase.Folder);
                taskItem.ParentID = itemBase.Folder;
                taskItem.CreationDate = (uint)itemBase.CreationDate;
                taskItem.Name = itemBase.Name;
                taskItem.Description = itemBase.Description;
                taskItem.Type = itemBase.AssetType;
                taskItem.InvType = itemBase.InvType;
                taskItem.OwnerID = itemBase.Owner;
                taskItem.CreatorID = itemBase.Creator;
                taskItem.BasePermissions = itemBase.BasePermissions;
                taskItem.CurrentPermissions = itemBase.CurrentPermissions;
                taskItem.EveryonePermissions = itemBase.EveryOnePermissions;
                taskItem.NextPermissions = itemBase.NextPermissions;
                taskItem.GroupID = itemBase.GroupID;
                taskItem.GroupPermissions = 0;
                taskItem.Flags = itemBase.Flags;
                taskItem.PermsGranter = UUID.Zero;
                taskItem.PermsMask = 0;
                taskItem.AssetID = asset.ID;

                part.AddInventoryItem(taskItem);
                part.GetProperties(remoteClient);

                part.CreateScriptInstance(taskItem, 0, false, DefaultScriptEngine);
            }
        }

        /// <summary>
        /// Rez a script into a prim's inventory from another prim
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"> </param>
        /// <param name="localID"></param>
        public void RezScript(UUID srcId, SceneObjectPart srcPart, UUID destId, int pin, int running, int start_param)
        {
            TaskInventoryItem srcTaskItem = srcPart.GetInventoryItem(srcId);

            if (srcTaskItem == null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for rezzing a script but the "
                        + " item does not exist in this inventory",
                    srcId, srcPart.Name, srcPart.UUID);

                return;
            }

            SceneObjectPart destPart = GetSceneObjectPart(destId);

            if (destPart == null)
            {
                m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Could not find script for ID {0}",
                        destId);
                return;
            }

            // Must own the object, and have modify rights
            if (srcPart.OwnerID != destPart.OwnerID)
                return;

            if ((destPart.OwnerMask & (uint)PermissionMask.Modify) == 0)
                return;

            if (destPart.ScriptAccessPin != pin)
            {
                m_log.WarnFormat(
                        "[PRIM INVENTORY]: " +
                        "Script in object {0} : {1}, attempted to load script {2} : {3} into object {4} : {5} with invalid pin {6}",
                        srcPart.Name, srcId, srcTaskItem.Name, srcTaskItem.ItemID, destPart.Name, destId, pin);
                // the LSL Wiki says we are supposed to shout on the DEBUG_CHANNEL -
                //   "Object: Task Object trying to illegally load script onto task Other_Object!"
                // How do we shout from in here?
                return;
            }

            TaskInventoryItem destTaskItem = new TaskInventoryItem();

            destTaskItem.ItemID = UUID.Random();
            destTaskItem.CreatorID = srcTaskItem.CreatorID;
            destTaskItem.AssetID = srcTaskItem.AssetID;
            destTaskItem.GroupID = destPart.GroupID;
            destTaskItem.OwnerID = destPart.OwnerID;
            destTaskItem.ParentID = destPart.UUID;
            destTaskItem.ParentPartID = destPart.UUID;

            destTaskItem.BasePermissions = srcTaskItem.BasePermissions;
            destTaskItem.EveryonePermissions = srcTaskItem.EveryonePermissions;
            destTaskItem.GroupPermissions = srcTaskItem.GroupPermissions;
            destTaskItem.CurrentPermissions = srcTaskItem.CurrentPermissions;
            destTaskItem.NextPermissions = srcTaskItem.NextPermissions;
            destTaskItem.Flags = srcTaskItem.Flags;

            if (destPart.OwnerID != srcPart.OwnerID)
            {
                if (ExternalChecks.ExternalChecksPropagatePermissions())
                {
                    destTaskItem.CurrentPermissions = srcTaskItem.CurrentPermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.GroupPermissions = srcTaskItem.GroupPermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.EveryonePermissions = srcTaskItem.EveryonePermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.BasePermissions = srcTaskItem.BasePermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.CurrentPermissions |= 8; // Slam!
                }
            }

            destTaskItem.Description = srcTaskItem.Description;
            destTaskItem.Name = srcTaskItem.Name;
            destTaskItem.InvType = srcTaskItem.InvType;
            destTaskItem.Type = srcTaskItem.Type;

            destPart.AddInventoryItemExclusive(destTaskItem);

            if (running > 0)
            {
                destPart.CreateScriptInstance(destTaskItem, 0, false, DefaultScriptEngine);
            }

            ScenePresence avatar;

            if (TryGetAvatar(srcTaskItem.OwnerID, out avatar))
            {
                destPart.GetProperties(avatar.ControllingClient);
            }
        }

        /// <summary>
        /// Called when an object is removed from the environment into inventory.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="remoteClient"></param>
        public virtual void DeRezObject(Packet packet, IClientAPI remoteClient)
        {
            DeRezObjectPacket DeRezPacket = (DeRezObjectPacket) packet;

            UUID folderID = UUID.Zero;

            foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
            {
//                    m_log.DebugFormat(
//                        "[AGENT INVENTORY]: Received request to derez {0} into folder {1}",
//                        Data.ObjectLocalID, DeRezPacket.AgentBlock.DestinationID);

                EntityBase selectedEnt = null;
                //m_log.Info("[CLIENT]: LocalID:" + Data.ObjectLocalID.ToString());

                List<EntityBase> EntityList = GetEntities();

                foreach (EntityBase ent in EntityList)
                {
                    if (ent.LocalId == Data.ObjectLocalID)
                    {
                        selectedEnt = ent;
                        break;
                    }
                }
                if (selectedEnt != null)
                {
                    bool permissionToTake = false;
                    bool permissionToDelete = false;
                    if (DeRezPacket.AgentBlock.Destination == 1)// Take Copy
                    {
                        permissionToTake = ExternalChecks.ExternalChecksCanTakeCopyObject(((SceneObjectGroup)selectedEnt).UUID, remoteClient.AgentId);
                        permissionToDelete = false; //Just taking copy!

                    }
                    else if (DeRezPacket.AgentBlock.Destination == 4) //Take
                    {
                        // Take
                        permissionToTake = ExternalChecks.ExternalChecksCanTakeObject(((SceneObjectGroup)selectedEnt).UUID, remoteClient.AgentId);
                        permissionToDelete = permissionToTake; //If they can take, they can delete!
                    }

                    else if (DeRezPacket.AgentBlock.Destination == 6) //Delete
                    {
                        permissionToTake = ExternalChecks.ExternalChecksCanDeleteObject(((SceneObjectGroup)selectedEnt).UUID, remoteClient.AgentId);
                        permissionToDelete = ExternalChecks.ExternalChecksCanDeleteObject(((SceneObjectGroup)selectedEnt).UUID, remoteClient.AgentId);
                    }
                    else if (DeRezPacket.AgentBlock.Destination == 9) //Return
                    {
                        permissionToTake = ExternalChecks.ExternalChecksCanDeleteObject(((SceneObjectGroup)selectedEnt).UUID, remoteClient.AgentId);
                        permissionToDelete = ExternalChecks.ExternalChecksCanDeleteObject(((SceneObjectGroup)selectedEnt).UUID, remoteClient.AgentId);
                    }

                    SceneObjectGroup objectGroup = (SceneObjectGroup)selectedEnt;

                    if (permissionToTake)
                    {
                        if (m_inventoryTicker != null)
                        {
                            m_inventoryTicker.Stop();
                        }
                        else
                        {
                            m_inventoryTicker = new Timer(2000);
                            m_inventoryTicker.AutoReset = false;
                            m_inventoryTicker.Elapsed += InventoryRunDeleteTimer;
                        }

                        lock (m_inventoryDeletes)
                        {
                            DeleteToInventoryHolder dtis = new DeleteToInventoryHolder();
                            dtis.DeRezPacket = DeRezPacket;
                            dtis.folderID = folderID;
                            dtis.objectGroup = objectGroup;
                            dtis.remoteClient = remoteClient;
                            dtis.selectedEnt = selectedEnt;
                            dtis.permissionToDelete = permissionToDelete;

                            m_inventoryDeletes.Enqueue(dtis);
                        }

                        m_inventoryTicker.Start();

                        // Visually remove it, even if it isnt really gone yet.
                        if (permissionToDelete)
                            objectGroup.FakeDeleteGroup();
                    }
                    else if (permissionToDelete)
                    {
                        DeleteSceneObject(objectGroup);
                    }
                }
            }
        }

        void InventoryRunDeleteTimer(object sender, ElapsedEventArgs e)
        {
            m_log.Info("Starting inventory send loop");
            while (InventoryDeQueueAndDelete() == true)
            {
                m_log.Info("Returned item successfully, continuing...");
            }
        }

        private bool InventoryDeQueueAndDelete()
        {
            DeleteToInventoryHolder x = null;

            try
            {
                lock (m_inventoryDeletes)
                {
                    int left = m_inventoryDeletes.Count;
                    if (left > 0)
                    {
                        m_log.InfoFormat("Sending deleted object to user's inventory, {0} item(s) remaining.", left);
                        x = m_inventoryDeletes.Dequeue();
                        DeleteToInventory(x.DeRezPacket, x.selectedEnt, x.remoteClient, x.objectGroup, x.folderID, x.permissionToDelete);
                        return true;
                    }
                }
            }
            catch(Exception e)
            {
                // We can't put the object group details in here since the root part may have disappeared (which is where these sit).
                // FIXME: This needs to be fixed.
                m_log.ErrorFormat(
                    "[AGENT INVENTORY]: Queued deletion of scene object to agent {0} {1} failed: {2}",
                    (x != null ? x.remoteClient.Name : "unavailable"), (x != null ? x.remoteClient.AgentId : "unavailable"), e.ToString());
            }

            m_log.Info("No objects left in inventory delete queue.");
            return false;
        }

        private void DeleteToInventory(DeRezObjectPacket DeRezPacket, EntityBase selectedEnt, IClientAPI remoteClient, SceneObjectGroup objectGroup, UUID folderID, bool permissionToDelete)
        {
            string sceneObjectXml = objectGroup.ToXmlString();

            CachedUserInfo userInfo =
                CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
//                            string searchFolder = "";

//                            if (DeRezPacket.AgentBlock.Destination == 6)
//                                searchFolder = "Trash";
//                            else if (DeRezPacket.AgentBlock.Destination == 9)
//                                searchFolder = "Lost And Found";

                // If we're deleting someone else's item, it goes back to their deleted items folder
                // If we're returning someone's item, it goes back to the owner's Lost And Found folder.

                if (DeRezPacket.AgentBlock.DestinationID == UUID.Zero || (DeRezPacket.AgentBlock.Destination == 6 && objectGroup.OwnerID != remoteClient.AgentId))
                {
                    List<InventoryFolderBase> subrootfolders = userInfo.RootFolder.RequestListOfFolders();
                    foreach (InventoryFolderBase flder in subrootfolders)
                    {
                        if (flder.Name == "Lost And Found")
                        {
                            folderID = flder.ID;
                            break;
                        }
                    }

                    if (folderID == UUID.Zero)
                    {
                        folderID = userInfo.RootFolder.ID;
                    }
                    //currently following code not used (or don't know of any case of destination being zero
                }
                else
                {
                    folderID = DeRezPacket.AgentBlock.DestinationID;
                }

                AssetBase asset = CreateAsset(
                    ((SceneObjectGroup) selectedEnt).GetPartName(selectedEnt.LocalId),
                    ((SceneObjectGroup) selectedEnt).GetPartDescription(selectedEnt.LocalId),
                    (sbyte)AssetType.Object,
                    Utils.StringToBytes(sceneObjectXml));
                AssetCache.AddAsset(asset);

                InventoryItemBase item = new InventoryItemBase();
                item.Creator = objectGroup.RootPart.CreatorID;

                if (DeRezPacket.AgentBlock.Destination == 1 || DeRezPacket.AgentBlock.Destination == 4)// Take / Copy
                    item.Owner = remoteClient.AgentId;
                else // Delete / Return
                    item.Owner = objectGroup.OwnerID;

                item.ID = UUID.Random();
                item.AssetID = asset.FullID;
                item.Description = asset.Description;
                item.Name = asset.Name;
                item.AssetType = asset.Type;
                item.InvType = (int)InventoryType.Object;
                item.Folder = folderID;
                if ((remoteClient.AgentId != objectGroup.RootPart.OwnerID) && ExternalChecks.ExternalChecksPropagatePermissions())
                {
                    uint perms=objectGroup.GetEffectivePermissions();
                    uint nextPerms=(perms & 7) << 13;
                    if ((nextPerms & (uint)PermissionMask.Copy) == 0)
                        perms &= ~(uint)PermissionMask.Copy;
                    if ((nextPerms & (uint)PermissionMask.Transfer) == 0)
                        perms &= ~(uint)PermissionMask.Transfer;
                    if ((nextPerms & (uint)PermissionMask.Modify) == 0)
                        perms &= ~(uint)PermissionMask.Modify;

                    item.BasePermissions = perms & objectGroup.RootPart.NextOwnerMask;
                    item.CurrentPermissions = item.BasePermissions;
                    item.NextPermissions = objectGroup.RootPart.NextOwnerMask;
                    item.EveryOnePermissions = objectGroup.RootPart.EveryoneMask & objectGroup.RootPart.NextOwnerMask;
                    item.CurrentPermissions |= 8; // Slam!
                }
                else
                {
                    item.BasePermissions = objectGroup.GetEffectivePermissions();
                    item.CurrentPermissions = objectGroup.GetEffectivePermissions();
                    item.NextPermissions = objectGroup.RootPart.NextOwnerMask;
                    item.EveryOnePermissions = objectGroup.RootPart.EveryoneMask;
                }

                // TODO: add the new fields (Flags, Sale info, etc)
                item.CreationDate = Util.UnixTimeSinceEpoch();

                userInfo.AddItem(item);
                if (item.Owner == remoteClient.AgentId)
                {
                    remoteClient.SendInventoryItemCreateUpdate(item);
                }
                else
                {
                    ScenePresence notifyUser = GetScenePresence(item.Owner);
                    if (notifyUser != null)
                    {
                        notifyUser.ControllingClient.SendInventoryItemCreateUpdate(item);
                    }
                }
            }

            // Finally remove the item, for reals this time.
            if (permissionToDelete)
                DeleteSceneObject(objectGroup);
        }

        public void updateKnownAsset(IClientAPI remoteClient, SceneObjectGroup grp, UUID assetID, UUID agentID)
        {
            SceneObjectGroup objectGroup = grp;
            if (objectGroup != null)
            {
                string sceneObjectXml = objectGroup.ToXmlString();

                CachedUserInfo userInfo =
                    CommsManager.UserProfileCacheService.GetUserDetails(agentID);
                if (userInfo != null && userInfo.RootFolder != null)
                {
                    Queue<InventoryFolderImpl> searchfolders = new Queue<InventoryFolderImpl>();
                    searchfolders.Enqueue(userInfo.RootFolder);

                    UUID foundFolder = userInfo.RootFolder.ID;

                    // search through folders to find the asset.
                    while (searchfolders.Count > 0)
                    {
                        InventoryFolderImpl fld = searchfolders.Dequeue();
                        lock (fld)
                        {
                            if (fld != null)
                            {
                                if (fld.Items.ContainsKey(assetID))
                                {
                                    foundFolder = fld.ID;
                                    searchfolders.Clear();
                                    break;
                                }
                                else
                                {
                                    foreach (InventoryFolderImpl subfld in fld.SubFolders.Values)
                                    {
                                        searchfolders.Enqueue(subfld);
                                    }
                                }
                            }
                        }
                    }

                    AssetBase asset = CreateAsset(
                        objectGroup.GetPartName(objectGroup.LocalId),
                        objectGroup.GetPartDescription(objectGroup.LocalId),
                        (sbyte)AssetType.Object,
                        Utils.StringToBytes(sceneObjectXml));
                    AssetCache.AddAsset(asset);

                    InventoryItemBase item = new InventoryItemBase();
                    item.Creator = objectGroup.RootPart.CreatorID;
                    item.Owner = agentID;
                    item.ID = assetID;
                    item.AssetID = asset.FullID;
                    item.Description = asset.Description;
                    item.Name = asset.Name;
                    item.AssetType = asset.Type;
                    item.InvType = (int)InventoryType.Object;

                    // Sticking it in root folder for now..    objects folder later?

                    item.Folder = foundFolder;// DeRezPacket.AgentBlock.DestinationID;
                    if ((agentID != objectGroup.RootPart.OwnerID) && ExternalChecks.ExternalChecksPropagatePermissions())
                    {
                        item.BasePermissions = objectGroup.RootPart.NextOwnerMask;
                        item.CurrentPermissions = objectGroup.RootPart.NextOwnerMask;
                        item.NextPermissions = objectGroup.RootPart.NextOwnerMask;
                        item.EveryOnePermissions = objectGroup.RootPart.EveryoneMask & objectGroup.RootPart.NextOwnerMask;
                    }
                    else
                    {
                        item.BasePermissions = objectGroup.GetEffectivePermissions();
                        item.CurrentPermissions = objectGroup.GetEffectivePermissions();
                        item.NextPermissions = objectGroup.RootPart.NextOwnerMask;
                        item.EveryOnePermissions = objectGroup.RootPart.EveryoneMask;
                    }

                    userInfo.AddItem(item);

                    // this gets called when the agent loggs off!
                    if (remoteClient != null)
                    {
                        remoteClient.SendInventoryItemCreateUpdate(item);
                    }

                }
            }
        }

        public UUID attachObjectAssetStore(IClientAPI remoteClient, SceneObjectGroup grp, UUID AgentId)
        {
            SceneObjectGroup objectGroup = grp;
            if (objectGroup != null)
            {
                string sceneObjectXml = objectGroup.ToXmlString();

                CachedUserInfo userInfo =
                    CommsManager.UserProfileCacheService.GetUserDetails(AgentId);
                if (userInfo != null)
                {
                    AssetBase asset = CreateAsset(
                        objectGroup.GetPartName(objectGroup.LocalId),
                        objectGroup.GetPartDescription(objectGroup.LocalId),
                        (sbyte)AssetType.Object,
                        Utils.StringToBytes(sceneObjectXml));
                    AssetCache.AddAsset(asset);

                    InventoryItemBase item = new InventoryItemBase();
                    item.Creator = objectGroup.RootPart.CreatorID;
                    item.Owner = remoteClient.AgentId;
                    item.ID = UUID.Random();
                    item.AssetID = asset.FullID;
                    item.Description = asset.Description;
                    item.Name = asset.Name;
                    item.AssetType = asset.Type;
                    item.InvType = (int)InventoryType.Object;

                    // Sticking it in root folder for now..    objects folder later?

                    item.Folder = userInfo.RootFolder.ID;// DeRezPacket.AgentBlock.DestinationID;
                    if ((remoteClient.AgentId != objectGroup.RootPart.OwnerID) && ExternalChecks.ExternalChecksPropagatePermissions())
                    {
                        item.BasePermissions = objectGroup.RootPart.NextOwnerMask;
                        item.CurrentPermissions = objectGroup.RootPart.NextOwnerMask;
                        item.NextPermissions = objectGroup.RootPart.NextOwnerMask;
                        item.EveryOnePermissions = objectGroup.RootPart.EveryoneMask & objectGroup.RootPart.NextOwnerMask;
                    }
                    else
                    {
                        item.BasePermissions = objectGroup.RootPart.BaseMask;
                        item.CurrentPermissions = objectGroup.RootPart.OwnerMask;
                        item.NextPermissions = objectGroup.RootPart.NextOwnerMask;
                        item.EveryOnePermissions = objectGroup.RootPart.EveryoneMask;
                    }
                    item.CreationDate = Util.UnixTimeSinceEpoch();

                    userInfo.AddItem(item);
                    remoteClient.SendInventoryItemCreateUpdate(item);
                    return item.AssetID;
                }
                return UUID.Zero;
            }
            return UUID.Zero;

        }

        /// <summary>
        /// Event Handler Rez an object into a scene
        /// Calls the non-void event handler
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="RayEnd"></param>
        /// <param name="RayStart"></param>
        /// <param name="RayTargetID"></param>
        /// <param name="BypassRayCast"></param>
        /// <param name="RayEndIsIntersection"></param>
        /// <param name="EveryoneMask"></param>
        /// <param name="GroupMask"></param>
        /// <param name="NextOwnerMask"></param>
        /// <param name="ItemFlags"></param>
        /// <param name="RezSelected"></param>
        /// <param name="RemoveItem"></param>
        /// <param name="fromTaskID"></param>
        public virtual void RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                    UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    uint EveryoneMask, uint GroupMask, uint NextOwnerMask, uint ItemFlags,
                                    bool RezSelected, bool RemoveItem, UUID fromTaskID)
        {
            RezObject(
                remoteClient, itemID, RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection,
                EveryoneMask, GroupMask, NextOwnerMask, ItemFlags, RezSelected, RemoveItem, fromTaskID, false);
        }

       /// <summary>
       /// Returns SceneObjectGroup or null from asset request.
       /// </summary>
       /// <param name="remoteClient"></param>
       /// <param name="itemID"></param>
       /// <param name="RayEnd"></param>
       /// <param name="RayStart"></param>
       /// <param name="RayTargetID"></param>
       /// <param name="BypassRayCast"></param>
       /// <param name="RayEndIsIntersection"></param>
       /// <param name="EveryoneMask"></param>
       /// <param name="GroupMask"></param>
       /// <param name="NextOwnerMask"></param>
       /// <param name="ItemFlags"></param>
       /// <param name="RezSelected"></param>
       /// <param name="RemoveItem"></param>
       /// <param name="fromTaskID"></param>
       /// <param name="difference"></param>
       /// <returns></returns>
        public virtual SceneObjectGroup RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                    UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    uint EveryoneMask, uint GroupMask, uint NextOwnerMask, uint ItemFlags,
                                    bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            // Work out position details
            byte bRayEndIsIntersection = (byte)0;

            if (RayEndIsIntersection)
            {
                bRayEndIsIntersection = (byte)1;
            }
            else
            {
                bRayEndIsIntersection = (byte)0;
            }

            Vector3 scale = new Vector3(0.5f, 0.5f, 0.5f);


            Vector3 pos = GetNewRezLocation(
                      RayStart, RayEnd, RayTargetID, Quaternion.Identity,
                      BypassRayCast, bRayEndIsIntersection,true,scale, false);

            // Rez object
            CachedUserInfo userInfo = CommsManager.UserProfileCacheService.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.FindItem(itemID);

                    if (item != null)
                    {
                        AssetBase rezAsset = AssetCache.GetAsset(item.AssetID, false);

                        if (rezAsset != null)
                        {
                            string xmlData = Utils.BytesToString(rezAsset.Data);
                            SceneObjectGroup group = new SceneObjectGroup(this, m_regionHandle, xmlData);
                            if (!ExternalChecks.ExternalChecksCanRezObject(group.Children.Count, remoteClient.AgentId, pos) && !attachment)
                            {
                                return null;
                            }

                            group.ResetIDs();

                            AddNewSceneObject(group, true);

                            // if attachment we set it's asset id so object updates can reflect that
                            // if not, we set it's position in world.
                            if (!attachment)
                            {
                                pos = GetNewRezLocation(
                                    RayStart, RayEnd, RayTargetID, Quaternion.Identity,
                                    BypassRayCast, bRayEndIsIntersection, true, group.GroupScale(), false);
                                group.AbsolutePosition = pos;
                            }
                            else
                            {
                                group.SetFromAssetID(itemID);
                            }

                            SceneObjectPart rootPart = null;
                            try
                            {
                                rootPart = group.GetChildPart(group.UUID);
                            }
                            catch (NullReferenceException)
                            {
                                string isAttachment = "";

                                if (attachment)
                                    isAttachment = " Object was an attachment";

                                m_log.Error("[OJECTREZ]: Error rezzing ItemID: " + itemID + " object has no rootpart." + isAttachment);
                            }

                            // Since renaming the item in the inventory does not affect the name stored
                            // in the serialization, transfer the correct name from the inventory to the
                            // object itself before we rez.
                            rootPart.Name = item.Name;
                            rootPart.Description = item.Description;

                            List<SceneObjectPart> partList = new List<SceneObjectPart>(group.Children.Values);

                            if (rootPart.OwnerID != item.Owner)
                            {
                                //Need to kill the for sale here
                                rootPart.ObjectSaleType = 0;
                                rootPart.SalePrice = 10;

                                if (ExternalChecks.ExternalChecksPropagatePermissions())
                                {
                                    if ((item.CurrentPermissions & 8) != 0)
                                    {
                                        foreach (SceneObjectPart part in partList)
                                        {
                                            part.EveryoneMask = item.EveryOnePermissions;
                                            part.NextOwnerMask = item.NextPermissions;
                                        }
                                    }
                                    group.ApplyNextOwnerPermissions();
                                }
                            }

                            foreach (SceneObjectPart part in partList)
                            {
                                if (part.OwnerID != item.Owner)
                                {
                                    part.LastOwnerID = part.OwnerID;
                                    part.OwnerID = item.Owner;
                                    part.ChangeInventoryOwner(item.Owner);
                                }
                                else if (((item.CurrentPermissions & 8) != 0) && (!attachment)) // Slam!
                                {
                                    part.EveryoneMask = item.EveryOnePermissions;
                                    part.NextOwnerMask = item.NextPermissions;
                                }
                            }

                            rootPart.TrimPermissions();

                            if (!attachment)
                            {
                                if (group.RootPart.Shape.PCode == (byte)PCode.Prim)
                                {
                                    group.ClearPartAttachmentData();
                                }
                                // Ghost prim if this is enabled!
                                //group.ApplyPhysics(m_physicalPrim);
                            }

                            // TODO: make this true to fire on_rez when scripts have state while in inventory
                            group.CreateScriptInstances(0, false, DefaultScriptEngine);

                            if (!attachment)
                                rootPart.ScheduleFullUpdate();

                            if (!ExternalChecks.ExternalChecksBypassPermissions())
                            {
                                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                                    userInfo.DeleteItem(item.ID);
                            }

                            return rootPart.ParentGroup;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Rez an object in the scene
        /// </summary>
        /// <param name="item"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="vel"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public virtual SceneObjectGroup RezObject(
            SceneObjectPart sourcePart, TaskInventoryItem item,
            Vector3 pos, Quaternion rot, Vector3 vel, int param)
        {
            // Rez object
            if (item != null)
            {
                UUID ownerID = item.OwnerID;

                AssetBase rezAsset = AssetCache.GetAsset(item.AssetID, false);

                if (rezAsset != null)
                {
                    string xmlData = Utils.BytesToString(rezAsset.Data);
                    SceneObjectGroup group = new SceneObjectGroup(this, m_regionHandle, xmlData);

                    if (!ExternalChecks.ExternalChecksCanRezObject(group.Children.Count, ownerID, pos))
                    {
                        return null;
                    }
                    group.ResetIDs();

                    AddNewSceneObject(group, true);

                    // we set it's position in world.
                    group.AbsolutePosition = pos;

                    SceneObjectPart rootPart = group.GetChildPart(group.UUID);

                    // Since renaming the item in the inventory does not affect the name stored
                    // in the serialization, transfer the correct name from the inventory to the
                    // object itself before we rez.
                    rootPart.Name = item.Name;
                    rootPart.Description = item.Description;

                    List<SceneObjectPart> partList = new List<SceneObjectPart>(group.Children.Values);

                    if (rootPart.OwnerID != item.OwnerID)
                    {
                        if (ExternalChecks.ExternalChecksPropagatePermissions())
                        {
                            if ((item.CurrentPermissions & 8) != 0)
                            {
                                foreach (SceneObjectPart part in partList)
                                {
                                    part.EveryoneMask = item.EveryonePermissions;
                                    part.NextOwnerMask = item.NextPermissions;
                                }
                            }
                            group.ApplyNextOwnerPermissions();
                        }
                    }

                    foreach (SceneObjectPart part in partList)
                    {
                        if (part.OwnerID != item.OwnerID)
                        {
                            part.LastOwnerID = part.OwnerID;
                            part.OwnerID = item.OwnerID;
                            part.ChangeInventoryOwner(item.OwnerID);
                        }
                        else if ((item.CurrentPermissions & 8) != 0) // Slam!
                        {
                            part.EveryoneMask = item.EveryonePermissions;
                            part.NextOwnerMask = item.NextPermissions;
                        }
                    }
                    rootPart.TrimPermissions();
                    if (group.RootPart.Shape.PCode == (byte)PCode.Prim)
                    {
                        group.ClearPartAttachmentData();
                    }
                    group.UpdateGroupRotation(rot);
                    //group.ApplyPhysics(m_physicalPrim);
                    group.Velocity = vel;
                    group.CreateScriptInstances(param, true, DefaultScriptEngine);
                    rootPart.ScheduleFullUpdate();

                    if (!ExternalChecks.ExternalChecksBypassPermissions())
                    {
                        if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                            sourcePart.RemoveInventoryItem(item.ItemID);
                    }
                    return rootPart.ParentGroup;
                }
            }

            return null;
        }

        public virtual bool returnObjects(SceneObjectGroup[] returnobjects, UUID AgentId)
        {
            string message = "";
            if (returnobjects.Length <= 0)
                return false;

            // for the moment we're going to store them individually..   however, in the future, the rezObject
            // will be able to have more items.

            //string returnstring = "";
            //returnstring += "<scene>\n";
            //for (int i = 0; i < returnobjects.Length; i++)
            //{
            //    returnstring += grp.ToXmlString2();
            //}
            //returnstring += "</scene>\n";

            bool permissionToDelete = false;

            for (int i = 0; i < returnobjects.Length; i++)
            {
                CachedUserInfo userInfo =
                    CommsManager.UserProfileCacheService.GetUserDetails(returnobjects[i].OwnerID);
                if (userInfo == null)
                {
                    CommsManager.UserProfileCacheService.AddNewUser(returnobjects[i].OwnerID);

                }
                if (userInfo != null)
                {
                    if (userInfo.HasReceivedInventory)
                    {
                        UUID folderID = UUID.Zero;

                        List<InventoryFolderBase> subrootfolders = userInfo.RootFolder.RequestListOfFolders();
                        foreach (InventoryFolderBase flder in subrootfolders)
                        {
                            if (flder.Name == "Lost And Found")
                            {
                                folderID = flder.ID;
                                break;
                            }
                        }

                        if (folderID == UUID.Zero)
                        {
                            folderID = userInfo.RootFolder.ID;
                        }
                        permissionToDelete = ExternalChecks.ExternalChecksCanDeleteObject(returnobjects[i].UUID, AgentId);

                        // If the user doesn't have permission, go on to the next one.
                        if (!permissionToDelete)
                            continue;

                        string sceneObjectXml = returnobjects[i].ToXmlString2();
                        AssetBase asset = CreateAsset(
                            returnobjects[i].GetPartName(returnobjects[i].LocalId),
                            returnobjects[i].GetPartDescription(returnobjects[i].LocalId),
                            (sbyte)AssetType.Object,
                            Utils.StringToBytes(sceneObjectXml));
                        AssetCache.AddAsset(asset);

                        InventoryItemBase item = new InventoryItemBase();
                        item.Creator = returnobjects[i].RootPart.CreatorID;
                        item.Owner = returnobjects[i].OwnerID;
                        item.ID = UUID.Random();
                        item.AssetID = asset.FullID;
                        item.Description = asset.Description;
                        item.Name = asset.Name;
                        item.AssetType = asset.Type;
                        item.InvType = (int)InventoryType.Object;
                        item.Folder = folderID;

                        if ((AgentId != returnobjects[i].RootPart.OwnerID) && ExternalChecks.ExternalChecksPropagatePermissions())
                        {
                            uint perms = returnobjects[i].GetEffectivePermissions();
                            uint nextPerms = (perms & 7) << 13;
                            if ((nextPerms & (uint)PermissionMask.Copy) == 0)
                                perms &= ~(uint)PermissionMask.Copy;
                            if ((nextPerms & (uint)PermissionMask.Transfer) == 0)
                                perms &= ~(uint)PermissionMask.Transfer;
                            if ((nextPerms & (uint)PermissionMask.Modify) == 0)
                                perms &= ~(uint)PermissionMask.Modify;

                            item.BasePermissions = perms & returnobjects[i].RootPart.NextOwnerMask;
                            item.CurrentPermissions = item.BasePermissions;
                            item.NextPermissions = returnobjects[i].RootPart.NextOwnerMask;
                            item.EveryOnePermissions = returnobjects[i].RootPart.EveryoneMask & returnobjects[i].RootPart.NextOwnerMask;
                            item.CurrentPermissions |= 8; // Slam!
                        }
                        else
                        {
                            item.BasePermissions = returnobjects[i].GetEffectivePermissions();
                            item.CurrentPermissions = returnobjects[i].GetEffectivePermissions();
                            item.NextPermissions = returnobjects[i].RootPart.NextOwnerMask;
                            item.EveryOnePermissions = returnobjects[i].RootPart.EveryoneMask;
                        }

                        // TODO: add the new fields (Flags, Sale info, etc)

                        userInfo.AddItem(item);

                        ScenePresence notifyUser = GetScenePresence(item.Owner);
                        if (notifyUser != null)
                        {
                            notifyUser.ControllingClient.SendInventoryItemCreateUpdate(item);
                        }

                        SceneObjectGroup ObjectDeleting = returnobjects[i];

                        returnobjects[i] = null;

                        DeleteSceneObject(ObjectDeleting);
                        ObjectDeleting = null;
                    }
                    else
                    {
                        CommsManager.UserProfileCacheService.RequestInventoryForUser(returnobjects[i].OwnerID);
                        message = "Still waiting on the inventory service, some of the items won't be returned until the inventory services completes it's task.  Try again shortly.";
                    }
                }
                else
                {
                    message = "Still waiting on the inventory service, some of the items won't be returned until the inventory services completes it's task.  Try again shortly.";
                }
                //return true;
            }

            if (message.Length != 0)
            {
                ScenePresence returningavatar = GetScenePresence(AgentId);
                if (returningavatar != null)
                {
                    returningavatar.ControllingClient.SendAlertMessage(message);
                }
                return false;
            }
            return true;

        }

        public void SetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID, bool running)
        {
            SceneObjectPart part = GetSceneObjectPart(objectID);
            if (part == null)
                return;

            if (running)
                EventManager.TriggerStartScript(part.LocalId, itemID);
            else
                EventManager.TriggerStopScript(part.LocalId, itemID);
        }

        public void RezSingleAttachment(IClientAPI remoteClient, UUID itemID,
                uint AttachmentPt, uint ItemFlags, uint NextOwnerMask)
        {
            SceneObjectGroup att = m_innerScene.RezSingleAttachment(remoteClient, itemID, AttachmentPt, ItemFlags, NextOwnerMask);

            if (att == null)
            {
                DetachSingleAttachmentToInv(itemID, remoteClient);
                return;
            }

            RezSingleAttachment(att, remoteClient, itemID, AttachmentPt,
                    ItemFlags, NextOwnerMask);
        }

        public void RezSingleAttachment(SceneObjectGroup att,
                IClientAPI remoteClient, UUID itemID, uint AttachmentPt,
                uint ItemFlags, uint NextOwnerMask)
        {
            if (att.RootPart != null)
                AttachmentPt = att.RootPart.AttachmentPoint;

            ScenePresence presence;
            if (TryGetAvatar(remoteClient.AgentId, out presence))
            {
                presence.Appearance.SetAttachment((int)AttachmentPt, itemID, att.UUID);
                IAvatarFactory ava = RequestModuleInterface<IAvatarFactory>();
                if (ava != null)
                {
                    ava.UpdateDatabase(remoteClient.AgentId, presence.Appearance);
                }

            }
        }

        public void AttachObject(IClientAPI controllingClient, uint localID, uint attachPoint, Quaternion rot, Vector3 pos)
        {
            m_innerScene.AttachObject(controllingClient, localID, attachPoint, rot, pos);
        }

        public void DetachSingleAttachmentToInv(UUID itemID, IClientAPI remoteClient)
        {
            ScenePresence presence;
            if (TryGetAvatar(remoteClient.AgentId, out presence))
            {
                presence.Appearance.DetachAttachment(itemID);
                IAvatarFactory ava = RequestModuleInterface<IAvatarFactory>();
                if (ava != null)
                {
                    ava.UpdateDatabase(remoteClient.AgentId, presence.Appearance);
                }

            }
            m_innerScene.DetachSingleAttachmentToInv(itemID, remoteClient);
        }

        public void GetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID)
        {
            EventManager.TriggerGetScriptRunning(controllingClient, objectID, itemID);
        }
    }
}
