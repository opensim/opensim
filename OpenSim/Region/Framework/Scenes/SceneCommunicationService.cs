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
using System.Net;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OSD = OpenMetaverse.StructuredData.OSD;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.Framework.Scenes
{
    public delegate void RemoveKnownRegionsFromAvatarList(UUID avatarID, List<ulong> regionlst);

    /// <summary>
    /// Class that Region communications runs through
    /// </summary>
    public class SceneCommunicationService //one instance per region
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected RegionInfo m_regionInfo;
        protected Scene m_scene;

        public void SetScene(Scene s)
        {
            m_scene = s;
            m_regionInfo = s.RegionInfo;
        }

        public delegate void InformNeighbourThatRegionUpDelegate(INeighbourService nService, RegionInfo region, ulong regionhandle);

        private void InformNeighborsThatRegionisUpCompleted(IAsyncResult iar)
        {
            InformNeighbourThatRegionUpDelegate icon = (InformNeighbourThatRegionUpDelegate) iar.AsyncState;
            icon.EndInvoke(iar);
        }

        /// <summary>
        /// Asynchronous call to information neighbouring regions that this region is up
        /// </summary>
        /// <param name="region"></param>
        /// <param name="regionhandle"></param>
        private void InformNeighboursThatRegionIsUpAsync(INeighbourService neighbourService, RegionInfo region, ulong regionhandle)
        {
            uint x = 0, y = 0;
            Utils.LongToUInts(regionhandle, out x, out y);

            GridRegion neighbour = null;
            if (neighbourService != null)
                neighbour = neighbourService.HelloNeighbour(regionhandle, region);
            else
                m_log.DebugFormat("[SCS]: No neighbour service provided for informing neigbhours of this region");

            if (neighbour != null)
            {
              //  m_log.DebugFormat("[INTERGRID]: Successfully informed neighbour {0}-{1} that I'm here", x / Constants.RegionSize, y / Constants.RegionSize);
                m_scene.EventManager.TriggerOnRegionUp(neighbour);
            }
            else
            {
                m_log.InfoFormat("[INTERGRID]: Failed to inform neighbour {0}-{1} that I'm here.", x / Constants.RegionSize, y / Constants.RegionSize);
            }
        }

        public void InformNeighborsThatRegionisUp(INeighbourService neighbourService, RegionInfo region)
        {
            //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: Sending InterRegion Notification that region is up " + region.RegionName);

            List<GridRegion> neighbours = m_scene.GridService.GetNeighbours(m_scene.RegionInfo.ScopeID, m_scene.RegionInfo.RegionID);
            //m_log.DebugFormat("[INTERGRID]: Informing {0} neighbours that this region is up", neighbours.Count);
            foreach (GridRegion n in neighbours)
            {
                InformNeighbourThatRegionUpDelegate d = InformNeighboursThatRegionIsUpAsync;
                d.BeginInvoke(neighbourService, region, n.RegionHandle,
                              InformNeighborsThatRegionisUpCompleted,
                              d);
            }
        }

        public delegate void SendChildAgentDataUpdateDelegate(AgentPosition cAgentData, UUID scopeID, GridRegion dest);

        /// <summary>
        /// This informs all neighboring regions about the settings of it's child agent.
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        ///
        /// This contains information, such as, Draw Distance, Camera location, Current Position, Current throttle settings, etc.
        ///
        /// </summary>
        private void SendChildAgentDataUpdateAsync(AgentPosition cAgentData, UUID scopeID, GridRegion dest)
        {
            //m_log.Info("[INTERGRID]: Informing neighbors about my agent in " + m_regionInfo.RegionName);
            try
            {
                m_scene.SimulationService.UpdateAgent(dest, cAgentData);
            }
            catch
            {
                // Ignore; we did our best
            }
        }

        private void SendChildAgentDataUpdateCompleted(IAsyncResult iar)
        {
            SendChildAgentDataUpdateDelegate icon = (SendChildAgentDataUpdateDelegate) iar.AsyncState;
            icon.EndInvoke(iar);
        }

        ExpiringCache<string, bool> _failedSims = new ExpiringCache<string, bool>();
        public void SendChildAgentDataUpdate(AgentPosition cAgentData, ScenePresence presence)
        {
            // This assumes that we know what our neighbors are.
            try
            {
                uint x = 0, y = 0;
                List<string> simulatorList = new List<string>();
                foreach (ulong regionHandle in presence.KnownRegionHandles)
                {
                    if (regionHandle != m_regionInfo.RegionHandle)
                    {
                        // we only want to send one update to each simulator; the simulator will
                        // hand it off to the regions where a child agent exists, this does assume
                        // that the region position is cached or performance will degrade
                        Utils.LongToUInts(regionHandle, out x, out y);
                        GridRegion dest = m_scene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                        bool v = true;
                        if (! simulatorList.Contains(dest.ServerURI) && !_failedSims.TryGetValue(dest.ServerURI, out v))
                        {
                            // we havent seen this simulator before, add it to the list
                            // and send it an update
                            simulatorList.Add(dest.ServerURI);
                            // Let move this to sync. Mono definitely does not like async networking.
                            if (!m_scene.SimulationService.UpdateAgent(dest, cAgentData))
                                // Also if it fails, get it out of the loop for a bit
                                _failedSims.Add(dest.ServerURI, true, 120);

                            // Leaving this here as a reminder that we tried, and it sucks.
                            //SendChildAgentDataUpdateDelegate d = SendChildAgentDataUpdateAsync;
                            //d.BeginInvoke(cAgentData, m_regionInfo.ScopeID, dest,
                            //              SendChildAgentDataUpdateCompleted,
                            //              d);
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // We're ignoring a collection was modified error because this data gets old and outdated fast.
            }
        }

        public delegate void SendCloseChildAgentDelegate(UUID agentID, ulong regionHandle);

        /// <summary>
        /// This Closes child agents on neighboring regions
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        protected void SendCloseChildAgentAsync(UUID agentID, ulong regionHandle)
        {
            // let's do our best, but there's not much we can do if the neighbour doesn't accept.

            //m_commsProvider.InterRegion.TellRegionToCloseChildConnection(regionHandle, agentID);
            uint x = 0, y = 0;
            Utils.LongToUInts(regionHandle, out x, out y);

            GridRegion destination = m_scene.GridService.GetRegionByPosition(m_regionInfo.ScopeID, (int)x, (int)y);
            m_scene.SimulationService.CloseChildAgent(destination, agentID);
        }

        private void SendCloseChildAgentCompleted(IAsyncResult iar)
        {
            SendCloseChildAgentDelegate icon = (SendCloseChildAgentDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }

        public void SendCloseChildAgentConnections(UUID agentID, List<ulong> regionslst)
        {
            foreach (ulong handle in regionslst)
            {
                SendCloseChildAgentDelegate d = SendCloseChildAgentAsync;
                d.BeginInvoke(agentID, handle,
                              SendCloseChildAgentCompleted,
                              d);
            }
        }

        public List<GridRegion> RequestNamedRegions(string name, int maxNumber)
        {
            return m_scene.GridService.GetRegionsByName(UUID.Zero, name, maxNumber);
        }
    }
}
