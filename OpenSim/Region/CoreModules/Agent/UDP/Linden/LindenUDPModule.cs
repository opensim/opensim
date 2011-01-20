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

namespace OpenSim.Region.CoreModules.UDP.Linden
{
    /// <summary>
    /// A module that just holds commands for inspecting/changing the Linden UDP client stack
    /// </summary>
    /// <remarks>
    /// All actual client stack functionality remains in OpenSim.Region.ClientStack.LindenUDP
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LindenUDPModule")]
    public class LindenUDPModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);                
        
        protected Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
        
        public string Name { get { return "Linden UDP Module"; } }        
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[LINDEN UDP MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[LINDEN UDP MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[LINDEN UDP MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[LINDEN UDP MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes[scene.RegionInfo.RegionID] = scene;            

            scene.AddCommand(
                this, "show queues",
                "show queues [full]",
                "Show queue data for each client", 
                "Without the 'full' option, only users actually on the region are shown."
                  + "  With the 'full' option child agents of users in neighbouring regions are also shown.",                                          
                ShowQueuesReport);          
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[LINDEN UDP MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Remove(scene.RegionInfo.RegionID);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[LINDEN UDP MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }                 

        protected void ShowQueuesReport(string module, string[] cmd)
        {                       
            MainConsole.Instance.Output(GetQueuesReport(cmd));
        }
        
        /// <summary>
        /// Generate UDP Queue data report for each client
        /// </summary>
        /// <param name="showParams"></param>
        /// <returns></returns>
        protected string GetQueuesReport(string[] showParams)
        {
            bool showChildren = false;
            
            if (showParams.Length > 2 && showParams[2] == "full")
                showChildren = true;               
                
            StringBuilder report = new StringBuilder();            
            
            int columnPadding = 2;
            int maxNameLength = 18;                                    
            int maxRegionNameLength = 14;
            int maxTypeLength = 4;
            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;                        
                        
            report.AppendFormat("{0,-" + maxNameLength +  "}{1,-" + columnPadding + "}", "User", "");
            report.AppendFormat("{0,-" + maxRegionNameLength +  "}{1,-" + columnPadding + "}", "Region", "");
            report.AppendFormat("{0,-" + maxTypeLength +  "}{1,-" + columnPadding + "}", "Type", "");
            
            report.AppendFormat(
                "{0,7} {1,7} {2,9} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7} {10,7}\n",
                "Pkts",
                "Pkts",
                "Bytes",
                "Pkts",
                "Pkts",
                "Pkts",
                "Pkts",
                "Pkts",
                "Pkts",
                "Pkts",
                "Pkts");
    
            report.AppendFormat("{0,-" + totalInfoFieldsLength +  "}", "");
            report.AppendFormat(
                "{0,7} {1,7} {2,9} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7} {10,7}\n",
                "Out",
                "In",
                "Unacked",
                "Resend",
                "Land",
                "Wind",
                "Cloud",
                "Task",
                "Texture",
                "Asset",
                "State");            
            
            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is IStatsCollector)
                            {
                                bool isChild = scene.PresenceChildStatus(client.AgentId);
                                if (isChild && !showChildren)
                                    return;
                        
                                string name = client.Name;
                                string regionName = scene.RegionInfo.RegionName;
                                
                                report.AppendFormat(
                                    "{0,-" + maxNameLength + "}{1,-" + columnPadding + "}", 
                                    name.Length > maxNameLength ? name.Substring(0, maxNameLength) : name, "");
                                report.AppendFormat(
                                    "{0,-" + maxRegionNameLength + "}{1,-" + columnPadding + "}", 
                                    regionName.Length > maxRegionNameLength ? regionName.Substring(0, maxRegionNameLength) : regionName, "");
                                report.AppendFormat(
                                    "{0,-" + maxTypeLength + "}{1,-" + columnPadding + "}", 
                                    isChild ? "Cd" : "Rt", "");                                    

                                IStatsCollector stats = (IStatsCollector)client;
                        
                                report.AppendLine(stats.Report());
                            }
                        });
                }
            }

            return report.ToString();
        }        
    }
}