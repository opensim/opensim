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
                                          "Delete a scene object by owner", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object creator",
                                          "delete object creator <UUID>",
                                          "Delete a scene object by creator", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object uuid",
                                          "delete object uuid <UUID>",
                                          "Delete a scene object by uuid", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object name",
                                          "delete object name <name>",
                                          "Delete a scene object by name", HandleDeleteObject);
            m_console.Commands.AddCommand("region", false, "delete object outside",
                                          "delete object outside",
                                          "Delete all scene objects outside region boundaries", HandleDeleteObject);

            m_console.Commands.AddCommand(
                "region",
                false,
                "show object uuid",
                "show object uuid <UUID>",
                "Show details of a scene object with the given UUID", HandleShowObjectByUuid);

//            m_console.Commands.AddCommand(
//                "region",
//                false,
//                "show object name <UUID>",
//                "show object name <UUID>",
//                "Show details of scene objects with the given name", HandleShowObjectName);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }

        private void HandleShowObjectByUuid(string module, string[] cmd)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            if (cmd.Length < 4)
            {
                m_console.OutputFormat("Usage: show object uuid <uuid>");
                return;
            }

            UUID objectUuid;
            if (!UUID.TryParse(cmd[3], out objectUuid))
            {
                m_console.OutputFormat("{0} is not a valid uuid", cmd[3]);
                return;
            }

            SceneObjectPart sop = m_scene.GetSceneObjectPart(objectUuid);

            if (sop == null)
            {
//                m_console.OutputFormat("No object found with uuid {0}", objectUuid);
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Name:        {0}\n", sop.Name);
            sb.AppendFormat("Description: {0}\n", sop.Description);
            sb.AppendFormat("Location:    {0} @ {1}\n", sop.AbsolutePosition, sop.ParentGroup.Scene.RegionInfo.RegionName);
            sb.AppendFormat("Parent:      {0}",
                sop.IsRoot ? "Is Root\n" : string.Format("{0} {1}\n", sop.ParentGroup.Name, sop.ParentGroup.UUID));
            sb.AppendFormat("Parts:       {0}", sop.IsRoot ? "1" : sop.ParentGroup.PrimCount.ToString());

            m_console.OutputFormat(sb.ToString());
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

//                if (deletes.Count == 0)
//                    m_console.OutputFormat("No objects were found with owner {0}", match);

                break;

            case "creator":
                if (!UUID.TryParse(o, out match))
                    return;

                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    if (g.RootPart.CreatorID == match && !g.IsAttachment)
                        deletes.Add(g);
                });

//                if (deletes.Count == 0)
//                    m_console.OutputFormat("No objects were found with creator {0}", match);

                break;

            case "uuid":
                if (!UUID.TryParse(o, out match))
                    return;

                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    if (g.UUID == match && !g.IsAttachment)
                        deletes.Add(g);
                });

//                if (deletes.Count == 0)
//                    m_console.OutputFormat("No objects were found with uuid {0}", match);

                break;

            case "name":
                m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                {
                    if (g.RootPart.Name == o && !g.IsAttachment)
                        deletes.Add(g);
                });

//                if (deletes.Count == 0)
//                    m_console.OutputFormat("No objects were found with name {0}", o);

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

//                if (deletes.Count == 0)
//                    m_console.OutputFormat("No objects were found outside region bounds");

                break;
            }

            m_console.OutputFormat("Deleting {0} objects in {1}", deletes.Count, m_scene.RegionInfo.RegionName);

            foreach (SceneObjectGroup g in deletes)
            {
                m_console.OutputFormat("Deleting object {0} {1}", g.UUID, g.Name);
                m_scene.DeleteSceneObject(g, false);
            }
        }
    }
}