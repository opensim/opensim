/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Framework
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "CapabilitiesModule")]
    public class CapabilitiesModule : INonSharedRegionModule, ICapabilitiesModule
    { 
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_showCapsCommandFormat = "   {0,-38} {1,-60}\n";
        
        protected Scene m_scene;
        
        /// <summary>
        /// Each agent has its own capabilities handler.
        /// </summary>
        protected Dictionary<uint, Caps> m_capsObjects = new Dictionary<uint, Caps>();
        
        protected Dictionary<UUID, string> m_capsPaths = new Dictionary<UUID, string>();

        protected Dictionary<UUID, Dictionary<ulong, string>> m_childrenSeeds 
            = new Dictionary<UUID, Dictionary<ulong, string>>();
        
        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<ICapabilitiesModule>(this);

            MainConsole.Instance.Commands.AddCommand(
                "Comms", false, "show caps list",
                "show caps list",
                "Shows list of registered capabilities for users.", HandleShowCapsListCommand);

            MainConsole.Instance.Commands.AddCommand(
                "Comms", false, "show caps stats by user",
                "show caps stats by user [<first-name> <last-name>]",
                "Shows statistics on capabilities use by user.",
                "If a user name is given, then prints a detailed breakdown of caps use ordered by number of requests received.",
                HandleShowCapsStatsByUserCommand);

            MainConsole.Instance.Commands.AddCommand(
                "Comms", false, "show caps stats by cap",
                "show caps stats by cap [<cap-name>]",
                "Shows statistics on capabilities use by capability.",
                "If a capability name is given, then prints a detailed breakdown of use by each user.",
                HandleShowCapsStatsByCapCommand);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.UnregisterModuleInterface<ICapabilitiesModule>(this);
        }
        
        public void PostInitialise() 
        {
        }

        public void Close() {}

        public string Name 
        { 
            get { return "Capabilities Module"; } 
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void CreateCaps(UUID agentId, uint circuitCode)
        {
            int ts = Util.EnvironmentTickCount();
/*  this as no business here...
 * must be done elsewhere ( and is )
            int flags = m_scene.GetUserFlags(agentId);

            m_log.ErrorFormat("[CreateCaps]: banCheck {0} ", Util.EnvironmentTickCountSubtract(ts));

            if (m_scene.RegionInfo.EstateSettings.IsBanned(agentId, flags))
                return;
*/
            Caps caps;
            String capsObjectPath = GetCapsPath(agentId);

            lock (m_capsObjects)
            {
                if (m_capsObjects.ContainsKey(circuitCode))
                {
                    Caps oldCaps = m_capsObjects[circuitCode];


                    if (capsObjectPath == oldCaps.CapsObjectPath)
                    {
                        m_log.WarnFormat(
                           "[CAPS]: Reusing caps for agent {0} in region {1}.  Old caps path {2}, new caps path {3}. ",
                            agentId, m_scene.RegionInfo.RegionName, oldCaps.CapsObjectPath, capsObjectPath);
                        return;
                    }
                    else
                    {
                        // not reusing  add extra melanie cleanup
                        // Remove tge handlers. They may conflict with the
                        // new object created below
                        oldCaps.DeregisterHandlers();

                        // Better safe ... should not be needed but also 
                        // no big deal
                        m_capsObjects.Remove(circuitCode);
                    }
                }

//                m_log.DebugFormat(
//                    "[CAPS]: Adding capabilities for agent {0} in {1} with path {2}",
//                    agentId, m_scene.RegionInfo.RegionName, capsObjectPath);

                caps = new Caps(MainServer.Instance, m_scene.RegionInfo.ExternalHostName,
                        (MainServer.Instance == null) ? 0: MainServer.Instance.Port,
                        capsObjectPath, agentId, m_scene.RegionInfo.RegionName);

                m_log.DebugFormat("[CreateCaps]: new caps agent {0}, circuit {1}, path {2}, time {3} ",agentId,
                    circuitCode,caps.CapsObjectPath, Util.EnvironmentTickCountSubtract(ts));

                m_capsObjects[circuitCode] = caps;
            }
            m_scene.EventManager.TriggerOnRegisterCaps(agentId, caps);
//            m_log.ErrorFormat("[CreateCaps]: end {0} ", Util.EnvironmentTickCountSubtract(ts));

        }

        public void RemoveCaps(UUID agentId, uint circuitCode)
        {
            m_log.DebugFormat("[CAPS]: Remove caps for agent {0} in region {1}", agentId, m_scene.RegionInfo.RegionName);
            lock (m_childrenSeeds)
            {
                if (m_childrenSeeds.ContainsKey(agentId))
                {
                    m_childrenSeeds.Remove(agentId);
                }
            }

            lock (m_capsObjects)
            {
                if (m_capsObjects.ContainsKey(circuitCode))
                {
                    m_capsObjects[circuitCode].DeregisterHandlers();
                    m_scene.EventManager.TriggerOnDeregisterCaps(agentId, m_capsObjects[circuitCode]);
                    m_capsObjects.Remove(circuitCode);
                }
                else
                {
                    foreach (KeyValuePair<uint, Caps> kvp in m_capsObjects)
                    {
                        if (kvp.Value.AgentID == agentId)
                        {
                            kvp.Value.DeregisterHandlers();
                            m_scene.EventManager.TriggerOnDeregisterCaps(agentId, kvp.Value);
                            m_capsObjects.Remove(kvp.Key);
                            return;
                        }
                    }
                    m_log.WarnFormat(
                        "[CAPS]: Received request to remove CAPS handler for root agent {0} in {1}, but no such CAPS handler found!",
                        agentId, m_scene.RegionInfo.RegionName);
                }
            }
        }
        
        public Caps GetCapsForUser(uint circuitCode)
        {
            lock (m_capsObjects)
            {
                if (m_capsObjects.ContainsKey(circuitCode))
                {
                    return m_capsObjects[circuitCode];
                }
            }
            
            return null;
        }
        
        public void ActivateCaps(uint circuitCode)
        {
            lock (m_capsObjects)
            {
                if (m_capsObjects.ContainsKey(circuitCode))
                {
                    m_capsObjects[circuitCode].Activate();
                }
            }
        }

        public void SetAgentCapsSeeds(AgentCircuitData agent)
        {
            lock (m_capsPaths)
                m_capsPaths[agent.AgentID] = agent.CapsPath;

            lock (m_childrenSeeds)
                m_childrenSeeds[agent.AgentID] 
                    = ((agent.ChildrenCapSeeds == null) ? new Dictionary<ulong, string>() : agent.ChildrenCapSeeds);
        }
        
        public string GetCapsPath(UUID agentId)
        {
            lock (m_capsPaths)
            {
                if (m_capsPaths.ContainsKey(agentId))
                {
                    return m_capsPaths[agentId];
                }
            }

            return null;
        }
        
        public Dictionary<ulong, string> GetChildrenSeeds(UUID agentID)
        {
            Dictionary<ulong, string> seeds = null;

            lock (m_childrenSeeds)
                if (m_childrenSeeds.TryGetValue(agentID, out seeds))
                    return seeds;

            return new Dictionary<ulong, string>();
        }

        public void DropChildSeed(UUID agentID, ulong handle)
        {
            Dictionary<ulong, string> seeds;

            lock (m_childrenSeeds)
            {
                if (m_childrenSeeds.TryGetValue(agentID, out seeds))
                {
                    seeds.Remove(handle);
                }
            }
        }

        public string GetChildSeed(UUID agentID, ulong handle)
        {
            Dictionary<ulong, string> seeds;
            string returnval;

            lock (m_childrenSeeds)
            {
                if (m_childrenSeeds.TryGetValue(agentID, out seeds))
                {
                    if (seeds.TryGetValue(handle, out returnval))
                        return returnval;
                }
            }

            return null;
        }

        public void SetChildrenSeed(UUID agentID, Dictionary<ulong, string> seeds)
        {
            //m_log.DebugFormat(" !!! Setting child seeds in {0} to {1}", m_scene.RegionInfo.RegionName, seeds.Count);

            lock (m_childrenSeeds)
                m_childrenSeeds[agentID] = seeds;
        }

        public void DumpChildrenSeeds(UUID agentID)
        {
            m_log.Info("================ ChildrenSeed "+m_scene.RegionInfo.RegionName+" ================");

            lock (m_childrenSeeds)
            {
                foreach (KeyValuePair<ulong, string> kvp in m_childrenSeeds[agentID])
                {
                    uint x, y;
                    Util.RegionHandleToRegionLoc(kvp.Key, out x, out y);
                    m_log.Info(" >> "+x+", "+y+": "+kvp.Value);
                }
            }
        }

        private void HandleShowCapsListCommand(string module, string[] cmdParams)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_scene)
                return;

            StringBuilder capsReport = new StringBuilder();
            capsReport.AppendFormat("Region {0}:\n", m_scene.RegionInfo.RegionName);

            lock (m_capsObjects)
            {
                foreach (KeyValuePair<uint, Caps> kvp in m_capsObjects)
                {
                    capsReport.AppendFormat("** Circuit {0}:\n", kvp.Key);
                    Caps caps = kvp.Value;

                    for (IDictionaryEnumerator kvp2 = caps.CapsHandlers.GetCapsDetails(false, null).GetEnumerator(); kvp2.MoveNext(); )
                    {
                        Uri uri = new Uri(kvp2.Value.ToString());
                        capsReport.AppendFormat(m_showCapsCommandFormat, kvp2.Key, uri.PathAndQuery);
                    }

                    foreach (KeyValuePair<string, PollServiceEventArgs> kvp2 in caps.GetPollHandlers())
                        capsReport.AppendFormat(m_showCapsCommandFormat, kvp2.Key, kvp2.Value.Url);

                    foreach (KeyValuePair<string, string> kvp3 in caps.ExternalCapsHandlers)
                        capsReport.AppendFormat(m_showCapsCommandFormat, kvp3.Key, kvp3.Value);
                }
            }

            MainConsole.Instance.Output(capsReport.ToString());
        }

        private void HandleShowCapsStatsByCapCommand(string module, string[] cmdParams)
        {
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_scene)
                return;

            if (cmdParams.Length != 5 && cmdParams.Length != 6)
            {
                MainConsole.Instance.Output("Usage: show caps stats by cap [<cap-name>]");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Region {0}:\n", m_scene.Name);

            if (cmdParams.Length == 5)
            {
                BuildSummaryStatsByCapReport(sb);
            }
            else if (cmdParams.Length == 6)
            {
                BuildDetailedStatsByCapReport(sb, cmdParams[5]);
            }

            MainConsole.Instance.Output(sb.ToString());
        }

        private void BuildDetailedStatsByCapReport(StringBuilder sb, string capName)
        {
            /*
            sb.AppendFormat("Capability name {0}\n", capName);

            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("User Name", 34);
            cdt.AddColumn("Req Received", 12);
            cdt.AddColumn("Req Handled", 12);
            cdt.Indent = 2;

            Dictionary<string, int> receivedStats = new Dictionary<string, int>();
            Dictionary<string, int> handledStats = new Dictionary<string, int>();

            m_scene.ForEachScenePresence(
                sp =>
                {
                    Caps caps = m_scene.CapsModule.GetCapsForUser(sp.UUID);

                    if (caps == null)
                        return;

                    Dictionary<string, IRequestHandler> capsHandlers = caps.CapsHandlers.GetCapsHandlers();

                    IRequestHandler reqHandler;
                    if (capsHandlers.TryGetValue(capName, out reqHandler))
                    {
                        receivedStats[sp.Name] = reqHandler.RequestsReceived;
                        handledStats[sp.Name] = reqHandler.RequestsHandled;
                    }        
                    else 
                    {
                        PollServiceEventArgs pollHandler = null;
                        if (caps.TryGetPollHandler(capName, out pollHandler))
                        {
                            receivedStats[sp.Name] = pollHandler.RequestsReceived;
                            handledStats[sp.Name] = pollHandler.RequestsHandled;
                        }
                    }
                }
            );

            foreach (KeyValuePair<string, int> kvp in receivedStats.OrderByDescending(kp => kp.Value))
            {
                cdt.AddRow(kvp.Key, kvp.Value, handledStats[kvp.Key]);
            }

            sb.Append(cdt.ToString());
            */
        }

        private void BuildSummaryStatsByCapReport(StringBuilder sb)
        {
            /*
            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Name", 34);
            cdt.AddColumn("Req Received", 12);
            cdt.AddColumn("Req Handled", 12);
            cdt.Indent = 2;

            Dictionary<string, int> receivedStats = new Dictionary<string, int>();
            Dictionary<string, int> handledStats = new Dictionary<string, int>();

            m_scene.ForEachScenePresence(
                sp =>
                {
                    Caps caps = m_scene.CapsModule.GetCapsForUser(sp.UUID);

                    if (caps == null)
                        return;            

                    foreach (IRequestHandler reqHandler in caps.CapsHandlers.GetCapsHandlers().Values)
                    {
                        string reqName = reqHandler.Name ?? "";

                        if (!receivedStats.ContainsKey(reqName))
                        {
                            receivedStats[reqName] = reqHandler.RequestsReceived;
                            handledStats[reqName] = reqHandler.RequestsHandled;
                        }
                        else
                        {
                            receivedStats[reqName] += reqHandler.RequestsReceived;
                            handledStats[reqName] += reqHandler.RequestsHandled;
                        }
                    }

                    foreach (KeyValuePair<string, PollServiceEventArgs> kvp in caps.GetPollHandlers())
                    {
                        string name = kvp.Key;
                        PollServiceEventArgs pollHandler = kvp.Value;

                        if (!receivedStats.ContainsKey(name))
                        {
                            receivedStats[name] = pollHandler.RequestsReceived;
                            handledStats[name] = pollHandler.RequestsHandled;
                        }
                            else
                        {
                            receivedStats[name] += pollHandler.RequestsReceived;
                            handledStats[name] += pollHandler.RequestsHandled;
                        }
                    }
                }
            );
                    
            foreach (KeyValuePair<string, int> kvp in receivedStats.OrderByDescending(kp => kp.Value))
                cdt.AddRow(kvp.Key, kvp.Value, handledStats[kvp.Key]);

            sb.Append(cdt.ToString());
            */
        }

        private void HandleShowCapsStatsByUserCommand(string module, string[] cmdParams)
        {
            /*
            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_scene)
                return;

            if (cmdParams.Length != 5 && cmdParams.Length != 7)
            {
                MainConsole.Instance.Output("Usage: show caps stats by user [<first-name> <last-name>]");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Region {0}:\n", m_scene.Name);

            if (cmdParams.Length == 5)
            {
                BuildSummaryStatsByUserReport(sb);
            }
            else if (cmdParams.Length == 7)
            {
                string firstName = cmdParams[5];
                string lastName = cmdParams[6];

                ScenePresence sp = m_scene.GetScenePresence(firstName, lastName);

                if (sp == null)
                    return;

                BuildDetailedStatsByUserReport(sb, sp);
            }

            MainConsole.Instance.Output(sb.ToString());
            */
        }

        private void BuildDetailedStatsByUserReport(StringBuilder sb, ScenePresence sp)
        {
            /*
            sb.AppendFormat("Avatar name {0}, type {1}\n", sp.Name, sp.IsChildAgent ? "child" : "root");

            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Cap Name", 34);
            cdt.AddColumn("Req Received", 12);
            cdt.AddColumn("Req Handled", 12);
            cdt.Indent = 2;

            Caps caps = m_scene.CapsModule.GetCapsForUser(sp.UUID);

            if (caps == null)
                return;

            List<CapTableRow> capRows = new List<CapTableRow>();

            foreach (IRequestHandler reqHandler in caps.CapsHandlers.GetCapsHandlers().Values)
                capRows.Add(new CapTableRow(reqHandler.Name, reqHandler.RequestsReceived, reqHandler.RequestsHandled));

            foreach (KeyValuePair<string, PollServiceEventArgs> kvp in caps.GetPollHandlers())
                capRows.Add(new CapTableRow(kvp.Key, kvp.Value.RequestsReceived, kvp.Value.RequestsHandled));

            foreach (CapTableRow ctr in capRows.OrderByDescending(ctr => ctr.RequestsReceived))
                cdt.AddRow(ctr.Name, ctr.RequestsReceived, ctr.RequestsHandled);            

            sb.Append(cdt.ToString());
            */
        }

        private void BuildSummaryStatsByUserReport(StringBuilder sb)
        {
            /*
            ConsoleDisplayTable cdt = new ConsoleDisplayTable();
            cdt.AddColumn("Name", 32);
            cdt.AddColumn("Type", 5);
            cdt.AddColumn("Req Received", 12);
            cdt.AddColumn("Req Handled", 12);
            cdt.Indent = 2;

            m_scene.ForEachScenePresence(
                sp =>
                {
                    Caps caps = m_scene.CapsModule.GetCapsForUser(sp.UUID);

                    if (caps == null)
                        return;

                    Dictionary<string, IRequestHandler> capsHandlers = caps.CapsHandlers.GetCapsHandlers();

                    int totalRequestsReceived = 0;
                    int totalRequestsHandled = 0;

                    foreach (IRequestHandler reqHandler in capsHandlers.Values)
                    {
                        totalRequestsReceived += reqHandler.RequestsReceived;
                        totalRequestsHandled += reqHandler.RequestsHandled;
                    }

                    Dictionary<string, PollServiceEventArgs> capsPollHandlers = caps.GetPollHandlers();

                    foreach (PollServiceEventArgs handler in capsPollHandlers.Values)
                    {
                        totalRequestsReceived += handler.RequestsReceived;
                        totalRequestsHandled += handler.RequestsHandled;
                    }
                    
                    cdt.AddRow(sp.Name, sp.IsChildAgent ? "child" : "root", totalRequestsReceived, totalRequestsHandled);
                }
            );

            sb.Append(cdt.ToString());
            */
        }

        private class CapTableRow
        {
            public string Name { get; set; }
            public int RequestsReceived { get; set; }
            public int RequestsHandled { get; set; }

            public CapTableRow(string name, int requestsReceived, int requestsHandled)
            {
                Name = name;
                RequestsReceived = requestsReceived;
                RequestsHandled = requestsHandled;
            }
        }
    }
}
