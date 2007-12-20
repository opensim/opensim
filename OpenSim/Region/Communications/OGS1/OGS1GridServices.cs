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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
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
        private LocalBackEndServices m_localBackend = new LocalBackEndServices();
        private Dictionary<ulong, RegionInfo> m_remoteRegionInfoCache = new Dictionary<ulong, RegionInfo>();
        private List<SimpleRegionInfo> m_knownRegions = new List<SimpleRegionInfo>();

        public BaseHttpServer httpListener;
        public NetworkServersInfo serversInfo;
        public BaseHttpServer httpServer;
        public string _gdebugRegionName = "";

        public string gdebugRegionName
        {
            get { return _gdebugRegionName; }
            set
            {
                _gdebugRegionName = value;

            }
        }
        public string _rdebugRegionName = "";

        public string rdebugRegionName
        {
            get { return _rdebugRegionName; }
            set
            {
                _rdebugRegionName = value;

            }
        }
        /// <summary>
        /// 
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
            GridParams["UUID"] = regionInfo.RegionID.ToString();
            GridParams["sim_ip"] = regionInfo.ExternalHostName;
            GridParams["sim_port"] = regionInfo.InternalEndPoint.Port.ToString();
            GridParams["region_locx"] = regionInfo.RegionLocX.ToString();
            GridParams["region_locy"] = regionInfo.RegionLocY.ToString();
            GridParams["sim_name"] = regionInfo.RegionName;
            GridParams["http_port"] = serversInfo.HttpListenerPort.ToString();
            GridParams["remoting_port"] = NetworkServersInfo.RemotingListenerPort.ToString();
            GridParams["map-image-id"] = regionInfo.EstateSettings.terrainImageID.ToString();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_login", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(serversInfo.GridURL, 10000);
            Hashtable GridRespData = (Hashtable) GridResp.Value;

            Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string)GridRespData["error"];
                MainLog.Instance.Error("Unable to connect to grid: " + errorstring);
                return null;
            }
            else
            {
                m_knownRegions = RequestNeighbours(regionInfo.RegionLocX, regionInfo.RegionLocY);
                
                
            }
            return m_localBackend.RegisterRegion(regionInfo);
        }

        public bool DeregisterRegion(RegionInfo regionInfo)
        {
            return false;
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

            Hashtable responseData = (Hashtable)GridResp.Value;

            if (responseData.ContainsKey("error"))
            {
                Console.WriteLine("error received from grid server" + responseData["error"]);
                return null;
            }

            uint regX = Convert.ToUInt32((string)responseData["region_locx"]);
            uint regY = Convert.ToUInt32((string)responseData["region_locy"]);
            string internalIpStr = (string)responseData["sim_ip"];
            uint port = Convert.ToUInt32(responseData["sim_port"]);
            string externalUri = (string)responseData["sim_uri"];

            IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(internalIpStr), (int)port);
            string neighbourExternalUri = externalUri;
            regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);

            regionInfo.RemotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
            regionInfo.RemotingAddress = internalIpStr;

            regionInfo.RegionID = new LLUUID((string)responseData["region_UUID"]);
            regionInfo.RegionName = (string)responseData["region_name"];

            if (requestData.ContainsKey("regionHandle"))
            {
                m_remoteRegionInfoCache.Add(Convert.ToUInt64((string)requestData["regionHandle"]), regionInfo);
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

                    Hashtable responseData = (Hashtable)GridResp.Value;

                    if (responseData.ContainsKey("error"))
                    {
                        Console.WriteLine("error received from grid server" + responseData["error"]);
                        return null;
                    }

                    uint regX = Convert.ToUInt32((string)responseData["region_locx"]);
                    uint regY = Convert.ToUInt32((string)responseData["region_locy"]);
                    string internalIpStr = (string)responseData["sim_ip"];
                    uint port = Convert.ToUInt32(responseData["sim_port"]);
                    string externalUri = (string)responseData["sim_uri"];

                    IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(internalIpStr), (int)port);
                    string neighbourExternalUri = externalUri;
                    regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);

                    regionInfo.RemotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                    regionInfo.RemotingAddress = internalIpStr;

                    regionInfo.RegionID = new LLUUID((string)responseData["region_UUID"]);
                    regionInfo.RegionName = (string)responseData["region_name"];

                    m_remoteRegionInfoCache.Add(regionHandle, regionInfo);
                }
                catch (System.Net.WebException)
                {
                    MainLog.Instance.Error("GRID", "Region lookup failed for: " + regionHandle.ToString() + " - Is the GridServer down?");
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
                MainLog.Instance.Error("MapBlockQuery XMLRPC failure: " + e.ToString());
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
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse ExpectUser(XmlRpcRequest request)
        {
            Console.WriteLine("Expecting User...");
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
                agentData.child = true;
            }
            else
            {
                agentData.startpos =
                    new LLVector3(Convert.ToUInt32(requestData["startpos_x"]),
                                  Convert.ToUInt32(requestData["startpos_y"]),
                                  Convert.ToUInt32(requestData["startpos_z"]));
                agentData.child = false;
            }

            ulong regionHandle = Convert.ToUInt64((string) requestData["regionhandle"]);

            m_localBackend.TriggerExpectUser(regionHandle, agentData);

            MainLog.Instance.Verbose("GRID", "Welcoming new user...");

            return new XmlRpcResponse();
        }
        




        
        #region m_interRegion Comms

        /// <summary>
        /// 
        /// </summary>
        private void StartRemoting()
        {
            TcpChannel ch = new TcpChannel((int) NetworkServersInfo.RemotingListenerPort);
            ChannelServices.RegisterChannel(ch, false); // Disabled security as Mono doesnt support this.

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
            //InterRegionSingleton.Instance.OnRegionUp += RegionUp;
        }

        #region Methods called by regions in this instance

        public bool ChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
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
                        Console.WriteLine("remoting object not found");
                    }
                    remObject = null;
                    MainLog.Instance.Verbose("INTER", gdebugRegionName + ": OGS1 tried to Update Child Agent data on outside region and got " + retValue.ToString());

                    return retValue;
                }

                return false;
            }
            catch (RemotingException e)
            {
                MainLog.Instance.Warn("Remoting Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (SocketException e)
            {
                MainLog.Instance.Warn("Socket Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (InvalidCredentialException e)
            {
                MainLog.Instance.Warn("Invalid Credentials: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (AuthenticationException e)
            {
                MainLog.Instance.Warn("Authentication exception: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("Unknown exception: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
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


                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting) Activator.GetObject(
                                                                                      typeof (OGS1InterRegionRemoting),
                                                                                      "tcp://" + regInfo.RemotingAddress +
                                                                                      ":" + regInfo.RemotingPort +
                                                                                      "/InterRegions");

                    if (remObject != null)
                    {
                        retValue = remObject.InformRegionOfChildAgent(regionHandle, new sAgentCircuitData(agentData));
                    }
                    else
                    {
                        Console.WriteLine("remoting object not found");
                    }
                    remObject = null;
                    MainLog.Instance.Verbose("INTER", gdebugRegionName + ": OGS1 tried to InformRegionOfChildAgent for " + agentData.firstname + " " + agentData.lastname + " and got " + retValue.ToString());

                    return retValue;
                }

                return false;
            }
            catch (RemotingException e)
            {
                MainLog.Instance.Warn("Remoting Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (SocketException e)
            {
                MainLog.Instance.Warn("Socket Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (InvalidCredentialException e)
            {
                MainLog.Instance.Warn("Invalid Credentials: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (AuthenticationException e)
            {
                MainLog.Instance.Warn("Authentication exception: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("Unknown exception: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
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
                if (m_localBackend.RegionUp(region,regionhandle))
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


                        OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                                                                                          typeof(OGS1InterRegionRemoting),
                                                                                          "tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions");

                        if (remObject != null)
                        {
                            retValue = remObject.RegionUp(region, regionhandle);
                        }
                        else
                        {
                            Console.WriteLine("remoting object not found");
                        }
                        remObject = null;
                        MainLog.Instance.Verbose("INTER", gdebugRegionName + ": OGS1 tried to inform region I'm up");

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
                MainLog.Instance.Warn("Remoting Error: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY + " - Is this neighbor up?");
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (SocketException e)
            {
                MainLog.Instance.Warn("Socket Error: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY + " - Is this neighbor up?");
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (InvalidCredentialException e)
            {
                MainLog.Instance.Warn("Invalid Credentials: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (AuthenticationException e)
            {
                MainLog.Instance.Warn("Authentication exception: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                                                                                          ":" + regInfo.RemotingPort +
                                                                                          "/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (Exception e)
            {
                // This line errors with a Null Reference Exception..    Why?  @.@
                //MainLog.Instance.Warn("Unknown exception: Unable to connect to adjacent region using tcp://" + regInfo.RemotingAddress +
                                                                                         // ":" + regInfo.RemotingPort +
                                                                                          //"/InterRegions - @ " + regInfo.RegionLocX + "," + regInfo.RegionLocY + " - This is likely caused by an incompatibility in the protocol between this sim and that one");
                MainLog.Instance.Debug(e.ToString());
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
            RegionInfo regInfo = null;
            try
            {
                if (m_localBackend.InformRegionOfPrimCrossing(regionHandle,primID, objData))
                {
                    return true;
                }

                regInfo = RequestNeighbourInfo(regionHandle);
                if (regInfo != null)
                {
                    //don't want to be creating a new link to the remote instance every time like we are here
                    bool retValue = false;


                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting) Activator.GetObject(
                                                                                      typeof (OGS1InterRegionRemoting),
                                                                                      "tcp://" + regInfo.RemotingAddress +
                                                                                      ":" + regInfo.RemotingPort +
                                                                                      "/InterRegions");

                    if (remObject != null)
                    {
                        retValue = remObject.InformRegionOfPrimCrossing(regionHandle,primID.UUID, objData);
                    }
                    else
                    {
                        Console.WriteLine("remoting object not found");
                    }
                    remObject = null;


                    return retValue;
                }

                return false;
            }
            catch (RemotingException e)
            {
                MainLog.Instance.Warn("Remoting Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (SocketException e)
            {
                MainLog.Instance.Warn("Socket Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (InvalidCredentialException e)
            {
                MainLog.Instance.Warn("Invalid Credentials: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (AuthenticationException e)
            {
                MainLog.Instance.Warn("Authentication exception: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("Unknown exception: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
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
                        retValue = remObject.ExpectAvatarCrossing(regionHandle, agentID.UUID, new sLLVector3(position), isFlying);
                    }
                    else
                    {
                        Console.WriteLine("remoting object not found");
                    }
                    remObject = null;

                    return retValue;
                }
                //TODO need to see if we know about where this region is and use .net remoting 
                // to inform it. 
                return false;
            }
            catch (RemotingException e)
            {
                MainLog.Instance.Warn("Remoting Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch
            {
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
                    OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                                                                                      typeof(OGS1InterRegionRemoting),
                                                                                      "tcp://" + regInfo.RemotingAddress +
                                                                                      ":" + regInfo.RemotingPort +
                                                                                      "/InterRegions");
                    if (remObject != null)
                    {
                        retValue = remObject.ExpectAvatarCrossing(regionHandle, agentID.UUID, new sLLVector3(position), isPhysical);
                    }
                    else
                    {
                        Console.WriteLine("remoting object not found");
                    }
                    remObject = null;

                    return retValue;
                }
                //TODO need to see if we know about where this region is and use .net remoting 
                // to inform it. 
                return false;
            }
            catch (RemotingException e)
            {
                MainLog.Instance.Warn("Remoting Error: Unable to connect to adjacent region: " + regInfo.RegionName + " " + regInfo.RegionLocX + "," + regInfo.RegionLocY);
                MainLog.Instance.Debug(e.ToString());
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void TellRegionToCloseChildConnection(ulong regionHandle, LLUUID agentID)
        {

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
            //MainLog.Instance.Verbose("INTER", gdebugRegionName + ": Incoming OGS1 Agent " + agentData.firstname + " " + agentData.lastname);

            try
            {
                return m_localBackend.IncomingChildAgent(regionHandle, agentData);
            }
            catch (RemotingException)
            {
                //MainLog.Instance.Error("Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        public bool TriggerRegionUp(SearializableRegionInfo regionData, ulong regionhandle)
        {
            MainLog.Instance.Verbose("INTER", gdebugRegionName + ": Incoming OGS1 RegionUpReport:  " + regionData.RegionLocX + "," + regionData.RegionLocY);

            try
            {
                return m_localBackend.TriggerRegionUp(new RegionInfo(regionData), regionhandle);
            }

            catch (RemotingException e)
            {
                MainLog.Instance.Error("Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }
        public bool TriggerChildAgentUpdate(ulong regionHandle, ChildAgentDataUpdate cAgentData)
        {
            MainLog.Instance.Verbose("INTER", "Incoming OGS1 Child Agent Data Update");

            try
            {
                return m_localBackend.TriggerChildAgentUpdate(regionHandle, cAgentData);
            }
            catch (RemotingException e)
            {
                MainLog.Instance.Error("Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
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
                //return m_localBackend.TriggerExpectPrim(regionHandle,primID, objData);
                //m_localBackend.
                return false;
            }
            catch (RemotingException e)
            {
                MainLog.Instance.Error("Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
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
                MainLog.Instance.Error("Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
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
                MainLog.Instance.Error("Remoting Error: Unable to connect to adjacent region.\n" + e.ToString());
                return false;
            }
        }

        #endregion

        #endregion
    }
}
