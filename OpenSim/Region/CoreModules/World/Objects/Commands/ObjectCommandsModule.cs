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

            m_console.Commands.AddCommand(
                "Objects", false, "delete object owner",
                "delete object owner <UUID>",
                "Delete a scene object by owner", HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object creator",
                "delete object creator <UUID>",
                "Delete a scene object by creator", HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object uuid",
                "delete object uuid <UUID>",
                "Delete a scene object by uuid", HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object name",
                "delete object name [--regex] <name>",
                "Delete a scene object by name.",
                "If --regex is specified then the name is treatead as a regular expression",
                HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects", false, "delete object outside",
                "delete object outside",
                "Delete all scene objects outside region boundaries", HandleDeleteObject);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show object uuid",
                "show object uuid <UUID>",
                "Show details of a scene object with the given UUID", HandleShowObjectByUuid);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show object name",
                "show object name [--regex] <name>",
                "Show details of scene objects with the given name.",
                "If --regex is specified then the name is treatead as a regular expression",
                HandleShowObjectByName);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show object pos",
                "show object pos <start-coord> to <end-coord>",
                "Show details of scene objects within the given area.",
                "Each component of the coord is comma separated.  There must be no spaces between the commas.\n"
                    + "If you don't care about the z component you can simply omit it.\n"
                    + "If you don't care about the x or y components then you can leave them blank (though a comma is still required)\n"
                    + "If you want to specify the maxmimum value of a component then you can use ~ instead of a number\n"
                    + "If you want to specify the minimum value of a component then you can use -~ instead of a number\n"
                    + "e.g.\n"
                    + "show object pos 20,20,20 to 40,40,40\n"
                    + "show object pos 20,20 to 40,40\n"
                    + "show object pos ,20,20 to ,40,40\n"
                    + "show object pos ,,30 to ,,~\n"
                    + "show object pos ,,-~ to ,,30",
                HandleShowObjectByPos);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show part uuid",
                "show part uuid <UUID>",
                "Show details of a scene object parts with the given UUID", HandleShowPartByUuid);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show part name",
                "show part name [--regex] <name>",
                "Show details of scene object parts with the given name.",
                "If --regex is specified then the name is treatead as a regular expression",
                HandleShowPartByName);

            m_console.Commands.AddCommand(
                "Objects",
                false,
                "show part pos",
                "show part pos <start-coord> to <end-coord>",
                "Show details of scene object parts within the given area.",
                "Each component of the coord is comma separated.  There must be no spaces between the commas.\n"
                    + "If you don't care about the z component you can simply omit it.\n"
                    + "If you don't care about the x or y components then you can leave them blank (though a comma is still required)\n"
                    + "If you want to specify the maxmimum value of a component then you can use ~ instead of a number\n"
                    + "If you want to specify the minimum value of a component then you can use -~ instead of a number\n"
                    + "e.g.\n"
                    + "show object pos 20,20,20 to 40,40,40\n"
                    + "show object pos 20,20 to 40,40\n"
                    + "show object pos ,20,20 to ,40,40\n"
                    + "show object pos ,,30 to ,,~\n"
                    + "show object pos ,,-~ to ,,30",
                HandleShowPartByPos);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[OBJECTS COMMANDS MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }

        private void OutputSogsToConsole(Predicate<SceneObjectGroup> searchPredicate)
        {
            List<SceneObjectGroup> sceneObjects = m_scene.GetSceneObjectGroups().FindAll(searchPredicate);

            StringBuilder sb = new StringBuilder();

            foreach (SceneObjectGroup so in sceneObjects)
            {
                AddSceneObjectReport(sb, so);
                sb.Append("\n");
            }

            sb.AppendFormat("{0} object(s) found in {1}\n", sceneObjects.Count, m_scene.Name);

            m_console.OutputFormat(sb.ToString());
        }

        private void OutputSopsToConsole(Predicate<SceneObjectPart> searchPredicate)
        {
            List<SceneObjectGroup> sceneObjects = m_scene.GetSceneObjectGroups();
            List<SceneObjectPart> parts = new List<SceneObjectPart>();

            sceneObjects.ForEach(so => parts.AddRange(Array.FindAll<SceneObjectPart>(so.Parts, searchPredicate)));

            StringBuilder sb = new StringBuilder();

            foreach (SceneObjectPart part in parts)
            {
                AddScenePartReport(sb, part);
                sb.Append("\n");
            }

            sb.AppendFormat("{0} parts found in {1}\n", parts.Count, m_scene.Name);

            m_console.OutputFormat(sb.ToString());
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

            SceneObjectGroup so = m_scene.GetSceneObjectGroup(objectUuid);

            if (so == null)
            {
//                m_console.OutputFormat("No part found with uuid {0}", objectUuid);
                return;
            }

            StringBuilder sb = new StringBuilder();
            AddSceneObjectReport(sb, so);

            m_console.OutputFormat(sb.ToString());
        }

        private void HandleShowObjectByName(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            bool useRegex = false;
            OptionSet options = new OptionSet().Add("regex", v=> useRegex = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: show object name [--regex] <name>");
                return;
            }

            string name = mainParams[3];

            Predicate<SceneObjectGroup> searchPredicate;

            if (useRegex)
            {
                Regex nameRegex = new Regex(name);
                searchPredicate = so => nameRegex.IsMatch(so.Name);
            }
            else
            {
                searchPredicate = so => so.Name == name;
            }

            OutputSogsToConsole(searchPredicate);
        }

        private void HandleShowObjectByPos(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            if (cmdparams.Length < 5)
            {
                m_console.OutputFormat("Usage: show object pos <start-coord> to <end-coord>");
                return;
            }

            string rawConsoleStartVector = cmdparams[3];
            Vector3 startVector;

            if (!ConsoleUtil.TryParseConsoleMinVector(rawConsoleStartVector, out startVector))
            {
                m_console.OutputFormat("Error: Start vector {0} does not have a valid format", rawConsoleStartVector);
                return;
            }

            string rawConsoleEndVector = cmdparams[5];
            Vector3 endVector;

            if (!ConsoleUtil.TryParseConsoleMaxVector(rawConsoleEndVector, out endVector))
            {
                m_console.OutputFormat("Error: End vector {0} does not have a valid format", rawConsoleEndVector);
                return;
            }

            Predicate<SceneObjectGroup> searchPredicate
                = so => Util.IsInsideBox(so.AbsolutePosition, startVector, endVector);

            OutputSogsToConsole(searchPredicate);
        }

        private void HandleShowPartByUuid(string module, string[] cmd)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            if (cmd.Length < 4)
            {
                m_console.OutputFormat("Usage: show part uuid <uuid>");
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
//                m_console.OutputFormat("No part found with uuid {0}", objectUuid);
                return;
            }

            StringBuilder sb = new StringBuilder();
            AddScenePartReport(sb, sop);

            m_console.OutputFormat(sb.ToString());
        }

        private void HandleShowPartByPos(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            if (cmdparams.Length < 5)
            {
                m_console.OutputFormat("Usage: show part pos <start-coord> to <end-coord>");
                return;
            }

            string rawConsoleStartVector = cmdparams[3];
            Vector3 startVector;

            if (!ConsoleUtil.TryParseConsoleMinVector(rawConsoleStartVector, out startVector))
            {
                m_console.OutputFormat("Error: Start vector {0} does not have a valid format", rawConsoleStartVector);
                return;
            }

            string rawConsoleEndVector = cmdparams[5];
            Vector3 endVector;

            if (!ConsoleUtil.TryParseConsoleMaxVector(rawConsoleEndVector, out endVector))
            {
                m_console.OutputFormat("Error: End vector {0} does not have a valid format", rawConsoleEndVector);
                return;
            }

            OutputSopsToConsole(sop => Util.IsInsideBox(sop.AbsolutePosition, startVector, endVector));
        }

        private void HandleShowPartByName(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return;

            bool useRegex = false;
            OptionSet options = new OptionSet().Add("regex", v=> useRegex = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: show part name [--regex] <name>");
                return;
            }

            string name = mainParams[3];

            Predicate<SceneObjectPart> searchPredicate;

            if (useRegex)
            {
                Regex nameRegex = new Regex(name);
                searchPredicate = sop => nameRegex.IsMatch(sop.Name);
            }
            else
            {
                searchPredicate = sop => sop.Name == name;
            }

            OutputSopsToConsole(searchPredicate);
        }

        private StringBuilder AddSceneObjectReport(StringBuilder sb, SceneObjectGroup so)
        {
            sb.AppendFormat("Name:        {0}\n", so.Name);
            sb.AppendFormat("Description: {0}\n", so.Description);
            sb.AppendFormat("Location:    {0} @ {1}\n", so.AbsolutePosition, so.Scene.RegionInfo.RegionName);
            sb.AppendFormat("Parts:       {0}\n", so.PrimCount);
            sb.AppendFormat("Flags:       {0}\n", so.RootPart.Flags);

            return sb;
        }

        private StringBuilder AddScenePartReport(StringBuilder sb, SceneObjectPart sop)
        {
            sb.AppendFormat("Name:        {0}\n", sop.Name);
            sb.AppendFormat("Description: {0}\n", sop.Description);
            sb.AppendFormat("Location:    {0} @ {1}\n", sop.AbsolutePosition, sop.ParentGroup.Scene.RegionInfo.RegionName);
            sb.AppendFormat("Parent:      {0}",
                sop.IsRoot ? "Is Root\n" : string.Format("{0} {1}\n", sop.ParentGroup.Name, sop.ParentGroup.UUID));
            sb.AppendFormat("Link number: {0}\n", sop.LinkNum);
            sb.AppendFormat("Flags:       {0}\n", sop.Flags);

            return sb;
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

            List<SceneObjectGroup> deletes = null;
            UUID match;
            bool requireConfirmation = true;

            switch (mode)
            {
                case "owner":
                    if (!UUID.TryParse(o, out match))
                        return;

                    deletes = new List<SceneObjectGroup>();

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

                    deletes = new List<SceneObjectGroup>();

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

                    requireConfirmation = false;
                    deletes = new List<SceneObjectGroup>();
        
                    m_scene.ForEachSOG(delegate (SceneObjectGroup g)
                    {
                        if (g.UUID == match && !g.IsAttachment)
                            deletes.Add(g);
                    });
        
        //                if (deletes.Count == 0)
        //                    m_console.OutputFormat("No objects were found with uuid {0}", match);
        
                    break;
        
                case "name":
                    deletes = GetDeleteCandidatesByName(module, cmd);
                    break;
        
                case "outside":
                    deletes = new List<SceneObjectGroup>();

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
        
                    if (deletes.Count == 0)
                        m_console.OutputFormat("No objects were found outside region bounds");
        
                    break;

                default:
                    m_console.OutputFormat("Unrecognized mode {0}", mode);
                    return;
            }

            if (deletes == null || deletes.Count <= 0)
                return;

            if (requireConfirmation)
            {
                string response = MainConsole.Instance.CmdPrompt(
                    string.Format(
                        "Are you sure that you want to delete {0} objects from {1}",
                        deletes.Count, m_scene.RegionInfo.RegionName),
                    "n");
    
                if (response.ToLower() != "y")
                {
                    MainConsole.Instance.OutputFormat(
                        "Aborting delete of {0} objects from {1}", deletes.Count, m_scene.RegionInfo.RegionName);

                    return;
                }
            }

            m_console.OutputFormat("Deleting {0} objects in {1}", deletes.Count, m_scene.RegionInfo.RegionName);

            foreach (SceneObjectGroup g in deletes)
            {
                m_console.OutputFormat("Deleting object {0} {1}", g.UUID, g.Name);
                m_scene.DeleteSceneObject(g, false);
            }
        }

        private List<SceneObjectGroup> GetDeleteCandidatesByName(string module, string[] cmdparams)
        {
            if (!(m_console.ConsoleScene == null || m_console.ConsoleScene == m_scene))
                return null;

            bool useRegex = false;
            OptionSet options = new OptionSet().Add("regex", v=> useRegex = v != null );

            List<string> mainParams = options.Parse(cmdparams);

            if (mainParams.Count < 4)
            {
                m_console.OutputFormat("Usage: delete object name [--regex] <name>");
                return null;
            }

            string name = mainParams[3];

            List<SceneObjectGroup> sceneObjects = new List<SceneObjectGroup>();
            Action<SceneObjectGroup> searchAction;

            if (useRegex)
            {
                Regex nameRegex = new Regex(name);
                searchAction = so => { if (nameRegex.IsMatch(so.Name)) { sceneObjects.Add(so); }};
            }
            else
            {
                searchAction = so => { if (so.Name == name) { sceneObjects.Add(so); }};
            }

            m_scene.ForEachSOG(searchAction);

            if (sceneObjects.Count == 0)
                m_console.OutputFormat("No objects with name {0} found in {1}", name, m_scene.RegionInfo.RegionName);

            return sceneObjects;
        }
    }
}