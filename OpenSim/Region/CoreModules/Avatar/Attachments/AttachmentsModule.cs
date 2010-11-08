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
        
        protected Scene m_scene = null;
        
        public string Name { get { return "Attachments Module"; } }
        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source) {}
        
        public void AddRegion(Scene scene)
        {
            m_scene = scene;
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
            client.OnDetachAttachmentIntoInv += ShowDetachInUserInventory;
        }
        
        public void UnsubscribeFromClientEvents(IClientAPI client)
        {
            client.OnRezSingleAttachmentFromInv -= RezSingleAttachmentFromInventory;
            client.OnRezMultipleAttachmentsFromInv -= RezMultipleAttachmentsFromInventory;
            client.OnObjectAttach -= AttachObject;
            client.OnObjectDetach -= DetachObject;
            client.OnDetachAttachmentIntoInv -= ShowDetachInUserInventory;
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
            m_log.Debug("[ATTACHMENTS MODULE]: Invoking AttachObject");

            try
            {
                // If we can't take it, we can't attach it!
                SceneObjectPart part = m_scene.GetSceneObjectPart(objectLocalID);
                if (part == null)
                    return;

                if (!m_scene.Permissions.CanTakeObject(part.UUID, remoteClient.AgentId))
                    return;

                // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
                // be removed when that functionality is implemented in opensim
                AttachmentPt &= 0x7f;

                // Calls attach with a Zero position
                if (AttachObject(remoteClient, part.ParentGroup, AttachmentPt, false))
                {
                    m_scene.EventManager.TriggerOnAttach(objectLocalID, part.ParentGroup.GetFromItemID(), remoteClient.AgentId);

                    // Save avatar attachment information
                    m_log.Info(
                        "[ATTACHMENTS MODULE]: Saving avatar attachment. AgentID: " + remoteClient.AgentId
                        + ", AttachmentPoint: " + AttachmentPt);

                    if (m_scene.AvatarFactory != null)
                        m_scene.AvatarFactory.QueueAppearanceSave(remoteClient.AgentId);
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ATTACHMENTS MODULE]: exception upon Attach Object {0}", e);
            }
        }
        
        public bool AttachObject(IClientAPI remoteClient, SceneObjectGroup group, uint AttachmentPt, bool silent)
        {
            Vector3 attachPos = group.AbsolutePosition;

            if (m_scene.Permissions.CanTakeObject(group.UUID, remoteClient.AgentId))
            {
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
                ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);
                UUID itemID = UUID.Zero;
                if (sp != null)
                {
                    foreach(SceneObjectGroup grp in sp.Attachments)
                    {
                        if (grp.GetAttachmentPoint() == (byte)AttachmentPt)
                        {
                            itemID = grp.GetFromItemID();
                            break;
                        }
                    }
                    if (itemID != UUID.Zero)
                        DetachSingleAttachmentToInv(itemID, remoteClient);
                }

                if (group.GetFromItemID() == UUID.Zero)
                {
                    m_scene.attachObjectAssetStore(remoteClient, group, remoteClient.AgentId, out itemID);
                }
                else
                {
                    itemID = group.GetFromItemID();
                }

                ShowAttachInUserInventory(remoteClient, AttachmentPt, itemID, group);

                AttachToAgent(sp, group, AttachmentPt, attachPos, silent);
            }
            else
            {
                remoteClient.SendAgentAlertMessage(
                    "You don't have sufficient permissions to attach this object", false);
                
                return false;
            }

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
            m_log.DebugFormat(
                "[ATTACHMENTS MODULE]: Rezzing attachment to point {0} from item {1} for {2}", 
                (AttachmentPoint)AttachmentPt, itemID, remoteClient.Name);
            
            // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
            // be removed when that functionality is implemented in opensim
            AttachmentPt &= 0x7f;

            SceneObjectGroup att = RezSingleAttachmentFromInventoryInternal(remoteClient, itemID, AttachmentPt);

            if (updateInventoryStatus)
            {
                if (att == null)
                    ShowDetachInUserInventory(itemID, remoteClient);
                else
                    ShowAttachInUserInventory(att, remoteClient, itemID, AttachmentPt);
            }

            if (null == att)
                return UUID.Zero;
            else
                return att.UUID;
        }

        protected SceneObjectGroup RezSingleAttachmentFromInventoryInternal(
            IClientAPI remoteClient, UUID itemID, uint AttachmentPt)
        {
            IInventoryAccessModule invAccess = m_scene.RequestModuleInterface<IInventoryAccessModule>();
            if (invAccess != null)
            {
                SceneObjectGroup objatt = invAccess.RezObject(remoteClient,
                    itemID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                    false, false, remoteClient.AgentId, true);

//                m_log.DebugFormat(
//                    "[ATTACHMENTS MODULE]: Retrieved single object {0} for attachment to {1} on point {2}", 
//                    objatt.Name, remoteClient.Name, AttachmentPt);
                
                if (objatt != null)
                {
                    bool tainted = false;
                    if (AttachmentPt != 0 && AttachmentPt != objatt.GetAttachmentPoint())
                        tainted = true;

                    // This will throw if the attachment fails
                    try
                    {
                        AttachObject(remoteClient, objatt, AttachmentPt, false);
                    }
                    catch
                    {
                        // Make sure the object doesn't stick around and bail
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
                    m_scene.EventManager.TriggerOnAttach(objatt.LocalId, itemID, remoteClient.AgentId);
                }
                else
                {
                    m_log.WarnFormat(
                        "[ATTACHMENTS MODULE]: Could not retrieve item {0} for attaching to avatar {1} at point {2}", 
                        itemID, remoteClient.Name, AttachmentPt);
                }
                
                return objatt;
            }
            
            return null;
        }
        
        /// <summary>
        /// Update the user inventory to the attachment of an item
        /// </summary>
        /// <param name="att"></param>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="AttachmentPt"></param>
        /// <returns></returns>
        protected UUID ShowAttachInUserInventory(
            SceneObjectGroup att, IClientAPI remoteClient, UUID itemID, uint AttachmentPt)
        {
//            m_log.DebugFormat(
//                "[ATTACHMENTS MODULE]: Updating inventory of {0} to show attachment of {1} (item ID {2})", 
//                remoteClient.Name, att.Name, itemID);
            
            if (!att.IsDeleted)
                AttachmentPt = att.RootPart.AttachmentPoint;

            ScenePresence presence;
            if (m_scene.TryGetScenePresence(remoteClient.AgentId, out presence))
            {
                InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
                item = m_scene.InventoryService.GetItem(item);

                presence.Appearance.SetAttachment((int)AttachmentPt, itemID, item.AssetID /*att.UUID*/);
            }
            
            return att.UUID;
        }

        /// <summary>
        /// Update the user inventory to reflect an attachment
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="itemID"></param>
        /// <param name="att"></param>
        protected void ShowAttachInUserInventory(
            IClientAPI remoteClient, uint AttachmentPt, UUID itemID, SceneObjectGroup att)
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

            ScenePresence presence;
            if (m_scene.TryGetScenePresence(remoteClient.AgentId, out presence))
            {
                // XXYY!!
                InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
                item = m_scene.InventoryService.GetItem(item);
                presence.Appearance.SetAttachment((int)AttachmentPt, itemID, item.AssetID /* att.UUID */);

                if (m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(remoteClient.AgentId);
            }
        }

        public void DetachObject(uint objectLocalID, IClientAPI remoteClient)
        {
            SceneObjectGroup group = m_scene.GetGroupByPrim(objectLocalID);
            if (group != null)
            {
                //group.DetachToGround();
                ShowDetachInUserInventory(group.GetFromItemID(), remoteClient);
            }
        }
        
        public void ShowDetachInUserInventory(UUID itemID, IClientAPI remoteClient)
        {
            ScenePresence presence;
            if (m_scene.TryGetScenePresence(remoteClient.AgentId, out presence))
            {
                presence.Appearance.DetachAttachment(itemID);

                // Save avatar attachment information
                m_log.Debug("[ATTACHMENTS MODULE]: Detaching from UserID: " + remoteClient.AgentId + ", ItemID: " + itemID);
                if (m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(remoteClient.AgentId);
            }

            DetachSingleAttachmentToInv(itemID, remoteClient);
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

                presence.Appearance.DetachAttachment(itemID);

                if (m_scene.AvatarFactory != null)
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
        protected void DetachSingleAttachmentToInv(UUID itemID, IClientAPI remoteClient)
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
                        m_log.Debug("[ATTACHMENTS MODULE]: Saving attachpoint: " + ((uint)group.GetAttachmentPoint()).ToString());
                        UpdateKnownItem(remoteClient, group, group.GetFromItemID(), group.OwnerID);
                        m_scene.DeleteSceneObject(group, false);
                        return;
                    }
                }
            }
        }
        
        public void UpdateAttachmentPosition(IClientAPI client, SceneObjectGroup sog, Vector3 pos)
        {
            // If this is an attachment, then we need to save the modified
            // object back into the avatar's inventory. First we save the
            // attachment point information, then we update the relative 
            // positioning (which caused this method to get driven in the
            // first place. Then we have to mark the object as NOT an
            // attachment. This is necessary in order to correctly save
            // and retrieve GroupPosition information for the attachment.
            // Then we save the asset back into the appropriate inventory
            // entry. Finally, we restore the object's attachment status.
            byte attachmentPoint = sog.GetAttachmentPoint();
            sog.UpdateGroupPosition(pos);
            sog.RootPart.IsAttachment = false;
            sog.AbsolutePosition = sog.RootPart.AttachedPos;
            UpdateKnownItem(client, sog, sog.GetFromItemID(), sog.OwnerID);
            sog.SetAttachmentPoint(attachmentPoint);
        }
        
        /// <summary>
        /// Update the attachment asset for the new sog details if they have changed.
        /// </summary>
        /// 
        /// This is essential for preserving attachment attributes such as permission.  Unlike normal scene objects,
        /// these details are not stored on the region.
        /// 
        /// <param name="remoteClient"></param>
        /// <param name="grp"></param>
        /// <param name="itemID"></param>
        /// <param name="agentID"></param>
        protected void UpdateKnownItem(IClientAPI remoteClient, SceneObjectGroup grp, UUID itemID, UUID agentID)
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

                string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(grp);

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
        /// <param name="AttachOffset"></param>
        /// <param name="silent"></param>
        protected void AttachToAgent(ScenePresence avatar, SceneObjectGroup so, uint attachmentpoint, Vector3 AttachOffset, bool silent)
        {
            // don't attach attachments to child agents
            if (avatar.IsChildAgent) return;

//                m_log.DebugFormat("[ATTACHMENTS MODULE]: Adding attachment {0} to avatar {1}", Name, avatar.Name);
                              
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

            so.AbsolutePosition = AttachOffset;
            so.RootPart.AttachedPos = AttachOffset;
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
            so.HasGroupChanged = false;
        }
    }
}
