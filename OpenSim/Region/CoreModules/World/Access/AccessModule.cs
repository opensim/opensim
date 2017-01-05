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
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.World
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AccessModule")]
    public class AccessModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_SceneList = new List<Scene>();

        public void Initialise(IConfigSource config)
        {
            MainConsole.Instance.Commands.AddCommand("Users", true,
                    "login enable",
                    "login enable",
                    "Enable simulator logins",
                    String.Empty,
                    HandleLoginCommand);

            MainConsole.Instance.Commands.AddCommand("Users", true,
                    "login disable",
                    "login disable",
                    "Disable simulator logins",
                    String.Empty,
                    HandleLoginCommand);

            MainConsole.Instance.Commands.AddCommand("Users", true,
                    "login status",
                    "login status",
                    "Show login status",
                    String.Empty,
                    HandleLoginCommand);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "AccessModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            lock (m_SceneList)
            {
                if (!m_SceneList.Contains(scene))
                    m_SceneList.Add(scene);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_SceneList)
                m_SceneList.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void HandleLoginCommand(string module, string[] cmd)
        {
            if ((Scene)MainConsole.Instance.ConsoleScene == null)
            {
                foreach (Scene s in m_SceneList)
                {
                    if (!ProcessCommand(s, cmd))
                        break;
                }
            }
            else
            {
                ProcessCommand((Scene)MainConsole.Instance.ConsoleScene, cmd);
            }
        }

        bool ProcessCommand(Scene scene, string[] cmd)
        {
            if (cmd.Length < 2)
            {
                MainConsole.Instance.Output("Syntax: login enable|disable|status");
                return false;
            }

            switch (cmd[1])
            {
            case "enable":
                scene.LoginsEnabled = true;
                MainConsole.Instance.Output(String.Format("Logins are enabled for region {0}", scene.RegionInfo.RegionName));
                break;
            case "disable":
                scene.LoginsEnabled = false;
                MainConsole.Instance.Output(String.Format("Logins are disabled for region {0}", scene.RegionInfo.RegionName));
                break;
            case "status":
                if (scene.LoginsEnabled)
                    MainConsole.Instance.Output(String.Format("Login in {0} are enabled", scene.RegionInfo.RegionName));
                else
                    MainConsole.Instance.Output(String.Format("Login in {0} are disabled", scene.RegionInfo.RegionName));
                break;
            default:
                MainConsole.Instance.Output("Syntax: login enable|disable|status");
                return false;
            }

            return true;
        }
    }
}
