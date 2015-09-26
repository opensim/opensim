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

using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class RegionCache
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private Dictionary<ulong, GridRegion> m_neighbours = new Dictionary<ulong, GridRegion>();

        public string RegionName
        {
            get { return m_scene.RegionInfo.RegionName; }
        }

        public RegionCache(Scene s)
        {
            m_scene = s;
            m_scene.EventManager.OnRegionUp += OnRegionUp;
        }

        private void OnRegionUp(GridRegion otherRegion)
        {
            // This shouldn't happen
            if (otherRegion == null)
                return;

            m_log.DebugFormat("[REGION CACHE]: (on region {0}) Region {1} is up @ {2}-{3}",
                m_scene.RegionInfo.RegionName, otherRegion.RegionName, Util.WorldToRegionLoc((uint)otherRegion.RegionLocX), Util.WorldToRegionLoc((uint)otherRegion.RegionLocY));

            m_neighbours[otherRegion.RegionHandle] = otherRegion;
        }

        public void Clear()
        {
            m_scene.EventManager.OnRegionUp -= OnRegionUp;
            m_neighbours.Clear();
        }

        public List<GridRegion> GetNeighbours()
        {
            return new List<GridRegion>(m_neighbours.Values);
        }
 
        public GridRegion GetRegionByPosition(int x, int y)
        {
            // do actual search by position
            // not the best, but this will not hold that many regions
            GridRegion foundRegion = null;
            foreach(GridRegion r in m_neighbours.Values)
            {
                if (x >= r.RegionLocX && x < r.RegionLocX + r.RegionSizeX
                     && y >= r.RegionLocY && y < r.RegionLocY + r.RegionSizeY)
                {
                    foundRegion = r;
                    break;
                }
            }

            return foundRegion;
        }
    }
}
