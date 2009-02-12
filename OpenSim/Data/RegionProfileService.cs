using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public class RegionProfileService
    {
        /// <summary>
        /// Request sim data based on arbitrary key/value
        /// </summary>
        private static RegionProfileData RequestSimData(Uri gridserverUrl, string gridserverSendkey, string keyField, string keyValue)
        {
            Hashtable requestData = new Hashtable();
            requestData[keyField] = keyValue;
            requestData["authkey"] = gridserverSendkey;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(gridserverUrl.ToString(), 3000);

            Hashtable responseData = (Hashtable) GridResp.Value;

            RegionProfileData simData = null;

            if (!responseData.ContainsKey("error"))
            {
                simData = new RegionProfileData();
                simData.regionLocX = Convert.ToUInt32((string) responseData["region_locx"]);
                simData.regionLocY = Convert.ToUInt32((string) responseData["region_locy"]);
                simData.regionHandle =
                    Utils.UIntsToLong((simData.regionLocX * Constants.RegionSize),
                                      (simData.regionLocY*Constants.RegionSize));
                simData.serverIP = (string) responseData["sim_ip"];
                simData.serverPort = Convert.ToUInt32((string) responseData["sim_port"]);
                simData.httpPort = Convert.ToUInt32((string) responseData["http_port"]);
                simData.remotingPort = Convert.ToUInt32((string) responseData["remoting_port"]);
                simData.serverURI = (string) responseData["server_uri"];
                simData.httpServerURI = "http://" + (string)responseData["sim_ip"] + ":" + simData.httpPort.ToString() + "/";
                simData.UUID = new UUID((string) responseData["region_UUID"]);
                simData.regionName = (string) responseData["region_name"];
            }

            return simData;
        }

        /// <summary>
        /// Request sim profile information from a grid server, by Region UUID
        /// </summary>
        /// <param name="regionId">The region UUID to look for</param>
        /// <param name="gridserverUrl"></param>
        /// <param name="gridserverSendkey"></param>
        /// <param name="gridserverRecvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        /// <remarks>This method should be statics</remarks>
        public static RegionProfileData RequestSimProfileData(UUID regionId, Uri gridserverUrl,
                                                              string gridserverSendkey, string gridserverRecvkey)
        {
            return RequestSimData(gridserverUrl, gridserverSendkey, "region_UUID", regionId.Guid.ToString());
        }

        /// <summary>
        /// Request sim profile information from a grid server, by Region Handle
        /// </summary>
        /// <param name="region_handle">the region handle to look for</param>
        /// <param name="gridserver_url"></param>
        /// <param name="gridserver_sendkey"></param>
        /// <param name="gridserver_recvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        public static RegionProfileData RequestSimProfileData(ulong region_handle, Uri gridserver_url,
                                                              string gridserver_sendkey, string gridserver_recvkey)
        {
            return RequestSimData(gridserver_url, gridserver_sendkey, "region_handle", region_handle.ToString());
        }

        /// <summary>
        /// Request sim profile information from a grid server, by Region Name
        /// </summary>
        /// <param name="regionName">the region name to look for</param>
        /// <param name="gridserverUrl"></param>
        /// <param name="gridserverSendkey"></param>
        /// <param name="gridserverRecvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        public static RegionProfileData RequestSimProfileData(string regionName, Uri gridserverUrl,
                                                              string gridserverSendkey, string gridserverRecvkey)
        {
            return RequestSimData(gridserverUrl, gridserverSendkey, "region_name_search", regionName );
        }
    }
}
