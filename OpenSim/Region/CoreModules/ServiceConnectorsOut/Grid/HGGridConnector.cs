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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenSim.Services.Connectors.Grid;

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

        private IGridService m_GridServiceConnector;
        private HypergridServiceConnector m_HypergridServiceConnector;

        // Hyperlink regions are hyperlinks on the map
        protected Dictionary<UUID, SimpleRegionInfo> m_HyperlinkRegions = new Dictionary<UUID, SimpleRegionInfo>();

        // Known regions are home regions of visiting foreign users.
        // They are not on the map as static hyperlinks. They are dynamic hyperlinks, they go away when
        // the visitor goes away. They are mapped to X=0 on the map.
        // This is key-ed on agent ID
        protected Dictionary<UUID, SimpleRegionInfo> m_knownRegions = new Dictionary<UUID, SimpleRegionInfo>();

        protected Dictionary<UUID, ulong> m_HyperlinkHandles = new Dictionary<UUID, ulong>();

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
                //return;
                throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
            }

            Object[] args = new Object[] { source };
            m_GridServiceConnector = ServerUtils.LoadPlugin<IGridService>(module, args);

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IGridService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled && !m_Initialized)
            {
                m_HypergridServiceConnector = new HypergridServiceConnector(scene.AssetService);
                m_Initialized = true;
            }
        }

        #endregion

        #region IGridService

        public bool RegisterRegion(UUID scopeID, SimpleRegionInfo regionInfo)
        {
            // Region doesn't exist here. Trying to link remote region
            if (regionInfo.RegionID.Equals(UUID.Zero))
            {
                m_log.Info("[HGrid]: Linking remote region " + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort);
                ulong regionHandle = 0;
                regionInfo.RegionID = m_HypergridServiceConnector.LinkRegion(regionInfo, out regionHandle); 
                if (!regionInfo.RegionID.Equals(UUID.Zero))
                {
                    AddHyperlinkRegion(regionInfo, regionHandle);
                    m_log.Info("[HGrid]: Successfully linked to region_uuid " + regionInfo.RegionID);

                    // Try get the map image
                    m_HypergridServiceConnector.GetMapImage(regionInfo);
                    return true;
                }
                else
                {
                    m_log.Info("[HGrid]: No such region " + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort + "(" + regionInfo.InternalEndPoint.Port + ")");
                    return false;
                }
                // Note that these remote regions aren't registered in localBackend, so return null, no local listeners
            }
            else // normal grid
                return m_GridServiceConnector.RegisterRegion(scopeID, regionInfo);
        }

        public bool DeregisterRegion(UUID regionID)
        {
            // Try the hyperlink collection
            if (m_HyperlinkRegions.ContainsKey(regionID))
            {
                RemoveHyperlinkRegion(regionID);
                return true;
            }
            // Try the foreign users home collection

            foreach (SimpleRegionInfo r in m_knownRegions.Values)
                if (r.RegionID == regionID)
                {
                    RemoveHyperlinkHomeRegion(regionID);
                    return true;
                }

            // Finally, try the normal route
            return m_GridServiceConnector.DeregisterRegion(regionID);
        }

        public List<SimpleRegionInfo> GetNeighbours(UUID scopeID, UUID regionID)
        {
            // No serving neighbours on hyperliked regions.
            // Just the regular regions.
            return m_GridServiceConnector.GetNeighbours(scopeID, regionID);
        }

        public SimpleRegionInfo GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            // Try the hyperlink collection
            if (m_HyperlinkRegions.ContainsKey(regionID))
                return m_HyperlinkRegions[regionID];
            
            // Try the foreign users home collection
            foreach (SimpleRegionInfo r in m_knownRegions.Values)
                if (r.RegionID == regionID)
                    return m_knownRegions[regionID];

            // Finally, try the normal route
            return m_GridServiceConnector.GetRegionByUUID(scopeID, regionID);
        }

        public SimpleRegionInfo GetRegionByPosition(UUID scopeID, int x, int y)
        {
            int snapX = (int) (x / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapY = (int) (y / Constants.RegionSize) * (int)Constants.RegionSize;
            // Try the hyperlink collection
            foreach (SimpleRegionInfo r in m_HyperlinkRegions.Values)
            {
                if ((r.RegionLocX == snapX) && (r.RegionLocY == snapY))
                    return r;
            }

            // Try the foreign users home collection
            foreach (SimpleRegionInfo r in m_knownRegions.Values)
            {
                if ((r.RegionLocX == snapX) && (r.RegionLocY == snapY))
                    return r;
            }

            // Finally, try the normal route
            return m_GridServiceConnector.GetRegionByPosition(scopeID, x, y);
        }

        public SimpleRegionInfo GetRegionByName(UUID scopeID, string regionName)
        {
            // Try normal grid first
            SimpleRegionInfo region = m_GridServiceConnector.GetRegionByName(scopeID, regionName);
            if (region != null)
                return region;

            // Try the hyperlink collection
            foreach (SimpleRegionInfo r in m_HyperlinkRegions.Values)
            {
                if (r.RegionName == regionName)
                    return r;
            }

            // Try the foreign users home collection
            foreach (SimpleRegionInfo r in m_knownRegions.Values)
            {
                if (r.RegionName == regionName)
                    return r;
            }
            return null;
        }

        public List<SimpleRegionInfo> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            List<SimpleRegionInfo> rinfos = new List<SimpleRegionInfo>();

            // Commenting until regionname exists
            //foreach (SimpleRegionInfo r in m_HyperlinkRegions.Values)
            //    if ((r.RegionName != null) && r.RegionName.StartsWith(name))
            //        rinfos.Add(r);

            rinfos.AddRange(m_GridServiceConnector.GetRegionsByName(scopeID, name, maxNumber));
            return rinfos;
        }

        public List<SimpleRegionInfo> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            int snapXmin = (int)(xmin / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapXmax = (int)(xmax / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapYmin = (int)(ymin / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapYmax = (int)(ymax / Constants.RegionSize) * (int)Constants.RegionSize;

            List<SimpleRegionInfo> rinfos = new List<SimpleRegionInfo>();
            foreach (SimpleRegionInfo r in m_HyperlinkRegions.Values)
                if ((r.RegionLocX > snapXmin) && (r.RegionLocX < snapYmax) &&
                    (r.RegionLocY > snapYmin) && (r.RegionLocY < snapYmax))
                    rinfos.Add(r);

            rinfos.AddRange(m_GridServiceConnector.GetRegionRange(scopeID, xmin, xmax, ymin, ymax));

            return rinfos;
        }

        #endregion

        private void AddHyperlinkRegion(SimpleRegionInfo regionInfo, ulong regionHandle)
        {
            m_HyperlinkRegions.Add(regionInfo.RegionID, regionInfo);
            m_HyperlinkHandles.Add(regionInfo.RegionID, regionHandle);
        }

        private void RemoveHyperlinkRegion(UUID regionID)
        {
            m_HyperlinkRegions.Remove(regionID);
            m_HyperlinkHandles.Remove(regionID);
        }

        private void AddHyperlinkHomeRegion(UUID userID, SimpleRegionInfo regionInfo, ulong regionHandle)
        {
            m_knownRegions.Add(userID, regionInfo);
            m_HyperlinkHandles.Add(regionInfo.RegionID, regionHandle);
        }

        private void RemoveHyperlinkHomeRegion(UUID regionID)
        {
            foreach (KeyValuePair<UUID, SimpleRegionInfo> kvp in m_knownRegions)
            {
                if (kvp.Value.RegionID == regionID)
                {
                    m_knownRegions.Remove(kvp.Key);
                }
            }
            m_HyperlinkHandles.Remove(regionID);
        }

    }
}
