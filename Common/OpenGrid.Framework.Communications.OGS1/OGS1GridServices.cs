using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using OpenSim.Servers;

using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenGrid.Framework.Communications;

using Nwc.XmlRpc;
using libsecondlife;

namespace OpenGrid.Framework.Communications.OGS1
{
    public class OGS1GridServices : IGridServices
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
            GridParams["sim_ip"] = regionInfo.CommsExternalAddress;
            GridParams["sim_port"] = regionInfo.CommsIPListenPort.ToString();

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
            
            // Initialise the background listeners
            listeners[regionInfo.RegionHandle] = new RegionCommsListener();

            if (!initialised)
            {
                initialised = true;
                httpListener = new BaseHttpServer(regionInfo.CommsIPListenPort);
                httpListener.AddXmlRPCHandler("expect_user", this.ExpectUser);
                httpListener.Start();
            }

            return listeners[regionInfo.RegionHandle];
        }

        public List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            Hashtable respData = MapBlockQuery((int)regionInfo.RegionLocX - 1, (int)regionInfo.RegionLocY - 1, (int)regionInfo.RegionLocX + 1, (int)regionInfo.RegionLocY + 1);

            List<RegionInfo> neighbours = new List<RegionInfo>();

            foreach (Hashtable n in (Hashtable)respData.Values)
            {
                RegionInfo neighbour = new RegionInfo();

                //OGS1
                neighbour.RegionHandle = (ulong)n["regionhandle"];
                neighbour.RegionLocX = (uint)n["x"];
                neighbour.RegionLocY = (uint)n["y"];
                neighbour.RegionName = (string)n["name"];

                //OGS1+
                neighbour.CommsIPListenAddr = (string)n["sim_ip"];
                neighbour.CommsIPListenPort = (int)n["sim_port"];
                neighbour.CommsExternalAddress = (string)n["sim_uri"];
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


    }
}
