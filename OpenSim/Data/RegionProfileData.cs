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
using OpenMetaverse;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Data
{
    /// <summary>
    /// A class which contains information known to the grid server about a region
    /// </summary>
    public class RegionProfileData
    {
        /// <summary>
        /// The name of the region
        /// </summary>
        public string regionName = String.Empty;

        /// <summary>
        /// A 64-bit number combining map position into a (mostly) unique ID
        /// </summary>
        public ulong regionHandle;

        /// <summary>
        /// OGS/OpenSim Specific ID for a region
        /// </summary>
        public UUID UUID;

        /// <summary>
        /// Coordinates of the region
        /// </summary>
        public uint regionLocX;
        public uint regionLocY;
        public uint regionLocZ; // Reserved (round-robin, layers, etc)

        /// <summary>
        /// Authentication secrets
        /// </summary>
        /// <remarks>Not very secure, needs improvement.</remarks>
        public string regionSendKey = String.Empty;
        public string regionRecvKey = String.Empty;
        public string regionSecret = String.Empty;

        /// <summary>
        /// Whether the region is online
        /// </summary>
        public bool regionOnline;

        /// <summary>
        /// Information about the server that the region is currently hosted on
        /// </summary>
        public string serverIP = String.Empty;
        public uint serverPort;
        public string serverURI = String.Empty;

        public uint httpPort;
        public uint remotingPort;
        public string httpServerURI = String.Empty;

        /// <summary>
        /// Set of optional overrides. Can be used to create non-eulicidean spaces.
        /// </summary>
        public ulong regionNorthOverrideHandle;
        public ulong regionSouthOverrideHandle;
        public ulong regionEastOverrideHandle;
        public ulong regionWestOverrideHandle;

        /// <summary>
        /// Optional: URI Location of the region database
        /// </summary>
        /// <remarks>Used for floating sim pools where the region data is not nessecarily coupled to a specific server</remarks>
        public string regionDataURI = String.Empty;

        /// <summary>
        /// Region Asset Details
        /// </summary>
        public string regionAssetURI = String.Empty;

        public string regionAssetSendKey = String.Empty;
        public string regionAssetRecvKey = String.Empty;

        /// <summary>
        /// Region Userserver Details
        /// </summary>
        public string regionUserURI = String.Empty;

        public string regionUserSendKey = String.Empty;
        public string regionUserRecvKey = String.Empty;

        /// <summary>
        /// Region Map Texture Asset
        /// </summary>
        public UUID regionMapTextureID = new UUID("00000000-0000-1111-9999-000000000006");

        /// <summary>
        /// this particular mod to the file provides support within the spec for RegionProfileData for the
        /// owner_uuid for the region
        /// </summary>
        public UUID owner_uuid = UUID.Zero;

        /// <summary>
        /// OGS/OpenSim Specific original ID for a region after move/split
        /// </summary>
        public UUID originUUID;

        /// <summary>
        /// Request sim data based on arbitrary key/value
        /// </summary>
        private static RegionProfileData RequestSimData(Uri gridserver_url, string gridserver_sendkey, string keyField, string keyValue)
        {
            Hashtable requestData = new Hashtable();
            requestData[keyField] = keyValue;
            requestData["authkey"] = gridserver_sendkey;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);
            XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(gridserver_url.ToString(), 3000);

            Hashtable responseData = (Hashtable) GridResp.Value;

            RegionProfileData simData = null;

            if (!responseData.ContainsKey("error"))
            {
                simData = new RegionProfileData();
                simData.regionLocX = Convert.ToUInt32((string) responseData["region_locx"]);
                simData.regionLocY = Convert.ToUInt32((string) responseData["region_locy"]);
                simData.regionHandle =
                    Helpers.UIntsToLong((simData.regionLocX*Constants.RegionSize),
                                        (simData.regionLocY*Constants.RegionSize));
                simData.serverIP = (string) responseData["sim_ip"];
                simData.serverPort = Convert.ToUInt32((string) responseData["sim_port"]);
                simData.httpPort = Convert.ToUInt32((string) responseData["http_port"]);
                simData.remotingPort = Convert.ToUInt32((string) responseData["remoting_port"]);
                simData.serverURI = (string) responseData["server_uri"];
                simData.httpServerURI = "http://" + simData.serverIP + ":" + simData.httpPort.ToString() + "/";
                simData.UUID = new UUID((string) responseData["region_UUID"]);
                simData.regionName = (string) responseData["region_name"];
            }

            return simData;
        }

        /// <summary>
        /// Request sim profile information from a grid server, by Region UUID
        /// </summary>
        /// <param name="region_UUID">The region UUID to look for</param>
        /// <param name="gridserver_url"></param>
        /// <param name="gridserver_sendkey"></param>
        /// <param name="gridserver_recvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        /// <remarks>This method should be statics</remarks>
        public static RegionProfileData RequestSimProfileData(UUID region_uuid, Uri gridserver_url,
                                                       string gridserver_sendkey, string gridserver_recvkey)
        {
            return RequestSimData(gridserver_url, gridserver_sendkey, "region_UUID", region_uuid.Guid.ToString());
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
        /// <param name="region_handle">the region name to look for</param>
        /// <param name="gridserver_url"></param>
        /// <param name="gridserver_sendkey"></param>
        /// <param name="gridserver_recvkey"></param>
        /// <returns>The sim profile.  Null if there was a request failure</returns>
        public static RegionProfileData RequestSimProfileData(string regionName, Uri gridserver_url,
                                                              string gridserver_sendkey, string gridserver_recvkey)
        {
            return RequestSimData(gridserver_url, gridserver_sendkey, "region_name_search", regionName );
        }
    }
}
