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
using System.Reflection;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using log4net;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class RegionInfoCache
    {
        private const double CACHE_EXPIRATION_SECONDS = 300.0; // 5 minutes

//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);
        
        internal struct ScopedRegionUUID
        {
            public UUID m_scopeID;
            public UUID m_regionID;
            public ScopedRegionUUID(UUID scopeID, UUID regionID)
            {
                m_scopeID = scopeID;
                m_regionID = regionID;
            }
        }
            
        internal struct ScopedRegionName
        {
            public UUID m_scopeID;
            public string m_name;
            public ScopedRegionName(UUID scopeID, string name)
            {
                m_scopeID = scopeID;
                m_name = name;
            }
        }

        internal struct ScopedRegionPosition
        {
            public UUID m_scopeID;
            public ulong m_regionHandle;
            public ScopedRegionPosition(UUID scopeID, ulong handle)
            {
                m_scopeID = scopeID;
                m_regionHandle = handle;
            }
        }

        private ExpiringCache<ScopedRegionUUID, GridRegion> m_UUIDCache;
        private ExpiringCache<ScopedRegionName, ScopedRegionUUID> m_NameCache;
        private ExpiringCache<ScopedRegionPosition, GridRegion> m_PositionCache;

        public RegionInfoCache()
        {
            m_UUIDCache = new ExpiringCache<ScopedRegionUUID, GridRegion>();
            m_NameCache = new ExpiringCache<ScopedRegionName, ScopedRegionUUID>();
            m_PositionCache = new ExpiringCache<ScopedRegionPosition, GridRegion>();
        }

        public void Cache(GridRegion rinfo)
        {
            if (rinfo != null)
                this.Cache(rinfo.ScopeID,rinfo.RegionID,rinfo);
        }
        
        public void Cache(UUID scopeID, UUID regionID, GridRegion rinfo)
        {
            // for now, do not cache negative results; this is because
            // we need to figure out how to handle regions coming online
            // in a timely way
            if (rinfo == null)
                return;
            
            ScopedRegionUUID id = new ScopedRegionUUID(scopeID,regionID);
            
            // Cache even null accounts
            m_UUIDCache.AddOrUpdate(id, rinfo, CACHE_EXPIRATION_SECONDS);
            if (rinfo != null)
            {
                ScopedRegionName name = new ScopedRegionName(scopeID,rinfo.RegionName);
                m_NameCache.AddOrUpdate(name, id, CACHE_EXPIRATION_SECONDS);

                ScopedRegionPosition pos = new ScopedRegionPosition(scopeID, rinfo.RegionHandle);
                m_PositionCache.AddOrUpdate(pos, rinfo, CACHE_EXPIRATION_SECONDS);
            }
        }

        public GridRegion Get(UUID scopeID, UUID regionID, out bool inCache)
        {
            inCache = false;

            GridRegion rinfo = null;
            ScopedRegionUUID id = new ScopedRegionUUID(scopeID,regionID);
            if (m_UUIDCache.TryGetValue(id, out rinfo))
            {
                inCache = true;
                return rinfo;
            }

            return null;
        }

        public GridRegion Get(UUID scopeID, ulong handle, out bool inCache)
        {
            inCache = false;

            GridRegion rinfo = null;
            ScopedRegionPosition pos = new ScopedRegionPosition(scopeID, handle);
            if (m_PositionCache.TryGetValue(pos, out rinfo))
            {
                inCache = true;
                return rinfo;
            }

            return null;
        }


        public GridRegion Get(UUID scopeID, string name, out bool inCache)
        {
            inCache = false;

            ScopedRegionName sname = new ScopedRegionName(scopeID,name);

            ScopedRegionUUID id;
            if (m_NameCache.TryGetValue(sname, out id))
            {
                GridRegion rinfo = null;
                if (m_UUIDCache.TryGetValue(id, out rinfo))
                {
                    inCache = true;
                    return rinfo;
                }
            }
            
            return null;
        }
    }
}
