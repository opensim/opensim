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

namespace OpenSim.Region.OptionalModules.UDP.Linden
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
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);                
        
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
                "Comms", this, "show pqueues",
                "show pqueues [full]",
                "Show priority queue data for each client", 
                "Without the 'full' option, only root agents are shown."
                  + "  With the 'full' option child agents are also shown.",                                          
                (mod, cmd) => MainConsole.Instance.Output(GetPQueuesReport(cmd)));
            
            scene.AddCommand(
                "Comms", this, "show queues",
                "show queues [full]",
                "Show queue data for each client", 
                "Without the 'full' option, only root agents are shown.\n"
                    + "With the 'full' option child agents are also shown.\n\n"
                    + "Type          - Rt is a root (avatar) client whilst cd is a child (neighbour interacting) client.\n"
                    + "Since Last In - Time in milliseconds since last packet received.\n"
                    + "Pkts In       - Number of packets processed from the client.\n"
                    + "Pkts Out      - Number of packets sent to the client.\n"
                    + "Pkts Resent   - Number of packets resent to the client.\n"
                    + "Bytes Unacked - Number of bytes transferred to the client that are awaiting acknowledgement.\n"
                    + "Q Pkts *      - Number of packets of various types (land, wind, etc.) to be sent to the client that are waiting for available bandwidth.\n",
                (mod, cmd) => MainConsole.Instance.Output(GetQueuesReport(cmd)));

            scene.AddCommand(
                "Comms", this, "show image queues",
                "show image queues <first-name> <last-name>",
                "Show the image queues (textures downloaded via UDP) for a particular client.",
                (mod, cmd) => MainConsole.Instance.Output(GetImageQueuesReport(cmd)));

            scene.AddCommand(
                "Comms", this, "clear image queues",
                "clear image queues <first-name> <last-name>",
                "Clear the image queues (textures downloaded via UDP) for a particular client.",
                (mod, cmd) => MainConsole.Instance.Output(HandleImageQueuesClear(cmd)));
            
            scene.AddCommand(
                "Comms", this, "show throttles",
                "show throttles [full]",
                "Show throttle settings for each client and for the server overall", 
                "Without the 'full' option, only root agents are shown."
                  + "  With the 'full' option child agents are also shown.",                                          
                (mod, cmd) => MainConsole.Instance.Output(GetThrottlesReport(cmd)));

            scene.AddCommand(
                "Comms", this, "emergency-monitoring",
                "emergency-monitoring",
                "Go on/off emergency monitoring mode",
                "Go on/off emergency monitoring mode",
                HandleEmergencyMonitoring);

            scene.AddCommand(
                "Comms", this, "show client stats",
                "show client stats [first_name last_name]",
                "Show client request stats",
                "Without the 'first_name last_name' option, all clients are shown."
                  + "  With the 'first_name last_name' option only a specific client is shown.",
                (mod, cmd) => MainConsole.Instance.Output(HandleClientStatsReport(cmd)));

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

        protected string HandleImageQueuesClear(string[] cmd)
        {
            if (cmd.Length != 5)
                return "Usage: image queues clear <first-name> <last-name>";

            string firstName = cmd[3];
            string lastName = cmd[4];

            List<ScenePresence> foundAgents = new List<ScenePresence>();

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    ScenePresence sp = scene.GetScenePresence(firstName, lastName);
                    if (sp != null)
                        foundAgents.Add(sp);
                }
            }

            if (foundAgents.Count == 0)
                return string.Format("No agents found for {0} {1}", firstName, lastName);

            StringBuilder report = new StringBuilder();

            foreach (ScenePresence agent in foundAgents)
            {
                LLClientView client = agent.ControllingClient as LLClientView;
    
                if (client == null)
                    return "This command is only supported for LLClientView";

                int requestsDeleted = client.ImageManager.ClearImageQueue();

                report.AppendFormat(
                    "In region {0} ({1} agent) cleared {2} requests\n",
                    agent.Scene.RegionInfo.RegionName, agent.IsChildAgent ? "child" : "root", requestsDeleted);
            }

            return report.ToString();
        }

        protected void HandleEmergencyMonitoring(string module, string[] cmd)
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
//            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;                        
                                    
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
                                bool isChild = client.SceneAgent.IsChildAgent;
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
        /// Generate an image queue report
        /// </summary>
        /// <param name="showParams"></param>
        /// <returns></returns>
        private string GetImageQueuesReport(string[] showParams)
        {
            if (showParams.Length < 5 || showParams.Length > 6)
                return "Usage: image queues show <first-name> <last-name> [full]";

            string firstName = showParams[3];
            string lastName = showParams[4];

            bool showChildAgents = showParams.Length == 6;

            List<ScenePresence> foundAgents = new List<ScenePresence>();

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    ScenePresence sp = scene.GetScenePresence(firstName, lastName);
                    if (sp != null && (showChildAgents || !sp.IsChildAgent))
                        foundAgents.Add(sp);
                }
            }

            if (foundAgents.Count == 0)
                return string.Format("No agents found for {0} {1}", firstName, lastName);

            StringBuilder report = new StringBuilder();

            foreach (ScenePresence agent in foundAgents)
            {
                LLClientView client = agent.ControllingClient as LLClientView;
    
                if (client == null)
                    return "This command is only supported for LLClientView";
    
                J2KImage[] images = client.ImageManager.GetImages();

                report.AppendFormat(
                    "In region {0} ({1} agent)\n",
                    agent.Scene.RegionInfo.RegionName, agent.IsChildAgent ? "child" : "root");
                report.AppendFormat("Images in queue: {0}\n", images.Length);
    
                if (images.Length > 0)
                {
                    report.AppendFormat(
                    "{0,-36}  {1,-8}  {2,-10}  {3,-9}  {4,-9}  {5,-7}\n",
                    "Texture ID",
                    "Last Seq",
                    "Priority",
                    "Start Pkt",
                    "Has Asset",
                    "Decoded");
    
                    foreach (J2KImage image in images)
                        report.AppendFormat(
                            "{0,36}  {1,8}  {2,10}  {3,10}  {4,9}  {5,7}\n",
                            image.TextureID, image.LastSequence, image.Priority, image.StartPacket, image.HasAsset, image.IsDecoded);
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

            int totalInfoFieldsLength
                = maxNameLength + columnPadding
                + maxRegionNameLength + columnPadding
                + maxTypeLength + columnPadding;
                                    
            report.Append(GetColumnEntry("User", maxNameLength, columnPadding));
            report.Append(GetColumnEntry("Region", maxRegionNameLength, columnPadding));
            report.Append(GetColumnEntry("Type", maxTypeLength, columnPadding));
            
            report.AppendFormat(
                "{0,7} {1,7} {2,7} {3,7} {4,9} {5,7} {6,7} {7,7} {8,7} {9,7} {10,8} {11,7} {12,7}\n",
                "Since",
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
                "{0,7} {1,7} {2,7} {3,7} {4,9} {5,7} {6,7} {7,7} {8,7} {9,7} {10,8} {11,7} {12,7}\n",
                "Last In",
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
                            bool isChild = client.SceneAgent.IsChildAgent;
                            if (isChild && !showChildren)
                                return;
                    
                            string name = client.Name;
                            if (pname != "" && name != pname)
                                return;

                            string regionName = scene.RegionInfo.RegionName;

                            report.Append(GetColumnEntry(name, maxNameLength, columnPadding));
                            report.Append(GetColumnEntry(regionName, maxRegionNameLength, columnPadding));
                            report.Append(GetColumnEntry(isChild ? "Cd" : "Rt", maxTypeLength, columnPadding));

                            if (client is IStatsCollector)
                            {
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

                                bool isChild = client.SceneAgent.IsChildAgent;
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

        /// <summary>
        /// Show client stats data
        /// </summary>
        /// <param name="showParams"></param>
        /// <returns></returns>
        protected string HandleClientStatsReport(string[] showParams)
        {
            // NOTE: This writes to m_log on purpose. We want to store this information
            // in case we need to analyze it later.
            //
            if (showParams.Length <= 4)
            {
                m_log.InfoFormat("[INFO]: {0,-12} {1,20} {2,6} {3,11} {4, 10}", "Region", "Name", "Root", "Time", "Reqs/min");
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is LLClientView)
                            {
                                LLClientView llClient = client as LLClientView;
                                ClientInfo cinfo = llClient.UDPClient.GetClientInfo();
                                int avg_reqs = cinfo.AsyncRequests.Values.Sum() + cinfo.GenericRequests.Values.Sum() + cinfo.SyncRequests.Values.Sum();
                                avg_reqs = avg_reqs / ((DateTime.Now - cinfo.StartedTime).Minutes + 1);

                                m_log.InfoFormat("[INFO]: {0,-12} {1,20} {2,4} {3,9}min {4,10}", 
                                    scene.RegionInfo.RegionName, llClient.Name,
                                    (llClient.SceneAgent.IsChildAgent ? "N" : "Y"), (DateTime.Now - cinfo.StartedTime).Minutes, avg_reqs);
                            }
                        });
                }
                return string.Empty;
            }

            string fname = "", lname = "";

            if (showParams.Length > 3)
                fname = showParams[3];
            if (showParams.Length > 4)
                lname = showParams[4];

            foreach (Scene scene in m_scenes.Values)
            {
                scene.ForEachClient(
                    delegate(IClientAPI client)
                    {
                        if (client is LLClientView)
                        {
                            LLClientView llClient = client as LLClientView;

                            if (llClient.Name == fname + " " + lname)
                            {

                                ClientInfo cinfo = llClient.GetClientInfo();
                                AgentCircuitData aCircuit = scene.AuthenticateHandler.GetAgentCircuitData(llClient.CircuitCode);
                                if (aCircuit == null) // create a dummy one
                                    aCircuit = new AgentCircuitData();

                                if (!llClient.SceneAgent.IsChildAgent)
                                    m_log.InfoFormat("[INFO]: {0} # {1} # {2}", llClient.Name, aCircuit.Viewer, aCircuit.Id0);

                                int avg_reqs = cinfo.AsyncRequests.Values.Sum() + cinfo.GenericRequests.Values.Sum() + cinfo.SyncRequests.Values.Sum();
                                avg_reqs = avg_reqs / ((DateTime.Now - cinfo.StartedTime).Minutes + 1);

                                m_log.InfoFormat("[INFO]:");
                                m_log.InfoFormat("[INFO]: {0} # {1} # Time: {2}min # Avg Reqs/min: {3}", scene.RegionInfo.RegionName,
                                    (llClient.SceneAgent.IsChildAgent ? "Child" : "Root"), (DateTime.Now - cinfo.StartedTime).Minutes, avg_reqs);

                                Dictionary<string, int> sortedDict = (from entry in cinfo.AsyncRequests orderby entry.Value descending select entry)
                                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                                PrintRequests("TOP ASYNC", sortedDict, cinfo.AsyncRequests.Values.Sum());

                                sortedDict = (from entry in cinfo.SyncRequests orderby entry.Value descending select entry)
                                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                                PrintRequests("TOP SYNC", sortedDict, cinfo.SyncRequests.Values.Sum());

                                sortedDict = (from entry in cinfo.GenericRequests orderby entry.Value descending select entry)
                                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                                PrintRequests("TOP GENERIC", sortedDict, cinfo.GenericRequests.Values.Sum());
                            }
                        }
                    });
            }
            return string.Empty;
        }

        private void PrintRequests(string type, Dictionary<string, int> sortedDict, int sum)
        {
            m_log.InfoFormat("[INFO]:");
            m_log.InfoFormat("[INFO]: {0,25}", type);
            foreach (KeyValuePair<string, int> kvp in sortedDict.Take(12))
                m_log.InfoFormat("[INFO]: {0,25} {1,-6}", kvp.Key, kvp.Value);
            m_log.InfoFormat("[INFO]: {0,25}", "...");
            m_log.InfoFormat("[INFO]: {0,25} {1,-6}", "Total", sum);
        }
    }
}