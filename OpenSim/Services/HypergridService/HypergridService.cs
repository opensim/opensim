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
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;

namespace OpenSim.Services.HypergridService
{
    public class HypergridService : HypergridServiceBase, IHypergridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private static HypergridService m_RootInstance = null;
        protected IConfigSource m_config;

        protected IPresenceService m_PresenceService = null;
        protected IGridService m_GridService;
        protected IAssetService m_AssetService;
        protected HypergridServiceConnector m_HypergridConnector;

        protected bool m_AllowDuplicateNames = false;
        protected UUID m_ScopeID = UUID.Zero;

        // Hyperlink regions are hyperlinks on the map
        public readonly Dictionary<UUID, GridRegion> m_HyperlinkRegions = new Dictionary<UUID, GridRegion>();
        protected Dictionary<UUID, ulong> m_HyperlinkHandles = new Dictionary<UUID, ulong>();

        protected GridRegion m_DefaultRegion;
        protected GridRegion DefaultRegion
        {
            get
            {
                if (m_DefaultRegion == null)
                {
                    List<GridRegion> defs = m_GridService.GetDefaultRegions(m_ScopeID);
                    if (defs != null && defs.Count > 0)
                        m_DefaultRegion = defs[0];
                    else
                    {
                        // Best guess, may be totally off
                        m_DefaultRegion = new GridRegion(1000, 1000);
                        m_log.WarnFormat("[HYPERGRID SERVICE]: This grid does not have a default region. Assuming default coordinates at 1000, 1000.");
                    }
                }
                return m_DefaultRegion;
            }
        }

        public HypergridService(IConfigSource config)
            : base(config)
        {
            m_log.DebugFormat("[HYPERGRID SERVICE]: Starting...");

            m_config = config;
            IConfig gridConfig = config.Configs["HypergridService"];
            if (gridConfig != null)
            {
                string gridService = gridConfig.GetString("GridService", string.Empty);                
                string presenceService = gridConfig.GetString("PresenceService", String.Empty);
                string assetService = gridConfig.GetString("AssetService", string.Empty);

                Object[] args = new Object[] { config };
                if (gridService != string.Empty)
                    m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);

                if (m_GridService == null)
                    throw new Exception("HypergridService cannot function without a GridService");

                if (presenceService != String.Empty)
                    m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);

                if (assetService != string.Empty)
                    m_AssetService = ServerUtils.LoadPlugin<IAssetService>(assetService, args);

                m_AllowDuplicateNames = gridConfig.GetBoolean("AllowDuplicateNames", m_AllowDuplicateNames);

                string scope = gridConfig.GetString("ScopeID", string.Empty);
                if (scope != string.Empty)
                    UUID.TryParse(scope, out m_ScopeID);

                m_HypergridConnector = new HypergridServiceConnector(m_AssetService);

                m_log.DebugFormat("[HYPERGRID SERVICE]: Loaded all services...");
            }
            
            if (m_RootInstance == null)
            {
                m_RootInstance = this;

                HGCommands hgCommands = new HGCommands(this);
                MainConsole.Instance.Commands.AddCommand("hypergrid", false, "link-region",
                    "link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>] <cr>",
                    "Link a hypergrid region", hgCommands.RunCommand);
                MainConsole.Instance.Commands.AddCommand("hypergrid", false, "unlink-region",
                    "unlink-region <local name> or <HostName>:<HttpPort> <cr>",
                    "Unlink a hypergrid region", hgCommands.RunCommand);
                MainConsole.Instance.Commands.AddCommand("hypergrid", false, "link-mapping", "link-mapping [<x> <y>] <cr>",
                    "Set local coordinate to map HG regions to", hgCommands.RunCommand);
                MainConsole.Instance.Commands.AddCommand("hypergrid", false, "show hyperlinks", "show hyperlinks <cr>",
                    "List the HG regions", hgCommands.HandleShow);
            }
        }

        #region Link Region

        public bool LinkRegion(string regionDescriptor, out UUID regionID, out ulong regionHandle, out string imageURL, out string reason)
        {
            regionID = UUID.Zero;
            imageURL = string.Empty;
            regionHandle = 0;
            reason = string.Empty;
            int xloc = random.Next(0, Int16.MaxValue) * (int)Constants.RegionSize;
            GridRegion region = TryLinkRegionToCoords(regionDescriptor, xloc, 0, out reason);
            if (region == null)
                return false;

            regionID = region.RegionID;
            regionHandle = region.RegionHandle;
            return true;
        }

        private static Random random = new Random();

        // From the command line link-region
        public GridRegion TryLinkRegionToCoords(string mapName, int xloc, int yloc, out string reason)
        {
            reason = string.Empty;
            string host = "127.0.0.1";
            string portstr;
            string regionName = "";
            uint port = 9000;
            string[] parts = mapName.Split(new char[] { ':' });
            if (parts.Length >= 1)
            {
                host = parts[0];
            }
            if (parts.Length >= 2)
            {
                portstr = parts[1];
                //m_log.Debug("-- port = " + portstr);
                if (!UInt32.TryParse(portstr, out port))
                    regionName = parts[1];
            }
            // always take the last one
            if (parts.Length >= 3)
            {
                regionName = parts[2];
            }

            // Sanity check. Don't ever link to this sim.
            IPAddress ipaddr = null;
            try
            {
                ipaddr = Util.GetHostFromDNS(host);
            }
            catch { }

            GridRegion regInfo;
            bool success = TryCreateLink(xloc, yloc, regionName, port, host, out regInfo, out reason);
            if (success)
            {
                regInfo.RegionName = mapName;
                return regInfo;
            }

            return null;
        }


        // From the command line and the 2 above
        public bool TryCreateLink(int xloc, int yloc,
            string externalRegionName, uint externalPort, string externalHostName, out GridRegion regInfo, out string reason)
        {
            m_log.DebugFormat("[HYPERGRID SERVICE]: Link to {0}:{1}, in {2}-{3}", externalHostName, externalPort, xloc, yloc);

            reason = string.Empty;
            regInfo = new GridRegion();
            regInfo.RegionName = externalRegionName;
            regInfo.HttpPort = externalPort;
            regInfo.ExternalHostName = externalHostName;
            regInfo.RegionLocX = xloc;
            regInfo.RegionLocY = yloc;

            try
            {
                regInfo.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)0);
            }
            catch (Exception e)
            {
                m_log.Warn("[HYPERGRID SERVICE]: Wrong format for link-region: " + e.Message);
                reason = "Internal error";
                return false;
            }

            // Finally, link it
            ulong handle = 0;
            UUID regionID = UUID.Zero;
            string imageURL = string.Empty;
            if (!m_HypergridConnector.LinkRegion(regInfo, out regionID, out handle, out imageURL, out reason))
                return false;

            if (regionID != UUID.Zero)
            {
                regInfo.RegionID = regionID;

                AddHyperlinkRegion(regInfo, handle);
                m_log.Info("[HYPERGRID SERVICE]: Successfully linked to region_uuid " + regInfo.RegionID);

                // Try get the map image
                regInfo.TerrainImage = m_HypergridConnector.GetMapImage(regionID, imageURL);
            }
            else
            {
                m_log.Warn("[HYPERGRID SERVICE]: Unable to link region");
                reason = "Remote region could not be found";
                return false;
            }

            uint x, y;
            if (!Check4096(regInfo, out x, out y))
            {
                RemoveHyperlinkRegion(regInfo.RegionID);
                reason = "Region is too far (" + x + ", " + y + ")";
                m_log.Info("[HYPERGRID SERVICE]: Unable to link, region is too far (" + x + ", " + y + ")");
                return false;
            }

            m_log.Debug("[HYPERGRID SERVICE]: link region succeeded");
            return true;
        }

        public bool TryUnlinkRegion(string mapName)
        {
            GridRegion regInfo = null;
            if (mapName.Contains(":"))
            {
                string host = "127.0.0.1";
                //string portstr;
                //string regionName = "";
                uint port = 9000;
                string[] parts = mapName.Split(new char[] { ':' });
                if (parts.Length >= 1)
                {
                    host = parts[0];
                }

                foreach (GridRegion r in m_HyperlinkRegions.Values)
                    if (host.Equals(r.ExternalHostName) && (port == r.HttpPort))
                        regInfo = r;
            }
            else
            {
                foreach (GridRegion r in m_HyperlinkRegions.Values)
                    if (r.RegionName.Equals(mapName))
                        regInfo = r;
            }
            if (regInfo != null)
            {
                RemoveHyperlinkRegion(regInfo.RegionID);
                return true;
            }
            else
            {
                m_log.InfoFormat("[HYPERGRID SERVICE]: Region {0} not found", mapName);
                return false;
            }
        }

        /// <summary>
        /// Cope with this viewer limitation.
        /// </summary>
        /// <param name="regInfo"></param>
        /// <returns></returns>
        public bool Check4096(GridRegion regInfo, out uint x, out uint y)
        {
            GridRegion defRegion = DefaultRegion;

            ulong realHandle = m_HyperlinkHandles[regInfo.RegionID];
            uint ux = 0, uy = 0;
            Utils.LongToUInts(realHandle, out ux, out uy);
            x = ux / Constants.RegionSize;
            y = uy / Constants.RegionSize;

            if ((Math.Abs((int)defRegion.RegionLocX - ux) >= 4096 * Constants.RegionSize) ||
                (Math.Abs((int)defRegion.RegionLocY - uy) >= 4096 * Constants.RegionSize))
            {
                return false;
            }
            return true;
        }

        private void AddHyperlinkRegion(GridRegion regionInfo, ulong regionHandle)
        {
            m_HyperlinkRegions[regionInfo.RegionID] = regionInfo;
            m_HyperlinkHandles[regionInfo.RegionID] = regionHandle;
        }

        private void RemoveHyperlinkRegion(UUID regionID)
        {
            // Try the hyperlink collection
            if (m_HyperlinkRegions.ContainsKey(regionID))
            {
                m_HyperlinkRegions.Remove(regionID);
                m_HyperlinkHandles.Remove(regionID);
            }
        }

        #endregion

        #region Get Hyperlinks

        public GridRegion GetHyperlinkRegion(GridRegion gatekeeper, UUID regionID)
        {
            if (m_HyperlinkRegions.ContainsKey(regionID))
                return m_HypergridConnector.GetHyperlinkRegion(gatekeeper, regionID);
            else
                return gatekeeper;
        }

        #endregion

        #region GetRegionBy X

        public GridRegion GetRegionByUUID(UUID regionID)
        {
            if (m_HyperlinkRegions.ContainsKey(regionID))
                return m_HyperlinkRegions[regionID];

            return null;
        }

        public GridRegion GetRegionByPosition(int x, int y)
        {
            foreach (GridRegion r in m_HyperlinkRegions.Values)
                if (r.RegionLocX == x && r.RegionLocY == y)
                    return r;

            return null;
        }

        public GridRegion GetRegionByName(string name)
        {
            foreach (GridRegion r in m_HyperlinkRegions.Values)
                if (r.RegionName.ToLower() == name.ToLower())
                    return r;

            return null;
        }

        public List<GridRegion> GetRegionsByName(string name)
        {
            List<GridRegion> regions = new List<GridRegion>();

            foreach (GridRegion r in m_HyperlinkRegions.Values)
                if ((r.RegionName != null) && r.RegionName.ToLower().StartsWith(name.ToLower()))
                    regions.Add(r);

            return regions;

        }

        public List<GridRegion> GetRegionRange(int xmin, int xmax, int ymin, int ymax)
        {
            List<GridRegion> regions = new List<GridRegion>();

            foreach (GridRegion r in m_HyperlinkRegions.Values)
                if ((r.RegionLocX > xmin) && (r.RegionLocX < xmax) &&
                    (r.RegionLocY > ymin) && (r.RegionLocY < ymax))
                    regions.Add(r);

            return regions;
        }

        #endregion



    }
}
