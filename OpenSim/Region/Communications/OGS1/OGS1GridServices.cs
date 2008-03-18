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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Security.Authentication;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Region.Communications.Local;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1GridServices : IGridServices, IInterRegionCommunications
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private LocalBackEndServices m_localBackend = new LocalBackEndServices();
        private Dictionary<ulong, RegionInfo> m_remoteRegionInfoCache = new Dictionary<ulong, RegionInfo>();
        private List<SimpleRegionInfo> m_knownRegions = new List<SimpleRegionInfo>();
        private Dictionary<ulong, int> m_deadRegionCache = new Dictionary<ulong, int>();
        private Dictionary<string, string> m_queuedGridSettings = new Dictionary<string, string>();

        public BaseHttpServer httpListener;
        public NetworkServersInfo serversInfo;
        public BaseHttpServer httpServer;
        public string _gdebugRegionName = String.Empty;

        public string gdebugRegionName
        {
            get { return _gdebugRegionName; }
            set { _gdebugRegionName = value; }
        }

        public string _rdebugRegionName = String.Empty;

        public string rdebugRegionName
        {
            get { return _rdebugRegionName; }
            set { _rdebugRegionName = value; }
        }

        /// <summary>
        /// Contructor.  Adds "expect_user" and "check" xmlrpc method handlers
        /// </summary>
        /// <param name="servers_info"></param>
        /// <param name="httpServe"></param>
        public OGS1GridServices(NetworkServersInfo servers_info, BaseHttpServer httpServe)
        {
            serversInfo = servers_info;
            httpServer = httpServe;
            //Respond to Grid Services requests
            httpServer.AddXmlRPCHandler("expect_user", ExpectUser);
            httpServer.AddXmlRPCHandler("check", PingCheckReply);

            StartRemoting();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
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
            GridParams["remoting_port"] = NetworkServersInfo.RemotingListenerPort.ToString();
            GridParams["map-image-id"] = regionInfo.EstateSettings.terrainImageID.ToString();
            GridParams["originUUID"] = regionInfo.originRegionID.ToString();
			GridParams["server_uri"] = regionInfo.ServerURI;

            // part of an initial brutish effort to provide accurate information (as per the xml region spec)
            // wrt the ownership of a given region
            // the (very bad) assumption is that this value is being read and handled inconsistently or
            // not at all. Current strategy is to put the code in place to support the validity of this information
            // and to roll forward debugging any issues from that point
            //
            // this particular section of the mod attempts to supply a value from the region's xml file to the grid 
            // server for the UUID of the region's owner (master avatar)
            GridParams["master_avatar_uuid"] = regionInfo.MasterAvatarAssignedUUID.ToString();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            XmlRpcRequest GridReq;
            XmlRpcResponse GridResp;
            try
            {
                GridReq = new XmlRpcRequest("simulator_login", SendParams);
                GridResp = GridReq.Send(serversInfo.GridURL, 16000);
            } catch (Exception ex)
            {
                m_log.Error("Unable to connect to grid. Grid server not running?");
                throw(ex);
            }
            Hashtable GridRespData = (Hashtable)GridResp.Value;
            Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string) GridRespData["error"];
                m_log.Error("Unable to connect to grid: " + errorstring);
                return null;
            }
            else
            {
                m_knownRegions = RequestNeighbours(regionInfo.RegionLocX, regionInfo.RegionLocY);
                if (GridRespData.ContainsKey("allow_forceful_banlines"))
                {
                    if ((string) GridRespData["allow_forceful_banlines"] != "TRUE")
                    {
                        //m_localBackend.SetForcefulBanlistsDisallowed(regionInfo.RegionHandle);
                        m_queuedGridSettings.Add("allow_forceful_banlines", "FALSE");
                    }
                }
            }
            return m_localBackend.RegisterRegion(regionInfo);
        }

        public bool DeregisterRegion(RegionInfo regionInfo)
        {
            Hashtable GridParams = new Hashtable();

            GridParams["UUID"] = regionInfo.RegionID.ToString();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_after_region_moved", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(serversInfo.GridURL, 10000);
            Hashtable GridRespData = (Hashtable) GridResp.Value;

            Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error")) {
                string errorstring = (string)GridRespData["error"];
                m_log.Error("Unable to connect to grid: " + errorstring);
                return false;
            }

			// What does DeregisterRegion() do?
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
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
                        string externalUri = (string) neighbourData["sim_uri"];

                        string externalIpStr = Util.GetHostFromDNS(simIp).ToString();
                        SimpleRegionInfo sri = new SimpleRegionInfo(regX, regY, simIp, port);
                        sri.RemotingPort = Convert.ToUInt32(neighbourData["remoting_port"]);
                        sri.RegionID = new LLUUID((string) neighbourData["uuid"]);

                        neighbours.Add(sri);
                    }
                }
            }

            return neighbours;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(LLUUID Region_UUID)
        {
            RegionInfo regionInfo;
            Hashtable requestData = new Hashtable();
            requestData["region_UUID"] = Region_UUID.ToString();
            requestData["authkey"] = serversInfo.GridSendKey;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(serversInfo.GridURL, 3000);

            Hashtable responseData = (Hashtable) GridResp.Value;

            if (responseData.ContainsKey("error"))
            {
                Console.WriteLine("error received from grid server" + responseData["error"]);
                return null;
            }

            uint regX = Convert.ToUInt32((string) responseData["region_locx"]);
            uint regY = Convert.ToUInt32((string) responseData["region_locy"]);
            string internalIpStr = (string) responseData["sim_ip"];
            uint port = Convert.ToUInt32(responseData["sim_port"]);
            string externalUri = (string) responseData["sim_uri"];

            IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(internalIpStr), (int) port);
            string neighbourExternalUri = externalUri;
            regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);

            regionInfo.RemotingPort = Convert.ToUInt32((string) responseData["remoting_port"]);
            regionInfo.RemotingAddress = internalIpStr;

            regionInfo.RegionID = new LLUUID((string) responseData["region_UUID"]);
            regionInfo.RegionName = (string) responseData["region_name"];

            if (requestData.ContainsKey("regionHandle"))
            {
                m_remoteRegionInfoCache.Add(Convert.ToUInt64((string) requestData["regionHandle"]), regionInfo);
            }

            return regionInfo;
        }

        /// <summary>
        /// 
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

            if (m_remoteRegionInfoCache.TryGetValue(regionHandle, out regionInfo))
            {
            }
            else
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
                        m_log.Error("[OGS1 GRID SERVICES]: Error received from grid server" + responseData["error"]);
                        return null;
                    }

                    uint regX = Convert.ToUInt32((string) responseData["region_locx"]);
                    uint regY = Convert.ToUInt32((string) responseData["region_locy"]);
                    string internalIpStr = (string) responseData["sim_ip"];
                    uint port = Convert.ToUInt32(responseData["sim_port"]);
                    string externalUri = (string) responseData["sim_uri"];

                    IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(internalIpStr), (int) port);
                    string neighbourExternalUri = externalUri;
                    regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);

                    regionInfo.RemotingPort = Convert.ToUInt32((string) responseData["remoting_port"]);
                    regionInfo.RemotingAddress = internalIpStr;

                    regionInfo.RegionID = new LLUUID((string) responseData["region_UUID"]);
                    regionInfo.RegionName = (string) responseData["region_name"];

                    m_remoteRegionInfoCache.Add(regionHandle, regionInfo);
                }
                catch (WebException)
                {
                    m_log.Error("[OGS1 GRID SERVICES]: " +
                                "Region lookup failed for: " + regionHandle.ToString() +
                                " - Is the GridServer down?");
                    return null;
                }
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
                    neighbour.MapImageId = new LLUUID((string) n["map-image-id"]);

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
                m_log.Error("MapBlockQuery XMLRPC failure: " + e.ToString());
                return new Hashtable();
            }
        }

        /// <summary>
        /// A ping / version check
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse PingCheckReply(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            Hashtable respData = new Hashtable();
            respData["online"] = "true";

            m_localBackend.PingCheckReply(respData);

            response.Value = respData;

            return response;
        }


        // Grid Request Processing
        /// <summary>
        /// Received from the user server when a user starts logging in.  This call allows
        /// the region to prepare for direct communication from the client.  Sends back an empty
        /// xmlrpc response on completion.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse ExpectUser(XmlRpcRequest request)
        {
            m_log.Debug("[CONNECTION DEBUGGING]: Expect User called, starting agent setup ... ");
            Hashtable requestData = (Hashtable) request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new LLUUID((string) requestData["session_id"]);
            agentData.SecureSessionID = new LLUUID((string) requestData["secure_session_id"]);
            agentData.firstname = (string) requestData["firstname"];
            agentData.lastname = (string) requestData["lastname"];
            agentData.AgentID = new LLUUID((string) requestData["agent_id"]);
            agentData.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
            agentData.CapsPath = (string) requestData["caps_path"];

            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                m_log.Debug("[CONNECTION DEBUGGING]: Child agent detected");
                agentData.child = true;
            }
            else
            {
                m_log.Debug("[CONNECTION DEBUGGING]: Main agent detected");
                agentData.startpos =
                    new LLVector3(Convert.ToUInt32(requestData["startpos_x"]),
                                  Convert.ToUInt32(requestData["startpos_y"]),
                                  Convert.ToUInt32(requestData["startpos_z"]));
                agentData.child = false;
            }

            ulong regionHandle = Convert.ToUInt64((string) requestData["regionhandle"]);

            m_log.Debug("[CONNECTION DEBUGGING]: Triggering welcome for " + agentData.AgentID.ToString() + " into " + regionHandle.ToString());
            m_localBackend.TriggerExpectUser(regionHandle, agentData);

            m_log.Info("[OGS1 GRID SERVICES]: Welcoming new user...");

            return new XmlRpcResponse();
        }

        #region m_interRegion Comms

        /// <summary>
        /// 
        /// </summary>
        private void StartRemoting()
        {
            TcpChannel ch;
            try
            {
                ch = new TcpChannel((int)NetworkServersInfo.RemotingListenerPort);
                ChannelServices.RegisterChannel(ch, false); // Disabled security as Mono doesnt support this.
            }
            catch (Exception ex)
            {
                m_log.Error("[OGS1 GRID SERVICES]: Exception while attempting to listen on TCP port " + (int)NetworkServersInfo.RemotingListenerPort + ".");
                throw (ex);
            }

            WellKnownServiceTypeEntry wellType =
                new WellKnownServiceTypeEntry(typeof (OGS1InterRegionRemoting), "InterRegions",
                                              WellKnownObjectMode.Singleton);
            RemotingConfiguration.RegisterWellKnownServiceType(wellType);
            InterRegionSingleton.Instance.OnArrival += TriggerExpectAvatarCrossing;
            InterRegionSingleton.Instance.OnChildAgent += IncomingChildAgent;
            InterRegionSingleton.Instance.OnPrimGroupArrival += IncomingPrim;
            InterRegionSingleton.Instance.OnPrimGroupNear += TriggerExpectPrimCrossing;
            InterRegionSingleton.Instance.OnRegionUp += TriggerRegionUp;
            InterRegionSingleton.Instance.OnChildAgentUpdate += TriggerChildAgentUpdate;
            InterRegionSingleton.Instance.OnTellRegionToCloseChildConnection += TriggerTellRegionToCloseChildConnection;
            
        }

        #region Methods called by regions in this instance

        public bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            int failures = 0;
            lock (m_deadRegionCache)
            {
                if (m_deadRegionCache.ContainsKey(regionHandle))
                {
                    failures = m_deadRegionCache[regionHandle];
                }
            }
            if (failures <= 3)
            {
                RegionInfo regInfo = null;
                try
                {
                    if (m_localBackend.ChildAgentUpdate(regionHandle, cAgentData))
                    {
                        return true;
                    }

                    regInfo = RequestNeighbourInfo(regionHandle);
                    if (regInfo != null)
                    {
                        //don't want to be creating a new link to the remote instance every time like we are here
                        bool retValue = false;


                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                                                                                          typeof(OGS1InterRegionRemoting),
                                                                                          "tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.ChildAgentUpdate(regionHandle, cAgentData);
                        }
                        else
                        {
                            m_log.Warn("[OGS1 GRID SERVICES]: remoting object not found");
                        }
                        remObject = null;
                        //m_log.Info("[INTER]: " +
                                                 //gdebugRegionName +
                                                 //": OGS1 tried to Update Child Agent data on outside region and got " +
                                                 //retValue.ToString());

                        return retValue;
                    }
                    NoteDeadRegion(regionHandle);

                    return false;
                }
                catch (RemotingException e)
                {
                    NoteDeadRegion(regionHandle);
                
                    m_log.WarnFormat(
                        "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                    return false;
                }
                catch (SocketException e)
                {
                    NoteDeadRegion(regionHandle);
                
                    m_log.WarnFormat(
                        "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                    return false;
                }
                catch (InvalidCredentialException e)
                {
                    NoteDeadRegion(regionHandle);
                
                    m_log.WarnFormat(
                        "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                    return false;
                }
                catch (AuthenticationException e)
                {
                    NoteDeadRegion(regionHandle);
                
                    m_log.WarnFormat(
                        "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                    return false;
                }
                catch (Exception e)
                {
                    NoteDeadRegion(regionHandle);
                
                    m_log.WarnFormat("[OGS1 GRID SERVICES]: Unable to connect to adjacent region: {0} {1},{2}",
                        regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                    return false;
                }
            }
            else
            {
                //m_log.Info("[INTERREGION]: Skipped Sending Child Update to a region because it failed too many times:" + regionHandle.ToString());
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            RegionInfo regInfo = null;
            try
            {
                if (m_localBackend.InformRegionOfChildAgent(regionHandle, agentData))
                {
                    return true;
                }

                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    //don't want to be creating a new link to the remote instance every time like we are here
                    bool retValue = false;


                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                                                                                          typeof(OGS1InterRegionRemoting),
                                                                                          "tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.InformRegionOfChildAgent(regionHandle, new sAgentCircuitData(agentData));
                        }
                        else
                        {
                            m_log.Warn("[OGS1 GRID SERVICES]: remoting object not found");
                        }
                        remObject = null;
                        m_log.Info("[OGS1 GRID SERVICES]: " +
                                   gdebugRegionName + ": OGS1 tried to InformRegionOfChildAgent for " +
                                   agentData.firstname + " " + agentData.lastname + " and got " +
                                   retValue.ToString());

                        return retValue;

                }
                NoteDeadRegion(regionHandle);
                return false;
            }
            catch (RemotingException e)
            {
                NoteDeadRegion(regionHandle);
                
                m_log.WarnFormat(
                    "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                return false;
            }
            catch (SocketException e)
            {
                NoteDeadRegion(regionHandle);
                
                m_log.WarnFormat(
                    "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                return false;
            }
            catch (InvalidCredentialException e)
            {
                NoteDeadRegion(regionHandle);
                
                m_log.WarnFormat(
                    "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                return false;
            }
            catch (AuthenticationException e)
            {
                NoteDeadRegion(regionHandle);
                
                m_log.WarnFormat(
                    "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                return false;
            }
            catch (Exception e)
            {
                NoteDeadRegion(regionHandle);
                
                m_log.WarnFormat(
                    "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                return false;
            }
        }

        // UGLY!
        public bool RegionUp(SearializableRegionInfo region, ulong regionhandle)
        {
            SearializableRegionInfo regInfo = null;
            try
            {
                // You may ask why this is in here...   
                // The region asking the grid services about itself..  
                // And, surprisingly, the reason is..  it doesn't know 
                // it's own remoting port!  How special.
                region = new SearializableRegionInfo(RequestNeighbourInfo(region.RegionHandle));
                region.RemotingAddress = region.ExternalHostName;
                region.RemotingPort = NetworkServersInfo.RemotingListenerPort;
                if (m_localBackend.RegionUp(region, regionhandle))
                {
                    return true;
                }

                regInfo = new SearializableRegionInfo(RequestNeighbourInfo(regionhandle));
                if (regInfo != null)
                {
                    // If we're not trying to remote to ourselves.
                    if (regInfo.RemotingAddress != region.RemotingAddress && region.RemotingAddress != null)
                    {
                        //don't want to be creating a new link to the remote instance every time like we are here
                        bool retValue = false;


                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting) Activator.GetObject(
                                                                                          typeof (
                                                                                              OGS1InterRegionRemoting),
                                                                                          "tcp://" +
                                                                                          regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.RegionUp(region, regionhandle);
                        }
                        else
                        {
                            m_log.Warn("[OGS1 GRID SERVICES]: remoting object not found");
                        }
                        remObject = null;
                        m_log.Info("[INTER]: " + gdebugRegionName + ": OGS1 tried to inform region I'm up");

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
                m_log.Warn("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY +
                           " - Is this neighbor up?");
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (SocketException e)
            {
                m_log.Warn("[OGS1 GRID SERVICES]: Socket Error: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY +
                           " - Is this neighbor up?");
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (InvalidCredentialException e)
            {
                m_log.Warn("[OGS1 GRID SERVICES]: Invalid Credentials: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (AuthenticationException e)
            {
                m_log.Warn("[OGS1 GRID SERVICES]: Authentication exception: Unable to connect to adjacent region using tcp://" +
                           regInfo.RemotingAddress +
                           ":" + regInfo.RemotingPort +
                           "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (Exception e)
            {
                // This line errors with a Null Reference Exception..    Why?  @.@
                //m_log.Warn("Unknown exception: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                // ":" + regInfo.RemotingPort +
                //"/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY + " - This is likely caused by an incompatibility in the protocol between this sim and that one");
                m_log.Debug(e.ToString());
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool InformRegionOfPrimCrossing(ulong regionHandle, LLUUID primID, string objData)
        {
             int failures = 0;
            lock (m_deadRegionCache)
            {
                if (m_deadRegionCache.ContainsKey(regionHandle))
                {
                    failures = m_deadRegionCache[regionHandle];
                }
            }
            if (failures <= 1)
            {
                RegionInfo regInfo = null;
                try
                {
                    if (m_localBackend.InformRegionOfPrimCrossing(regionHandle, primID, objData))
                    {
                        return true;
                    }

                    regInfo = RequestNeighbourInfo(regionHandle);
                    if (regInfo != null)
                    {
                        //don't want to be creating a new link to the remote instance every time like we are here
                        bool retValue = false;


                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                                                                                          typeof(OGS1InterRegionRemoting),
                                                                                          "tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.InformRegionOfPrimCrossing(regionHandle, primID.UUID, objData);
                        }
                        else
                        {
                            m_log.Warn("[OGS1 GRID SERVICES]: Remoting object not found");
                        }
                        remObject = null;


                        return retValue;
                    }
                    NoteDeadRegion(regionHandle);
                    return false;
                }
                catch (RemotingException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (SocketException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (InvalidCredentialException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[OGS1 GRID SERVICES]: Invalid Credential Exception: Invalid Credentials : " + regionHandle);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (AuthenticationException e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[OGS1 GRID SERVICES]: Authentication exception: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                    return false;
                }
                catch (Exception e)
                {
                    NoteDeadRegion(regionHandle);
                    m_log.Warn("[OGS1 GRID SERVICES]: Unknown exception: Unable to connect to adjacent region: " + regionHandle);
                    m_log.DebugFormat("[OGS1 GRID SERVICES]: {0}", e);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool ExpectAvatarCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            RegionInfo regInfo = null;
            try
            {
                if (m_localBackend.TriggerExpectAvatarCrossing(regionHandle, agentID, position, isFlying))
                {
                    return true;
                }

                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    bool retValue = false;
                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting) Activator.GetObject(
                                                                                      typeof (OGS1InterRegionRemoting),
                                                                                      "tcp://" + regInfo.RemotingAddress +
                                                                                      ":" + regInfo.RemotingPort +
                                                                                      "/InterRegions");
                    if (remObject != null)
                    {
                        retValue =
                            remObject.ExpectAvatarCrossing(regionHandle, agentID.UUID, new sLLVector3(position),
                                                           isFlying);
                    }
                    else
                    {
                        m_log.Warn("[OGS1 GRID SERVICES]: Remoting object not found");
                    }
                    remObject = null;

                    return retValue;
                }
                //TODO need to see if we know about where this region is and use .net remoting 
                // to inform it. 
                NoteDeadRegion(regionHandle);
                return false;
            }
            catch (RemotingException e)
            {
                NoteDeadRegion(regionHandle);
                
                m_log.WarnFormat(
                    "[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: {0} {1},{2}",
                    regInfo.RegionName, regInfo.RegionLocX, regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                
                return false;
            }
            catch
            {
                NoteDeadRegion(regionHandle);
                return false;
            }
        }

        public bool ExpectPrimCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isPhysical)
        {
            RegionInfo regInfo = null;
            try
            {
                if (m_localBackend.TriggerExpectPrimCrossing(regionHandle, agentID, position, isPhysical))
                {
                    return true;
                }

                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    bool retValue = false;
                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting) Activator.GetObject(
                                                                                      typeof (OGS1InterRegionRemoting),
                                                                                      "tcp://" + regInfo.RemotingAddress +
                                                                                      ":" + regInfo.RemotingPort +
                                                                                      "/InterRegions");
                    if (remObject != null)
                    {
                        retValue =
                            remObject.ExpectAvatarCrossing(regionHandle, agentID.UUID, new sLLVector3(position),
                                                           isPhysical);
                    }
                    else
                    {
                        m_log.Warn("[OGS1 GRID SERVICES]: Remoting object not found");
                    }
                    remObject = null;

                    return retValue;
                }
                //TODO need to see if we know about where this region is and use .net remoting 
                // to inform it. 
                NoteDeadRegion(regionHandle);
                return false;
            }
            catch (RemotingException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (SocketException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region: " + regionHandle);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (InvalidCredentialException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Invalid Credential Exception: Invalid Credentials : " + regionHandle);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (AuthenticationException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Authentication exception: Unable to connect to adjacent region: " + regionHandle);                         
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (Exception e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Unknown exception: Unable to connect to adjacent region: " + regionHandle);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0}", e);
                return false;
            }
        }

        public bool TellRegionToCloseChildConnection(ulong regionHandle, LLUUID agentID)
        {
            RegionInfo regInfo = null;
            try
            {
                if (m_localBackend.TriggerTellRegionToCloseChildConnection(regionHandle, agentID))
                {
                    return true;
                }

                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    bool retValue = false;
                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                                                                                      typeof(OGS1InterRegionRemoting),
                                                                                      "tcp://" + regInfo.RemotingAddress +
                                                                                      ":" + regInfo.RemotingPort +
                                                                                      "/InterRegions");
                    if (remObject != null)
                    {
                        retValue =
                            remObject.TellRegionToCloseChildConnection(regionHandle, agentID.UUID);
                    }
                    else
                    {
                        m_log.Warn("[OGS1 GRID SERVICES]: Remoting object not found");
                    }
                    remObject = null;

                    return true;
                }
                //TODO need to see if we know about where this region is and use .net remoting 
                // to inform it. 
                NoteDeadRegion(regionHandle);
                return false;
            }
            catch (RemotingException)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region to tell it to close child agents: " + regInfo.RegionName +
                                      " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                //m_log.Debug(e.ToString());
                return false;
            }
            
            catch (SocketException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Socket Error: Unable to connect to adjacent region using tcp://" +
                                      regInfo.RemotingAddress +
                                      ":" + regInfo.RemotingPort +
                                      "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY +
                                      " - Is this neighbor up?");
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (InvalidCredentialException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Invalid Credentials: Unable to connect to adjacent region using tcp://" +
                                      regInfo.RemotingAddress +
                                      ":" + regInfo.RemotingPort +
                                      "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (AuthenticationException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: Authentication exception: Unable to connect to adjacent region using tcp://" +
                                      regInfo.RemotingAddress +
                                      ":" + regInfo.RemotingPort +
                                      "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (WebException e)
            {
                NoteDeadRegion(regionHandle);
                m_log.Warn("[OGS1 GRID SERVICES]: WebException exception: Unable to connect to adjacent region using tcp://" +
                                      regInfo.RemotingAddress +
                                      ":" + regInfo.RemotingPort +
                                      "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0} {1}", e.Source, e.Message);
                return false;
            }
            catch (Exception e)
            {
                NoteDeadRegion(regionHandle);
                // This line errors with a Null Reference Exception..    Why?  @.@
                //m_log.Warn("Unknown exception: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                // ":" + regInfo.RemotingPort +
                //"/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY + " - This is likely caused by an incompatibility in the protocol between this sim and that one");
                m_log.DebugFormat("[OGS1 GRID SERVICES]: {0}", e);
                return false;
            }
        }

        public bool AcknowledgeAgentCrossed(ulong regionHandle, LLUUID agentId)
        {
            return m_localBackend.AcknowledgeAgentCrossed(regionHandle, agentId);
        }

        public bool AcknowledgePrimCrossed(ulong regionHandle, LLUUID primId)
        {
            return m_localBackend.AcknowledgePrimCrossed(regionHandle, primId);
        }

        #endregion

        #region Methods triggered by calls from external instances

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool IncomingChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            //m_log.Info("[INTER]: " + gdebugRegionName + ": Incoming OGS1 Agent " + agentData.firstname + " " + agentData.lastname);

            try
            {
                return m_localBackend.IncomingChildAgent(regionHandle, agentData);
            }
            catch (RemotingException)
            {
                //m_log.Error("Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        public bool TriggerRegionUp(SearializableRegionInfo regionData, ulong regionhandle)
        {
            m_log.Info("[OGS1 GRID SERVICES]: " +
                       gdebugRegionName + "Incoming OGS1 RegionUpReport:  " + "(" + regionData.RegionLocX +
                       "," + regionData.RegionLocY + "). Giving this region a fresh set of 'dead' tries");

            try
            {
                lock (m_deadRegionCache)
                {
                    if (m_deadRegionCache.ContainsKey(regionData.RegionHandle))
                    {
                        
                        m_deadRegionCache.Remove(regionData.RegionHandle);
                    }
                }

                return m_localBackend.TriggerRegionUp(new RegionInfo(regionData), regionhandle);
            }

            catch (RemotingException e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        public bool TriggerChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            //m_log.Info("[INTER]: Incoming OGS1 Child Agent Data Update");

            try
            {
                return m_localBackend.TriggerChildAgentUpdate(regionHandle, cAgentData);
            }
            catch (RemotingException e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool IncomingPrim(ulong regionHandle, LLUUID primID, string objData)
        {
            // Is this necessary?   
            try
            {
                m_localBackend.TriggerExpectPrim(regionHandle, primID, objData);
                return true;
                //m_localBackend.

            }
            catch (RemotingException e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool TriggerExpectAvatarCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            try
            {
                return m_localBackend.TriggerExpectAvatarCrossing(regionHandle, agentID, position, isFlying);
            }
            catch (RemotingException e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        public bool TriggerExpectPrimCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isPhysical)
        {
            try
            {
                return m_localBackend.TriggerExpectPrimCrossing(regionHandle, agentID, position, isPhysical);
            }
            catch (RemotingException e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        public bool TriggerTellRegionToCloseChildConnection(ulong regionHandle, LLUUID agentID)
        {
            try
            {
                return m_localBackend.TriggerTellRegionToCloseChildConnection(regionHandle, agentID);
            }
            catch (RemotingException)
            {
                m_log.Info("[OGS1 GRID SERVICES]: Remoting Error: Unable to connect to neighbour to tell it to close a child connection");
                return false;
            }
        }

        #endregion

        #endregion

        // helper to see if remote region is up
        bool m_bAvailable = false;
        int timeOut = 10; //10 seconds

        public void CheckRegion(string address, uint port)
        {
            m_bAvailable = false;
            IPAddress ia = null;
            IPAddress.TryParse(address, out ia);
            IPEndPoint m_EndPoint = new IPEndPoint(ia, (int)port);
            AsyncCallback ConnectedMethodCallback = new AsyncCallback(ConnectedMethod);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IAsyncResult ar = socket.BeginConnect(m_EndPoint, ConnectedMethodCallback, socket);
            ar.AsyncWaitHandle.WaitOne(timeOut*1000, false);
        }

        public bool Available
        {
            get { return m_bAvailable; }
        }

        void ConnectedMethod(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            try
            {
                socket.EndConnect(ar);
                m_bAvailable = true;
            }
            catch (Exception)
            {
            }
            socket.Close();
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
    }
}
