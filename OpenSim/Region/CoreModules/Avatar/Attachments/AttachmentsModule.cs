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
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace OpenSim.Region.CoreModules.Avatar.Attachments
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AttachmentsModule")]
    public class AttachmentsModule : IAttachmentsModule, INonSharedRegionModule
    {
        #region INonSharedRegionModule
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private Scene m_scene;
        private IDialogModule m_dialogModule;

        /// <summary>
        /// Are attachments enabled?
        /// </summary>
        public bool Enabled { get; private set; }
        
        public string Name { get { return "Attachments Module"; } }
        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["Attachments"];
            if (config != null)
                Enabled = config.GetBoolean("Enabled", true);
            else
                Enabled = true;
        }
        
        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_dialogModule = m_scene.RequestModuleInterface<IDialogModule>();
            m_scene.RegisterModuleInterface<IAttachmentsModule>(this);

            if (Enabled)
                m_scene.EventManager.OnNewClient += SubscribeToClientEvents;

            // TODO: Should probably be subscribing to CloseClient too, but this doesn't yet give us IClientAPI
        }
        
        public void RemoveRegion(Scene scene) 
        {
            m_scene.UnregisterModuleInterface<IAttachmentsModule>(this);

            if (Enabled)
                m_scene.EventManager.OnNewClient -= SubscribeToClientEvents;
        }
        
        public void RegionLoaded(Scene scene) {}
        
        public void Close() 
        {
            RemoveRegion(m_scene);
        }

        #endregion

        #region IAttachmentsModule

        /// <summary>
        /// RezAttachments. This should only be called upon login on the first region.
        /// Attachment rezzings on crossings and TPs are done in a different way.
        /// </summary>
        public void RezAttachments(IScenePresence sp)
        {
            if (!Enabled)
                return;

            if (null == sp.Appearance)
            {
                m_log.WarnFormat("[ATTACHMENTS MODULE]: Appearance has not been initialized for agent {0}", sp.UUID);
                return;
            }

//            m_log.DebugFormat("[ATTACHMENTS MODULE]: Rezzing any attachments for {0}", sp.Name);

            List<AvatarAttachment> attachments = sp.Appearance.GetAttachments();
            foreach (AvatarAttachment attach in attachments)
            {
                uint p = (uint)attach.AttachPoint;

//                m_log.DebugFormat(
//                    "[ATTACHMENTS MODULE]: Doing initial rez of attachment with itemID {0}, assetID {1}, point {2} for {3} in {4}",
//                    attach.ItemID, attach.AssetID, p, sp.Name, m_scene.RegionInfo.RegionName);

                // For some reason assetIDs are being written as Zero's in the DB -- need to track tat down
                // But they're not used anyway, the item is being looked up for now, so let's proceed.
                //if (UUID.Zero == assetID) 
                //{
                //    m_log.DebugFormat("[ATTACHMENT]: Cannot rez attachment in point {0} with itemID {1}", p, itemID);
                //    continue;
                //}

                try
                {
                    // If we're an NPC then skip all the item checks and manipulations since we don't have an
                    // inventory right now.
                    if (sp.PresenceType == PresenceType.Npc)
                        RezSingleAttachmentFromInventoryInternal(sp, UUID.Zero, attach.AssetID, p);
                    else
                        RezSingleAttachmentFromInventory(sp, attach.ItemID, p);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[ATTACHMENTS MODULE]: Unable to rez attachment: {0}{1}", e.Message, e.StackTrace);
                }
            }
        }

        public void SaveChangedAttachments(IScenePresence sp)
        {
//            m_log.DebugFormat("[ATTACHMENTS MODULE]: Saving changed attachments for {0}", sp.Name);

            if (!Enabled)
                return;

            foreach (SceneObjectGroup grp in sp.GetAttachments())
            {
//                if (grp.HasGroupChanged) // Resizer scripts?
//                {
                    grp.IsAttachment = false;
                    grp.AbsolutePosition = grp.RootPart.AttachedPos;
                    UpdateKnownItem(sp, grp);
                    grp.IsAttachment = true;
//                }
            }
        }

        public void DeleteAttachmentsFromScene(IScenePresence sp, bool silent)
        {
//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: Deleting attachments from scene {0} for {1}, silent = {2}",
//                m_scene.RegionInfo.RegionName, sp.Name, silent);

            if (!Enabled)
                return;

            foreach (SceneObjectGroup sop in sp.GetAttachments())
            {
                sop.Scene.DeleteSceneObject(sop, silent);
            }

            sp.ClearAttachments();
        }
        
        public bool AttachObject(IScenePresence sp, SceneObjectGroup group, uint attachmentPt, bool silent)
        {
            lock (sp.AttachmentsSyncLock)
            {
//                m_log.DebugFormat(
//                    "[ATTACHMENTS MODULE]: Attaching object {0} {1} to {2} point {3} from ground (silent = {4})",
//                    group.Name, group.LocalId, sp.Name, attachmentPt, silent);
    
                if (sp.GetAttachments(attachmentPt).Contains(group))
                {
    //                m_log.WarnFormat(
    //                    "[ATTACHMENTS MODULE]: Ignoring request to attach {0} {1} to {2} on {3} since it's already attached",
    //                    group.Name, group.LocalId, sp.Name, AttachmentPt);
    
                    return false;
                }
    
                Vector3 attachPos = group.AbsolutePosition;
    
                // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
                // be removed when that functionality is implemented in opensim
                attachmentPt &= 0x7f;
                
                // If the attachment point isn't the same as the one previously used
                // set it's offset position = 0 so that it appears on the attachment point
                // and not in a weird location somewhere unknown.
                if (attachmentPt != 0 && attachmentPt != group.AttachmentPoint)
                {
                    attachPos = Vector3.Zero;
                }
    
                // AttachmentPt 0 means the client chose to 'wear' the attachment.
                if (attachmentPt == 0)
                {
                    // Check object for stored attachment point
                    attachmentPt = group.AttachmentPoint;
                }
    
                // if we still didn't find a suitable attachment point.......
                if (attachmentPt == 0)
                {
                    // Stick it on left hand with Zero Offset from the attachment point.
                    attachmentPt = (uint)AttachmentPoint.LeftHand;
                    attachPos = Vector3.Zero;
                }
    
                group.AttachmentPoint = attachmentPt;
                group.AbsolutePosition = attachPos;
    
                // We also don't want to do any of the inventory operations for an NPC.
                if (sp.PresenceType != PresenceType.Npc)
                {
                    // Remove any previous attachments
                    List<SceneObjectGroup> attachments = sp.GetAttachments(attachmentPt);
    
                    // At the moment we can only deal with a single attachment
                    if (attachments.Count != 0)
                    {
                        UUID oldAttachmentItemID = attachments[0].GetFromItemID();
        
                        if (oldAttachmentItemID != UUID.Zero)
                            DetachSingleAttachmentToInvInternal(sp, oldAttachmentItemID);
                        else
                            m_log.WarnFormat(
                                "[ATTACHMENTS MODULE]: When detaching existing attachment {0} {1} at point {2} to make way for {3} {4} for {5}, couldn't find the associated item ID to adjust inventory attachment record!",
                                attachments[0].Name, attachments[0].LocalId, attachmentPt, group.Name, group.LocalId, sp.Name);
                    }
    
                    // Add the new attachment to inventory if we don't already have it.
                    UUID newAttachmentItemID = group.GetFromItemID();
                    if (newAttachmentItemID == UUID.Zero)
                        newAttachmentItemID = AddSceneObjectAsNewAttachmentInInv(sp, group).ID;
        
                    ShowAttachInUserInventory(sp, attachmentPt, newAttachmentItemID, group);
                }
    
                AttachToAgent(sp, group, attachmentPt, attachPos, silent);
            }

            return true;
        }

        public ISceneEntity RezSingleAttachmentFromInventory(IScenePresence sp, UUID itemID, uint AttachmentPt)
        {
            if (!Enabled)
                return null;

//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: RezSingleAttachmentFromInventory to point {0} from item {1} for {2}",
//                (AttachmentPoint)AttachmentPt, itemID, sp.Name);

            // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
            // be removed when that functionality is implemented in opensim
            AttachmentPt &= 0x7f;

            // Viewer 2/3 sometimes asks to re-wear items that are already worn (and show up in it's inventory as such).
            // This often happens during login - not sure the exact reason.
            // For now, we will ignore the request.  Unfortunately, this means that we need to dig through all the
            // ScenePresence attachments.  We can't use the data in AvatarAppearance because that's present at login
            // before anything has actually been attached.
            bool alreadyOn = false;
            List<SceneObjectGroup> existingAttachments = sp.GetAttachments();
            foreach (SceneObjectGroup so in existingAttachments)
            {
                if (so.GetFromItemID() == itemID)
                {
                    alreadyOn = true;
                    break;
                }
            }

//            if (sp.Appearance.GetAttachmentForItem(itemID) != null)
            if (alreadyOn)
            {
//                m_log.WarnFormat(
//                    "[ATTACHMENTS MODULE]: Ignoring request by {0} to wear item {1} at {2} since it is already worn",
//                    sp.Name, itemID, AttachmentPt);

                return null;
            }

            SceneObjectGroup att = RezSingleAttachmentFromInventoryInternal(sp, itemID, UUID.Zero, AttachmentPt);

            if (att == null)
                DetachSingleAttachmentToInv(sp, itemID);

            return att;
        }

        public void RezMultipleAttachmentsFromInventory(IScenePresence sp, List<KeyValuePair<UUID, uint>> rezlist)
        {
            if (!Enabled)
                return;

            //                m_log.DebugFormat("[ATTACHMENTS MODULE]: Rezzing multiple attachments from inventory for {0}", sp.Name);
            lock (sp.AttachmentsSyncLock)
            {
                foreach (KeyValuePair<UUID, uint> rez in rezlist)
                {
                    RezSingleAttachmentFromInventory(sp, rez.Key, rez.Value);
                }
            }
        }

        public void DetachSingleAttachmentToGround(IScenePresence sp, uint soLocalId)
        {
            if (!Enabled)
                return;

//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: DetachSingleAttachmentToGround() for {0}, object {1}",
//                sp.UUID, soLocalId);

            SceneObjectGroup so = m_scene.GetGroupByPrim(soLocalId);

            if (so == null)
                return;

            if (so.AttachedAvatar != sp.UUID)
                return;

            UUID inventoryID = so.GetFromItemID();

//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: In DetachSingleAttachmentToGround(), object is {0} {1}, associated item is {2}",
//                so.Name, so.LocalId, inventoryID);

            lock (sp.AttachmentsSyncLock)
            {
                if (!m_scene.Permissions.CanRezObject(
                    so.PrimCount, sp.UUID, sp.AbsolutePosition))
                    return;

                bool changed = sp.Appearance.DetachAttachment(inventoryID);
                if (changed && m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);

                sp.RemoveAttachment(so);

                SceneObjectPart rootPart = so.RootPart;
                rootPart.FromItemID = UUID.Zero;
                so.AbsolutePosition = sp.AbsolutePosition;
                so.AttachedAvatar = UUID.Zero;
                rootPart.SetParentLocalId(0);
                so.ClearPartAttachmentData();
                rootPart.ApplyPhysics(rootPart.GetEffectiveObjectFlags(), rootPart.VolumeDetectActive);
                so.HasGroupChanged = true;
                rootPart.Rezzed = DateTime.Now;
                rootPart.RemFlag(PrimFlags.TemporaryOnRez);
                so.AttachToBackup();
                m_scene.EventManager.TriggerParcelPrimCountTainted();
                rootPart.ScheduleFullUpdate();
                rootPart.ClearUndoState();

                List<UUID> uuids = new List<UUID>();
                uuids.Add(inventoryID);
                m_scene.InventoryService.DeleteItems(sp.UUID, uuids);
                sp.ControllingClient.SendRemoveInventoryItem(inventoryID);
            }

            m_scene.EventManager.TriggerOnAttach(so.LocalId, so.UUID, UUID.Zero);
        }

        public void DetachSingleAttachmentToInv(IScenePresence sp, UUID itemID)
        {
            lock (sp.AttachmentsSyncLock)
            {
                // Save avatar attachment information
                m_log.Debug("[ATTACHMENTS MODULE]: Detaching from UserID: " + sp.UUID + ", ItemID: " + itemID);

                bool changed = sp.Appearance.DetachAttachment(itemID);
                if (changed && m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);

                DetachSingleAttachmentToInvInternal(sp, itemID);
            }
        }
        
        public void UpdateAttachmentPosition(SceneObjectGroup sog, Vector3 pos)
        {
            if (!Enabled)
                return;

            // First we save the
            // attachment point information, then we update the relative 
            // positioning. Then we have to mark the object as NOT an
            // attachment. This is necessary in order to correctly save
            // and retrieve GroupPosition information for the attachment.
            // Finally, we restore the object's attachment status.
            uint attachmentPoint = sog.AttachmentPoint;
            sog.UpdateGroupPosition(pos);
            sog.IsAttachment = false;
            sog.AbsolutePosition = sog.RootPart.AttachedPos;
            sog.AttachmentPoint = attachmentPoint;
            sog.HasGroupChanged = true;            
        }

        #endregion

        #region AttachmentModule private methods

        // This is public but is not part of the IAttachmentsModule interface.
        // RegionCombiner module needs to poke at it to deliver client events.
        // This breaks the encapsulation of the module and should get fixed somehow. 
        public void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRezSingleAttachmentFromInv += Client_OnRezSingleAttachmentFromInv;
            client.OnRezMultipleAttachmentsFromInv += Client_OnRezMultipleAttachmentsFromInv;
            client.OnObjectAttach += Client_OnObjectAttach;
            client.OnObjectDetach += Client_OnObjectDetach;
            client.OnDetachAttachmentIntoInv += Client_OnDetachAttachmentIntoInv;
            client.OnObjectDrop += Client_OnObjectDrop;
        }

        // This is public but is not part of the IAttachmentsModule interface.
        // RegionCombiner module needs to poke at it to deliver client events.
        // This breaks the encapsulation of the module and should get fixed somehow. 
        public void UnsubscribeFromClientEvents(IClientAPI client)
        {
            client.OnRezSingleAttachmentFromInv -= Client_OnRezSingleAttachmentFromInv;
            client.OnRezMultipleAttachmentsFromInv -= Client_OnRezMultipleAttachmentsFromInv;
            client.OnObjectAttach -= Client_OnObjectAttach;
            client.OnObjectDetach -= Client_OnObjectDetach;
            client.OnDetachAttachmentIntoInv -= Client_OnDetachAttachmentIntoInv;
            client.OnObjectDrop -= Client_OnObjectDrop;
        }

        /// <summary>
        /// Update the attachment asset for the new sog details if they have changed.
        /// </summary>
        /// <remarks>
        /// This is essential for preserving attachment attributes such as permission.  Unlike normal scene objects,
        /// these details are not stored on the region.
        /// </remarks>
        /// <param name="sp"></param>
        /// <param name="grp"></param>
        private void UpdateKnownItem(IScenePresence sp, SceneObjectGroup grp)
        {
            // Saving attachments for NPCs messes them up for the real owner!
            INPCModule module = m_scene.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                if (module.IsNPC(sp.UUID, m_scene))
                    return;
            }

            if (grp.HasGroupChanged || grp.ContainsScripts())
            {
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Updating asset for attachment {0}, attachpoint {1}",
                    grp.UUID, grp.AttachmentPoint);

                string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(grp);

                InventoryItemBase item = new InventoryItemBase(grp.GetFromItemID(), sp.UUID);
                item = m_scene.InventoryService.GetItem(item);

                if (item != null)
                {
                    AssetBase asset = m_scene.CreateAsset(
                        grp.GetPartName(grp.LocalId),
                        grp.GetPartDescription(grp.LocalId),
                        (sbyte)AssetType.Object,
                        Utils.StringToBytes(sceneObjectXml),
                        sp.UUID);
                    m_scene.AssetService.Store(asset);

                    item.AssetID = asset.FullID;
                    item.Description = asset.Description;
                    item.Name = asset.Name;
                    item.AssetType = asset.Type;
                    item.InvType = (int)InventoryType.Object;

                    m_scene.InventoryService.UpdateItem(item);

                    // this gets called when the agent logs off!
                    if (sp.ControllingClient != null)
                        sp.ControllingClient.SendInventoryItemCreateUpdate(item, 0);
                }
            }
            else
            {
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Don't need to update asset for unchanged attachment {0}, attachpoint {1}",
                    grp.UUID, grp.AttachmentPoint);
            }
        }

        /// <summary>
        /// Attach this scene object to the given avatar.
        /// </summary>
        /// <remarks>
        /// This isn't publicly available since attachments should always perform the corresponding inventory 
        /// operation (to show the attach in user inventory and update the asset with positional information).
        /// </remarks>
        /// <param name="sp"></param>
        /// <param name="so"></param>
        /// <param name="attachmentpoint"></param>
        /// <param name="attachOffset"></param>
        /// <param name="silent"></param>
        private void AttachToAgent(
            IScenePresence sp, SceneObjectGroup so, uint attachmentpoint, Vector3 attachOffset, bool silent)
        {
            //            m_log.DebugFormat(
            //                "[ATTACHMENTS MODULE]: Adding attachment {0} to avatar {1} in pt {2} pos {3} {4}",
            //                so.Name, avatar.Name, attachmentpoint, attachOffset, so.RootPart.AttachedPos);

            so.DetachFromBackup();

            // Remove from database and parcel prim count
            m_scene.DeleteFromStorage(so.UUID);
            m_scene.EventManager.TriggerParcelPrimCountTainted();

            so.AttachedAvatar = sp.UUID;

            if (so.RootPart.PhysActor != null)
                so.RootPart.RemoveFromPhysics();

            so.AbsolutePosition = attachOffset;
            so.RootPart.AttachedPos = attachOffset;
            so.IsAttachment = true;
            so.RootPart.SetParentLocalId(sp.LocalId);
            so.AttachmentPoint = attachmentpoint;

            sp.AddAttachment(so);

            if (!silent)
            {
                // Killing it here will cause the client to deselect it
                // It then reappears on the avatar, deselected
                // through the full update below
                //
                if (so.IsSelected)
                {
                    m_scene.SendKillObject(new List<uint> { so.RootPart.LocalId });
                }

                so.IsSelected = false; // fudge....
                so.ScheduleGroupForFullUpdate();
            }

            // In case it is later dropped again, don't let
            // it get cleaned up
            so.RootPart.RemFlag(PrimFlags.TemporaryOnRez);
        }

        /// <summary>
        /// Add a scene object as a new attachment in the user inventory.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="grp"></param>
        /// <returns>The user inventory item created that holds the attachment.</returns>
        private InventoryItemBase AddSceneObjectAsNewAttachmentInInv(IScenePresence sp, SceneObjectGroup grp)
        {
            //            m_log.DebugFormat(
            //                "[ATTACHMENTS MODULE]: Called AddSceneObjectAsAttachment for object {0} {1} for {2}",
            //                grp.Name, grp.LocalId, remoteClient.Name);

            Vector3 inventoryStoredPosition = new Vector3
                   (((grp.AbsolutePosition.X > (int)Constants.RegionSize)
                         ? Constants.RegionSize - 6
                         : grp.AbsolutePosition.X)
                    ,
                    (grp.AbsolutePosition.Y > (int)Constants.RegionSize)
                        ? Constants.RegionSize - 6
                        : grp.AbsolutePosition.Y,
                    grp.AbsolutePosition.Z);

            Vector3 originalPosition = grp.AbsolutePosition;

            grp.AbsolutePosition = inventoryStoredPosition;

            // If we're being called from a script, then trying to serialize that same script's state will not complete
            // in any reasonable time period.  Therefore, we'll avoid it.  The worst that can happen is that if
            // the client/server crashes rather than logging out normally, the attachment's scripts will resume
            // without state on relog.  Arguably, this is what we want anyway.
            string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(grp, false);

            grp.AbsolutePosition = originalPosition;

            AssetBase asset = m_scene.CreateAsset(
                grp.GetPartName(grp.LocalId),
                grp.GetPartDescription(grp.LocalId),
                (sbyte)AssetType.Object,
                Utils.StringToBytes(sceneObjectXml),
                sp.UUID);

            m_scene.AssetService.Store(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.CreatorId = grp.RootPart.CreatorID.ToString();
            item.CreatorData = grp.RootPart.CreatorData;
            item.Owner = sp.UUID;
            item.ID = UUID.Random();
            item.AssetID = asset.FullID;
            item.Description = asset.Description;
            item.Name = asset.Name;
            item.AssetType = asset.Type;
            item.InvType = (int)InventoryType.Object;

            InventoryFolderBase folder = m_scene.InventoryService.GetFolderForType(sp.UUID, AssetType.Object);
            if (folder != null)
                item.Folder = folder.ID;
            else // oopsies
                item.Folder = UUID.Zero;

            if ((sp.UUID != grp.RootPart.OwnerID) && m_scene.Permissions.PropagatePermissions())
            {
                item.BasePermissions = grp.RootPart.NextOwnerMask;
                item.CurrentPermissions = grp.RootPart.NextOwnerMask;
                item.NextPermissions = grp.RootPart.NextOwnerMask;
                item.EveryOnePermissions = grp.RootPart.EveryoneMask & grp.RootPart.NextOwnerMask;
                item.GroupPermissions = grp.RootPart.GroupMask & grp.RootPart.NextOwnerMask;
            }
            else
            {
                item.BasePermissions = grp.RootPart.BaseMask;
                item.CurrentPermissions = grp.RootPart.OwnerMask;
                item.NextPermissions = grp.RootPart.NextOwnerMask;
                item.EveryOnePermissions = grp.RootPart.EveryoneMask;
                item.GroupPermissions = grp.RootPart.GroupMask;
            }
            item.CreationDate = Util.UnixTimeSinceEpoch();

            // sets itemID so client can show item as 'attached' in inventory
            grp.SetFromItemID(item.ID);

            if (m_scene.AddInventoryItem(item))
            {
                sp.ControllingClient.SendInventoryItemCreateUpdate(item, 0);
            }
            else
            {
                if (m_dialogModule != null)
                    m_dialogModule.SendAlertToUser(sp.ControllingClient, "Operation failed");
            }

            return item;
        }

        // What makes this method odd and unique is it tries to detach using an UUID....     Yay for standards.
        // To LocalId or UUID, *THAT* is the question. How now Brown UUID??
        private void DetachSingleAttachmentToInvInternal(IScenePresence sp, UUID itemID)
        {
            //            m_log.DebugFormat("[ATTACHMENTS MODULE]: Detaching item {0} to inventory for {1}", itemID, sp.Name);

            if (itemID == UUID.Zero) // If this happened, someone made a mistake....
                return;

            // We can NOT use the dictionries here, as we are looking
            // for an entity by the fromAssetID, which is NOT the prim UUID
            EntityBase[] detachEntities = m_scene.GetEntities();
            SceneObjectGroup group;

            lock (sp.AttachmentsSyncLock)
            {
                foreach (EntityBase entity in detachEntities)
                {
                    if (entity is SceneObjectGroup)
                    {
                        group = (SceneObjectGroup)entity;
                        if (group.GetFromItemID() == itemID)
                        {
                            m_scene.EventManager.TriggerOnAttach(group.LocalId, itemID, UUID.Zero);
                            sp.RemoveAttachment(group);

                            // Prepare sog for storage
                            group.AttachedAvatar = UUID.Zero;
                            group.RootPart.SetParentLocalId(0);
                            group.IsAttachment = false;
                            group.AbsolutePosition = group.RootPart.AttachedPos;

                            UpdateKnownItem(sp, group);
                            m_scene.DeleteSceneObject(group, false);

                            return;
                        }
                    }
                }
            }
        }

        private SceneObjectGroup RezSingleAttachmentFromInventoryInternal(
            IScenePresence sp, UUID itemID, UUID assetID, uint attachmentPt)
        {
            IInventoryAccessModule invAccess = m_scene.RequestModuleInterface<IInventoryAccessModule>();
            if (invAccess != null)
            {
                lock (sp.AttachmentsSyncLock)
                {
                    SceneObjectGroup objatt;

                    if (itemID != UUID.Zero)
                        objatt = invAccess.RezObject(sp.ControllingClient,
                            itemID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                            false, false, sp.UUID, true);
                    else
                        objatt = invAccess.RezObject(sp.ControllingClient,
                            null, assetID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                            false, false, sp.UUID, true);

                    //                m_log.DebugFormat(
                    //                    "[ATTACHMENTS MODULE]: Retrieved single object {0} for attachment to {1} on point {2}",
                    //                    objatt.Name, remoteClient.Name, AttachmentPt);

                    if (objatt != null)
                    {
                        // HasGroupChanged is being set from within RezObject.  Ideally it would be set by the caller.
                        objatt.HasGroupChanged = false;
                        bool tainted = false;
                        if (attachmentPt != 0 && attachmentPt != objatt.AttachmentPoint)
                            tainted = true;

                        // This will throw if the attachment fails
                        try
                        {
                            AttachObject(sp, objatt, attachmentPt, false);
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[ATTACHMENTS MODULE]: Failed to attach {0} {1} for {2}, exception {3}{4}",
                                objatt.Name, objatt.UUID, sp.Name, e.Message, e.StackTrace);

                            // Make sure the object doesn't stick around and bail
                            sp.RemoveAttachment(objatt);
                            m_scene.DeleteSceneObject(objatt, false);
                            return null;
                        }

                        if (tainted)
                            objatt.HasGroupChanged = true;

                        // Fire after attach, so we don't get messy perms dialogs
                        // 4 == AttachedRez
                        objatt.CreateScriptInstances(0, true, m_scene.DefaultScriptEngine, 4);
                        objatt.ResumeScripts();

                        // Do this last so that event listeners have access to all the effects of the attachment
                        m_scene.EventManager.TriggerOnAttach(objatt.LocalId, itemID, sp.UUID);

                        return objatt;
                    }
                    else
                    {
                        m_log.WarnFormat(
                            "[ATTACHMENTS MODULE]: Could not retrieve item {0} for attaching to avatar {1} at point {2}",
                            itemID, sp.Name, attachmentPt);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Update the user inventory to reflect an attachment
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="itemID"></param>
        /// <param name="att"></param>
        private void ShowAttachInUserInventory(IScenePresence sp, uint AttachmentPt, UUID itemID, SceneObjectGroup att)
        {
            //            m_log.DebugFormat(
            //                "[USER INVENTORY]: Updating attachment {0} for {1} at {2} using item ID {3}",
            //                att.Name, sp.Name, AttachmentPt, itemID);

            if (UUID.Zero == itemID)
            {
                m_log.Error("[ATTACHMENTS MODULE]: Unable to save attachment. Error inventory item ID.");
                return;
            }

            if (0 == AttachmentPt)
            {
                m_log.Error("[ATTACHMENTS MODULE]: Unable to save attachment. Error attachment point.");
                return;
            }

            InventoryItemBase item = new InventoryItemBase(itemID, sp.UUID);
            item = m_scene.InventoryService.GetItem(item);
            bool changed = sp.Appearance.SetAttachment((int)AttachmentPt, itemID, item.AssetID);
            if (changed && m_scene.AvatarFactory != null)
                m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);
        }

        #endregion

        #region Client Event Handlers

        private ISceneEntity Client_OnRezSingleAttachmentFromInv(IClientAPI remoteClient, UUID itemID, uint AttachmentPt)
        {
            if (!Enabled)
                return null;

            //            m_log.DebugFormat(
            //                "[ATTACHMENTS MODULE]: Rezzing attachment to point {0} from item {1} for {2}",
            //                (AttachmentPoint)AttachmentPt, itemID, remoteClient.Name);

            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);

            if (sp == null)
            {
                m_log.ErrorFormat(
                    "[ATTACHMENTS MODULE]: Could not find presence for client {0} {1} in RezSingleAttachmentFromInventory()",
                    remoteClient.Name, remoteClient.AgentId);
                return null;
            }

            return RezSingleAttachmentFromInventory(sp, itemID, AttachmentPt);
        }

        private void Client_OnRezMultipleAttachmentsFromInv(IClientAPI remoteClient, List<KeyValuePair<UUID, uint>> rezlist)
        {
            if (!Enabled)
                return;

            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);
            if (sp != null)
                RezMultipleAttachmentsFromInventory(sp, rezlist);
            else
                m_log.ErrorFormat(
                    "[ATTACHMENTS MODULE]: Could not find presence for client {0} {1} in RezMultipleAttachmentsFromInventory()",
                    remoteClient.Name, remoteClient.AgentId);
        }

        private void Client_OnObjectAttach(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, bool silent)
        {
            //            m_log.DebugFormat(
            //                "[ATTACHMENTS MODULE]: Attaching object local id {0} to {1} point {2} from ground (silent = {3})",
            //                objectLocalID, remoteClient.Name, AttachmentPt, silent);

            if (!Enabled)
                return;

            try
            {
                ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);

                if (sp == null)
                {
                    m_log.ErrorFormat(
                        "[ATTACHMENTS MODULE]: Could not find presence for client {0} {1}", remoteClient.Name, remoteClient.AgentId);
                    return;
                }

                // If we can't take it, we can't attach it!
                SceneObjectPart part = m_scene.GetSceneObjectPart(objectLocalID);
                if (part == null)
                    return;

                if (!m_scene.Permissions.CanTakeObject(part.UUID, remoteClient.AgentId))
                {
                    remoteClient.SendAgentAlertMessage(
                        "You don't have sufficient permissions to attach this object", false);

                    return;
                }

                // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
                // be removed when that functionality is implemented in opensim
                AttachmentPt &= 0x7f;

                // Calls attach with a Zero position
                if (AttachObject(sp, part.ParentGroup, AttachmentPt, false))
                {
                    m_scene.EventManager.TriggerOnAttach(objectLocalID, part.ParentGroup.GetFromItemID(), remoteClient.AgentId);

                    // Save avatar attachment information
                    m_log.Debug(
                        "[ATTACHMENTS MODULE]: Saving avatar attachment. AgentID: " + remoteClient.AgentId
                        + ", AttachmentPoint: " + AttachmentPt);

                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[ATTACHMENTS MODULE]: exception upon Attach Object {0}{1}", e.Message, e.StackTrace);
            }
        }

        private void Client_OnObjectDetach(uint objectLocalID, IClientAPI remoteClient)
        {
            if (!Enabled)
                return;

            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);
            SceneObjectGroup group = m_scene.GetGroupByPrim(objectLocalID);
            if (sp != null && group != null)
                DetachSingleAttachmentToInv(sp, group.GetFromItemID());
        }

        private void Client_OnDetachAttachmentIntoInv(UUID itemID, IClientAPI remoteClient)
        {
            if (!Enabled)
                return;

            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);
            if (sp != null)
                DetachSingleAttachmentToInv(sp, itemID);
        }

        private void Client_OnObjectDrop(uint soLocalId, IClientAPI remoteClient)
        {
            if (!Enabled)
                return;

            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);
            if (sp != null)
                DetachSingleAttachmentToGround(sp, soLocalId);
        }

        #endregion
    }
}
