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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Agent.Capabilities
{
    public class CapabilitiesModule : INonSharedRegionModule, ICapabilitiesModule
    { 
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Scene m_scene;
        
        /// <summary>
        /// Each agent has its own capabilities handler.
        /// </summary>
        protected Dictionary<UUID, Caps> m_capsHandlers = new Dictionary<UUID, Caps>();
        
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
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.UnregisterModuleInterface<ICapabilitiesModule>(this);
        }
        
        public void PostInitialise() {}

        public void Close() {}

        public string Name 
        { 
            get { return "Capabilities Module"; } 
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddCapsHandler(UUID agentId)
        {
            if (m_scene.RegionInfo.EstateSettings.IsBanned(agentId))
                return;

            String capsObjectPath = GetCapsPath(agentId);

            if (m_capsHandlers.ContainsKey(agentId))
            {
                Caps oldCaps = m_capsHandlers[agentId];
                
                m_log.DebugFormat(
                    "[CAPS]: Reregistering caps for agent {0}.  Old caps path {1}, new caps path {2}. ", 
                    agentId, oldCaps.CapsObjectPath, capsObjectPath);
                // This should not happen. The caller code is confused. We need to fix that.
                // CAPs can never be reregistered, or the client will be confused.
                // Hence this return here.
                //return;
            }

            Caps caps
                = new Caps(
                    m_scene.AssetService, MainServer.Instance, m_scene.RegionInfo.ExternalHostName,
                    MainServer.Instance.Port,
                    capsObjectPath, agentId, m_scene.DumpAssetsToFile, m_scene.RegionInfo.RegionName);
            
            caps.RegisterHandlers();

            m_scene.EventManager.TriggerOnRegisterCaps(agentId, caps);

            caps.AddNewInventoryItem = m_scene.AddUploadedInventoryItem;
            caps.ItemUpdatedCall = m_scene.CapsUpdateInventoryItemAsset;
            caps.TaskScriptUpdatedCall = m_scene.CapsUpdateTaskInventoryScriptAsset;
            caps.CAPSFetchInventoryDescendents = m_scene.HandleFetchInventoryDescendentsCAPS;
            caps.GetClient = m_scene.SceneContents.GetControllingClient;
            
            m_capsHandlers[agentId] = caps;
        }

        public void RemoveCapsHandler(UUID agentId)
        {
            if (childrenSeeds.ContainsKey(agentId))
            {
                childrenSeeds.Remove(agentId);
            }

            lock (m_capsHandlers)
            {
                if (m_capsHandlers.ContainsKey(agentId))
                {
                    m_capsHandlers[agentId].DeregisterHandlers();
                    m_scene.EventManager.TriggerOnDeregisterCaps(agentId, m_capsHandlers[agentId]);
                    m_capsHandlers.Remove(agentId);
                }
                else
                {
                    m_log.WarnFormat(
                        "[CAPS]: Received request to remove CAPS handler for root agent {0} in {1}, but no such CAPS handler found!",
                        agentId, m_scene.RegionInfo.RegionName);
                }
            }
        }
        
        public Caps GetCapsHandlerForUser(UUID agentId)
        {
            lock (m_capsHandlers)
            {
                if (m_capsHandlers.ContainsKey(agentId))
                {
                    return m_capsHandlers[agentId];
                }
            }
            
            return null;
        }
        
        public void NewUserConnection(AgentCircuitData agent)
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
    }
}
