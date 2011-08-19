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
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using System.Reflection;
using log4net;

namespace OpenSim.Data.Null
{
    public class NullRegionData : IRegionData
    {
        private static NullRegionData Instance = null;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        Dictionary<UUID, RegionData> m_regionData = new Dictionary<UUID, RegionData>();

        public NullRegionData(string connectionString, string realm)
        {
            if (Instance == null)
                Instance = this;
            //Console.WriteLine("[XXX] NullRegionData constructor");
        }

        private delegate bool Matcher(string value);

        public List<RegionData> Get(string regionName, UUID scopeID)
        {
            if (Instance != this)
                return Instance.Get(regionName, scopeID);

            string cleanName = regionName.ToLower();

            // Handle SQL wildcards
            const string wildcard = "%";
            bool wildcardPrefix = false;
            bool wildcardSuffix = false;
            if (cleanName.Equals(wildcard))
            {
                wildcardPrefix = wildcardSuffix = true;
                cleanName = string.Empty;
            }
            else
            {
                if (cleanName.StartsWith(wildcard))
                {
                    wildcardPrefix = true;
                    cleanName = cleanName.Substring(1);
                }
                if (regionName.EndsWith(wildcard))
                {
                    wildcardSuffix = true;
                    cleanName = cleanName.Remove(cleanName.Length - 1);
                }
            }
            Matcher queryMatch;
            if (wildcardPrefix && wildcardSuffix)
                queryMatch = delegate(string s) { return s.Contains(cleanName); };
            else if (wildcardSuffix)
                queryMatch = delegate(string s) { return s.StartsWith(cleanName); };
            else if (wildcardPrefix)
                queryMatch = delegate(string s) { return s.EndsWith(cleanName); };
            else
                queryMatch = delegate(string s) { return s.Equals(cleanName); };

            // Find region data
            List<RegionData> ret = new List<RegionData>();

            foreach (RegionData r in m_regionData.Values)
            {
//                    m_log.DebugFormat("[NULL REGION DATA]: comparing {0} to {1}", cleanName, r.RegionName.ToLower());
                    if (queryMatch(r.RegionName.ToLower()))
                        ret.Add(r);
            }

            if (ret.Count > 0)
                return ret;

            return null;
        }

        public RegionData Get(int posX, int posY, UUID scopeID)
        {
            if (Instance != this)
                return Instance.Get(posX, posY, scopeID);

            List<RegionData> ret = new List<RegionData>();

            foreach (RegionData r in m_regionData.Values)
            {
                if (r.posX == posX && r.posY == posY)
                    ret.Add(r);
            }

            if (ret.Count > 0)
                return ret[0];

            return null;
        }

        public RegionData Get(UUID regionID, UUID scopeID)
        {
            if (Instance != this)
                return Instance.Get(regionID, scopeID);

            if (m_regionData.ContainsKey(regionID))
                return m_regionData[regionID];

            return null;
        }

        public List<RegionData> Get(int startX, int startY, int endX, int endY, UUID scopeID)
        {
            if (Instance != this)
                return Instance.Get(startX, startY, endX, endY, scopeID);

            List<RegionData> ret = new List<RegionData>();

            foreach (RegionData r in m_regionData.Values)
            {
                if (r.posX >= startX && r.posX <= endX && r.posY >= startY && r.posY <= endY)
                    ret.Add(r);
            }

            return ret;
        }

        public bool Store(RegionData data)
        {
            if (Instance != this)
                return Instance.Store(data);

            m_regionData[data.RegionID] = data;

            return true;
        }

        public bool SetDataItem(UUID regionID, string item, string value)
        {
            if (Instance != this)
                return Instance.SetDataItem(regionID, item, value);

            if (!m_regionData.ContainsKey(regionID))
                return false;

            m_regionData[regionID].Data[item] = value;

            return true;
        }

        public bool Delete(UUID regionID)
        {
            if (Instance != this)
                return Instance.Delete(regionID);

            if (!m_regionData.ContainsKey(regionID))
                return false;

            m_regionData.Remove(regionID);

            return true;
        }

        public List<RegionData> GetDefaultRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.DefaultRegion, scopeID);
        }

        public List<RegionData> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            List<RegionData> regions = Get((int)RegionFlags.FallbackRegion, scopeID);
            RegionDataDistanceCompare distanceComparer = new RegionDataDistanceCompare(x, y);
            regions.Sort(distanceComparer);
            return regions;
        }

        public List<RegionData> GetHyperlinks(UUID scopeID)
        {
            return Get((int)RegionFlags.Hyperlink, scopeID);
        }

        private List<RegionData> Get(int regionFlags, UUID scopeID)
        {
            if (Instance != this)
                return Instance.Get(regionFlags, scopeID);

            List<RegionData> ret = new List<RegionData>();

            foreach (RegionData r in m_regionData.Values)
            {
                if ((Convert.ToInt32(r.Data["flags"]) & regionFlags) != 0)
                    ret.Add(r);
            }

            return ret;
        }
    }
}
