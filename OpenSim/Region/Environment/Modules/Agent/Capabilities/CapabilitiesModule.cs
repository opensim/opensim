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
 *     * Neither the name of the OpenSim Project nor the
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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.Environment.Modules.Agent.Capabilities
{
    public class CapabilitiesModule : IRegionModule, ICapabilitiesModule
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
        
        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<ICapabilitiesModule>(this);
        }
        
        public void PostInitialise() {}
        public void Close() {}
        public string Name { get { return "Capabilities Module"; } }
        public bool IsSharedModule { get { return false; } }

        public void AddCapsHandler(UUID agentId)
        {
            if (m_scene.RegionInfo.EstateSettings.IsBanned(agentId))
                return;

            String capsObjectPath = GetCapsPath(agentId);

            Caps cap = null;
            if (m_capsHandlers.TryGetValue(agentId, out cap))
            {
                m_log.DebugFormat(
                    "[CAPS]: Attempt at registering twice for the same agent {0}. {1}. Ignoring.", 
                    agentId, capsObjectPath);
                //return;
            }

            cap 
                = new Caps(
                    m_scene.AssetCache, m_scene.CommsManager.HttpServer, m_scene.RegionInfo.ExternalHostName, 
                    m_scene.CommsManager.HttpServer.Port,
                    capsObjectPath, agentId, m_scene.DumpAssetsToFile, m_scene.RegionInfo.RegionName);
            
            cap.RegisterHandlers();

            m_scene.EventManager.TriggerOnRegisterCaps(agentId, cap);

            cap.AddNewInventoryItem = m_scene.AddUploadedInventoryItem;
            cap.ItemUpdatedCall = m_scene.CapsUpdateInventoryItemAsset;
            cap.TaskScriptUpdatedCall = m_scene.CapsUpdateTaskInventoryScriptAsset;
            cap.CAPSFetchInventoryDescendents = m_scene.HandleFetchInventoryDescendentsCAPS;
            cap.GetClient = m_scene.m_sceneGraph.GetControllingClient;
            
            m_capsHandlers[agentId] = cap;
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
            childrenSeeds[agent.AgentID] = ((agent.ChildrenCapSeeds == null) ? new Dictionary<ulong, string>() : agent.ChildrenCapSeeds);
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
            if (childrenSeeds.TryGetValue(agentID, out seeds))
            {
                return seeds[handle];
            }
            return null;
        }

        public void SetChildrenSeed(UUID agentID, Dictionary<ulong, string> seeds)
        {
            //Console.WriteLine(" !!! Setting child seeds in {0} to {1}", RegionInfo.RegionName, value.Count);
            childrenSeeds[agentID] = seeds;
        }

        public void DumpChildrenSeeds(UUID agentID)
        {
            Console.WriteLine("================ ChildrenSeed {0} ================", m_scene.RegionInfo.RegionName);
            foreach (KeyValuePair<ulong, string> kvp in childrenSeeds[agentID])
            {
                uint x, y;
                Utils.LongToUInts(kvp.Key, out x, out y);
                x = x / Constants.RegionSize;
                y = y / Constants.RegionSize;
                Console.WriteLine(" >> {0}, {1}: {2}", x, y, kvp.Value);
            }
        }        
    }
}