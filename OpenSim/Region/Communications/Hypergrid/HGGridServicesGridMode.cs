/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */

using System;
using System.Collections.Generic;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Environment.Scenes;

using OpenMetaverse;

using log4net;

namespace OpenSim.Region.Communications.Hypergrid
{
    public class HGGridServicesGridMode : HGGridServices
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Encapsulate remote backend services for manipulation of grid regions
        /// </summary>
        private OGS1GridServices m_remoteBackend = null;

        public OGS1GridServices RemoteBackend
        {
            get { return m_remoteBackend; }
        }


        public override string gdebugRegionName
        {
            get { return m_remoteBackend.gdebugRegionName; }
            set { m_remoteBackend.gdebugRegionName = value; }
        }

        public override bool RegionLoginsEnabled
        {
            get { return m_remoteBackend.RegionLoginsEnabled; }
            set { m_remoteBackend.RegionLoginsEnabled = value; }
        }      

        public HGGridServicesGridMode(NetworkServersInfo servers_info, BaseHttpServer httpServe, 
            AssetCache asscache, SceneManager sman, UserProfileCacheService userv)
            : base(servers_info, httpServe, asscache, sman)
        {
            m_remoteBackend = new OGS1GridServices(servers_info, httpServe);
            // Let's deregister this, so we can handle it here first
            InterRegionSingleton.Instance.OnChildAgent -= m_remoteBackend.IncomingChildAgent;
            InterRegionSingleton.Instance.OnChildAgent += IncomingChildAgent;
            m_userProfileCache = userv;
        }

        #region IGridServices interface

        public override RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            if (!regionInfo.RegionID.Equals(UUID.Zero))
            {
                m_regionsOnInstance.Add(regionInfo);
                return m_remoteBackend.RegisterRegion(regionInfo);
            }
            else
                return base.RegisterRegion(regionInfo);
        }

        public override bool DeregisterRegion(RegionInfo regionInfo)
        {
            bool success = m_remoteBackend.DeregisterRegion(regionInfo);
            if (!success)
                success = base.DeregisterRegion(regionInfo);
            return success;
        }

        public override List<SimpleRegionInfo> RequestNeighbours(uint x, uint y)
        {
            List<SimpleRegionInfo> neighbours = m_remoteBackend.RequestNeighbours(x, y);
            List<SimpleRegionInfo> remotes = base.RequestNeighbours(x, y);
            neighbours.AddRange(remotes);

            return neighbours;
        }

        public override RegionInfo RequestNeighbourInfo(UUID Region_UUID)
        {
            RegionInfo info = m_remoteBackend.RequestNeighbourInfo(Region_UUID);
            if (info == null)
                info = base.RequestNeighbourInfo(Region_UUID);
            return info;
        }

        public override RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            RegionInfo info = m_remoteBackend.RequestNeighbourInfo(regionHandle);
            if (info == null)
                info = base.RequestNeighbourInfo(regionHandle);
            return info;
        }

        public override RegionInfo RequestClosestRegion(string regionName)
        {
            RegionInfo info = m_remoteBackend.RequestClosestRegion(regionName);
            if (info == null)
                info = base.RequestClosestRegion(regionName);
            return info;
        }

        public override List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            List<MapBlockData> neighbours = m_remoteBackend.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
            List<MapBlockData> remotes = base.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
            neighbours.AddRange(remotes);

            return neighbours;
        }

        public override LandData RequestLandData(ulong regionHandle, uint x, uint y)
        {
            LandData land = m_remoteBackend.RequestLandData(regionHandle, x, y);
            if (land == null)
                land = base.RequestLandData(regionHandle, x, y);
            return land;
        }

        public override List<RegionInfo> RequestNamedRegions(string name, int maxNumber)
        {
            List<RegionInfo> infos = m_remoteBackend.RequestNamedRegions(name, maxNumber);
            List<RegionInfo> remotes = base.RequestNamedRegions(name, maxNumber);
            infos.AddRange(remotes);
            return infos;
        }

        #endregion

        #region IInterRegionCommunications interface

        public override bool AcknowledgeAgentCrossed(ulong regionHandle, UUID agentId)
        {
            return m_remoteBackend.AcknowledgeAgentCrossed(regionHandle, agentId);
        }

        public override bool AcknowledgePrimCrossed(ulong regionHandle, UUID primID)
        {
            return m_remoteBackend.AcknowledgePrimCrossed(regionHandle, primID);
        }

        public override bool CheckRegion(string address, uint port)
        {
            return m_remoteBackend.CheckRegion(address, port);
        }

        public override bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            return m_remoteBackend.ChildAgentUpdate(regionHandle, cAgentData);
        }

        public override bool ExpectAvatarCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying)
        {
            if (base.ExpectAvatarCrossing(regionHandle, agentID, position, isFlying))
                return true;
            return m_remoteBackend.ExpectAvatarCrossing(regionHandle, agentID, position, isFlying);
        }

        public override bool ExpectPrimCrossing(ulong regionHandle, UUID primID, Vector3 position, bool isFlying)
        {
            return m_remoteBackend.ExpectPrimCrossing(regionHandle, primID, position, isFlying);
        }

        public override bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            CachedUserInfo user = m_userProfileCache.GetUserDetails(agentData.AgentID);

            if (IsLocalUser(user))
            {
                Console.WriteLine("XXX Home User XXX");
                if (IsHyperlinkRegion(regionHandle))
                {
                    Console.WriteLine("XXX Going Hyperlink XXX");
                    return base.InformRegionOfChildAgent(regionHandle, agentData);
                }
                else
                {
                    // non-hypergrid case
                    Console.WriteLine("XXX Going local-grid region XXX");
                    return m_remoteBackend.InformRegionOfChildAgent(regionHandle, agentData);
                }
            }

            // Foregin users 
            Console.WriteLine("XXX Foreign User XXX");
            if (IsLocalRegion(regionHandle)) // regions on the same instance
            {
                Console.WriteLine("XXX Going onInstance region XXX");
                return m_remoteBackend.InformRegionOfChildAgent(regionHandle, agentData);
            }

            if (IsHyperlinkRegion(regionHandle)) // hyperlinked regions
            {
                Console.WriteLine("XXX Going Hyperlink XXX");
                return base.InformRegionOfChildAgent(regionHandle, agentData);
            }
            else
            {
                // foreign user going to a non-local region on the same grid
                // We need to inform that region about this user before
                // proceeding to the normal backend process.
                Console.WriteLine("XXX Going local-grid region XXX");
                RegionInfo regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                    InformRegionOfUser(regInfo, agentData);
                return m_remoteBackend.InformRegionOfChildAgent(regionHandle, agentData);
            }

        }

        public override bool InformRegionOfPrimCrossing(ulong regionHandle, UUID primID, string objData, int XMLMethod)
        {
            return m_remoteBackend.InformRegionOfPrimCrossing(regionHandle, primID, objData, XMLMethod);
        }

        public override bool RegionUp(SerializableRegionInfo region, ulong regionhandle)
        {
            if (m_remoteBackend.RegionUp(region, regionhandle))
                return true;
            return base.RegionUp(region, regionhandle);
        }

        public override bool TellRegionToCloseChildConnection(ulong regionHandle, UUID agentID)
        {
            return m_remoteBackend.TellRegionToCloseChildConnection(regionHandle, agentID);
        }


        #endregion

        #region Methods triggered by calls from external instances

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool IncomingChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            HGIncomingChildAgent(regionHandle, agentData);

            m_log.Info("[HGrid]: Incoming HGrid Agent " + agentData.firstname + " " + agentData.lastname);

            return m_remoteBackend.IncomingChildAgent(regionHandle, agentData);
        }
        #endregion

    }
}
