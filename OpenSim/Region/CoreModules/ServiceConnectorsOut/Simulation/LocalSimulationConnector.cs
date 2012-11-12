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
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation
{
    public class LocalSimulationConnectorModule : ISharedRegionModule, ISimulationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Version of this service
        /// </summary>
        private const string m_Version = "SIMULATION/0.1";

        /// <summary>
        /// Map region ID to scene.
        /// </summary>
        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();

        /// <summary>
        /// Is this module enabled?
        /// </summary>
        private bool m_ModuleEnabled = false;

        #region Region Module interface

        public void Initialise(IConfigSource config)
        {
            IConfig moduleConfig = config.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("SimulationServices", "");
                if (name == Name)
                {
                    //IConfig userConfig = config.Configs["SimulationService"];
                    //if (userConfig == null)
                    //{
                    //    m_log.Error("[AVATAR CONNECTOR]: SimulationService missing from OpenSim.ini");
                    //    return;
                    //}

                    m_ModuleEnabled = true;

                    m_log.Info("[SIMULATION CONNECTOR]: Local simulation enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_ModuleEnabled)
                return;

            Init(scene);
            scene.RegisterModuleInterface<ISimulationService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_ModuleEnabled)
                return;

            RemoveScene(scene);
            scene.UnregisterModuleInterface<ISimulationService>(this);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalSimulationConnectorModule"; }
        }

        /// <summary>
        /// Can be called from other modules.
        /// </summary>
        /// <param name="scene"></param>
        public void RemoveScene(Scene scene)
        {
            lock (m_scenes)
            {
                if (m_scenes.ContainsKey(scene.RegionInfo.RegionID))
                    m_scenes.Remove(scene.RegionInfo.RegionID);
                else
                    m_log.WarnFormat(
                        "[LOCAL SIMULATION CONNECTOR]: Tried to remove region {0} but it was not present",
                        scene.RegionInfo.RegionName);
            }
        }

        /// <summary>
        /// Can be called from other modules.
        /// </summary>
        /// <param name="scene"></param>
        public void Init(Scene scene)
        {
            lock (m_scenes)
            {
                if (!m_scenes.ContainsKey(scene.RegionInfo.RegionID))
                    m_scenes[scene.RegionInfo.RegionID] = scene;
                else
                    m_log.WarnFormat(
                        "[LOCAL SIMULATION CONNECTOR]: Tried to add region {0} but it is already present",
                        scene.RegionInfo.RegionName);
            }
        }

        #endregion

        #region ISimulation

        public IScene GetScene(UUID regionId)
        {
            if (m_scenes.ContainsKey(regionId))
            {
                return m_scenes[regionId];
            }
            else
            {
                // FIXME: This was pre-existing behaviour but possibly not a good idea, since it hides an error rather
                // than making it obvious and fixable.  Need to see if the error message comes up in practice.
                Scene s = m_scenes.Values.ToArray()[0];

                m_log.ErrorFormat(
                    "[LOCAL SIMULATION CONNECTOR]: Region with id {0} not found.  Returning {1} {2} instead",
                    regionId, s.RegionInfo.RegionName, s.RegionInfo.RegionID);

                return s;
            }
        }

        public ISimulationService GetInnerService()
        {
            return this;
        }

        /**
         * Agent-related communications
         */

        public bool CreateAgent(GridRegion destination, AgentCircuitData aCircuit, uint teleportFlags, out string reason)
        {
            if (destination == null)
            {
                reason = "Given destination was null";
                m_log.DebugFormat("[LOCAL SIMULATION CONNECTOR]: CreateAgent was given a null destination");
                return false;
            }

            if (m_scenes.ContainsKey(destination.RegionID))
            {
//                    m_log.DebugFormat("[LOCAL SIMULATION CONNECTOR]: Found region {0} to send SendCreateChildAgent", destination.RegionName);
                return m_scenes[destination.RegionID].NewUserConnection(aCircuit, teleportFlags, out reason);
            }

            reason = "Did not find region " + destination.RegionName;
            return false;
        }

        public bool UpdateAgent(GridRegion destination, AgentData cAgentData)
        {
            if (destination == null)
                return false;

            if (m_scenes.ContainsKey(destination.RegionID))
            {
//                    m_log.DebugFormat(
//                        "[LOCAL SIMULATION CONNECTOR]: Found region {0} {1} to send AgentUpdate",
//                        s.RegionInfo.RegionName, destination.RegionHandle);

                return m_scenes[destination.RegionID].IncomingChildAgentDataUpdate(cAgentData);
            }

//            m_log.DebugFormat("[LOCAL COMMS]: Did not find region {0} for ChildAgentUpdate", regionHandle);
            return false;
        }

        public bool UpdateAgent(GridRegion destination, AgentPosition cAgentData)
        {
            if (destination == null)
                return false;

            // We limit the number of messages sent for a position change to just one per
            // simulator so when we receive the update we need to hand it to each of the
            // scenes; scenes each check to see if the is a scene presence for the avatar
            // note that we really don't need the GridRegion for this call
            foreach (Scene s in m_scenes.Values)
            {
                //m_log.Debug("[LOCAL COMMS]: Found region to send ChildAgentUpdate");
                s.IncomingChildAgentDataUpdate(cAgentData);
            }

            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return true;
        }

        public bool RetrieveAgent(GridRegion destination, UUID id, out IAgentData agent)
        {
            agent = null;
            
            if (destination == null)
                return false;

            if (m_scenes.ContainsKey(destination.RegionID))
            {
//                    m_log.DebugFormat(
//                        "[LOCAL SIMULATION CONNECTOR]: Found region {0} {1} to send AgentUpdate",
//                        s.RegionInfo.RegionName, destination.RegionHandle);

                return m_scenes[destination.RegionID].IncomingRetrieveRootAgent(id, out agent);
            }

            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return false;
        }

        public bool QueryAccess(GridRegion destination, UUID id, Vector3 position, out string version, out string reason)
        {
            reason = "Communications failure";
            version = m_Version;
            if (destination == null)
                return false;

            if (m_scenes.ContainsKey(destination.RegionID))
            {
//                    m_log.DebugFormat(
//                        "[LOCAL SIMULATION CONNECTOR]: Found region {0} {1} to send AgentUpdate",
//                        s.RegionInfo.RegionName, destination.RegionHandle);

                return m_scenes[destination.RegionID].QueryAccess(id, position, out reason);
            }

            //m_log.Debug("[LOCAL COMMS]: region not found for QueryAccess");
            return false;
        }

        public bool ReleaseAgent(UUID originId, UUID agentId, string uri)
        {
            if (m_scenes.ContainsKey(originId))
            {
//                    m_log.DebugFormat(
//                        "[LOCAL SIMULATION CONNECTOR]: Found region {0} {1} to send AgentUpdate",
//                        s.RegionInfo.RegionName, destination.RegionHandle);

                m_scenes[originId].EntityTransferModule.AgentArrivedAtDestination(agentId);
                return true;
            }

            //m_log.Debug("[LOCAL COMMS]: region not found in SendReleaseAgent " + origin);
            return false;
        }

        public bool CloseChildAgent(GridRegion destination, UUID id)
        {
            return CloseAgent(destination, id);
        }

        public bool CloseAgent(GridRegion destination, UUID id)
        {
            if (destination == null)
                return false;

            if (m_scenes.ContainsKey(destination.RegionID))
            {
//                    m_log.DebugFormat(
//                        "[LOCAL SIMULATION CONNECTOR]: Found region {0} {1} to send AgentUpdate",
//                        s.RegionInfo.RegionName, destination.RegionHandle);

                Util.FireAndForget(delegate { m_scenes[destination.RegionID].IncomingCloseAgent(id, false); });
                return true;
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendCloseAgent");
            return false;
        }

        /**
         * Object-related communications
         */

        public bool CreateObject(GridRegion destination, Vector3 newPosition, ISceneObject sog, bool isLocalCall)
        {
            if (destination == null)
                return false;

            if (m_scenes.ContainsKey(destination.RegionID))
            {
//                    m_log.DebugFormat(
//                        "[LOCAL SIMULATION CONNECTOR]: Found region {0} {1} to send AgentUpdate",
//                        s.RegionInfo.RegionName, destination.RegionHandle);

                Scene s = m_scenes[destination.RegionID];

                if (isLocalCall)
                {
                    // We need to make a local copy of the object
                    ISceneObject sogClone = sog.CloneForNewScene();
                    sogClone.SetState(sog.GetStateSnapshot(), s);
                    return s.IncomingCreateObject(newPosition, sogClone);
                }
                else
                {
                    // Use the object as it came through the wire
                    return s.IncomingCreateObject(newPosition, sog);
                }
            }

            return false;
        }

        #endregion /* IInterregionComms */

        #region Misc

        public bool IsLocalRegion(ulong regionhandle)
        {
            foreach (Scene s in m_scenes.Values)
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return true;

            return false;
        }

        public bool IsLocalRegion(UUID id)
        {
            return m_scenes.ContainsKey(id);
        }

        #endregion
    }
}
