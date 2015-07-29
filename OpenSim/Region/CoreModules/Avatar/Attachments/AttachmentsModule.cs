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
using System.IO;
using System.Threading;
using System.Xml;
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

        public int DebugLevel { get; set; }

        /// <summary>
        /// Period to sleep per 100 prims in order to avoid CPU spikes when an avatar with many attachments logs in/changes
        /// outfit or many avatars with a medium levels of attachments login/change outfit simultaneously.
        /// </summary>
        /// <remarks>
        /// A value of 0 will apply no pause.  The pause is specified in milliseconds.
        /// </remarks>
        public int ThrottlePer100PrimsRezzed { get; set; }
        
        private Scene m_scene;
        private IInventoryAccessModule m_invAccessModule;

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
            {
                Enabled = config.GetBoolean("Enabled", true);

                ThrottlePer100PrimsRezzed = config.GetInt("ThrottlePer100PrimsRezzed", 0);
            }
            else
            {
                Enabled = true;
            }
        }
        
        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            if (Enabled)
            {
                // Only register module with scene if it is enabled. All callers check for a null attachments module.
                // Ideally, there should be a null attachments module for when this core attachments module has been 
                // disabled. Registering only when enabled allows for other attachments module implementations.
                m_scene.RegisterModuleInterface<IAttachmentsModule>(this);
                m_scene.EventManager.OnNewClient += SubscribeToClientEvents;
                m_scene.EventManager.OnStartScript += (localID, itemID) => HandleScriptStateChange(localID, true);
                m_scene.EventManager.OnStopScript += (localID, itemID) => HandleScriptStateChange(localID, false);

                MainConsole.Instance.Commands.AddCommand(
                    "Debug",
                    false,
                    "debug attachments log",
                    "debug attachments log [0|1]",
                    "Turn on attachments debug logging",
                    "  <= 0 - turns off debug logging\n"
                        + "  >= 1 - turns on attachment message debug logging",
                    HandleDebugAttachmentsLog);

                MainConsole.Instance.Commands.AddCommand(
                    "Debug",
                    false,
                    "debug attachments throttle",
                    "debug attachments throttle <ms>",
                    "Turn on attachments throttling.",
                    "This requires a millisecond value.  " +
                    "  == 0 - disable throttling.\n"
                        + "  > 0 - sleeps for this number of milliseconds per 100 prims rezzed.",
                    HandleDebugAttachmentsThrottle);

                MainConsole.Instance.Commands.AddCommand(
                    "Debug",
                    false,
                    "debug attachments status",
                    "debug attachments status",
                    "Show current attachments debug status",
                    HandleDebugAttachmentsStatus);
            }

            // TODO: Should probably be subscribing to CloseClient too, but this doesn't yet give us IClientAPI
        }

        private void HandleDebugAttachmentsLog(string module, string[] args)
        {
            int debugLevel;

            if (!(args.Length == 4 && int.TryParse(args[3], out debugLevel)))
            {
                MainConsole.Instance.OutputFormat("Usage: debug attachments log [0|1]");
            }
            else
            {
                DebugLevel = debugLevel;
                MainConsole.Instance.OutputFormat(
                    "Set attachments debug level to {0} in {1}", DebugLevel, m_scene.Name);
            }
        }

        private void HandleDebugAttachmentsThrottle(string module, string[] args)
        {
            int ms;

            if (args.Length == 4 && int.TryParse(args[3], out ms))
            {
                ThrottlePer100PrimsRezzed = ms;
                MainConsole.Instance.OutputFormat(
                    "Attachments rez throttle per 100 prims is now {0} in {1}", ThrottlePer100PrimsRezzed, m_scene.Name);

                return;
            }

            MainConsole.Instance.OutputFormat("Usage: debug attachments throttle <ms>");
        }

        private void HandleDebugAttachmentsStatus(string module, string[] args)
        {
            MainConsole.Instance.OutputFormat("Settings for {0}", m_scene.Name);
            MainConsole.Instance.OutputFormat("Debug logging level: {0}", DebugLevel);
            MainConsole.Instance.OutputFormat("Throttle per 100 prims: {0}ms", ThrottlePer100PrimsRezzed);
        }

        /// <summary>
        /// Listen for client triggered running state changes so that we can persist the script's object if necessary.
        /// </summary>
        /// <param name='localID'></param>
        /// <param name='itemID'></param>
        private void HandleScriptStateChange(uint localID, bool started)
        {
            SceneObjectGroup sog = m_scene.GetGroupByPrim(localID);
            if (sog != null && sog.IsAttachment) 
            {
                if (!started)
                {
                    // FIXME: This is a convoluted way for working out whether the script state has changed to stop
                    // because it has been manually stopped or because the stop was called in UpdateDetachedObject() below
                    // This needs to be handled in a less tangled way.
                    ScenePresence sp = m_scene.GetScenePresence(sog.AttachedAvatar);
                    if (sp.ControllingClient.IsActive)
                        sog.HasGroupChanged = true;
                }
                else
                {
                    sog.HasGroupChanged = true;
                }
            }
        }
        
        public void RemoveRegion(Scene scene) 
        {
            m_scene.UnregisterModuleInterface<IAttachmentsModule>(this);

            if (Enabled)
                m_scene.EventManager.OnNewClient -= SubscribeToClientEvents;
        }
        
        public void RegionLoaded(Scene scene)
        {
            m_invAccessModule = m_scene.RequestModuleInterface<IInventoryAccessModule>();
        }
        
        public void Close() 
        {
            RemoveRegion(m_scene);
        }

        #endregion

        #region IAttachmentsModule

        public void CopyAttachments(IScenePresence sp, AgentData ad)
        {
            lock (sp.AttachmentsSyncLock)
            {
                // Attachment objects
                List<SceneObjectGroup> attachments = sp.GetAttachments();
                if (attachments.Count > 0)
                {
                    ad.AttachmentObjects = new List<ISceneObject>();
                    ad.AttachmentObjectStates = new List<string>();
    //                IScriptModule se = m_scene.RequestModuleInterface<IScriptModule>();
                    sp.InTransitScriptStates.Clear();

                    foreach (SceneObjectGroup sog in attachments)
                    {
                        // We need to make a copy and pass that copy
                        // because of transfers withn the same sim
                        ISceneObject clone = sog.CloneForNewScene();
                        // Attachment module assumes that GroupPosition holds the offsets...!
                        ((SceneObjectGroup)clone).RootPart.GroupPosition = sog.RootPart.AttachedPos;
                        ((SceneObjectGroup)clone).IsAttachment = false;
                        ad.AttachmentObjects.Add(clone);
                        string state = sog.GetStateSnapshot();
                        ad.AttachmentObjectStates.Add(state);
                        sp.InTransitScriptStates.Add(state);

                        // Scripts of the originals will be removed when the Agent is successfully removed.
                        // sog.RemoveScriptInstances(true);
                    }
                }
            }
        }

        public void CopyAttachments(AgentData ad, IScenePresence sp)
        {
//            m_log.DebugFormat("[ATTACHMENTS MODULE]: Copying attachment data into {0} in {1}", sp.Name, m_scene.Name);

            if (ad.AttachmentObjects != null && ad.AttachmentObjects.Count > 0)
            {
                lock (sp.AttachmentsSyncLock)
                    sp.ClearAttachments();

                int i = 0;
                foreach (ISceneObject so in ad.AttachmentObjects)
                {
                    ((SceneObjectGroup)so).LocalId = 0;
                    ((SceneObjectGroup)so).RootPart.ClearUpdateSchedule();

//                    m_log.DebugFormat(
//                        "[ATTACHMENTS MODULE]: Copying script state with {0} bytes for object {1} for {2} in {3}", 
//                        ad.AttachmentObjectStates[i].Length, so.Name, sp.Name, m_scene.Name);

                    so.SetState(ad.AttachmentObjectStates[i++], m_scene);
                    m_scene.IncomingCreateObject(Vector3.Zero, so);
                }
            }
        }

        public void RezAttachments(IScenePresence sp)
        {
            if (!Enabled)
                return;

            if (null == sp.Appearance)
            {
                m_log.WarnFormat("[ATTACHMENTS MODULE]: Appearance has not been initialized for agent {0}", sp.UUID);

                return;
            }

            if (sp.GetAttachments().Count > 0)
            {
                if (DebugLevel > 0)
                    m_log.DebugFormat(
                        "[ATTACHMENTS MODULE]: Not doing simulator-side attachment rez for {0} in {1} as their viewer has already rezzed attachments", 
                        m_scene.Name, sp.Name);

                  return;
            }

            if (DebugLevel > 0)
                m_log.DebugFormat("[ATTACHMENTS MODULE]: Rezzing any attachments for {0} from simulator-side", sp.Name);

            List<AvatarAttachment> attachments = sp.Appearance.GetAttachments();
            foreach (AvatarAttachment attach in attachments)
            {
                uint attachmentPt = (uint)attach.AttachPoint;

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
                    RezSingleAttachmentFromInventoryInternal(
                        sp, sp.PresenceType == PresenceType.Npc ? UUID.Zero : attach.ItemID, attach.AssetID, attachmentPt, true);
                }
                catch (Exception e)
                {
                    UUID agentId = (sp.ControllingClient == null) ? default(UUID) : sp.ControllingClient.AgentId;
                    m_log.ErrorFormat("[ATTACHMENTS MODULE]: Unable to rez attachment with itemID {0}, assetID {1}, point {2} for {3}: {4}\n{5}",
                        attach.ItemID, attach.AssetID, attachmentPt, agentId, e.Message, e.StackTrace);
                }
            }
        }

        public void DeRezAttachments(IScenePresence sp)
        {
            if (!Enabled)
                return;

            List<SceneObjectGroup> attachments = sp.GetAttachments();

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Saving for {0} attachments for {1} in {2}",
                    attachments.Count, sp.Name, m_scene.Name);

            if (attachments.Count <= 0)
                return;

            Dictionary<SceneObjectGroup, string> scriptStates = new Dictionary<SceneObjectGroup, string>();

            foreach (SceneObjectGroup so in attachments)
            {
                // Scripts MUST be snapshotted before the object is
                // removed from the scene because doing otherwise will
                // clobber the run flag
                // This must be done outside the sp.AttachmentSyncLock so that there is no risk of a deadlock from
                // scripts performing attachment operations at the same time.  Getting object states stops the scripts.
                scriptStates[so] = PrepareScriptInstanceForSave(so, false);

//                m_log.DebugFormat(
//                    "[ATTACHMENTS MODULE]: For object {0} for {1} in {2} got saved state {3}", 
//                    so.Name, sp.Name, m_scene.Name, scriptStates[so]);
            }

            lock (sp.AttachmentsSyncLock)
            {
                foreach (SceneObjectGroup so in attachments)
                    UpdateDetachedObject(sp, so, scriptStates[so]);
    
                sp.ClearAttachments();
            }
        }

        public void DeleteAttachmentsFromScene(IScenePresence sp, bool silent)
        {
            if (!Enabled)
                return;

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Deleting attachments from scene {0} for {1}, silent = {2}",
                    m_scene.RegionInfo.RegionName, sp.Name, silent);            

            foreach (SceneObjectGroup sop in sp.GetAttachments())
            {
                sop.Scene.DeleteSceneObject(sop, silent);
            }

            sp.ClearAttachments();
        }
        
        public bool AttachObject(
            IScenePresence sp, SceneObjectGroup group, uint attachmentPt, bool silent, bool addToInventory, bool append)
        {
            if (!Enabled)
                return false;

            group.DetachFromBackup();

            bool success = AttachObjectInternal(sp, group, attachmentPt, silent, addToInventory, false, append);

            if (!success)
                group.AttachToBackup();

            return success;
        }

        /// <summary>
        /// Internal method which actually does all the work for attaching an object.
        /// </summary>
        /// <returns>The object attached.</returns>
        /// <param name='sp'></param>
        /// <param name='group'>The object to attach.</param>
        /// <param name='attachmentPt'></param>
        /// <param name='silent'></param>
        /// <param name='addToInventory'>If true then add object to user inventory.</param>
        /// <param name='resumeScripts'>If true then scripts are resumed on the attached object.</param>
        /// <param name='append'>Append to attachment point rather than replace.</param>
        private bool AttachObjectInternal(
            IScenePresence sp, SceneObjectGroup group, uint attachmentPt, bool silent, bool addToInventory, bool resumeScripts, bool append)
        {
            if (group.GetSittingAvatarsCount() != 0)
            {
                if (DebugLevel > 0)
                    m_log.WarnFormat(
                        "[ATTACHMENTS MODULE]: Ignoring request to attach {0} {1} to {2} on {3} since {4} avatars are still sitting on it",
                        group.Name, group.LocalId, sp.Name, attachmentPt, group.GetSittingAvatarsCount());

                return false;
            }

            Vector3 attachPos = group.AbsolutePosition;
            // If the attachment point isn't the same as the one previously used
            // set it's offset position = 0 so that it appears on the attachment point
            // and not in a weird location somewhere unknown.
            if (attachmentPt != (uint)AttachmentPoint.Default && attachmentPt != group.AttachmentPoint)
            {
                attachPos = Vector3.Zero;
            }

            // if the attachment point is the same as previous, make sure we get the saved
            // position info.
            if (attachmentPt != 0 && attachmentPt == group.RootPart.Shape.LastAttachPoint)
            {
                attachPos = group.RootPart.AttachedPos;
            }

            // AttachmentPt 0 means the client chose to 'wear' the attachment.
            if (attachmentPt == (uint)AttachmentPoint.Default)
            {
                // Check object for stored attachment point
                attachmentPt = group.AttachmentPoint;
            }

            // if we didn't find an attach point, look for where it was last attached
            if (attachmentPt == 0)
            {
                attachmentPt = (uint)group.RootPart.Shape.LastAttachPoint;
                attachPos = group.RootPart.AttachedPos;
                group.HasGroupChanged = true;
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

            List<SceneObjectGroup> attachments = sp.GetAttachments(attachmentPt);

            if (attachments.Contains(group))
            {
                if (DebugLevel > 0)
                    m_log.WarnFormat(
                        "[ATTACHMENTS MODULE]: Ignoring request to attach {0} {1} to {2} on {3} since it's already attached",
                        group.Name, group.LocalId, sp.Name, attachmentPt);

                return false;
            }

            // If we already have 5, remove the oldest until only 4 are left. Skip over temp ones
            while (attachments.Count >= 5)
            {
                if (attachments[0].FromItemID != UUID.Zero)
                    DetachSingleAttachmentToInv(sp, attachments[0]);
                attachments.RemoveAt(0);
            }

            // If we're not appending, remove the rest as well
            if (attachments.Count != 0 && !append)
            {
                foreach (SceneObjectGroup g in attachments)
                {
                    if (g.FromItemID != UUID.Zero)
                        DetachSingleAttachmentToInv(sp, g);
                }
            }

            lock (sp.AttachmentsSyncLock)
            {
                if (addToInventory && sp.PresenceType != PresenceType.Npc)
                    UpdateUserInventoryWithAttachment(sp, group, attachmentPt, append);
    
                AttachToAgent(sp, group, attachmentPt, attachPos, silent);

                if (resumeScripts)
                {
                    // Fire after attach, so we don't get messy perms dialogs
                    // 4 == AttachedRez
                    group.CreateScriptInstances(0, true, m_scene.DefaultScriptEngine, 4);
                    group.ResumeScripts();
                }

                // Do this last so that event listeners have access to all the effects of the attachment
                m_scene.EventManager.TriggerOnAttach(group.LocalId, group.FromItemID, sp.UUID);
            }

            return true;
        }

        private void UpdateUserInventoryWithAttachment(IScenePresence sp, SceneObjectGroup group, uint attachmentPt, bool append)
        {
            // Add the new attachment to inventory if we don't already have it.
            UUID newAttachmentItemID = group.FromItemID;
            if (newAttachmentItemID == UUID.Zero)
                newAttachmentItemID = AddSceneObjectAsNewAttachmentInInv(sp, group).ID;

            ShowAttachInUserInventory(sp, attachmentPt, newAttachmentItemID, group, append);
        }

        public SceneObjectGroup RezSingleAttachmentFromInventory(IScenePresence sp, UUID itemID, uint AttachmentPt)
        {
            if (!Enabled)
                return null;

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: RezSingleAttachmentFromInventory to point {0} from item {1} for {2} in {3}",
                    (AttachmentPoint)AttachmentPt, itemID, sp.Name, m_scene.Name);

            // We check the attachments in the avatar appearance here rather than the objects attached to the
            // ScenePresence itself so that we can ignore calls by viewer 2/3 to attach objects on startup.  We are 
            // already doing this in ScenePresence.MakeRootAgent().  Simulator-side attaching needs to be done 
            // because pre-outfit folder viewers (most version 1 viewers) require it.
            bool alreadyOn = false;
            List<AvatarAttachment> existingAttachments = sp.Appearance.GetAttachments();
            foreach (AvatarAttachment existingAttachment in existingAttachments)
            {
                if (existingAttachment.ItemID == itemID)
                {
                    alreadyOn = true;
                    break;
                }
            }

            if (alreadyOn)
            {
                if (DebugLevel > 0)
                    m_log.DebugFormat(
                        "[ATTACHMENTS MODULE]: Ignoring request by {0} to wear item {1} at {2} since it is already worn",
                        sp.Name, itemID, AttachmentPt);

                return null;
            }

            bool append = (AttachmentPt & 0x80) != 0;
            AttachmentPt &= 0x7f;

            return RezSingleAttachmentFromInventoryInternal(sp, itemID, UUID.Zero, AttachmentPt, append);
        }

        public void RezMultipleAttachmentsFromInventory(IScenePresence sp, List<KeyValuePair<UUID, uint>> rezlist)
        {
            if (!Enabled)
                return;

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Rezzing {0} attachments from inventory for {1} in {2}", 
                    rezlist.Count, sp.Name, m_scene.Name);

            foreach (KeyValuePair<UUID, uint> rez in rezlist)
            {
                RezSingleAttachmentFromInventory(sp, rez.Key, rez.Value);
            }
        }

        public void DetachSingleAttachmentToGround(IScenePresence sp, uint soLocalId)
        {
            DetachSingleAttachmentToGround(sp, soLocalId, sp.AbsolutePosition, Quaternion.Identity);
        }

        public void DetachSingleAttachmentToGround(IScenePresence sp, uint soLocalId, Vector3 absolutePos, Quaternion absoluteRot)
        {
            if (!Enabled)
                return;

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: DetachSingleAttachmentToGround() for {0}, object {1}",
                    sp.UUID, soLocalId);

            SceneObjectGroup so = m_scene.GetGroupByPrim(soLocalId);

            if (so == null)
                return;

            if (so.AttachedAvatar != sp.UUID)
                return;

            UUID inventoryID = so.FromItemID;

            // As per Linden spec, drop is disabled for temp attachs
            if (inventoryID == UUID.Zero)
                return;

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: In DetachSingleAttachmentToGround(), object is {0} {1}, associated item is {2}",
                    so.Name, so.LocalId, inventoryID);

            lock (sp.AttachmentsSyncLock)
            {
                if (!m_scene.Permissions.CanRezObject(
                    so.PrimCount, sp.UUID, sp.AbsolutePosition))
                    return;

                bool changed = false;
                if (inventoryID != UUID.Zero)
                    changed = sp.Appearance.DetachAttachment(inventoryID);
                if (changed && m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);

                sp.RemoveAttachment(so);
                so.FromItemID = UUID.Zero;

                SceneObjectPart rootPart = so.RootPart;
                so.AbsolutePosition = absolutePos;
                if (absoluteRot != Quaternion.Identity)
                {
                    so.UpdateGroupRotationR(absoluteRot);
                }
                so.AttachedAvatar = UUID.Zero;
                rootPart.SetParentLocalId(0);
                so.ClearPartAttachmentData();
                rootPart.ApplyPhysics(rootPart.GetEffectiveObjectFlags(), rootPart.VolumeDetectActive);
                so.HasGroupChanged = true;
                so.RootPart.Shape.LastAttachPoint = (byte)so.AttachmentPoint;
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

        public void DetachSingleAttachmentToInv(IScenePresence sp, SceneObjectGroup so)
        {
            if (so.AttachedAvatar != sp.UUID)
            {
                m_log.WarnFormat(
                    "[ATTACHMENTS MODULE]: Tried to detach object {0} from {1} {2} but attached avatar id was {3} in {4}",
                    so.Name, sp.Name, sp.UUID, so.AttachedAvatar, m_scene.RegionInfo.RegionName);

                return;
            }

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Detaching object {0} {1} (FromItemID {2}) for {3} in {4}", 
                    so.Name, so.LocalId, so.FromItemID, sp.Name, m_scene.Name);

            // Scripts MUST be snapshotted before the object is
            // removed from the scene because doing otherwise will
            // clobber the run flag
            // This must be done outside the sp.AttachmentSyncLock so that there is no risk of a deadlock from
            // scripts performing attachment operations at the same time.  Getting object states stops the scripts.
            string scriptedState = PrepareScriptInstanceForSave(so, true);

            lock (sp.AttachmentsSyncLock)
            {
                // Save avatar attachment information
//                m_log.Debug("[ATTACHMENTS MODULE]: Detaching from UserID: " + sp.UUID + ", ItemID: " + itemID);

                bool changed = sp.Appearance.DetachAttachment(so.FromItemID);
                if (changed && m_scene.AvatarFactory != null)
                    m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);

                sp.RemoveAttachment(so);
                UpdateDetachedObject(sp, so, scriptedState);
            }
        }
        
        public void UpdateAttachmentPosition(SceneObjectGroup sog, Vector3 pos)
        {
            if (!Enabled)
                return;

            sog.UpdateGroupPosition(pos);
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
        /// <param name="saveAllScripted"></param>
        private void UpdateKnownItem(IScenePresence sp, SceneObjectGroup grp, string scriptedState)
        {
            if (grp.FromItemID == UUID.Zero)
            {
                // We can't save temp attachments
                grp.HasGroupChanged = false;
                return;
            }

            // Saving attachments for NPCs messes them up for the real owner!
            INPCModule module = m_scene.RequestModuleInterface<INPCModule>();
            if (module != null)
            {
                if (module.IsNPC(sp.UUID, m_scene))
                    return;
            }

            if (grp.HasGroupChanged)
            {
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Updating asset for attachment {0}, attachpoint {1}",
                    grp.UUID, grp.AttachmentPoint);

                string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(grp, scriptedState);

                InventoryItemBase item = new InventoryItemBase(grp.FromItemID, sp.UUID);
                item = m_scene.InventoryService.GetItem(item);

                if (item != null)
                {
                    AssetBase asset = m_scene.CreateAsset(
                        grp.GetPartName(grp.LocalId),
                        grp.GetPartDescription(grp.LocalId),
                        (sbyte)AssetType.Object,
                        Utils.StringToBytes(sceneObjectXml),
                        sp.UUID);

                    if (m_invAccessModule != null)
                        m_invAccessModule.UpdateInventoryItemAsset(sp.UUID, item, asset);

                    // If the name of the object has been changed whilst attached then we want to update the inventory
                    // item in the viewer.
                    if (sp.ControllingClient != null)
                        sp.ControllingClient.SendInventoryItemCreateUpdate(item, 0);
                }

                grp.HasGroupChanged = false; // Prevent it being saved over and over
            }
            else if (DebugLevel > 0)
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
            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Adding attachment {0} to avatar {1} at pt {2} pos {3} {4} in {5}",
                    so.Name, sp.Name, attachmentpoint, attachOffset, so.RootPart.AttachedPos, m_scene.Name);

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
                if (so.HasPrivateAttachmentPoint)
                {
                    if (DebugLevel > 0)
                        m_log.DebugFormat(
                            "[ATTACHMENTS MODULE]: Killing private HUD {0} for avatars other than {1} at attachment point {2}",
                            so.Name, sp.Name, so.AttachmentPoint);

                    // As this scene object can now only be seen by the attaching avatar, tell everybody else in the
                    // scene that it's no longer in their awareness.
                    m_scene.ForEachClient(
                        client =>
                            { if (client.AgentId != so.AttachedAvatar)
                                client.SendKillObject(new List<uint>() { so.LocalId });
                            });
                }

                // Fudge below is an extremely unhelpful comment.  It's probably here so that the scheduled full update
                // will succeed, as that will not update if an attachment is selected.
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
            if (m_invAccessModule == null)
                return null;

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Called AddSceneObjectAsAttachment for object {0} {1} for {2}",
                    grp.Name, grp.LocalId, sp.Name);

            InventoryItemBase newItem
                = m_invAccessModule.CopyToInventory(
                    DeRezAction.TakeCopy,
                    m_scene.InventoryService.GetFolderForType(sp.UUID, AssetType.Object).ID,
                    new List<SceneObjectGroup> { grp },
                    sp.ControllingClient, true)[0];

            // sets itemID so client can show item as 'attached' in inventory
            grp.FromItemID = newItem.ID;

            return newItem;
        }

        /// <summary>
        /// Prepares the script instance for save.
        /// </summary>
        /// <remarks>
        /// This involves triggering the detach event and getting the script state (which also stops the script)
        /// This MUST be done outside sp.AttachmentsSyncLock, since otherwise there is a chance of deadlock if a 
        /// running script is performing attachment operations.
        /// </remarks>
        /// <returns>
        /// The script state ready for persistence.
        /// </returns>
        /// <param name='grp'>
        /// </param>
        /// <param name='fireDetachEvent'>
        /// If true, then fire the script event before we save its state.
        /// </param>
        private string PrepareScriptInstanceForSave(SceneObjectGroup grp, bool fireDetachEvent)
        {
            if (fireDetachEvent)
                m_scene.EventManager.TriggerOnAttach(grp.LocalId, grp.FromItemID, UUID.Zero);

            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    grp.SaveScriptedState(writer);
                }

                return sw.ToString();
            }
        }

        private void UpdateDetachedObject(IScenePresence sp, SceneObjectGroup so, string scriptedState)
        {
            // Don't save attachments for HG visitors, it
            // messes up their inventory. When a HG visitor logs
            // out on a foreign grid, their attachments will be
            // reloaded in the state they were in when they left
            // the home grid. This is best anyway as the visited
            // grid may use an incompatible script engine.
            bool saveChanged
                    = sp.PresenceType != PresenceType.Npc
                    && (m_scene.UserManagementModule == null
                    || m_scene.UserManagementModule.IsLocalGridUser(sp.UUID));

            // Remove the object from the scene so no more updates
            // are sent. Doing this before the below changes will ensure
            // updates can't cause "HUD artefacts"
            m_scene.DeleteSceneObject(so, false, false);

            // Prepare sog for storage
            so.AttachedAvatar = UUID.Zero;
            so.RootPart.SetParentLocalId(0);
            so.IsAttachment = false;

            if (saveChanged)
            {
                // We cannot use AbsolutePosition here because that would
                // attempt to cross the prim as it is detached
                so.ForEachPart(x => { x.GroupPosition = so.RootPart.AttachedPos; });

                UpdateKnownItem(sp, so, scriptedState);
            }

            // Now, remove the scripts
            so.RemoveScriptInstances(true);
        }

        protected SceneObjectGroup RezSingleAttachmentFromInventoryInternal(
            IScenePresence sp, UUID itemID, UUID assetID, uint attachmentPt, bool append)
        {
            if (m_invAccessModule == null)
                return null;

            SceneObjectGroup objatt;

            if (itemID != UUID.Zero)
                objatt = m_invAccessModule.RezObject(sp.ControllingClient,
                    itemID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                    false, false, sp.UUID, true);
            else
                objatt = m_invAccessModule.RezObject(sp.ControllingClient,
                    null, assetID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                    false, false, sp.UUID, true);

            if (objatt == null)
            {
                m_log.WarnFormat(
                    "[ATTACHMENTS MODULE]: Could not retrieve item {0} for attaching to avatar {1} at point {2}",
                    itemID, sp.Name, attachmentPt);

                return null;
            }
            else if (itemID == UUID.Zero)
            {
                // We need to have a FromItemID for multiple attachments on a single attach point to appear.  This is 
                // true on Singularity 1.8.5 and quite possibly other viewers as well.  As NPCs don't have an inventory
                // we will satisfy this requirement by inserting a random UUID.
                objatt.FromItemID = UUID.Random();
            }

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Rezzed single object {0} with {1} prims for attachment to {2} on point {3} in {4}",
                    objatt.Name, objatt.PrimCount, sp.Name, attachmentPt, m_scene.Name);

            // HasGroupChanged is being set from within RezObject.  Ideally it would be set by the caller.
            objatt.HasGroupChanged = false;
            bool tainted = false;
            if (attachmentPt != 0 && attachmentPt != objatt.AttachmentPoint)
                tainted = true;

            // FIXME: Detect whether it's really likely for AttachObject to throw an exception in the normal
            // course of events.  If not, then it's probably not worth trying to recover the situation
            // since this is more likely to trigger further exceptions and confuse later debugging.  If
            // exceptions can be thrown in expected error conditions (not NREs) then make this consistent
            // since other normal error conditions will simply return false instead.
            // This will throw if the attachment fails
            try
            {
                AttachObjectInternal(sp, objatt, attachmentPt, false, true, true, append);
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

            if (ThrottlePer100PrimsRezzed > 0)
            {
                int throttleMs = (int)Math.Round((float)objatt.PrimCount / 100 * ThrottlePer100PrimsRezzed);

                if (DebugLevel > 0)
                    m_log.DebugFormat(
                        "[ATTACHMENTS MODULE]: Throttling by {0}ms after rez of {1} with {2} prims for attachment to {3} on point {4} in {5}",
                        throttleMs, objatt.Name, objatt.PrimCount, sp.Name, attachmentPt, m_scene.Name);

                Thread.Sleep(throttleMs);
            }

            return objatt;
        }

        /// <summary>
        /// Update the user inventory to reflect an attachment
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="AttachmentPt"></param>
        /// <param name="itemID"></param>
        /// <param name="att"></param>
        private void ShowAttachInUserInventory(IScenePresence sp, uint AttachmentPt, UUID itemID, SceneObjectGroup att, bool append)
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
            if (item == null)
                return;

            int attFlag = append ? 0x80 : 0;
            bool changed = sp.Appearance.SetAttachment((int)AttachmentPt | attFlag, itemID, item.AssetID);
            if (changed && m_scene.AvatarFactory != null)
            {
                if (DebugLevel > 0)
                    m_log.DebugFormat(
                        "[ATTACHMENTS MODULE]: Queueing appearance save for {0}, attachment {1} point {2} in ShowAttachInUserInventory()",
                        sp.Name, att.Name, AttachmentPt);

                m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);
            }
        }

        #endregion

        #region Client Event Handlers

        private ISceneEntity Client_OnRezSingleAttachmentFromInv(IClientAPI remoteClient, UUID itemID, uint AttachmentPt)
        {
            if (!Enabled)
                return null;

            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Rezzing attachment to point {0} from item {1} for {2}",
                    (AttachmentPoint)AttachmentPt, itemID, remoteClient.Name);

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
            if (DebugLevel > 0)
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Attaching object local id {0} to {1} point {2} from ground (silent = {3})",
                    objectLocalID, remoteClient.Name, AttachmentPt, silent);

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

                bool append = (AttachmentPt & 0x80) != 0;
                AttachmentPt &= 0x7f;

                // Calls attach with a Zero position
                if (AttachObject(sp, part.ParentGroup, AttachmentPt, false, true, append))
                {
                    if (DebugLevel > 0)
                        m_log.Debug(
                            "[ATTACHMENTS MODULE]: Saving avatar attachment. AgentID: " + remoteClient.AgentId
                            + ", AttachmentPoint: " + AttachmentPt);

                    // Save avatar attachment information
                    m_scene.EventManager.TriggerOnAttach(objectLocalID, part.ParentGroup.FromItemID, remoteClient.AgentId);
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

            if (sp != null && group != null && group.FromItemID != UUID.Zero)
                DetachSingleAttachmentToInv(sp, group);
        }

        private void Client_OnDetachAttachmentIntoInv(UUID itemID, IClientAPI remoteClient)
        {
            if (!Enabled)
                return;

            ScenePresence sp = m_scene.GetScenePresence(remoteClient.AgentId);
            if (sp != null)
            {
                List<SceneObjectGroup> attachments = sp.GetAttachments();

                foreach (SceneObjectGroup group in attachments)
                {
                    if (group.FromItemID == itemID && group.FromItemID != UUID.Zero)
                    {
                        DetachSingleAttachmentToInv(sp, group);
                        return;
                    }
                }
            }
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
