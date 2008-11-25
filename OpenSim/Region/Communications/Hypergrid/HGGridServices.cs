/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Security.Authentication;
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Environment.Modules.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Interfaces;

namespace OpenSim.Region.Communications.Hypergrid
{
    /// <summary>
    /// This class encapsulates the main hypergrid functions related to creating and managing
    /// hyperlinks, as well as processing all the inter-region comms between a region and
    /// an hyperlinked region.
    /// </summary>
    public class HGGridServices : IGridServices, IInterRegionCommunications, IHyperlink
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

        protected AssetCache m_assetcache;
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

        /// <summary>
        /// Contructor.  Adds "expect_hg_user" and "check" xmlrpc method handlers
        /// </summary>
        /// <param name="servers_info"></param>
        /// <param name="httpServe"></param>
        public HGGridServices(NetworkServersInfo servers_info, BaseHttpServer httpServe, AssetCache asscache, SceneManager sman)
        {
            serversInfo = servers_info;
            httpServer = httpServe;
            m_assetcache = asscache;
            m_sceneman = sman;

            httpServer.AddXmlRPCHandler("link_region", LinkRegionRequest);
            httpServer.AddXmlRPCHandler("expect_hg_user", ExpectHGUser);

            HGNetworkServersInfo.Init(servers_info.AssetURL, servers_info.InventoryURL, servers_info.UserURL);
        }

        // see IGridServices
        public virtual RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            // Region doesn't exist here. Trying to link remote region

            m_log.Info("[HGrid]: Linking remote region " + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort );
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
                    //Console.WriteLine("CommsManager- RequestNeighbours() - found a different region in list, checking location");
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
            //Console.WriteLine("RequestNeighbourInfo for " + regionHandle);
            foreach (RegionInfo info in m_hyperlinkRegions)
            {
                //Console.WriteLine("    .. " + info.RegionHandle);
                if (info.RegionHandle == regionHandle) return info;
            }

            foreach (RegionInfo info in m_knownRegions.Values)
            {
                if (info.RegionHandle == regionHandle)
                {
                    //Console.WriteLine("XXX------ Found known region " + info.RegionHandle);
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
//                    Console.WriteLine("ImgID: " + map.MapImageId);
                    map.Agents = 1;
                    map.RegionFlags = 72458694;
                    map.Access = 13;
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
                //Console.WriteLine("JPEG: " + uri);
                c.DownloadFile(uri, info.RegionID.ToString() + ".jpg");
                Bitmap m = new Bitmap(info.RegionID.ToString() + ".jpg");
                //Console.WriteLine("Size: " + m.PhysicalDimension.Height + "-" + m.PhysicalDimension.Width);
                byte[] imageData = OpenJPEG.EncodeFromImage(m, true);
                AssetBase ass = new AssetBase(UUID.Random(), "region " + info.RegionID.ToString());
                info.RegionSettings.TerrainImageID = ass.FullID;
                ass.Type = (int)AssetType.Texture;
                ass.Temporary = false;
                //imageData.CopyTo(ass.Data, 0);
                ass.Data = imageData;
                m_assetcache.AddAsset(ass);
                
            }
            catch (Exception e) // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                Console.WriteLine("Failed getting/storing map image: " + e);
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
                //    Console.WriteLine(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
                try
                {
                    UUID.TryParse((string)hash["uuid"], out uuid);
                    info.RegionID = uuid;
                    if ((string)hash["handle"] != null)
                    {
                        info.regionSecret = (string)hash["handle"];
                        //Console.WriteLine(">> HERE: " + info.regionSecret);
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
                    }
                    if (hash["internal_port"] != null)
                    {
                        int port = Convert.ToInt32((string)hash["internal_port"]);
                        info.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
                        //Console.WriteLine(">> " + info.InternalEndPoint.ToString());
                    }
                    if (hash["remoting_port"] != null)
                    {
                        info.RemotingPort = Convert.ToUInt32(hash["remoting_port"]);
                        //Console.WriteLine(">> " + info.RemotingPort);
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
        public XmlRpcResponse LinkRegionRequest(XmlRpcRequest request)
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
            //Console.WriteLine(">> Here " + regInfo.RegionHandle);
            hash["region_image"] = regInfo.RegionSettings.TerrainImageID.ToString();
            hash["region_name"] = regInfo.RegionName;
            hash["internal_port"] = regInfo.InternalEndPoint.Port.ToString();
            hash["remoting_port"] = NetworkServersInfo.RemotingListenerPort.ToString();
            //Console.WriteLine(">> Here: " + regInfo.InternalEndPoint.Port);


            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        public bool InformRegionOfUser(RegionInfo regInfo, AgentCircuitData agentData)
        {
            ulong regionHandle = regInfo.RegionHandle;
            try
            {
                regionHandle = Convert.ToUInt64(regInfo.regionSecret);
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
                //Console.WriteLine("  --------- Home Region UUID -------");
                //Console.WriteLine("  >> " + loginParams["region_uuid"] + " <<");
                //Console.WriteLine("  --------- ---------------- -------");

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
                        //Console.WriteLine("XXX--- informregionofuser (foreign user) here handle: " + rinfo.regionSecret);

                        loginParams["home_address"]  = ((ForeignUserProfileData)(u.UserProfile)).UserHomeAddress;
                        loginParams["home_port"]     = ((ForeignUserProfileData)(u.UserProfile)).UserHomePort;
                        loginParams["home_remoting"] = ((ForeignUserProfileData)(u.UserProfile)).UserHomeRemotingPort;
                    }
                    else
                    {
                        //Console.WriteLine("XXX--- informregionofuser (local user) here handle: " + rinfo.regionSecret);

                        //// local user about to jump out, let's process the name
                        // On second thoughts, let's not do this for the *user*; let's only do it for the *agent*
                        //loginParams["firstname"] = agentData.firstname + "." + agentData.lastname;
                        //loginParams["lastname"] = serversInfo.UserURL;

                        // local user, first time out. let's ask the grid about this user's home region
                        loginParams["regionhandle"] = u.UserProfile.HomeRegion.ToString(); // user.CurrentAgent.Handle.ToString();

                        loginParams["home_address"] = rinfo.ExternalHostName;
                        Console.WriteLine("  --------- Home Address -------");
                        Console.WriteLine("  >> " + loginParams["home_address"] + " <<");
                        Console.WriteLine("  --------- ------------ -------");
                        loginParams["home_port"] = rinfo.HttpPort.ToString();
                        loginParams["home_remoting"] = NetworkServersInfo.RemotingListenerPort.ToString(); ;
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
            //Console.WriteLine("XXX uri: " + uri);
            XmlRpcRequest request = new XmlRpcRequest("expect_hg_user", SendParams);
            XmlRpcResponse reply = request.Send(uri, 6000);

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
        public XmlRpcResponse ExpectHGUser(XmlRpcRequest request)
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


            m_log.DebugFormat("[HGrid]: Told by user service to prepare for a connection from {0} {1} {2}",
                              userData.FirstName, userData.SurName, userData.ID);
            m_log.Debug("[HGrid]: home_address: " + userData.UserHomeAddress + 
                       "; home_port: " + userData.UserHomePort + "; remoting: " + userData.UserHomeRemotingPort);


            XmlRpcResponse resp = new XmlRpcResponse();

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
                RegionInfo[] regions = m_regionsOnInstance.ToArray();
                //bool banned = false;
                // Just check one region. We assume they all belong to the same estate.
                if ((regions.Length > 0) && (regions[0].EstateSettings.IsBanned(userData.ID)))
                {
                    m_log.InfoFormat(
                        "[HGrid]: Denying access for user {0} {1} because user is banned",
                        userData.FirstName, userData.SurName);

                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "FALSE";
                    respdata["reason"] = "banned";
                    resp.Value = respdata;
                }
                else
                {
                    // Finally, everything looks ok
                    //Console.WriteLine("XXX---- EVERYTHING OK ---XXX");

                    // Nope, let's do it only for the *agent*
                    //// 0 - Switch name if necessary
                    //if (IsComingHome(userData))
                    //{
                    //    string[] parts = userData.FirstName.Split( new char[] {'.'});
                    //    if (parts.Length >= 1)
                    //        userData.FirstName = parts[0];
                    //    if (parts.Length == 2)
                    //        userData.SurName = parts[1];
                    //    else
                    //        m_log.Warn("[HGrid]: Something fishy with user " + userData.FirstName + userData.SurName);

                    //    m_log.Info("[HGrid]: Welcome home, " + userData.FirstName + " " + userData.SurName);
                    //}

                    // 1 - Preload the user data
                    m_userProfileCache.PreloadUserCache(userData.ID, userData);

                    // 2 - Load the region info into list of known regions
                    RegionInfo rinfo = new RegionInfo();
                    rinfo.RegionID         = userData.HomeRegionID;
                    rinfo.ExternalHostName = userData.UserHomeAddress;
                    rinfo.HttpPort         = Convert.ToUInt32(userData.UserHomePort);
                    rinfo.RemotingPort     = Convert.ToUInt32(userData.UserHomeRemotingPort);
                    rinfo.RegionID = userData.HomeRegionID;
                    // X=0 on the map
                    rinfo.RegionLocX = 0;
                    rinfo.RegionLocY = (uint)m_knownRegions.Count;
                    rinfo.regionSecret = userRegionHandle.ToString();
                    //Console.WriteLine("XXX--- Here: handle = " + rinfo.regionSecret);
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
                    else
                        // just update it. The previous one was left there when the user departed
                        m_knownRegions[userData.ID] = rinfo;

                    // 3 - Send the reply
                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "TRUE";
                    resp.Value = respdata;

                    DumpUserData(userData);
                    DumpRegionData(rinfo);
                }
            }

            return resp;
        }

        #region IInterRegionCommunications interface

        public virtual bool AcknowledgeAgentCrossed(ulong regionHandle, UUID agentId) { return false;  }
        public virtual bool AcknowledgePrimCrossed(ulong regionHandle, UUID primID) { return false; }
        public virtual bool CheckRegion(string address, uint port) { return false; }
        public virtual bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData) { return false; }

        public virtual bool ExpectAvatarCrossing(ulong regionHandle, UUID agentID, Vector3 position, bool isFlying) {
            // Remote region
            RegionInfo regInfo = null;
            ulong remoteHandle = 0;
            try
            {
                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    try
                    {
                        remoteHandle = Convert.ToUInt64(regInfo.regionSecret);
                    }
                    catch
                    {
                        m_log.Warn("[HGrid]: Invalid remote region with handle " + regInfo.regionSecret);
                        return false;
                    }
                    //Console.WriteLine("XXX---- Sending Expectavatarcrossing into : " + remoteHandle);

                    bool retValue = false;
                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                        typeof(OGS1InterRegionRemoting),
                        "tcp://" + regInfo.RemotingAddress +
                        ":" + regInfo.RemotingPort +
                        "/InterRegions");

                    if (remObject != null)
                    {
                        retValue =
                            remObject.ExpectAvatarCrossing(remoteHandle, agentID.Guid, new sLLVector3(position),
                                                           isFlying);
                    }
                    else
                    {
                        m_log.Warn("[HGrid]: Remoting object not found");
                    }
                    remObject = null;

                    return retValue;
                }
                //TODO need to see if we know about where this region is and use .net remoting
                // to inform it.
                //NoteDeadRegion(regionHandle);
                return false;
            }
            catch (RemotingException e)
            {
//                NoteDeadRegion(regionHandle);

                m_log.WarnFormat(
                    "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                return false;
            }
            catch
            {
//                NoteDeadRegion(regionHandle);
                return false;
            }

        }

        public virtual bool ExpectPrimCrossing(ulong regionHandle, UUID primID, Vector3 position, bool isFlying) { return false; }

        public virtual bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData) 
        {
            // If we're here, it's because regionHandle is a remote, non-grided region
            m_log.Info("[HGrid]: InformRegionOfChildAgent for " + regionHandle);

            RegionInfo regInfo = GetHyperlinkRegion(regionHandle);
            if (regInfo == null)
                return false;
                
            ulong realHandle = regionHandle;

            CachedUserInfo uinfo = m_userProfileCache.GetUserDetails(agentData.AgentID);
            if ((uinfo == null) || !IsGoingHome(uinfo, regInfo))
            {
                m_log.Info("[HGrid]: User seems to be going to foreign region " + uinfo.UserProfile.FirstName + " " + uinfo.UserProfile.SurName);
                if (!InformRegionOfUser(regInfo, agentData))
                {
                    m_log.Warn("[HGrid]: Could not inform remote region of transferring user.");
                    return false;
                }
            }
            else
                m_log.Info("[HGrid]: User seems to be going home " + uinfo.UserProfile.FirstName + " " + uinfo.UserProfile.SurName);

            try
            {
                // ... and then

                m_log.Debug("[HGrid]: Region is hyperlink.");
                bool retValue = false;
                try
                {
                    regionHandle = Convert.ToUInt64(regInfo.regionSecret);
                }
                catch (Exception)
                {
                    m_log.Warn("[HGrid]: Invalid hyperlink region.");
                    return false;
                }

                OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                    typeof(OGS1InterRegionRemoting),
                    "tcp://" + regInfo.RemotingAddress +
                    ":" + regInfo.RemotingPort +
                    "/InterRegions");

                if (remObject != null)
                {
                    sAgentCircuitData sag = new sAgentCircuitData(agentData);
                    // May need to change agent's name
                    if (IsLocalUser(uinfo))
                    {
                        sag.firstname = agentData.firstname + "." + agentData.lastname;
                        sag.lastname = serversInfo.UserURL; //HGNetworkServersInfo.Singleton.LocalUserServerURI;
                    }
                    retValue = remObject.InformRegionOfChildAgent(regionHandle, sag);
                }
                else
                {
                    m_log.Warn("[HGrid]: remoting object not found");
                }
                remObject = null;
                m_log.Info("[HGrid]: tried to InformRegionOfChildAgent for " +
                       agentData.firstname + " " + agentData.lastname + " and got " +
                       retValue.ToString());

                // Remove the info from this region
                if (m_knownRegions.ContainsKey(uinfo.UserProfile.ID))
                    m_knownRegions.Remove(uinfo.UserProfile.ID);

                return retValue;
            }
            catch (RemotingException e)
            {
                //NoteDeadRegion(regionHandle);

                m_log.WarnFormat(
                    "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                return false;
            }
            catch (SocketException e)
            {
                //NoteDeadRegion(regionHandle);

                m_log.WarnFormat(
                    "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                return false;
            }
            catch (InvalidCredentialException e)
            {
                //NoteDeadRegion(regionHandle);

                m_log.WarnFormat(
                    "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                return false;
            }
            catch (AuthenticationException e)
            {
                //NoteDeadRegion(regionHandle);

                m_log.WarnFormat(
                    "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                return false;
            }
            catch (Exception e)
            {
                //NoteDeadRegion(regionHandle);

                if (regInfo != null)
                {
                    m_log.WarnFormat(
                        "[HGrid]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                }
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);

                return false;
            }


        }

        public virtual bool InformRegionOfPrimCrossing(ulong regionHandle, UUID primID, string objData, int XMLMethod) { return false; }

        public virtual bool RegionUp(SerializableRegionInfo region, ulong regionhandle) {

            ulong realHandle = FindRegionHandle(regionhandle);

            if (realHandle == regionhandle) // something wrong, not remote region
                return false;

            SerializableRegionInfo regInfo = null;
            try
            {
                // You may ask why this is in here...
                // The region asking the grid services about itself..
                // And, surprisingly, the reason is..  it doesn't know
                // it's own remoting port!  How special.
                RegionUpData regiondata = new RegionUpData(region.RegionLocX, region.RegionLocY, region.ExternalHostName, region.InternalEndPoint.Port);

                region = new SerializableRegionInfo(RequestNeighbourInfo(realHandle));
                region.RemotingAddress = region.ExternalHostName;
                region.RemotingPort = NetworkServersInfo.RemotingListenerPort;
                region.HttpPort = serversInfo.HttpListenerPort;

                regInfo = new SerializableRegionInfo(RequestNeighbourInfo(regionhandle));
                if (regInfo != null)
                {
                    // If we're not trying to remote to ourselves.
                    if (regInfo.RemotingAddress != region.RemotingAddress && region.RemotingAddress != null)
                    {
                        //don't want to be creating a new link to the remote instance every time like we are here
                        bool retValue = false;

                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                            typeof(OGS1InterRegionRemoting),
                            "tcp://" +
                            regInfo.RemotingAddress +
                            ":" + regInfo.RemotingPort +
                            "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.RegionUp(regiondata, realHandle);
                        }
                        else
                        {
                            m_log.Warn("[HGrid]: remoting object not found");
                        }
                        remObject = null;

                        m_log.Info(
                            "[HGrid]: tried to inform region I'm up");

                        return retValue;
                    }
                    else
                    {
                        // We're trying to inform ourselves via remoting.
                        // This is here because we're looping over the listeners before we get here.
                        // Odd but it should work.
                        return true;
                    }
                }

                return false;
            }
            catch (RemotingException e)
            {
                m_log.Warn("[HGrid]: Remoting Error: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY +
                           " - Is this neighbor up?");
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (SocketException e)
            {
                m_log.Warn("[HGrid]: Socket Error: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY +
                           " - Is this neighbor up?");
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (InvalidCredentialException e)
            {
                m_log.Warn("[HGrid]: Invalid Credentials: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (AuthenticationException e)
            {
                m_log.Warn("[HGrid]: Authentication exception: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[HGrid]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (Exception e)
            {
                m_log.Debug(e.ToString());
                return false;
            }

        }

        public virtual bool TellRegionToCloseChildConnection(ulong regionHandle, UUID agentID) { return false; }

        public virtual List<UUID> InformFriendsInOtherRegion(UUID agentId, ulong destRegionHandle, List<UUID> friends, bool online)
        {
            return new List<UUID>();
        }

        public virtual bool TriggerTerminateFriend(ulong regionHandle, UUID agentID, UUID exFriendID)
        {
            return true;
        }


        #endregion

        #region Methods triggered by calls from external instances

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        protected bool HGIncomingChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            CachedUserInfo uinfo = m_userProfileCache.GetUserDetails(agentData.AgentID);
            if ((uinfo != null) && (uinfo.UserProfile != null) && 
                (IsLocalUser(uinfo) || !(uinfo.UserProfile is ForeignUserProfileData)))
            {
                //Console.WriteLine("---------------> Local User!");
                string[] parts = agentData.firstname.Split(new char[] { '.' });
                if (parts.Length == 2)
                {
                    agentData.firstname = parts[0];
                    agentData.lastname = parts[1];
                }
            }
            //else
            //    Console.WriteLine("---------------> Foreign User!");
            return true;
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
            Console.WriteLine(" ------------ User Data Dump ----------");
            Console.WriteLine(" >> Name: " + userData.FirstName + " " + userData.SurName);
            Console.WriteLine(" >> HomeID: " + userData.HomeRegionID);
            Console.WriteLine(" >> HomeHandle: " + userData.HomeRegion);
            Console.WriteLine(" >> HomeX: " + userData.HomeRegionX);
            Console.WriteLine(" >> HomeY: " + userData.HomeRegionY);
            Console.WriteLine(" >> UserServer: " + userData.UserServerURI);
            Console.WriteLine(" >> InvServer: " + userData.UserInventoryURI);
            Console.WriteLine(" >> AssetServer: " + userData.UserAssetURI);
            Console.WriteLine(" ------------ -------------- ----------");
        }

        private void DumpRegionData(RegionInfo rinfo)
        {
            Console.WriteLine(" ------------ Region Data Dump ----------");
            Console.WriteLine(" >> handle: " + rinfo.RegionHandle);
            Console.WriteLine(" >> coords: " + rinfo.RegionLocX + ", " + rinfo.RegionLocY);
            Console.WriteLine(" >> secret: " + rinfo.regionSecret);
            Console.WriteLine(" >> remoting address: " + rinfo.RemotingAddress);
            Console.WriteLine(" >> remoting port: " + rinfo.RemotingPort);
            Console.WriteLine(" >> external host name: " + rinfo.ExternalHostName);
            Console.WriteLine(" >> http port: " + rinfo.HttpPort);
            Console.WriteLine(" >> external EP address: " + rinfo.ExternalEndPoint.Address);
            Console.WriteLine(" >> external EP port: " + rinfo.ExternalEndPoint.Port);
            Console.WriteLine(" ------------ -------------- ----------");
        }


        #endregion


    }
}
