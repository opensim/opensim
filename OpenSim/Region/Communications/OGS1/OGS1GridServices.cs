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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Communications.Local;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1GridServices : IGridServices
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_useRemoteRegionCache = true;
        /// <summary>
        /// Encapsulate local backend services for manipulation of local regions
        /// </summary>
        private LocalBackEndServices m_localBackend = new LocalBackEndServices();
        
        private Dictionary<ulong, RegionInfo> m_remoteRegionInfoCache = new Dictionary<ulong, RegionInfo>();
        // private List<SimpleRegionInfo> m_knownRegions = new List<SimpleRegionInfo>();
        private Dictionary<ulong, int> m_deadRegionCache = new Dictionary<ulong, int>();
        private Dictionary<string, string> m_queuedGridSettings = new Dictionary<string, string>();
        private List<RegionInfo> m_regionsOnInstance = new List<RegionInfo>();

        public BaseHttpServer httpListener;
        public NetworkServersInfo serversInfo;
        
        public string gdebugRegionName
        {
            get { return m_localBackend.gdebugRegionName; }
            set { m_localBackend.gdebugRegionName = value; }
        }  

        public string rdebugRegionName
        {
            get { return _rdebugRegionName; }
            set { _rdebugRegionName = value; }
        }
        private string _rdebugRegionName = String.Empty;
        
        public bool RegionLoginsEnabled
        {
            get { return m_localBackend.RegionLoginsEnabled; }
            set { m_localBackend.RegionLoginsEnabled = value; }
        }      

        /// <summary>
        /// Contructor.  Adds "expect_user" and "check" xmlrpc method handlers
        /// </summary>
        /// <param name="servers_info"></param>
        /// <param name="httpServe"></param>
        public OGS1GridServices(NetworkServersInfo servers_info)
        {
            serversInfo = servers_info;

            //Respond to Grid Services requests
            MainServer.Instance.AddXmlRPCHandler("check", PingCheckReply);
        }

        // see IGridServices
        public RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            if (m_regionsOnInstance.Contains(regionInfo))
            {
                m_log.Error("[OGS1 GRID SERVICES]: Foobar! Caller is confused, region already registered " + regionInfo.RegionName);
                Exception e = new Exception(String.Format("Unable to register region"));

                throw e;
            }

            m_log.InfoFormat(
                "[OGS1 GRID SERVICES]: Registering region {0} with grid at {1}",
                regionInfo.RegionName, serversInfo.GridURL);
            
            m_regionsOnInstance.Add(regionInfo);

            Hashtable GridParams = new Hashtable();
            // Login / Authentication

            GridParams["authkey"] = serversInfo.GridSendKey;
            GridParams["recvkey"] = serversInfo.GridRecvKey;
            GridParams["UUID"] = regionInfo.RegionID.ToString();
            GridParams["sim_ip"] = regionInfo.ExternalHostName;
            GridParams["sim_port"] = regionInfo.InternalEndPoint.Port.ToString();
            GridParams["region_locx"] = regionInfo.RegionLocX.ToString();
            GridParams["region_locy"] = regionInfo.RegionLocY.ToString();
            GridParams["sim_name"] = regionInfo.RegionName;
            GridParams["http_port"] = serversInfo.HttpListenerPort.ToString();
            GridParams["remoting_port"] = ConfigSettings.DefaultRegionRemotingPort.ToString();
            GridParams["map-image-id"] = regionInfo.RegionSettings.TerrainImageID.ToString();
            GridParams["originUUID"] = regionInfo.originRegionID.ToString();
            GridParams["server_uri"] = regionInfo.ServerURI;
            GridParams["region_secret"] = regionInfo.regionSecret;
            GridParams["major_interface_version"] = VersionInfo.MajorInterfaceVersion.ToString();

            if (regionInfo.MasterAvatarAssignedUUID != UUID.Zero)
                GridParams["master_avatar_uuid"] = regionInfo.MasterAvatarAssignedUUID.ToString();
            else
                GridParams["master_avatar_uuid"] = regionInfo.EstateSettings.EstateOwner.ToString();

            GridParams["maturity"] = regionInfo.RegionSettings.Maturity.ToString();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_login", SendParams);
            XmlRpcResponse GridResp;
            
            try
            {                
                // The timeout should always be significantly larger than the timeout for the grid server to request
                // the initial status of the region before confirming registration.
                GridResp = GridReq.Send(serversInfo.GridURL, 9999999);
            }
            catch (Exception e)
            {
                Exception e2
                    = new Exception(
                        String.Format(
                            "Unable to register region with grid at {0}. Grid service not running?", 
                            serversInfo.GridURL),
                        e);

                throw e2;
            }

            Hashtable GridRespData = (Hashtable)GridResp.Value;
            // Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string) GridRespData["error"];

                Exception e = new Exception(
                    String.Format("Unable to connect to grid at {0}: {1}", serversInfo.GridURL, errorstring));

                throw e;
            }
            else
            {
                // m_knownRegions = RequestNeighbours(regionInfo.RegionLocX, regionInfo.RegionLocY);
                if (GridRespData.ContainsKey("allow_forceful_banlines"))
                {
                    if ((string) GridRespData["allow_forceful_banlines"] != "TRUE")
                    {
                        //m_localBackend.SetForcefulBanlistsDisallowed(regionInfo.RegionHandle);
                        if (!m_queuedGridSettings.ContainsKey("allow_forceful_banlines"))
                            m_queuedGridSettings.Add("allow_forceful_banlines", "FALSE");
                    }
                }

                m_log.InfoFormat(
                    "[OGS1 GRID SERVICES]: Region {0} successfully registered with grid at {1}",
                    regionInfo.RegionName, serversInfo.GridURL);
            }
            
            return m_localBackend.RegisterRegion(regionInfo);
        }

        // see IGridServices
        public bool DeregisterRegion(RegionInfo regionInfo)
        {
            Hashtable GridParams = new Hashtable();

            GridParams["UUID"] = regionInfo.RegionID.ToString();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_after_region_moved", SendParams);
            XmlRpcResponse GridResp = null;
            
            try
            {            
                GridResp = GridReq.Send(serversInfo.GridURL, 10000);
            }
            catch (Exception e)
            {
                Exception e2
                    = new Exception(
                        String.Format(
                            "Unable to deregister region with grid at {0}. Grid service not running?", 
                            serversInfo.GridURL),
                        e);

                throw e2;
            }
        
            Hashtable GridRespData = (Hashtable) GridResp.Value;

            // Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData != null && GridRespData.ContainsKey("error"))
            {
                string errorstring = (string)GridRespData["error"];
                m_log.Error("Unable to connect to grid: " + errorstring);
                return false;
            }

            return m_localBackend.DeregisterRegion(regionInfo);
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
        public List<SimpleRegionInfo> RequestNeighbours(uint x, uint y)
        {
            Hashtable respData = MapBlockQuery((int) x - 1, (int) y - 1, (int) x + 1, (int) y + 1);

            List<SimpleRegionInfo> neighbours = new List<SimpleRegionInfo>();

            foreach (ArrayList neighboursList in respData.Values)
            {
                foreach (Hashtable neighbourData in neighboursList)
                {
                    uint regX = Convert.ToUInt32(neighbourData["x"]);
                    uint regY = Convert.ToUInt32(neighbourData["y"]);
                    if ((x != regX) || (y != regY))
                    {
                        string simIp = (string) neighbourData["sim_ip"];

                        uint port = Convert.ToUInt32(neighbourData["sim_port"]);
                        // string externalUri = (string) neighbourData["sim_uri"];

                        // string externalIpStr = String.Empty;
                        try
                        {
                            // externalIpStr = Util.GetHostFromDNS(simIp).ToString();
                            Util.GetHostFromDNS(simIp).ToString();
                        }
                        catch (SocketException e)
                        {
                            m_log.WarnFormat(
                                "[OGS1 GRID SERVICES]: RequestNeighbours(): Lookup of neighbour {0} failed!  Not including in neighbours list.  {1}",
                                simIp, e);

                            continue;
                        }

                        SimpleRegionInfo sri = new SimpleRegionInfo(regX, regY, simIp, port);

                        sri.RemotingPort = Convert.ToUInt32(neighbourData["remoting_port"]);

                        if (neighbourData.ContainsKey("http_port"))
                        {
                            sri.HttpPort = Convert.ToUInt32(neighbourData["http_port"]);
                        }
                        else
                        {
                            m_log.Error("[OGS1 GRID SERVICES]: Couldn't find httpPort, using default 9000; please upgrade your grid-server to r7621 or later");
                            sri.HttpPort = 9000; // that's the default and will probably be wrong
                        }

                        sri.RegionID = new UUID((string) neighbourData["uuid"]);

                        neighbours.Add(sri);
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
        public RegionInfo RequestNeighbourInfo(UUID Region_UUID)
        {
            // don't ask the gridserver about regions on this instance...
            foreach (RegionInfo info in m_regionsOnInstance)
            {
                if (info.RegionID == Region_UUID) return info;
            }

            // didn't find it so far, we have to go the long way
            RegionInfo regionInfo;
            Hashtable requestData = new Hashtable();
            requestData["region_UUID"] = Region_UUID.ToString();
            requestData["authkey"] = serversInfo.GridSendKey;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);
            XmlRpcRequest gridReq = new XmlRpcRequest("simulator_data_request", SendParams);
            XmlRpcResponse gridResp = null;

            try
            {
                gridResp = gridReq.Send(serversInfo.GridURL, 3000);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[OGS1 GRID SERVICES]: Communication with the grid server at {0} failed, {1}",
                    serversInfo.GridURL, e);

                return null;
            }

            Hashtable responseData = (Hashtable)gridResp.Value;

            if (responseData.ContainsKey("error"))
            {
                m_log.WarnFormat("[OGS1 GRID SERVICES]: Error received from grid server: {0}", responseData["error"]);
                return null;
            }

            regionInfo = buildRegionInfo(responseData, String.Empty);
            if ((m_useRemoteRegionCache) && (requestData.ContainsKey("regionHandle")))
            {
                m_remoteRegionInfoCache.Add(Convert.ToUInt64((string) requestData["regionHandle"]), regionInfo);
            }

            return regionInfo;
        }

        /// <summary>
        /// Request information about a region.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            RegionInfo regionInfo = m_localBackend.RequestNeighbourInfo(regionHandle);

            if (regionInfo != null)
            {
                return regionInfo;
            }

            if ((!m_useRemoteRegionCache) || (!m_remoteRegionInfoCache.TryGetValue(regionHandle, out regionInfo)))
            {
                try
                {
                    Hashtable requestData = new Hashtable();
                    requestData["region_handle"] = regionHandle.ToString();
                    requestData["authkey"] = serversInfo.GridSendKey;
                    ArrayList SendParams = new ArrayList();
                    SendParams.Add(requestData);
                    XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
                    XmlRpcResponse GridResp = GridReq.Send(serversInfo.GridURL, 3000);

                    Hashtable responseData = (Hashtable) GridResp.Value;

                    if (responseData.ContainsKey("error"))
                    {
                        m_log.Error("[OGS1 GRID SERVICES]: Error received from grid server: " + responseData["error"]);
                        return null;
                    }

                    uint regX = Convert.ToUInt32((string) responseData["region_locx"]);
                    uint regY = Convert.ToUInt32((string) responseData["region_locy"]);
                    string externalHostName = (string) responseData["sim_ip"];
                    uint simPort = Convert.ToUInt32(responseData["sim_port"]);
                    string regionName = (string)responseData["region_name"];
                    UUID regionID = new UUID((string)responseData["region_UUID"]);
                    uint remotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                    
                    uint httpPort = 9000;
                    if (responseData.ContainsKey("http_port"))
                    {
                        httpPort = Convert.ToUInt32((string)responseData["http_port"]);
                    }

                    // Ok, so this is definitively the wrong place to do this, way too hard coded, but it doesn't seem we GET this info?

                    string simURI = "http://" + externalHostName + ":" + simPort;

                    // string externalUri = (string) responseData["sim_uri"];

                    //IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(internalIpStr), (int) port);
                    regionInfo = RegionInfo.Create(regionID, regionName, regX, regY, externalHostName, httpPort, simPort, remotingPort, simURI);

                    if (m_useRemoteRegionCache)
                    {
                        lock (m_remoteRegionInfoCache)
                        {
                            if (!m_remoteRegionInfoCache.ContainsKey(regionHandle))
                            {
                                m_remoteRegionInfoCache.Add(regionHandle, regionInfo);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[OGS1 GRID SERVICES]: " +
                                "Region lookup failed for: " + regionHandle.ToString() +
                                " - Is the GridServer down?" + e.ToString());
                    return null;
                }
            }

            return regionInfo;
        }

        /// <summary>
        /// Get information about a neighbouring region
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(string name)
        {
            // Not implemented yet
            return null;
        }

        /// <summary>
        /// Get information about a neighbouring region
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(string host, uint port)
        {
            // Not implemented yet
            return null;
        }

        public RegionInfo RequestClosestRegion(string regionName)
        {
            if (m_useRemoteRegionCache)
            {
                foreach (RegionInfo ri in m_remoteRegionInfoCache.Values)
                {
                    if (ri.RegionName == regionName)
                        return ri;
                }
            }

            RegionInfo regionInfo = null;
            try
            {
                Hashtable requestData = new Hashtable();
                requestData["region_name_search"] = regionName;
                requestData["authkey"] = serversInfo.GridSendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);
                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(serversInfo.GridURL, 3000);

                Hashtable responseData = (Hashtable) GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    m_log.ErrorFormat("[OGS1 GRID SERVICES]: Error received from grid server: ", responseData["error"]);
                    return null;
                }

                regionInfo = buildRegionInfo(responseData, "");

                if ((m_useRemoteRegionCache) && (!m_remoteRegionInfoCache.ContainsKey(regionInfo.RegionHandle)))
                    m_remoteRegionInfoCache.Add(regionInfo.RegionHandle, regionInfo);
            }
            catch
            {
                m_log.Error("[OGS1 GRID SERVICES]: " +
                            "Region lookup failed for: " + regionName +
                            " - Is the GridServer down?");
            }

            return regionInfo;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            int temp = 0;

            if (minX > maxX)
            {
                temp = minX;
                minX = maxX;
                maxX = temp;
            }
            if (minY > maxY)
            {
                temp = minY;
                minY = maxY;
                maxY = temp;
            }

            Hashtable respData = MapBlockQuery(minX, minY, maxX, maxY);

            List<MapBlockData> neighbours = new List<MapBlockData>();

            foreach (ArrayList a in respData.Values)
            {
                foreach (Hashtable n in a)
                {
                    MapBlockData neighbour = new MapBlockData();

                    neighbour.X = Convert.ToUInt16(n["x"]);
                    neighbour.Y = Convert.ToUInt16(n["y"]);

                    neighbour.Name = (string) n["name"];
                    neighbour.Access = Convert.ToByte(n["access"]);
                    neighbour.RegionFlags = Convert.ToUInt32(n["region-flags"]);
                    neighbour.WaterHeight = Convert.ToByte(n["water-height"]);
                    neighbour.MapImageId = new UUID((string) n["map-image-id"]);

                    neighbours.Add(neighbour);
                }
            }

            return neighbours;
        }

        /// <summary>
        /// Performs a XML-RPC query against the grid server returning mapblock information in the specified coordinates
        /// </summary>
        /// <remarks>REDUNDANT - OGS1 is to be phased out in favour of OGS2</remarks>
        /// <param name="minX">Minimum X value</param>
        /// <param name="minY">Minimum Y value</param>
        /// <param name="maxX">Maximum X value</param>
        /// <param name="maxY">Maximum Y value</param>
        /// <returns>Hashtable of hashtables containing map data elements</returns>
        private Hashtable MapBlockQuery(int minX, int minY, int maxX, int maxY)
        {
            Hashtable param = new Hashtable();
            param["xmin"] = minX;
            param["ymin"] = minY;
            param["xmax"] = maxX;
            param["ymax"] = maxY;
            IList parameters = new ArrayList();
            parameters.Add(param);
            
            try
            {
                XmlRpcRequest req = new XmlRpcRequest("map_block", parameters);
                XmlRpcResponse resp = req.Send(serversInfo.GridURL, 10000);
                Hashtable respData = (Hashtable) resp.Value;
                return respData;
            }
            catch (Exception e)
            {
                m_log.Error("MapBlockQuery XMLRPC failure: " + e);
                return new Hashtable();
            }
        }

        /// <summary>
        /// A ping / version check
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse PingCheckReply(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            Hashtable respData = new Hashtable();
            respData["online"] = "true";

            m_localBackend.PingCheckReply(respData);

            response.Value = respData;

            return response;
        }

        /// <summary>
        /// Received from the user server when a user starts logging in.  This call allows
        /// the region to prepare for direct communication from the client.  Sends back an empty
        /// xmlrpc response on completion.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse ExpectUser(XmlRpcRequest request)
        {            
            Hashtable requestData = (Hashtable) request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new UUID((string) requestData["session_id"]);
            agentData.SecureSessionID = new UUID((string) requestData["secure_session_id"]);
            agentData.firstname = (string) requestData["firstname"];
            agentData.lastname = (string) requestData["lastname"];
            agentData.AgentID = new UUID((string) requestData["agent_id"]);
            agentData.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
            agentData.CapsPath = (string)requestData["caps_path"];
            ulong regionHandle = Convert.ToUInt64((string) requestData["regionhandle"]);

            // Appearance
            if (requestData["appearance"] != null)
                agentData.Appearance = new AvatarAppearance((Hashtable)requestData["appearance"]);

            m_log.DebugFormat(
                "[CLIENT]: Told by user service to prepare for a connection from {0} {1} {2}, circuit {3}",
                agentData.firstname, agentData.lastname, agentData.AgentID, agentData.circuitcode);            

            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                //m_log.Debug("[CLIENT]: Child agent detected");
                agentData.child = true;
            }
            else
            {
                //m_log.Debug("[CLIENT]: Main agent detected");
                agentData.startpos =
                    new Vector3((float)Convert.ToDecimal((string)requestData["startpos_x"]),
                                  (float)Convert.ToDecimal((string)requestData["startpos_y"]),
                                  (float)Convert.ToDecimal((string)requestData["startpos_z"]));
                agentData.child = false;
            }

            XmlRpcResponse resp = new XmlRpcResponse();
                        
            if (!RegionLoginsEnabled)
            {
                m_log.InfoFormat(
                    "[CLIENT]: Denying access for user {0} {1} because region login is currently disabled",
                    agentData.firstname, agentData.lastname);

                Hashtable respdata = new Hashtable();
                respdata["success"] = "FALSE";
                respdata["reason"] = "region login currently disabled";
                resp.Value = respdata;                
            }
            else
            {
                RegionInfo[] regions = m_regionsOnInstance.ToArray();
                bool banned = false;

                for (int i = 0; i < regions.Length; i++)
                {
                    if (regions[i] != null)
                    {
                        if (regions[i].RegionHandle == regionHandle)
                        {
                            if (regions[i].EstateSettings.IsBanned(agentData.AgentID))
                            {
                                banned = true;
                                break;
                            }
                        }
                    }
                }                
            
                if (banned)
                {
                    m_log.InfoFormat(
                        "[CLIENT]: Denying access for user {0} {1} because user is banned",
                        agentData.firstname, agentData.lastname);

                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "FALSE";
                    respdata["reason"] = "banned";
                    resp.Value = respdata;
                }
                else
                {
                    m_localBackend.TriggerExpectUser(regionHandle, agentData);
                    Hashtable respdata = new Hashtable();
                    respdata["success"] = "TRUE";
                    resp.Value = respdata;
                }
            }

            return resp;
        }
        
        // Grid Request Processing
        /// <summary>
        /// Ooops, our Agent must be dead if we're getting this request!
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LogOffUser(XmlRpcRequest request)
        {
            m_log.Debug("[CONNECTION DEBUGGING]: LogOff User Called");
            
            Hashtable requestData = (Hashtable)request.Params[0];
            string message = (string)requestData["message"];
            UUID agentID = UUID.Zero;
            UUID RegionSecret = UUID.Zero;
            UUID.TryParse((string)requestData["agent_id"], out agentID);
            UUID.TryParse((string)requestData["region_secret"], out RegionSecret);

            ulong regionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);

            m_localBackend.TriggerLogOffUser(regionHandle, agentID, RegionSecret,message);

            return new XmlRpcResponse();
        }

        public void NoteDeadRegion(ulong regionhandle)
        {
            lock (m_deadRegionCache)
            {
                if (m_deadRegionCache.ContainsKey(regionhandle))
                {
                    m_deadRegionCache[regionhandle] = m_deadRegionCache[regionhandle] + 1;
                }
                else
                {
                    m_deadRegionCache.Add(regionhandle, 1);
                }
            }
        }

        public LandData RequestLandData (ulong regionHandle, uint x, uint y)
        {
            m_log.DebugFormat("[OGS1 GRID SERVICES] requests land data in {0}, at {1}, {2}",
                              regionHandle, x, y);
            LandData landData = m_localBackend.RequestLandData(regionHandle, x, y);
            if (landData == null)
            {
                Hashtable hash = new Hashtable();
                hash["region_handle"] = regionHandle.ToString();
                hash["x"] = x.ToString();
                hash["y"] = y.ToString();

                IList paramList = new ArrayList();
                paramList.Add(hash);

                try
                {
                    // this might be cached, as we probably requested it just a moment ago...
                    RegionInfo info = RequestNeighbourInfo(regionHandle);
                    if (info != null) // just to be sure
                    {
                        XmlRpcRequest request = new XmlRpcRequest("land_data", paramList);
                        string uri = "http://" + info.ExternalEndPoint.Address + ":" + info.HttpPort + "/";
                        XmlRpcResponse response = request.Send(uri, 10000);
                        if (response.IsFault)
                        {
                            m_log.ErrorFormat("[OGS1 GRID SERVICES] remote call returned an error: {0}", response.FaultString);
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
                                m_log.DebugFormat("[OGS1 GRID SERVICES] Got land data for parcel {0}", landData.Name);
                            }
                            catch (Exception e)
                            {
                                m_log.Error("[OGS1 GRID SERVICES] Got exception while parsing land-data:", e);
                            }
                        }
                    }
                    else m_log.WarnFormat("[OGS1 GRID SERVICES] Couldn't find region with handle {0}", regionHandle);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[OGS1 GRID SERVICES] Couldn't contact region {0}: {1}", regionHandle, e);
                }
            }
            return landData;
        }

        // Grid Request Processing
        /// <summary>
        /// Someone asked us about parcel-information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LandData(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            ulong regionHandle = Convert.ToUInt64(requestData["region_handle"]);
            uint x = Convert.ToUInt32(requestData["x"]);
            uint y = Convert.ToUInt32(requestData["y"]);
            m_log.DebugFormat("[OGS1 GRID SERVICES]: Got XML reqeuest for land data at {0}, {1} in region {2}", x, y, regionHandle);

            LandData landData = m_localBackend.RequestLandData(regionHandle, x, y);
            Hashtable hash = new Hashtable();
            if (landData != null)
            {
                // for now, only push out the data we need for answering a ParcelInfoReqeust
                hash["AABBMax"] = landData.AABBMax.ToString();
                hash["AABBMin"] = landData.AABBMin.ToString();
                hash["Area"] = landData.Area.ToString();
                hash["AuctionID"] = landData.AuctionID.ToString();
                hash["Description"] = landData.Description;
                hash["Flags"] = landData.Flags.ToString();
                hash["GlobalID"] = landData.GlobalID.ToString();
                hash["Name"] = landData.Name;
                hash["OwnerID"] = landData.OwnerID.ToString();
                hash["SalePrice"] = landData.SalePrice.ToString();
                hash["SnapshotID"] = landData.SnapshotID.ToString();
                hash["UserLocation"] = landData.UserLocation.ToString();
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        public List<RegionInfo> RequestNamedRegions (string name, int maxNumber)
        {
            // no asking of the local backend first, here, as we have to ask the gridserver anyway.
            Hashtable hash = new Hashtable();
            hash["name"] = name;
            hash["maxNumber"] = maxNumber.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            Hashtable result = XmlRpcSearchForRegionByName(paramList);
            if (result == null) return null;

            uint numberFound = Convert.ToUInt32(result["numFound"]);
            List<RegionInfo> infos = new List<RegionInfo>();
            for (int i = 0; i < numberFound; ++i)
            {
                string prefix = "region" + i + ".";
                RegionInfo info = buildRegionInfo(result, prefix);
                infos.Add(info);
            }
            return infos;
        }

        private RegionInfo buildRegionInfo(Hashtable responseData, string prefix)
        {
            uint regX = Convert.ToUInt32((string) responseData[prefix + "region_locx"]);
            uint regY = Convert.ToUInt32((string) responseData[prefix + "region_locy"]);
            string internalIpStr = (string) responseData[prefix + "sim_ip"];
            uint port = Convert.ToUInt32(responseData[prefix + "sim_port"]);

            IPEndPoint neighbourInternalEndPoint = new IPEndPoint(Util.GetHostFromDNS(internalIpStr), (int) port);

            RegionInfo regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);
            regionInfo.RemotingPort = Convert.ToUInt32((string) responseData[prefix + "remoting_port"]);
            regionInfo.RemotingAddress = internalIpStr;

            if (responseData.ContainsKey(prefix + "http_port"))
            {
                regionInfo.HttpPort = Convert.ToUInt32((string) responseData[prefix + "http_port"]);
            }

            regionInfo.RegionID = new UUID((string) responseData[prefix + "region_UUID"]);
            regionInfo.RegionName = (string) responseData[prefix + "region_name"];

            regionInfo.RegionSettings.TerrainImageID = new UUID((string) responseData[prefix + "map_UUID"]);
            return regionInfo;
        }

        private Hashtable XmlRpcSearchForRegionByName(IList parameters)
        {
            try
            {
                XmlRpcRequest request = new XmlRpcRequest("search_for_region_by_name", parameters);
                XmlRpcResponse resp = request.Send(serversInfo.GridURL, 10000);
                Hashtable respData = (Hashtable) resp.Value;
                if (respData != null && respData.Contains("faultCode"))
                {
                    m_log.WarnFormat("[OGS1 GRID SERVICES]: Got an error while contacting GridServer: {0}", respData["faultString"]);
                    return null;
                }

                return respData;
            }
            catch (Exception e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: MapBlockQuery XMLRPC failure: ", e);
                return null;
            }
        }
    }
}
