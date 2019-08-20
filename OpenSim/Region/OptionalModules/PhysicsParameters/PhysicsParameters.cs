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
using System.Reflection;
using System.Collections.Generic;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.OptionalModules.PhysicsParameters
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "PhysicsParameters")]
    public class PhysicsParameters : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
//        private static string LogHeader = "[PHYSICS PARAMETERS]";

        private List<Scene> m_scenes = new List<Scene>();
        private static bool m_commandsLoaded = false;

        #region ISharedRegionModule
        public string Name { get { return "Runtime Physics Parameter Module"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
            // m_log.DebugFormat("{0}: INITIALIZED MODULE", LogHeader);
        }

        public void PostInitialise()
        {
            // m_log.DebugFormat("[{0}: POST INITIALIZED MODULE", LogHeader);
            InstallInterfaces();
        }

        public void Close()
        {
            // m_log.DebugFormat("{0}: CLOSED MODULE", LogHeader);
        }

        public void AddRegion(Scene scene)
        {
            // m_log.DebugFormat("{0}: REGION {1} ADDED", LogHeader, scene.RegionInfo.RegionName);
            m_scenes.Add(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            // m_log.DebugFormat("{0}: REGION {1} REMOVED", LogHeader, scene.RegionInfo.RegionName);
            if (m_scenes.Contains(scene))
                m_scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            // m_log.DebugFormat("{0}: REGION {1} LOADED", LogHeader, scene.RegionInfo.RegionName);
        }
        #endregion INonSharedRegionModule

        private const string getInvocation = "physics get [<param>|ALL]";
        private const string setInvocation = "physics set <param> [<value>|TRUE|FALSE] [localID|ALL]";
        private const string listInvocation = "physics list";
        private void InstallInterfaces()
        {
            if (!m_commandsLoaded)
            {
                MainConsole.Instance.Commands.AddCommand(
                    "Regions", false, "physics set",
                    setInvocation,
                    "Set physics parameter from currently selected region",
                    ProcessPhysicsSet);

                MainConsole.Instance.Commands.AddCommand(
                    "Regions", false, "physics get",
                    getInvocation,
                    "Get physics parameter from currently selected region",
                    ProcessPhysicsGet);

                MainConsole.Instance.Commands.AddCommand(
                    "Regions", false, "physics list",
                    listInvocation,
                    "List settable physics parameters",
                    ProcessPhysicsList);

                m_commandsLoaded = true;
            }
        }

        // TODO: extend get so you can get a value from an individual localID
        private void ProcessPhysicsGet(string module, string[] cmdparms)
        {
            if (cmdparms.Length != 3)
            {
                WriteError("Parameter count error. Invocation: " + getInvocation);
                return;
            }
            string parm = cmdparms[2];

            if (SceneManager.Instance == null || SceneManager.Instance.CurrentScene == null)
            {
                WriteError("Error: no region selected. Use 'change region' to select a region.");
                return;
            }

            Scene scene = SceneManager.Instance.CurrentScene;
            IPhysicsParameters physScene = scene.PhysicsScene as IPhysicsParameters;
            if (physScene != null)
            {
                if (parm.ToLower() == "all")
                {
                    foreach (PhysParameterEntry ppe in physScene.GetParameterList())
                    {
                        string val = string.Empty;
                        if (physScene.GetPhysicsParameter(ppe.name, out val))
                        {
                            WriteOut("  {0}/{1} = {2}", scene.RegionInfo.RegionName, ppe.name, val);
                        }
                        else
                        {
                            WriteOut("  {0}/{1} = {2}", scene.RegionInfo.RegionName, ppe.name, "unknown");
                        }
                    }
                }
                else
                {
                    string val = string.Empty;
                    if (physScene.GetPhysicsParameter(parm, out val))
                    {
                        WriteOut("  {0}/{1} = {2}", scene.RegionInfo.RegionName, parm, val);
                    }
                    else
                    {
                        WriteError("Failed fetch of parameter '{0}' from region '{1}'", parm, scene.RegionInfo.RegionName);
                    }
                }
            }
            else
            {
                WriteError("Region '{0}' physics engine has no gettable physics parameters", scene.RegionInfo.RegionName);
            }
            return;
        }

        private void ProcessPhysicsSet(string module, string[] cmdparms)
        {
            if (cmdparms.Length < 4 || cmdparms.Length > 5)
            {
                WriteError("Parameter count error. Invocation: " + getInvocation);
                return;
            }
            string parm = "xxx";
            string valparm = String.Empty;
            uint localID = (uint)PhysParameterEntry.APPLY_TO_NONE;  // set default value
            try
            {
                parm = cmdparms[2];
                valparm = cmdparms[3].ToLower();
                if (cmdparms.Length > 4)
                {
                    if (cmdparms[4].ToLower() == "all")
                        localID = (uint)PhysParameterEntry.APPLY_TO_ALL;
                    else
                        localID = uint.Parse(cmdparms[2], Culture.NumberFormatInfo);
                }
            }
            catch
            {
                WriteError("  Error parsing parameters. Invocation: " + setInvocation);
                return;
            }

            if (SceneManager.Instance == null || SceneManager.Instance.CurrentScene == null)
            {
                WriteError("Error: no region selected. Use 'change region' to select a region.");
                return;
            }

            Scene scene = SceneManager.Instance.CurrentScene;
            IPhysicsParameters physScene = scene.PhysicsScene as IPhysicsParameters;
            if (physScene != null)
            {
                if (!physScene.SetPhysicsParameter(parm, valparm, localID))
                {
                    WriteError("Failed set of parameter '{0}' for region '{1}'", parm, scene.RegionInfo.RegionName);
                }
            }
            else
            {
                WriteOut("Region '{0}'s physics engine has no settable physics parameters", scene.RegionInfo.RegionName);
            }
            return;
        }

        private void ProcessPhysicsList(string module, string[] cmdparms)
        {
            if (SceneManager.Instance == null || SceneManager.Instance.CurrentScene == null)
            {
                WriteError("Error: no region selected. Use 'change region' to select a region.");
                return;
            }
            Scene scene = SceneManager.Instance.CurrentScene;

            IPhysicsParameters physScene = scene.PhysicsScene as IPhysicsParameters;
            if (physScene != null)
            {
                WriteOut("Available physics parameters:");
                PhysParameterEntry[] parms = physScene.GetParameterList();
                foreach (PhysParameterEntry ent in parms)
                {
                    WriteOut("   {0}: {1}", ent.name, ent.desc);
                }
            }
            else
            {
                WriteError("Current regions's physics engine has no settable physics parameters");
            }
            return;
        }

        private void WriteOut(string msg, params object[] args)
        {
            // m_log.InfoFormat(msg, args);
            MainConsole.Instance.Output(msg, null, args);
        }

        private void WriteError(string msg, params object[] args)
        {
            // m_log.ErrorFormat(msg, args);
            MainConsole.Instance.Output(msg, null, args);
        }
    }
}
