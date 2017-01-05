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
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using Mono.Addins;
using NDesk.Options;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatars.Commands
{
    /// <summary>
    /// A module that holds commands for manipulating objects in the scene.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "UserCommandsModule")]
    public class UserCommandsModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const string TeleportUserCommandSyntax = "teleport user <first-name> <last-name> <destination>";

        public static Regex InterRegionDestinationRegex
            = new Regex(@"^(?<regionName>.+)/(?<x>\d+)/(?<y>\d+)/(?<z>\d+)$", RegexOptions.Compiled);

        public static Regex WithinRegionDestinationRegex
            = new Regex(@"^(?<x>\d+)/(?<y>\d+)/(?<z>\d+)$", RegexOptions.Compiled);

        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();

        public string Name { get { return "User Commands Module"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[USER COMMANDS MODULE]: INITIALIZED MODULE");
        }

        public void PostInitialise()
        {
//            m_log.DebugFormat("[USER COMMANDS MODULE]: POST INITIALIZED MODULE");
        }

        public void Close()
        {
//            m_log.DebugFormat("[USER COMMANDS MODULE]: CLOSED MODULE");
        }

        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[USER COMMANDS MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);

            lock (m_scenes)
                m_scenes[scene.RegionInfo.RegionID] = scene;

            scene.AddCommand(
                "Users",
                this,
                "teleport user",
                TeleportUserCommandSyntax,
                "Teleport a user in this simulator to the given destination",
                "<destination> is in format [<region-name>]/<x>/<y>/<z>, e.g. regionone/20/30/40 or just 20/30/40 to teleport within same region."
                    + "\nIf the region contains a space then the whole destination must be in quotes, e.g. \"region one/20/30/40\"",
                HandleTeleportUser);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[USER COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);

            lock (m_scenes)
                m_scenes.Remove(scene.RegionInfo.RegionID);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[USER COMMANDS MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }

        private ScenePresence GetUser(string firstName, string lastName)
        {
            ScenePresence userFound = null;

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    ScenePresence user = scene.GetScenePresence(firstName, lastName);
                    if (user != null && !user.IsChildAgent)
                    {
                        userFound = user;
                        break;
                    }
                }
            }

            return userFound;
        }

        private void HandleTeleportUser(string module, string[] cmd)
        {
            if (cmd.Length < 5)
            {
                MainConsole.Instance.OutputFormat("Usage: " + TeleportUserCommandSyntax);
                return;
            }

            string firstName = cmd[2];
            string lastName = cmd[3];
            string rawDestination = cmd[4];

            ScenePresence user = GetUser(firstName, lastName);

            if (user == null)
            {
                MainConsole.Instance.OutputFormat("No user found with name {0} {1}", firstName, lastName);
                return;
            }

//            MainConsole.Instance.OutputFormat("rawDestination [{0}]", rawDestination);

            Match m = WithinRegionDestinationRegex.Match(rawDestination);

            if (!m.Success)
            {
                m = InterRegionDestinationRegex.Match(rawDestination);

                if (!m.Success)
                {
                    MainConsole.Instance.OutputFormat("Invalid destination {0}", rawDestination);
                    return;
                }
            }

            string regionName
                = m.Groups["regionName"].Success ? m.Groups["regionName"].Value : user.Scene.RegionInfo.RegionName;

            MainConsole.Instance.OutputFormat(
                "Teleporting {0} to {1},{2},{3} in {4}",
                user.Name,
                m.Groups["x"], m.Groups["y"], m.Groups["z"],
                regionName);

            user.Scene.RequestTeleportLocation(
                user.ControllingClient,
                regionName,
                new Vector3(
                    float.Parse(m.Groups["x"].Value),
                    float.Parse(m.Groups["y"].Value),
                    float.Parse(m.Groups["z"].Value)),
                user.Lookat,
                (uint)TeleportFlags.ViaLocation);
        }
    }
}