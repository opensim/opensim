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
using Mono.Addins;
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
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalGridServicesConnector")]
    public class LocalGridServicesConnector : ISharedRegionModule, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[LOCAL GRID SERVICE CONNECTOR]";

        private IGridService m_GridService;
        private Dictionary<UUID, RegionCache> m_LocalCache = new Dictionary<UUID, RegionCache>();
        private RegionInfoCache m_RegionInfoCache = null;

        private bool m_Enabled;

        public LocalGridServicesConnector()
        {
            m_log.DebugFormat("{0} LocalGridServicesConnector no parms.", LogHeader);
        }

        public LocalGridServicesConnector(IConfigSource source, RegionInfoCache regionInfoCache)
        {
            m_log.DebugFormat("{0} LocalGridServicesConnector instantiated directly.", LogHeader);
            InitialiseService(source, regionInfoCache);
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
                    
                    if(InitialiseService(source,null))
                        m_log.Info("[LOCAL GRID SERVICE CONNECTOR]: Local grid connector enabled");
                }
            }
        }

        private bool InitialiseService(IConfigSource source, RegionInfoCache ric)
        {
            if(ric == null)
                m_RegionInfoCache = new RegionInfoCache();
            else
                m_RegionInfoCache = ric;

            IConfig config = source.Configs["GridService"];
            if (config == null)
            {
                m_log.Error("[LOCAL GRID SERVICE CONNECTOR]: GridService missing from OpenSim.ini");
                return false;
            }

            string serviceDll = config.GetString("LocalServiceModule", String.Empty);

            if (serviceDll == String.Empty)
            {
                m_log.Error("[LOCAL GRID SERVICE CONNECTOR]: No LocalServiceModule named in section GridService");
                return false;
            }

            Object[] args = new Object[] { source };
            m_GridService =
                    ServerUtils.LoadPlugin<IGridService>(serviceDll,
                    args);

            if (m_GridService == null)
            {
                m_log.Error("[LOCAL GRID SERVICE CONNECTOR]: Can't load grid service");
                return false;
            }

            m_Enabled = true;
            return true;
        }

        public void PostInitialise()
        {
            // FIXME: We will still add this command even if we aren't enabled since RemoteGridServiceConnector
            // will have instantiated us directly.
            MainConsole.Instance.Commands.AddCommand("Regions", false, "show neighbours",
                "show neighbours",
                "Shows the local regions' neighbours", HandleShowNeighboursCommand);
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IGridService>(this);

            lock (m_LocalCache)
            {
                if (m_LocalCache.ContainsKey(scene.RegionInfo.RegionID))
                    m_log.ErrorFormat("[LOCAL GRID SERVICE CONNECTOR]: simulator seems to have more than one region with the same UUID. Please correct this!");
                else
                    m_LocalCache.Add(scene.RegionInfo.RegionID, new RegionCache(scene));
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_LocalCache)
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

        public string RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            return m_GridService.RegisterRegion(scopeID, regionInfo);
        }

        public bool DeregisterRegion(UUID regionID)
        {
            return m_GridService.DeregisterRegion(regionID);
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            return m_GridService.GetNeighbours(scopeID, regionID); 
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            bool inCache = false;
            GridRegion rinfo = m_RegionInfoCache.Get(scopeID,regionID,out inCache);
            if (inCache)
                return rinfo;

            rinfo = m_GridService.GetRegionByUUID(scopeID, regionID);
            if(rinfo != null)
             m_RegionInfoCache.Cache(scopeID, rinfo);
            return rinfo;
        }

        // Get a region given its base coordinates.
        // NOTE: this is NOT 'get a region by some point in the region'. The coordinate MUST
        //     be the base coordinate of the region.
        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            // try in cache by handler first
            ulong regionHandle = Util.RegionWorldLocToHandle((uint)x, (uint)y);

            bool inCache = false;
            GridRegion rinfo = m_RegionInfoCache.Get(scopeID, regionHandle, out inCache);
            if (inCache)
                return rinfo;

            // try in cache by slower position next
            rinfo = m_RegionInfoCache.Get(scopeID, x, y, out inCache);
            if (inCache)
                return rinfo;

            // Then try on this sim (may be a lookup in DB if this is using MySql).
            rinfo = m_GridService.GetRegionByPosition(scopeID, x, y);
            if(rinfo != null)
                m_RegionInfoCache.Cache(scopeID, rinfo);
            return rinfo;
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            bool inCache = false;
            GridRegion rinfo = m_RegionInfoCache.Get(scopeID, regionName, out inCache);
            if (inCache)
                return rinfo;

            rinfo =  m_GridService.GetRegionByName(scopeID, regionName);
            if(rinfo != null)
                m_RegionInfoCache.Cache(scopeID, rinfo);
            return rinfo;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            return m_GridService.GetRegionsByName(scopeID, name, maxNumber);
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            return m_GridService.GetRegionRange(scopeID, xmin, xmax, ymin, ymax);
        }

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            return m_GridService.GetDefaultRegions(scopeID);
        }

        public List<GridRegion> GetDefaultHypergridRegions(UUID scopeID)
        {
            return m_GridService.GetDefaultHypergridRegions(scopeID);
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            return m_GridService.GetFallbackRegions(scopeID, x, y);
        }

        public List<GridRegion> GetHyperlinks(UUID scopeID)
        {
            return m_GridService.GetHyperlinks(scopeID);
        }
        
        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            return m_GridService.GetRegionFlags(scopeID, regionID);
        }

        public Dictionary<string, object> GetExtraFeatures()
        {
            return m_GridService.GetExtraFeatures();
        }

        #endregion

        public void HandleShowNeighboursCommand(string module, string[] cmdparams)
        {
            System.Text.StringBuilder caps = new System.Text.StringBuilder();

            lock (m_LocalCache)
            {
                foreach (KeyValuePair<UUID, RegionCache> kvp in m_LocalCache)
                {
                    caps.AppendFormat("*** Neighbours of {0} ({1}) ***\n", kvp.Value.RegionName, kvp.Key);
                    List<GridRegion> regions = kvp.Value.GetNeighbours();
                    foreach (GridRegion r in regions)
                        caps.AppendFormat("    {0} @ {1}-{2}\n", r.RegionName, Util.WorldToRegionLoc((uint)r.RegionLocX), Util.WorldToRegionLoc((uint)r.RegionLocY));
                }
            }

            MainConsole.Instance.Output(caps.ToString());
        }
    }
}
