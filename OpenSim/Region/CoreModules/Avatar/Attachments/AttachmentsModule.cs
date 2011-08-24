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
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private Scene m_scene = null;
        private IDialogModule m_dialogModule;
        
        public string Name { get { return "Attachments Module"; } }
        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source) {}
        
        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_dialogModule = m_scene.RequestModuleInterface<IDialogModule>();
            m_scene.RegisterModuleInterface<IAttachmentsModule>(this);
            m_scene.EventManager.OnNewClient += SubscribeToClientEvents;
            // TODO: Should probably be subscribing to CloseClient too, but this doesn't yet give us IClientAPI
        }
        
        public void RemoveRegion(Scene scene) 
        {
            m_scene.UnregisterModuleInterface<IAttachmentsModule>(this);
            m_scene.EventManager.OnNewClient -= SubscribeToClientEvents;
        }
        
        public void RegionLoaded(Scene scene) {}
        
        public void Close() 
        {
            RemoveRegion(m_scene);
        }
        
        public void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRezSingleAttachmentFromInv += RezSingleAttachmentFromInventory;
            client.OnRezMultipleAttachmentsFromInv += RezMultipleAttachmentsFromInventory;
            client.OnObjectAttach += AttachObject;
            client.OnObjectDetach += DetachObject;
            client.OnDetachAttachmentIntoInv += DetachSingleAttachmentToInv;
        }
        
        public void UnsubscribeFromClientEvents(IClientAPI client)
        {
            client.OnRezSingleAttachmentFromInv -= RezSingleAttachmentFromInventory;
            client.OnRezMultipleAttachmentsFromInv -= RezMultipleAttachmentsFromInventory;
            client.OnObjectAttach -= AttachObject;
            client.OnObjectDetach -= DetachObject;
            client.OnDetachAttachmentIntoInv -= DetachSingleAttachmentToInv;
        }
        
        /// <summary>
        /// Called by client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="objectLocalID"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="silent"></param>
        public void AttachObject(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, bool silent)
        {
//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: Attaching object local id {0} to {1} point {2} from ground (silent = {3})",
//                objectLocalID, remoteClient.Name, AttachmentPt, silent);

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
                    m_log.Info(
                        "[ATTACHMENTS MODULE]: Saving avatar attachment. AgentID: " + remoteClient.AgentId
                        + ", AttachmentPoint: " + AttachmentPt);

                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[ATTACHMENTS MODULE]: exception upon Attach Object {0}{1}", e.Message, e.StackTrace);
            }
        }

        public bool AttachObject(IClientAPI remoteClient, SceneObjectGroup group, uint AttachmentPt, bool silent)
        {
            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);

            if (sp == null)
            {
                m_log.ErrorFormat(
                    "[ATTACHMENTS MODULE]: Could not find presence for client {0} {1}", remoteClient.Name, remoteClient.AgentId);
                return false;
            }

            return AttachObject(sp, group, AttachmentPt, silent);
        }
        
        private bool AttachObject(ScenePresence sp, SceneObjectGroup group, uint AttachmentPt, bool silent)
        {
//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: Attaching object {0} {1} to {2} point {3} from ground (silent = {4})",
//                group.Name, group.LocalId, sp.Name, AttachmentPt, silent);

            if (sp.GetAttachments(AttachmentPt).Contains(group))
            {
//                m_log.WarnFormat(
//                    "[ATTACHMENTS MODULE]: Ignoring request to attach {0} {1} to {2} on {3} since it's already attached",
//                    group.Name, group.LocalId, sp.Name, AttachmentPt);

                return false;
            }

            Vector3 attachPos = group.AbsolutePosition;

            // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
            // be removed when that functionality is implemented in opensim
            AttachmentPt &= 0x7f;
            
            // If the attachment point isn't the same as the one previously used
            // set it's offset position = 0 so that it appears on the attachment point
            // and not in a weird location somewhere unknown.
            if (AttachmentPt != 0 && AttachmentPt != (uint)group.GetAttachmentPoint())
            {
                attachPos = Vector3.Zero;
            }

            // AttachmentPt 0 means the client chose to 'wear' the attachment.
            if (AttachmentPt == 0)
            {
                // Check object for stored attachment point
                AttachmentPt = (uint)group.GetAttachmentPoint();
            }

            // if we still didn't find a suitable attachment point.......
            if (AttachmentPt == 0)
            {
                // Stick it on left hand with Zero Offset from the attachment point.
                AttachmentPt = (uint)AttachmentPoint.LeftHand;
                attachPos = Vector3.Zero;
            }

            group.SetAttachmentPoint((byte)AttachmentPt);
            group.AbsolutePosition = attachPos;

            // Remove any previous attachments
            UUID itemID = UUID.Zero;
            foreach (SceneObjectGroup grp in sp.Attachments)
            {
                if (grp.GetAttachmentPoint() == (byte)AttachmentPt)
                {
                    itemID = grp.GetFromItemID();
                    break;
                }
            }

            if (itemID != UUID.Zero)
                DetachSingleAttachmentToInv(itemID, sp);

            itemID = group.GetFromItemID();
            if (itemID == UUID.Zero)
                itemID = AddSceneObjectAsAttachment(sp.ControllingClient, group).ID;

            ShowAttachInUserInventory(sp, AttachmentPt, itemID, group);

            AttachToAgent(sp, group, AttachmentPt, attachPos, silent);

            return true;
        }

        public void RezMultipleAttachmentsFromInventory(
            IClientAPI remoteClient, 
            RezMultipleAttachmentsFromInvPacket.HeaderDataBlock header,
            RezMultipleAttachmentsFromInvPacket.ObjectDataBlock[] objects)
        {
            foreach (RezMultipleAttachmentsFromInvPacket.ObjectDataBlock obj in objects)
            {
                RezSingleAttachmentFromInventory(remoteClient, obj.ItemID, obj.AttachmentPt);
            }
        }
        
        public UUID RezSingleAttachmentFromInventory(IClientAPI remoteClient, UUID itemID, uint AttachmentPt)
        {
            return RezSingleAttachmentFromInventory(remoteClient, itemID, AttachmentPt, true);
        }

        public UUID RezSingleAttachmentFromInventory(
            IClientAPI remoteClient, UUID itemID, uint AttachmentPt, bool updateInventoryStatus)
        {
//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: Rezzing attachment to point {0} from item {1} for {2}", 
//                (AttachmentPoint)AttachmentPt, itemID, remoteClient.Name);

            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);

            if (sp == null)
            {
                m_log.ErrorFormat(
                    "[ATTACHMENTS MODULE]: Could not find presence for client {0} {1} in RezSingleAttachmentFromInventory()",
                    remoteClient.Name, remoteClient.AgentId);
                return UUID.Zero;
            }
            
            // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
            // be removed when that functionality is implemented in opensim
            AttachmentPt &= 0x7f;

            SceneObjectGroup att = RezSingleAttachmentFromInventoryInternal(sp, itemID, AttachmentPt);

            if (updateInventoryStatus)
            {
                if (att == null)
                    DetachSingleAttachmentToInv(itemID, sp.ControllingClient);
                else
                    ShowAttachInUserInventory(att, sp, itemID, AttachmentPt);
            }

            if (null == att)
                return UUID.Zero;
            else
                return att.UUID;
        }

        private SceneObjectGroup RezSingleAttachmentFromInventoryInternal(
            ScenePresence sp, UUID itemID, uint AttachmentPt)
        {
            IInventoryAccessModule invAccess = m_scene.RequestModuleInterface<IInventoryAccessModule>();
            if (invAccess != null)
            {
                SceneObjectGroup objatt = invAccess.RezObject(sp.ControllingClient,
                    itemID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                    false, false, sp.UUID, true);

//                m_log.DebugFormat(
//                    "[ATTACHMENTS MODULE]: Retrieved single object {0} for attachment to {1} on point {2}",
//                    objatt.Name, remoteClient.Name, AttachmentPt);
                
                if (objatt != null)
                {
                    // Loading the inventory from XML will have set this, but
                    // there is no way the object could have changed yet,
                    // since scripts aren't running yet. So, clear it here.
                    objatt.HasGroupChanged = false;
                    bool tainted = false;
                    if (AttachmentPt != 0 && AttachmentPt != objatt.GetAttachmentPoint())
                        tainted = true;

                    // This will throw if the attachment fails
                    try
                    {
                        AttachObject(sp, objatt, AttachmentPt, false);
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
                }
                else
                {
                    m_log.WarnFormat(
                        "[ATTACHMENTS MODULE]: Could not retrieve item {0} for attaching to avatar {1} at point {2}", 
                        itemID, sp.Name, AttachmentPt);
                }
                
                return objatt;
            }
            
            return null;
        }
        
        /// <summary>
        /// Update the user inventory to the attachment of an item
        /// </summary>
        /// <param name="att"></param>
        /// <param name="sp"></param>
        /// <param name="itemID"></param>
        /// <param name="AttachmentPt"></param>
        /// <returns></returns>
        private UUID ShowAttachInUserInventory(
            SceneObjectGroup att, ScenePresence sp, UUID itemID, uint AttachmentPt)
        {
//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: Updating inventory of {0} to show attachment of {1} (item ID {2})", 
//                remoteClient.Name, att.Name, itemID);
            
            if (!att.IsDeleted)
                AttachmentPt = att.RootPart.AttachmentPoint;

            InventoryItemBase item = new InventoryItemBase(itemID, sp.UUID);
            item = m_scene.InventoryService.GetItem(item);

            bool changed = sp.Appearance.SetAttachment((int)AttachmentPt, itemID, item.AssetID);
            if (changed && m_scene.AvatarFactory != null)
                m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);
            
            return att.UUID;
        }

        /// <summary>
        /// Update the user inventory to reflect an attachment
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="itemID"></param>
        /// <param name="att"></param>
        private void ShowAttachInUserInventory(
            ScenePresence sp, uint AttachmentPt, UUID itemID, SceneObjectGroup att)
        {
//            m_log.DebugFormat(
//                "[USER INVENTORY]: Updating attachment {0} for {1} at {2} using item ID {3}", 
//                att.Name, remoteClient.Name, AttachmentPt, itemID);
            
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

            if (null == att.RootPart)
            {
                m_log.Error("[ATTACHMENTS MODULE]: Unable to save attachment for a prim without the rootpart!");
                return;
            }

            InventoryItemBase item = new InventoryItemBase(itemID, sp.UUID);
            item = m_scene.InventoryService.GetItem(item);
            bool changed = sp.Appearance.SetAttachment((int)AttachmentPt, itemID, item.AssetID);
            if (changed && m_scene.AvatarFactory != null)
                m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);
        }

        public void DetachObject(uint objectLocalID, IClientAPI remoteClient)
        {
            SceneObjectGroup group = m_scene.GetGroupByPrim(objectLocalID);
            if (group != null)
            {
                DetachSingleAttachmentToInv(group.GetFromItemID(), remoteClient);
            }
        }
        
        public void DetachSingleAttachmentToInv(UUID itemID, IClientAPI remoteClient)
        {
            ScenePresence presence;
            if (m_scene.TryGetScenePresence(remoteClient.AgentId, out presence))
            {
                // Save avatar attachment information
                m_log.Debug("[ATTACHMENTS MODULE]: Detaching from UserID: " + remoteClient.AgentId + ", ItemID: " + itemID);

                bool changed = presence.Appearance.DetachAttachment(itemID);
                if (changed && m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(remoteClient.AgentId);

                DetachSingleAttachmentToInv(itemID, presence);
            }
        }

        public void DetachSingleAttachmentToGround(UUID itemID, IClientAPI remoteClient)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(itemID);
            if (part == null || part.ParentGroup == null)
                return;

            if (part.ParentGroup.RootPart.AttachedAvatar != remoteClient.AgentId)
                return;

            UUID inventoryID = part.ParentGroup.GetFromItemID();

            ScenePresence presence;
            if (m_scene.TryGetScenePresence(remoteClient.AgentId, out presence))
            {
                if (!m_scene.Permissions.CanRezObject(
                    part.ParentGroup.PrimCount, remoteClient.AgentId, presence.AbsolutePosition))
                    return;

                bool changed = presence.Appearance.DetachAttachment(itemID);
                if (changed && m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(remoteClient.AgentId);

                part.ParentGroup.DetachToGround();

                List<UUID> uuids = new List<UUID>();
                uuids.Add(inventoryID);
                m_scene.InventoryService.DeleteItems(remoteClient.AgentId, uuids);
                remoteClient.SendRemoveInventoryItem(inventoryID);
            }

            m_scene.EventManager.TriggerOnAttach(part.ParentGroup.LocalId, itemID, UUID.Zero);
        }
        
        // What makes this method odd and unique is it tries to detach using an UUID....     Yay for standards.
        // To LocalId or UUID, *THAT* is the question. How now Brown UUID??
        private void DetachSingleAttachmentToInv(UUID itemID, ScenePresence sp)
        {
            if (itemID == UUID.Zero) // If this happened, someone made a mistake....
                return;

            // We can NOT use the dictionries here, as we are looking
            // for an entity by the fromAssetID, which is NOT the prim UUID
            EntityBase[] detachEntities = m_scene.GetEntities();
            SceneObjectGroup group;

            foreach (EntityBase entity in detachEntities)
            {
                if (entity is SceneObjectGroup)
                {
                    group = (SceneObjectGroup)entity;
                    if (group.GetFromItemID() == itemID)
                    {
                        m_scene.EventManager.TriggerOnAttach(group.LocalId, itemID, UUID.Zero);
                        group.DetachToInventoryPrep();
//                        m_log.Debug("[ATTACHMENTS MODULE]: Saving attachpoint: " + ((uint)group.GetAttachmentPoint()).ToString());

                        // If an item contains scripts, it's always changed.
                        // This ensures script state is saved on detach
                        foreach (SceneObjectPart p in group.Parts)
                            if (p.Inventory.ContainsScripts())
                                group.HasGroupChanged = true;

                        UpdateKnownItem(sp.ControllingClient, group, group.GetFromItemID(), group.OwnerID);
                        m_scene.DeleteSceneObject(group, false);
                        return;
                    }
                }
            }
        }
        
        public void UpdateAttachmentPosition(SceneObjectGroup sog, Vector3 pos)
        {
            // First we save the
            // attachment point information, then we update the relative 
            // positioning. Then we have to mark the object as NOT an
            // attachment. This is necessary in order to correctly save
            // and retrieve GroupPosition information for the attachment.
            // Finally, we restore the object's attachment status.
            byte attachmentPoint = sog.GetAttachmentPoint();
            sog.UpdateGroupPosition(pos);
            sog.RootPart.IsAttachment = false;
            sog.AbsolutePosition = sog.RootPart.AttachedPos;
            sog.SetAttachmentPoint(attachmentPoint);                                       
            sog.HasGroupChanged = true;            
        }
        
        /// <summary>
        /// Update the attachment asset for the new sog details if they have changed.
        /// </summary>
        /// <remarks>
        /// This is essential for preserving attachment attributes such as permission.  Unlike normal scene objects,
        /// these details are not stored on the region.
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="grp"></param>
        /// <param name="itemID"></param>
        /// <param name="agentID"></param>
        public void UpdateKnownItem(IClientAPI remoteClient, SceneObjectGroup grp, UUID itemID, UUID agentID)
        {
            if (grp != null)
            {
                if (!grp.HasGroupChanged)
                {
                    m_log.WarnFormat("[ATTACHMENTS MODULE]: Save request for {0} which is unchanged", grp.UUID);
                    return;
                }

                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Updating asset for attachment {0}, attachpoint {1}",
                    grp.UUID, grp.GetAttachmentPoint());

                // If we're being called from a script, then trying to serialize that same script's state will not complete
                // in any reasonable time period.  Therefore, we'll avoid it.  The worst that can happen is that if
                // the client/server crashes rather than logging out normally, the attachment's scripts will resume
                // without state on relog.  Arguably, this is what we want anyway.
                string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(grp, false);

                InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
                item = m_scene.InventoryService.GetItem(item);

                if (item != null)
                {
                    AssetBase asset = m_scene.CreateAsset(
                        grp.GetPartName(grp.LocalId),
                        grp.GetPartDescription(grp.LocalId),
                        (sbyte)AssetType.Object,
                        Utils.StringToBytes(sceneObjectXml),
                        remoteClient.AgentId);
                    m_scene.AssetService.Store(asset);

                    item.AssetID = asset.FullID;
                    item.Description = asset.Description;
                    item.Name = asset.Name;
                    item.AssetType = asset.Type;
                    item.InvType = (int)InventoryType.Object;

                    m_scene.InventoryService.UpdateItem(item);

                    // this gets called when the agent logs off!
                    if (remoteClient != null)
                        remoteClient.SendInventoryItemCreateUpdate(item, 0);
                }
            }
        } 
        
        /// <summary>
        /// Attach this scene object to the given avatar.
        /// </summary>
        /// 
        /// This isn't publicly available since attachments should always perform the corresponding inventory 
        /// operation (to show the attach in user inventory and update the asset with positional information).
        /// 
        /// <param name="sp"></param>
        /// <param name="so"></param>
        /// <param name="attachmentpoint"></param>
        /// <param name="attachOffset"></param>
        /// <param name="silent"></param>
        protected void AttachToAgent(ScenePresence avatar, SceneObjectGroup so, uint attachmentpoint, Vector3 attachOffset, bool silent)
        {
//            m_log.DebugFormat("[ATTACHMENTS MODULE]: Adding attachment {0} to avatar {1} in pt {2} pos {3} {4}",
//                so.Name, avatar.Name, attachmentpoint, attachOffset, so.RootPart.AttachedPos);
                              
            so.DetachFromBackup();

            // Remove from database and parcel prim count
            m_scene.DeleteFromStorage(so.UUID);
            m_scene.EventManager.TriggerParcelPrimCountTainted();

            so.RootPart.AttachedAvatar = avatar.UUID;

            //Anakin Lohner bug #3839 
            SceneObjectPart[] parts = so.Parts;
            for (int i = 0; i < parts.Length; i++)
                parts[i].AttachedAvatar = avatar.UUID;

            if (so.RootPart.PhysActor != null)
            {
                m_scene.PhysicsScene.RemovePrim(so.RootPart.PhysActor);
                so.RootPart.PhysActor = null;
            }

            so.AbsolutePosition = attachOffset;
            so.RootPart.AttachedPos = attachOffset;
            so.RootPart.IsAttachment = true;

            so.RootPart.SetParentLocalId(avatar.LocalId);
            so.SetAttachmentPoint(Convert.ToByte(attachmentpoint));

            avatar.AddAttachment(so);

            if (!silent)
            {
                // Killing it here will cause the client to deselect it
                // It then reappears on the avatar, deselected
                // through the full update below
                //
                if (so.IsSelected)
                {
                    m_scene.SendKillObject(so.RootPart.LocalId);
                }

                so.IsSelected = false; // fudge....
                so.ScheduleGroupForFullUpdate();
            }
                            
            // In case it is later dropped again, don't let
            // it get cleaned up
            so.RootPart.RemFlag(PrimFlags.TemporaryOnRez);
        }

        /// <summary>
        /// Add a scene object that was previously free in the scene as an attachment to an avatar.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="grp"></param>
        /// <returns>The user inventory item created that holds the attachment.</returns>
        private InventoryItemBase AddSceneObjectAsAttachment(IClientAPI remoteClient, SceneObjectGroup grp)
        {
//            m_log.DebugFormat("[SCENE]: Called AddSceneObjectAsAttachment for object {0} {1} for {2} {3} {4}", grp.Name, grp.LocalId, remoteClient.Name, remoteClient.AgentId, AgentId);

            grp.UpdatePrimFlags(grp.LocalId, grp.UsesPhysics, grp.IsTemporary, true, grp.IsVolumeDetect);
            
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
                remoteClient.AgentId);

            m_scene.AssetService.Store(asset);

            InventoryItemBase item = new InventoryItemBase();
            item.CreatorId = grp.RootPart.CreatorID.ToString();
            item.CreatorData = grp.RootPart.CreatorData;
            item.Owner = remoteClient.AgentId;
            item.ID = UUID.Random();
            item.AssetID = asset.FullID;
            item.Description = asset.Description;
            item.Name = asset.Name;
            item.AssetType = asset.Type;
            item.InvType = (int)InventoryType.Object;

            InventoryFolderBase folder = m_scene.InventoryService.GetFolderForType(remoteClient.AgentId, AssetType.Object);
            if (folder != null)
                item.Folder = folder.ID;
            else // oopsies
                item.Folder = UUID.Zero;

            if ((remoteClient.AgentId != grp.RootPart.OwnerID) && m_scene.Permissions.PropagatePermissions())
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
                remoteClient.SendInventoryItemCreateUpdate(item, 0);
            }
            else
            {
                if (m_dialogModule != null)
                    m_dialogModule.SendAlertToUser(remoteClient, "Operation failed");
            }

            return item;
        }
    }
}
