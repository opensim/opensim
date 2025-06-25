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
using System.Xml;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Serialization;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.Framework.Scenes
{
    public partial class Scene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        //private static readonly string LogHeader = "[SCENE INVENTORY]";

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
            m_log.Info($"[SCENE]: Initializing script instances in {RegionInfo.RegionName}");

            int scriptsValidForStarting = 0;

            EntityBase[] entities = Entities.GetEntities();
            foreach (EntityBase group in entities)
            {
                if (group is SceneObjectGroup sog)
                {
                    scriptsValidForStarting += sog.CreateScriptInstances(0, false, DefaultScriptEngine, 0);
                    sog.ResumeScripts();
                }
            }

            m_log.Info($"[SCENE]: Initialized {scriptsValidForStarting} script instances in {RegionInfo.RegionName}");

            return scriptsValidForStarting;
        }

        /// <summary>
        /// Lets the script engines start processing scripts.
        /// </summary>
        public void StartScripts()
        {
            //m_log.InfoFormat("[SCENE]: Starting scripts in {0}, please wait.", RegionInfo.RegionName);

            IScriptModule[] engines = RequestModuleInterfaces<IScriptModule>();
            foreach (IScriptModule engine in engines)
                engine.StartProcessing();
        }

        public void AddUploadedInventoryItem(UUID agentID, InventoryItemBase item, uint cost)
        {
            IMoneyModule money = RequestModuleInterface<IMoneyModule>();
            money?.ApplyUploadCharge(agentID, (int)cost, "Asset upload");

            AddInventoryItem(item);
        }

        public bool AddInventoryItemReturned(UUID AgentId, InventoryItemBase item)
        {
            if (AddInventoryItem(item))
                return true;

            m_log.Warn($"[AGENT INVENTORY]: Unable to add item {item.Name} to agent {AgentId} inventory");
            return false;
        }

        public bool AddInventoryItem(InventoryItemBase item)
        {
            return AddInventoryItem(item, true);
        }

        /// <summary>
        /// Add the given inventory item to a user's inventory.
        /// </summary>
        /// <param name="item"></param>
        public bool AddInventoryItem(InventoryItemBase item, bool trigger)
        {
            if (item.Folder.IsNotZero() && InventoryService.AddItem(item))
            {
                int userlevel = Permissions.IsGod(item.Owner) ? 1 : 0;
                if (trigger)
                    EventManager.TriggerOnNewInventoryItemUploadComplete(item, userlevel);

                return true;
            }

            // OK so either the viewer didn't send a folderID or AddItem failed
            UUID originalFolder = item.Folder;
            InventoryFolderBase f = null;
            if (Enum.IsDefined(typeof(FolderType), (sbyte)item.AssetType))
                f = InventoryService.GetFolderForType(item.Owner, (FolderType)item.AssetType);
            if (f is not null)
            {
                m_log.Debug(
                    $"[AGENT INVENTORY]: Found folder {f.Name} type {(AssetType)f.Type} for item {item.Name}");

                item.Folder = f.ID;
            }
            else
            {
                f = InventoryService.GetRootFolder(item.Owner);
                if (f is not null)
                {
                    item.Folder = f.ID;
                }
                else
                {
                    m_log.Warn(
                        $"[AGENT INVENTORY]: Could not find root folder for {item.Owner} when trying to add item {item.Name} with no parent folder specified");
                    return false;
                }
            }

            if (InventoryService.AddItem(item))
            {
                int userlevel = Permissions.IsGod(item.Owner) ? 1 : 0;
                if (trigger)
                    EventManager.TriggerOnNewInventoryItemUploadComplete(item, userlevel);

                if (originalFolder.IsNotZero())
                {
                    // Tell the viewer that the item didn't go there
                    ChangePlacement(item, f);
                }

                return true;
            }
            else
            {
                m_log.Warn($"[AGENT INVENTORY]: Agent {item.Owner} could not add item {item.Name} {item.ID}");

                return false;
            }
        }

        private void ChangePlacement(InventoryItemBase item, InventoryFolderBase f)
        {
            ScenePresence sp = GetScenePresence(item.Owner);
            if (sp is not null && sp.ControllingClient is not null && sp.ControllingClient.IsActive)
            {
                IClientAPI cli = sp.ControllingClient;
                InventoryFolderBase parent = InventoryService.GetFolder(f.Owner, f.ParentID);
                cli.SendRemoveInventoryItems([item.ID]);
                cli.SendBulkUpdateInventory(Array.Empty<InventoryFolderBase>(), [item]);
                string message = "The item was placed in folder " + f.Name;
                if (parent is not null)
                    message += " under " + parent.Name;
                cli.SendAgentAlertMessage(message, false);
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
        public UUID CapsUpdateItemAsset(UUID avatarId, UUID itemID, UUID objectID, byte[] data)
        {
            if (!TryGetScenePresence(avatarId, out ScenePresence avatar))
            {
                m_log.ErrorFormat("[CapsUpdateItemAsset]: Avatar {0} cannot be found to update item asset", avatarId);
                return UUID.Zero;
            }

            if (objectID.IsZero())
            {
                IInventoryAccessModule invAccess = RequestModuleInterface<IInventoryAccessModule>();
                return invAccess is not null ? invAccess.CapsUpdateInventoryItemAsset(avatar.ControllingClient, itemID, data) : UUID.Zero;
            }

            SceneObjectPart sop = GetSceneObjectPart(objectID);
            if(sop is null || sop.ParentGroup.IsDeleted)
            {
                m_log.ErrorFormat("[CapsUpdateItemAsset]: Object {0} cannot be found to update item asset", objectID);
                return UUID.Zero;
            }

            TaskInventoryItem item = sop.Inventory.GetInventoryItem(itemID);
            if (item is null)
            {
                m_log.ErrorFormat("[CapsUpdateItemAsset]: Could not find item {0} for asset update", itemID);
                return UUID.Zero;
            }

            if (!Permissions.CanEditObjectInventory(objectID, avatarId))
                return UUID.Zero;

            InventoryType itemType = (InventoryType)item.InvType;
            switch (itemType)
            {
                case InventoryType.Notecard:
                {
                    if (!Permissions.CanEditNotecard(itemID, objectID, avatarId))
                    {
                        avatar.ControllingClient.SendAgentAlertMessage("Insufficient permissions to edit notecard", false);
                        return UUID.Zero;
                    }

                    avatar.ControllingClient.SendAlertMessage("Notecard updated");
                    break;
                }
                case (InventoryType)CustomInventoryType.AnimationSet:
                {
                    AnimationSet animSet = new(data);
                    uint res = animSet.Validate(x => {
                        const int required = (int)(PermissionMask.Transfer | PermissionMask.Copy);
                        int perms = InventoryService.GetAssetPermissions(avatarId, x);
                        // enforce previus perm rule
                        if ((perms & required) != required)
                            return 0;
                        return (uint)perms;
                    });
                    if (res == 0)
                    {
                        avatar.ControllingClient.SendAgentAlertMessage("Not enought permissions on asset(s) referenced by animation set '{0}', update failed", false);
                        return UUID.Zero;
                    }
                    break;
                }
                case InventoryType.Gesture:
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    {
                        avatar.ControllingClient.SendAgentAlertMessage("Insufficient permissions to edit gesture", false);
                        return UUID.Zero;
                    }

                    avatar.ControllingClient.SendAlertMessage("Gesture updated");
                    break;
                }
                case InventoryType.Settings:
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    {
                        avatar.ControllingClient.SendAgentAlertMessage("Insufficient permissions to edit setting", false);
                        return UUID.Zero;
                    }

                    avatar.ControllingClient.SendAlertMessage("Setting updated");
                    break;
                }
                case InventoryType.Material:
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    {
                        avatar.ControllingClient.SendAgentAlertMessage("Insufficient permissions to edit setting", false);
                        return UUID.Zero;
                    }
                    break;
                }
            }

            AssetBase asset = CreateAsset(item.Name, item.Description, (sbyte)item.Type, data, avatarId);
            item.AssetID = asset.FullID;
            AssetService.Store(asset);

            sop.Inventory.UpdateInventoryItem(item);

            // remoteClient.SendInventoryItemCreateUpdate(item);
            return asset.FullID;
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
            if (part is null)
                return new ArrayList();

            SceneObjectGroup group = part.ParentGroup;

            // Retrieve item
            TaskInventoryItem item = group.GetInventoryItem(part.LocalId, itemId);
            if (item is null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for caps script update "
                        + " but the item does not exist in this inventory",
                    itemId, part.Name, part.UUID);

                return new ArrayList();
            }

            item.ScriptRunning = isScriptRunning;
            AssetBase asset = CreateAsset(item.Name, item.Description, (sbyte)AssetType.LSLText, data, remoteClient.AgentId);
            AssetService.Store(asset);

            //m_log.DebugFormat(
            //    "[PRIM INVENTORY]: Stored asset {0} when updating item {1} in prim {2} for {3}",
            //    asset.ID, item.Name, part.Name, remoteClient.Name);

            part.Inventory.RemoveScriptInstance(item.ItemID, false);

            // Update item with new asset
            item.AssetID = asset.FullID;
            group.UpdateInventoryItem(item);
            group.InvalidateEffectivePerms();

            part.SendPropertiesToClient(remoteClient);

            // Trigger rerunning of script (use TriggerRezScript event, see RezScript)
            // Needs to determine which engine was running it and use that
            ArrayList errors = part.Inventory.CreateScriptInstanceEr(item.ItemID, 0, false, DefaultScriptEngine, 1);

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
            if (TryGetScenePresence(avatarId, out ScenePresence avatar))
            {
                return CapsUpdateTaskInventoryScriptAsset(
                    avatar.ControllingClient, itemId, primId, isScriptRunning, data);
            }
            else
            {
                m_log.ErrorFormat("[PRIM INVENTORY]: Avatar {0} cannot be found to update its prim item asset", avatarId);
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
        public void UpdateInventoryItem(IClientAPI remoteClient, UUID transactionID,
                                             UUID itemID, InventoryItemBase itemUpd)
        {
            //m_log.DebugFormat(
            //    "[USER INVENTORY]: Updating asset for item {0} {1}, transaction ID {2} for {3}",
            //    itemID, itemUpd.Name, transactionID, remoteClient.Name);

            // This one will let people set next perms on items in agent
            // inventory. Rut-Roh. Whatever. Make this secure. Yeah.
            //
            // Passing something to another avatar or a an object will already
            InventoryItemBase item = InventoryService.GetItem(remoteClient.AgentId, itemID);
            if (item is not null)
            {
                if (item.Owner.NotEqual(remoteClient.AgentId))
                    return;

                bool sendUpdate = false;

                item.Flags = (item.Flags & ~(uint)255) | (itemUpd.Flags & (uint)255);
                if(item.AssetType == (int)AssetType.Landmark)
                {
                    if(item.Name.StartsWith("HG ") && !itemUpd.Name.StartsWith("HG "))
                    {
                        itemUpd.Name = "HG " + itemUpd.Name;
                        sendUpdate = true;
                    }

                    int origIndx = item.Description.LastIndexOf("@ htt");
                    if(origIndx >= 0)
                    {
                        if(itemUpd.Description.LastIndexOf('@') < 0)
                        {
                            itemUpd.Description += string.Concat(" ", item.Description.AsSpan(origIndx));
                            sendUpdate = true;
                        }
                    }
                }
                item.Name = itemUpd.Name;
                item.Description = itemUpd.Description;

                //m_log.DebugFormat(
                //    "[USER INVENTORY]: itemUpd {0} {1} {2} {3}, item {4} {5} {6} {7}",
                //    itemUpd.NextPermissions, itemUpd.GroupPermissions, itemUpd.EveryOnePermissions, item.Flags,
                //    item.NextPermissions, item.GroupPermissions, item.EveryOnePermissions, item.CurrentPermissions);

                if (itemUpd.NextPermissions != 0) // Use this to determine validity. Can never be 0 if valid
                {
                    // Create a set of base permissions that will not include export if the user
                    // is not allowed to change the export flag.
                    bool denyExportChange = false;

                    //m_log.DebugFormat("[XXX]: B: {0} O: {1} E: {2}", itemUpd.BasePermissions, itemUpd.CurrentPermissions, itemUpd.EveryOnePermissions);

                    // If the user is not the creator or doesn't have "E" in both "B" and "O", deny setting export
                    if ((item.BasePermissions & (uint)(PermissionMask.All | PermissionMask.Export)) != (uint)(PermissionMask.All | PermissionMask.Export) || (item.CurrentPermissions & (uint)PermissionMask.Export) == 0 || item.CreatorIdAsUuid != item.Owner)
                        denyExportChange = true;

                    //m_log.DebugFormat("[XXX]: Deny Export Update {0}", denyExportChange);

                    // If it is already set, force it set and also force full perm
                    // else prevent setting it. It can and should never be set unless
                    // set in base, so the condition above is valid
                    if (denyExportChange)
                    {
                        // If we are not allowed to change it, then force it to the
                        // original item's setting and if it was on, also force full perm
                        if ((item.EveryOnePermissions & (uint)PermissionMask.Export) != 0)
                        {
                            itemUpd.NextPermissions = (uint)(PermissionMask.All);
                            itemUpd.EveryOnePermissions |= (uint)PermissionMask.Export;
                        }
                        else
                        {
                            itemUpd.EveryOnePermissions &= ~(uint)PermissionMask.Export;
                        }
                    }
                    else
                    {
                        // If the new state is exportable, force full perm
                        if ((itemUpd.EveryOnePermissions & (uint)PermissionMask.Export) != 0)
                        {
                            //m_log.DebugFormat("[XXX]: Force full perm");
                            itemUpd.NextPermissions = (uint)(PermissionMask.All);
                        }
                    }

                    if (item.NextPermissions != (itemUpd.NextPermissions & item.BasePermissions))
                        item.Flags |= (uint)InventoryItemFlags.ObjectOverwriteNextOwner;
                    item.NextPermissions = itemUpd.NextPermissions & item.BasePermissions;

                    if (item.EveryOnePermissions != (itemUpd.EveryOnePermissions & item.BasePermissions))
                        item.Flags |= (uint)InventoryItemFlags.ObjectOverwriteEveryone;
                    item.EveryOnePermissions = itemUpd.EveryOnePermissions & item.BasePermissions;

                    if (item.GroupPermissions != (itemUpd.GroupPermissions & item.BasePermissions))
                        item.Flags |= (uint)InventoryItemFlags.ObjectOverwriteGroup;
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

                    if (item.InvType == (int)InventoryType.Wearable && (item.Flags & 0xf) == 0 && (itemUpd.Flags & 0xf) != 0)
                    {
                        item.Flags = (item.Flags & 0xfffffff0) | (itemUpd.Flags & 0xf);
                        sendUpdate = true;
                    }

                    InventoryService.UpdateItem(item);
                }

                if (transactionID.IsNotZero())
                {
                    AgentTransactionsModule?.HandleItemUpdateFromTransaction(remoteClient, transactionID, item);
                }
                else
                {
                    // This MAY be problematic, if it is, another solution
                    // needs to be found. If inventory item flags are updated
                    // the viewer's notion of the item needs to be refreshed.
                    //
                    // In other situations we cannot send out a bulk update here, since this will cause editing of clothing to start
                    // failing frequently.  Possibly this is a race with a separate transaction that uploads the asset.
                    if (sendUpdate)
                        remoteClient.SendBulkUpdateInventory(item);
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
        public virtual void GiveInventoryItem(IClientAPI recipientClient, UUID senderId, UUID itemId, out string message)
        {
            InventoryItemBase itemCopy = GiveInventoryItem(recipientClient.AgentId, senderId, itemId, out message);
            if (itemCopy is not null)
                recipientClient.SendBulkUpdateInventory(itemCopy);
        }

        /// <summary>
        /// Give an inventory item from one user to another
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="senderId">ID of the sender of the item</param>
        /// <param name="itemId"></param>
        /// <returns>The inventory item copy given, null if the give was unsuccessful</returns>
        public virtual InventoryItemBase GiveInventoryItem(UUID recipient, UUID senderId, UUID itemId, out string message)
        {
            return GiveInventoryItem(recipient, senderId, itemId, UUID.Zero, out message);
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
            UUID recipient, UUID senderId, UUID itemId, UUID recipientFolderId, out string message)
        {
            //Console.WriteLine("Scene.Inventory.cs: GiveInventoryItem");

            if (!Permissions.CanTransferUserInventory(itemId, senderId, recipient))
            {
                message = "Not allowed to transfer this item.";
                return null;
            }

            InventoryItemBase item = InventoryService.GetItem(senderId, itemId);
            if (item is null)
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Failed to find item {0} sent by {1} to {2}", itemId, senderId, recipient);
                message = string.Format("Item not found: {0}.", itemId);
                return null;
            }

            if (item.AssetType == (int)AssetType.Link || item.AssetType == (int)AssetType.LinkFolder)
            {
                message = "inventory links not supported.";
                return null;
            }

            if (item.Owner.NotEqual(senderId))
            {
                m_log.WarnFormat(
                    "[AGENT INVENTORY]: Attempt to send item {0} {1} to {2} failed because sender {3} did not match item owner {4}",
                    item.Name, item.ID, recipient, senderId, item.Owner);
                message = "Sender did not match item owner.";
                return null;
            }

            IUserManagement uman = RequestModuleInterface<IUserManagement>();
            uman?.AddCreatorUser(item.CreatorIdAsUuid, item.CreatorData);

            if (!Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Transfer) == 0)
                {
                    message = "Item doesn't have the Transfer permission.";
                    return null;
                }
            }

            // Insert a copy of the item into the recipient
            InventoryItemBase itemCopy = new()
            {
                Owner = recipient,
                CreatorId = item.CreatorId,
                CreatorData = item.CreatorData,
                ID = UUID.Random(),
                AssetID = item.AssetID,
                Description = item.Description,
                Name = item.Name,
                AssetType = item.AssetType,
                InvType = item.InvType,
                Folder = recipientFolderId,
                Flags = item.Flags
            };

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
                // Modify
                uint permsMask = ~ ((uint)PermissionMask.Copy |
                                    (uint)PermissionMask.Transfer |
                                    (uint)PermissionMask.Modify |
                                    (uint)PermissionMask.Export);

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

                // These will be applied to the root prim at next rez.
                // The legacy slam bit (bit 3) and folded permission (bits 0-2)
                // are preserved due to the above mangling
                //ownerPerms &= nextPerms;

                // Mask the base permissions. This is a conservative
                // approach altering only the three main perms
                //basePerms &= nextPerms;

                // Mask out the folded portion of the base mask.
                // While the owner mask carries the actual folded
                // permissions, the base mask carries the original
                // base mask, before masking with the folded perms.
                // We need this later for rezzing.
                //basePerms &= ~(uint)PermissionMask.FoldedMask;
                //basePerms |= ((basePerms >> 13) & 7) | (((basePerms & (uint)PermissionMask.Export) != 0) ? (uint)PermissionMask.FoldedExport : 0);

                // If this is an object, root prim perms may be more
                // permissive than folded perms. Use folded perms as
                // a mask
                uint foldedPerms = (item.CurrentPermissions & (uint)PermissionMask.FoldedMask) << (int)PermissionMask.FoldingShift;
                if (foldedPerms != 0 && item.InvType == (int)InventoryType.Object)
                {
                    foldedPerms |= permsMask;

                    bool isRootMod = (item.CurrentPermissions & (uint)PermissionMask.Modify) != 0;

                    // Mask the owner perms to the folded perms
                    // Note that this is only to satisfy the viewer.
                    // The effect of this will be reversed on rez.
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

                // move here so nextperms are mandatory
                ownerPerms &= nextPerms;
                basePerms &= nextPerms;
                basePerms &= ~(uint)PermissionMask.FoldedMask;
                basePerms |= ((basePerms >> 13) & 7) | (((basePerms & (uint)PermissionMask.Export) != 0) ? (uint)PermissionMask.FoldedExport : 0);
                // Assign to the actual item. Make sure the slam bit is
                // set, if it wasn't set before.
                itemCopy.BasePermissions = basePerms;
                itemCopy.CurrentPermissions = ownerPerms;
                itemCopy.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                // Need to clear the other inventory slam options.
                // That is so we can handle the case where the recipient
                // changes the bits in inventory before rezzing
                itemCopy.Flags &= ~(uint)(InventoryItemFlags.ObjectOverwriteBase | InventoryItemFlags.ObjectOverwriteOwner | InventoryItemFlags.ObjectOverwriteGroup | InventoryItemFlags.ObjectOverwriteEveryone | InventoryItemFlags.ObjectOverwriteNextOwner);

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

            if (itemCopy.Folder.IsZero())
            {
                InventoryFolderBase folder = null;
                if (Enum.IsDefined(typeof(FolderType), (sbyte)item.AssetType))
                    folder = InventoryService.GetFolderForType(recipient, (FolderType)itemCopy.AssetType);

                if (folder is not null)
                {
                    itemCopy.Folder = folder.ID;
                }
                else
                {
                    InventoryFolderBase root = InventoryService.GetRootFolder(recipient);

                    if (root is not null)
                    {
                        itemCopy.Folder = root.ID;
                    }
                    else
                    {
                        message = "Can't find a folder to add the item to.";
                        return null;
                    }
                }
            }

            itemCopy.GroupID = UUID.Zero;
            itemCopy.GroupOwned = false;
            itemCopy.SalePrice = 0; //item.SalePrice;
            itemCopy.SaleType = 0; //item.SaleType;

            IInventoryAccessModule invAccess = RequestModuleInterface<IInventoryAccessModule>();
            invAccess?.TransferInventoryAssets(itemCopy, senderId, recipient);
            AddInventoryItem(itemCopy, false);

            if (!Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                {
                    List<UUID> items = new() { itemId };
                    InventoryService.DeleteItems(senderId, items);
                }
            }

            message = null;
            return itemCopy;
        }

        private readonly HashSet<short> denyGiveFolderTypes = new()
            {
                (short)FolderType.Trash,
                (short)FolderType.LostAndFound,
                (short)FolderType.CurrentOutfit,
                (short)FolderType.Inbox,
                (short)FolderType.Outbox,
                (short)FolderType.Suitcase,
                (short)FolderType.Root
            };
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
        public virtual InventoryFolderBase GiveInventoryFolder(IClientAPI client,
            UUID recipientId, UUID senderId, UUID folderId, UUID recipientParentFolderId)
        {
            //// Retrieve the folder from the sender
            InventoryFolderBase folder = InventoryService.GetFolder(senderId, folderId);
            if (folder is null)
            {
                m_log.ErrorFormat("[AGENT INVENTORY]: Could not find inventory folder {0} to give", folderId);
                return null;
            }

            if (denyGiveFolderTypes.Contains(folder.Type))
            {
                m_log.ErrorFormat("[AGENT INVENTORY]: can not give inventory folder {0}", folderId);
                return null;
            }

            if (recipientParentFolderId.IsZero())
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
            InventoryFolderBase newFolder = new(
                    newFolderId, folder.Name, recipientId, folder.Type, recipientParentFolderId, folder.Version);
            InventoryService.AddFolder(newFolder);

            // Give all the subfolders
            InventoryCollection contents = InventoryService.GetFolderContent(senderId, folderId);

            // Give all the items (first, this is recursive call)
            foreach (InventoryItemBase item in contents.Items)
            {
                if (GiveInventoryItem(recipientId, senderId, item.ID, newFolder.ID, out string message) is null)
                {
                    client?.SendAgentAlertMessage(message, false);
                }
            }
            contents.Items = null;

            foreach (InventoryFolderBase childFolder in contents.Folders)
            {
                GiveInventoryFolder(client, recipientId, senderId, childFolder.ID, newFolder.ID);
            }

            return newFolder;
        }

        public virtual InventoryFolderBase GiveInventoryFolder(IClientAPI client,
            UUID recipientId, UUID senderId, UUID folderId, UUID recipientParentFolderId, Dictionary<UUID, AssetType> ids)
        {
            //// Retrieve the folder from the sender
            InventoryFolderBase folder = InventoryService.GetFolder(senderId, folderId);
            if (folder is null)
            {
                m_log.ErrorFormat("[AGENT INVENTORY]: Could not find inventory folder {0} to give", folderId);
                return null;
            }

            if(!ids.Remove(folder.ID))
                return null;

            if (denyGiveFolderTypes.Contains(folder.Type))
            {
                m_log.ErrorFormat("[AGENT INVENTORY]: can not give inventory folder {0}", folderId);
                return null;
            }

            if (recipientParentFolderId.IsZero())
            {
                InventoryFolderBase recipientRootFolder = InventoryService.GetRootFolder(recipientId);
                if (recipientRootFolder is not null)
                    recipientParentFolderId = recipientRootFolder.ID;
                else
                {
                    m_log.WarnFormat("[AGENT INVENTORY]: Unable to find root folder for receiving agent");
                    return null;
                }
            }

            UUID newFolderId = UUID.Random();
            InventoryFolderBase newFolder = new(
                    newFolderId, folder.Name, recipientId, folder.Type, recipientParentFolderId, folder.Version);
            InventoryService.AddFolder(newFolder);

            InventoryCollection contents = InventoryService.GetFolderContent(senderId, folderId);
            if(client is null)
            {
                foreach (InventoryItemBase item in contents.Items)
                {
                    if (ids.Remove(item.ID))
                        GiveInventoryItem(recipientId, senderId, item.ID, newFolder.ID, out string _);
                }
            }
            else
            {
                foreach (InventoryItemBase item in contents.Items)
                {
                    if (ids.Remove(item.ID))
                    {
                        if (GiveInventoryItem(recipientId, senderId, item.ID, newFolder.ID, out string message) == null)
                            client.SendAgentAlertMessage(message, false);
                    }
                }
            }
            contents.Items = null;

            foreach (InventoryFolderBase childFolder in contents.Folders)
            {
                GiveInventoryFolder(client, recipientId, senderId, childFolder.ID, newFolder.ID);
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
            if (LibraryService is not null && LibraryService.LibraryRootFolder is not null)
                item = LibraryService.LibraryRootFolder.FindItem(oldItemID);

            if (item is null)
            {
                item = InventoryService.GetItem(remoteClient.AgentId, oldItemID);

                if (item is null)
                {
                    m_log.Error("[AGENT INVENTORY]: Failed to find item " + oldItemID.ToString());
                    return;
                }

                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    return;
            }

            if (string.IsNullOrEmpty(newName))
                newName = item.Name;

            if (remoteClient.AgentId.Equals(oldAgentID)
                || (LibraryService is not null
                    && LibraryService.LibraryRootFolder is not null
                    && oldAgentID.Equals(LibraryService.LibraryRootFolder.Owner)))
            {
                CreateNewInventoryItem(
                    remoteClient, item.CreatorId, item.CreatorData, newFolderID,
                    newName, item.Description, item.Flags, callbackID, item.AssetID, (sbyte)item.AssetType, (sbyte)item.InvType,
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
                        item.AssetID, (sbyte)item.AssetType, (sbyte)item.InvType,
                        item.NextPermissions, item.NextPermissions, item.EveryOnePermissions & item.NextPermissions,
                        item.NextPermissions, item.GroupPermissions, Util.UnixTimeSinceEpoch());
                }
            }
        }

        /// <summary>
        /// Create a new asset data structure.
        /// </summary>
        public AssetBase CreateAsset(string name, string description, sbyte assetType, byte[] data, UUID creatorID)
        {
            return new AssetBase(UUID.Random(), name, assetType, creatorID.ToString())
            {
                Description = description,
                Data = data ?? (new byte[1])
            };
        }

        /// <summary>
        /// Move an item within the agent's inventory, and leave a copy (used in making a new outfit)
        /// </summary>
        public void MoveInventoryItemsLeaveCopy(IClientAPI remoteClient, List<InventoryItemBase> items, UUID destfolder)
        {
            List<InventoryItemBase> moveitems = new();
            foreach (InventoryItemBase b in items)
            {
                CopyInventoryItem(remoteClient, 0, remoteClient.AgentId, b.ID, b.Folder, null);
                InventoryItemBase n = InventoryService.GetItem(b.Owner, b.ID);
                n.Folder = destfolder;
                moveitems.Add(n);
                remoteClient.SendInventoryItemCreateUpdate(n, 0);
            }

            MoveInventoryItem(remoteClient, moveitems);
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
            UUID agentId = remoteClient.AgentId;
            m_log.DebugFormat(
                "[AGENT INVENTORY]: Moving {0} items for user {1}", items.Count, agentId);

            if (!InventoryService.MoveItems(agentId, items))
                m_log.Warn("[AGENT INVENTORY]: Failed to move items for user " + agentId);

            foreach (InventoryItemBase it in items)
            {
                InventoryItemBase n = InventoryService.GetItem(agentId, it.ID);
                if(n is not null)
                    remoteClient.SendBulkUpdateInventory(n);
            }
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
        public void CreateNewInventoryItem(
            IClientAPI remoteClient, string creatorID, string creatorData, UUID folderID,
            string name, string description, uint flags, uint callbackID, UUID assetID, sbyte assetType, sbyte invType,
            uint baseMask, uint currentMask, uint everyoneMask, uint nextOwnerMask, uint groupMask, int creationDate,
            bool assetUpload)
        {
            CreateNewInventoryItem(
                remoteClient, creatorID, creatorData, folderID,
                name, description, flags, callbackID, assetID, assetType, invType,
                baseMask, currentMask, everyoneMask, nextOwnerMask, groupMask, creationDate);
        }

        public void CreateNewInventoryItem(
            IClientAPI remoteClient, string creatorID, string creatorData, UUID folderID,
            string name, string description, uint flags, uint callbackID, UUID assetID, sbyte assetType, sbyte invType,
            uint baseMask, uint currentMask, uint everyoneMask, uint nextOwnerMask, uint groupMask, int creationDate)
        {
            InventoryItemBase item = new()
            {
                Owner = remoteClient.AgentId,
                CreatorId = creatorID,
                CreatorData = creatorData,
                ID = UUID.Random(),
                AssetID = assetID,
                Name = name,
                Description = description,
                Flags = flags,
                AssetType = assetType,
                InvType = invType,
                Folder = folderID,
                CurrentPermissions = currentMask,
                NextPermissions = nextOwnerMask,
                EveryOnePermissions = everyoneMask,
                GroupPermissions = groupMask,
                BasePermissions = baseMask,
                CreationDate = creationDate
            };
            // special AnimationSet case
            if (item.InvType == (int)CustomInventoryType.AnimationSet)
                AnimationSet.enforceItemPermitions(item,true);
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
            //m_log.DebugFormat(
            //    "[AGENT INVENTORY]: Received request from {0} to create inventory item link {1} in folder {2} pointing to {3}, assetType {4}, inventoryType {5}",
            //    remoteClient.Name, name, folderID, olditemID, (AssetType)type, (InventoryType)invType);

            if (!Permissions.CanCreateUserInventory(invType, remoteClient.AgentId))
                return;

            if (TryGetScenePresence(remoteClient.AgentId, out ScenePresence _))
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

                CreateNewInventoryItem(
                    remoteClient, remoteClient.AgentId.ToString(), string.Empty, folderID,
                    name, description, 0, callbackID, olditemID, type, invType,
                    (uint)PermissionMask.All | (uint)PermissionMask.Export, (uint)PermissionMask.All | (uint)PermissionMask.Export, (uint)PermissionMask.All,
                    (uint)PermissionMask.All | (uint)PermissionMask.Export, (uint)PermissionMask.All | (uint)PermissionMask.Export, Util.UnixTimeSinceEpoch(),
                    false);
            }
            else
            {
                m_log.Error($"[HandleLinkInventoryItem] ScenePresence for agent {remoteClient.AgentId} not found");
            }
        }

        /// <summary>
        /// Remove an inventory item for the client's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        private void RemoveInventoryItem(IClientAPI remoteClient, List<UUID> itemIDs)
        {
            //m_log.DebugFormat(
            //    "[AGENT INVENTORY]: Removing inventory items {0} for {1}",
            //    string.Join(",", itemIDs.ConvertAll<string>(uuid => uuid.ToString()).ToArray()),
            //    remoteClient.Name);

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
            if (part is null)
                return;

            if (XferManager is not null)
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
            if (part is null)
                return;

            SceneObjectGroup group =  part.ParentGroup;
            if(group is null)
                return;

            if (!Permissions.CanEditObjectInventory(part.UUID, remoteClient.AgentId))
                return;

            TaskInventoryItem item = group.GetInventoryItem(localID, itemID);
            if (item is null)
                return;

            InventoryFolderBase destFolder = InventoryService.GetFolderForType(remoteClient.AgentId, FolderType.Trash);

            // Move the item to trash. If this is a copyable item, only
            // a copy will be moved and we will still need to delete
            // the item from the prim. If it was no copy, it will be
            // deleted by this method.
            InventoryItemBase item2 = MoveTaskInventoryItem(remoteClient, destFolder.ID, part, itemID, out string message);

            if (item2 is null)
            {
                m_log.WarnFormat("[SCENE INVENTORY]: RemoveTaskInventory of item {0} failed: {1}", itemID, message);
                remoteClient.SendAgentAlertMessage(message, false);
                return;
            }

            if (group.GetInventoryItem(localID, itemID) is not null)
            {
                if (item.Type == (int)InventoryType.LSL)
                {
                    part.RemoveScriptEvents(itemID);
                    part.ParentGroup.AddActiveScriptCount(-1);
                }

                group.RemoveInventoryItem(localID, itemID);
                group.InvalidateEffectivePerms();
            }

            part.SendPropertiesToClient(remoteClient);
        }


        /// <summary>
        /// Creates (in memory only) a user inventory item that will contain a copy of a task inventory item.
        /// </summary>
        private InventoryItemBase CreateAgentInventoryItemFromTask(UUID destAgent, SceneObjectPart part, UUID itemId, out string message)
        {
            TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(itemId);
            if (taskItem is null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for creating an avatar"
                        + " inventory item from a prim's inventory item "
                        + " but the required item does not exist in the prim's inventory",
                    itemId, part.Name, part.UUID);
                message = "Item not found: " + itemId;
                return null;
            }

            if ((destAgent != taskItem.OwnerID) && ((taskItem.CurrentPermissions & (uint)PermissionMask.Transfer) == 0))
            {
                message = "Item doesn't have the Transfer permission.";
                return null;
            }

            InventoryItemBase agentItem = new()
            {
                ID = UUID.Random(),
                CreatorId = taskItem.CreatorID.ToString(),
                CreatorData = taskItem.CreatorData,
                Owner = destAgent,
                AssetID = taskItem.AssetID,
                Description = taskItem.Description,
                Name = taskItem.Name,
                AssetType = taskItem.Type,
                InvType = taskItem.InvType,
                Flags = taskItem.Flags
            };

            // The code below isn't OK. It doesn't account for flags being changed
            // in the object inventory, so it will break when you do it. That
            // is the previous behaviour, so no matter at this moment. However, there is a lot
            // TODO: Fix this after the inventory fixer exists and has beenr run
            if (part.OwnerID.NotEqual(destAgent) && Permissions.PropagatePermissions())
            {
                uint perms = taskItem.BasePermissions & taskItem.NextPermissions;
                if (taskItem.InvType == (int)InventoryType.Object)
                {
                     PermissionsUtil.ApplyFoldedPermissions(taskItem.CurrentPermissions, ref perms );
                     perms = PermissionsUtil.FixAndFoldPermissions(perms);
                }
                else
                    perms &= taskItem.CurrentPermissions;

                // always unlock
                perms |= (uint)PermissionMask.Move;
                            
                agentItem.BasePermissions = perms;
                agentItem.CurrentPermissions = perms;
                agentItem.NextPermissions = perms & taskItem.NextPermissions;
                agentItem.EveryOnePermissions = perms & taskItem.EveryonePermissions;
                agentItem.GroupPermissions = perms & taskItem.GroupPermissions;
 
                agentItem.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                agentItem.Flags &= ~(uint)(InventoryItemFlags.ObjectOverwriteBase | InventoryItemFlags.ObjectOverwriteOwner | InventoryItemFlags.ObjectOverwriteGroup | InventoryItemFlags.ObjectOverwriteEveryone | InventoryItemFlags.ObjectOverwriteNextOwner);
            }
            else
            {
                agentItem.BasePermissions = taskItem.BasePermissions;
                agentItem.CurrentPermissions = taskItem.CurrentPermissions;
                agentItem.NextPermissions = taskItem.NextPermissions;
                agentItem.EveryOnePermissions = taskItem.EveryonePermissions;
                agentItem.GroupPermissions = taskItem.GroupPermissions;
            }

            message = null;
            return agentItem;
        }

        /// <summary>
        /// If the task item is not-copyable then remove it from the prim.
        /// </summary>
        private void RemoveNonCopyTaskItemFromPrim(SceneObjectPart part, UUID itemId)
        {
            TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(itemId);
            if (taskItem is null)
                return;

            if (!Permissions.BypassPermissions())
            {
                if ((taskItem.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                {
                    if (taskItem.Type == (int)AssetType.LSLText)
                    {
                        part.RemoveScriptEvents(itemId);
                        part.ParentGroup.AddActiveScriptCount(-1);
                    }

                    part.Inventory.RemoveInventoryItem(itemId);
                }
            }
        }

        /// <summary>
        /// Move the given item in the given prim to a folder in the client's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="part"></param>
        /// <param name="itemID"></param>
        public InventoryItemBase MoveTaskInventoryItem(IClientAPI remoteClient, UUID folderId, SceneObjectPart part, UUID itemId, out string message)
        {
            m_log.DebugFormat(
                "[PRIM INVENTORY]: Adding item {0} from {1} to folder {2} for {3}",
                itemId, part.Name, folderId, remoteClient.Name);

            InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(remoteClient.AgentId, part, itemId, out message);
            if (agentItem is null)
                return null;

            agentItem.Folder = folderId;
            AddInventoryItem(remoteClient, agentItem);

            RemoveNonCopyTaskItemFromPrim(part, itemId);

            message = null;
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
            // Can't move a null item
            if (itemId.IsZero())
                return;
            SceneObjectPart part = GetSceneObjectPart(primLocalId);
            if (part is null)
            {
                m_log.WarnFormat(
                    "[PRIM INVENTORY]: " +
                    "Move of inventory item {0} from prim with local id {1} failed because the prim could not be found",
                    itemId, primLocalId);

                return;
            }

            TaskInventoryItem taskItem = part.Inventory.GetInventoryItem(itemId);
            if (taskItem is null)
            {
                m_log.WarnFormat("[PRIM INVENTORY]: Move of inventory item {0} from prim with local id {1} failed"
                    + " because the inventory item could not be found",
                    itemId, primLocalId);

                return;
            }

            if (!Permissions.CanCopyObjectInventory(itemId, part.UUID, remoteClient.AgentId))
                return;

            InventoryItemBase item = MoveTaskInventoryItem(remoteClient, folderId, part, itemId, out string message);
            if (item is null)
                remoteClient.SendAgentAlertMessage(message, false);
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
        public InventoryItemBase MoveTaskInventoryItem(UUID avatarId, UUID folderId, SceneObjectPart part, UUID itemId, out string message)
        {
            if (TryGetScenePresence(avatarId, out ScenePresence avatar))
            {
                return MoveTaskInventoryItem(avatar.ControllingClient, folderId, part, itemId, out message);
            }
            else
            {
                InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(avatarId, part, itemId, out message);
                if (agentItem is null)
                    return null;

                agentItem.Folder = folderId;

                AddInventoryItem(agentItem);

                RemoveNonCopyTaskItemFromPrim(part, itemId);

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
            if (srcTaskItem is null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for moving"
                        + " but the item does not exist in this inventory",
                    itemId, part.Name, part.UUID);

                return;
            }

            SceneObjectPart destPart = GetSceneObjectPart(destId);
            if (destPart is null)
            {
                m_log.ErrorFormat(
                        "[PRIM INVENTORY]: " +
                        "Could not find prim for ID {0}",
                        destId);
                return;
            }

            if(!Permissions.CanDoObjectInvToObjectInv(srcTaskItem, part, destPart))
                return;

            TaskInventoryItem destTaskItem = new()
            {
                ItemID = UUID.Random(),
                CreatorID = srcTaskItem.CreatorID,
                CreatorData = srcTaskItem.CreatorData,
                AssetID = srcTaskItem.AssetID,
                GroupID = destPart.GroupID,
                OwnerID = destPart.OwnerID,
                ParentID = destPart.UUID,
                ParentPartID = destPart.UUID,

                BasePermissions = srcTaskItem.BasePermissions,
                EveryonePermissions = srcTaskItem.EveryonePermissions,
                GroupPermissions = srcTaskItem.GroupPermissions,
                CurrentPermissions = srcTaskItem.CurrentPermissions,
                NextPermissions = srcTaskItem.NextPermissions,
                Flags = srcTaskItem.Flags
            };

            if (destPart.OwnerID.NotEqual(part.OwnerID))
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
            {
                part.Inventory.RemoveInventoryItem(itemId);
            }

            if (TryGetScenePresence(srcTaskItem.OwnerID, out ScenePresence avatar))
            {
                destPart.SendPropertiesToClient(avatar.ControllingClient);
            }
        }

        public UUID MoveTaskInventoryItems(UUID destID, string category, SceneObjectPart host, List<UUID> items, bool sendUpdates = true)
        {
            SceneObjectPart destPart = GetSceneObjectPart(destID);
            if (destPart is not null) // Move into a prim
            {
                foreach(UUID itemID in items)
                    MoveTaskInventoryItem(destID, host, itemID);
                return destID; // Prim folder ID == prim ID
            }

            // move to a avatar inventory
            IClientAPI remoteClient;
            if (TryGetScenePresence(destID, out ScenePresence avatar))
                remoteClient = avatar.ControllingClient;
            else
                return UUID.Zero;

            if(remoteClient is null)
                return UUID.Zero;

            InventoryFolderBase rootFolder = InventoryService.GetRootFolder(destID);
            if(rootFolder is null)
                return UUID.Zero;

            UUID newFolderID = UUID.Random();
            InventoryFolderBase newFolder = new(newFolderID, category, destID, -1, rootFolder.ID, rootFolder.Version);
            InventoryService.AddFolder(newFolder);

            foreach (UUID itemID in items)
            {
                InventoryItemBase agentItem = CreateAgentInventoryItemFromTask(destID, host, itemID, out string message);
                if (agentItem is not null)
                {
                    agentItem.Folder = newFolderID;
                    AddInventoryItem(agentItem);
                    RemoveNonCopyTaskItemFromPrim(host, itemID);
                }
                else
                {
                    remoteClient.SendAgentAlertMessage(message, false);
                }
            }

            if(sendUpdates)
            {
                SendInventoryUpdate(remoteClient, rootFolder, true, false);
                SendInventoryUpdate(remoteClient, newFolder, false, true);
            }

            return newFolderID;
        }

        public void SendInventoryUpdate(IClientAPI client, InventoryFolderBase folder, bool fetchFolders, bool fetchItems)
        {
            if (folder is null)
                return;

            // TODO: This code for looking in the folder for the library should be folded somewhere else
            // so that this class doesn't have to know the details (and so that multiple libraries, etc.
            // can be handled transparently).
            InventoryFolderImpl fold;
            if (LibraryService is not null && LibraryService.LibraryRootFolder is not null)
            {
                if ((fold = LibraryService.LibraryRootFolder.FindFolder(folder.ID)) is not null)
                {
                    List<InventoryItemBase> its = fold.RequestListOfItems();
                    List<InventoryFolderBase> fds = fold.RequestListOfFolders();
                    client.SendInventoryFolderDetails(
                        fold.Owner, folder.ID, its, fds,
                        fold.Version, its.Count + fds.Count, fetchFolders, fetchItems);
                    return;
                }
            }

            // Fetch the folder contents
            InventoryCollection contents = InventoryService.GetFolderContent(client.AgentId, folder.ID);

            // Fetch the folder itself to get its current version
            InventoryFolderBase containingFolder = InventoryService.GetFolder(client.AgentId, folder.ID);

            //m_log.DebugFormat("[AGENT INVENTORY]: Sending inventory folder contents ({0} nodes) for \"{1}\" to {2} {3}",
            //    contents.Folders.Count + contents.Items.Count, containingFolder.Name, client.FirstName, client.LastName);

            if (containingFolder is not null )
            {
                int descendents = contents.Folders.Count + contents.Items.Count;

                if(fetchItems && contents.Items.Count > 0)
                {
                    var linksIDs = new HashSet<UUID>();
                    var links = new List<InventoryItemBase>();
                    for (int i = 0; i < contents.Items.Count; ++i)
                    {
                        InventoryItemBase item = contents.Items[i];
                        if (item.AssetType == (int)AssetType.Link)
                        {
                            if(linksIDs.Contains(item.AssetID))
                                continue;

                            InventoryItemBase linkedItem = InventoryService.GetItem(item.Owner, item.AssetID);
                            if (linkedItem is not null && linkedItem.AssetType != (int)AssetType.Link &&
                                    linkedItem.AssetType != (int)AssetType.LinkFolder)
                            {
                                links.Add(linkedItem);
                                linksIDs.Add(linkedItem.ID);
                            }
                        }
                    }
                    if(links.Count > 0)
                    {
                        links.AddRange(contents.Items);
                        contents.Items = links;
                    }
                }

                client.SendInventoryFolderDetails(
                    client.AgentId, folder.ID, contents.Items, contents.Folders,
                    containingFolder.Version, descendents, fetchFolders, fetchItems);
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
            if (itemID.IsZero())
            {
                m_log.Error($"[PRIM INVENTORY]: UpdateTaskInventory called with item ID Zero on update for {remoteClient.Name}");
                return;
            }

            // Find the prim we're dealing with
            SceneObjectPart part = GetSceneObjectPart(primLocalID);
            if(part is null)
            {
                m_log.Warn(
                    $"[PRIM INVENTORY]: Update prim {primLocalID} with item {itemID} by {remoteClient.Name} but prim not found");
                return;
            }

            TaskInventoryItem currentItem = part.Inventory.GetInventoryItem(itemID);

            if (currentItem == null)
            {
                InventoryItemBase item = InventoryService.GetItem(remoteClient.AgentId, itemID);

                // if not found Try library
                if (item is null && LibraryService is not null && LibraryService.LibraryRootFolder is not null)
                    item = LibraryService.LibraryRootFolder.FindItem(itemID);

                if(item is null)
                {
                    m_log.Error(
                            $"[PRIM INVENTORY]: Could not find inventory item {itemID} to update for {remoteClient.Name}");
                    return;
                }

                if (!Permissions.CanDropInObjectInv(item, remoteClient, part))
                    return;

                bool modrights = Permissions.CanEditObject(part.ParentGroup, remoteClient);
                part.ParentGroup.AddInventoryItem(remoteClient.AgentId, primLocalID, item, UUID.Random(), modrights);
                m_log.Info(
                    $"[PRIM INVENTORY]: Update prim {primLocalID} with item {item.Name} requested by {remoteClient.Name}");

                IInventoryAccessModule invAccess = RequestModuleInterface<IInventoryAccessModule>();
                invAccess?.FetchRemoteHGItemAssets(remoteClient.AgentId, item);

                part.SendPropertiesToClient(remoteClient);
                if (!Permissions.BypassPermissions())
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                        RemoveInventoryItem(remoteClient, [itemID]);
                }
            }
            else // Updating existing item with new perms etc
            {
                //m_log.DebugFormat(
                //    "[PRIM INVENTORY]: Updating item {0} in {1} for UpdateTaskInventory()",
                //    currentItem.Name, part.Name);

                if (!Permissions.CanEditObjectInventory(part.UUID, remoteClient.AgentId))
                    return;

                // Only look for an uploaded updated asset if we are passed a transaction ID.  This is only the
                // case for updates uploded through UDP.  Updates uploaded via a capability (e.g. a script update)
                // will not pass in a transaction ID in the update message.
                if (transactionID.IsNotZero() && AgentTransactionsModule is not null)
                {
                    AgentTransactionsModule.HandleTaskItemUpdateFromTransaction(
                        remoteClient, part, transactionID, currentItem);

                    //if ((InventoryType)itemInfo.InvType == InventoryType.Notecard)
                    //    remoteClient.SendAgentAlertMessage("Notecard saved", false);
                    //else if ((InventoryType)itemInfo.InvType == InventoryType.LSL)
                    //    remoteClient.SendAgentAlertMessage("Script saved", false);
                    //else
                    //    remoteClient.SendAgentAlertMessage("Item saved", false);
                }

                // Base ALWAYS has move
                currentItem.BasePermissions |= (uint)PermissionMask.Move;

                itemInfo.Flags = currentItem.Flags;

                // Check if we're allowed to mess with permissions
                if (!Permissions.IsGod(remoteClient.AgentId)) // Not a god
                {
                    bool noChange;
                    if (remoteClient.AgentId.NotEqual(part.OwnerID)) // Not owner
                    {
                        noChange = true;
                        if(itemInfo.OwnerID.IsZero() && itemInfo.GroupID.IsNotZero())
                        {
                            if(remoteClient.IsGroupMember(itemInfo.GroupID))
                            {
                                ulong powers = remoteClient.GetGroupPowers(itemInfo.GroupID);
                                if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                                    noChange = false;
                            }
                        }
                    }
                    else
                        noChange = false;

                    if(noChange)
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

            if (itemBase.ID.IsZero())
                partWhereRezzed = RezNewScript(remoteClient.AgentId, itemBase);
            else
                partWhereRezzed = RezScriptFromAgentInventory(remoteClient.AgentId, itemBase.ID, localID);

            partWhereRezzed?.SendPropertiesToClient(remoteClient);
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
            InventoryItemBase item = InventoryService.GetItem(agentID, fromItemID);

            // Try library
            // XXX clumsy, possibly should be one call
            if (item is null && LibraryService is not null && LibraryService.LibraryRootFolder is not null)
            {
                item = LibraryService.LibraryRootFolder.FindItem(fromItemID);
            }

            if (item is not null)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part is not null)
                {
                    if (!Permissions.CanEditObjectInventory(part.UUID, agentID))
                        return null;

                    part.ParentGroup.AddInventoryItem(agentID, localID, item, copyID);
                    // TODO: switch to posting on_rez here when scripts
                    // have state in inventory
                    part.Inventory.CreateScriptInstance(copyID, 0, false, DefaultScriptEngine, 0);

                    // tell anyone watching that there is a new script in town
                    EventManager.TriggerNewScript(agentID, part, copyID);

                    //m_log.InfoFormat("[PRIMINVENTORY]: " +
                    //   "Rezzed script {0} into prim local ID {1} for user {2}",
                    //   item.inventoryName, localID, remoteClient.Name);

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
            return RezNewScript(agentID, itemBase, null);
        }

        /// <summary>
        /// Rez a new script from nothing with given script text.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemBase">Template item.</param>
        /// <param name="scriptText"></param>
        /// <returns>The part where the script was rezzed if successful.  False otherwise.</returns>
        public SceneObjectPart RezNewScript(UUID agentID, InventoryItemBase itemBase, string scriptText)
        {
            // The part ID is the folder ID!
            SceneObjectPart part = GetSceneObjectPart(itemBase.Folder);
            if (part is null)
            {
                //m_log.DebugFormat(
                //    "[SCENE INVENTORY]: Could not find part with id {0} for {1} to rez new script",
                //    itemBase.Folder, agentID);

                return null;
            }

            if (!Permissions.CanCreateObjectInventory(itemBase.InvType, part.UUID, agentID))
            {
                //m_log.DebugFormat(
                //    "[SCENE INVENTORY]: No permission to create new script in {0} for {1}", part.Name, agentID);

                return null;
            }

            UUID assetID;

            if (scriptText is null)
                assetID = Constants.DefaultScriptID;
            else
            {
                AssetBase asset = CreateAsset(
                    itemBase.Name, itemBase.Description,
                    (sbyte)AssetType.LSLText,
                    Encoding.ASCII.GetBytes(scriptText),
                    agentID);

                AssetService.Store(asset);
                assetID = asset.FullID;
            }

            TaskInventoryItem taskItem = new()
            {
                ItemID = UUID.Random(),
                OldItemID = UUID.Zero,
                ParentPartID = itemBase.Folder,
                ParentID = itemBase.Folder,
                CreationDate = (uint)itemBase.CreationDate,
                Name = itemBase.Name,
                Description = itemBase.Description,
                Type = itemBase.AssetType,
                InvType = itemBase.InvType,
                OwnerID = itemBase.Owner,
                CreatorID = itemBase.CreatorIdAsUuid,
                BasePermissions = itemBase.BasePermissions,
                CurrentPermissions = itemBase.CurrentPermissions,
                EveryonePermissions = itemBase.EveryOnePermissions,
                //GroupPermissions = itemBase.GroupPermissions,
                GroupPermissions = 0,
                NextPermissions = itemBase.NextPermissions,
                GroupID = itemBase.GroupID,
                Flags = itemBase.Flags,
                PermsGranter = UUID.Zero,
                PermsMask = 0,
                AssetID = assetID
            };

            part.Inventory.AddInventoryItem(taskItem, false);
            part.Inventory.CreateScriptInstance(taskItem, 0, false, DefaultScriptEngine, 1);

            part.ParentGroup.InvalidateEffectivePerms();

            // tell anyone managing scripts that a new script exists
            EventManager.TriggerNewScript(agentID, part, taskItem.ItemID);

            part.ParentGroup.ResumeScripts();

            return part;
        }

        /// <summary>
        /// Rez a script into a prim's inventory from another prim
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="srcPart"> </param>
        /// <param name="destId"> </param>
        /// <param name="pin"></param>
        /// <param name="running"></param>
        /// <param name="start_param"></param>
        public void RezScriptFromPrim(UUID srcId, SceneObjectPart srcPart, UUID destId, int pin, int running, int start_param)
        {
            TaskInventoryItem srcTaskItem = srcPart.Inventory.GetInventoryItem(srcId);
            if (srcTaskItem is null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Tried to retrieve item ID {0} from prim {1}, {2} for rezzing a script but the "
                        + " item does not exist in this inventory",
                    srcId, srcPart.Name, srcPart.UUID);

                return;
            }

            SceneObjectPart destPart = GetSceneObjectPart(destId);
            if (destPart is null)
            {
                m_log.ErrorFormat(
                    "[PRIM INVENTORY]: Could not find part {0} to insert script item {1} from {2} {3} in {4}",
                    destId, srcId, srcPart.Name, srcPart.UUID, Name);
                return;
            }

            // Must own the object, and have modify rights
            if (srcPart.OwnerID.NotEqual(destPart.OwnerID))
            {
                // Group permissions
                if ((destPart.GroupID.IsZero()) || (destPart.GroupID.NotEqual(srcPart.GroupID)) ||
                    ((destPart.GroupMask & (uint)PermissionMask.Modify) == 0))
                    return;
                if((srcPart.OwnerMask & (uint)PermissionMask.Transfer) == 0)
                    return;
            }
            else
            {
                if ((destPart.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return;
            }

            if (destPart.ScriptAccessPin == 0 || destPart.ScriptAccessPin != pin)
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

            TaskInventoryItem destTaskItem = new()
            {
                ItemID = UUID.Random(),
                CreatorID = srcTaskItem.CreatorID,
                CreatorData = srcTaskItem.CreatorData,
                AssetID = srcTaskItem.AssetID,
                GroupID = destPart.GroupID,
                OwnerID = destPart.OwnerID,
                ParentID = destPart.UUID,
                ParentPartID = destPart.UUID,

                BasePermissions = srcTaskItem.BasePermissions,
                EveryonePermissions = srcTaskItem.EveryonePermissions,
                GroupPermissions = srcTaskItem.GroupPermissions,
                CurrentPermissions = srcTaskItem.CurrentPermissions,
                NextPermissions = srcTaskItem.NextPermissions,
                Flags = srcTaskItem.Flags
            };

            if (destPart.OwnerID.NotEqual(srcPart.OwnerID))
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
            destTaskItem.ScriptRunning = running != 0;

            destPart.Inventory.AddInventoryItemExclusive(destTaskItem, false);

            destPart.Inventory.CreateScriptInstance(destTaskItem, start_param, false, DefaultScriptEngine, 1);

            destPart.ParentGroup.ResumeScripts();

            if (TryGetScenePresence(srcTaskItem.OwnerID, out ScenePresence avatar))
            {
                destPart.SendPropertiesToClient(avatar.ControllingClient);
            }
        }

        /// <summary>
        /// Derez one or more objects from the scene.
        /// </summary>
        /// <remarks>
        /// Won't actually remove the scene object in the case where the object is being copied to a user inventory.
        /// </remarks>
        /// <param name='remoteClient'>Client requesting derez</param>
        /// <param name='localIDs'>Local ids of root parts of objects to delete.</param>
        /// <param name='groupID'>Not currently used.  Here because the client passes this to us.</param>
        /// <param name='action'>DeRezAction</param>
        /// <param name='destinationID'>User folder ID to place derezzed object</param>
        public virtual void DeRezObjects(
            IClientAPI remoteClient, List<uint> localIDs, UUID groupID, DeRezAction action, UUID destinationID, bool AddToReturns = true)
        {
            // First, see of we can perform the requested action and
            // build a list of eligible objects
            List<uint> deleteIDs = new();
            List<SceneObjectGroup> deleteGroups = new();
            List<SceneObjectGroup> takeCopyGroups = new();
            List<SceneObjectGroup> takeDeleteGroups = new();
            List<SceneObjectGroup> noPermtakeCopyGroups = new();

            ScenePresence sp = null;
            if(remoteClient is not null)
                sp = remoteClient.SceneAgent as ScenePresence;
            else if(action != DeRezAction.Return)
                return; // only Return can be called without a client

            // this is not as 0.8x code
            // 0.8x did refuse all operation if not allowed on all objects
            // this will do it on allowed objects
            // current viewers only ask if all allowed

            foreach (uint localID in localIDs)
            {
                // Invalid id
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part is null)
                {
                    //Client still thinks the object exists, kill it
                    deleteIDs.Add(localID);
                    continue;
                }

                SceneObjectGroup grp = part.ParentGroup;
                if (grp is null || grp.IsDeleted)
                {
                    //Client still thinks the object exists, kill it
                    deleteIDs.Add(localID);
                    continue;
                }

                // Can't delete child prims
                if (part != grp.RootPart)
                    continue;

                if (grp.IsAttachment)
                {
                    if(!sp.IsGod || action != DeRezAction.Return || action != DeRezAction.Delete)
                        continue;
                    // this may break the attachment, but its a security action
                    // viewers don't allow it anyways
                }

                // If child prims have invalid perms, fix them
                grp.AdjustChildPrimPermissions(false);

                switch (action)
                {
                    case DeRezAction.SaveToExistingUserInventoryItem:
                    {
                        if (Permissions.CanTakeCopyObject(grp, sp))
                            takeCopyGroups.Add(grp);
                        break;
                    }

                    case DeRezAction.TakeCopy:
                    {
                        if (Permissions.CanTakeCopyObject(grp, sp))
                            takeCopyGroups.Add(grp);
                        else                                  
                            noPermtakeCopyGroups.Add(grp);
                        break;
                    }

                    case DeRezAction.Take:
                    {
                        if (Permissions.CanTakeObject(grp, sp))
                            takeDeleteGroups.Add(grp);
                        break;
                    }

                    case DeRezAction.GodTakeCopy:
                    {
                        if((remoteClient is not null) && Permissions.IsGod(remoteClient.AgentId))
                            takeCopyGroups.Add(grp);
                        break;
                    }

                    case DeRezAction.Delete:
                    {
                        if (Permissions.CanDeleteObject(grp, remoteClient))
                        {
                            if(m_useTrashOnDelete || (sp.IsGod && grp.OwnerID.NotEqual(sp.UUID)))
                                takeDeleteGroups.Add(grp);
                            else
                                deleteGroups.Add(grp);
                        }
                        break;
                    }

                    case DeRezAction.Return:
                    {
                        if (remoteClient is not null)
                        {
                            if (Permissions.CanReturnObjects( null, remoteClient, new List<SceneObjectGroup>() {grp}))
                            {
                                takeDeleteGroups.Add(grp);
                                if (AddToReturns)
                                    AddReturn(grp.OwnerID.Equals(grp.GroupID) ? grp.LastOwnerID : grp.OwnerID, grp.Name, grp.AbsolutePosition,
                                            "parcel owner return");
                            }
                        }
                        else // Auto return passes through here with null agent
                        {
                            takeDeleteGroups.Add(grp);
                        }
                        break;
                    }

                    default:
                        break;
                }
            }

            if(deleteIDs.Count > 0)
                SendKillObject(deleteIDs);

            if (noPermtakeCopyGroups.Count > 0 && remoteClient is not null)
            {
                if(noPermtakeCopyGroups.Count == 1)
                    remoteClient.SendAlertMessage("No permission to take copy object " + noPermtakeCopyGroups[0].Name);
                else
                {
                    StringBuilder sb = new(1024);
                    sb.Append("No permission to take copy object ");
                    int i = 0;
                    while(i < noPermtakeCopyGroups.Count - 1)
                    {
                        sb.Append(noPermtakeCopyGroups[i++].Name);
                        sb.Append(", ");
                    }
                    sb.Append(noPermtakeCopyGroups[i].Name);
                    remoteClient.SendAlertMessage(sb.ToString());
                }
            }

            if (takeDeleteGroups.Count > 0)
            {
                m_asyncSceneObjectDeleter.DeleteToInventory(action, destinationID, takeDeleteGroups,
                        remoteClient, true);
            }

            if (takeCopyGroups.Count > 0)
            {
                m_asyncSceneObjectDeleter.DeleteToInventory(action, destinationID, takeCopyGroups,
                        remoteClient, false);
            }

            if (deleteGroups.Count > 0)
            {
                foreach (SceneObjectGroup g in deleteGroups)
                    DeleteSceneObject(g, false);
            }
        }

        public UUID attachObjectAssetStore(IClientAPI remoteClient, SceneObjectGroup grp, UUID AgentId, out UUID itemID)
        {
            itemID = UUID.Zero;
            if (grp is not null)
            {
                Vector3 inventoryStoredPosition = new(
                        Math.Min(grp.AbsolutePosition.X, RegionInfo.RegionSizeX - 6),
                        Math.Min(grp.AbsolutePosition.Y, RegionInfo.RegionSizeY - 6),
                        grp.AbsolutePosition.Z);

                Vector3 originalPosition = grp.AbsolutePosition;

                grp.AbsolutePosition = inventoryStoredPosition;

                string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(grp);

                grp.AbsolutePosition = originalPosition;
                SceneObjectPart grpRootPart = grp.RootPart;

                AssetBase asset = CreateAsset(
                    grpRootPart.Name,
                    grpRootPart.Description,
                    (sbyte)AssetType.Object,
                    Utils.StringToBytes(sceneObjectXml),
                    remoteClient.AgentId);
                AssetService.Store(asset);

                InventoryItemBase item = new()
                {
                    CreatorId = grpRootPart.CreatorID.ToString(),
                    CreatorData = grpRootPart.CreatorData,
                    Owner = remoteClient.AgentId,
                    ID = UUID.Random(),
                    AssetID = asset.FullID,
                    Description = grpRootPart.Description,
                    Name = grpRootPart.Name,
                    AssetType = asset.Type,
                    InvType = (int)InventoryType.Object
                };

                InventoryFolderBase folder = InventoryService.GetFolderForType(remoteClient.AgentId, FolderType.Object);
                if (folder is not null)
                    item.Folder = folder.ID;
                else // oopsies
                    item.Folder = UUID.Zero;

                // Set up base perms properly
                uint permsBase = (uint)(PermissionMask.Move | PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify);
                permsBase &= grpRootPart.BaseMask;
                permsBase |= (uint)PermissionMask.Move;

                // Make sure we don't lock it
                grpRootPart.NextOwnerMask |= (uint)PermissionMask.Move;

                if (remoteClient.AgentId.NotEqual(grpRootPart.OwnerID) && Permissions.PropagatePermissions())
                {
                    item.BasePermissions = permsBase & grpRootPart.NextOwnerMask;
                    item.CurrentPermissions = permsBase & grpRootPart.NextOwnerMask;
                    item.NextPermissions = permsBase & grpRootPart.NextOwnerMask;
                    item.EveryOnePermissions = permsBase & grpRootPart.EveryoneMask & grpRootPart.NextOwnerMask;
                    item.GroupPermissions = permsBase & grpRootPart.GroupMask & grpRootPart.NextOwnerMask;
                }
                else
                {
                    item.BasePermissions = permsBase;
                    item.CurrentPermissions = permsBase & grpRootPart.OwnerMask;
                    item.NextPermissions = permsBase & grpRootPart.NextOwnerMask;
                    item.EveryOnePermissions = permsBase & grpRootPart.EveryoneMask;
                    item.GroupPermissions = permsBase & grpRootPart.GroupMask;
                }
                item.CreationDate = Util.UnixTimeSinceEpoch();

                // sets itemID so client can show item as 'attached' in inventory
                grp.FromItemID = item.ID;

                if (AddInventoryItem(item))
                    remoteClient.SendInventoryItemCreateUpdate(item, 0);
                else
                    m_dialogModule.SendAlertToUser(remoteClient, "Operation failed");

                itemID = item.ID;
                return item.AssetID;
            }
            return UUID.Zero;
        }

        /// <summary>
        /// Returns the list of Scene Objects in an asset.
        /// </summary>
        /// <remarks>
        /// Returns one object if the asset is a regular object, and multiple objects for a coalesced object.
        /// </remarks>
        /// <param name="assetData">Asset data</param>
        /// <param name="isAttachment">True if the object is an attachment.</param>
        /// <param name="objlist">The objects included in the asset</param>
        /// <param name="veclist">Relative positions of the objects</param>
        /// <param name="bbox">Bounding box of all the objects</param>
        /// <param name="offsetHeight">Offset in the Z axis from the centre of the bounding box
        /// to the centre of the root prim (relevant only when returning a single object)</param>
        /// <returns>
        /// true if returning a single object or deserialization fails, false if returning the coalesced
        /// list of objects
        /// </returns>
        public bool GetObjectsToRez(
            byte[] assetData, bool isAttachment, out List<SceneObjectGroup> objlist, out List<Vector3> veclist,
            out Vector3 bbox, out float offsetHeight)
        {
            objlist = new List<SceneObjectGroup>();
            veclist = new List<Vector3>();
            bbox = Vector3.Zero;
            offsetHeight = 0;

            string xmlData = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(assetData));

            try
            {
                using (XmlTextReader wrappedReader = new(xmlData, XmlNodeType.Element, null))
                {
                    using (XmlReader reader = XmlReader.Create(wrappedReader, Util.SharedXmlReaderSettings))
                    {
                        reader.Read();
                        bool isSingleObject = reader.Name != "CoalescedObject";

                        if (isSingleObject || isAttachment)
                        {
                            SceneObjectGroup g;
                            try
                            {
                                g = SceneObjectSerializer.FromOriginalXmlFormat(reader);
                            }
                            catch (Exception e)
                            {
                                m_log.Error("[AGENT INVENTORY]: Deserialization of xml failed ", e);
                                Util.LogFailedXML("[AGENT INVENTORY]:", xmlData);
                                g = null;
                            }

                            if (g is not null)
                            {
                                objlist.Add(g);
                                veclist.Add(Vector3.Zero);
                                bbox = g.GetAxisAlignedBoundingBox(out offsetHeight);
                            }

                            return true;
                        }
                        else
                        {
                            XmlDocument doc = new();
                            doc.LoadXml(xmlData);
                            XmlElement e = (XmlElement)doc.SelectSingleNode("/CoalescedObject");
                            XmlElement coll = (XmlElement)e;
                            float bx = Convert.ToSingle(coll.GetAttribute("x"));
                            float by = Convert.ToSingle(coll.GetAttribute("y"));
                            float bz = Convert.ToSingle(coll.GetAttribute("z"));
                            bbox = new Vector3(bx, by, bz);
                            offsetHeight = 0;

                            XmlNodeList groups = e.SelectNodes("SceneObjectGroup");
                            foreach (XmlNode n in groups)
                            {
                                SceneObjectGroup g = SceneObjectSerializer.FromOriginalXmlFormat(n.OuterXml);
                                if (g is not null)
                                {
                                    objlist.Add(g);

                                    XmlElement el = (XmlElement)n;
                                    string rawX = el.GetAttribute("offsetx");
                                    string rawY = el.GetAttribute("offsety");
                                    string rawZ = el.GetAttribute("offsetz");

                                    float x = Convert.ToSingle(rawX);
                                    float y = Convert.ToSingle(rawY);
                                    float z = Convert.ToSingle(rawZ);
                                    veclist.Add(new Vector3(x, y, z));
                                }
                            }

                            return false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[AGENT INVENTORY]: Deserialization of xml failed when looking for CoalescedObject tag ", e);
                Util.LogFailedXML("[AGENT INVENTORY]:", xmlData);
            }

            return true;
        }

        public SceneObjectGroup GetSingleObjectToRez(byte[] assetData)
        {
            string xmlData = string.Empty;
            try
            {
                xmlData = ExternalRepresentationUtils.SanitizeXml(Utils.BytesToString(assetData));
                using XmlTextReader wrappedReader = new(xmlData, XmlNodeType.Element, null);
                using XmlReader reader = XmlReader.Create(wrappedReader, Util.SharedXmlReaderSettings);
                reader.Read();

                if (!"CoalescedObject".Equals(reader.Name))
                {
                    SceneObjectGroup g = SceneObjectSerializer.FromOriginalXmlFormat(reader);
                    return g;
                }
            }
            catch (Exception e)
            {
                m_log.Error("[AGENT INVENTORY]: single object xml deserialization failed" + e.Message);
                Util.LogFailedXML("[AGENT INVENTORY]:", xmlData);
            }

            return null;
        }

        /// <summary>
        /// Event Handler Rez an object into a scene
        /// Calls the non-void event handler
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="rezGroupID"></param>
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
        public virtual void RezObject(IClientAPI remoteClient, UUID itemID, UUID rezGroupID,
                                    Vector3 RayEnd, Vector3 RayStart,
                                    UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                    bool RezSelected, bool RemoveItem, UUID fromTaskID)
        {
            //m_log.DebugFormat(
            //    "[PRIM INVENTORY]: RezObject from {0} for item {1} from task id {2}",
            //    remoteClient.Name, itemID, fromTaskID);

            if (fromTaskID.IsZero())
            {
                // rez from user inventory
                IInventoryAccessModule invAccess = RequestModuleInterface<IInventoryAccessModule>();
                invAccess?.RezObject(
                        remoteClient, itemID, rezGroupID, RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection,
                        RezSelected, RemoveItem, fromTaskID, false);
                return;
            }

            // rez from a prim inventory 
            SceneObjectPart part = GetSceneObjectPart(fromTaskID);
            if (part is null)
            {
                m_log.ErrorFormat(
                    "[TASK INVENTORY]: {0} tried to rez item id {1} from object id {2} but there is no such scene object",
                    remoteClient.Name, itemID, fromTaskID);

                return;
            }

            TaskInventoryItem item = part.Inventory.GetInventoryItem(itemID);
            if (item is null)
            {
                m_log.ErrorFormat(
                    "[TASK INVENTORY]: {0} tried to rez item id {1} from object id {2} but there is no such item",
                    remoteClient.Name, itemID, fromTaskID);
                return;
            }

            if(item.InvType != (int)InventoryType.Object)
            {
                m_log.ErrorFormat(
                    "[TASK INVENTORY]: {0} tried to rez item id {1} from object id {2} but item is not a object",
                    remoteClient.Name, itemID, fromTaskID);
                return;
            }

            byte bRayEndIsIntersection = (byte)(RayEndIsIntersection ? 1 : 0);
            Vector3 scale = new(0.5f, 0.5f, 0.5f);
            Vector3 pos = GetNewRezLocation(
                    RayStart, RayEnd, RayTargetID, Quaternion.Identity,
                    BypassRayCast, bRayEndIsIntersection, true, scale, false);

            RezObject(part, item, remoteClient.AgentId, rezGroupID, pos, null, Vector3.Zero, 0, false, true, true);
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
        /// <returns>The SceneObjectGroup(s) rezzed, or null if rez was unsuccessful</returns>
        public virtual List<SceneObjectGroup> RezObject(SceneObjectPart sourcePart, TaskInventoryItem item,
                Vector3 pos, Quaternion? rot, Vector3 vel, int param, bool atRoot, bool rezSelected = false)
        {
            return RezObject(sourcePart, item, item.OwnerID, sourcePart.GroupID,
                  pos, rot, vel, param, atRoot, rezSelected, false);
        }

        public virtual List<SceneObjectGroup> RezObject(SceneObjectPart sourcePart, TaskInventoryItem item,
                UUID newowner, UUID newgroup,
                Vector3 pos, Quaternion? rot, Vector3 vel, int param, bool atRoot, bool rezSelected, bool humanRez)
        {
            if (item is null)
                return null;

            bool success = sourcePart.Inventory.GetRezReadySceneObjects(item, newowner, newgroup,
                out List<SceneObjectGroup> objlist, out List<Vector3> veclist,
                out Vector3 bbox, out float _);

            if (!success)
                return null;

            int totalPrims = 0;
            foreach (SceneObjectGroup group in objlist)
                totalPrims += group.PrimCount;

            if (!Permissions.CanRezObject(totalPrims, newowner, pos))
                return null;

            if (!Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    sourcePart.Inventory.RemoveInventoryItem(item.ItemID);
            }

            SceneObjectGroup sog;

            bool fixrot = false;
            Quaternion netRot = Quaternion.Identity;

            //  position adjust
            if (totalPrims > 1) // nothing to do on a single prim
            {
                if (objlist.Count == 1)
                {
                    // current object position is root position
                    if(!atRoot)
                    {
                        sog = objlist[0];
                        Quaternion orot = rot ?? sog.RootPart.GetWorldRotation();
                        // possible should be bbox, but geometric center looks better
                        Vector3 off = sog.GetGeometricCenter();
                        //Vector3 off = bbox * 0.5f;
                        off *= orot;
                        pos -= off;
                    }
                }
                else
                {
                    //veclist[] are relative to bbox corner with min X,Y and Z
                    // rez at root, and rot will be referenced to first object in list
                    if (rot is null)
                    {
                        // use original rotations
                        if (atRoot)
                            pos -= veclist[0];
                        else
                            pos -= bbox / 2;
                    }
                    else
                    {
                        fixrot = true;
                        sog = objlist[0];
                        netRot = Quaternion.Conjugate(sog.RootPart.GetWorldRotation());
                        netRot *= rot.Value;
                        Vector3 off;
                        if (atRoot)
                            off = veclist[0];
                        else
                            off = bbox / 2;
                        off *= netRot;
                        pos -= off;
                    }
                }
            }

            UUID rezzerID;
            if(humanRez)
                rezzerID = newowner;
            else
                rezzerID = sourcePart.UUID;

            for (int i = 0; i < objlist.Count; i++)
            {
                SceneObjectGroup group = objlist[i];
                Vector3 curpos;
                if(fixrot)
                    curpos = pos + veclist[i] * netRot;
                else
                    curpos = pos + veclist[i];

                if (group.IsAttachment == false && group.RootPart.Shape.State != 0)
                {
                    group.RootPart.AttachedPos = group.AbsolutePosition;
                    group.RootPart.Shape.LastAttachPoint = (byte)group.AttachmentPoint;
                }

                group.RezzerID = rezzerID;
                group.RezStringParameter = null;

                if (rezSelected)
                {
                    group.IsSelected = true;
                    group.RootPart.CreateSelected = true;
                }

                if ( i == 0)
                    AddNewSceneObject(group, true, curpos, rot, vel);
                else
                {
                    Quaternion crot = objlist[i].RootPart.GetWorldRotation();
                    if (fixrot)
                    {
                        crot *= netRot;
                    }
                    AddNewSceneObject(group, true, curpos, crot, vel);
                }

                // We can only call this after adding the scene object, since the scene object references the scene
                // to find out if scripts should be activated at all.
                group.InvalidateEffectivePerms();
                group.CreateScriptInstances(param, true, DefaultScriptEngine, 3);
                if(humanRez)
                    group.ResumeScripts();

                group.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);
            }

            return objlist;
        }

        public SceneObjectGroup ScriptRezObject(SceneObjectPart sourcePart, TaskInventoryItem item,
                UUID newSOGID,
                Vector3 pos, Quaternion? rot, Vector3 vel, int param, bool atRoot)
        {
            if (item is null)
                return null;

            if(TryGetSceneObjectGroup(newSOGID, out _))
                return null;

            SceneObjectGroup sog = sourcePart.Inventory.GetSingleRezReadySceneObject(item, sourcePart.OwnerID, sourcePart.GroupID);
            if(sog is null)
                return null;

            int totalPrims  = sog.PrimCount;

            if (!Permissions.CanRezObject(totalPrims, sourcePart.OwnerID, pos))
                return null;

            if (!Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                    sourcePart.Inventory.RemoveInventoryItem(item.ItemID);
            }

            //  position adjust
            if (totalPrims > 1) // nothing to do on a single prim
            {
                // current object position is root position
                if(!atRoot)
                {
                    Quaternion orot = rot ?? sog.RootPart.GetWorldRotation();
                    // possible should be bbox, but geometric center looks better
                    Vector3 off = sog.GetGeometricCenter();
                    off *= orot;
                    pos -= off;
                }
            }

            if (sog.IsAttachment == false && sog.RootPart.Shape.State != 0)
            {
                sog.RootPart.AttachedPos = sog.AbsolutePosition;
                sog.RootPart.Shape.LastAttachPoint = (byte)sog.AttachmentPoint;
            }

            sog.RezzerID = sourcePart.UUID;

            AddNewSceneObject(sog, true, pos, rot, vel);

            // We can only call this after adding the scene object, since the scene object references the scene
            // to find out if scripts should be activated at all.
            sog.InvalidateEffectivePerms();
            sog.CreateScriptInstances(param, true, DefaultScriptEngine, 3);

            sog.ScheduleGroupForUpdate(PrimUpdateFlags.FullUpdatewithAnimMatOvr);

            return sog;
        }

        public virtual bool returnObjects(SceneObjectGroup[] returnobjects,
                IClientAPI client)
        {
            List<uint> localIDs = new();

            foreach (SceneObjectGroup grp in returnobjects)
            {
                AddReturn(grp.OwnerID, grp.Name, grp.AbsolutePosition,
                        "parcel owner return");
                localIDs.Add(grp.RootPart.LocalId);
            }
            DeRezObjects(client, localIDs, UUID.Zero, DeRezAction.Return,
                    UUID.Zero, false);

            return true;
        }

        public void SetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID, bool running)
        {
            if (!Permissions.CanEditScript(itemID, objectID, controllingClient.AgentId))
                return;

            SceneObjectPart part = GetSceneObjectPart(objectID);
            if (part is null)
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
                if (ownerID.IsNotZero())
                    return;
            }

            List<SceneObjectGroup> groups = new();

            foreach (uint localID in localIDs)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part is null)
                    continue;

                if (!groups.Contains(part.ParentGroup))
                    groups.Add(part.ParentGroup);
            }

            foreach (SceneObjectGroup sog in groups)
            {
                if (ownerID.IsNotZero())
                {
                    sog.SetOwnerId(ownerID);
                    sog.SetGroup(groupID, remoteClient);

                    SceneObjectPart[] partList = sog.Parts;

                    foreach (SceneObjectPart child in partList)
                    {
                        child.Inventory.ChangeInventoryOwner(ownerID);
                        child.TriggerScriptChangedEvent(Changed.OWNER);
                    }

                    sog.ScheduleGroupForFullUpdate();
                }
                else // The object deeded to the group
                {
                    if (!Permissions.CanDeedObject(remoteClient, sog, groupID))
                        continue;

                    sog.SetOwnerId(groupID);

                    // this is wrong, GroupMask is used for group sharing, still possible to set
                    // this whould give owner rights to users that are member of group but don't have role powers to edit
                    //sog.RootPart.GroupMask = sog.RootPart.OwnerMask;

                    // we should keep all permissions on deed to group
                    // and with this comented code, if user does not set next permissions on the object
                    // and on ALL contents of ALL prims, he may loose rights, making the object useless
                    sog.ApplyNextOwnerPermissions();
                    sog.InvalidateEffectivePerms();
                    sog.ScheduleGroupForFullUpdate();

                    SceneObjectPart[] partList = sog.Parts;
                    foreach (SceneObjectPart child in partList)
                    {
                        child.Inventory.ChangeInventoryOwner(groupID);
                        child.TriggerScriptChangedEvent(Changed.OWNER);
                    }
                }
            }

            foreach (uint localID in localIDs)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);
                if (part is null)
                    continue;
                part.SendPropertiesToClient(remoteClient);
            }
        }

        public void DelinkObjects(List<uint> primIds, IClientAPI client)
        {
            List<SceneObjectPart> parts = new();

            foreach (uint localID in primIds)
            {
                SceneObjectPart part = GetSceneObjectPart(localID);

                if (part is null)
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
            List<UUID> owners = new();
            List<SceneObjectPart> children = new();
            SceneObjectPart root = GetSceneObjectPart(parentPrimId);

            if (root is null)
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

                if (part is null)
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

            bool oldUsePhysics = (root.Flags & PrimFlags.Physics) != 0;
            m_sceneGraph.LinkObjects(root, children);

            if (TryGetScenePresence(agentId, out ScenePresence sp))
            {
                root.SendPropertiesToClient(sp.ControllingClient);
                if (oldUsePhysics && (root.Flags & PrimFlags.Physics) == 0)
                {
                    sp.ControllingClient.SendAlertMessage("Object physics cancelled");
                }
            }
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
