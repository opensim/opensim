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
using System.Text;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public class RegionProfileServiceProxy : IRegionProfileRouter
    {
        /// <summary>
        /// Request sim data based on arbitrary key/value
        /// </summary>
        private RegionProfileData RequestSimData(Uri gridserverUrl, string gridserverSendkey, string keyField, string keyValue)
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
                uint locX = Convert.ToUInt32((string)responseData["region_locx"]);
                uint locY = Convert.ToUInt32((string)responseData["region_locy"]);
                string externalHostName = (string)responseData["sim_ip"];
                uint simPort = Convert.ToUInt32((string)responseData["sim_port"]);
                uint httpPort = Convert.ToUInt32((string)responseData["http_port"]);
                uint remotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                string serverUri = (string)responseData["server_uri"];
                UUID regionID = new UUID((string)responseData["region_UUID"]);
                string regionName = (string)responseData["region_name"];
                byte access = Convert.ToByte((string)responseData["access"]);

                simData = RegionProfileData.Create(regionID, regionName, locX, locY, externalHostName, simPort, httpPort, remotingPort, serverUri, access);
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
        public RegionProfileData RequestSimProfileData(UUID regionId, Uri gridserverUrl,
                                                              string gridserverSendkey, string gridserverRecvkey)
        {
            return RequestSimData(gridserverUrl, gridserverSendkey, "region_UUID", regionId.Guid.ToString());
        }

        /// <summary>
        /// Request sim profile information from a grid server, by Region Handle
        /// </summary>
        /// <param name="regionHandle">the region handle to look for</param>
        /// <param name="gridserverUrl"></param>
        /// <param name="gridserverSendkey"></param>
        /// <param name="gridserverRecvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        public RegionProfileData RequestSimProfileData(ulong regionHandle, Uri gridserverUrl,
                                                              string gridserverSendkey, string gridserverRecvkey)
        {
            return RequestSimData(gridserverUrl, gridserverSendkey, "region_handle", regionHandle.ToString());
        }

        /// <summary>
        /// Request sim profile information from a grid server, by Region Name
        /// </summary>
        /// <param name="regionName">the region name to look for</param>
        /// <param name="gridserverUrl"></param>
        /// <param name="gridserverSendkey"></param>
        /// <param name="gridserverRecvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        public RegionProfileData RequestSimProfileData(string regionName, Uri gridserverUrl,
                                                              string gridserverSendkey, string gridserverRecvkey)
        {
            return RequestSimData(gridserverUrl, gridserverSendkey, "region_name_search", regionName);
        }
    }
}
