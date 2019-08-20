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
using System.Text;
using System.Threading;
using System.Xml;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.CoreModules.Avatar.Attachments
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AttachmentsModule")]
    public class AttachmentsModule : IAttachmentsModule, INonSharedRegionModule
    {
        #region INonSharedRegionModule
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public int DebugLevel { get; set; }

        private Scene m_scene;
        private IRegionConsole m_regionConsole;
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
                m_scene.EventManager.OnStartScript += (localID, itemID) => OnScriptStateChange(localID, true);
                m_scene.EventManager.OnStopScript += (localID, itemID) => OnScriptStateChange(localID, false);

            }

            // TODO: Should probably be subscribing to CloseClient too, but this doesn't yet give us IClientAPI
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.UnregisterModuleInterface<IAttachmentsModule>(this);

            if (Enabled)
                m_scene.EventManager.OnNewClient -= SubscribeToClientEvents;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!Enabled)
                return;

            m_invAccessModule = m_scene.RequestModuleInterface<IInventoryAccessModule>();
            m_regionConsole = scene.RequestModuleInterface<IRegionConsole>();
            if (m_regionConsole != null)
            {
                m_regionConsole.AddCommand("AttachModule", false, "set auto_grant_attach_perms", "set auto_grant_attach_perms true|false", "Allow objects owned by the region owner or estate managers to obtain attach permissions without asking the user", HandleSetAutoGrantAttachPerms);
            }

            scene.AddCommand(
                "Debug",
                this,
                "debug attachments log",
                "debug attachments log [0|1]",
                "Turn on attachments debug logging",
                "  <= 0 - turns off debug logging\n"
                    + "  >= 1 - turns on attachment message debug logging",
                HandleDebugAttachmentsLog);

            scene.AddCommand(
                "Debug",
                this,
                "debug attachments status",
                "debug attachments status",
                "Show current attachments debug status",
                HandleDebugAttachmentsStatus);

            // next should work on console root also
            MainConsole.Instance.Commands.AddCommand(
                "Users", true, "attachments show",
                "attachments show [<first-name> <last-name>]",
                "Show attachment information for avatars in this simulator.",
                "If no name is supplied then information for all avatars is shown.",
                HandleShowAttachmentsCommand);
        }

        public void Close()
        {
            if (!Enabled)
                return;
            RemoveRegion(m_scene);
        }

        private void HandleDebugAttachmentsLog(string module, string[] args)
        {
            int debugLevel;

            if (!(args.Length == 4 && int.TryParse(args[3], out debugLevel)))
            {
                MainConsole.Instance.Output("Usage: debug attachments log [0|1]");
            }
            else
            {
                DebugLevel = debugLevel;
                MainConsole.Instance.Output(
                    "Set attachments debug level to {0} in {1}", null, DebugLevel, m_scene.Name);
            }
        }

        private void HandleDebugAttachmentsStatus(string module, string[] args)
        {
            MainConsole.Instance.Output("Settings for {0}", null, m_scene.Name);
            MainConsole.Instance.Output("Debug logging level: {0}", null, DebugLevel);
        }

        protected void HandleShowAttachmentsCommand(string module, string[] cmd)
        {
            if (cmd.Length != 2 && cmd.Length < 4)
            {
                MainConsole.Instance.Output("Usage: attachments show [<first-name> <last-name>]");
                return;
            }

            SceneManager sm = SceneManager.Instance;
            if(sm == null || sm.Scenes.Count == 0)
                return;

            bool targetNameSupplied = false;
            string optionalTargetFirstName = null;
            string optionalTargetLastName = null;

            if (cmd.Length >= 4)
            {
                targetNameSupplied = true;
                optionalTargetFirstName = cmd[2];
                optionalTargetLastName = cmd[3];
            }

            StringBuilder sb = new StringBuilder();
            sm.ForEachSelectedScene(
                scene =>
                {
                    if (targetNameSupplied)
                    {
                        ScenePresence sp = scene.GetScenePresence(optionalTargetFirstName, optionalTargetLastName);
                        if (sp != null && !sp.IsChildAgent)
                            GetAttachmentsReport(sp, sb);
                    }
                    else
                    {
                        sb.AppendFormat("--- All attachments for region {0}:\n", scene.Name);
                        scene.ForEachRootScenePresence(sp => GetAttachmentsReport(sp, sb));
                    }
                });

            MainConsole.Instance.Output(sb.ToString());
        }

        private void GetAttachmentsReport(ScenePresence sp, StringBuilder sb)
        {
            sb.AppendFormat("Attachments for {0}\n\n", sp.Name);

            ConsoleDisplayList ct = new ConsoleDisplayList();

            List<SceneObjectGroup> attachmentObjects = sp.GetAttachments();
            foreach (SceneObjectGroup attachmentObject in attachmentObjects)
            {
                ct.Indent = 2;
                ct.AddRow("Attachment Name", attachmentObject.Name);
                ct.AddRow("Local ID", attachmentObject.LocalId);
                ct.AddRow("Item ID", attachmentObject.UUID);
                ct.AddRow("From Item ID", attachmentObject.FromItemID);
                ct.AddRow("Attach Point", ((AttachmentPoint)attachmentObject.AttachmentPoint));
                ct.AddRow("Position", attachmentObject.RootPart.AttachedPos + "\n\n");
            }

            ct.AddToStringBuilder(sb);
        }

        private void SendConsoleOutput(UUID agentID, string text)
        {
            if (m_regionConsole == null)
                return;

            m_regionConsole.SendConsoleOutput(agentID, text);
        }

        private void HandleSetAutoGrantAttachPerms(string module, string[] parms)
        {
            UUID agentID = new UUID(parms[parms.Length - 1]);
            Array.Resize(ref parms, parms.Length - 1);

            if (parms.Length != 3)
            {
                SendConsoleOutput(agentID, "Command parameter error");
                return;
            }

            string val = parms[2];
            if (val != "true" && val != "false")
            {
                SendConsoleOutput(agentID, "Command parameter error");
                return;
            }

            m_scene.StoreExtraSetting("auto_grant_attach_perms", val);

            SendConsoleOutput(agentID, String.Format("auto_grant_attach_perms set to {0}", val));
        }

        /// <summary>
        /// Listen for client triggered running state changes so that we can persist the script's object if necessary.
        /// </summary>
        /// <param name='localID'></param>
        /// <param name='itemID'></param>
        private void OnScriptStateChange(uint localID, bool started)
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

            XmlDocument doc = new XmlDocument();
            string stateData = String.Empty;

            IAttachmentsService attServ = m_scene.RequestModuleInterface<IAttachmentsService>();
            if (attServ != null)
            {
                m_log.DebugFormat("[ATTACHMENT]: Loading attachment data from attachment service");
                stateData = attServ.Get(sp.UUID.ToString());
                if (stateData != String.Empty)
                {
                    try
                    {
                        doc.LoadXml(stateData);
                    }
                    catch { }
                }
            }

            Dictionary<UUID, string> itemData = new Dictionary<UUID, string>();

            XmlNodeList nodes = doc.GetElementsByTagName("Attachment");
            if (nodes.Count > 0)
            {
                foreach (XmlNode n in nodes)
                {
                    XmlElement elem = (XmlElement)n;
                    string itemID = elem.GetAttribute("ItemID");
                    string xml = elem.InnerXml;

                    itemData[new UUID(itemID)] = xml;
                }
            }

            List<AvatarAttachment> attachments = sp.Appearance.GetAttachments();

            // Let's get all items at once, so they get cached
            UUID[] items = new UUID[attachments.Count];
            int i = 0;
            foreach (AvatarAttachment attach in attachments)
                items[i++] = attach.ItemID;
            m_scene.InventoryService.GetMultipleItems(sp.UUID, items);

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
                    string xmlData;
                    XmlDocument d = null;

                    if (itemData.TryGetValue(attach.ItemID, out xmlData))
                    {
                        d = new XmlDocument();
                        d.LoadXml(xmlData);
                        m_log.InfoFormat("[ATTACHMENT]: Found saved state for item {0}, loading it", attach.ItemID);
                    }

                    // If we're an NPC then skip all the item checks and manipulations since we don't have an
                    // inventory right now.
                    RezSingleAttachmentFromInventoryInternal(
                        sp, sp.PresenceType == PresenceType.Npc ? UUID.Zero : attach.ItemID, attach.AssetID, attachmentPt, true, d);
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

            if (sp.PresenceType != PresenceType.Npc)
            {
                foreach (SceneObjectGroup so in attachments)
                {
                    // Scripts MUST be snapshotted before the object is
                    // removed from the scene because doing otherwise will
                    // clobber the run flag
                    // This must be done outside the sp.AttachmentSyncLock so that there is no risk of a deadlock from
                    // scripts performing attachment operations at the same time.  Getting object states stops the scripts.
                    scriptStates[so] = PrepareScriptInstanceForSave(so, false);
                }

                lock (sp.AttachmentsSyncLock)
                {
                    foreach (SceneObjectGroup so in attachments)
                        UpdateDetachedObject(sp, so, scriptStates[so]);
                    sp.ClearAttachments();
                }
            }
            else
            {
                lock (sp.AttachmentsSyncLock)
                {
                    foreach (SceneObjectGroup so in attachments)
                        UpdateDetachedObject(sp, so, String.Empty);
                    sp.ClearAttachments();
                }
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

        public bool AttachObject(IScenePresence sp, SceneObjectGroup group, uint attachmentPt, bool silent,
                    bool addToInventory, bool append)
        {
            if (!Enabled)
                return false;

            return AttachObjectInternal(sp, group, attachmentPt, silent, addToInventory, false, append);
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
        private bool AttachObjectInternal(IScenePresence sp, SceneObjectGroup group, uint attachmentPt,
                bool silent, bool addToInventory, bool resumeScripts, bool append)
        {
//                m_log.DebugFormat(
//                    "[ATTACHMENTS MODULE]: Attaching object {0} {1} to {2} point {3} from ground (silent = {4})",
//                    group.Name, group.LocalId, sp.Name, attachmentPt, silent);


            if (group.GetSittingAvatarsCount() != 0)
            {
                if (DebugLevel > 0)
                    m_log.WarnFormat(
                        "[ATTACHMENTS MODULE]: Ignoring request to attach {0} {1} to {2} on {3} since {4} avatars are still sitting on it",
                        group.Name, group.LocalId, sp.Name, attachmentPt, group.GetSittingAvatarsCount());

                return false;
            }

            Vector3 attachPos = group.AbsolutePosition;

            // TODO: this short circuits multiple attachments functionality  in  LL viewer 2.1+ and should
            // be removed when that functionality is implemented in opensim
            attachmentPt &= 0x7f;

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
            }

            // if we still didn't find a suitable attachment point.......
            if (attachmentPt == 0)
            {
                // Stick it on left hand with Zero Offset from the attachment point.
                attachmentPt = (uint)AttachmentPoint.LeftHand;
                attachPos = Vector3.Zero;
            }

            List<SceneObjectGroup> attachments = sp.GetAttachments(attachmentPt);
            if (attachments.Contains(group))
            {
//                if (DebugLevel > 0)
//                    m_log.WarnFormat(
//                        "[ATTACHMENTS MODULE]: Ignoring request to attach {0} {1} to {2} on {3} since it's already attached",
//                        group.Name, group.LocalId, sp.Name, attachmentPt);

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

            group.DetachFromBackup();

            lock (sp.AttachmentsSyncLock)
            {
                group.AttachmentPoint = attachmentPt;
                group.RootPart.AttachedPos = attachPos;

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

                else
                // Do this last so that event listeners have access to all the effects of the attachment
                // this can't be done when creating scripts:
                // scripts do internal enqueue of attach event
                // and not all scripts are loaded at this point
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

        public ISceneEntity RezSingleAttachmentFromInventory(IScenePresence sp, UUID itemID, uint AttachmentPt)
        {
            return RezSingleAttachmentFromInventory(sp, itemID, AttachmentPt, null);
        }

        public ISceneEntity RezSingleAttachmentFromInventory(IScenePresence sp, UUID itemID, uint AttachmentPt, XmlDocument doc)
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

            return RezSingleAttachmentFromInventoryInternal(sp, itemID, UUID.Zero, AttachmentPt, append, doc);
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
            Vector3 pos = new Vector3(2.5f, 0f, 0f);
            pos *= ((ScenePresence)sp).Rotation;
            pos += sp.AbsolutePosition;
            DetachSingleAttachmentToGround(sp, soLocalId, pos, Quaternion.Identity);
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

                so.RootPart.Shape.LastAttachPoint = (byte)so.AttachmentPoint;

                sp.RemoveAttachment(so);
                so.FromItemID = UUID.Zero;

                so.AttachedAvatar = UUID.Zero;
                so.ClearPartAttachmentData();

                SceneObjectPart rootPart = so.RootPart;

                rootPart.SetParentLocalId(0);
                so.AbsolutePosition = absolutePos;
                if (absoluteRot != Quaternion.Identity)
                {
                    so.UpdateGroupRotationR(absoluteRot);
                }

                rootPart.RemFlag(PrimFlags.TemporaryOnRez);

                so.ApplyPhysics();

                rootPart.Rezzed = DateTime.Now;
                so.AttachToBackup();
                m_scene.EventManager.TriggerParcelPrimCountTainted();

                rootPart.ClearUndoState();

                List<UUID> uuids = new List<UUID>();
                uuids.Add(inventoryID);
                m_scene.InventoryService.DeleteItems(sp.UUID, uuids);
                sp.ControllingClient.SendRemoveInventoryItem(inventoryID);
            }

            m_scene.EventManager.TriggerOnAttach(so.LocalId, so.UUID, UUID.Zero);

            // Attach (NULL) stops scripts. We don't want that. Resume them.
            so.ResumeScripts();
            so.HasGroupChanged = true;
            so.RootPart.ScheduleFullUpdate();
            so.ScheduleGroupForTerseUpdate();
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

            // If this didn't come from inventory, it also shouldn't go there
            // on detach. It's likely a temp attachment.
            if (so.FromItemID == UUID.Zero)
            {
                // Retirn value is ignored
                PrepareScriptInstanceForSave(so, true);

                lock (sp.AttachmentsSyncLock)
                {
                    bool changed = sp.Appearance.DetachAttachment(so.FromItemID);
                    if (changed && m_scene.AvatarFactory != null)
                        m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);

                    sp.RemoveAttachment(so);
                }

                m_scene.DeleteSceneObject(so, false, false);
                so.RemoveScriptInstances(true);
                so.Clear();

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

            if(sp.IsNPC)
                return;

            if (grp.HasGroupChanged)
            {
                m_log.DebugFormat(
                    "[ATTACHMENTS MODULE]: Updating asset for attachment {0}, attachpoint {1}",
                    grp.UUID, grp.AttachmentPoint);

                string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(grp, scriptedState);

                InventoryItemBase item = m_scene.InventoryService.GetItem(sp.UUID, grp.FromItemID);

                if (item != null)
                {
                    // attach is rez, need to update permissions
                    item.Flags &= ~(uint)(InventoryItemFlags.ObjectSlamPerm | InventoryItemFlags.ObjectOverwriteBase |
                            InventoryItemFlags.ObjectOverwriteOwner | InventoryItemFlags.ObjectOverwriteGroup |
                            InventoryItemFlags.ObjectOverwriteEveryone | InventoryItemFlags.ObjectOverwriteNextOwner);

                    uint permsBase = (uint)(PermissionMask.Copy | PermissionMask.Transfer |
                                 PermissionMask.Modify | PermissionMask.Move |
                                 PermissionMask.Export | PermissionMask.FoldedMask);
                    
                    permsBase &= grp.CurrentAndFoldedNextPermissions();
                    permsBase |= (uint)PermissionMask.Move;
                    item.BasePermissions = permsBase;
                    item.CurrentPermissions = permsBase;
                    item.NextPermissions = permsBase & grp.RootPart.NextOwnerMask | (uint)PermissionMask.Move;
                    item.EveryOnePermissions = permsBase & grp.RootPart.EveryoneMask;
                    item.GroupPermissions = permsBase & grp.RootPart.GroupMask;
                    item.CurrentPermissions &=
                        ((uint)PermissionMask.Copy |
                         (uint)PermissionMask.Transfer |
                         (uint)PermissionMask.Modify |
                         (uint)PermissionMask.Move |
                         (uint)PermissionMask.Export |
                         (uint)PermissionMask.FoldedMask); // Preserve folded permissions ??

                    string name = grp.RootPart.Name;
                    string desc = grp.RootPart.Description;

                    AssetBase asset = m_scene.CreateAsset(
                        name, desc,
                        (sbyte)AssetType.Object,
                        Utils.StringToBytes(sceneObjectXml),
                        sp.UUID);

                    item.Name = name;
                    item.Description = desc;
                    item.AssetID = asset.FullID;
                    item.AssetType = (int)AssetType.Object;
                    item.InvType = (int)InventoryType.Object;

                    if (m_invAccessModule != null)
                        m_invAccessModule.UpdateInventoryItemAsset(sp.UUID, item, asset);

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


            foreach (SceneObjectPart part in so.Parts)
            {
//                if (part.KeyframeMotion != null)
//                    part.KeyframeMotion.Suspend();

                if (part.PhysActor != null)
                {
                    part.RemoveFromPhysics();
                }
            }

            so.RootPart.SetParentLocalId(sp.LocalId);
            so.AttachedAvatar = sp.UUID;
            so.AttachmentPoint = attachmentpoint;
            so.RootPart.AttachedPos = attachOffset;
            so.AbsolutePosition = attachOffset;
            so.IsAttachment = true;

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

                so.ScheduleGroupForFullAnimUpdate();
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
                    m_scene.InventoryService.GetFolderForType(sp.UUID, FolderType.Object).ID,
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
            {
                m_scene.EventManager.TriggerOnAttach(grp.LocalId, grp.FromItemID, UUID.Zero);
                // Allow detach event time to do some work before stopping the script
                Thread.Sleep(2);
            }

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
            so.Clear();
        }

        protected SceneObjectGroup RezSingleAttachmentFromInventoryInternal(
            IScenePresence sp, UUID itemID, UUID assetID, uint attachmentPt, bool append, XmlDocument doc)
        {
            if (m_invAccessModule == null)
                return null;

            SceneObjectGroup objatt;

            UUID rezGroupID;

            // This will fail if the user aborts login. sp will exist
            // but ControllintClient will be null.
            try
            {
                rezGroupID = sp.ControllingClient.ActiveGroupId;
            }
            catch
            {
                return null;
            }

            if (itemID != UUID.Zero)
                objatt = m_invAccessModule.RezObject(sp.ControllingClient,
                    itemID, rezGroupID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
                    false, false, sp.UUID, true);
            else
                objatt = m_invAccessModule.RezObject(sp.ControllingClient,
                    null, rezGroupID, assetID, Vector3.Zero, Vector3.Zero, UUID.Zero, (byte)1, true,
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
                if (doc != null)
                {
                    objatt.LoadScriptState(doc);
                    objatt.ResetOwnerChangeFlag();
                }

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

            InventoryItemBase item = m_scene.InventoryService.GetItem(sp.UUID, itemID);
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

                SceneObjectGroup group = part.ParentGroup;

                if (!m_scene.Permissions.CanTakeObject(group, sp))
                {
                    remoteClient.SendAgentAlertMessage(
                        "You don't have sufficient permissions to attach this object", false);

                    return;
                }

                bool append = (AttachmentPt & 0x80) != 0;
                AttachmentPt &= 0x7f;

                // Calls attach with a Zero position
                if (AttachObject(sp, group , AttachmentPt, false, true, append))
                {
                    if (DebugLevel > 0)
                        m_log.Debug(
                            "[ATTACHMENTS MODULE]: Saving avatar attachment. AgentID: " + remoteClient.AgentId
                            + ", AttachmentPoint: " + AttachmentPt);

                    // Save avatar attachment information
                    m_scene.AvatarFactory.QueueAppearanceSave(sp.UUID);
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
