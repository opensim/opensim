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
using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Services.Connectors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RemoteGridServicesConnector")]
    public class RemoteGridServicesConnector : ISharedRegionModule, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;

        private IGridService m_LocalGridService;
        private IGridService m_RemoteGridService;

        private RegionInfoCache m_RegionInfoCache = new RegionInfoCache();
        
        public RemoteGridServicesConnector()
        {
        }

        public RemoteGridServicesConnector(IConfigSource source)
        {
            InitialiseServices(source);
        }

        #region ISharedRegionmodule

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteGridServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("GridServices", "");
                if (name == Name)
                {
                    InitialiseServices(source);
                    m_Enabled = true;
                    m_log.Info("[REMOTE GRID CONNECTOR]: Remote grid enabled");
                }
            }
        }

        private void InitialiseServices(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig == null)
            {
                m_log.Error("[REMOTE GRID CONNECTOR]: GridService missing from OpenSim.ini");
                return;
            }

            string networkConnector = gridConfig.GetString("NetworkConnector", string.Empty);
            if (networkConnector == string.Empty)
            {
                m_log.Error("[REMOTE GRID CONNECTOR]: Please specify a network connector under [GridService]");
                return;
            }

            Object[] args = new Object[] { source }; 
            m_RemoteGridService = ServerUtils.LoadPlugin<IGridService>(networkConnector, args);

            m_LocalGridService = new LocalGridServicesConnector(source);
        }   

        public void PostInitialise()
        {
            if (m_LocalGridService != null)
                ((ISharedRegionModule)m_LocalGridService).PostInitialise();
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
                scene.RegisterModuleInterface<IGridService>(this);

            if (m_LocalGridService != null)
                ((ISharedRegionModule)m_LocalGridService).AddRegion(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_LocalGridService != null)
                ((ISharedRegionModule)m_LocalGridService).RemoveRegion(scene);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

        #region IGridService

        public string RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            string msg = m_LocalGridService.RegisterRegion(scopeID, regionInfo);

            if (msg == String.Empty)
                return m_RemoteGridService.RegisterRegion(scopeID, regionInfo);

            return msg;
        }

        public bool DeregisterRegion(UUID regionID)
        {
            if (m_LocalGridService.DeregisterRegion(regionID))
                return m_RemoteGridService.DeregisterRegion(regionID);

            return false;
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            return m_RemoteGridService.GetNeighbours(scopeID, regionID);
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            bool inCache = false;
            GridRegion rinfo = m_RegionInfoCache.Get(scopeID,regionID,out inCache);
            if (inCache)
                return rinfo;
            
            rinfo = m_LocalGridService.GetRegionByUUID(scopeID, regionID);
            if (rinfo == null)
                rinfo = m_RemoteGridService.GetRegionByUUID(scopeID, regionID);

            m_RegionInfoCache.Cache(scopeID,regionID,rinfo);
            return rinfo;
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            bool inCache = false;
            GridRegion rinfo = m_RegionInfoCache.Get(scopeID, Util.UIntsToLong((uint)x, (uint)y), out inCache);
            if (inCache)
                return rinfo;

            rinfo = m_LocalGridService.GetRegionByPosition(scopeID, x, y);
            if (rinfo == null)
                rinfo = m_RemoteGridService.GetRegionByPosition(scopeID, x, y);

            m_RegionInfoCache.Cache(rinfo);
            return rinfo;
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            bool inCache = false;
            GridRegion rinfo = m_RegionInfoCache.Get(scopeID,regionName, out inCache);
            if (inCache)
                return rinfo;
            
            rinfo = m_LocalGridService.GetRegionByName(scopeID, regionName);
            if (rinfo == null)
                rinfo = m_RemoteGridService.GetRegionByName(scopeID, regionName);

            // can't cache negative results for name lookups
            m_RegionInfoCache.Cache(rinfo);
            return rinfo;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            List<GridRegion> rinfo = m_LocalGridService.GetRegionsByName(scopeID, name, maxNumber);
            //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Local GetRegionsByName {0} found {1} regions", name, rinfo.Count);
            List<GridRegion> grinfo = m_RemoteGridService.GetRegionsByName(scopeID, name, maxNumber);

            if (grinfo != null)
            {
                //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Remote GetRegionsByName {0} found {1} regions", name, grinfo.Count);
                foreach (GridRegion r in grinfo)
                {
                    m_RegionInfoCache.Cache(r);
                    if (rinfo.Find(delegate(GridRegion gr) { return gr.RegionID == r.RegionID; }) == null)
                        rinfo.Add(r);
                }
            }

            return rinfo;
        }

        public virtual List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            List<GridRegion> rinfo = m_LocalGridService.GetRegionRange(scopeID, xmin, xmax, ymin, ymax);
            //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Local GetRegionRange {0} found {1} regions", name, rinfo.Count);
            List<GridRegion> grinfo = m_RemoteGridService.GetRegionRange(scopeID, xmin, xmax, ymin, ymax);

            if (grinfo != null)
            {
                //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Remote GetRegionRange {0} found {1} regions", name, grinfo.Count);
                foreach (GridRegion r in grinfo)
                {
                    m_RegionInfoCache.Cache(r);
                    if (rinfo.Find(delegate(GridRegion gr) { return gr.RegionID == r.RegionID; }) == null)
                        rinfo.Add(r);
                }
            }

            return rinfo;
        }

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            List<GridRegion> rinfo = m_LocalGridService.GetDefaultRegions(scopeID);
            //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Local GetDefaultRegions {0} found {1} regions", name, rinfo.Count);
            List<GridRegion> grinfo = m_RemoteGridService.GetDefaultRegions(scopeID);

            if (grinfo != null)
            {
                //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Remote GetDefaultRegions {0} found {1} regions", name, grinfo.Count);
                foreach (GridRegion r in grinfo)
                {
                    m_RegionInfoCache.Cache(r);
                    if (rinfo.Find(delegate(GridRegion gr) { return gr.RegionID == r.RegionID; }) == null)
                        rinfo.Add(r);
                }
            }

            return rinfo;
        }

        public List<GridRegion> GetDefaultHypergridRegions(UUID scopeID)
        {
            List<GridRegion> rinfo = m_LocalGridService.GetDefaultHypergridRegions(scopeID);
            //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Local GetDefaultHypergridRegions {0} found {1} regions", name, rinfo.Count);
            List<GridRegion> grinfo = m_RemoteGridService.GetDefaultHypergridRegions(scopeID);

            if (grinfo != null)
            {
                //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Remote GetDefaultHypergridRegions {0} found {1} regions", name, grinfo.Count);
                foreach (GridRegion r in grinfo)
                {
                    m_RegionInfoCache.Cache(r);
                    if (rinfo.Find(delegate(GridRegion gr) { return gr.RegionID == r.RegionID; }) == null)
                        rinfo.Add(r);
                }
            }

            return rinfo;
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            List<GridRegion> rinfo = m_LocalGridService.GetFallbackRegions(scopeID, x, y);
            //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Local GetFallbackRegions {0} found {1} regions", name, rinfo.Count);
            List<GridRegion> grinfo = m_RemoteGridService.GetFallbackRegions(scopeID, x, y);

            if (grinfo != null)
            {
                //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Remote GetFallbackRegions {0} found {1} regions", name, grinfo.Count);
                foreach (GridRegion r in grinfo)
                {
                    m_RegionInfoCache.Cache(r);
                    if (rinfo.Find(delegate(GridRegion gr) { return gr.RegionID == r.RegionID; }) == null)
                        rinfo.Add(r);
                }
            }

            return rinfo;
        }

        public List<GridRegion> GetHyperlinks(UUID scopeID)
        {
            List<GridRegion> rinfo = m_LocalGridService.GetHyperlinks(scopeID);
            //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Local GetHyperlinks {0} found {1} regions", name, rinfo.Count);
            List<GridRegion> grinfo = m_RemoteGridService.GetHyperlinks(scopeID);

            if (grinfo != null)
            {
                //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Remote GetHyperlinks {0} found {1} regions", name, grinfo.Count);
                foreach (GridRegion r in grinfo)
                {
                    m_RegionInfoCache.Cache(r);
                    if (rinfo.Find(delegate(GridRegion gr) { return gr.RegionID == r.RegionID; }) == null)
                        rinfo.Add(r);
                }
            }

            return rinfo;
        }

        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            int flags = m_LocalGridService.GetRegionFlags(scopeID, regionID);
            if (flags == -1)
                flags = m_RemoteGridService.GetRegionFlags(scopeID, regionID);

            return flags;
        }
        #endregion
    }
}
