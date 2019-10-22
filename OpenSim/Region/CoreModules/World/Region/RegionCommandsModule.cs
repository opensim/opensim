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
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.World.Objects.Commands
{
    /// <summary>
    /// A module that holds commands for manipulating objects in the scene.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RegionCommandsModule")]
    public class RegionCommandsModule : INonSharedRegionModule
    {
        private Scene m_scene;
        private ICommandConsole m_console;

        public string Name { get { return "Region Commands Module"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: INITIALIZED MODULE");
        }

        public void PostInitialise()
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: POST INITIALIZED MODULE");
        }

        public void Close()
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: CLOSED MODULE");
        }

        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);

            m_scene = scene;
            m_console = MainConsole.Instance;

            m_console.Commands.AddCommand(
                "Regions", false, "show scene",
                "show scene",
                "Show live information for the currently selected scene (fps, prims, etc.).", HandleShowScene);

            m_console.Commands.AddCommand(
                "Regions", false, "show region",
                "show region",
                "Show control information for the currently selected region (host name, max physical prim size, etc).",
                "A synonym for \"region get\"",
                HandleShowRegion);

            m_console.Commands.AddCommand(
                "Regions", false, "region get",
                "region get",
                "Show control information for the currently selected region (host name, max physical prim size, etc).",
                "Some parameters can be set with the \"region set\" command.\n"
                + "Others must be changed via a viewer (usually via the region/estate dialog box).",
                HandleShowRegion);

            m_console.Commands.AddCommand(
                "Regions", false, "region set",
                "region set",
                "Set control information for the currently selected region.",
                "Currently, the following parameters can be set:\n"
                + "agent-limit <int>     - Current root agent limit.  This is persisted over restart.\n"
                + "max-agent-limit <int> - Maximum root agent limit.  agent-limit cannot exceed this."
                + "  This is not persisted over restart - to set it every time you must add a MaxAgents entry to your regions file.",
                HandleRegionSet);

            m_console.Commands.AddCommand("Regions", false, "show neighbours",
                "show neighbours",
                "Shows the local region neighbours", HandleShowNeighboursCommand);

            m_console.Commands.AddCommand("Regions", false, "show regionsinview",
                "show regionsinview",
                "Shows regions that can be seen from a region", HandleShowRegionsInViewCommand);

        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }

        private void HandleShowRegion(string module, string[] cmd)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            RegionInfo ri = m_scene.RegionInfo;
            RegionSettings rs = ri.RegionSettings;

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Region information for {0}\n", m_scene.Name);

            ConsoleDisplayList dispList = new ConsoleDisplayList();
            dispList.AddRow("Region ID", ri.RegionID);
            dispList.AddRow("Region handle", ri.RegionHandle);
            dispList.AddRow("Region location", string.Format("{0},{1}", ri.RegionLocX, ri.RegionLocY));
            dispList.AddRow("Region size", string.Format("{0}x{1}", ri.RegionSizeX, ri.RegionSizeY));
            //dispList.AddRow("Region type", ri.RegionType);
            dispList.AddRow("Maturity", rs.Maturity);
            dispList.AddRow("Region address", ri.ServerURI);
            dispList.AddRow("From region file", ri.RegionFile);
            dispList.AddRow("External endpoint", ri.ExternalEndPoint);
            dispList.AddRow("Internal endpoint", ri.InternalEndPoint);
            dispList.AddRow("Access level", ri.AccessLevel);
            dispList.AddRow("Agent limit", rs.AgentLimit);
            dispList.AddRow("Max agent limit", ri.AgentCapacity);
            dispList.AddRow("Linkset capacity", ri.LinksetCapacity <= 0 ? "not set" : ri.LinksetCapacity.ToString());
            dispList.AddRow("Prim capacity", ri.ObjectCapacity);
            dispList.AddRow("Prim bonus", rs.ObjectBonus);
            dispList.AddRow("Max prims per user", ri.MaxPrimsPerUser < 0 ? "n/a" : ri.MaxPrimsPerUser.ToString());
            dispList.AddRow("Clamp prim size", ri.ClampPrimSize);
            dispList.AddRow("Non physical prim min size", ri.NonphysPrimMin <= 0 ? "not set" : string.Format("{0} m", ri.NonphysPrimMin));
            dispList.AddRow("Non physical prim max size", ri.NonphysPrimMax <= 0 ? "not set" : string.Format("{0} m", ri.NonphysPrimMax));
            dispList.AddRow("Physical prim min size", ri.PhysPrimMin <= 0 ? "not set" : string.Format("{0} m", ri.PhysPrimMin));
            dispList.AddRow("Physical prim max size", ri.PhysPrimMax <= 0 ? "not set" : string.Format("{0} m", ri.PhysPrimMax));

            dispList.AddRow("Allow Damage", rs.AllowDamage);
            dispList.AddRow("Allow Land join/divide", rs.AllowLandJoinDivide);
            dispList.AddRow("Allow land resell", rs.AllowLandResell);
            dispList.AddRow("Block fly", rs.BlockFly);
            dispList.AddRow("Block show in search", rs.BlockShowInSearch);
            dispList.AddRow("Block terraform", rs.BlockTerraform);
            dispList.AddRow("Covenant UUID", rs.Covenant);
            dispList.AddRow("Convenant change Unix time", rs.CovenantChangedDateTime);
            dispList.AddRow("Disable collisions", rs.DisableCollisions);
            dispList.AddRow("Disable physics", rs.DisablePhysics);
            dispList.AddRow("Disable scripts", rs.DisableScripts);
            dispList.AddRow("Restrict pushing", rs.RestrictPushing);
            dispList.AddRow("Fixed sun", rs.FixedSun);
            dispList.AddRow("Sun position", rs.SunPosition);
            dispList.AddRow("Sun vector", rs.SunVector);
            dispList.AddRow("Use estate sun", rs.UseEstateSun);
            dispList.AddRow("Telehub UUID", rs.TelehubObject);
            dispList.AddRow("Terrain lower limit", string.Format("{0} m", rs.TerrainLowerLimit));
            dispList.AddRow("Terrain raise limit", string.Format("{0} m", rs.TerrainRaiseLimit));
            dispList.AddRow("Water height", string.Format("{0} m", rs.WaterHeight));

            dispList.AddRow("Maptile static file", ri.MaptileStaticFile);
            dispList.AddRow("Maptile static UUID", ri.MaptileStaticUUID);
            dispList.AddRow("Last map refresh", ri.lastMapRefresh);
            dispList.AddRow("Last map UUID", ri.lastMapUUID);

            dispList.AddToStringBuilder(sb);

            MainConsole.Instance.Output(sb.ToString());
        }

        private void HandleRegionSet(string module, string[] args)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            if (args.Length != 4)
            {
                MainConsole.Instance.Output("Usage: region set <param> <value>");
                return;
            }

            string param = args[2];
            string rawValue = args[3];

            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            RegionInfo ri = m_scene.RegionInfo;
            RegionSettings rs = ri.RegionSettings;

            if (param == "agent-limit")
            {
                int newValue;

                if (!ConsoleUtil.TryParseConsoleNaturalInt(MainConsole.Instance, rawValue, out newValue))
                    return;

                if (newValue > ri.AgentCapacity)
                {
                    MainConsole.Instance.Output(
                        "Cannot set {0} to {1} in {2} as max-agent-limit is {3}", "agent-limit",
                        newValue, m_scene.Name, ri.AgentCapacity);
                }
                else
                {
                    rs.AgentLimit = newValue;

                    MainConsole.Instance.Output(
                        "{0} set to {1} in {2}", "agent-limit", newValue, m_scene.Name);
                }

                rs.Save();
            }
            else if (param == "max-agent-limit")
            {
                int newValue;

                if (!ConsoleUtil.TryParseConsoleNaturalInt(MainConsole.Instance, rawValue, out newValue))
                    return;

                ri.AgentCapacity = newValue;

                MainConsole.Instance.Output(
                    "max-agent-limit set to {0} in {1}", newValue, m_scene.Name);

                if (ri.AgentCapacity < rs.AgentLimit)
                {
                    rs.AgentLimit = ri.AgentCapacity;

                    MainConsole.Instance.Output(
                        "agent-limit set to {0} in {1}", rs.AgentLimit, m_scene.Name);
                }

                rs.Save();
            }
        }

        private void HandleShowScene(string module, string[] cmd)
        {
            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            SimStatsReporter r = m_scene.StatsReporter;
            float[] stats = r.LastReportedSimStats;

            float timeDilation            = stats[0];
            float simFps                  = stats[1];
            float physicsFps              = stats[2];
            float agentUpdates            = stats[3];
            float rootAgents              = stats[4];
            float childAgents             = stats[5];
            float totalPrims              = stats[6];
            float activePrims             = stats[7];
            float totalFrameTime          = stats[8];
//            float netFrameTime            = stats.StatsBlock[9].StatValue; // Ignored - not used by OpenSimulator
            float physicsFrameTime        = stats[10];
            float otherFrameTime          = stats[12];
//            float imageFrameTime          = stats.StatsBlock[11].StatValue; // Ignored
            float inPacketsPerSecond      = stats[13];
            float outPacketsPerSecond     = stats[14];
            float unackedBytes            = stats[15];
//            float agentFrameTime          = stats.StatsBlock[16].StatValue; // Not really used
            float pendingDownloads        = stats[17];
            float pendingUploads          = stats[18];
            float activeScripts           = stats[19];
            float scriptLinesPerSecond    = stats[23];

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Scene statistics for {0}\n", m_scene.RegionInfo.RegionName);

            ConsoleDisplayList dispList = new ConsoleDisplayList();
            dispList.AddRow("Time Dilation", timeDilation);
            dispList.AddRow("Sim FPS", simFps);
            dispList.AddRow("Physics FPS", physicsFps);
            dispList.AddRow("Avatars", rootAgents);
            dispList.AddRow("Child agents", childAgents);
            dispList.AddRow("Total prims", totalPrims);
            dispList.AddRow("Scripts", activeScripts);
            dispList.AddRow("Script lines processed per second", scriptLinesPerSecond);
            dispList.AddRow("Physics enabled prims", activePrims);
            dispList.AddRow("Total frame time", totalFrameTime);
            dispList.AddRow("Physics frame time", physicsFrameTime);
            dispList.AddRow("Other frame time", otherFrameTime);
            dispList.AddRow("Agent Updates per second", agentUpdates);
            dispList.AddRow("Packets processed from clients per second", inPacketsPerSecond);
            dispList.AddRow("Packets sent to clients per second", outPacketsPerSecond);
            dispList.AddRow("Bytes unacknowledged by clients", unackedBytes);
            dispList.AddRow("Pending asset downloads to clients", pendingDownloads);
            dispList.AddRow("Pending asset uploads from clients", pendingUploads);

            dispList.AddToStringBuilder(sb);

            MainConsole.Instance.Output(sb.ToString());
        }

        public void HandleShowNeighboursCommand(string module, string[] cmdparams)
        {
            if(m_scene == null)
                return;

            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            System.Text.StringBuilder caps = new System.Text.StringBuilder();

            RegionInfo sr = m_scene.RegionInfo;
            caps.AppendFormat("*** Neighbours of {0} ({1}) ***\n", sr.RegionName, sr.RegionID);
            List<GridRegion> regions = m_scene.GridService.GetNeighbours(sr.ScopeID, sr.RegionID);
                foreach (GridRegion r in regions)
                    caps.AppendFormat("    {0} @ {1}-{2}\n", r.RegionName, Util.WorldToRegionLoc((uint)r.RegionLocX), Util.WorldToRegionLoc((uint)r.RegionLocY));

            MainConsole.Instance.Output(caps.ToString());
        }

        public void HandleShowRegionsInViewCommand(string module, string[] cmdparams)
        {
            if(m_scene == null)
                return;

            if (!(MainConsole.Instance.ConsoleScene == null || MainConsole.Instance.ConsoleScene == m_scene))
                return;

            System.Text.StringBuilder caps = new System.Text.StringBuilder();
            int maxview = (int)m_scene.MaxRegionViewDistance;
            RegionInfo sr = m_scene.RegionInfo;
            caps.AppendFormat("*** Regions that can be seen from {0} ({1}) (MaxRegionViewDistance {2}m) ***\n", sr.RegionName, sr.RegionID, maxview);
            int startX = (int)sr.WorldLocX;
            int endX = startX + (int)sr.RegionSizeX;
            int startY = (int)sr.WorldLocY;
            int endY = startY + (int)sr.RegionSizeY;
            startX -= maxview;
            if(startX < 0 )
                startX = 0;
            startY -= maxview;
            if(startY < 0)
                startY = 0;
            endX += maxview;
            endY += maxview;

            List<GridRegion> regions = m_scene.GridService.GetRegionRange(sr.ScopeID, startX, endX, startY, endY);
            foreach (GridRegion r in regions)
            {
                if(r.RegionHandle == sr.RegionHandle)
                    continue;
                caps.AppendFormat("    {0} @ {1}-{2}\n", r.RegionName, Util.WorldToRegionLoc((uint)r.RegionLocX), Util.WorldToRegionLoc((uint)r.RegionLocY));
            }

            MainConsole.Instance.Output(caps.ToString());
        }
    }
}
