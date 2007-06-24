using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenGrid.Framework.Communications;

using Nwc.XmlRpc;

namespace OpenGrid.Framework.Communications.OGS1
{
    public class OGS1GridServices : IGridServices
    {
        public RegionCommsListener listener;

        public RegionCommsListener RegisterRegion(RegionInfo regionInfo, GridInfo gridInfo)
        {
            Hashtable GridParams = new Hashtable();

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
            //this.neighbours = (ArrayList)GridRespData["neighbours"];

            listener = new RegionCommsListener();

            return listener;
        }

        public List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return null;
        }
        public RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            return null;
        }
        public List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            return null;
        }
    }
}
