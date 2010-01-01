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

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation
{
    public class LocalSimulationConnectorModule : ISharedRegionModule, ISimulationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<Scene> m_sceneList = new List<Scene>();


        #region IRegionModule

        public void Initialise(IConfigSource config)
        {
            // This module is always on
            m_log.Debug("[LOCAL SIMULATION]: Enabling LocalSimulation module");
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            RemoveScene(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            Init(scene);
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
                    scene.RegisterModuleInterface<ISimulationService>(this);
                }

            }
        }

        #endregion /* IRegionModule */

        #region ISimulation

        /**
         * Agent-related communications
         */

        public bool CreateAgent(ulong regionHandle, AgentCircuitData aCircuit, uint teleportFlags, out string reason)
        {

            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
//                    m_log.DebugFormat("[LOCAL COMMS]: Found region {0} to send SendCreateChildAgent", regionHandle);
                    return s.NewUserConnection(aCircuit, teleportFlags, out reason);
                }
            }

//            m_log.DebugFormat("[LOCAL COMMS]: Did not find region {0} for SendCreateChildAgent", regionHandle);
            uint x = 0, y = 0;
            Utils.LongToUInts(regionHandle, out x, out y);
            reason = "Did not find region " + x + "-" + y;
            return false;
        }

        public bool UpdateAgent(ulong regionHandle, AgentData cAgentData)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.DebugFormat(
                    //    "[LOCAL COMMS]: Found region {0} {1} to send ChildAgentUpdate",
                    //    s.RegionInfo.RegionName, regionHandle);

                    s.IncomingChildAgentDataUpdate(cAgentData);
                    return true;
                }
            }

//            m_log.DebugFormat("[LOCAL COMMS]: Did not find region {0} for ChildAgentUpdate", regionHandle);
            return false;
        }

        public bool UpdateAgent(ulong regionHandle, AgentPosition cAgentData)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to send ChildAgentUpdate");
                    s.IncomingChildAgentDataUpdate(cAgentData);
                    return true;
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return false;
        }

        public bool RetrieveAgent(ulong regionHandle, UUID id, out IAgentData agent)
        {
            agent = null;
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to send ChildAgentUpdate");
                    return s.IncomingRetrieveRootAgent(id, out agent);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found for ChildAgentUpdate");
            return false;
        }

        public bool ReleaseAgent(ulong regionHandle, UUID id, string uri)
        {
            //uint x, y;
            //Utils.LongToUInts(regionHandle, out x, out y);
            //x = x / Constants.RegionSize;
            //y = y / Constants.RegionSize;
            //m_log.Debug("\n >>> Local SendReleaseAgent " + x + "-" + y);
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendReleaseAgent");
                    return s.IncomingReleaseAgent(id);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendReleaseAgent");
            return false;
        }

        public bool CloseAgent(ulong regionHandle, UUID id)
        {
            //uint x, y;
            //Utils.LongToUInts(regionHandle, out x, out y);
            //x = x / Constants.RegionSize;
            //y = y / Constants.RegionSize;
            //m_log.Debug("\n >>> Local SendCloseAgent " + x + "-" + y);
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.Debug("[LOCAL COMMS]: Found region to SendCloseAgent");
                    return s.IncomingCloseAgent(id);
                }
            }
            //m_log.Debug("[LOCAL COMMS]: region not found in SendCloseAgent");
            return false;
        }

        /**
         * Object-related communications
         */

        public bool CreateObject(ulong regionHandle, ISceneObject sog, bool isLocalCall)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
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

        public bool CreateObject(ulong regionHandle, UUID userID, UUID itemID)
        {
            foreach (Scene s in m_sceneList)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    return s.IncomingCreateObject(userID, itemID);
                }
            }
            return false;
        }


        #endregion /* IInterregionComms */

        #region Misc

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

        public bool IsLocalRegion(ulong regionhandle)
        {
            foreach (Scene s in m_sceneList)
                if (s.RegionInfo.RegionHandle == regionhandle)
                    return true;
            return false;
        }

        #endregion
    }
}
