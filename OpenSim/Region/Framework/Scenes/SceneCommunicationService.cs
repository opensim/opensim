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
        private static string LogHeader = "[SCENE COMMUNICATION SERVICE]";

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
            InformNeighbourThatRegionUpDelegate icon = (InformNeighbourThatRegionUpDelegate)iar.AsyncState;
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
                m_log.DebugFormat("{0} neighbour service provided for region {0} to inform neigbhours of status", LogHeader, m_scene.Name);

            if (neighbour != null)
            {
                m_log.DebugFormat("{0} Region {1} successfully informed neighbour {2} at {3}-{4} that it is up",
                    LogHeader, m_scene.Name, neighbour.RegionName, Util.WorldToRegionLoc(x), Util.WorldToRegionLoc(y));

                m_scene.EventManager.TriggerOnRegionUp(neighbour);
            }
            else
            {
                m_log.WarnFormat(
                    "[SCENE COMMUNICATION SERVICE]: Region {0} failed to inform neighbour at {1}-{2} that it is up.",
                    m_scene.Name, Util.WorldToRegionLoc(x), Util.WorldToRegionLoc(y));
            }
        }

        public void InformNeighborsThatRegionisUp(INeighbourService neighbourService, RegionInfo region)
        {
            //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: Sending InterRegion Notification that region is up " + region.RegionName);

            List<GridRegion> neighbours
                = m_scene.GridService.GetNeighbours(m_scene.RegionInfo.ScopeID, m_scene.RegionInfo.RegionID);

            List<GridRegion> onlineNeighbours = new List<GridRegion>();

            foreach (GridRegion n in neighbours)
            {
                OpenSim.Framework.RegionFlags? regionFlags = n.RegionFlags;

                //                m_log.DebugFormat(
                //                    "{0}: Region flags for {1} as seen by {2} are {3}",
                //                    LogHeader, n.RegionName, m_scene.Name, regionFlags != null ? regionFlags.ToString() : "not present");

                // Robust services before 2015-01-14 do not return the regionFlags information.  In this case, we could
                // make a separate RegionFlags call but this would involve a network call for each neighbour.
                if (regionFlags != null)
                {
                    if ((regionFlags & OpenSim.Framework.RegionFlags.RegionOnline) != 0)
                        onlineNeighbours.Add(n);
                }
                else
                {
                    onlineNeighbours.Add(n);
                }
            }

            m_log.DebugFormat(
                "{0} Informing {1} neighbours that region {2} is up",
                LogHeader, onlineNeighbours.Count, m_scene.Name);

            foreach (GridRegion n in onlineNeighbours)
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
            SendChildAgentDataUpdateDelegate icon = (SendChildAgentDataUpdateDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }

        public void SendChildAgentDataUpdate(AgentPosition cAgentData, ScenePresence presence)
        {
            //            m_log.DebugFormat(
            //                "[SCENE COMMUNICATION SERVICE]: Sending child agent position updates for {0} in {1}",
            //                presence.Name, m_scene.Name);

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
                        Util.RegionHandleToWorldLoc(regionHandle, out x, out y);
                        GridRegion dest = m_scene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                        if (dest == null)
                            continue;

                        if (!simulatorList.Contains(dest.ServerURI))
                        {
                            // we havent seen this simulator before, add it to the list
                            // and send it an update
                            simulatorList.Add(dest.ServerURI);
                            // Let move this to sync. Mono definitely does not like async networking.
                            m_scene.SimulationService.UpdateAgent(dest, cAgentData);

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
        protected void SendCloseChildAgent(UUID agentID, ulong regionHandle, string auth_token)
        {
            // let's do our best, but there's not much we can do if the neighbour doesn't accept.

            //m_commsProvider.InterRegion.TellRegionToCloseChildConnection(regionHandle, agentID);
            uint x = 0, y = 0;
            Util.RegionHandleToWorldLoc(regionHandle, out x, out y);

            GridRegion destination = m_scene.GridService.GetRegionByPosition(m_regionInfo.ScopeID, (int)x, (int)y);

            if (destination == null)
            {
                m_log.DebugFormat(
                    "[SCENE COMMUNICATION SERVICE]: Sending close agent ID {0} FAIL, region with handle {1} not found", agentID, regionHandle);
                return;
            }

            m_log.DebugFormat(
                "[SCENE COMMUNICATION SERVICE]: Sending close agent ID {0} to {1}", agentID, destination.RegionName);

            m_scene.SimulationService.CloseAgent(destination, agentID, auth_token);
        }

        /// <summary>
        /// Closes a child agents in a collection of regions. Does so asynchronously
        /// so that the caller doesn't wait.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="regionslst"></param>
        public void SendCloseChildAgentConnections(UUID agentID, string auth_code, List<ulong> regionslst)
        {
            if (regionslst.Count == 0)
                return;

            // use a single thread job for all
            Util.FireAndForget(o =>
            {
                foreach (ulong handle in regionslst)
                {
                    SendCloseChildAgent(agentID, handle, auth_code);
                }
            }, null, "SceneCommunicationService.SendCloseChildAgentConnections");
        }

        public List<GridRegion> RequestNamedRegions(string name, int maxNumber)
        {
            return m_scene.GridService.GetRegionsByName(UUID.Zero, name, maxNumber);
        }
    }
}
