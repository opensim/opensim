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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting;
using System.Security.Authentication;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
// using OpenSim.Region.Environment.Modules.Framework;

namespace OpenSim.Region.Communications.Hypergrid
{
    /// <summary>
    /// This class encapsulates the main hypergrid functions related to creating and managing
    /// hyperlinks, as well as processing all the inter-region comms between a region and
    /// an hyperlinked region.
    /// </summary>
    public class HGGridServices : IGridServices, IHyperlink
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        public BaseHttpServer httpListener;
        public NetworkServersInfo serversInfo;
        public BaseHttpServer httpServer;

        protected List<RegionInfo> m_regionsOnInstance = new List<RegionInfo>();

        // Hyperlink regions are hyperlinks on the map
        protected List<RegionInfo> m_hyperlinkRegions = new List<RegionInfo>();

        // Known regions are home regions of visiting foreign users.
        // They are not on the map as static hyperlinks. They are dynamic hyperlinks, they go away when
        // the visitor goes away. They are mapped to X=0 on the map.
        // This is key-ed on agent ID
        protected Dictionary<UUID, RegionInfo> m_knownRegions = new Dictionary<UUID, RegionInfo>();

        protected IAssetCache m_assetcache;
        protected UserProfileCacheService m_userProfileCache;
        protected SceneManager m_sceneman;

        private Dictionary<string, string> m_queuedGridSettings = new Dictionary<string, string>();

        public virtual string gdebugRegionName
        {
            get { return "Override me"; }
            set { ; }
        }

        public string rdebugRegionName
        {
            get { return _rdebugRegionName; }
            set { _rdebugRegionName = value; }
        }
        private string _rdebugRegionName = String.Empty;

        public virtual bool RegionLoginsEnabled
        {
            get { return true; }
            set { ; }
        }

        public UserProfileCacheService UserProfileCache
        {
            set { m_userProfileCache = value; }
        }

        private Random random;

        /// <summary>
        /// Contructor.  Adds "expect_hg_user" and "check" xmlrpc method handlers
        /// </summary>
        /// <param name="servers_info"></param>
        /// <param name="httpServe"></param>
        public HGGridServices(NetworkServersInfo servers_info, BaseHttpServer httpServe, IAssetCache asscache, SceneManager sman)
        {
            serversInfo = servers_info;
            httpServer = httpServe;
            m_assetcache = asscache;
            m_sceneman = sman;

            random = new Random();

            httpServer.AddXmlRPCHandler("link_region", LinkRegionRequest);
            httpServer.AddXmlRPCHandler("expect_hg_user", ExpectHGUser);

            HGNetworkServersInfo.Init(servers_info.AssetURL, servers_info.InventoryURL, servers_info.UserURL);
        }

        // see IGridServices
        public virtual RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            // Region doesn't exist here. Trying to link remote region

            m_log.Info("[HGrid]: Linking remote region " + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort);
            regionInfo.RegionID = LinkRegion(regionInfo); // UUID.Random();
            if (!regionInfo.RegionID.Equals(UUID.Zero))
            {
                m_hyperlinkRegions.Add(regionInfo);
                m_log.Info("[HGrid]: Successfully linked to region_uuid " + regionInfo.RegionID);

                //Try get the map image
                GetMapImage(regionInfo);
            }
            else
            {
                m_log.Info("[HGrid]: No such region " + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort + "(" + regionInfo.InternalEndPoint.Port + ")");
            }
            // Note that these remote regions aren't registered in localBackend, so return null, no local listeners
            return null;
        }

        // see IGridServices
        public virtual bool DeregisterRegion(RegionInfo regionInfo)
        {
            if (m_hyperlinkRegions.Contains(regionInfo))
            {
                m_hyperlinkRegions.Remove(regionInfo);
                return true;
            }
            foreach (KeyValuePair<UUID, RegionInfo> kvp in m_knownRegions)
            {
                if (kvp.Value == regionInfo)
                {
                    m_knownRegions.Remove(kvp.Key);
                    return true;
                }
            }
            return false;
        }

        public virtual Dictionary<string, string> GetGridSettings()
        {
            Dictionary<string, string> returnGridSettings = new Dictionary<string, string>();
            lock (m_queuedGridSettings)
            {
                foreach (string Dictkey in m_queuedGridSettings.Keys)
                {
                    returnGridSettings.Add(Dictkey, m_queuedGridSettings[Dictkey]);
                }

                m_queuedGridSettings.Clear();
            }

            return returnGridSettings;
        }

        // see IGridServices
        public virtual List<SimpleRegionInfo> RequestNeighbours(uint x, uint y)
        {
            List<SimpleRegionInfo> neighbours = new List<SimpleRegionInfo>();
            foreach (RegionInfo reg in m_hyperlinkRegions)
            {
                if (reg.RegionLocX != x || reg.RegionLocY != y)
                {
                    //m_log.Debug("CommsManager- RequestNeighbours() - found a different region in list, checking location");
                    if ((reg.RegionLocX > (x - 2)) && (reg.RegionLocX < (x + 2)))
                    {
                        if ((reg.RegionLocY > (y - 2)) && (reg.RegionLocY < (y + 2)))
                        {
                            neighbours.Add(reg);
                        }
                    }
                }
            }

            return neighbours;
        }

        /// <summary>
        /// Request information about a region.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns>
        /// null on a failure to contact or get a response from the grid server
        /// FIXME: Might be nicer to return a proper exception here since we could inform the client more about the
        /// nature of the faiulre.
        /// </returns>
        public virtual RegionInfo RequestNeighbourInfo(UUID Region_UUID)
        {
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                if (info.RegionID == Region_UUID) return info;
            }

            // I don't trust region uuids to be unique...
            //foreach (RegionInfo info in m_knownRegions.Values)
            //{
            //    if (info.RegionID == Region_UUID) return info;
            //}

            return null;
        }

        /// <summary>
        /// Request information about a region.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public virtual RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            //m_log.Debug(" >> RequestNeighbourInfo for " + regionHandle);
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                //m_log.Debug("    .. " + info.RegionHandle);
                if (info.RegionHandle == regionHandle) return info;
            }

            foreach (RegionInfo info in m_knownRegions.Values)
            {
                if (info.RegionHandle == regionHandle)
                {
                    //m_log.Debug("XXX------ known region " + info.RegionHandle);
                    return info;
                }
            }

            return null;
        }

        public virtual RegionInfo RequestNeighbourInfo(string name)
        {
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                //m_log.Debug("    .. " + info.RegionHandle);
                if (info.RegionName == name) return info;
            }

            foreach (RegionInfo info in m_knownRegions.Values)
            {
                if (info.RegionName == name)
                {
                    //m_log.Debug("XXX------ known region " + info.RegionHandle);
                    return info;
                }
            }

            return null;
        }

        public virtual RegionInfo RequestNeighbourInfo(string hostName, uint port)
        {
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                //m_log.Debug("    .. " + info.RegionHandle);
                if ((info.ExternalHostName == hostName) && (info.HttpPort == port))
                    return info;
            }

            foreach (RegionInfo info in m_knownRegions.Values)
            {
                if ((info.ExternalHostName == hostName) && (info.HttpPort == port))
                {
                    //m_log.Debug("XXX------ known region " + info.RegionHandle);
                    return info;
                }
            }

            return null;
        }

        public virtual RegionInfo RequestClosestRegion(string regionName)
        {
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                if (info.RegionName == regionName) return info;
            }

            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public virtual List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            List<MapBlockData> neighbours = new List<MapBlockData>();

            foreach (RegionInfo regInfo in m_hyperlinkRegions)
            {
                if (((regInfo.RegionLocX >= minX) && (regInfo.RegionLocX <= maxX)) &&
                    ((regInfo.RegionLocY >= minY) && (regInfo.RegionLocY <= maxY)))
                {
                    MapBlockData map = new MapBlockData();
                    map.Name = regInfo.RegionName;
                    map.X = (ushort)regInfo.RegionLocX;
                    map.Y = (ushort)regInfo.RegionLocY;
                    map.WaterHeight = (byte)regInfo.RegionSettings.WaterHeight;
                    map.MapImageId = regInfo.RegionSettings.TerrainImageID;
                    //                    m_log.Debug("ImgID: " + map.MapImageId);
                    map.Agents = 1;
                    map.RegionFlags = 72458694;
                    map.Access = regInfo.AccessLevel;
                    neighbours.Add(map);
                }
            }

            return neighbours;
        }


        protected virtual void GetMapImage(RegionInfo info)
        {
            try
            {
                string regionimage = "regionImage" + info.RegionID.ToString();
                regionimage = regionimage.Replace("-", "");

                WebClient c = new WebClient();
                string uri = "http://" + info.ExternalHostName + ":" + info.HttpPort + "/index.php?method=" + regionimage;
                //m_log.Debug("JPEG: " + uri);
                c.DownloadFile(uri, info.RegionID.ToString() + ".jpg");
                Bitmap m = new Bitmap(info.RegionID.ToString() + ".jpg");
                //m_log.Debug("Size: " + m.PhysicalDimension.Height + "-" + m.PhysicalDimension.Width);
                byte[] imageData = OpenJPEG.EncodeFromImage(m, true);
                AssetBase ass = new AssetBase(UUID.Random(), "region " + info.RegionID.ToString());
                info.RegionSettings.TerrainImageID = ass.FullID;
                ass.Type = (int)AssetType.Texture;
                ass.Temporary = false;
                ass.Local = true;
                ass.Data = imageData;
                
                m_sceneman.CurrentOrFirstScene.AssetService.Store(ass);

            }
            catch // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                m_log.Warn("[HGrid]: Failed getting/storing map image, because it is probably already in the cache");
            }
        }

        // A little ugly, since this code is exactly the same as OSG1's, and we're already
        // calling that for when the region in in grid mode... (for the grid regions)
        //
        public virtual LandData RequestLandData (ulong regionHandle, uint x, uint y)
        {
            m_log.DebugFormat("[HGrid]: requests land data in {0}, at {1}, {2}",
                              regionHandle, x, y);

            // Remote region

            Hashtable hash = new Hashtable();
            hash["region_handle"] = regionHandle.ToString();
            hash["x"] = x.ToString();
            hash["y"] = y.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);
            LandData landData = null;

            try
            {
                RegionInfo info = RequestNeighbourInfo(regionHandle);
                if (info != null) // just to be sure
                {
                    XmlRpcRequest request = new XmlRpcRequest("land_data", paramList);
                    string uri = "http://" + info.ExternalEndPoint.Address + ":" + info.HttpPort + "/";
                    XmlRpcResponse response = request.Send(uri, 10000);
                    if (response.IsFault)
                    {
                        m_log.ErrorFormat("[HGrid]: remote call returned an error: {0}", response.FaultString);
                    }
                    else
                    {
                        hash = (Hashtable)response.Value;
                        try
                        {
                            landData = new LandData();
                            landData.AABBMax = Vector3.Parse((string)hash["AABBMax"]);
                            landData.AABBMin = Vector3.Parse((string)hash["AABBMin"]);
                            landData.Area = Convert.ToInt32(hash["Area"]);
                            landData.AuctionID = Convert.ToUInt32(hash["AuctionID"]);
                            landData.Description = (string)hash["Description"];
                            landData.Flags = Convert.ToUInt32(hash["Flags"]);
                            landData.GlobalID = new UUID((string)hash["GlobalID"]);
                            landData.Name = (string)hash["Name"];
                            landData.OwnerID = new UUID((string)hash["OwnerID"]);
                            landData.SalePrice = Convert.ToInt32(hash["SalePrice"]);
                            landData.SnapshotID = new UUID((string)hash["SnapshotID"]);
                            landData.UserLocation = Vector3.Parse((string)hash["UserLocation"]);
                            m_log.DebugFormat("[HGrid]: Got land data for parcel {0}", landData.Name);
                        }
                        catch (Exception e)
                        {
                            m_log.Error("[HGrid]: Got exception while parsing land-data:", e);
                        }
                    }
                }
                else m_log.WarnFormat("[HGrid]: Couldn't find region with handle {0}", regionHandle);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[HGrid]: Couldn't contact region {0}: {1}", regionHandle, e);
            }

            return landData;
        }

        // Grid Request Processing
        public virtual List<RegionInfo> RequestNamedRegions (string name, int maxNumber)
        {
            List<RegionInfo> infos = new List<RegionInfo>();
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                if (info.RegionName.ToLower().Contains(name))
                {
                    infos.Add(info);
                }
            }
            return infos;
        }


        private UUID LinkRegion(RegionInfo info)
        {
            UUID uuid = UUID.Zero;

            Hashtable hash = new Hashtable();
            hash["region_name"] = info.RegionName;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("link_region", paramList);
            string uri = "http://" + info.ExternalEndPoint.Address + ":" + info.HttpPort + "/";
            m_log.Debug("[HGrid]: Linking to " + uri);
            XmlRpcResponse response = request.Send(uri, 10000);
            if (response.IsFault)
            {
                m_log.ErrorFormat("[HGrid]: remote call returned an error: {0}", response.FaultString);
            }
            else
            {
                hash = (Hashtable)response.Value;
                //foreach (Object o in hash)
                //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
                try
                {
                    UUID.TryParse((string)hash["uuid"], out uuid);
                    info.RegionID = uuid;
                    if ((string)hash["handle"] != null)
                    {
                        info.regionSecret = (string)hash["handle"];
                        //m_log.Debug(">> HERE: " + info.regionSecret);
                    }
                    if (hash["region_image"] != null)
                    {
                        UUID img = UUID.Zero;
                        UUID.TryParse((string)hash["region_image"], out img);
                        info.RegionSettings.TerrainImageID = img;
                    }
                    if (hash["region_name"] != null)
                    {
                        info.RegionName = (string)hash["region_name"];
                        //m_log.Debug(">> " + info.RegionName);
                    }
                    if (hash["internal_port"] != null)
                    {
                        int port = Convert.ToInt32((string)hash["internal_port"]);
                        info.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
                        //m_log.Debug(">> " + info.InternalEndPoint.ToString());
                    }
                    if (hash["remoting_port"] != null)
                    {
                        info.RemotingPort = Convert.ToUInt32(hash["remoting_port"]);
                        //m_log.Debug(">> " + info.RemotingPort);
                    }

                }
                catch (Exception e)
                {
                    m_log.Error("[HGrid]: Got exception while parsing hyperlink response " + e.StackTrace);
                }
            }
            return uuid;
        }

        /// <summary>
        /// Someone wants to link to us
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LinkRegionRequest(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string name = (string)requestData["region_name"];

            m_log.DebugFormat("[HGrid]: Hyperlink request");


            RegionInfo regInfo = null;
            foreach (RegionInfo r in m_regionsOnInstance)
            {
                if ((r.RegionName != null) && (name != null) && (r.RegionName.ToLower() == name.ToLower()))
                {
                    regInfo = r;
                    break;
                }
            }

            if (regInfo == null)
                regInfo = m_regionsOnInstance[0]; // Send out the first region

            Hashtable hash = new Hashtable();
            hash["uuid"] = regInfo.RegionID.ToString();
            hash["handle"] = regInfo.RegionHandle.ToString();
            //m_log.Debug(">> Here " + regInfo.RegionHandle);
            hash["region_image"] = regInfo.RegionSettings.TerrainImageID.ToString();
            hash["region_name"] = regInfo.RegionName;
            hash["internal_port"] = regInfo.InternalEndPoint.Port.ToString();
            hash["remoting_port"] = ConfigSettings.DefaultRegionRemotingPort.ToString();
            //m_log.Debug(">> Here: " + regInfo.InternalEndPoint.Port);


            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        public bool InformRegionOfUser(RegionInfo regInfo, AgentCircuitData agentData)
        {
            //ulong regionHandle = regInfo.RegionHandle;
            try
            {
                //regionHandle = Convert.ToUInt64(regInfo.regionSecret);
                m_log.Info("[HGrid]: InformRegionOfUser: Remote hyperlinked region " + regInfo.regionSecret);
            }
            catch
            {
                m_log.Info("[HGrid]: InformRegionOfUser: Local grid region " + regInfo.regionSecret);
            }

            string capsPath = agentData.CapsPath;
            Hashtable loginParams = new Hashtable();
            loginParams["session_id"] = agentData.SessionID.ToString();
            loginParams["secure_session_id"] = agentData.SecureSessionID.ToString();

            loginParams["firstname"] = agentData.firstname;
            loginParams["lastname"] = agentData.lastname;

            loginParams["agent_id"] = agentData.AgentID.ToString();
            loginParams["circuit_code"] = agentData.circuitcode.ToString();
            loginParams["startpos_x"] = agentData.startpos.X.ToString();
            loginParams["startpos_y"] = agentData.startpos.Y.ToString();
            loginParams["startpos_z"] = agentData.startpos.Z.ToString();
            loginParams["caps_path"] = capsPath;

            CachedUserInfo u = m_userProfileCache.GetUserDetails(agentData.AgentID);
            if (u != null && u.UserProfile != null)
            {
                loginParams["region_uuid"] = u.UserProfile.HomeRegionID.ToString(); // This seems to be always Zero
                //m_log.Debug("  --------- Home Region UUID -------");
                //m_log.Debug("  >> " + loginParams["region_uuid"] + " <<");
                //m_log.Debug("  --------- ---------------- -------");

                string serverURI = "";
                if (u.UserProfile is ForeignUserProfileData)
                    serverURI = HGNetworkServersInfo.ServerURI(((ForeignUserProfileData)u.UserProfile).UserServerURI);
                loginParams["userserver_id"] = (serverURI == "") || (serverURI == null) ? HGNetworkServersInfo.Singleton.LocalUserServerURI : serverURI;

                serverURI = HGNetworkServersInfo.ServerURI(u.UserProfile.UserAssetURI);
                loginParams["assetserver_id"] = (serverURI == "") || (serverURI == null) ? HGNetworkServersInfo.Singleton.LocalAssetServerURI : serverURI;

                serverURI = HGNetworkServersInfo.ServerURI(u.UserProfile.UserInventoryURI);
                loginParams["inventoryserver_id"] = (serverURI == "") || (serverURI == null) ? HGNetworkServersInfo.Singleton.LocalInventoryServerURI : serverURI;

                loginParams["root_folder_id"] = u.UserProfile.RootInventoryFolderID;

                RegionInfo rinfo = RequestNeighbourInfo(u.UserProfile.HomeRegion);
                if (rinfo != null)
                {
                    loginParams["internal_port"] = rinfo.InternalEndPoint.Port.ToString();
                    if (!IsLocalUser(u))
                    {
                        loginParams["regionhandle"] = rinfo.regionSecret; // user.CurrentAgent.Handle.ToString();
                        //m_log.Debug("XXX--- informregionofuser (foreign user) here handle: " + rinfo.regionSecret);

                        loginParams["home_address"]  = ((ForeignUserProfileData)(u.UserProfile)).UserHomeAddress;
                        loginParams["home_port"]     = ((ForeignUserProfileData)(u.UserProfile)).UserHomePort;
                        loginParams["home_remoting"] = ((ForeignUserProfileData)(u.UserProfile)).UserHomeRemotingPort;
                    }
                    else
                    {
                        //m_log.Debug("XXX--- informregionofuser (local user) here handle: " + rinfo.regionSecret);

                        //// local user about to jump out, let's process the name
                        // On second thoughts, let's not do this for the *user*; let's only do it for the *agent*
                        //loginParams["firstname"] = agentData.firstname + "." + agentData.lastname;
                        //loginParams["lastname"] = serversInfo.UserURL;

                        // local user, first time out. let's ask the grid about this user's home region
                        loginParams["regionhandle"] = u.UserProfile.HomeRegion.ToString(); // user.CurrentAgent.Handle.ToString();

                        loginParams["home_address"] = rinfo.ExternalHostName;
                        m_log.Debug("  --------- Home Address -------");
                        m_log.Debug("  >> " + loginParams["home_address"] + " <<");
                        m_log.Debug("  --------- ------------ -------");
                        loginParams["home_port"] = rinfo.HttpPort.ToString();
                        loginParams["home_remoting"] = ConfigSettings.DefaultRegionRemotingPort.ToString(); ;
                    }
                }
                else
                {
                        m_log.Warn("[HGrid]: User's home region info not found: " + u.UserProfile.HomeRegionX + ", " + u.UserProfile.HomeRegionY);
                }
            }

            ArrayList SendParams = new ArrayList();
            SendParams.Add(loginParams);

            // Send
            string uri = "http://" + regInfo.ExternalHostName + ":" + regInfo.HttpPort + "/";
            //m_log.Debug("XXX uri: " + uri);
            XmlRpcRequest request = new XmlRpcRequest("expect_hg_user", SendParams);
            XmlRpcResponse reply;
            try
            {
                reply = request.Send(uri, 6000);
            }
            catch (Exception e)
            {
                m_log.Warn("[HGrid]: Failed to notify region about user. Reason: " + e.Message);
                return false;
            }

            if (!reply.IsFault)
            {
                bool responseSuccess = true;
                if (reply.Value != null)
                {
                    Hashtable resp = (Hashtable)reply.Value;
                    if (resp.ContainsKey("success"))
                    {
                        if ((string)resp["success"] == "FALSE")
                        {
                            responseSuccess = false;
                        }
                    }
                }
                if (responseSuccess)
                {
                    m_log.Info("[HGrid]: Successfully informed remote region about user " + agentData.AgentID);
                    return true;
                }
                else
                {
                    m_log.ErrorFormat("[HGrid]: Region responded that it is not available to receive clients");
                    return false;
                }
            }
            else
            {
                m_log.ErrorFormat("[HGrid]: XmlRpc request to region failed with message {0}, code {1} ", reply.FaultString, reply.FaultCode);
                return false;
            }
        }


        /// <summary>
        /// Received from other HGrid nodes when a user wants to teleport here.  This call allows
        /// the region to prepare for direct communication from the client.  Sends back an empty
        /// xmlrpc response on completion.
        /// This is somewhat similar to OGS1's ExpectUser, but with the additional task of
        /// registering the user in the local user cache.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse ExpectHGUser(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            ForeignUserProfileData userData = new ForeignUserProfileData();

            userData.FirstName = (string)requestData["firstname"];
            userData.SurName = (string)requestData["lastname"];
            userData.ID = new UUID((string)requestData["agent_id"]);
            userData.HomeLocation = new Vector3((float)Convert.ToDecimal((string)requestData["startpos_x"]),
                                  (float)Convert.ToDecimal((string)requestData["startpos_y"]),
                                  (float)Convert.ToDecimal((string)requestData["startpos_z"]));

            userData.UserServerURI = (string)requestData["userserver_id"];
            userData.UserAssetURI = (string)requestData["assetserver_id"];
            userData.UserInventoryURI = (string)requestData["inventoryserver_id"];

            UUID rootID = UUID.Zero;
            UUID.TryParse((string)requestData["root_folder_id"], out rootID);
            userData.RootInventoryFolderID = rootID;

            UUID uuid = UUID.Zero;
            UUID.TryParse((string)requestData["region_uuid"], out uuid);
            userData.HomeRegionID         = uuid; // not quite comfortable about this...
            ulong userRegionHandle        = Convert.ToUInt64((string)requestData["regionhandle"]);
            //userData.HomeRegion           = userRegionHandle;
            userData.UserHomeAddress      = (string)requestData["home_address"];
            userData.UserHomePort         = (string)requestData["home_port"];
            int userhomeinternalport      = Convert.ToInt32((string)requestData["internal_port"]);
            userData.UserHomeRemotingPort = (string)requestData["home_remoting"];


            m_log.DebugFormat("[HGrid]: Prepare for connection from {0} {1} (@{2}) UUID={3}",
                              userData.FirstName, userData.SurName, userData.UserServerURI, userData.ID);
            m_log.Debug("[HGrid]: home_address: " + userData.UserHomeAddress +
                       "; home_port: " + userData.UserHomePort + "; remoting: " + userData.UserHomeRemotingPort);

            XmlRpcResponse resp = new XmlRpcResponse();

            // Let's check if someone is trying to get in with a stolen local identity.
            // The need for this test is a consequence of not having truly global names :-/
            CachedUserInfo uinfo = m_userProfileCache.GetUserDetails(userData.ID);
            if ((uinfo != null) && !(uinfo.UserProfile is ForeignUserProfileData))
            {
                m_log.WarnFormat("[HGrid]: Foreign user trying to get in with local identity. Access denied.");
                Hashtable respdata = new Hashtable();
                respdata["success"] = "FALSE";
                respdata["reason"] = "Foreign user has the same ID as a local user.";
                resp.Value = respdata;
                return resp;
            }

            if (!RegionLoginsEnabled)
            {
                m_log.InfoFormat(
                    "[HGrid]: Denying access for user {0} {1} because region login is currently disabled",
                    userData.FirstName, userData.SurName);

                Hashtable respdata = new Hashtable();
                respdata["success"] = "FALSE";
                respdata["reason"] = "region login currently disabled";
                resp.Value = respdata;
            }
            else
            {
                // Finally, everything looks ok
                //m_log.Debug("XXX---- EVERYTHING OK ---XXX");

                // 1 - Preload the user data
                m_userProfileCache.PreloadUserCache(userData);

                if (m_knownRegions.ContainsKey(userData.ID))
                {
                    // This was left here when the user departed
                    m_knownRegions.Remove(userData.ID);
                }

                // 2 - Load the region info into list of known regions
                RegionInfo rinfo = new RegionInfo();
                rinfo.RegionID         = userData.HomeRegionID;
                rinfo.ExternalHostName = userData.UserHomeAddress;
                rinfo.HttpPort         = Convert.ToUInt32(userData.UserHomePort);
                rinfo.RemotingPort     = Convert.ToUInt32(userData.UserHomeRemotingPort);
                rinfo.RegionID = userData.HomeRegionID;
                // X=0 on the map
                rinfo.RegionLocX = 0;
                rinfo.RegionLocY = (uint)(random.Next(0, Int32.MaxValue)); //(uint)m_knownRegions.Count;
                rinfo.regionSecret = userRegionHandle.ToString();
                //m_log.Debug("XXX--- Here: handle = " + rinfo.regionSecret);
                try
                {
                    rinfo.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)userhomeinternalport);
                }
                catch (Exception e)
                {
                    m_log.Warn("[HGrid]: Exception while constructing internal endpoint: " + e);
                }
                rinfo.RemotingAddress = rinfo.ExternalEndPoint.Address.ToString(); //userData.UserHomeAddress;

                if (!IsComingHome(userData))
                {
                    // Change the user's home region here!!!
                    userData.HomeRegion = rinfo.RegionHandle;
                }

                if (!m_knownRegions.ContainsKey(userData.ID))
                    m_knownRegions.Add(userData.ID, rinfo);

                // 3 - Send the reply
                Hashtable respdata = new Hashtable();
                respdata["success"] = "TRUE";
                resp.Value = respdata;

                DumpUserData(userData);
                DumpRegionData(rinfo);
                
            }

            return resp;
        }

        public bool SendUserInformation(RegionInfo regInfo, AgentCircuitData agentData)
        {
            CachedUserInfo uinfo = m_userProfileCache.GetUserDetails(agentData.AgentID);

            if ((IsLocalUser(uinfo) && IsHyperlinkRegion(regInfo.RegionHandle)) ||
                (!IsLocalUser(uinfo) && !IsGoingHome(uinfo, regInfo)))
            {
                m_log.Info("[HGrid]: Local user is going to foreign region or foreign user is going elsewhere");
                if (!InformRegionOfUser(regInfo, agentData))
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
            if (IsLocalUser(uinfo) && IsHyperlinkRegion(regInfo.RegionHandle))
            {
                agentData.firstname = agentData.firstname + "." + agentData.lastname;
                agentData.lastname = "@" + serversInfo.UserURL.Replace("http://", ""); ; //HGNetworkServersInfo.Singleton.LocalUserServerURI;
            }

            return true;
        }


        #region Methods triggered by calls from external instances

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public void AdjustUserInformation(AgentCircuitData agentData)
        {
            CachedUserInfo uinfo = m_userProfileCache.GetUserDetails(agentData.AgentID);
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
        #endregion


        #region IHyperGrid interface

        public virtual bool IsHyperlinkRegion(ulong ihandle)
        {
            if (GetHyperlinkRegion(ihandle) == null)
                return false;
            else
                return true;
        }

        public virtual RegionInfo GetHyperlinkRegion(ulong ihandle)
        {
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                if (info.RegionHandle == ihandle)
                    return info;
            }

            foreach (RegionInfo info in m_knownRegions.Values)
            {
                if (info.RegionHandle == ihandle)
                    return info;
            }

            return null;
        }

        public virtual ulong FindRegionHandle(ulong ihandle)
        {
            long ohandle = -1;
            List<RegionInfo> rlist = new List<RegionInfo>(m_hyperlinkRegions);
            rlist.AddRange(m_knownRegions.Values);
            foreach (RegionInfo info in rlist)
            {
                if (info.RegionHandle == ihandle)
                {
                    try
                    {
                        ohandle = Convert.ToInt64(info.regionSecret);
                        m_log.Info("[HGrid] remote region " + ohandle);
                    }
                    catch
                    {
                        m_log.Error("[HGrid] Could not convert secret for " + ihandle + " (" + info.regionSecret + ")");
                    }
                    break;
                }
            }
            return ohandle < 0 ? ihandle : (ulong)ohandle;
        }
        #endregion

        #region Misc

        protected bool IsComingHome(ForeignUserProfileData userData)
        {
            return (userData.UserServerURI == HGNetworkServersInfo.Singleton.LocalUserServerURI);
        }

        protected bool IsGoingHome(CachedUserInfo uinfo, RegionInfo rinfo)
        {
            if (uinfo.UserProfile == null)
                return false;

            string userUserServerURI = String.Empty;
            if (uinfo.UserProfile is ForeignUserProfileData)
            {
                userUserServerURI = HGNetworkServersInfo.ServerURI(((ForeignUserProfileData)uinfo.UserProfile).UserServerURI);
            }

            return ((uinfo.UserProfile.HomeRegionID == rinfo.RegionID) &&
                    (userUserServerURI != HGNetworkServersInfo.Singleton.LocalUserServerURI));
        }

        protected bool IsLocalUser(CachedUserInfo uinfo)
        {
            if (uinfo == null)
                return true;

            if (uinfo.UserProfile is ForeignUserProfileData)
                return HGNetworkServersInfo.Singleton.IsLocalUser(((ForeignUserProfileData)uinfo.UserProfile).UserServerURI);
            else
                return true;

        }

        protected bool IsLocalRegion(ulong handle)
        {
            foreach (RegionInfo reg in m_regionsOnInstance)
                if (reg.RegionHandle == handle)
                    return true;
            return false;
        }

        private void DumpUserData(ForeignUserProfileData userData)
        {
            m_log.Info(" ------------ User Data Dump ----------");
            m_log.Info(" >> Name: " + userData.FirstName + " " + userData.SurName);
            m_log.Info(" >> HomeID: " + userData.HomeRegionID);
            m_log.Info(" >> HomeHandle: " + userData.HomeRegion);
            m_log.Info(" >> HomeX: " + userData.HomeRegionX);
            m_log.Info(" >> HomeY: " + userData.HomeRegionY);
            m_log.Info(" >> UserServer: " + userData.UserServerURI);
            m_log.Info(" >> InvServer: " + userData.UserInventoryURI);
            m_log.Info(" >> AssetServer: " + userData.UserAssetURI);
            m_log.Info(" ------------ -------------- ----------");
        }

        private void DumpRegionData(RegionInfo rinfo)
        {
            m_log.Info(" ------------ Region Data Dump ----------");
            m_log.Info(" >> handle: " + rinfo.RegionHandle);
            m_log.Info(" >> coords: " + rinfo.RegionLocX + ", " + rinfo.RegionLocY);
            m_log.Info(" >> secret: " + rinfo.regionSecret);
            m_log.Info(" >> remoting address: " + rinfo.RemotingAddress);
            m_log.Info(" >> remoting port: " + rinfo.RemotingPort);
            m_log.Info(" >> external host name: " + rinfo.ExternalHostName);
            m_log.Info(" >> http port: " + rinfo.HttpPort);
            m_log.Info(" >> external EP address: " + rinfo.ExternalEndPoint.Address);
            m_log.Info(" >> external EP port: " + rinfo.ExternalEndPoint.Port);
            m_log.Info(" ------------ -------------- ----------");
        }


        #endregion


    }
}
