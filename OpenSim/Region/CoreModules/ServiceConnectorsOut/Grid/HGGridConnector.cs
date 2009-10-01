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

using OpenSim.Framework.Communications.Cache;
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
        private static string LocalAssetServerURI, LocalInventoryServerURI, LocalUserServerURI;

        private bool m_Enabled = false;
        private bool m_Initialized = false;

        private Scene m_aScene;
        private Dictionary<ulong, Scene> m_LocalScenes = new Dictionary<ulong, Scene>();

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
            scene.RegisterModuleInterface<IHyperlinkService>(this);

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
                LocalAssetServerURI = m_aScene.CommsManager.NetworkServersInfo.UserURL;
                LocalInventoryServerURI = m_aScene.CommsManager.NetworkServersInfo.InventoryURL;
                LocalUserServerURI = m_aScene.CommsManager.NetworkServersInfo.UserURL;

                m_HypergridServiceConnector = new HypergridServiceConnector(scene.AssetService);

                HGCommands hgCommands = new HGCommands(this, scene);
                MainConsole.Instance.Commands.AddCommand("HGGridServicesConnector", false, "link-region",
                    "link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>] <cr>",
                    "Link a hypergrid region", hgCommands.RunCommand);
                MainConsole.Instance.Commands.AddCommand("HGGridServicesConnector", false, "unlink-region",
                    "unlink-region <local name> or <HostName>:<HttpPort> <cr>",
                    "Unlink a hypergrid region", hgCommands.RunCommand);
                MainConsole.Instance.Commands.AddCommand("HGGridServicesConnector", false, "link-mapping", "link-mapping [<x> <y>] <cr>",
                    "Set local coordinate to map HG regions to", hgCommands.RunCommand);

                // Yikes!! Remove this as soon as user services get refactored
                HGNetworkServersInfo.Init(LocalAssetServerURI, LocalInventoryServerURI, LocalUserServerURI);

                m_Initialized = true;
            }
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
                    return r;

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
                {
                    return r;
                }
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
            m_HyperlinkRegions[regionInfo.RegionID] = regionInfo;
            m_HyperlinkHandles[regionInfo.RegionID] = regionHandle;
        }

        private void RemoveHyperlinkRegion(UUID regionID)
        {
            m_HyperlinkRegions.Remove(regionID);
            m_HyperlinkHandles.Remove(regionID);
        }

        private void AddHyperlinkHomeRegion(UUID userID, GridRegion regionInfo, ulong regionHandle)
        {
            m_knownRegions[userID] = regionInfo;
            m_HyperlinkHandles[regionInfo.RegionID] = regionHandle;
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
            int xloc = random.Next(0, Int16.MaxValue) * (int) Constants.RegionSize;
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
                if (!RegisterRegion(UUID.Zero, regInfo))
                {
                    m_log.Warn("[HGrid]: Unable to link region");
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

            if ((Math.Abs((int)m_scene.RegionInfo.RegionLocX - x) >= 4096) ||
                (Math.Abs((int)m_scene.RegionInfo.RegionLocY - y) >= 4096))
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

            foreach (GridRegion r in m_knownRegions.Values)
                if ((r.RegionHandle == handle) && (m_HyperlinkHandles.ContainsKey(r.RegionID)))
                    return m_HyperlinkHandles[r.RegionID];

            return handle;
        }

        public bool SendUserInformation(GridRegion regInfo, AgentCircuitData agentData)
        {
            CachedUserInfo uinfo = m_aScene.CommsManager.UserProfileCacheService.GetUserDetails(agentData.AgentID);

            if ((IsLocalUser(uinfo) && (GetHyperlinkRegion(regInfo.RegionHandle) != null)) ||
                (!IsLocalUser(uinfo) && !IsGoingHome(uinfo, regInfo)))
            {
                m_log.Info("[HGrid]: Local user is going to foreign region or foreign user is going elsewhere");

                // Set the position of the region on the remote grid
                ulong realHandle = FindRegionHandle(regInfo.RegionHandle);
                uint x = 0, y = 0;
                Utils.LongToUInts(regInfo.RegionHandle, out x, out y);
                GridRegion clonedRegion = new GridRegion(regInfo);
                clonedRegion.RegionLocX = (int)x;
                clonedRegion.RegionLocY = (int)y;

                // Get the user's home region information
                GridRegion home = m_aScene.GridService.GetRegionByUUID(m_aScene.RegionInfo.ScopeID, uinfo.UserProfile.HomeRegionID);

                // Get the user's service URLs
                string serverURI = "";
                if (uinfo.UserProfile is ForeignUserProfileData)
                    serverURI = Util.ServerURI(((ForeignUserProfileData)uinfo.UserProfile).UserServerURI);
                string userServer = (serverURI == "") || (serverURI == null) ? LocalUserServerURI : serverURI;

                string assetServer = Util.ServerURI(uinfo.UserProfile.UserAssetURI);
                if ((assetServer == null) || (assetServer == ""))
                    assetServer = LocalAssetServerURI;

                string inventoryServer = Util.ServerURI(uinfo.UserProfile.UserInventoryURI);
                if ((inventoryServer == null) || (inventoryServer == ""))
                    inventoryServer = LocalInventoryServerURI;

                if (!m_HypergridServiceConnector.InformRegionOfUser(clonedRegion, agentData, home, userServer, assetServer, inventoryServer))
                {
                    m_log.Warn("[HGrid]: Could not inform remote region of transferring user.");
                    return false;
                }
            }
            //if ((uinfo == null) || !IsGoingHome(uinfo, regInfo))
            //{
            //    m_log.Info("[HGrid]: User seems to be going to foreign region.");
            //    if (!InformRegionOfUser(regInfo, agentData))
            //    {
            //        m_log.Warn("[HGrid]: Could not inform remote region of transferring user.");
            //        return false;
            //    }
            //}
            //else
            //    m_log.Info("[HGrid]: User seems to be going home " + uinfo.UserProfile.FirstName + " " + uinfo.UserProfile.SurName);

            // May need to change agent's name
            if (IsLocalUser(uinfo) && (GetHyperlinkRegion(regInfo.RegionHandle) != null))
            {
                agentData.firstname = agentData.firstname + "." + agentData.lastname;
                agentData.lastname = "@" + LocalUserServerURI.Replace("http://", ""); ; //HGNetworkServersInfo.Singleton.LocalUserServerURI;
            }

            return true;
        }

        public void AdjustUserInformation(AgentCircuitData agentData)
        {
            CachedUserInfo uinfo = m_aScene.CommsManager.UserProfileCacheService.GetUserDetails(agentData.AgentID);
            if ((uinfo != null) && (uinfo.UserProfile != null) &&
                (IsLocalUser(uinfo) || !(uinfo.UserProfile is ForeignUserProfileData)))
            {
                //m_log.Debug("---------------> Local User!");
                string[] parts = agentData.firstname.Split(new char[] { '.' });
                if (parts.Length == 2)
                {
                    agentData.firstname = parts[0];
                    agentData.lastname = parts[1];
                }
            }
            //else
            //    m_log.Debug("---------------> Foreign User!");
        }

        // Check if a local user exists with the same UUID as the incoming foreign user
        public bool CheckUserAtEntry(UUID userID, UUID sessionID, out bool comingHome)
        {
            comingHome = false;
            if (!m_aScene.SceneGridService.RegionLoginsEnabled)
                return false;

            CachedUserInfo uinfo = m_aScene.CommsManager.UserProfileCacheService.GetUserDetails(userID);
            if (uinfo != null) 
            {
                // uh-oh we have a potential intruder
                if (uinfo.SessionID != sessionID)
                    // can't have a foreigner with a local UUID
                    return false;
                else
                    // oh, so it's you! welcome back
                    comingHome = true;
            }

            // OK, user can come in
            return true;
        }

        public void AcceptUser(ForeignUserProfileData user, GridRegion home)
        {
            m_aScene.CommsManager.UserProfileCacheService.PreloadUserCache(user);
            ulong realHandle = home.RegionHandle;
            // Change the local coordinates
            // X=0 on the map
            home.RegionLocX = 0;
            home.RegionLocY = random.Next(0, 10000) * (int)Constants.RegionSize;
            
            AddHyperlinkHomeRegion(user.ID, home, realHandle);

            DumpUserData(user);
            DumpRegionData(home);

        }

        public bool IsLocalUser(UUID userID)
        {
            CachedUserInfo uinfo = m_aScene.CommsManager.UserProfileCacheService.GetUserDetails(userID);
            return IsLocalUser(uinfo);
        }

        #endregion

        #region IHyperlink Misc

        protected bool IsComingHome(ForeignUserProfileData userData)
        {
            return (userData.UserServerURI == LocalUserServerURI);
        }

        // Is the user going back to the home region or the home grid?
        protected bool IsGoingHome(CachedUserInfo uinfo, GridRegion rinfo)
        {
            if (uinfo.UserProfile == null)
                return false;

            if (!(uinfo.UserProfile is ForeignUserProfileData))
                // it's a home user, can't be outside to return home
                return false;

            // OK, it's a foreign user with a ForeignUserProfileData
            // and is going back to exactly the home region.
            // We can't check if it's going back to a non-home region
            // of the home grid. That will be dealt with in the
            // receiving end
            return (uinfo.UserProfile.HomeRegionID == rinfo.RegionID);
        }

        protected bool IsLocalUser(CachedUserInfo uinfo)
        {
            if (uinfo == null)
                return false;

            return !(uinfo.UserProfile is ForeignUserProfileData);

        }

        protected bool IsLocalRegion(ulong handle)
        {
            return m_LocalScenes.ContainsKey(handle);
        }

        private void DumpUserData(ForeignUserProfileData userData)
        {
            m_log.Info(" ------------ User Data Dump ----------");
            m_log.Info(" >> Name: " + userData.FirstName + " " + userData.SurName);
            m_log.Info(" >> HomeID: " + userData.HomeRegionID);
            m_log.Info(" >> UserServer: " + userData.UserServerURI);
            m_log.Info(" >> InvServer: " + userData.UserInventoryURI);
            m_log.Info(" >> AssetServer: " + userData.UserAssetURI);
            m_log.Info(" ------------ -------------- ----------");
        }

        private void DumpRegionData(GridRegion rinfo)
        {
            m_log.Info(" ------------ Region Data Dump ----------");
            m_log.Info(" >> handle: " + rinfo.RegionHandle);
            m_log.Info(" >> coords: " + rinfo.RegionLocX + ", " + rinfo.RegionLocY);
            m_log.Info(" >> external host name: " + rinfo.ExternalHostName);
            m_log.Info(" >> http port: " + rinfo.HttpPort);
            m_log.Info(" >> external EP address: " + rinfo.ExternalEndPoint.Address);
            m_log.Info(" >> external EP port: " + rinfo.ExternalEndPoint.Port);
            m_log.Info(" ------------ -------------- ----------");
        }


        #endregion


    }
}
