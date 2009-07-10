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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.Interfaces
{
    public class GridService : IGridService
    {
        bool RegisterRegion(UUID scopeID, RegionInfo regionInfos);
        {
            return false;
        }

        bool DeregisterRegion(UUID regionID);   
        {
            return false;
        }

        List<SimpleRegionInfo> RequestNeighbours(UUID scopeID, uint x, uint y)
        {
            return new List<SimpleRegionInfo>()
        }

        RegionInfo RequestNeighbourInfo(UUID regionID)
        {
            return null;
        }

        RegionInfo RequestClosestRegion(UUID scopeID, string regionName)
        {
            return null;
        }

        List<MapBlockData> RequestNeighbourMapBlocks(UUID scopeID, int minX,
                int minY, int maxX, int maxY)
        {
            return new List<MapBlockData>();
        }

        LandData RequestLandData(UUID scopeID, ulong regionHandle,
                uint x, uint y)
        {
            return null;
        }

        List<RegionInfo> RequestNamedRegions(UUID scopeID, string name,
                int maxNumber)
        {
            return new List<RegionInfo>();
        }
    }
}
