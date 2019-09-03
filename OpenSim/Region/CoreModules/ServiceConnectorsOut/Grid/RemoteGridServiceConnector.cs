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
using System.Collections;
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
        private string m_ThisGatekeeperURI = string.Empty;
        private string m_ThisGatekeeperHost = string.Empty;
        private string m_ThisGatekeeperIP = string.Empty;

        private IGridService m_LocalGridService;
        private IGridService m_RemoteGridService;

        private RegionInfoCache m_RegionInfoCache;

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
                    if(InitialiseServices(source))
                    {
                        m_Enabled = true;
                        m_log.Info("[REMOTE GRID CONNECTOR]: Remote grid enabled");
                    }
                }
            }
        }

        private bool InitialiseServices(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig == null)
            {
                m_log.Error("[REMOTE GRID CONNECTOR]: GridService missing from OpenSim.ini");
                return false;
            }

            string networkConnector = gridConfig.GetString("NetworkConnector", string.Empty);
            if (networkConnector == string.Empty)
            {
                m_log.Error("[REMOTE GRID CONNECTOR]: Please specify a network connector under [GridService]");
                return false;
            }

            Object[] args = new Object[] { source };
            m_RemoteGridService = ServerUtils.LoadPlugin<IGridService>(networkConnector, args);

            m_LocalGridService = new LocalGridServicesConnector(source, m_RegionInfoCache);
            if (m_LocalGridService == null)
            {
                m_log.Error("[REMOTE GRID CONNECTOR]: failed to load local connector");
                return false;
            }

            if(m_RegionInfoCache == null)
                m_RegionInfoCache = new RegionInfoCache();

            m_ThisGatekeeperURI = Util.GetConfigVarFromSections<string>(source, "GatekeeperURI",
                new string[] { "Startup", "Hypergrid", "GridService" }, String.Empty);
            // Legacy. Remove soon!
            m_ThisGatekeeperURI = gridConfig.GetString("Gatekeeper", m_ThisGatekeeperURI);

            Util.checkServiceURI(m_ThisGatekeeperURI, out m_ThisGatekeeperURI, out m_ThisGatekeeperHost, out m_ThisGatekeeperIP);
            return true;
        }

        public void PostInitialise()
        {
            if (m_Enabled)
                ((ISharedRegionModule)m_LocalGridService).PostInitialise();
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.RegisterModuleInterface<IGridService>(this);
                ((ISharedRegionModule)m_LocalGridService).AddRegion(scene);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
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
            GridRegion rinfo = m_LocalGridService.GetRegionByUUID(scopeID, regionID);
            if (rinfo != null)
                return rinfo;

            rinfo = m_RemoteGridService.GetRegionByUUID(scopeID, regionID);
            m_RegionInfoCache.Cache(scopeID, rinfo);
            return rinfo;
        }

        // Get a region given its base world coordinates (in meters).
        // NOTE: this is NOT 'get a region by some point in the region'. The coordinate MUST
        //     be the base coordinate of the region.
        // The coordinates are world coords (meters), NOT region units.
        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            GridRegion rinfo = m_LocalGridService.GetRegionByPosition(scopeID, x, y);
            if (rinfo != null)
            {
//                m_log.DebugFormat("[REMOTE GRID CONNECTOR]: GetRegionByPosition. Found region {0} on local. Pos=<{1},{2}>, RegionHandle={3}",
//                    rinfo.RegionName, rinfo.RegionCoordX, rinfo.RegionCoordY, rinfo.RegionHandle);
                return rinfo;
            }

            rinfo = m_RemoteGridService.GetRegionByPosition(scopeID, x, y);

            if (rinfo == null)
            {
//                uint regionX = Util.WorldToRegionLoc((uint)x);
//                uint regionY = Util.WorldToRegionLoc((uint)y);
//                m_log.WarnFormat("[REMOTE GRID CONNECTOR]: Requested region {0}-{1} not found", regionX, regionY);
            }
            else
            {
                m_RegionInfoCache.Cache(scopeID, rinfo);

//                m_log.DebugFormat("[REMOTE GRID CONNECTOR]: GetRegionByPosition. Added region {0} to the cache. Pos=<{1},{2}>, RegionHandle={3}",
//                    rinfo.RegionName, rinfo.RegionCoordX, rinfo.RegionCoordY, rinfo.RegionHandle);
            }
            return rinfo;
        }

        public GridRegion GetRegionByName(UUID scopeID, string name)
        {
            GridRegion rinfo = m_LocalGridService.GetRegionByName(scopeID, name);
            if (rinfo != null)
                return rinfo;

              // HG urls should not get here, strip them
            // side effect is that local regions with same name as HG may also be found
            // this mb good or bad
            string regionName = name;
            if(name.Contains("."))
            {
                if(string.IsNullOrWhiteSpace(m_ThisGatekeeperIP))
                    return rinfo; // no HG

                string regionURI = "";
                string regionHost = "";
                if (!Util.buildHGRegionURI(name, out regionURI, out regionHost, out regionName))
                    return rinfo; // invalid
                if (!m_ThisGatekeeperHost.Equals(regionHost, StringComparison.InvariantCultureIgnoreCase) && !m_ThisGatekeeperIP.Equals(regionHost))
                    return rinfo; // not local grid
            }

            if (String.IsNullOrEmpty(regionName))
            {
                rinfo = m_RemoteGridService.GetDefaultRegions(UUID.Zero)[0];
                if (rinfo == null)
                    m_log.Warn("[REMOTE GRID CONNECTOR] returned null default region");
                else
                    m_log.WarnFormat("[REMOTE GRID CONNECTOR] returned default region {0}", rinfo.RegionName);
            }
            else
                rinfo = m_RemoteGridService.GetRegionByName(scopeID, regionName);
            m_RegionInfoCache.Cache(scopeID, rinfo);
            return rinfo;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            List<GridRegion> rinfo = m_LocalGridService.GetRegionsByName(scopeID, name, maxNumber);
            //m_log.DebugFormat("[REMOTE GRID CONNECTOR]: Local GetRegionsByName {0} found {1} regions", name, rinfo.Count);

            // HG urls should not get here, strip them
            // side effect is that local regions with same name as HG may also be found
            // this mb good or bad
            string regionName = name;
            if(name.Contains("."))
            {
                if(string.IsNullOrWhiteSpace(m_ThisGatekeeperURI))
                    return rinfo; // no HG

                string regionURI = "";
                string regionHost = "";
                if (!Util.buildHGRegionURI(name, out regionURI, out regionHost, out regionName))
                    return rinfo; // invalid
                if (!m_ThisGatekeeperHost.Equals(regionHost, StringComparison.InvariantCultureIgnoreCase) && !m_ThisGatekeeperIP.Equals(regionHost))
                    return rinfo; // not local grid
            }

            List<GridRegion> grinfo = null;
            if (String.IsNullOrEmpty(regionName))
            {
                List<GridRegion> grinfos = m_RemoteGridService.GetDefaultRegions(UUID.Zero);
                if (grinfos == null)
                    m_log.Warn("[REMOTE GRID CONNECTOR] returned null default regions");
                else
                {
                    m_log.WarnFormat("[REMOTE GRID CONNECTOR] returned default regions {0}, ...", grinfos[0].RegionName);
                    // only return first
                    grinfo = new List<GridRegion>(){grinfos[0]};
                }
            }
            else
                grinfo = m_RemoteGridService.GetRegionsByName(scopeID, regionName, maxNumber);

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

        public Dictionary<string, object> GetExtraFeatures()
        {
            Dictionary<string, object> extraFeatures;
            extraFeatures = m_LocalGridService.GetExtraFeatures();

            if (extraFeatures.Count == 0)
                extraFeatures = m_RemoteGridService.GetExtraFeatures();

            return extraFeatures;
        }
        #endregion
    }
}
