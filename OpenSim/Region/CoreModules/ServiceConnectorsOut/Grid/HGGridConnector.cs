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
using OpenSim.Server.Base;
using OpenSim.Services.Connectors.Grid;
using OpenSim.Framework.Console;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class HGGridConnector : ISharedRegionModule, IGridService, IHyperlinkService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private bool m_Initialized = false;

        private IGridService m_GridServiceConnector;
        private HypergridServiceConnector m_HypergridServiceConnector;

        // Hyperlink regions are hyperlinks on the map
        protected Dictionary<UUID, GridRegion> m_HyperlinkRegions = new Dictionary<UUID, GridRegion>();

        // Known regions are home regions of visiting foreign users.
        // They are not on the map as static hyperlinks. They are dynamic hyperlinks, they go away when
        // the visitor goes away. They are mapped to X=0 on the map.
        // This is key-ed on agent ID
        protected Dictionary<UUID, GridRegion> m_knownRegions = new Dictionary<UUID, GridRegion>();

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
            scene.RegisterModuleInterface<IHyperlinkService>(this);

        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Initialized)
            {
                m_HypergridServiceConnector = new HypergridServiceConnector(scene.AssetService);
                HGCommands hgCommands = new HGCommands(this, scene);
                MainConsole.Instance.Commands.AddCommand("HGGridServicesConnector", false, "linkk-region",
                    "link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>] <cr>",
                    "Link a hypergrid region", hgCommands.RunCommand);
                MainConsole.Instance.Commands.AddCommand("HGGridServicesConnector", false, "unlinkk-region",
                    "unlink-region <local name> or <HostName>:<HttpPort> <cr>",
                    "Unlink a hypergrid region", hgCommands.RunCommand);
                MainConsole.Instance.Commands.AddCommand("HGGridServicesConnector", false, "linkk-mapping", "link-mapping [<x> <y>] <cr>",
                    "Set local coordinate to map HG regions to", hgCommands.RunCommand);
                m_Initialized = true;
            }


            //scene.AddCommand("HGGridServicesConnector", "linkk-region",
            //    "link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>] <cr>",
            //    "Link a hypergrid region", hgCommands.RunCommand);
            //scene.AddCommand("HGGridServicesConnector", "unlinkk-region",
            //    "unlink-region <local name> or <HostName>:<HttpPort> <cr>",
            //    "Unlink a hypergrid region", hgCommands.RunCommand);
            //scene.AddCommand("HGGridServicesConnector", "linkk-mapping", "link-mapping [<x> <y>] <cr>",
            //    "Set local coordinate to map HG regions to", hgCommands.RunCommand);

        }

        #endregion

        #region IGridService

        public bool RegisterRegion(UUID scopeID, GridRegion regionInfo)
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

            foreach (GridRegion r in m_knownRegions.Values)
                if (r.RegionID == regionID)
                {
                    RemoveHyperlinkHomeRegion(regionID);
                    return true;
                }

            // Finally, try the normal route
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
            // Try the hyperlink collection
            if (m_HyperlinkRegions.ContainsKey(regionID))
                return m_HyperlinkRegions[regionID];
            
            // Try the foreign users home collection
            foreach (GridRegion r in m_knownRegions.Values)
                if (r.RegionID == regionID)
                    return m_knownRegions[regionID];

            // Finally, try the normal route
            return m_GridServiceConnector.GetRegionByUUID(scopeID, regionID);
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            int snapX = (int) (x / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapY = (int) (y / Constants.RegionSize) * (int)Constants.RegionSize;
            // Try the hyperlink collection
            foreach (GridRegion r in m_HyperlinkRegions.Values)
            {
                if ((r.RegionLocX == snapX) && (r.RegionLocY == snapY))
                    return r;
            }

            // Try the foreign users home collection
            foreach (GridRegion r in m_knownRegions.Values)
            {
                if ((r.RegionLocX == snapX) && (r.RegionLocY == snapY))
                    return r;
            }

            // Finally, try the normal route
            return m_GridServiceConnector.GetRegionByPosition(scopeID, x, y);
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            // Try normal grid first
            GridRegion region = m_GridServiceConnector.GetRegionByName(scopeID, regionName);
            if (region != null)
                return region;

            // Try the hyperlink collection
            foreach (GridRegion r in m_HyperlinkRegions.Values)
            {
                if (r.RegionName == regionName)
                    return r;
            }

            // Try the foreign users home collection
            foreach (GridRegion r in m_knownRegions.Values)
            {
                if (r.RegionName == regionName)
                    return r;
            }
            return null;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            List<GridRegion> rinfos = new List<GridRegion>();

            // Commenting until regionname exists
            //foreach (SimpleRegionInfo r in m_HyperlinkRegions.Values)
            //    if ((r.RegionName != null) && r.RegionName.StartsWith(name))
            //        rinfos.Add(r);

            rinfos.AddRange(m_GridServiceConnector.GetRegionsByName(scopeID, name, maxNumber));
            return rinfos;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            int snapXmin = (int)(xmin / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapXmax = (int)(xmax / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapYmin = (int)(ymin / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapYmax = (int)(ymax / Constants.RegionSize) * (int)Constants.RegionSize;

            List<GridRegion> rinfos = new List<GridRegion>();
            foreach (GridRegion r in m_HyperlinkRegions.Values)
                if ((r.RegionLocX > snapXmin) && (r.RegionLocX < snapYmax) &&
                    (r.RegionLocY > snapYmin) && (r.RegionLocY < snapYmax))
                    rinfos.Add(r);

            rinfos.AddRange(m_GridServiceConnector.GetRegionRange(scopeID, xmin, xmax, ymin, ymax));

            return rinfos;
        }

        #endregion

        #region Auxiliary

        private void AddHyperlinkRegion(GridRegion regionInfo, ulong regionHandle)
        {
            m_HyperlinkRegions.Add(regionInfo.RegionID, regionInfo);
            m_HyperlinkHandles.Add(regionInfo.RegionID, regionHandle);
        }

        private void RemoveHyperlinkRegion(UUID regionID)
        {
            m_HyperlinkRegions.Remove(regionID);
            m_HyperlinkHandles.Remove(regionID);
        }

        private void AddHyperlinkHomeRegion(UUID userID, GridRegion regionInfo, ulong regionHandle)
        {
            m_knownRegions.Add(userID, regionInfo);
            m_HyperlinkHandles.Add(regionInfo.RegionID, regionHandle);
        }

        private void RemoveHyperlinkHomeRegion(UUID regionID)
        {
            foreach (KeyValuePair<UUID, GridRegion> kvp in m_knownRegions)
            {
                if (kvp.Value.RegionID == regionID)
                {
                    m_knownRegions.Remove(kvp.Key);
                }
            }
            m_HyperlinkHandles.Remove(regionID);
        }
        #endregion

        #region IHyperlinkService

        private static Random random = new Random();


        public GridRegion TryLinkRegionToCoords(Scene m_scene, IClientAPI client, string mapName, int xloc, int yloc)
        {
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

            if ((ipaddr != null) &&
                !((m_scene.RegionInfo.ExternalEndPoint.Address.Equals(ipaddr)) && (m_scene.RegionInfo.HttpPort == port)))
            {
                GridRegion regInfo;
                bool success = TryCreateLink(m_scene, client, xloc, yloc, regionName, port, host, out regInfo);
                if (success)
                {
                    regInfo.RegionName = mapName;
                    return regInfo;
                }
            }

            return null;
        }


        // From the map search and secondlife://blah
        public GridRegion TryLinkRegion(Scene m_scene, IClientAPI client, string mapName)
        {
            int xloc = random.Next(0, Int16.MaxValue);
            return TryLinkRegionToCoords(m_scene, client, mapName, xloc, 0);
        }

        public bool TryCreateLink(Scene m_scene, IClientAPI client, int xloc, int yloc,
            string externalRegionName, uint externalPort, string externalHostName, out GridRegion regInfo)
        {
            m_log.DebugFormat("[HGrid]: Link to {0}:{1}, in {2}-{3}", externalHostName, externalPort, xloc, yloc);

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
                m_log.Warn("[HGrid]: Wrong format for link-region: " + e.Message);
                return false;
            }

            // Finally, link it
            try
            {
                RegisterRegion(UUID.Zero, regInfo);
            }
            catch (Exception e)
            {
                m_log.Warn("[HGrid]: Unable to link region: " + e.Message);
                return false;
            }

            int x, y;
            if (!Check4096(m_scene, regInfo, out x, out y))
            {
                DeregisterRegion(regInfo.RegionID);
                if (client != null)
                    client.SendAlertMessage("Region is too far (" + x + ", " + y + ")");
                m_log.Info("[HGrid]: Unable to link, region is too far (" + x + ", " + y + ")");
                return false;
            }

            if (!CheckCoords(m_scene.RegionInfo.RegionLocX, m_scene.RegionInfo.RegionLocY, x, y))
            {
                DeregisterRegion(regInfo.RegionID);
                if (client != null)
                    client.SendAlertMessage("Region has incompatible coordinates (" + x + ", " + y + ")");
                m_log.Info("[HGrid]: Unable to link, region has incompatible coordinates (" + x + ", " + y + ")");
                return false;
            }

            m_log.Debug("[HGrid]: link region succeeded");
            return true;
        }

        public bool TryUnlinkRegion(Scene m_scene, string mapName)
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
                //                if (parts.Length >= 2)
                //                {
                //                    portstr = parts[1];
                //                    if (!UInt32.TryParse(portstr, out port))
                //                        regionName = parts[1];
                //                }
                // always take the last one
                //                if (parts.Length >= 3)
                //                {
                //                    regionName = parts[2];
                //                }
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
                return DeregisterRegion(regInfo.RegionID);
            }
            else
            {
                m_log.InfoFormat("[HGrid]: Region {0} not found", mapName);
                return false;
            }
        }

        /// <summary>
        /// Cope with this viewer limitation.
        /// </summary>
        /// <param name="regInfo"></param>
        /// <returns></returns>
        public bool Check4096(Scene m_scene, GridRegion regInfo, out int x, out int y)
        {
            ulong realHandle = m_HyperlinkHandles[regInfo.RegionID];
            uint ux = 0, uy = 0;
            Utils.LongToUInts(realHandle, out ux, out uy);
            x = (int)(ux / Constants.RegionSize);
            y = (int)(uy / Constants.RegionSize);

            if ((Math.Abs((int)(m_scene.RegionInfo.RegionLocX / Constants.RegionSize) - x) >= 4096) ||
                (Math.Abs((int)(m_scene.RegionInfo.RegionLocY / Constants.RegionSize) - y) >= 4096))
            {
                return false;
            }
            return true;
        }

        public bool CheckCoords(uint thisx, uint thisy, int x, int y)
        {
            if ((thisx == x) && (thisy == y))
                return false;
            return true;
        }

        public GridRegion TryLinkRegion(IClientAPI client, string regionDescriptor)
        {
            return TryLinkRegion((Scene)client.Scene, client, regionDescriptor);
        }

        public GridRegion GetHyperlinkRegion(ulong handle)
        {
            foreach (GridRegion r in m_HyperlinkRegions.Values)
                if (r.RegionHandle == handle)
                    return r;
            foreach (GridRegion r in m_knownRegions.Values)
                if (r.RegionHandle == handle)
                    return r;
            return null;
        }

        public ulong FindRegionHandle(ulong handle)
        {
            foreach (GridRegion r in m_HyperlinkRegions.Values)
                if ((r.RegionHandle == handle) && (m_HyperlinkHandles.ContainsKey(r.RegionID)))
                    return m_HyperlinkHandles[r.RegionID];
            return handle;
        }

        #endregion

    }
}
