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
using System.Net;
using System.Reflection;
using System.Xml;


using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Hypergrid;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;
using OpenSim.Server.Base;
using OpenSim.Framework.Console;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class HGGridConnector : ISharedRegionModule, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private bool m_Initialized = false;

        private Scene m_aScene;
        private Dictionary<ulong, Scene> m_LocalScenes = new Dictionary<ulong, Scene>();

        private IGridService m_GridServiceConnector;
        private IHypergridService m_HypergridService;


        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "HGGridServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("GridServices", "");
                if (name == Name)
                {
                    IConfig gridConfig = source.Configs["GridService"];
                    if (gridConfig == null)
                    {
                        m_log.Error("[HGGRID CONNECTOR]: GridService missing from OpenSim.ini");
                        return;
                    }


                    InitialiseConnectorModule(source);
                    
                    m_Enabled = true;
                    m_log.Info("[HGGRID CONNECTOR]: HG grid enabled");
                }
            }
        }

        private void InitialiseConnectorModule(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig == null)
            {
                m_log.Error("[HGGRID CONNECTOR]: GridService missing from OpenSim.ini");
                throw new Exception("Grid connector init error");
            }

            string module = gridConfig.GetString("GridServiceConnectorModule", String.Empty);
            if (module == String.Empty)
            {
                m_log.Error("[HGGRID CONNECTOR]: No GridServiceConnectorModule named in section GridService");
                throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
            }

            Object[] args = new Object[] { source };
            m_GridServiceConnector = ServerUtils.LoadPlugin<IGridService>(module, args);

            string hypergrid = gridConfig.GetString("HypergridService", string.Empty);
            if (hypergrid == String.Empty)
            {
                m_log.Error("[HGGRID CONNECTOR]: No HypergridService named in section GridService");
                throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
            }
            m_HypergridService = ServerUtils.LoadPlugin<IHypergridService>(hypergrid, args);

            if (m_GridServiceConnector == null || m_HypergridService == null)
                throw new Exception("Unable to proceed. HGGrid services could not be loaded.");
        }

        public void PostInitialise()
        {
            if (m_Enabled)
                ((ISharedRegionModule)m_GridServiceConnector).PostInitialise();
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_LocalScenes[scene.RegionInfo.RegionHandle] = scene;
            scene.RegisterModuleInterface<IGridService>(this);

            ((ISharedRegionModule)m_GridServiceConnector).AddRegion(scene);

        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                m_LocalScenes.Remove(scene.RegionInfo.RegionHandle);
                ((ISharedRegionModule)m_GridServiceConnector).RemoveRegion(scene);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Initialized)
            {
                m_aScene = scene;

                m_Initialized = true;
            }
        }

        #endregion

        #region IGridService

        public string RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            return m_GridServiceConnector.RegisterRegion(scopeID, regionInfo);
        }

        public bool DeregisterRegion(UUID regionID)
        {
            return m_GridServiceConnector.DeregisterRegion(regionID);
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            // No serving neighbours on hyperliked regions.
            // Just the regular regions.
            return m_GridServiceConnector.GetNeighbours(scopeID, regionID);
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            GridRegion region = m_GridServiceConnector.GetRegionByUUID(scopeID, regionID);
            if (region != null)
                return region;

            region = m_HypergridService.GetRegionByUUID(regionID);

            return region;
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            int snapX = (int) (x / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapY = (int) (y / Constants.RegionSize) * (int)Constants.RegionSize;

            GridRegion region = m_GridServiceConnector.GetRegionByPosition(scopeID, x, y);
            if (region != null)
                return region;

            region = m_HypergridService.GetRegionByPosition(snapX, snapY);

            return region;
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            // Try normal grid first
            GridRegion region = m_GridServiceConnector.GetRegionByName(scopeID, regionName);
            if (region != null)
                return region;

            region = m_HypergridService.GetRegionByName(regionName);

            return region;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            if (name == string.Empty)
                return new List<GridRegion>();

            List<GridRegion> rinfos = m_GridServiceConnector.GetRegionsByName(scopeID, name, maxNumber);

            rinfos.AddRange(m_HypergridService.GetRegionsByName(name));
            if (rinfos.Count > maxNumber)
                rinfos.RemoveRange(maxNumber - 1, rinfos.Count - maxNumber);

            return rinfos;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            int snapXmin = (int)(xmin / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapXmax = (int)(xmax / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapYmin = (int)(ymin / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapYmax = (int)(ymax / Constants.RegionSize) * (int)Constants.RegionSize;

            List<GridRegion> rinfos = m_GridServiceConnector.GetRegionRange(scopeID, xmin, xmax, ymin, ymax);

            rinfos.AddRange(m_HypergridService.GetRegionRange(snapXmin, snapXmax, snapYmin, snapYmax));

            return rinfos;
        }

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            return m_GridServiceConnector.GetDefaultRegions(scopeID);
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            return m_GridServiceConnector.GetFallbackRegions(scopeID, x, y);
        }

        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            return m_GridServiceConnector.GetRegionFlags(scopeID, regionID);
        }
     
        #endregion

    }
}
