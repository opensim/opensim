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
using System.Threading;
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
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

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

            m_scene.RegisterModuleInterface<ISceneCommandsModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[SCENE COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);

            scene.AddCommand(
                "Debug", this, "debug scene get",
                "debug scene get",
                "List current scene options.",
                      "active          - if false then main scene update and maintenance loops are suspended.\n"
                    + "animations      - if true  then extra animations debug information is logged.\n"
                    + "collisions      - if false then collisions with other objects are turned off.\n"
                    + "pbackup         - if false then periodic scene backup is turned off.\n"
                    + "physics         - if false then all physics objects are non-physical.\n"
                    + "scripting       - if false then no scripting operations happen.\n"
                    + "teleport        - if true  then some extra teleport debug information is logged.\n"
                    + "updates         - if true  then any frame which exceeds double the maximum desired frame time is logged.",
                HandleDebugSceneGetCommand);

            scene.AddCommand(
                "Debug", this, "debug scene set",
                "debug scene set <param> <value>",
                "Turn on scene debugging options.",
                      "active          - if false then main scene update and maintenance loops are suspended.\n"
                    + "animations      - if true  then extra animations debug information is logged.\n"
                    + "collisions      - if false then collisions with other objects are turned off.\n"
                    + "pbackup         - if false then periodic scene backup is turned off.\n"
                    + "physics         - if false then all physics objects are non-physical.\n"
                    + "scripting       - if false then no scripting operations happen.\n"
                    + "teleport        - if true  then some extra teleport debug information is logged.\n"
                    + "updates         - if true  then any frame which exceeds double the maximum desired frame time is logged.",
                HandleDebugSceneSetCommand);
        }

        private void HandleDebugSceneGetCommand(string module, string[] args)
        {
            if (args.Length == 3)
            {
                if (MainConsole.Instance.ConsoleScene != m_scene && MainConsole.Instance.ConsoleScene != null)
                    return;

                OutputSceneDebugOptions();
            }
            else
            {
                MainConsole.Instance.Output("Usage: debug scene get");
            }
        }

        private void OutputSceneDebugOptions()
        {
            ConsoleDisplayList cdl = new ConsoleDisplayList();
            cdl.AddRow("active", m_scene.Active);
            cdl.AddRow("animations", m_scene.DebugAnimations);
            cdl.AddRow("pbackup", m_scene.PeriodicBackup);
            cdl.AddRow("physics", m_scene.PhysicsEnabled);
            cdl.AddRow("scripting", m_scene.ScriptsEnabled);
            cdl.AddRow("teleport", m_scene.DebugTeleporting);
            cdl.AddRow("updates", m_scene.DebugUpdates);

            MainConsole.Instance.Output("Scene {0} options:", null, m_scene.Name);
            MainConsole.Instance.Output(cdl.ToString());
        }

        private void HandleDebugSceneSetCommand(string module, string[] args)
        {
            if (args.Length == 5)
            {
                if (MainConsole.Instance.ConsoleScene != m_scene && MainConsole.Instance.ConsoleScene != null)
                    return;

                string key = args[3];
                string value = args[4];
                SetSceneDebugOptions(new Dictionary<string, string>() { { key, value } });

                MainConsole.Instance.Output("Set {0} debug scene {1} = {2}", null, m_scene.Name, key, value);
            }
            else
            {
                MainConsole.Instance.Output("Usage: debug scene set <param> <value>");
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

            if (options.ContainsKey("animations"))
            {
                bool active;

                if (bool.TryParse(options["animations"], out active))
                    m_scene.DebugAnimations = active;
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
                }
            }
        }
    }
}
