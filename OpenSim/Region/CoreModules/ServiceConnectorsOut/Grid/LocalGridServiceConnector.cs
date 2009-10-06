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

using log4net;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class LocalGridServicesConnector :
            ISharedRegionModule, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private static LocalGridServicesConnector m_MainInstance;

        private IGridService m_GridService;
        private Dictionary<UUID, RegionCache> m_LocalCache = new Dictionary<UUID, RegionCache>();

        private bool m_Enabled = false;

        public LocalGridServicesConnector()
        {
        }

        public LocalGridServicesConnector(IConfigSource source)
        {
            m_log.Debug("[LOCAL GRID CONNECTOR]: LocalGridServicesConnector instantiated");
            m_MainInstance = this;
            InitialiseService(source);
        }

        #region ISharedRegionModule

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalGridServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("GridServices", "");
                if (name == Name)
                {
                    InitialiseService(source);
                    m_MainInstance = this;
                    m_Enabled = true;
                    m_log.Info("[LOCAL GRID CONNECTOR]: Local grid connector enabled");
                }
            }
        }

        private void InitialiseService(IConfigSource source)
        {
            IConfig assetConfig = source.Configs["GridService"];
            if (assetConfig == null)
            {
                m_log.Error("[LOCAL GRID CONNECTOR]: GridService missing from OpenSim.ini");
                return;
            }

            string serviceDll = assetConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (serviceDll == String.Empty)
            {
                m_log.Error("[LOCAL GRID CONNECTOR]: No LocalServiceModule named in section GridService");
                return;
            }

            Object[] args = new Object[] { source };
            m_GridService =
                    ServerUtils.LoadPlugin<IGridService>(serviceDll,
                    args);

            if (m_GridService == null)
            {
                m_log.Error("[LOCAL GRID CONNECTOR]: Can't load grid service");
                return;
            }
        }

        public void PostInitialise()
        {
            if (m_MainInstance == this)
            {
                MainConsole.Instance.Commands.AddCommand("LocalGridConnector", false, "show neighbours",
                    "show neighbours",
                    "Shows the local regions' neighbours", NeighboursCommand);
            }
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
                scene.RegisterModuleInterface<IGridService>(this);

            if (m_MainInstance == this)
            {
                if (m_LocalCache.ContainsKey(scene.RegionInfo.RegionID))
                    m_log.ErrorFormat("[LOCAL GRID CONNECTOR]: simulator seems to have more than one region with the same UUID. Please correct this!");
                else
                    m_LocalCache.Add(scene.RegionInfo.RegionID, new RegionCache(scene));
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_MainInstance == this)
            {
                m_LocalCache[scene.RegionInfo.RegionID].Clear();
                m_LocalCache.Remove(scene.RegionInfo.RegionID);
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

        #region IGridService

        public bool RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            return m_GridService.RegisterRegion(scopeID, regionInfo);
        }

        public bool DeregisterRegion(UUID regionID)
        {
            return m_GridService.DeregisterRegion(regionID);
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            if (m_LocalCache.ContainsKey(regionID))
            {
                List<GridRegion> neighbours = m_LocalCache[regionID].GetNeighbours();
                if (neighbours.Count == 0)
                    // try the DB
                    neighbours = m_GridService.GetNeighbours(scopeID, regionID);
                return neighbours;
            }
            else
            {
                m_log.WarnFormat("[LOCAL GRID CONNECTOR]: GetNeighbours: Requested region {0} is not on this sim", regionID);
                return new List<GridRegion>();
            }

            // Don't go to the DB
            //return m_GridService.GetNeighbours(scopeID, regionID);
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            return m_GridService.GetRegionByUUID(scopeID, regionID);
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            GridRegion region = null;

            // First see if it's a neighbour, even if it isn't on this sim.
            // Neighbour data is cached in memory, so this is fast
            foreach (RegionCache rcache in m_LocalCache.Values)
            {
                region = rcache.GetRegionByPosition(x, y);
                if (region != null)
                {
                    return region;
                }
            }

            // Then try on this sim (may be a lookup in DB if this is using MySql).
            return m_GridService.GetRegionByPosition(scopeID, x, y);
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            return m_GridService.GetRegionByName(scopeID, regionName);
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            return m_GridService.GetRegionsByName(scopeID, name, maxNumber);
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            return m_GridService.GetRegionRange(scopeID, xmin, xmax, ymin, ymax);
        }

        #endregion

        public void NeighboursCommand(string module, string[] cmdparams)
        {
            foreach (KeyValuePair<UUID, RegionCache> kvp in m_LocalCache)
            {
                m_log.InfoFormat("*** Neighbours of {0} {1} ***", kvp.Key, kvp.Value.RegionName);
                List<GridRegion> regions = kvp.Value.GetNeighbours();
                foreach (GridRegion r in regions)
                    m_log.InfoFormat("    {0} @ {1}={2}", r.RegionName, r.RegionLocX / Constants.RegionSize, r.RegionLocY / Constants.RegionSize);
            }
        }
    }
}
