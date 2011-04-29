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
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.UDP.Linden
{
    /// <summary>
    /// A module that just holds commands for inspecting the current state of the Linden UDP stack.
    /// </summary>
    /// <remarks>
    /// All actual client stack functionality remains in OpenSim.Region.ClientStack.LindenUDP
    /// </remarks>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LindenUDPInfoModule")]
    public class LindenUDPInfoModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);                
        
        protected Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
        
        public string Name { get { return "Linden UDP Module"; } }        
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[LINDEN UDP INFO MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[LINDEN UDP INFO MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[LINDEN UDP INFO MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[LINDEN UDP INFO MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes[scene.RegionInfo.RegionID] = scene;            

            scene.AddCommand(
                this, "show pqueues",
                "show pqueues [full]",
                "Show priority queue data for each client", 
                "Without the 'full' option, only root agents are shown."
                  + "  With the 'full' option child agents are also shown.",                                          
                ShowPQueuesReport);   
            
            scene.AddCommand(
                this, "show queues",
                "show queues [full]",
                "Show queue data for each client", 
                "Without the 'full' option, only root agents are shown."
                  + "  With the 'full' option child agents are also shown.",                                          
                ShowQueuesReport);   
            
            scene.AddCommand(
                this, "show throttles",
                "show throttles [full]",
                "Show throttle settings for each client and for the server overall", 
                "Without the 'full' option, only root agents are shown."
                  + "  With the 'full' option child agents are also shown.",                                          
                ShowThrottlesReport);

            scene.AddCommand(
                this, "emergency-monitoring",
                "Go on/off emergency monitoring mode",
                "Go on/off emergency monitoring mode",
                "Go on/off emergency monitoring mode",
                EmergencyMonitoring);                             

        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[LINDEN UDP INFO MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
            
            lock (m_scenes)
                m_scenes.Remove(scene.RegionInfo.RegionID);
        }        
        
        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[LINDEN UDP INFO MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }                 

        protected void ShowPQueuesReport(string module, string[] cmd)
        {                       
            MainConsole.Instance.Output(GetPQueuesReport(cmd));
        }
        
        protected void ShowQueuesReport(string module, string[] cmd)
        {                       
            MainConsole.Instance.Output(GetQueuesReport(cmd));
        }
        
        protected void ShowThrottlesReport(string module, string[] cmd)
        {
            MainConsole.Instance.Output(GetThrottlesReport(cmd));
        }

        protected void EmergencyMonitoring(string module, string[] cmd)
        {
            bool mode = true;
            if (cmd.Length == 1 || (cmd.Length > 1 && cmd[1] == "on"))
            {
                mode = true;
                MainConsole.Instance.Output("Emergency Monitoring ON");
            }
            else
            {
                mode = false;
                MainConsole.Instance.Output("Emergency Monitoring OFF");
            }

            foreach (Scene s in m_scenes.Values)
                s.EmergencyMonitoring = mode;
        }

        protected string GetColumnEntry(string entry, int maxLength, int columnPadding)
        {                       
            return string.Format(
                "{0,-" + maxLength +  "}{1,-" + columnPadding + "}", 
                entry.Length > maxLength ? entry.Substring(0, maxLength) : entry, 
                "");
        }
        

        /// <summary>
        /// Generate UDP Queue data report for each client
        /// </summary>
        /// <param name="showParams"></param>
        /// <returns></returns>
        protected string GetPQueuesReport(string[] showParams)
        {
            bool showChildren = false;
            string pname = "";
            
            if (showParams.Length > 2 && showParams[2] == "full")
                showChildren = true;               
            else if (showParams.Length > 3)
                pname = showParams[2] + " " + showParams[3];
            
            StringBuilder report = new StringBuilder();            

            int columnPadding = 2;
            int maxNameLength = 18;                                    
            int maxRegionNameLength = 14;
            int maxTypeLength = 4;
            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;                        
                                    
            report.Append(GetColumnEntry("User", maxNameLength, columnPadding));
            report.Append(GetColumnEntry("Region", maxRegionNameLength, columnPadding));
            report.Append(GetColumnEntry("Type", maxTypeLength, columnPadding));
            
            report.AppendFormat(
                "{0,7} {1,7} {2,7} {3,7} {4,7} {5,7} {6,7} {7,7} {8,7} {9,7} {10,7} {11,7}\n",
                "Pri 0",
                "Pri 1",
                "Pri 2",                                
                "Pri 3",
                "Pri 4",
                "Pri 5",
                "Pri 6",
                "Pri 7",
                "Pri 8",
                "Pri 9",
                "Pri 10",
                "Pri 11");

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is LLClientView)
                            {
                                bool isChild = scene.PresenceChildStatus(client.AgentId);
                                if (isChild && !showChildren)
                                    return;
                        
                                string name = client.Name;
                                if (pname != "" && name != pname)
                                    return;
                                
                                string regionName = scene.RegionInfo.RegionName;
                                
                                report.Append(GetColumnEntry(name, maxNameLength, columnPadding));
                                report.Append(GetColumnEntry(regionName, maxRegionNameLength, columnPadding));
                                report.Append(GetColumnEntry(isChild ? "Cd" : "Rt", maxTypeLength, columnPadding));                                  
                                report.AppendLine(((LLClientView)client).EntityUpdateQueue.ToString());
                            }
                        });
                }
            }

            return report.ToString();
        }
        
        /// <summary>
        /// Generate UDP Queue data report for each client
        /// </summary>
        /// <param name="showParams"></param>
        /// <returns></returns>
        protected string GetQueuesReport(string[] showParams)
        {
            bool showChildren = false;
            string pname = "";
            
            if (showParams.Length > 2 && showParams[2] == "full")
                showChildren = true;               
            else if (showParams.Length > 3)
                pname = showParams[2] + " " + showParams[3];
            
            StringBuilder report = new StringBuilder();            
            
            int columnPadding = 2;
            int maxNameLength = 18;                                    
            int maxRegionNameLength = 14;
            int maxTypeLength = 4;
            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;                        
                                    
            report.Append(GetColumnEntry("User", maxNameLength, columnPadding));
            report.Append(GetColumnEntry("Region", maxRegionNameLength, columnPadding));
            report.Append(GetColumnEntry("Type", maxTypeLength, columnPadding));
            
            report.AppendFormat(
                "{0,7} {1,7} {2,7} {3,9} {4,7} {5,7} {6,7} {7,7} {8,7} {9,8} {10,7} {11,7}\n",
                "Pkts",
                "Pkts",
                "Pkts",                                
                "Bytes",
                "Q Pkts",
                "Q Pkts",
                "Q Pkts",
                "Q Pkts",
                "Q Pkts",
                "Q Pkts",
                "Q Pkts",
                "Q Pkts");
    
            report.AppendFormat("{0,-" + totalInfoFieldsLength +  "}", "");
            report.AppendFormat(
                "{0,7} {1,7} {2,7} {3,9} {4,7} {5,7} {6,7} {7,7} {8,7} {9,8} {10,7} {11,7}\n",
                "In",
                "Out",
                "Resent",
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
                                if (pname != "" && name != pname)
                                    return;

                                string regionName = scene.RegionInfo.RegionName;
                                
                                report.Append(GetColumnEntry(name, maxNameLength, columnPadding));
                                report.Append(GetColumnEntry(regionName, maxRegionNameLength, columnPadding));
                                report.Append(GetColumnEntry(isChild ? "Cd" : "Rt", maxTypeLength, columnPadding));                                  

                                IStatsCollector stats = (IStatsCollector)client;
                        
                                report.AppendLine(stats.Report());
                            }
                        });
                }
            }

            return report.ToString();
        }  
        
        /// <summary>
        /// Show throttle data
        /// </summary>
        /// <param name="showParams"></param>
        /// <returns></returns>
        protected string GetThrottlesReport(string[] showParams)
        {
            bool showChildren = false;
            string pname = "";
            
            if (showParams.Length > 2 && showParams[2] == "full")
                showChildren = true;               
            else if (showParams.Length > 3)
                pname = showParams[2] + " " + showParams[3];
            
            StringBuilder report = new StringBuilder();               
            
            int columnPadding = 2;
            int maxNameLength = 18;                                    
            int maxRegionNameLength = 14;
            int maxTypeLength = 4;     
            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;                        
            
            report.Append(GetColumnEntry("User", maxNameLength, columnPadding));
            report.Append(GetColumnEntry("Region", maxRegionNameLength, columnPadding));
            report.Append(GetColumnEntry("Type", maxTypeLength, columnPadding));            
            
            report.AppendFormat(
                "{0,7} {1,8} {2,7} {3,7} {4,7} {5,7} {6,9} {7,7}\n",
                "Total",
                "Resend",
                "Land",
                "Wind",
                "Cloud",
                "Task",
                "Texture",
                "Asset");          
    
            report.AppendFormat("{0,-" + totalInfoFieldsLength +  "}", "");
            report.AppendFormat(
                "{0,7} {1,8} {2,7} {3,7} {4,7} {5,7} {6,9} {7,7}",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s");                 
            
            report.AppendLine();
            
            bool firstClient = true;
            
            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is LLClientView)
                            {
                                LLClientView llClient = client as LLClientView;

                                if (firstClient)
                                {
                                    report.AppendLine(GetServerThrottlesReport(llClient.UDPServer));
                                    firstClient = false;
                                }

                                bool isChild = scene.PresenceChildStatus(client.AgentId);
                                if (isChild && !showChildren)
                                    return;
                        
                                string name = client.Name;
                                if (pname != "" && name != pname)
                                    return;

                                string regionName = scene.RegionInfo.RegionName;
                            
                                LLUDPClient llUdpClient = llClient.UDPClient;
                                ClientInfo ci = llUdpClient.GetClientInfo();
                            
                                report.Append(GetColumnEntry(name, maxNameLength, columnPadding));
                                report.Append(GetColumnEntry(regionName, maxRegionNameLength, columnPadding));
                                report.Append(GetColumnEntry(isChild ? "Cd" : "Rt", maxTypeLength, columnPadding));                                                             
                            
                                report.AppendFormat(
                                    "{0,7} {1,8} {2,7} {3,7} {4,7} {5,7} {6,9} {7,7}",
                                    (ci.totalThrottle * 8) / 1000,
                                    (ci.resendThrottle * 8) / 1000,
                                    (ci.landThrottle * 8) / 1000,
                                    (ci.windThrottle * 8) / 1000,
                                    (ci.cloudThrottle * 8) / 1000,
                                    (ci.taskThrottle * 8) / 1000,
                                    (ci.textureThrottle  * 8) / 1000,
                                    (ci.assetThrottle  * 8) / 1000);                                                                                      
                        
                                report.AppendLine();
                            }
                        });
                }
            }

            return report.ToString();
        }         
                
        protected string GetServerThrottlesReport(LLUDPServer udpServer)
        {
            StringBuilder report = new StringBuilder();
            
            int columnPadding = 2;
            int maxNameLength = 18;                                    
            int maxRegionNameLength = 14;
            int maxTypeLength = 4;
            
            string name = "SERVER AGENT RATES";
                                
            report.Append(GetColumnEntry(name, maxNameLength, columnPadding));
            report.Append(GetColumnEntry("-", maxRegionNameLength, columnPadding));
            report.Append(GetColumnEntry("-", maxTypeLength, columnPadding));             
            
            ThrottleRates throttleRates = udpServer.ThrottleRates;
            report.AppendFormat(
                "{0,7} {1,8} {2,7} {3,7} {4,7} {5,7} {6,9} {7,7}",
                (throttleRates.Total * 8) / 1000,
                (throttleRates.Resend * 8) / 1000,
                (throttleRates.Land * 8) / 1000,
                (throttleRates.Wind * 8) / 1000,
                (throttleRates.Cloud * 8) / 1000,
                (throttleRates.Task * 8) / 1000,
                (throttleRates.Texture  * 8) / 1000,
                (throttleRates.Asset  * 8) / 1000);  

            return report.ToString();
        }        
    }
}