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
        private List<Scene> m_sceneList = new List<Scene>();

        private IEntityTransferModule m_AgentTransferModule;
        protected IEntityTransferModule AgentTransferModule
        {
            get
            {
                if (m_AgentTransferModule == null)
                    m_AgentTransferModule = m_sceneList[0].RequestModuleInterface<IEntityTransferModule>();
                return m_AgentTransferModule;
            }
        }

        private bool m_ModuleEnabled = false;

        #region IRegionModule

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
            lock (m_sceneList)
            {
                if (m_sceneList.Contains(scene))
                {
                    m_sceneList.Remove(scene);
                }
            }
        }

        /// <summary>
        /// Can be called from other modules.
        /// </summary>
        /// <param name="scene"></param>
        public void Init(Scene scene)
        {
            if (!m_sceneList.Contains(scene))
            {
                lock (m_sceneList)
                {
                    m_sceneList.Add(scene);
                }

            }
        }

        #endregion /* IRegionModule */

        #region ISimulation

        public IScene GetScene(ulong regionhandle)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return s;
            }
            // ? weird. should not happen
            return m_sceneList[0];
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

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == destination.RegionHandle)
                {
                    m_log.DebugFormat("[LOCAL SIMULATION CONNECTOR]: Found region {0} to send SendCreateChildAgent", destination.RegionName);
                    return s.NewUserConnection(aCircuit, teleportFlags, out reason);
                }
            }

            m_log.DebugFormat("[LOCAL SIMULATION CONNECTOR]: Did not find region {0} for SendCreateChildAgent", destination.RegionName);
            reason = "Did not find region " + destination.RegionName;
            return false;
        }

        public bool UpdateAgent(GridRegion destination, AgentData cAgentData)
        {
            if (destination == null)
                return false;

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == destination.RegionHandle)
                {
                    m_log.DebugFormat(
                        "[LOCAL SIMULATION CONNECTOR]: Found region {0} {1} to send AgentUpdate",
                        s.RegionInfo.RegionName, destination.RegionHandle);

                    s.IncomingChildAgentDataUpdate(cAgentData);
                    return true;
                }
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
            foreach (Scene s in m_sceneList)
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

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == destination.RegionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to send ChildAgentUpdate");
                    return s.IncomingRetrieveRootAgent(id, out agent);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return false;
        }

        public bool QueryAccess(GridRegion destination, UUID id, Vector3 position, out string reason)
        {
            reason = "Communications failure";
            if (destination == null)
                return false;

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionID == destination.RegionID)
                    return s.QueryAccess(id, position, out reason);
            }
            //m_log.Debug("[LOCAL COMMS]: region not found for QueryAccess");
            return false;
        }

        public bool ReleaseAgent(UUID origin, UUID id, string uri)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionID == origin)
                {
                    m_log.Debug("[LOCAL COMMS]: Found region to SendReleaseAgent");
                    AgentTransferModule.AgentArrivedAtDestination(id);
                    return true;
//                    return s.IncomingReleaseAgent(id);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendReleaseAgent " + origin);
            return false;
        }

        public bool CloseAgent(GridRegion destination, UUID id)
        {
            if (destination == null)
                return false;

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionID == destination.RegionID)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendCloseAgent");
                    // Let's spawn a threadlet right here, because this may take
                    // a while
                    Util.FireAndForget(delegate { s.IncomingCloseAgent(id); });
                    return true;
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendCloseAgent");
            return false;
        }

        /**
         * Object-related communications
         */

        public bool CreateObject(GridRegion destination, ISceneObject sog, bool isLocalCall)
        {
            if (destination == null)
                return false;

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == destination.RegionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendCreateObject");
                    if (isLocalCall)
                    {
                        // We need to make a local copy of the object
                        ISceneObject sogClone = sog.CloneForNewScene();
                        sogClone.SetState(sog.GetStateSnapshot(), s);
                        return s.IncomingCreateObject(sogClone);
                    }
                    else
                    {
                        // Use the object as it came through the wire
                        return s.IncomingCreateObject(sog);
                    }
                }
            }
            return false;
        }

        public bool CreateObject(GridRegion destination, UUID userID, UUID itemID)
        {
            if (destination == null)
                return false;

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == destination.RegionHandle)
                {
                    return s.IncomingCreateObject(userID, itemID);
                }
            }
            return false;
        }


        #endregion /* IInterregionComms */

        #region Misc

        public bool IsLocalRegion(ulong regionhandle)
        {
            foreach (Scene s in m_sceneList)
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return true;
            return false;
        }

        public bool IsLocalRegion(UUID id)
        {
            foreach (Scene s in m_sceneList)
                if (s.RegionInfo.RegionID == id)
                    return true;
            return false;
        }

        #endregion
    }
}
