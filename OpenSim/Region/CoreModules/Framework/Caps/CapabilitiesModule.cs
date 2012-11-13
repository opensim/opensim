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
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
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
        protected Dictionary<UUID, Caps> m_capsObjects = new Dictionary<UUID, Caps>();
        
        protected Dictionary<UUID, string> capsPaths = new Dictionary<UUID, string>();
        protected Dictionary<UUID, Dictionary<ulong, string>> childrenSeeds 
            = new Dictionary<UUID, Dictionary<ulong, string>>();
        
        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<ICapabilitiesModule>(this);

            MainConsole.Instance.Commands.AddCommand("Comms", false, "show caps",
                "show caps",
                "Shows all registered capabilities for users", HandleShowCapsCommand);
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

        public void CreateCaps(UUID agentId)
        {
            int flags = m_scene.GetUserFlags(agentId);
            if (m_scene.RegionInfo.EstateSettings.IsBanned(agentId, flags))
                return;

            String capsObjectPath = GetCapsPath(agentId);

            if (m_capsObjects.ContainsKey(agentId))
            {
                Caps oldCaps = m_capsObjects[agentId];
                
                m_log.DebugFormat(
                    "[CAPS]: Recreating caps for agent {0}.  Old caps path {1}, new caps path {2}. ", 
                    agentId, oldCaps.CapsObjectPath, capsObjectPath);
                // This should not happen. The caller code is confused. We need to fix that.
                // CAPs can never be reregistered, or the client will be confused.
                // Hence this return here.
                //return;
            }

            Caps caps = new Caps(MainServer.Instance, m_scene.RegionInfo.ExternalHostName,
                    (MainServer.Instance == null) ? 0: MainServer.Instance.Port,
                    capsObjectPath, agentId, m_scene.RegionInfo.RegionName);

            m_capsObjects[agentId] = caps;

            m_scene.EventManager.TriggerOnRegisterCaps(agentId, caps);
        }

        public void RemoveCaps(UUID agentId)
        {
            if (childrenSeeds.ContainsKey(agentId))
            {
                childrenSeeds.Remove(agentId);
            }

            lock (m_capsObjects)
            {
                if (m_capsObjects.ContainsKey(agentId))
                {
                    m_capsObjects[agentId].DeregisterHandlers();
                    m_scene.EventManager.TriggerOnDeregisterCaps(agentId, m_capsObjects[agentId]);
                    m_capsObjects.Remove(agentId);
                }
                else
                {
                    m_log.WarnFormat(
                        "[CAPS]: Received request to remove CAPS handler for root agent {0} in {1}, but no such CAPS handler found!",
                        agentId, m_scene.RegionInfo.RegionName);
                }
            }
        }
        
        public Caps GetCapsForUser(UUID agentId)
        {
            lock (m_capsObjects)
            {
                if (m_capsObjects.ContainsKey(agentId))
                {
                    return m_capsObjects[agentId];
                }
            }
            
            return null;
        }
        
        public void SetAgentCapsSeeds(AgentCircuitData agent)
        {
            capsPaths[agent.AgentID] = agent.CapsPath;
            childrenSeeds[agent.AgentID] 
                = ((agent.ChildrenCapSeeds == null) ? new Dictionary<ulong, string>() : agent.ChildrenCapSeeds);
        }
        
        public string GetCapsPath(UUID agentId)
        {
            if (capsPaths.ContainsKey(agentId))
            {
                return capsPaths[agentId];
            }

            return null;
        }
        
        public Dictionary<ulong, string> GetChildrenSeeds(UUID agentID)
        {
            Dictionary<ulong, string> seeds = null;
            if (childrenSeeds.TryGetValue(agentID, out seeds))
                return seeds;
            return new Dictionary<ulong, string>();
        }

        public void DropChildSeed(UUID agentID, ulong handle)
        {
            Dictionary<ulong, string> seeds;
            if (childrenSeeds.TryGetValue(agentID, out seeds))
            {
                seeds.Remove(handle);
            }
        }

        public string GetChildSeed(UUID agentID, ulong handle)
        {
            Dictionary<ulong, string> seeds;
            string returnval;
            if (childrenSeeds.TryGetValue(agentID, out seeds))
            {
                if (seeds.TryGetValue(handle, out returnval))
                    return returnval;
            }
            return null;
        }

        public void SetChildrenSeed(UUID agentID, Dictionary<ulong, string> seeds)
        {
            //m_log.DebugFormat(" !!! Setting child seeds in {0} to {1}", m_scene.RegionInfo.RegionName, seeds.Count);
            childrenSeeds[agentID] = seeds;
        }

        public void DumpChildrenSeeds(UUID agentID)
        {
            m_log.Info("================ ChildrenSeed "+m_scene.RegionInfo.RegionName+" ================");
            foreach (KeyValuePair<ulong, string> kvp in childrenSeeds[agentID])
            {
                uint x, y;
                Utils.LongToUInts(kvp.Key, out x, out y);
                x = x / Constants.RegionSize;
                y = y / Constants.RegionSize;
                m_log.Info(" >> "+x+", "+y+": "+kvp.Value);
            }
        }

        private void HandleShowCapsCommand(string module, string[] cmdparams)
        {
            StringBuilder caps = new StringBuilder();
            caps.AppendFormat("Region {0}:\n", m_scene.RegionInfo.RegionName);

            foreach (KeyValuePair<UUID, Caps> kvp in m_capsObjects)
            {
                caps.AppendFormat("** User {0}:\n", kvp.Key);

                for (IDictionaryEnumerator kvp2 = kvp.Value.CapsHandlers.GetCapsDetails(false).GetEnumerator(); kvp2.MoveNext(); )
                {
                    Uri uri = new Uri(kvp2.Value.ToString());
                    caps.AppendFormat(m_showCapsCommandFormat, kvp2.Key, uri.PathAndQuery);
                }

                foreach (KeyValuePair<string, string> kvp3 in kvp.Value.ExternalCapsHandlers)
                    caps.AppendFormat(m_showCapsCommandFormat, kvp3.Key, kvp3.Value);
            }

            MainConsole.Instance.Output(caps.ToString());
        }
    }
}
