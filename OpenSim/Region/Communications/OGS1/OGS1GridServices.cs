using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1GridServices : IGridServices, IInterRegionCommunications
    {
        public Dictionary<ulong, RegionCommsListener> listeners = new Dictionary<ulong, RegionCommsListener>();
        protected Dictionary<ulong, RegionInfo> regions = new Dictionary<ulong, RegionInfo>();

        public BaseHttpServer httpListener;
        public NetworkServersInfo serversInfo;
        public BaseHttpServer httpServer;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="servers_info"></param>
        /// <param name="httpServe"></param>
        public OGS1GridServices(NetworkServersInfo servers_info, BaseHttpServer httpServe)
        {
            serversInfo = servers_info;
            httpServer = httpServe;
            httpServer.AddXmlRPCHandler("expect_user", this.ExpectUser);
            this.StartRemoting();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            if (!this.regions.ContainsKey((uint)regionInfo.RegionHandle))
            {
                this.regions.Add(regionInfo.RegionHandle, regionInfo);
            }

            Hashtable GridParams = new Hashtable();
            // Login / Authentication

            GridParams["authkey"] = serversInfo.GridSendKey;
            GridParams["UUID"] = regionInfo.SimUUID.ToStringHyphenated();
            GridParams["sim_ip"] = regionInfo.ExternalHostName;
            GridParams["sim_port"] = regionInfo.InternalEndPoint.Port.ToString();
            GridParams["region_locx"] = regionInfo.RegionLocX.ToString();
            GridParams["region_locy"] = regionInfo.RegionLocY.ToString();
            GridParams["sim_name"] = regionInfo.RegionName;
            GridParams["http_port"] = serversInfo.HttpListenerPort.ToString();
            GridParams["remoting_port"] = serversInfo.RemotingListenerPort.ToString();
            GridParams["map-image-id"] = regionInfo.estateSettings.terrainImageID.ToStringHyphenated();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(GridParams);

            // Send Request
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_login", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(serversInfo.GridURL, 3000);
            Hashtable GridRespData = (Hashtable)GridResp.Value;

            Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string)GridRespData["error"];
                MainLog.Instance.Error("Unable to connect to grid: " + errorstring);
                return null;
            }

            // Initialise the background listeners
            RegionCommsListener regListener = new RegionCommsListener();
            if (this.listeners.ContainsKey(regionInfo.RegionHandle))
            {
                this.listeners.Add(regionInfo.RegionHandle, regListener);
            }
            else
            {
                listeners[regionInfo.RegionHandle] = regListener;
            }

            return regListener;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {

            Hashtable respData = MapBlockQuery((int)regionInfo.RegionLocX - 1, (int)regionInfo.RegionLocY - 1, (int)regionInfo.RegionLocX + 1, (int)regionInfo.RegionLocY + 1);

            List<RegionInfo> neighbours = new List<RegionInfo>();

            foreach (ArrayList a in respData.Values)
            {
                foreach (Hashtable n in a)
                {
                    uint regX = Convert.ToUInt32(n["x"]);
                    uint regY = Convert.ToUInt32(n["y"]);
                    if ((regionInfo.RegionLocX != regX) || (regionInfo.RegionLocY != regY))
                    {
                        string externalIpStr = (string)n["sim_ip"];
                        uint port = Convert.ToUInt32(n["sim_port"]);
                        string externalUri = (string)n["sim_uri"];

                        IPEndPoint neighbourInternalEndPoint = new IPEndPoint(IPAddress.Parse(externalIpStr), (int)port);
                        string neighbourExternalUri = externalUri;
                        RegionInfo neighbour = new RegionInfo(regX, regY, neighbourInternalEndPoint, externalIpStr);

                        //OGS1
                        //neighbour.RegionHandle = (ulong)n["regionhandle"]; is now calculated locally

                        neighbour.RegionName = (string)n["name"];

                        //OGS1+
                        neighbour.SimUUID = (string)n["uuid"];

                        neighbours.Add(neighbour);
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
        public RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            if (this.regions.ContainsKey(regionHandle))
            {
                return this.regions[regionHandle];
            }
            //TODO not a region in this instance so ask remote grid server

            Hashtable requestData = new Hashtable();
            requestData["region_handle"] = regionHandle.ToString();
            requestData["authkey"] = this.serversInfo.GridSendKey;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(this.serversInfo.GridURL, 3000);

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
            RegionInfo regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);

            regionInfo.RemotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
            regionInfo.RemotingAddress = internalIpStr;

            regionInfo.SimUUID = new LLUUID((string)responseData["region_UUID"]);
            regionInfo.RegionName = (string)responseData["region_name"];

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
            Hashtable respData = MapBlockQuery(minX, minY, maxX, maxY);

            List<MapBlockData> neighbours = new List<MapBlockData>();

            foreach (ArrayList a in respData.Values)
            {
                foreach (Hashtable n in a)
                {
                    MapBlockData neighbour = new MapBlockData();

                    neighbour.X = Convert.ToUInt16(n["x"]);
                    neighbour.Y = Convert.ToUInt16(n["y"]);

                    neighbour.Name = (string)n["name"];
                    neighbour.Access = Convert.ToByte(n["access"]);
                    neighbour.RegionFlags = Convert.ToUInt32(n["region-flags"]);
                    neighbour.WaterHeight = Convert.ToByte(n["water-height"]);
                    neighbour.MapImageId = new LLUUID((string)n["map-image-id"]);

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
            XmlRpcRequest req = new XmlRpcRequest("map_block", parameters);
            XmlRpcResponse resp = req.Send(serversInfo.GridURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;
            return respData;
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
            Hashtable requestData = (Hashtable)request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new LLUUID((string)requestData["session_id"]);
            agentData.SecureSessionID = new LLUUID((string)requestData["secure_session_id"]);
            agentData.firstname = (string)requestData["firstname"];
            agentData.lastname = (string)requestData["lastname"];
            agentData.AgentID = new LLUUID((string)requestData["agent_id"]);
            agentData.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
            agentData.CapsPath = (string)requestData["caps_path"];

            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                agentData.child = true;
            }
            else
            {
                agentData.startpos = new LLVector3(Convert.ToUInt32(requestData["startpos_x"]), Convert.ToUInt32(requestData["startpos_y"]), Convert.ToUInt32(requestData["startpos_z"]));
                agentData.child = false;

            }

            if (listeners.ContainsKey(Convert.ToUInt64((string)requestData["regionhandle"])))
            {
                this.listeners[Convert.ToUInt64((string)requestData["regionhandle"])].TriggerExpectUser(Convert.ToUInt64((string)requestData["regionhandle"]), agentData);
            }
            else
            {
                MainLog.Instance.Error("ExpectUser() - Unknown region " + ((ulong)requestData["regionhandle"]).ToString());
            }

            MainLog.Instance.Verbose("ExpectUser() - Welcoming new user...");

            return new XmlRpcResponse();
        }

        #region InterRegion Comms
        /// <summary>
        /// 
        /// </summary>
        private void StartRemoting()
        {
            TcpChannel ch = new TcpChannel(this.serversInfo.RemotingListenerPort);
            ChannelServices.RegisterChannel(ch, true);

            WellKnownServiceTypeEntry wellType = new WellKnownServiceTypeEntry(typeof(OGS1InterRegionRemoting), "InterRegions", WellKnownObjectMode.Singleton);
            RemotingConfiguration.RegisterWellKnownServiceType(wellType);
            InterRegionSingleton.Instance.OnArrival += this.IncomingArrival;
            InterRegionSingleton.Instance.OnChildAgent += this.IncomingChildAgent;
        }

        #region Methods called by regions in this instance
        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            if (this.listeners.ContainsKey(regionHandle))
            {
                this.listeners[regionHandle].TriggerExpectUser(regionHandle, agentData);
                return true;
            }
            RegionInfo regInfo = this.RequestNeighbourInfo(regionHandle);
            if (regInfo != null)
            {
                //don't want to be creating a new link to the remote instance every time like we are here
                bool retValue = false;

               
                OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                    typeof(OGS1InterRegionRemoting),
                    "tcp://"+ regInfo.RemotingAddress+":"+regInfo.RemotingPort+"/InterRegions");
                if (remObject != null)
                {
                    
                    retValue = remObject.InformRegionOfChildAgent(regionHandle, agentData);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool ExpectAvatarCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position)
        {
            if (this.listeners.ContainsKey(regionHandle))
            {
                this.listeners[regionHandle].TriggerExpectAvatarCrossing(regionHandle, agentID, position);
                return true;
            }
            RegionInfo regInfo = this.RequestNeighbourInfo(regionHandle);
            if (regInfo != null)
            {
                bool retValue = false;


                OGS1InterRegionRemoting remObject = (OGS1InterRegionRemoting)Activator.GetObject(
                    typeof(OGS1InterRegionRemoting),
                    "tcp://" + regInfo.RemotingAddress + ":" + regInfo.RemotingPort + "/InterRegions");
                if (remObject != null)
                {

                    retValue = remObject.ExpectAvatarCrossing(regionHandle, agentID, position);
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
            if (this.listeners.ContainsKey(regionHandle))
            {
                this.listeners[regionHandle].TriggerExpectUser(regionHandle, agentData);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool IncomingArrival(ulong regionHandle, LLUUID agentID, LLVector3 position)
        {
            if (this.listeners.ContainsKey(regionHandle))
            {
                this.listeners[regionHandle].TriggerExpectAvatarCrossing(regionHandle, agentID, position);
                return true;
            }
            return false;
        }
        #endregion
        #endregion
    }
}
