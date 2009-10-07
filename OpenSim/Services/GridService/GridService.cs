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
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;

namespace OpenSim.Services.GridService
{
    public class GridService : GridServiceBase, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public GridService(IConfigSource config)
            : base(config)
        {
            m_log.DebugFormat("[GRID SERVICE]: Starting...");
        }

        #region IGridService

        public bool RegisterRegion(UUID scopeID, GridRegion regionInfos)
        {
            // This needs better sanity testing. What if regionInfo is registering in
            // overlapping coords?
            RegionData region = m_Database.Get(regionInfos.RegionLocX, regionInfos.RegionLocY, scopeID);
            if ((region != null) && (region.RegionID != regionInfos.RegionID))
            {
                m_log.WarnFormat("[GRID SERVICE]: Region {0} tried to register in coordinates {1}, {2} which are already in use in scope {3}.", 
                    regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY, scopeID);
                return false;
            }
            if ((region != null) && (region.RegionID == regionInfos.RegionID) && 
                ((region.posX != regionInfos.RegionLocX) || (region.posY != regionInfos.RegionLocY)))
            {
                // Region reregistering in other coordinates. Delete the old entry
                m_log.DebugFormat("[GRID SERVICE]: Region {0} ({1}) was previously registered at {2}-{3}. Deleting old entry.",
                    regionInfos.RegionName, regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY);

                try
                {
                    m_Database.Delete(regionInfos.RegionID);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[GRID SERVICE]: Database exception: {0}", e);
                }
            }

            // Everything is ok, let's register
            RegionData rdata = RegionInfo2RegionData(regionInfos);
            rdata.ScopeID = scopeID;
            try
            {
                m_Database.Store(rdata);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID SERVICE]: Database exception: {0}", e);
            }

            m_log.DebugFormat("[GRID SERVICE]: Region {0} ({1}) registered successfully at {2}-{3}", 
                regionInfos.RegionName, regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY);

            return true;
        }

        public bool DeregisterRegion(UUID regionID)
        {
            m_log.DebugFormat("[GRID SERVICE]: Region {0} deregistered", regionID);
            return m_Database.Delete(regionID);
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            List<GridRegion> rinfos = new List<GridRegion>();
            RegionData region = m_Database.Get(regionID, scopeID);
            if (region != null)
            {
                // Not really? Maybe?
                List<RegionData> rdatas = m_Database.Get(region.posX - (int)Constants.RegionSize, region.posY - (int)Constants.RegionSize, 
                    region.posX + (int)Constants.RegionSize, region.posY + (int)Constants.RegionSize, scopeID);

                foreach (RegionData rdata in rdatas)
                    if (rdata.RegionID != regionID)
                        rinfos.Add(RegionData2RegionInfo(rdata));

            }
            return rinfos;
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            RegionData rdata = m_Database.Get(regionID, scopeID);
            if (rdata != null)
                return RegionData2RegionInfo(rdata);

            return null;
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            int snapX = (int)(x / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapY = (int)(y / Constants.RegionSize) * (int)Constants.RegionSize;
            RegionData rdata = m_Database.Get(snapX, snapY, scopeID);
            if (rdata != null)
                return RegionData2RegionInfo(rdata);

            return null;
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            List<RegionData> rdatas = m_Database.Get(regionName + "%", scopeID);
            if ((rdatas != null) && (rdatas.Count > 0))
                return RegionData2RegionInfo(rdatas[0]); // get the first

            return null;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            List<RegionData> rdatas = m_Database.Get("%" + name + "%", scopeID);

            int count = 0;
            List<GridRegion> rinfos = new List<GridRegion>();

            if (rdatas != null)
            {
                foreach (RegionData rdata in rdatas)
                {
                    if (count++ < maxNumber)
                        rinfos.Add(RegionData2RegionInfo(rdata));
                }
            }

            return rinfos;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            int xminSnap = (int)(xmin / Constants.RegionSize) * (int)Constants.RegionSize;
            int xmaxSnap = (int)(xmax / Constants.RegionSize) * (int)Constants.RegionSize;
            int yminSnap = (int)(ymin / Constants.RegionSize) * (int)Constants.RegionSize;
            int ymaxSnap = (int)(ymax / Constants.RegionSize) * (int)Constants.RegionSize;

            List<RegionData> rdatas = m_Database.Get(xminSnap, yminSnap, xmaxSnap, ymaxSnap, scopeID);
            List<GridRegion> rinfos = new List<GridRegion>();
            foreach (RegionData rdata in rdatas)
                rinfos.Add(RegionData2RegionInfo(rdata));

            return rinfos;
        }

        #endregion

        #region Data structure conversions

        protected RegionData RegionInfo2RegionData(GridRegion rinfo)
        {
            RegionData rdata = new RegionData();
            rdata.posX = (int)rinfo.RegionLocX;
            rdata.posY = (int)rinfo.RegionLocY;
            rdata.RegionID = rinfo.RegionID;
            rdata.RegionName = rinfo.RegionName;
            rdata.Data = rinfo.ToKeyValuePairs();
            rdata.Data["regionHandle"] = Utils.UIntsToLong((uint)rdata.posX, (uint)rdata.posY);
            rdata.Data["owner_uuid"] = rinfo.EstateOwner.ToString();
            return rdata;
        }

        protected GridRegion RegionData2RegionInfo(RegionData rdata)
        {
            GridRegion rinfo = new GridRegion(rdata.Data);
            rinfo.RegionLocX = rdata.posX;
            rinfo.RegionLocY = rdata.posY;
            rinfo.RegionID = rdata.RegionID;
            rinfo.RegionName = rdata.RegionName;
            rinfo.ScopeID = rdata.ScopeID;

            return rinfo;
        }

        #endregion 

    }
}
