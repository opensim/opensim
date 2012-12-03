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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public class RegionData
    {
        public UUID RegionID;
        public UUID ScopeID;
        public string RegionName;

        /// <summary>
        /// The position in meters of this region.
        /// </summary>
        public int posX;

        /// <summary>
        /// The position in meters of this region.
        /// </summary>
        public int posY;

        public int sizeX;
        public int sizeY;

        /// <summary>
        /// Return the x-coordinate of this region.
        /// </summary>
        public int coordX { get { return posX / (int)Constants.RegionSize; } }

        /// <summary>
        /// Return the y-coordinate of this region.
        /// </summary>
        public int coordY { get { return posY / (int)Constants.RegionSize; } }

        public Dictionary<string, object> Data;
    }

    /// <summary>
    /// An interface for connecting to the authentication datastore
    /// </summary>
    public interface IRegionData 
    {
        RegionData Get(UUID regionID, UUID ScopeID);
        List<RegionData> Get(string regionName, UUID ScopeID);
        RegionData Get(int x, int y, UUID ScopeID);
        List<RegionData> Get(int xStart, int yStart, int xEnd, int yEnd, UUID ScopeID);

        bool Store(RegionData data);

        bool SetDataItem(UUID principalID, string item, string value);

        bool Delete(UUID regionID);

        List<RegionData> GetDefaultRegions(UUID scopeID);
        List<RegionData> GetFallbackRegions(UUID scopeID, int x, int y);
        List<RegionData> GetHyperlinks(UUID scopeID);
    }

    public class RegionDataDistanceCompare : IComparer<RegionData>
    {
        private Vector2 m_origin;

        public RegionDataDistanceCompare(int x, int y)
        {
            m_origin = new Vector2(x, y);
        }

        public int Compare(RegionData regionA, RegionData regionB)
        {
            Vector2 vectorA = new Vector2(regionA.posX, regionA.posY);
            Vector2 vectorB = new Vector2(regionB.posX, regionB.posY);
            return Math.Sign(VectorDistance(m_origin, vectorA) - VectorDistance(m_origin, vectorB));
        }

        private float VectorDistance(Vector2 x, Vector2 y)
        {
            return (x - y).Length();
        }
    }
}
