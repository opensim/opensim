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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Avatar.Attachments
{
    /// <summary>
    /// A module that just holds commands for inspecting avatar appearance.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SceneCommandsModule")]
    public class SceneCommandsModule : ISceneCommandsModule, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
//        private IAvatarFactoryModule m_avatarFactory;

        public string Name { get { return "Scene Commands Module"; } }
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[SCENE COMMANDS MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[SCENE COMMANDS MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[SCENE COMMANDS MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[SCENE COMMANDS MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);

            m_scene = scene;
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[SCENE COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);

            scene.AddCommand(
                "Debug", this, "debug scene set",
                "debug scene set active|collisions|pbackup|physics|scripting|teleport|updates true|false",
                "Turn on scene debugging options.",
                "If active     is false then main scene update and maintenance loops are suspended.\n"
                    + "If collisions is false then collisions with other objects are turned off.\n"
                    + "If pbackup    is false then periodic scene backup is turned off.\n"
                    + "If physics    is false then all physics objects are non-physical.\n"
                    + "If scripting  is false then no scripting operations happen.\n"
                    + "If teleport   is true  then some extra teleport debug information is logged.\n"
                    + "If updates    is true  then any frame which exceeds double the maximum desired frame time is logged.",
                HandleDebugSceneCommand);
        }

        private void HandleDebugSceneCommand(string module, string[] args)
        {
            if (args.Length == 5)
            {
                if (MainConsole.Instance.ConsoleScene == null)
                {
                    MainConsole.Instance.Output("Please use 'change region <regioname>' first");
                }
                else
                {
                    string key = args[3];
                    string value = args[4];
                    SetSceneDebugOptions(new Dictionary<string, string>() { { key, value } });

                    MainConsole.Instance.OutputFormat("Set debug scene {0} = {1}", key, value);
                }
            }
            else
            {
                MainConsole.Instance.Output(
                    "Usage: debug scene set active|collisions|pbackup|physics|scripting|teleport|updates true|false");
            }
        }

        public void SetSceneDebugOptions(Dictionary<string, string> options)
        {
            if (options.ContainsKey("active"))
            {
                bool active;

                if (bool.TryParse(options["active"], out active))
                    m_scene.Active = active;
            }

            if (options.ContainsKey("pbackup"))
            {
                bool active;

                if (bool.TryParse(options["pbackup"], out active))
                    m_scene.PeriodicBackup = active;
            }

            if (options.ContainsKey("scripting"))
            {
                bool enableScripts = true;
                if (bool.TryParse(options["scripting"], out enableScripts))
                    m_scene.ScriptsEnabled = enableScripts;
            }

            if (options.ContainsKey("physics"))
            {
                bool enablePhysics;
                if (bool.TryParse(options["physics"], out enablePhysics))
                    m_scene.PhysicsEnabled = enablePhysics;
            }

//            if (options.ContainsKey("collisions"))
//            {
//                // TODO: Implement.  If false, should stop objects colliding, though possibly should still allow
//                // the avatar themselves to collide with the ground.
//            }

            if (options.ContainsKey("teleport"))
            {
                bool enableTeleportDebugging;
                if (bool.TryParse(options["teleport"], out enableTeleportDebugging))
                    m_scene.DebugTeleporting = enableTeleportDebugging;
            }

            if (options.ContainsKey("updates"))
            {
                bool enableUpdateDebugging;
                if (bool.TryParse(options["updates"], out enableUpdateDebugging))
                {
                    m_scene.DebugUpdates = enableUpdateDebugging;
                    GcNotify.Enabled = enableUpdateDebugging;
                }
            }
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