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
using OpenSim.Region.Framework.Scenes.Animation;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.OptionalModules.Avatar.SitStand
{
    /// <summary>
    /// A module that just holds commands for changing avatar sitting and standing states.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AnimationsCommandModule")]
    public class SitStandCommandModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        public string Name { get { return "SitStand Command Module"; } }
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[ATTACHMENTS COMMAND MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[ANIMATIONS COMMAND MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);

            m_scene = scene;

            scene.AddCommand(
                "Users", this, "sit user name",
                "sit user name <first-name> <last-name>",
                "Sit the named user on an unoccupied object with a sit target.\n"
                    + "If there are no such objects then nothing happens",
                HandleSitUserNameCommand);

            scene.AddCommand(
                "Users", this, "stand user name",
                "stand user name <first-name> <last-name>",
                "Stand the named user.",
                HandleStandUserNameCommand);
        }

        protected void HandleSitUserNameCommand(string module, string[] cmd)
        {
            if (MainConsole.Instance.ConsoleScene != m_scene && MainConsole.Instance.ConsoleScene != null)
                return;

            if (cmd.Length != 5)
            {
                MainConsole.Instance.Output("Usage: sit user name <first-name> <last-name>");
                return;
            }

            string firstName = cmd[3];
            string lastName = cmd[4];

            ScenePresence sp = m_scene.GetScenePresence(firstName, lastName);
    
            if (sp == null || sp.IsChildAgent)
                return;

            SceneObjectPart sitPart = null;
            List<SceneObjectGroup> sceneObjects = m_scene.GetSceneObjectGroups();

            foreach (SceneObjectGroup sceneObject in sceneObjects)
            {
                foreach (SceneObjectPart part in sceneObject.Parts)
                {
                    if (part.IsSitTargetSet && part.SitTargetAvatar == UUID.Zero)
                    {
                        sitPart = part;
                        break;
                    }
                }
            }

            if (sitPart != null)
            {
                MainConsole.Instance.OutputFormat(
                    "Sitting {0} on {1} {2} in {3}", 
                    sp.Name, sitPart.ParentGroup.Name, sitPart.ParentGroup.UUID, m_scene.Name);

                sp.HandleAgentRequestSit(sp.ControllingClient, sp.UUID, sitPart.UUID, Vector3.Zero);
                sp.HandleAgentSit(sp.ControllingClient, sp.UUID);
            }
            else
            {
                MainConsole.Instance.OutputFormat(
                    "Could not find any unoccupied set seat on which to sit {0} in {1}",
                    sp.Name, m_scene.Name);
            }
        }

        protected void HandleStandUserNameCommand(string module, string[] cmd)
        {
            if (MainConsole.Instance.ConsoleScene != m_scene && MainConsole.Instance.ConsoleScene != null)
                return;

            if (cmd.Length != 5)
            {
                MainConsole.Instance.Output("Usage: stand user name <first-name> <last-name>");
                return;
            }

            string firstName = cmd[3];
            string lastName = cmd[4];

            ScenePresence sp = m_scene.GetScenePresence(firstName, lastName);
    
            if (sp == null || sp.IsChildAgent)
                return;

            sp.StandUp();
        }
    }
}