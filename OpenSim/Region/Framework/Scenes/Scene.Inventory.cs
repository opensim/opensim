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
using System.Collections;
using System.Reflection;
using System.Text;
using System.Timers;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace OpenSim.Region.Framework.Scenes
{
    public partial class Scene
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Allows asynchronous derezzing of objects from the scene into a client's inventory.
        /// </summary>
        protected AsyncSceneObjectGroupDeleter m_asyncSceneObjectDeleter;

        /// <summary>
        /// Allows inventory details to be sent to clients asynchronously
        /// </summary>
        protected AsyncInventorySender m_asyncInventorySender;

        /// <summary>
        /// Creates all the scripts in the scene which should be started.
        /// </summary>
        /// <returns>
        /// Number of scripts that were valid for starting.  This does not guarantee that all these scripts
        /// were actually started, but just that the start could be attempt (e.g. the asset data for the script could be found)
        /// </returns>
        public int CreateScriptInstances()
        {
            m_log.InfoFormat("[SCENE]: Initializing script instances in {0}", RegionInfo.RegionName);

            int scriptsValidForStarting = 0;

            EntityBase[] entities = Entities.GetEntities();
            foreach (EntityBase group in entities)
            {
                if (group is SceneObjectGroup)
                {
                    scriptsValidForStarting
                        += ((SceneObjectGroup) group).CreateScriptInstances(0, false, DefaultScriptEngine, 0);
                    ((SceneObjectGroup) group).ResumeScripts();
                }
            }

            m_log.InfoFormat(
                "[SCENE]: Initialized {0} script instances in {1}",
                scriptsValidForStarting, RegionInfo.RegionName);

            return scriptsValidForStarting;
        }

        /// <summary>
        /// Lets the script engines start processing scripts.
        /// </summary>
        public void StartScripts()
        {
//            m_log.InfoFormat("[SCENE]: Starting scripts in {0}, please wait.", RegionInfo.RegionName);

            IScriptModule[] engines = RequestModuleInterfaces<IScriptModule>();

            foreach (IScriptModule engine in engines)
                engine.StartProcessing();
        }

        public void AddUploadedInventoryItem(UUID agentID, InventoryItemBase item)
        {
            IMoneyModule money = RequestModuleInterface<IMoneyModule>();
            if (money != null)
            {
                money.ApplyUploadCharge(agentID, money.UploadCharge, "Asset upload");
            }

            AddInventoryItem(item);
        }

        public bool AddInventoryItemReturned(UUID AgentId, InventoryItemBase item)
        {
            if (AddInventoryItem(item))
                return true;
            else
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Unable to add item {1} to agent {2} inventory", item.Name, AgentId);

                return false;
            }
        }

        /// <summary>
        /// Add the given inventory item to a user's inventory.
        /// </summary>
        /// <param name="item"></param>
        public bool AddInventoryItem(InventoryItemBase item)
        {
            if (item.Folder != UUID.Zero && InventoryService.AddItem(item))
            {
                int userlevel = 0;
                if (Permissions.IsGod(item.Owner))
                {
                    userlevel = 1;
                }
                EventManager.TriggerOnNewInventoryItemUploadComplete(item.Owner, item.AssetID, item.Name, userlevel);

                return true;
            }

            // OK so either the viewer didn't send a folderID or AddItem failed
            UUID originalFolder = item.Folder;
            InventoryFolderBase f = InventoryService.GetFolderForType(item.Owner, (AssetType)item.AssetType);
            if (f != null)
            {
                m_log.DebugFormat(
                    "[AGENT INVENTORY]: Found folder {0} type {1} for item {2}",
                    f.Name, (AssetType)f.Type, item.Name);
                    
                item.Folder = f.ID;
            }
            else
            {
                f = InventoryService.GetRootFolder(item.Owner);
                if (f != null)
                {
                    item.Folder = f.ID;
                }
                else
                {
                    m_log.WarnFormat(
                        "[AGENT INVENTORY]: Could not find root folder for {0} when trying to add item {1} with no parent folder specified",
                        item.Owner, item.Name);
                    return false;
                }
            }
            
            if (InventoryService.AddItem(item))
            {
                int userlevel = 0;
                if (Permissions.IsGod(item.Owner))
                {
                    userlevel = 1;
                }
                EventManager.TriggerOnNewInventoryItemUploadComplete(item.Owner, item.AssetID, item.Name, userlevel);

                if (originalFolder != UUID.Zero)
                {
                    // Tell the viewer that the item didn't go there
                    ChangePlacement(item, f);
                }

                return true;
            }
            else
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Agent {0} could not add item {1} {2}",
                    item.Owner, item.Name, item.ID);

                return false;
            }
        }

        private void ChangePlacement(InventoryItemBase item, InventoryFolderBase f)
        {
            ScenePresence sp = GetScenePresence(item.Owner);
            if (sp != null)
            {
                if (sp.ControllingClient is IClientCore)
                {
                    IClientCore core = (IClientCore)sp.ControllingClient;
                    IClientInventory inv;

                    if (core.TryGet<IClientInventory>(out inv))
                    {
                        InventoryFolderBase parent = new InventoryFolderBase(f.ParentID, f.Owner);
                        parent = InventoryService.GetFolder(parent);
                        inv.SendRemoveInventoryItems(new UUID[] { item.ID });
                        inv.SendBulkUpdateInventory(new InventoryFolderBase[0], new InventoryItemBase[] { item });
                        string message = "The item was placed in folder " + f.Name;
                        if (parent != null)
                            message += " under " + parent.Name;
                        sp.ControllingClient.SendAgentAlertMessage(message, false);
                    }
                }
            }
        }

        /// <summary>
        /// Add the given inventory item to a user's inventory.
        /// </summary>
        /// <param name="AgentID">
        /// A <see cref="UUID"/>
        /// </param>
        /// <param name="item">
        /// A <see cref="InventoryItemBase"/>
        /// </param>
        [Obsolete("Use AddInventoryItem(InventoryItemBase item) instead.  This was deprecated in OpenSim 0.7.1")]
        public void AddInventoryItem(UUID AgentID, InventoryItemBase item)
        {
            AddInventoryItem(item);
        }

        /// <summary>
        /// Add an inventory item to an avatar's inventory.
        /// </summary>
        /// <param name="remoteClient">The remote client controlling the avatar</param>
        /// <param name="item">The item.  This structure contains all the item metadata, including the folder
        /// in which the item is to be placed.</param>
        public void AddInventoryItem(IClientAPI remoteClient, InventoryItemBase item)
        {
            AddInventoryItem(item);
            remoteClient.SendInventoryItemCreateUpdate(item, 0);
        }

        /// <summary>
        /// <see>CapsUpdatedInventoryItemAsset(IClientAPI, UUID, byte[])</see>
        /// </summary>
        public UUID CapsUpdateInventoryItemAsset(UUID avatarId, UUID itemID, byte[] data)
        {
            ScenePresence avatar;

            if (TryGetScenePresence(avatarId, out avatar))
            {
                IInventoryAccessModule invAccess = RequestModuleInterface<IInventoryAccessModule>();
                if (invAccess != null)
                    return invAccess.CapsUpdateInventoryItemAsset(avatar.ControllingClient, itemID, data);
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
        public ArrayList CapsUpdateTaskInventoryScriptAsset(IClientAPI remoteClient, UUID itemId,
                                                       UUID primId, bool isScriptRunning, byte[] data)
        {
            if (!Permissions.CanEditScript(itemId, primId, remoteClient.AgentId))
            {
                remoteClient.SendAgentAlertMessage("Insufficient permissions to edit script", false);
                return new ArrayList();
            }

            // Retrieve group
            SceneObjectPart part = GetSceneObjectPart(primId);
            if (part == null)
                return new ArrayList();

            SceneObjectGroup group = part.ParentGroup;

            // Retrieve item
            TaskInventoryItem item = group.GetInventoryItem(part.LocalId, itemId);

            if (null == item)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for caps script update "
                        + " but the item does not exist in this inventory",
                    itemId, part.Name, part.UUID);

                return new ArrayList();
            }

            AssetBase asset = CreateAsset(item.Name, item.Description, (sbyte)AssetType.LSLText, data, remoteClient.AgentId);
            AssetService.Store(asset);

//            m_log.DebugFormat(
//                "[PRIM INVENTORY]: Stored asset {0} when updating item {1} in prim {2} for {3}",
//                asset.ID, item.Name, part.Name, remoteClient.Name);

            if (isScriptRunning)
            {
                part.Inventory.RemoveScriptInstance(item.ItemID, false);
            }

            // Update item with new asset
            item.AssetID = asset.FullID;
            if (group.UpdateInventoryItem(item))
                remoteClient.SendAgentAlertMessage("Script saved", false);
            
            part.SendPropertiesToClient(remoteClient);

            // Trigger rerunning of script (use TriggerRezScript event, see RezScript)
            ArrayList errors = new ArrayList();

            if (isScriptRunning)
            {
                // Needs to determine which engine was running it and use that
                //
                part.Inventory.CreateScriptInstance(item.ItemID, 0, false, DefaultScriptEngine, 0);
                errors = part.Inventory.GetScriptErrors(item.ItemID);
            }
            else
            {
                remoteClient.SendAgentAlertMessage("Script saved", false);
            }

            // Tell anyone managing scripts that a script has been reloaded/changed
            EventManager.TriggerUpdateScript(remoteClient.AgentId, itemId, primId, isScriptRunning, item.AssetID);

            part.ParentGroup.ResumeScripts();
            return errors;
        }

        /// <summary>
        /// <see>CapsUpdateTaskInventoryScriptAsset(IClientAPI, UUID, UUID, bool, byte[])</see>
        /// </summary>
        public ArrayList CapsUpdateTaskInventoryScriptAsset(UUID avatarId, UUID itemId,
                                                        UUID primId, bool isScriptRunning, byte[] data)
        {
            ScenePresence avatar;

            if (TryGetScenePresence(avatarId, out avatar))
            {
                return CapsUpdateTaskInventoryScriptAsset(
                    avatar.ControllingClient, itemId, primId, isScriptRunning, data);
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: " +
                    "Avatar {0} cannot be found to update its prim item asset",
                    avatarId);
                return new ArrayList();
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
//            m_log.DebugFormat(
//                "[USER INVENTORY]: Updating asset for item {0} {1}, transaction ID {2} for {3}",
//                itemID, itemUpd.Name, transactionID, remoteClient.Name);

            // This one will let people set next perms on items in agent
            // inventory. Rut-Roh. Whatever. Make this secure. Yeah.
            //
            // Passing something to another avatar or a an object will already
            InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
            item = InventoryService.GetItem(item);

            if (item != null)
            {
                if (item.Owner != remoteClient.AgentId)
                    return;

                if (UUID.Zero == transactionID)
                {
                    item.Name = itemUpd.Name;
                    item.Description = itemUpd.Description;

//                    m_log.DebugFormat(
//                        "[USER INVENTORY]: itemUpd {0} {1} {2} {3}, item {4} {5} {6} {7}",
//                        itemUpd.NextPermissions, itemUpd.GroupPermissions, itemUpd.EveryOnePermissions, item.Flags,
//                        item.NextPermissions, item.GroupPermissions, item.EveryOnePermissions, item.CurrentPermissions);

                    if (item.NextPermissions != (itemUpd.NextPermissions & item.BasePermissions))
                        item.Flags |= (uint)InventoryItemFlags.ObjectOverwriteNextOwner;
                    item.NextPermissions = itemUpd.NextPermissions & item.BasePermissions;
                    if (item.EveryOnePermissions != (itemUpd.EveryOnePermissions & item.BasePermissions))
                        item.Flags |= (uint)InventoryItemFlags.ObjectOverwriteEveryone;
                    item.EveryOnePermissions = itemUpd.EveryOnePermissions & item.BasePermissions;
                    if (item.GroupPermissions != (itemUpd.GroupPermissions & item.BasePermissions))
                        item.Flags |= (uint)InventoryItemFlags.ObjectOverwriteGroup;

//                    m_log.DebugFormat("[USER INVENTORY]: item.Flags {0}", item.Flags);

                    item.GroupPermissions = itemUpd.GroupPermissions & item.BasePermissions;
                    item.GroupID = itemUpd.GroupID;
                    item.GroupOwned = itemUpd.GroupOwned;
                    item.CreationDate = itemUpd.CreationDate;
                    // The client sends zero if its newly created?

                    if (itemUpd.CreationDate == 0)
                        item.CreationDate = Util.UnixTimeSinceEpoch();
                    else
                        item.CreationDate = itemUpd.CreationDate;

                    // TODO: Check if folder changed and move item
                    //item.NextPermissions = itemUpd.Folder;
                    item.InvType = itemUpd.InvType;

                    if (item.SalePrice != itemUpd.SalePrice ||
                        item.SaleType != itemUpd.SaleType)
                        item.Flags |= (uint)InventoryItemFlags.ObjectSlamSale;
                    item.SalePrice = itemUpd.SalePrice;
                    item.SaleType = itemUpd.SaleType;

                    InventoryService.UpdateItem(item);
                }
                else
                {
                    if (AgentTransactionsModule != null)
                    {
                        AgentTransactionsModule.HandleItemUpdateFromTransaction(remoteClient, transactionID, item);
                    }
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[AGENTINVENTORY]: Item id {0} not found for an inventory item update for {1}.",
                    itemID, remoteClient.Name);
            }
        }

        /// <summary>
        /// Give an inventory item from one user to another
        /// </summary>
        /// <param name="recipientClient"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="itemId"></param>
        public virtual void GiveInventoryItem(IClientAPI recipientClient, UUID senderId, UUID itemId)
        {
            InventoryItemBase itemCopy = GiveInventoryItem(recipientClient.AgentId, senderId, itemId);

            if (itemCopy != null)
                recipientClient.SendBulkUpdateInventory(itemCopy);
        }

        /// <summary>
        /// Give an inventory item from one user to another
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="itemId"></param>
        /// <returns>The inventory item copy given, null if the give was unsuccessful</returns>
        public virtual InventoryItemBase GiveInventoryItem(UUID recipient, UUID senderId, UUID itemId)
        {
            return GiveInventoryItem(recipient, senderId, itemId, UUID.Zero);
        }

        /// <summary>
        /// Give an inventory item from one user to another
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="itemId"></param>
        /// <param name="recipientFolderId">
        /// The id of the folder in which the copy item should go.  If UUID.Zero then the item is placed in the most
        /// appropriate default folder.
        /// </param>
        /// <returns>
        /// The inventory item copy given, null if the give was unsuccessful
        /// </returns>
        public virtual InventoryItemBase GiveInventoryItem(
            UUID recipient, UUID senderId, UUID itemId, UUID recipientFolderId)
        {
            //Console.WriteLine("Scene.Inventory.cs: GiveInventoryItem");

            InventoryItemBase item = new InventoryItemBase(itemId, senderId);
            item = InventoryService.GetItem(item);

            if (item == null)
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Failed to find item {0} sent by {1} to {2}", itemId, senderId, recipient);
                return null;
            }

            if (item.Owner != senderId)
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Attempt to send item {0} {1} to {2} failed because sender {3} did not match item owner {4}",
                    item.Name, item.ID, recipient, senderId, item.Owner);
                return null;
            }

            IUserManagement uman = RequestModuleInterface<IUserManagement>();
            if (uman != null)
                uman.AddUser(item.CreatorIdAsUuid, item.CreatorData);

            if (!Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                    return null;
            }

            // Insert a copy of the item into the recipient
            InventoryItemBase itemCopy = new InventoryItemBase();
            itemCopy.Owner = recipient;
            itemCopy.CreatorId = item.CreatorId;
            itemCopy.CreatorData = item.CreatorData;
            itemCopy.ID = UUID.Random();
            itemCopy.AssetID = item.AssetID;
            itemCopy.Description = item.Description;
            itemCopy.Name = item.Name;
            itemCopy.AssetType = item.AssetType;
            itemCopy.InvType = item.InvType;
            itemCopy.Folder = recipientFolderId;

            if (Permissions.PropagatePermissions() && recipient != senderId)
            {
                // Trying to do this right this time. This is evil. If
                // you believe in Good, go elsewhere. Vampires and other
                // evil creatores only beyond this point. You have been
                // warned.

                // We're going to mask a lot of things by the next perms
                // Tweak the next perms to be nicer to our data
                //
                // In this mask, all the bits we do NOT want to mess
                // with are set. These are:
                //
                // Transfer
                // Copy
                // Modufy
                uint permsMask = ~ ((uint)PermissionMask.Copy |
                                    (uint)PermissionMask.Transfer |
                                    (uint)PermissionMask.Modify);

                // Now, reduce the next perms to the mask bits
                // relevant to the operation
                uint nextPerms = permsMask | (item.NextPermissions &
                                  ((uint)PermissionMask.Copy |
                                   (uint)PermissionMask.Transfer |
                                   (uint)PermissionMask.Modify));

                // nextPerms now has all bits set, except for the actual
                // next permission bits.

                // This checks for no mod, no copy, no trans.
                // This indicates an error or messed up item. Do it like
                // SL and assume trans
                if (nextPerms == permsMask)
                    nextPerms |= (uint)PermissionMask.Transfer;

                // Inventory owner perms are the logical AND of the
                // folded perms and the root prim perms, however, if
                // the root prim is mod, the inventory perms will be
                // mod. This happens on "take" and is of little concern
                // here, save for preventing escalation

                // This hack ensures that items previously permalocked
                // get unlocked when they're passed or rezzed
                uint basePerms = item.BasePermissions |
                                (uint)PermissionMask.Move;
                uint ownerPerms = item.CurrentPermissions;

                // If this is an object, root prim perms may be more
                // permissive than folded perms. Use folded perms as
                // a mask
                if (item.InvType == (int)InventoryType.Object)
                {
                    // Create a safe mask for the current perms
                    uint foldedPerms = (item.CurrentPermissions & 7) << 13;
                    foldedPerms |= permsMask;

                    bool isRootMod = (item.CurrentPermissions &
                                      (uint)PermissionMask.Modify) != 0 ?
                                      true : false;

                    // Mask the owner perms to the folded perms
                    ownerPerms &= foldedPerms;
                    basePerms &= foldedPerms;

                    // If the root was mod, let the mask reflect that
                    // We also need to adjust the base here, because
                    // we should be able to edit in-inventory perms
                    // for the root prim, if it's mod.
                    if (isRootMod)
                    {
                        ownerPerms |= (uint)PermissionMask.Modify;
                        basePerms |= (uint)PermissionMask.Modify;
                    }
                }

                // These will be applied to the root prim at next rez.
                // The slam bit (bit 3) and folded permission (bits 0-2)
                // are preserved due to the above mangling
                ownerPerms &= nextPerms;

                // Mask the base permissions. This is a conservative
                // approach altering only the three main perms
                basePerms &= nextPerms;

                // Assign to the actual item. Make sure the slam bit is
                // set, if it wasn't set before.
                itemCopy.BasePermissions = basePerms;
                itemCopy.CurrentPermissions = ownerPerms;
                itemCopy.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;

                itemCopy.NextPermissions = item.NextPermissions;

                // This preserves "everyone can move"
                itemCopy.EveryOnePermissions = item.EveryOnePermissions &
                                               nextPerms;

                // Intentionally killing "share with group" here, as
                // the recipient will not have the group this is
                // set to
                itemCopy.GroupPermissions = 0;
            }
            else
            {
                itemCopy.CurrentPermissions = item.CurrentPermissions;
                itemCopy.NextPermissions = item.NextPermissions;
                itemCopy.EveryOnePermissions = item.EveryOnePermissions & item.NextPermissions;
                itemCopy.GroupPermissions = item.GroupPermissions & item.NextPermissions;
                itemCopy.BasePermissions = item.BasePermissions;
            }
            
            if (itemCopy.Folder == UUID.Zero)
            {
                InventoryFolderBase folder = InventoryService.GetFolderForType(recipient, (AssetType)itemCopy.AssetType);

                if (folder != null)
                {
                    itemCopy.Folder = folder.ID;
                }
                else
                {
                    InventoryFolderBase root = InventoryService.GetRootFolder(recipient);

                    if (root != null)
                        itemCopy.Folder = root.ID;
                    else
                        return null; // No destination
                }
            }

            itemCopy.GroupID = UUID.Zero;
            itemCopy.GroupOwned = false;
            itemCopy.Flags = item.Flags;
            itemCopy.SalePrice = item.SalePrice;
            itemCopy.SaleType = item.SaleType;

            if (AddInventoryItem(itemCopy))
            {
                IInventoryAccessModule invAccess = RequestModuleInterface<IInventoryAccessModule>();
                if (invAccess != null)
                    Util.FireAndForget(delegate { invAccess.TransferInventoryAssets(itemCopy, senderId, recipient); });
            }

            if (!Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                {
                    List<UUID> items = new List<UUID>();
                    items.Add(itemId);
                    InventoryService.DeleteItems(senderId, items);
                }
            }

            return itemCopy;
        }

        /// <summary>
        /// Give an entire inventory folder from one user to another.  The entire contents (including all descendent
        /// folders) is given.
        /// </summary>
        /// <param name="recipientId"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="folderId"></param>
        /// <param name="recipientParentFolderId">
        /// The id of the receipient folder in which the send folder should be placed.  If UUID.Zero then the
        /// recipient folder is the root folder
        /// </param>
        /// <returns>
        /// The inventory folder copy given, null if the copy was unsuccessful
        /// </returns>
        public virtual InventoryFolderBase GiveInventoryFolder(
            UUID recipientId, UUID senderId, UUID folderId, UUID recipientParentFolderId)
        {
            //// Retrieve the folder from the sender
            InventoryFolderBase folder = InventoryService.GetFolder(new InventoryFolderBase(folderId));
            if (null == folder)
            {
                m_log.ErrorFormat(
                     "[AGENT INVENTORY]: Could not find inventory folder {0} to give", folderId);

                return null;
            }

            if (recipientParentFolderId == UUID.Zero)
            {
                InventoryFolderBase recipientRootFolder = InventoryService.GetRootFolder(recipientId);
                if (recipientRootFolder != null)
                    recipientParentFolderId = recipientRootFolder.ID;
                else
                {
                    m_log.WarnFormat("[AGENT INVENTORY]: Unable to find root folder for receiving agent");
                    return null;
                }
            }

            UUID newFolderId = UUID.Random();
            InventoryFolderBase newFolder 
                = new InventoryFolderBase(
                    newFolderId, folder.Name, recipientId, folder.Type, recipientParentFolderId, folder.Version);
            InventoryService.AddFolder(newFolder);

            // Give all the subfolders
            InventoryCollection contents = InventoryService.GetFolderContent(senderId, folderId);
            foreach (InventoryFolderBase childFolder in contents.Folders)
            {
                GiveInventoryFolder(recipientId, senderId, childFolder.ID, newFolder.ID);
            }

            // Give all the items
            foreach (InventoryItemBase item in contents.Items)
            {
                GiveInventoryItem(recipientId, senderId, item.ID, newFolder.ID);
            }

            return newFolder;
        }

        public void CopyInventoryItem(IClientAPI remoteClient, uint callbackID, UUID oldAgentID, UUID oldItemID,
                                      UUID newFolderID, string newName)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: CopyInventoryItem received by {0} with oldAgentID {1}, oldItemID {2}, new FolderID {3}, newName {4}",
                remoteClient.AgentId, oldAgentID, oldItemID, newFolderID, newName);

            InventoryItemBase item = null;
            if (LibraryService != null && LibraryService.LibraryRootFolder != null)
                item = LibraryService.LibraryRootFolder.FindItem(oldItemID);

            if (item == null)
            {
                item = new InventoryItemBase(oldItemID, remoteClient.AgentId);
                item = InventoryService.GetItem(item);

                if (item == null)
                {
                    m_log.Error("[AGENT INVENTORY]: Failed to find item " + oldItemID.ToString());
                    return;
                }

                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    return;
            }

            AssetBase asset = AssetService.Get(item.AssetID.ToString());

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

                if (remoteClient.AgentId == oldAgentID
                    || (LibraryService != null
                        && LibraryService.LibraryRootFolder != null
                        && oldAgentID == LibraryService.LibraryRootFolder.Owner))
                {
                    CreateNewInventoryItem(
                        remoteClient, item.CreatorId, item.CreatorData, newFolderID,
                        newName, item.Description, item.Flags, callbackID, asset, (sbyte)item.InvType,
                        item.BasePermissions, item.CurrentPermissions, item.EveryOnePermissions,
                        item.NextPermissions, item.GroupPermissions, Util.UnixTimeSinceEpoch());
                }
                else
                {  
                    // If item is transfer or permissions are off or calling agent is allowed to copy item owner's inventory item.
                    if (((item.CurrentPermissions & (uint)PermissionMask.Transfer) != 0)
                        && (m_permissions.BypassPermissions()
                            || m_permissions.CanCopyUserInventory(remoteClient.AgentId, oldItemID)))
                    {
                        CreateNewInventoryItem(
                            remoteClient, item.CreatorId, item.CreatorData, newFolderID, newName, item.Description, item.Flags, callbackID,
                            asset, (sbyte) item.InvType,
                            item.NextPermissions, item.NextPermissions, item.EveryOnePermissions & item.NextPermissions,
                            item.NextPermissions, item.GroupPermissions, Util.UnixTimeSinceEpoch());
                    }
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
        public AssetBase CreateAsset(string name, string description, sbyte assetType, byte[] data, UUID creatorID)
        {
            AssetBase asset = new AssetBase(UUID.Random(), name, assetType, creatorID.ToString());
            asset.Description = description;
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
        public void MoveInventoryItem(IClientAPI remoteClient, List<InventoryItemBase> items)
        {
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Moving {0} items for user {1}", items.Count, remoteClient.AgentId);

            if (!InventoryService.MoveItems(remoteClient.AgentId, items))
                m_log.Warn("[AGENT INVENTORY]: Failed to move items for user " + remoteClient.AgentId);
        }

        /// <summary>
        /// Create a new inventory item.
        /// </summary>
        /// <param name="remoteClient">Client creating this inventory item.</param>
        /// <param name="creatorID"></param>
        /// <param name="creatorData"></param>
        /// <param name="folderID">UUID of folder in which this item should be placed.</param>
        /// <param name="name">Item name.</para>
        /// <param name="description">Item description.</param>
        /// <param name="flags">Item flags</param>
        /// <param name="callbackID">Generated by the client.</para>
        /// <param name="asset">Asset to which this item refers.</param>
        /// <param name="invType">Type of inventory item.</param>
        /// <param name="nextOwnerMask">Next owner pemrissions mask.</param>
        /// <param name="creationDate">Unix timestamp at which this item was created.</param>
        public void CreateNewInventoryItem(
            IClientAPI remoteClient, string creatorID, string creatorData, UUID folderID,
            string name, string description, uint flags, uint callbackID,
            AssetBase asset, sbyte invType, uint nextOwnerMask, int creationDate)
        {
            CreateNewInventoryItem(
                remoteClient, creatorID, creatorData, folderID, name, description, flags, callbackID, asset, invType,
                (uint)PermissionMask.All, (uint)PermissionMask.All, 0, nextOwnerMask, 0, creationDate);
        }

        /// <summary>
        /// Create a new Inventory Item
        /// </summary>
        /// <param name="remoteClient">Client creating this inventory item.</param>
        /// <param name="creatorID"></param>
        /// <param name="creatorData"></param>
        /// <param name="folderID">UUID of folder in which this item should be placed.</param>
        /// <param name="name">Item name.</para>
        /// <param name="description">Item description.</param>
        /// <param name="flags">Item flags</param>
        /// <param name="callbackID">Generated by the client.</para>
        /// <param name="asset">Asset to which this item refers.</param>
        /// <param name="invType">Type of inventory item.</param>
        /// <param name="baseMask">Base permissions mask.</param>
        /// <param name="currentMask">Current permissions mask.</param>
        /// <param name="everyoneMask">Everyone permissions mask.</param>
        /// <param name="nextOwnerMask">Next owner pemrissions mask.</param>
        /// <param name="groupMask">Group permissions mask.</param>
        /// <param name="creationDate">Unix timestamp at which this item was created.</param>
        private void CreateNewInventoryItem(
            IClientAPI remoteClient, string creatorID, string creatorData, UUID folderID,
            string name, string description, uint flags, uint callbackID, AssetBase asset, sbyte invType,
            uint baseMask, uint currentMask, uint everyoneMask, uint nextOwnerMask, uint groupMask, int creationDate)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.Owner = remoteClient.AgentId;
            item.CreatorId = creatorID;
            item.CreatorData = creatorData;
            item.ID = UUID.Random();
            item.AssetID = asset.FullID;
            item.Name = name;
            item.Description = description;
            item.Flags = flags;
            item.AssetType = asset.Type;
            item.InvType = invType;
            item.Folder = folderID;
            item.CurrentPermissions = currentMask;
            item.NextPermissions = nextOwnerMask;
            item.EveryOnePermissions = everyoneMask;
            item.GroupPermissions = groupMask;
            item.BasePermissions = baseMask;
            item.CreationDate = creationDate;

            if (AddInventoryItem(item))
            {
                remoteClient.SendInventoryItemCreateUpdate(item, callbackID);
            }
            else
            {
                m_dialogModule.SendAlertToUser(remoteClient, "Failed to create item");
                m_log.WarnFormat(
                    "Failed to add item for {0} in CreateNewInventoryItem!",
                     remoteClient.Name);
            }
        }

        /// <summary>
        /// Link an inventory item to an existing item.
        /// </summary>
        /// <remarks>
        /// The linkee item id is placed in the asset id slot.  This appears to be what the viewer expects when
        /// it receives inventory information.
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="transActionID"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="description"></param>
        /// <param name="name"></param>
        /// <param name="invType"></param>
        /// <param name="type">/param>
        /// <param name="olditemID"></param>
        private void HandleLinkInventoryItem(IClientAPI remoteClient, UUID transActionID, UUID folderID,
                                             uint callbackID, string description, string name,
                                             sbyte invType, sbyte type, UUID olditemID)
        {
//            m_log.DebugFormat(
//                "[AGENT INVENTORY]: Received request from {0} to create inventory item link {1} in folder {2} pointing to {3}, assetType {4}, inventoryType {5}",
//                remoteClient.Name, name, folderID, olditemID, (AssetType)type, (InventoryType)invType);

            if (!Permissions.CanCreateUserInventory(invType, remoteClient.AgentId))
                return;

            ScenePresence presence;
            if (TryGetScenePresence(remoteClient.AgentId, out presence))
            {
                // Disabled the check for duplicate links.
                //
                // When outfits are being adjusted, the viewer rapidly sends delete link messages followed by
                // create links.  However, since these are handled asynchronously, the deletes do not complete before
                // the creates are handled.  Therefore, we cannot enforce a duplicate link check.
//                InventoryItemBase existingLink = null;
//                List<InventoryItemBase> existingItems = InventoryService.GetFolderItems(remoteClient.AgentId, folderID);
//                foreach (InventoryItemBase item in existingItems)
//                    if (item.AssetID == olditemID)
//                        existingLink = item;
//
//                if (existingLink != null)
//                {
//                    m_log.WarnFormat(
//                        "[AGENT INVENTORY]: Ignoring request from {0} to create item link {1} in folder {2} pointing to {3} since a link named {4} with id {5} already exists",
//                        remoteClient.Name, name, folderID, olditemID, existingLink.Name, existingLink.ID);
//
//                    return;
//                }

                AssetBase asset = new AssetBase();
                asset.FullID = olditemID;
                asset.Type = type;
                asset.Name = name;
                asset.Description = description;

                CreateNewInventoryItem(
                    remoteClient, remoteClient.AgentId.ToString(), string.Empty, folderID,
                    name, description, 0, callbackID, asset, invType,
                    (uint)PermissionMask.All, (uint)PermissionMask.All, (uint)PermissionMask.All,
                    (uint)PermissionMask.All, (uint)PermissionMask.All, Util.UnixTimeSinceEpoch());
            }
            else
            {
                m_log.ErrorFormat(
                    "ScenePresence for agent uuid {0} unexpectedly not found in HandleLinkInventoryItem",
                    remoteClient.AgentId);
            }
        }

        /// <summary>
        /// Remove an inventory item for the client's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        private void RemoveInventoryItem(IClientAPI remoteClient, List<UUID> itemIDs)
        {
//            m_log.DebugFormat(
//                "[AGENT INVENTORY]: Removing inventory items {0} for {1}",
//                string.Join(",", itemIDs.ConvertAll<string>(uuid => uuid.ToString()).ToArray()),
//                remoteClient.Name);

            InventoryService.DeleteItems(remoteClient.AgentId, itemIDs);
        }

        /// <summary>
        /// Removes an inventory folder.  This packet is sent when the user
        /// right-clicks a folder that's already in trash and chooses "purge"
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        private void RemoveInventoryFolder(IClientAPI remoteClient, List<UUID> folderIDs)
        {
            m_log.DebugFormat("[SCENE INVENTORY]: RemoveInventoryFolders count {0}", folderIDs.Count);
            InventoryService.DeleteFolders(remoteClient.AgentId, folderIDs);
        }

        /// <summary>
        /// Send the details of a prim's inventory to the client.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="primLocalID"></param>
        public void RequestTaskInventory(IClientAPI remoteClient, uint primLocalID)
        {
            SceneObjectPart part = GetSceneObjectPart(primLocalID);
            if (part == null)
                return;

            if (XferManager != null)
                part.Inventory.RequestInventoryFile(remoteClient, XferManager);
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
            SceneObjectGroup group = null;
            if (part != null)
            {
                group = part.ParentGroup;
            }
            if (part != null && group != null)
            {
                if (!Permissions.CanEditObjectInventory(part.UUID, remoteClient.AgentId))
                    return;

                TaskInventoryItem item = group.GetInventoryItem(localID, itemID);
                if (item == null)
                    return;

                InventoryFolderBase destFolder = InventoryService.GetFolderForType(remoteClient.AgentId, AssetType.TrashFolder);

                // Move the item to trash. If this is a copiable item, only
                // a copy will be moved and we will still need to delete
                // the item from the prim. If it was no copy, is will be
                // deleted by this method.
                MoveTaskInventoryItem(remoteClient, destFolder.ID, part, itemID);

                if (group.GetInventoryItem(localID, itemID) != null)
                {
                    if (item.Type == 10)
                    {
                        part.RemoveScriptEvents(itemID);
                        EventManager.TriggerRemoveScript(localID, itemID);
                    }

                    group.RemoveInventoryItem(localID, itemID);
                }
                part.SendPropertiesToClient(remoteClient);
            }
        }

        private InventoryItemBase CreateAgentInventoryItemFromTask(UUID destAgent, SceneObjectPart part, UUID itemId)
        {
            TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(itemId);

            if (null == taskItem)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for creating an avatar"
                        + " inventory item from a prim's inventory item "
                        + " but the required item does not exist in the prim's inventory",
                    itemId, part.Name, part.UUID);

                return null;
            }

            if ((destAgent != taskItem.OwnerID) && ((taskItem.CurrentPermissions & (uint)PermissionMask.Transfer) == 0))
            {
                return null;
            }

            InventoryItemBase agentItem = new InventoryItemBase();

            agentItem.ID = UUID.Random();
            agentItem.CreatorId = taskItem.CreatorID.ToString();
            agentItem.CreatorData = taskItem.CreatorData;
            agentItem.Owner = destAgent;
            agentItem.AssetID = taskItem.AssetID;
            agentItem.Description = taskItem.Description;
            agentItem.Name = taskItem.Name;
            agentItem.AssetType = taskItem.Type;
            agentItem.InvType = taskItem.InvType;
            agentItem.Flags = taskItem.Flags;

            if ((part.OwnerID != destAgent) && Permissions.PropagatePermissions())
            {
                agentItem.BasePermissions = taskItem.BasePermissions & (taskItem.NextPermissions | (uint)PermissionMask.Move);
                if (taskItem.InvType == (int)InventoryType.Object)
                    agentItem.CurrentPermissions = agentItem.BasePermissions & (((taskItem.CurrentPermissions & 7) << 13) | (taskItem.CurrentPermissions & (uint)PermissionMask.Move));
                else
                    agentItem.CurrentPermissions = agentItem.BasePermissions & taskItem.CurrentPermissions;

                agentItem.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                agentItem.NextPermissions = taskItem.NextPermissions;
                agentItem.EveryOnePermissions = taskItem.EveryonePermissions & (taskItem.NextPermissions | (uint)PermissionMask.Move);
                agentItem.GroupPermissions = taskItem.GroupPermissions & taskItem.NextPermissions;
            }
            else
            {
                agentItem.BasePermissions = taskItem.BasePermissions;
                agentItem.CurrentPermissions = taskItem.CurrentPermissions;
                agentItem.NextPermissions = taskItem.NextPermissions;
                agentItem.EveryOnePermissions = taskItem.EveryonePermissions;
                agentItem.GroupPermissions = taskItem.GroupPermissions;
            }

            if (!Permissions.BypassPermissions())
            {
                if ((taskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                {
                    if (taskItem.Type == 10)
                    {
                        part.RemoveScriptEvents(itemId);
                        EventManager.TriggerRemoveScript(part.LocalId, itemId);
                    }

                    part.Inventory.RemoveInventoryItem(itemId);
                }
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
        public InventoryItemBase MoveTaskInventoryItem(IClientAPI remoteClient, UUID folderId, SceneObjectPart part, UUID itemId)
        {
            m_log.DebugFormat(
                "[PRIM INVENTORY]: Adding item {0} from {1} to folder {2} for {3}", 
                itemId, part.Name, folderId, remoteClient.Name);
            
            InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(remoteClient.AgentId, part, itemId);

            if (agentItem == null)
                return null;

            agentItem.Folder = folderId;
            AddInventoryItem(remoteClient, agentItem);
            return agentItem;
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

            TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(itemId);

            if (null == taskItem)
            {
                m_log.WarnFormat("[PRIM INVENTORY]: Move of inventory item {0} from prim with local id {1} failed"
                    + " because the inventory item could not be found",
                    itemId, primLocalId);

                return;
            }

            if ((taskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
            {
                // If the item to be moved is no copy, we need to be able to
                // edit the prim.
                if (!Permissions.CanEditObjectInventory(part.UUID, remoteClient.AgentId))
                    return;
            }
            else
            {
                // If the item is copiable, then we just need to have perms
                // on it. The delete check is a pure rights check
                if (!Permissions.CanDeleteObject(part.UUID, remoteClient.AgentId))
                    return;
            }

            MoveTaskInventoryItem(remoteClient, folderId, part, itemId);
        }

        /// <summary>
        /// <see>MoveTaskInventoryItem</see>
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID">
        /// The user inventory folder to move (or copy) the item to.  If null, then the most
        /// suitable system folder is used (e.g. the Objects folder for objects).  If there is no suitable folder, then
        /// the item is placed in the user's root inventory folder
        /// </param>
        /// <param name="part"></param>
        /// <param name="itemID"></param>
        public InventoryItemBase MoveTaskInventoryItem(UUID avatarId, UUID folderId, SceneObjectPart part, UUID itemId)
        {
            ScenePresence avatar;

            if (TryGetScenePresence(avatarId, out avatar))
            {
                return MoveTaskInventoryItem(avatar.ControllingClient, folderId, part, itemId);
            }
            else
            {
                InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(avatarId, part, itemId);

                if (agentItem == null)
                    return null;

                agentItem.Folder = folderId;

                AddInventoryItem(agentItem);

                return agentItem;
            }
        }

        /// <summary>
        /// Copy a task (prim) inventory item to another task (prim)
        /// </summary>
        /// <param name="destId">ID of destination part</param>
        /// <param name="part">Source part</param>
        /// <param name="itemId">Source item id to transfer</param>
        public void MoveTaskInventoryItem(UUID destId, SceneObjectPart part, UUID itemId)
        {
            TaskInventoryItem srcTaskItem = part.Inventory.GetInventoryItem(itemId);

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

            if (part.OwnerID != destPart.OwnerID)
            {
                // Source must have transfer permissions
                if ((srcTaskItem.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                    return;

                // Object cannot copy items to an object owned by a different owner
                // unless llAllowInventoryDrop has been called on the destination
                if ((destPart.GetEffectiveObjectFlags() & (uint)PrimFlags.AllowInventoryDrop) == 0)
                    return;
            }

            // must have both move and modify permission to put an item in an object
            if ((part.OwnerMask & ((uint)PermissionMask.Move | (uint)PermissionMask.Modify)) == 0)
                return;

            TaskInventoryItem destTaskItem = new TaskInventoryItem();

            destTaskItem.ItemID = UUID.Random();
            destTaskItem.CreatorID = srcTaskItem.CreatorID;
            destTaskItem.CreatorData = srcTaskItem.CreatorData;
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
                if (Permissions.PropagatePermissions())
                {
                    destTaskItem.CurrentPermissions = srcTaskItem.CurrentPermissions &
                            (srcTaskItem.NextPermissions | (uint)PermissionMask.Move);
                    destTaskItem.GroupPermissions = srcTaskItem.GroupPermissions &
                            (srcTaskItem.NextPermissions | (uint)PermissionMask.Move);
                    destTaskItem.EveryonePermissions = srcTaskItem.EveryonePermissions &
                            (srcTaskItem.NextPermissions | (uint)PermissionMask.Move);
                    destTaskItem.BasePermissions = srcTaskItem.BasePermissions &
                            (srcTaskItem.NextPermissions | (uint)PermissionMask.Move);
                    destTaskItem.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                }
            }

            destTaskItem.Description = srcTaskItem.Description;
            destTaskItem.Name = srcTaskItem.Name;
            destTaskItem.InvType = srcTaskItem.InvType;
            destTaskItem.Type = srcTaskItem.Type;

            destPart.Inventory.AddInventoryItem(destTaskItem, part.OwnerID != destPart.OwnerID);

            if ((srcTaskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                part.Inventory.RemoveInventoryItem(itemId);

            ScenePresence avatar;

            if (TryGetScenePresence(srcTaskItem.OwnerID, out avatar))
            {
                destPart.SendPropertiesToClient(avatar.ControllingClient);
            }
        }

        public UUID MoveTaskInventoryItems(UUID destID, string category, SceneObjectPart host, List<UUID> items)
        {
            InventoryFolderBase rootFolder = InventoryService.GetRootFolder(destID);

            UUID newFolderID = UUID.Random();

            InventoryFolderBase newFolder = new InventoryFolderBase(newFolderID, category, destID, -1, rootFolder.ID, rootFolder.Version);
            InventoryService.AddFolder(newFolder);

            foreach (UUID itemID in items)
            {
                InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(destID, host, itemID);

                if (agentItem != null)
                {
                    agentItem.Folder = newFolderID;

                    AddInventoryItem(agentItem);
                }
            }

            ScenePresence avatar = null;
            if (TryGetScenePresence(destID, out avatar))
            {
                //profile.SendInventoryDecendents(avatar.ControllingClient,
                //        profile.RootFolder.ID, true, false);
                //profile.SendInventoryDecendents(avatar.ControllingClient,
                //        newFolderID, false, true);

                SendInventoryUpdate(avatar.ControllingClient, rootFolder, true, false);
                SendInventoryUpdate(avatar.ControllingClient, newFolder, false, true);
            }

            return newFolderID;
        }

        public void SendInventoryUpdate(IClientAPI client, InventoryFolderBase folder, bool fetchFolders, bool fetchItems)
        {
            if (folder == null)
                return;

            // TODO: This code for looking in the folder for the library should be folded somewhere else
            // so that this class doesn't have to know the details (and so that multiple libraries, etc.
            // can be handled transparently).
            InventoryFolderImpl fold = null;
            if (LibraryService != null && LibraryService.LibraryRootFolder != null)
            {
                if ((fold = LibraryService.LibraryRootFolder.FindFolder(folder.ID)) != null)
                {
                    client.SendInventoryFolderDetails(
                        fold.Owner, folder.ID, fold.RequestListOfItems(),
                        fold.RequestListOfFolders(), fold.Version, fetchFolders, fetchItems);
                    return;
                }
            }

            // Fetch the folder contents
            InventoryCollection contents = InventoryService.GetFolderContent(client.AgentId, folder.ID);

            // Fetch the folder itself to get its current version
            InventoryFolderBase containingFolder = new InventoryFolderBase(folder.ID, client.AgentId);
            containingFolder = InventoryService.GetFolder(containingFolder);

//            m_log.DebugFormat("[AGENT INVENTORY]: Sending inventory folder contents ({0} nodes) for \"{1}\" to {2} {3}",
//                contents.Folders.Count + contents.Items.Count, containingFolder.Name, client.FirstName, client.LastName);

            if (containingFolder != null)
            {
                // If the folder requested contains links, then we need to send those folders first, otherwise the links
                // will be broken in the viewer.
                HashSet<UUID> linkedItemFolderIdsToSend = new HashSet<UUID>();
                foreach (InventoryItemBase item in contents.Items)
                {
                    if (item.AssetType == (int)AssetType.Link)
                    {
                        InventoryItemBase linkedItem = InventoryService.GetItem(new InventoryItemBase(item.AssetID));

                        // Take care of genuinely broken links where the target doesn't exist
                        // HACK: Also, don't follow up links that just point to other links.  In theory this is legitimate,
                        // but no viewer has been observed to set these up and this is the lazy way of avoiding cycles
                        // rather than having to keep track of every folder requested in the recursion.
                        if (linkedItem != null && linkedItem.AssetType != (int)AssetType.Link)
                        {
                            // We don't need to send the folder if source and destination of the link are in the same
                            // folder.
                            if (linkedItem.Folder != containingFolder.ID)
                                linkedItemFolderIdsToSend.Add(linkedItem.Folder);
                        }
                    }
                }

                foreach (UUID linkedItemFolderId in linkedItemFolderIdsToSend)
                    SendInventoryUpdate(client, new InventoryFolderBase(linkedItemFolderId), false, true);

                client.SendInventoryFolderDetails(
                    client.AgentId, folder.ID, contents.Items, contents.Folders,
                    containingFolder.Version, fetchFolders, fetchItems);
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
                TaskInventoryItem currentItem = part.Inventory.GetInventoryItem(itemID);
                bool allowInventoryDrop = (part.GetEffectiveObjectFlags()
                                           & (uint)PrimFlags.AllowInventoryDrop) != 0;

                // Explicity allow anyone to add to the inventory if the
                // AllowInventoryDrop flag has been set. Don't however let
                // them update an item unless they pass the external checks
                //
                if (!Permissions.CanEditObjectInventory(part.UUID, remoteClient.AgentId)
                    && (currentItem != null || !allowInventoryDrop))
                    return;

                if (currentItem == null)
                {
                    UUID copyID = UUID.Random();
                    if (itemID != UUID.Zero)
                    {
                        InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
                        item = InventoryService.GetItem(item);

                        // Try library
                        if (null == item && LibraryService != null && LibraryService.LibraryRootFolder != null)
                        {
                            item = LibraryService.LibraryRootFolder.FindItem(itemID);
                        }

                        // If we've found the item in the user's inventory or in the library
                        if (item != null)
                        {
                            part.ParentGroup.AddInventoryItem(remoteClient.AgentId, primLocalID, item, copyID);
                            m_log.InfoFormat(
                                "[PRIM INVENTORY]: Update with item {0} requested of prim {1} for {2}",
                                item.Name, primLocalID, remoteClient.Name);
                            part.SendPropertiesToClient(remoteClient);
                            if (!Permissions.BypassPermissions())
                            {
                                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                                {
                                    List<UUID> uuids = new List<UUID>();
                                    uuids.Add(itemID);
                                    RemoveInventoryItem(remoteClient, uuids);
                                }
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
                else // Updating existing item with new perms etc
                {
//                    m_log.DebugFormat(
//                        "[PRIM INVENTORY]: Updating item {0} in {1} for UpdateTaskInventory()", 
//                        currentItem.Name, part.Name);

                    // Only look for an uploaded updated asset if we are passed a transaction ID.  This is only the
                    // case for updates uploded through UDP.  Updates uploaded via a capability (e.g. a script update)
                    // will not pass in a transaction ID in the update message.
                    if (transactionID != UUID.Zero && AgentTransactionsModule != null)
                    {
                        AgentTransactionsModule.HandleTaskItemUpdateFromTransaction(
                            remoteClient, part, transactionID, currentItem);

                        if ((InventoryType)itemInfo.InvType == InventoryType.Notecard)
                            remoteClient.SendAgentAlertMessage("Notecard saved", false);
                        else if ((InventoryType)itemInfo.InvType == InventoryType.LSL)
                            remoteClient.SendAgentAlertMessage("Script saved", false);
                        else
                            remoteClient.SendAgentAlertMessage("Item saved", false);
                    }

                    // Base ALWAYS has move
                    currentItem.BasePermissions |= (uint)PermissionMask.Move;

                    itemInfo.Flags = currentItem.Flags;

                    // Check if we're allowed to mess with permissions
                    if (!Permissions.IsGod(remoteClient.AgentId)) // Not a god
                    {
                        if (remoteClient.AgentId != part.OwnerID) // Not owner
                        {
                            // Friends and group members can't change any perms
                            itemInfo.BasePermissions = currentItem.BasePermissions;
                            itemInfo.EveryonePermissions = currentItem.EveryonePermissions;
                            itemInfo.GroupPermissions = currentItem.GroupPermissions;
                            itemInfo.NextPermissions = currentItem.NextPermissions;
                            itemInfo.CurrentPermissions = currentItem.CurrentPermissions;
                        }
                        else
                        {
                            // Owner can't change base, and can change other
                            // only up to base
                            itemInfo.BasePermissions = currentItem.BasePermissions;
                            if (itemInfo.EveryonePermissions != currentItem.EveryonePermissions)
                                itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteEveryone;
                            if (itemInfo.GroupPermissions != currentItem.GroupPermissions)
                                itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteGroup;
                            if (itemInfo.CurrentPermissions != currentItem.CurrentPermissions)
                                itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteOwner;
                            if (itemInfo.NextPermissions != currentItem.NextPermissions)
                                itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteNextOwner;
                            itemInfo.EveryonePermissions &= currentItem.BasePermissions;
                            itemInfo.GroupPermissions &= currentItem.BasePermissions;
                            itemInfo.CurrentPermissions &= currentItem.BasePermissions;
                            itemInfo.NextPermissions &= currentItem.BasePermissions;
                        }

                    }
                    else
                    {
                        if (itemInfo.BasePermissions != currentItem.BasePermissions)
                            itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteBase;
                        if (itemInfo.EveryonePermissions != currentItem.EveryonePermissions)
                            itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteEveryone;
                        if (itemInfo.GroupPermissions != currentItem.GroupPermissions)
                            itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteGroup;
                        if (itemInfo.CurrentPermissions != currentItem.CurrentPermissions)
                            itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteOwner;
                        if (itemInfo.NextPermissions != currentItem.NextPermissions)
                            itemInfo.Flags |= (uint)InventoryItemFlags.ObjectOverwriteNextOwner;
                    }

                    // Next ALWAYS has move
                    itemInfo.NextPermissions |= (uint)PermissionMask.Move;

                    if (part.Inventory.UpdateInventoryItem(itemInfo))
                    {
                        part.SendPropertiesToClient(remoteClient);
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
        /// Rez a script into a prim's inventory, either ex nihilo or from an existing avatar inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemBase"> </param>
        /// <param name="transactionID"></param>
        /// <param name="localID"></param>
        public void RezScript(IClientAPI remoteClient, InventoryItemBase itemBase, UUID transactionID, uint localID)
        {
            SceneObjectPart partWhereRezzed;

            if (itemBase.ID != UUID.Zero)
                partWhereRezzed = RezScriptFromAgentInventory(remoteClient.AgentId, itemBase.ID, localID);
            else
                partWhereRezzed = RezNewScript(remoteClient.AgentId, itemBase);

            if (partWhereRezzed != null)
                partWhereRezzed.SendPropertiesToClient(remoteClient);
        }

        /// <summary>
        /// Rez a script into a prim from an agent inventory.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="fromItemID"></param>
        /// <param name="localID"></param>
        /// <returns>The part where the script was rezzed if successful.  False otherwise.</returns>
        public SceneObjectPart RezScriptFromAgentInventory(UUID agentID, UUID fromItemID, uint localID)
        {
            UUID copyID = UUID.Random();
            InventoryItemBase item = new InventoryItemBase(fromItemID, agentID);
            item = InventoryService.GetItem(item);

            // Try library
            // XXX clumsy, possibly should be one call
            if (null == item && LibraryService != null && LibraryService.LibraryRootFolder != null)
            {
                item = LibraryService.LibraryRootFolder.FindItem(fromItemID);
            }

            if (item != null)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part != null)
                {
                    if (!Permissions.CanEditObjectInventory(part.UUID, agentID))
                        return null;

                    part.ParentGroup.AddInventoryItem(agentID, localID, item, copyID);
                    // TODO: switch to posting on_rez here when scripts
                    // have state in inventory
                    part.Inventory.CreateScriptInstance(copyID, 0, false, DefaultScriptEngine, 0);

                    // tell anyone watching that there is a new script in town
                    EventManager.TriggerNewScript(agentID, part, copyID);

                    //                        m_log.InfoFormat("[PRIMINVENTORY]: " +
                    //                                         "Rezzed script {0} into prim local ID {1} for user {2}",
                    //                                         item.inventoryName, localID, remoteClient.Name);

                    part.ParentGroup.ResumeScripts();

                    return part;
                }
                else
                {
                    m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Could not rez script {0} into prim local ID {1} for user {2}"
                        + " because the prim could not be found in the region!",
                        item.Name, localID, agentID);
                }
            }
            else
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Could not find script inventory item {0} to rez for {1}!",
                    fromItemID, agentID);
            }

            return null;
        }

        /// <summary>
        /// Rez a new script from nothing.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemBase"></param>
        /// <returns>The part where the script was rezzed if successful.  False otherwise.</returns>
        public SceneObjectPart RezNewScript(UUID agentID, InventoryItemBase itemBase)
        {
            // The part ID is the folder ID!
            SceneObjectPart part = GetSceneObjectPart(itemBase.Folder);
            if (part == null)
            {
//                m_log.DebugFormat(
//                    "[SCENE INVENTORY]: Could not find part with id {0} for {1} to rez new script",
//                    itemBase.Folder, agentID);

                return null;
            }

            if (!Permissions.CanCreateObjectInventory(itemBase.InvType, part.UUID, agentID))
            {
//                m_log.DebugFormat(
//                    "[SCENE INVENTORY]: No permission to create new script in {0} for {1}", part.Name, agentID);

                return null;
            }

            AssetBase asset = CreateAsset(itemBase.Name, itemBase.Description, (sbyte)itemBase.AssetType,
                Encoding.ASCII.GetBytes("default\n{\n    state_entry()\n    {\n        llSay(0, \"Script running\");\n    }\n}"),
                agentID);
            AssetService.Store(asset);

            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ResetIDs(itemBase.Folder);
            taskItem.ParentID = itemBase.Folder;
            taskItem.CreationDate = (uint)itemBase.CreationDate;
            taskItem.Name = itemBase.Name;
            taskItem.Description = itemBase.Description;
            taskItem.Type = itemBase.AssetType;
            taskItem.InvType = itemBase.InvType;
            taskItem.OwnerID = itemBase.Owner;
            taskItem.CreatorID = itemBase.CreatorIdAsUuid;
            taskItem.BasePermissions = itemBase.BasePermissions;
            taskItem.CurrentPermissions = itemBase.CurrentPermissions;
            taskItem.EveryonePermissions = itemBase.EveryOnePermissions;
            taskItem.GroupPermissions = itemBase.GroupPermissions;
            taskItem.NextPermissions = itemBase.NextPermissions;
            taskItem.GroupID = itemBase.GroupID;
            taskItem.GroupPermissions = 0;
            taskItem.Flags = itemBase.Flags;
            taskItem.PermsGranter = UUID.Zero;
            taskItem.PermsMask = 0;
            taskItem.AssetID = asset.FullID;

            part.Inventory.AddInventoryItem(taskItem, false);
            part.Inventory.CreateScriptInstance(taskItem, 0, false, DefaultScriptEngine, 0);

            // tell anyone managing scripts that a new script exists
            EventManager.TriggerNewScript(agentID, part, taskItem.ItemID);

            part.ParentGroup.ResumeScripts();

            return part;
        }

        /// <summary>
        /// Rez a script into a prim's inventory from another prim
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"> </param>
        /// <param name="localID"></param>
        public void RezScriptFromPrim(UUID srcId, SceneObjectPart srcPart, UUID destId, int pin, int running, int start_param)
        {
            TaskInventoryItem srcTaskItem = srcPart.Inventory.GetInventoryItem(srcId);

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
            {
                // Group permissions
                if ((destPart.GroupID == UUID.Zero) || (destPart.GroupID != srcPart.GroupID) ||
                    ((destPart.GroupMask & (uint)PermissionMask.Modify) == 0))
                    return;
            } else {
                if ((destPart.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return;
            }

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
            destTaskItem.CreatorData = srcTaskItem.CreatorData;
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
                if (Permissions.PropagatePermissions())
                {
                    destTaskItem.CurrentPermissions = srcTaskItem.CurrentPermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.GroupPermissions = srcTaskItem.GroupPermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.EveryonePermissions = srcTaskItem.EveryonePermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.BasePermissions = srcTaskItem.BasePermissions &
                            srcTaskItem.NextPermissions;
                    destTaskItem.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                }
            }

            destTaskItem.Description = srcTaskItem.Description;
            destTaskItem.Name = srcTaskItem.Name;
            destTaskItem.InvType = srcTaskItem.InvType;
            destTaskItem.Type = srcTaskItem.Type;

            destPart.Inventory.AddInventoryItemExclusive(destTaskItem, false);

            if (running > 0)
            {
                destPart.Inventory.CreateScriptInstance(destTaskItem, start_param, false, DefaultScriptEngine, 0);
            }

            destPart.ParentGroup.ResumeScripts();

            ScenePresence avatar;

            if (TryGetScenePresence(srcTaskItem.OwnerID, out avatar))
            {
                destPart.SendPropertiesToClient(avatar.ControllingClient);
            }
        }

        public virtual void DeRezObjects(IClientAPI remoteClient, List<uint> localIDs,
                UUID groupID, DeRezAction action, UUID destinationID)
        {
            // First, see of we can perform the requested action and
            // build a list of eligible objects
            List<uint> deleteIDs = new List<uint>();
            List<SceneObjectGroup> deleteGroups = new List<SceneObjectGroup>();

            // Start with true for both, then remove the flags if objects
            // that we can't derez are part of the selection
            bool permissionToTake = true;
            bool permissionToTakeCopy = true;
            bool permissionToDelete = true;

            foreach (uint localID in localIDs)
            {
                // Invalid id
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part == null)
                    continue;

                // Already deleted by someone else
                if (part.ParentGroup.IsDeleted)
                    continue;

                // Can't delete child prims
                if (part != part.ParentGroup.RootPart)
                    continue;

                SceneObjectGroup grp = part.ParentGroup;

                deleteIDs.Add(localID);
                deleteGroups.Add(grp);

                // If child prims have invalid perms, fix them
                grp.AdjustChildPrimPermissions();

                if (remoteClient == null)
                {
                    // Autoreturn has a null client. Nothing else does. So
                    // allow only returns
                    if (action != DeRezAction.Return)
                    {
                        m_log.WarnFormat(
                            "[AGENT INVENTORY]: Ignoring attempt to {0} {1} {2} without a client", 
                            action, grp.Name, grp.UUID);
                        return;
                    }

                    permissionToTakeCopy = false;
                }
                else
                {
                    if (!Permissions.CanTakeCopyObject(grp.UUID, remoteClient.AgentId))
                        permissionToTakeCopy = false;
                    
                    if (!Permissions.CanTakeObject(grp.UUID, remoteClient.AgentId))
                        permissionToTake = false;
                    
                    if (!Permissions.CanDeleteObject(grp.UUID, remoteClient.AgentId))
                        permissionToDelete = false;
                }
            }

            // Handle god perms
            if ((remoteClient != null) && Permissions.IsGod(remoteClient.AgentId))
            {
                permissionToTake = true;
                permissionToTakeCopy = true;
                permissionToDelete = true;
            }

            // If we're re-saving, we don't even want to delete
            if (action == DeRezAction.SaveToExistingUserInventoryItem)
                permissionToDelete = false;

            // if we want to take a copy, we also don't want to delete
            // Note: after this point, the permissionToTakeCopy flag
            // becomes irrelevant. It already includes the permissionToTake
            // permission and after excluding no copy items here, we can
            // just use that.
            if (action == DeRezAction.TakeCopy)
            {
                // If we don't have permission, stop right here
                if (!permissionToTakeCopy)
                    return;

                permissionToTake = true;
                // Don't delete
                permissionToDelete = false;
            }

            if (action == DeRezAction.Return)
            {
                if (remoteClient != null)
                {
                    if (Permissions.CanReturnObjects(
                                    null,
                                    remoteClient.AgentId,
                                    deleteGroups))
                    {
                        permissionToTake = true;
                        permissionToDelete = true;

                        foreach (SceneObjectGroup g in deleteGroups)
                        {
                            AddReturn(g.OwnerID == g.GroupID ? g.LastOwnerID : g.OwnerID, g.Name, g.AbsolutePosition, "parcel owner return");
                        }
                    }
                }
                else // Auto return passes through here with null agent
                {
                    permissionToTake = true;
                    permissionToDelete = true;
                }
            }

            if (permissionToTake && (action != DeRezAction.Delete || this.m_useTrashOnDelete))
            {
                m_asyncSceneObjectDeleter.DeleteToInventory(
                        action, destinationID, deleteGroups, remoteClient,
                        permissionToDelete);
            }
            else if (permissionToDelete)
            {
                foreach (SceneObjectGroup g in deleteGroups)
                    DeleteSceneObject(g, false);
            }
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
        /// <param name="RezSelected"></param>
        /// <param name="RemoveItem"></param>
        /// <param name="fromTaskID"></param>
        public virtual void RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                    UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    bool RezSelected, bool RemoveItem, UUID fromTaskID)
        {
//            m_log.DebugFormat(
//                "[PRIM INVENTORY]: RezObject from {0} for item {1} from task id {2}", 
//                remoteClient.Name, itemID, fromTaskID);
            
            if (fromTaskID == UUID.Zero)
            {
                IInventoryAccessModule invAccess = RequestModuleInterface<IInventoryAccessModule>();
                if (invAccess != null)
                    invAccess.RezObject(
                        remoteClient, itemID, RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection,
                        RezSelected, RemoveItem, fromTaskID, false);
            }
            else
            {            
                SceneObjectPart part = GetSceneObjectPart(fromTaskID);
                if (part == null)
                {
                    m_log.ErrorFormat(                                     
                        "[TASK INVENTORY]: {0} tried to rez item id {1} from object id {2} but there is no such scene object", 
                        remoteClient.Name, itemID, fromTaskID);
                    
                    return;
                }
                
                TaskInventoryItem item = part.Inventory.GetInventoryItem(itemID);
                if (item == null)
                {
                    m_log.ErrorFormat(                                     
                        "[TASK INVENTORY]: {0} tried to rez item id {1} from object id {2} but there is no such item", 
                        remoteClient.Name, itemID, fromTaskID);
                    
                    return;
                }                
                               
                byte bRayEndIsIntersection = (byte)(RayEndIsIntersection ? 1 : 0);
                Vector3 scale = new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 pos 
                    = GetNewRezLocation(
                        RayStart, RayEnd, RayTargetID, Quaternion.Identity,
                        BypassRayCast, bRayEndIsIntersection, true, scale, false);            
                
                RezObject(part, item, pos, null, Vector3.Zero, 0);
            }
        }
        
        /// <summary>
        /// Rez an object into the scene from a prim's inventory.
        /// </summary>
        /// <param name="sourcePart"></param>
        /// <param name="item"></param>
        /// <param name="pos">The position of the rezzed object.</param>
        /// <param name="rot">The rotation of the rezzed object.  If null, then the rotation stored with the object
        /// will be used if it exists.</param>
        /// <param name="vel">The velocity of the rezzed object.</param>
        /// <param name="param"></param>
        /// <returns>The SceneObjectGroup rezzed or null if rez was unsuccessful</returns>
        public virtual SceneObjectGroup RezObject(
            SceneObjectPart sourcePart, TaskInventoryItem item, Vector3 pos, Quaternion? rot, Vector3 vel, int param)
        {
            if (null == item)
                return null;
            
            SceneObjectGroup group = sourcePart.Inventory.GetRezReadySceneObject(item);
            
            if (null == group)
                return null;
            
            if (!Permissions.CanRezObject(group.PrimCount, item.OwnerID, pos))
                return null;

            if (!Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    sourcePart.Inventory.RemoveInventoryItem(item.ItemID);
            }

            group.FromPartID = sourcePart.UUID;
            AddNewSceneObject(group, true, pos, rot, vel);
            
            // We can only call this after adding the scene object, since the scene object references the scene
            // to find out if scripts should be activated at all.
            group.CreateScriptInstances(param, true, DefaultScriptEngine, 3);
            
            group.ScheduleGroupForFullUpdate();
        
            return group;
        }

        public virtual bool returnObjects(SceneObjectGroup[] returnobjects,
                UUID AgentId)
        {
            List<uint> localIDs = new List<uint>();

            foreach (SceneObjectGroup grp in returnobjects)
            {
                AddReturn(grp.OwnerID, grp.Name, grp.AbsolutePosition,
                        "parcel owner return");
                localIDs.Add(grp.RootPart.LocalId);
            }
            DeRezObjects(null, localIDs, UUID.Zero, DeRezAction.Return,
                    UUID.Zero);

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

        public void GetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID)
        {
            EventManager.TriggerGetScriptRunning(controllingClient, objectID, itemID);
        }

        void ObjectOwner(IClientAPI remoteClient, UUID ownerID, UUID groupID, List<uint> localIDs)
        {
            if (!Permissions.IsGod(remoteClient.AgentId))
            {
                if (ownerID != UUID.Zero)
                    return;
                
                if (!Permissions.CanDeedObject(remoteClient.AgentId, groupID))
                    return;
            }

            List<SceneObjectGroup> groups = new List<SceneObjectGroup>();

            foreach (uint localID in localIDs)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
	            if (part == null)
	                continue;

                if (!groups.Contains(part.ParentGroup))
                    groups.Add(part.ParentGroup);
            }

            foreach (SceneObjectGroup sog in groups)
            {
                if (ownerID != UUID.Zero)
                {
                    sog.SetOwnerId(ownerID);
                    sog.SetGroup(groupID, remoteClient);
                    sog.ScheduleGroupForFullUpdate();

                    SceneObjectPart[] partList = sog.Parts;
                    
                    foreach (SceneObjectPart child in partList)
                    {
                        child.Inventory.ChangeInventoryOwner(ownerID);
                        child.TriggerScriptChangedEvent(Changed.OWNER);
                    }
                }
                else
                {
                    if (!Permissions.CanEditObject(sog.UUID, remoteClient.AgentId))
                        continue;

                    if (sog.GroupID != groupID)
                        continue;
                    
                    SceneObjectPart[] partList = sog.Parts;

                    foreach (SceneObjectPart child in partList)
                    {
                        child.LastOwnerID = child.OwnerID;
                        child.Inventory.ChangeInventoryOwner(groupID);
                        child.TriggerScriptChangedEvent(Changed.OWNER);
                    }

                    sog.SetOwnerId(groupID);
                    sog.ApplyNextOwnerPermissions();
                }
            }

            foreach (uint localID in localIDs)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
	            if (part == null)
	                continue;
                part.SendPropertiesToClient(remoteClient);
            }
        }

        public void DelinkObjects(List<uint> primIds, IClientAPI client)
        {
            List<SceneObjectPart> parts = new List<SceneObjectPart>();

            foreach (uint localID in primIds)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);

                if (part == null)
                    continue;

                if (Permissions.CanDelinkObject(client.AgentId, part.ParentGroup.RootPart.UUID))
                    parts.Add(part);
            }

            m_sceneGraph.DelinkObjects(parts);
        }

        /// <summary>
        /// Link the scene objects containing the indicated parts to a root object.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="parentPrimId">A root prim id of the object which will be the root prim of the resulting linkset.</param>
        /// <param name="childPrimIds">A list of child prims for the objects that should be linked in.</param>
        public void LinkObjects(IClientAPI client, uint parentPrimId, List<uint> childPrimIds)
        {
            LinkObjects(client.AgentId, parentPrimId, childPrimIds);
        }

        /// <summary>
        /// Link the scene objects containing the indicated parts to a root object.
        /// </summary>
        /// <param name="agentId">The ID of the user linking.</param>
        /// <param name="parentPrimId">A root prim id of the object which will be the root prim of the resulting linkset.</param>
        /// <param name="childPrimIds">A list of child prims for the objects that should be linked in.</param>
        public void LinkObjects(UUID agentId, uint parentPrimId, List<uint> childPrimIds)
        {
            List<UUID> owners = new List<UUID>();

            List<SceneObjectPart> children = new List<SceneObjectPart>();
            SceneObjectPart root = GetSceneObjectPart(parentPrimId);

            if (root == null)
            {
                m_log.DebugFormat("[LINK]: Can't find linkset root prim {0}", parentPrimId);
                return;
            }

            if (!Permissions.CanLinkObject(agentId, root.ParentGroup.RootPart.UUID))
            {
                m_log.DebugFormat("[LINK]: Refusing link. No permissions on root prim");
                return;
            }

            foreach (uint localID in childPrimIds)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);

                if (part == null)
                    continue;

                if (!owners.Contains(part.OwnerID))
                    owners.Add(part.OwnerID);

                if (Permissions.CanLinkObject(agentId, part.ParentGroup.RootPart.UUID))
                    children.Add(part);
            }

            // Must be all one owner
            //
            if (owners.Count > 1)
            {
                m_log.DebugFormat("[LINK]: Refusing link. Too many owners");
                return;
            }

            if (children.Count == 0)
            {
                m_log.DebugFormat("[LINK]: Refusing link. No permissions to link any of the children");
                return;
            }

            m_sceneGraph.LinkObjects(root, children);
        }

        private string PermissionString(uint permissions)
        {
            PermissionMask perms = (PermissionMask)permissions &
                    (PermissionMask.Move |
                     PermissionMask.Copy |
                     PermissionMask.Transfer |
                     PermissionMask.Modify);
            return perms.ToString();
        }
    }
}
