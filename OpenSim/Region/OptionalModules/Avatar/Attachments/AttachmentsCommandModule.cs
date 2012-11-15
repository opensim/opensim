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
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Avatar.Attachments
{
    /// <summary>
    /// A module that just holds commands for inspecting avatar appearance.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AttachmentsCommandModule")]
    public class AttachmentsCommandModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_scenes = new List<Scene>();
//        private IAvatarFactoryModule m_avatarFactory;

        public string Name { get { return "Attachments Command Module"; } }
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Remove(scene);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Add(scene);

            scene.AddCommand(
                "Users", this, "attachments show",
                "attachments show [<first-name> <last-name>]",
                "Show attachment information for avatars in this simulator.",
                HandleShowAttachmentsCommand);
        }

        protected void HandleShowAttachmentsCommand(string module, string[] cmd)
        {
            if (cmd.Length != 2 && cmd.Length < 4)
            {
                MainConsole.Instance.OutputFormat("Usage: attachments show [<first-name> <last-name>]");
                return;
            }

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

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes)
                {
                    if (targetNameSupplied)
                    {
                        ScenePresence sp = scene.GetScenePresence(optionalTargetFirstName, optionalTargetLastName);
                        if (sp != null && !sp.IsChildAgent)
                            GetAttachmentsReport(sp, sb);
                    }
                    else
                    {
                        scene.ForEachRootScenePresence(sp => GetAttachmentsReport(sp, sb));
                    }
                }
            }

            MainConsole.Instance.Output(sb.ToString());
        }

        private void GetAttachmentsReport(ScenePresence sp, StringBuilder sb)
        {
            sb.AppendFormat("Attachments for {0}\n", sp.Name);

            ConsoleDisplayTable ct = new ConsoleDisplayTable() { Indent = 2 };
            ct.Columns.Add(new ConsoleDisplayTableColumn("Attachment Name", 50));
            ct.Columns.Add(new ConsoleDisplayTableColumn("Local ID", 10));
            ct.Columns.Add(new ConsoleDisplayTableColumn("Item ID", 36));
            ct.Columns.Add(new ConsoleDisplayTableColumn("Attach Point", 14));
            ct.Columns.Add(new ConsoleDisplayTableColumn("Position", 15));

//            sb.AppendFormat(
//                "  {0,-36}  {1,-10}  {2,-36}  {3,-14}  {4,-15}\n",
//                "Attachment Name", "Local ID", "Item ID", "Attach Point", "Position");

            List<SceneObjectGroup> attachmentObjects = sp.GetAttachments();
            foreach (SceneObjectGroup attachmentObject in attachmentObjects)
            {
//                InventoryItemBase attachmentItem
//                    = m_scenes[0].InventoryService.GetItem(new InventoryItemBase(attachmentObject.FromItemID));

//                if (attachmentItem == null)
//                {
//                    sb.AppendFormat(
//                        "WARNING: Couldn't find attachment for item {0} at point {1}\n",
//                        attachmentData.ItemID, (AttachmentPoint)attachmentData.AttachPoint);
//                        continue;
//                }
//                else
//                {
//                    sb.AppendFormat(
//                        "  {0,-36}  {1,-10}  {2,-36}  {3,-14}  {4,-15}\n",
//                        attachmentObject.Name, attachmentObject.LocalId, attachmentObject.FromItemID,
//                        (AttachmentPoint)attachmentObject.AttachmentPoint, attachmentObject.RootPart.AttachedPos);
                    ct.Rows.Add(
                        new ConsoleDisplayTableRow(
                            new List<string>()
                            {
                                attachmentObject.Name,
                                attachmentObject.LocalId.ToString(),
                                attachmentObject.FromItemID.ToString(),
                                ((AttachmentPoint)attachmentObject.AttachmentPoint).ToString(),
                                attachmentObject.RootPart.AttachedPos.ToString()
                            }));
//                }
            }

            ct.AddToStringBuilder(sb);
            sb.Append("\n");
        }
    }
}