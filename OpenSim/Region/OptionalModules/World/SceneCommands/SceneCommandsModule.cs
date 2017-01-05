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
                    + "appear-refresh  - if true  then appearance is resent to other avatars every 60 seconds.\n"
                    + "client-pos-upd  - the tolerance before clients are updated with new rotation information for an avatar.\n"
                    + "client-rot-upd  - the tolerance before clients are updated with new rotation information for an avatar.\n"
                    + "client-vel-upd  - the tolerance before clients are updated with new velocity information for an avatar.\n"
                    + "root-upd-per    - if greater than 1, terse updates are only sent to root agents other than the originator on every n updates.\n"
                    + "child-upd-per   - if greater than 1, terse updates are only sent to child agents on every n updates.\n"
                    + "collisions      - if false then collisions with other objects are turned off.\n"
                    + "pbackup         - if false then periodic scene backup is turned off.\n"
                    + "physics         - if false then all physics objects are non-physical.\n"
                    + "scripting       - if false then no scripting operations happen.\n"
                    + "teleport        - if true  then some extra teleport debug information is logged.\n"
                    + "update-on-timer - If true  then the scene is updated via a timer.  If false then a thread with sleep is used.\n"
                    + "updates         - if true  then any frame which exceeds double the maximum desired frame time is logged.",
                HandleDebugSceneGetCommand);

            scene.AddCommand(
                "Debug", this, "debug scene set",
                "debug scene set <param> <value>",
                "Turn on scene debugging options.",
                      "active          - if false then main scene update and maintenance loops are suspended.\n"
                    + "animations      - if true  then extra animations debug information is logged.\n"
                    + "appear-refresh  - if true  then appearance is resent to other avatars every 60 seconds.\n"
                    + "client-pos-upd  - the tolerance before clients are updated with new rotation information for an avatar.\n"
                    + "client-rot-upd  - the tolerance before clients are updated with new rotation information for an avatar.\n"
                    + "client-vel-upd  - the tolerance before clients are updated with new velocity information for an avatar.\n"
                    + "root-upd-per    - if greater than 1, terse updates are only sent to root agents other than the originator on every n updates.\n"
                    + "child-upd-per   - if greater than 1, terse updates are only sent to child agents on every n updates.\n"
                    + "collisions      - if false then collisions with other objects are turned off.\n"
                    + "pbackup         - if false then periodic scene backup is turned off.\n"
                    + "physics         - if false then all physics objects are non-physical.\n"
                    + "scripting       - if false then no scripting operations happen.\n"
                    + "teleport        - if true  then some extra teleport debug information is logged.\n"
                    + "update-on-timer - If true  then the scene is updated via a timer.  If false then a thread with sleep is used.\n"
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
            cdl.AddRow("appear-refresh", m_scene.SendPeriodicAppearanceUpdates);
            cdl.AddRow("client-pos-upd", m_scene.RootPositionUpdateTolerance);
            cdl.AddRow("client-rot-upd", m_scene.RootRotationUpdateTolerance);
            cdl.AddRow("client-vel-upd", m_scene.RootVelocityUpdateTolerance);
            cdl.AddRow("root-upd-per", m_scene.RootTerseUpdatePeriod);
            cdl.AddRow("child-upd-per", m_scene.ChildTerseUpdatePeriod);
            cdl.AddRow("pbackup", m_scene.PeriodicBackup);
            cdl.AddRow("physics", m_scene.PhysicsEnabled);
            cdl.AddRow("scripting", m_scene.ScriptsEnabled);
            cdl.AddRow("teleport", m_scene.DebugTeleporting);
//            cdl.AddRow("update-on-timer", m_scene.UpdateOnTimer);
            cdl.AddRow("updates", m_scene.DebugUpdates);

            MainConsole.Instance.OutputFormat("Scene {0} options:", m_scene.Name);
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

                MainConsole.Instance.OutputFormat("Set {0} debug scene {1} = {2}", m_scene.Name, key, value);
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

            if (options.ContainsKey("appear-refresh"))
            {
                bool newValue;

                // FIXME: This can only come from the console at the moment but might not always be true.
                if (ConsoleUtil.TryParseConsoleBool(MainConsole.Instance, options["appear-refresh"], out newValue))
                    m_scene.SendPeriodicAppearanceUpdates = newValue;
            }

            if (options.ContainsKey("client-pos-upd"))
            {
                float newValue;

                // FIXME: This can only come from the console at the moment but might not always be true.
                if (ConsoleUtil.TryParseConsoleFloat(MainConsole.Instance, options["client-pos-upd"], out newValue))
                    m_scene.RootPositionUpdateTolerance = newValue;
            }

            if (options.ContainsKey("client-rot-upd"))
            {
                float newValue;

                // FIXME: This can only come from the console at the moment but might not always be true.
                if (ConsoleUtil.TryParseConsoleFloat(MainConsole.Instance, options["client-rot-upd"], out newValue))
                    m_scene.RootRotationUpdateTolerance = newValue;
            }

            if (options.ContainsKey("client-vel-upd"))
            {
                float newValue;

                // FIXME: This can only come from the console at the moment but might not always be true.
                if (ConsoleUtil.TryParseConsoleFloat(MainConsole.Instance, options["client-vel-upd"], out newValue))
                    m_scene.RootVelocityUpdateTolerance = newValue;
            }

            if (options.ContainsKey("root-upd-per"))
            {
                int newValue;

                // FIXME: This can only come from the console at the moment but might not always be true.
                if (ConsoleUtil.TryParseConsoleNaturalInt(MainConsole.Instance, options["root-upd-per"], out newValue))
                    m_scene.RootTerseUpdatePeriod = newValue;
            }

            if (options.ContainsKey("child-upd-per"))
            {
                int newValue;

                // FIXME: This can only come from the console at the moment but might not always be true.
                if (ConsoleUtil.TryParseConsoleNaturalInt(MainConsole.Instance, options["child-upd-per"], out newValue))
                    m_scene.ChildTerseUpdatePeriod = newValue;
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

            if (options.ContainsKey("update-on-timer"))
            {
                bool enableUpdateOnTimer;
                if (bool.TryParse(options["update-on-timer"], out enableUpdateOnTimer))
                {
//                    m_scene.UpdateOnTimer = enableUpdateOnTimer;
                    m_scene.Active = false;

                    while (m_scene.IsRunning)
                        Thread.Sleep(20);

                    m_scene.Active = true;
                }
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
    }
}
