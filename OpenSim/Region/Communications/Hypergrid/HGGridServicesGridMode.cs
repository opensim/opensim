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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Communications.Hypergrid
{
    public class HGGridServicesGridMode : HGGridServices
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
            IAssetCache asscache, SceneManager sman, UserProfileCacheService userv)
            : base(servers_info, httpServe, asscache, sman)
        {
            m_remoteBackend = new OGS1GridServices(servers_info, httpServe);
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
            bool success = base.DeregisterRegion(regionInfo); 
            if (!success)
                success = m_remoteBackend.DeregisterRegion(regionInfo);
            return success;
        }

        public override List<SimpleRegionInfo> RequestNeighbours(uint x, uint y)
        {
            List<SimpleRegionInfo> neighbours = m_remoteBackend.RequestNeighbours(x, y);
            //List<SimpleRegionInfo> remotes = base.RequestNeighbours(x, y);
            //neighbours.AddRange(remotes);

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
            RegionInfo info = base.RequestNeighbourInfo(regionHandle);
            if (info == null)
                info = m_remoteBackend.RequestNeighbourInfo(regionHandle);
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


    }
}
