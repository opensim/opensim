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
                "Show live scene information for the currently selected region.", HandleShowScene);
        }

        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[REGION COMMANDS MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
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
            float otherFrameTime          = stats[11];
//            float imageFrameTime          = stats.StatsBlock[12].StatValue; // Ignored
            float inPacketsPerSecond      = stats[13];
            float outPacketsPerSecond     = stats[14];
            float unackedBytes            = stats[15];
//            float agentFrameTime          = stats.StatsBlock[16].StatValue; // Not really used
            float pendingDownloads        = stats[17];
            float pendingUploads          = stats[18];
            float activeScripts           = stats[19];
            float scriptLinesPerSecond    = stats[20];

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
    }
}