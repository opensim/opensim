using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

using OpenSim.Framework.Servers;
using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenSim.Framework.Communications;

using Nwc.XmlRpc;
using libsecondlife;
using System.Net;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1GridServices : IGridServices, IInterRegionCommunications
    {
        public Dictionary<ulong, RegionCommsListener> listeners = new Dictionary<ulong, RegionCommsListener>();
        public GridInfo grid;
        public BaseHttpServer httpListener;
        private bool initialised = false;

        public RegionCommsListener RegisterRegion(RegionInfo regionInfo, GridInfo gridInfo)
        {
            Hashtable GridParams = new Hashtable();

            grid = gridInfo;

            // Login / Authentication
            GridParams["authkey"] = gridInfo.GridServerSendKey;
            GridParams["UUID"] = regionInfo.SimUUID.ToStringHyphenated();
            GridParams["sim_ip"] = regionInfo.InternalEndPoint.Address.ToString();
            GridParams["sim_port"] = regionInfo.InternalEndPoint.Port.ToString();

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList(); 
            SendParams.Add(GridParams);

            
            
            // Send Request
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_login", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(gridInfo.GridServerURI, 3000);
            Hashtable GridRespData = (Hashtable)GridResp.Value;
            Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string)GridRespData["error"];
                OpenSim.Framework.Console.MainLog.Instance.Error("Unable to connect to grid: " + errorstring);
                return null;
            }  

            if (!this.listeners.ContainsKey(regionInfo.RegionHandle))
            {
               // initialised = true;
                httpListener = new BaseHttpServer( regionInfo.InternalEndPoint.Port );
                httpListener.AddXmlRPCHandler("expect_user", this.ExpectUser);
                httpListener.Start();
            }

            // Initialise the background listeners
            listeners[regionInfo.RegionHandle] = new RegionCommsListener();

            return listeners[regionInfo.RegionHandle];
        }

        public List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            Hashtable respData = MapBlockQuery((int)regionInfo.RegionLocX - 1, (int)regionInfo.RegionLocY - 1, (int)regionInfo.RegionLocX + 1, (int)regionInfo.RegionLocY + 1);

            List<RegionInfo> neighbours = new List<RegionInfo>();

            foreach (Hashtable n in (Hashtable)respData.Values)
            {
                string internalIpStr = (string)n["sim_ip"];
                int port = (int)n["sim_port"];
                string externalUri = (string)n["sim_uri"];
                
                IPEndPoint neighbourInternalEndPoint = new IPEndPoint( IPAddress.Parse( internalIpStr ), port);
                string neighbourExternalUri = externalUri;

                RegionInfo neighbour = new RegionInfo((uint)n["x"], (uint)n["y"], neighbourInternalEndPoint, neighbourExternalUri );

                //OGS1
                //neighbour.RegionHandle = (ulong)n["regionhandle"]; is now calculated locally

                neighbour.RegionName = (string)n["name"];

                //OGS1+
                neighbour.SimUUID = (string)n["uuid"];

                neighbours.Add(neighbour);
            }

            return neighbours;
        }

        public RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            OpenSim.Framework.Console.MainLog.Instance.Warn("Unimplemented - RequestNeighbourInfo()");
            return null;
        }

        public List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            Hashtable respData = MapBlockQuery(minX, minY, maxX, maxY);

            List<MapBlockData> neighbours = new List<MapBlockData>();

            foreach (Hashtable n in (Hashtable)respData.Values)
            {
                MapBlockData neighbour = new MapBlockData();

                neighbour.X = (ushort)n["x"];
                neighbour.Y = (ushort)n["y"];

                neighbour.Name = (string)n["name"];
                neighbour.Access = (byte)n["access"];
                neighbour.RegionFlags = (uint)n["region-flags"];
                neighbour.WaterHeight = (byte)n["water-height"];
                neighbour.MapImageId = (string)n["map-image-id"];

                neighbours.Add(neighbour);
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
            XmlRpcResponse resp = req.Send(grid.GridServerURI, 3000);
            Hashtable respData = (Hashtable)resp.Value;
            return respData;
        }

        // Grid Request Processing
        public XmlRpcResponse ExpectUser(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.SessionID = new LLUUID((string)requestData["session_id"]);
            agentData.SecureSessionID = new LLUUID((string)requestData["secure_session_id"]);
            agentData.firstname = (string)requestData["firstname"];
            agentData.lastname = (string)requestData["lastname"];
            agentData.AgentID = new LLUUID((string)requestData["agent_id"]);
            agentData.circuitcode = Convert.ToUInt32(requestData["circuit_code"]);
            if (requestData.ContainsKey("child_agent") && requestData["child_agent"].Equals("1"))
            {
                agentData.child = true;
            }
            else
            {
                agentData.startpos = new LLVector3(Convert.ToUInt32(requestData["startpos_x"]), Convert.ToUInt32(requestData["startpos_y"]), Convert.ToUInt32(requestData["startpos_z"]));
                agentData.child = false;

            }

            if (listeners.ContainsKey((ulong)requestData["regionhandle"]))
            {
                this.listeners[(ulong)requestData["regionhandle"]].TriggerExpectUser((ulong)requestData["regionhandle"], agentData);
            }
            else
            {
                OpenSim.Framework.Console.MainLog.Instance.Error("ExpectUser() - Unknown region " + ((ulong)requestData["regionhandle"]).ToString());
            }

            return new XmlRpcResponse();
        }

        #region InterRegion Comms
        private void StartRemoting()
        {
            TcpChannel ch = new TcpChannel(8895);
            ChannelServices.RegisterChannel(ch);

            WellKnownServiceTypeEntry wellType = new WellKnownServiceTypeEntry( Type.GetType("OGS1InterRegionRemoting"), "InterRegions", WellKnownObjectMode.Singleton);
            RemotingConfiguration.RegisterWellKnownServiceType(wellType);
            InterRegionSingleton.Instance.OnArrival += this.IncomingArrival;
            InterRegionSingleton.Instance.OnChildAgent += this.IncomingChildAgent;
        }

        #region Methods called by regions in this instance
        public bool InformRegionOfChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            if (this.listeners.ContainsKey(regionHandle))
            {
                this.listeners[regionHandle].TriggerExpectUser(regionHandle, agentData);
                return true;
            }
            //TODO need to see if we know about where this region is and use .net remoting 
            // to inform it. 
            return false;
        }

        public bool ExpectAvatarCrossing(ulong regionHandle, libsecondlife.LLUUID agentID, libsecondlife.LLVector3 position)
        {
            if (this.listeners.ContainsKey(regionHandle))
            {
                this.listeners[regionHandle].TriggerExpectAvatarCrossing(regionHandle, agentID, position);
                return true;
            }
            //TODO need to see if we know about where this region is and use .net remoting 
            // to inform it. 
            return false;
        }
        #endregion

        #region Methods triggered by calls from external instances
        public bool IncomingChildAgent(ulong regionHandle, AgentCircuitData agentData)
        {
            if (this.listeners.ContainsKey(regionHandle))
            {
                this.listeners[regionHandle].TriggerExpectUser(regionHandle, agentData);
                return true;
            }
            return false;
        }

        public bool IncomingArrival(ulong regionHandle, libsecondlife.LLUUID agentID, libsecondlife.LLVector3 position)
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
