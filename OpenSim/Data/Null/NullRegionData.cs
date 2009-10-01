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

namespace OpenSim.Data.Null
{
    public class NullRegionData : IRegionData
    {
        Dictionary<UUID, RegionData> m_regionData = new Dictionary<UUID, RegionData>();

        public NullRegionData(string connectionString, string realm)
        {
            //Console.WriteLine("[XXX] NullRegionData constructor");
        }

        public List<RegionData> Get(string regionName, UUID scopeID)
        {
            List<RegionData> ret = new List<RegionData>();

            foreach (RegionData r in m_regionData.Values)
            {
                if (regionName.Contains("%"))
                {
                    if (r.RegionName.Contains(regionName.Replace("%", "")))
                        ret.Add(r);
                }
                else
                {
                    if (r.RegionName == regionName)
                        ret.Add(r);
                }
            }

            if (ret.Count > 0)
                return ret;

            return null;
        }

        public RegionData Get(int posX, int posY, UUID scopeID)
        {
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
            if (m_regionData.ContainsKey(regionID))
                return m_regionData[regionID];

            return null;
        }

        public List<RegionData> Get(int startX, int startY, int endX, int endY, UUID scopeID)
        {
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
            m_regionData[data.RegionID] = data;

            return true;
        }

        public bool SetDataItem(UUID regionID, string item, string value)
        {
            if (!m_regionData.ContainsKey(regionID))
                return false;

            m_regionData[regionID].Data[item] = value;

            return true;
        }

        public bool Delete(UUID regionID)
        {
            if (!m_regionData.ContainsKey(regionID))
                return false;

            m_regionData.Remove(regionID);

            return true;
        }
    }
}
