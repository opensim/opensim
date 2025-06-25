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

        protected Dictionary<UUID, Scene> m_scenes = new();

        public string Name { get { return "Linden UDP Module"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            //m_log.DebugFormat("[LINDEN UDP INFO MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);

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
        }

        public void RemoveRegion(Scene scene)
        {
            //m_log.DebugFormat("[LINDEN UDP INFO MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);

            lock (m_scenes)
                m_scenes.Remove(scene.RegionInfo.RegionID);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        protected string HandleImageQueuesClear(string[] cmd)
        {
            if (cmd.Length != 5)
                return "Usage: image queues clear <first-name> <last-name>";

            string firstName = cmd[3];
            string lastName = cmd[4];

            List<ScenePresence> foundAgents = new();

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    ScenePresence sp = scene.GetScenePresence(firstName, lastName);
                    if (sp is not null)
                        foundAgents.Add(sp);
                }
            }

            if (foundAgents.Count == 0)
                return string.Format("No agents found for {0} {1}", firstName, lastName);

            StringBuilder report = new();

            foreach (ScenePresence agent in foundAgents)
            {
                if (agent.ControllingClient is not LLClientView client)
                    return "This command is only supported for LLClientView";

                int requestsDeleted = client.ImageManager.ClearImageQueue();

                report.AppendFormat(
                    "In region {0} ({1} agent) cleared {2} requests\n",
                    agent.Scene.RegionInfo.RegionName, agent.IsChildAgent ? "child" : "root", requestsDeleted);
            }

            return report.ToString();
        }

        protected string GetColumnEntry(string entry, int maxLength, int columnPadding)
        {
            return string.Format(
                "{0,-" + maxLength +  "}{1,-" + columnPadding + "}",
                entry.Length > maxLength ? entry[..maxLength] : entry,
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

            StringBuilder report = new();

            const int columnPadding = 2;
            const int maxNameLength = 18;
            const int maxRegionNameLength = 14;
            const int maxTypeLength = 4;
            //int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;

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
                            if (client is LLClientView llclient)
                            {
                                bool isChild = client.SceneAgent.IsChildAgent;
                                if (isChild && !showChildren)
                                    return;

                                if (pname != "" && client.Name != pname)
                                    return;

                                string regionName = scene.RegionInfo.RegionName;

                                report.Append(GetColumnEntry(client.Name, maxNameLength, columnPadding));
                                report.Append(GetColumnEntry(regionName, maxRegionNameLength, columnPadding));
                                report.Append(GetColumnEntry(isChild ? "Cd" : "Rt", maxTypeLength, columnPadding));
                                report.AppendLine(llclient.EntityUpdateQueue.ToString());
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
                return "Usage: show image queues <first-name> <last-name> [full]";

            string firstName = showParams[3];
            string lastName = showParams[4];

            bool showChildAgents = showParams.Length == 6;

            List<ScenePresence> foundAgents = new();

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    ScenePresence sp = scene.GetScenePresence(firstName, lastName);
                    if (sp is not null && (showChildAgents || !sp.IsChildAgent))
                        foundAgents.Add(sp);
                }
            }

            if (foundAgents.Count == 0)
                return string.Format("No agents found for {0} {1}", firstName, lastName);

            StringBuilder report = new();

            foreach (ScenePresence agent in foundAgents)
            {
                if (agent.ControllingClient is not LLClientView client)
                    return "This command is only supported for LLClientView";

                J2KImage[] images = client.ImageManager.GetImages();

                report.AppendFormat("In region {0} ({1} agent)\n",
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

            StringBuilder report = new();

            const int columnPadding = 2;
            const int maxNameLength = 18;
            const int maxRegionNameLength = 14;
            const int maxTypeLength = 4;

            int totalInfoFieldsLength = maxNameLength + columnPadding
                    + maxRegionNameLength + columnPadding
                    + maxTypeLength + columnPadding;

            report.Append(GetColumnEntry("User", maxNameLength, columnPadding));
            report.Append(GetColumnEntry("Region", maxRegionNameLength, columnPadding));
            report.Append(GetColumnEntry("Type", maxTypeLength, columnPadding));

            report.AppendFormat(
                "{0,7} {1,7} {2,7} {3,7} {4,9} {5,7} {6,7} {7,7} {8,7} {9,7} {10,8} {11,7}\n",
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
                "Q Pkts");

            report.AppendFormat("{0,-" + totalInfoFieldsLength +  "}", "");
            report.AppendFormat(
                "{0,7} {1,7} {2,7} {3,7} {4,9} {5,7} {6,7} {7,7} {8,7} {9,7} {10,8} {11,7}\n",
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
                "Asset");

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is IStatsCollector collector)
                            {
                                bool isChild = client.SceneAgent.IsChildAgent;
                                if (isChild && !showChildren)
                                    return;

                                if (pname != "" && client.Name != pname)
                                    return;

                                report.Append(GetColumnEntry(client.Name, maxNameLength, columnPadding));
                                report.Append(GetColumnEntry(scene.RegionInfo.RegionName, maxRegionNameLength, columnPadding));
                                report.Append(GetColumnEntry(isChild ? "Cd" : "Rt", maxTypeLength, columnPadding));

                                IStatsCollector stats = collector;
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

            StringBuilder report = new();

            const int columnPadding = 2;
            const int maxNameLength = 18;
            const int maxRegionNameLength = 14;
            const int maxTypeLength = 4;
            int totalInfoFieldsLength = maxNameLength + columnPadding + maxRegionNameLength + columnPadding + maxTypeLength + columnPadding;

            report.Append(GetColumnEntry("User", maxNameLength, columnPadding));
            report.Append(GetColumnEntry("Region", maxRegionNameLength, columnPadding));
            report.Append(GetColumnEntry("Type", maxTypeLength, columnPadding));

            report.AppendFormat(
                "{0,8} {1,8} {2,7} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7}\n",
                "Max",
                "Target",
                "Actual",
                "Resend",
                "Land",
                "Wind",
                "Cloud",
                "Task",
                "Texture",
                "Asset");

            report.AppendFormat("{0,-" + totalInfoFieldsLength +  "}", "");
            report.AppendFormat(
                "{0,8} {1,8} {2,7} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7}\n",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s",
                "kb/s");

            report.AppendLine();

            lock (m_scenes)
            {
                foreach (Scene scene in m_scenes.Values)
                {
                    scene.ForEachClient(
                        delegate(IClientAPI client)
                        {
                            if (client is LLClientView llClient)
                            {
                                bool isChild = client.SceneAgent.IsChildAgent;
                                if (isChild && !showChildren)
                                    return;

                                if (pname != "" && client.Name != pname)
                                    return;

                                LLUDPClient llUdpClient = llClient.UDPClient;
                                ClientInfo ci = llUdpClient.GetClientInfo();

                                report.Append(GetColumnEntry(client.Name, maxNameLength, columnPadding));
                                report.Append(GetColumnEntry(scene.RegionInfo.RegionName, maxRegionNameLength, columnPadding));
                                report.Append(GetColumnEntry(isChild ? "Cd" : "Rt", maxTypeLength, columnPadding));

                                report.AppendFormat(
                                    "{0,8} {1,8} {2,7} {3,8} {4,7} {5,7} {6,7} {7,7} {8,9} {9,7}\n",
                                    ci.maxThrottle > 0 ? ((ci.maxThrottle * 8) / 1000).ToString() : "-",
                                    llUdpClient.FlowThrottle.AdaptiveEnabled
                                        ? ((ci.targetThrottle * 8) / 1000).ToString()
                                        : (llUdpClient.FlowThrottle.TotalDripRequest * 8 / 1000).ToString(),
                                    (ci.totalThrottle * 8) / 1000,
                                    (ci.resendThrottle * 8) / 1000,
                                    (ci.landThrottle * 8) / 1000,
                                    (ci.windThrottle * 8) / 1000,
                                    (ci.cloudThrottle * 8) / 1000,
                                    (ci.taskThrottle * 8) / 1000,
                                    (ci.textureThrottle  * 8) / 1000,
                                    (ci.assetThrottle  * 8) / 1000);
                            }
                        });
                }
            }

            return report.ToString();
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
