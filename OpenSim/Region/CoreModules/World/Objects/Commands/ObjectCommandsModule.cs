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
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Statistics;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Objects.Commands
{
    /// <summary>
    /// A module that holds commands for manipulating objects in the scene.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ObjectCommandsModule")]
    public class ObjectCommandsModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);                

        private Scene m_scene;
        private ICommandConsole m_console;

        public string Name { get { return "Object Commands Module"; } }
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[OBJECT COMMANDS MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);

            m_scene = scene;
            m_console = MainConsole.Instance;

            m_console.Commands.AddCommand("region", false, "delete object owner",
                                          "delete object owner <UUID>",
                                          "Delete object by owner", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object creator",
                                          "delete object creator <UUID>",
                                          "Delete object by creator", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object uuid",
                                          "delete object uuid <UUID>",
                                          "Delete object by uuid", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object name",
                                          "delete object name <name>",
                                          "Delete object by name", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object outside",
                                          "delete object outside",
                                          "Delete all objects outside boundaries", HandleDeleteObject);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }

        private void HandleDeleteObject(string module, string[] cmd)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            if (cmd.Length < 3)
                return;

            string mode = cmd[2];
            string o = "";

            if (mode != "outside")
            {
                if (cmd.Length < 4)
                    return;

                o = cmd[3];
            }

            List<SceneObjectGroup> deletes = new List<SceneObjectGroup>();

            UUID match;

            switch (mode)
            {
            case "owner":
                if (!UUID.TryParse(o, out match))
                    return;

                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    if (g.OwnerID == match && !g.IsAttachment)
                        deletes.Add(g);
                });

                break;

            case "creator":
                if (!UUID.TryParse(o, out match))
                    return;

                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    if (g.RootPart.CreatorID == match && !g.IsAttachment)
                        deletes.Add(g);
                });

                break;

            case "uuid":
                if (!UUID.TryParse(o, out match))
                    return;

                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    if (g.UUID == match && !g.IsAttachment)
                        deletes.Add(g);
                });

                break;

            case "name":
                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    if (g.RootPart.Name == o && !g.IsAttachment)
                        deletes.Add(g);
                });

                break;

            case "outside":
                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    SceneObjectPart rootPart = g.RootPart;
                    bool delete = false;

                    if (rootPart.GroupPosition.Z < 0.0 || rootPart.GroupPosition.Z > 10000.0)
                    {
                        delete = true;
                    }
                    else
                    {
                        ILandObject parcel
                            = m_scene.LandChannel.GetLandObject(rootPart.GroupPosition.X, rootPart.GroupPosition.Y);

                        if (parcel == null || parcel.LandData.Name == "NO LAND")
                            delete = true;
                    }

                    if (delete && !g.IsAttachment && !deletes.Contains(g))
                        deletes.Add(g);
                });

                break;
            }

            foreach (SceneObjectGroup g in deletes)
            {
                m_console.OutputFormat("Deleting object {0}", g.UUID);
                m_scene.DeleteSceneObject(g, false);
            }
        }
    }
}