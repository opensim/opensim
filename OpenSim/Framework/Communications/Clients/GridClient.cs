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
using System.Reflection;

using log4net;
using OpenMetaverse;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Communications.Clients
{
    public class GridClient
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public bool RegisterRegion(
            string gridServerURL, string sendKey, string receiveKey, RegionInfo regionInfo, out bool forcefulBanLines)
        {
            m_log.InfoFormat(
                "[GRID CLIENT]: Registering region {0} with grid at {1}", regionInfo.RegionName, gridServerURL);
            
            forcefulBanLines = true;

            Hashtable GridParams = new Hashtable();
            // Login / Authentication

            GridParams["authkey"] = sendKey;
            GridParams["recvkey"] = receiveKey;
            GridParams["UUID"] = regionInfo.RegionID.ToString();
            GridParams["sim_ip"] = regionInfo.ExternalHostName;
            GridParams["sim_port"] = regionInfo.InternalEndPoint.Port.ToString();
            GridParams["region_locx"] = regionInfo.RegionLocX.ToString();
            GridParams["region_locy"] = regionInfo.RegionLocY.ToString();
            GridParams["sim_name"] = regionInfo.RegionName;
            GridParams["http_port"] = regionInfo.HttpPort.ToString();
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
                GridResp = GridReq.Send(gridServerURL, 90000);
            }
            catch (Exception e)
            {
                Exception e2
                    = new Exception(
                        String.Format(
                            "Unable to register region with grid at {0}. Grid service not running?",
                            gridServerURL),
                        e);

                throw e2;
            }

            Hashtable GridRespData = (Hashtable)GridResp.Value;
            // Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData.ContainsKey("error"))
            {
                string errorstring = (string)GridRespData["error"];

                Exception e = new Exception(
                    String.Format("Unable to connect to grid at {0}: {1}", gridServerURL, errorstring));

                throw e;
            }
            else
            {
                // m_knownRegions = RequestNeighbours(regionInfo.RegionLocX, regionInfo.RegionLocY);
                if (GridRespData.ContainsKey("allow_forceful_banlines"))
                {
                    if ((string)GridRespData["allow_forceful_banlines"] != "TRUE")
                    {
                        forcefulBanLines = false;
                    }
                }

            }
            return true;
        }

        public bool DeregisterRegion(string gridServerURL, string sendKey, string receiveKey, RegionInfo regionInfo, out string errorMsg)
        {
            errorMsg = "";
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
                GridResp = GridReq.Send(gridServerURL, 10000);
            }
            catch (Exception e)
            {
                Exception e2
                    = new Exception(
                        String.Format(
                            "Unable to deregister region with grid at {0}. Grid service not running?",
                            gridServerURL),
                        e);

                throw e2;
            }

            Hashtable GridRespData = (Hashtable)GridResp.Value;

            // Hashtable griddatahash = GridRespData;

            // Process Response
            if (GridRespData != null && GridRespData.ContainsKey("error"))
            {
                errorMsg = (string)GridRespData["error"];
                return false;
            }

            return true;
        }

        public bool RequestNeighborInfo(
            string gridServerURL, string sendKey, string receiveKey, UUID regionUUID,
            out RegionInfo regionInfo, out string errorMsg)
        {
            // didn't find it so far, we have to go the long way
            regionInfo = null;
            errorMsg = string.Empty;
            Hashtable requestData = new Hashtable();
            requestData["region_UUID"] = regionUUID.ToString();
            requestData["authkey"] = sendKey;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(requestData);
            XmlRpcRequest gridReq = new XmlRpcRequest("simulator_data_request", SendParams);
            XmlRpcResponse gridResp = null;

            try
            {
                gridResp = gridReq.Send(gridServerURL, 3000);
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                return false;
            }

            Hashtable responseData = (Hashtable)gridResp.Value;

            if (responseData.ContainsKey("error"))
            {
                errorMsg = (string)responseData["error"];
                return false; ;
            }

            regionInfo = BuildRegionInfo(responseData, String.Empty);

            return true; 
        }

        public bool RequestNeighborInfo(
            string gridServerURL, string sendKey, string receiveKey, ulong regionHandle, 
            out RegionInfo regionInfo, out string errorMsg)
        {
            // didn't find it so far, we have to go the long way
            regionInfo = null;
            errorMsg = string.Empty;

            try
            {
                Hashtable requestData = new Hashtable();
                requestData["region_handle"] = regionHandle.ToString();
                requestData["authkey"] = sendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);
                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(gridServerURL, 3000);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    errorMsg = (string)responseData["error"];
                    return false;
                }

                uint regX = Convert.ToUInt32((string)responseData["region_locx"]);
                uint regY = Convert.ToUInt32((string)responseData["region_locy"]);
                string externalHostName = (string)responseData["sim_ip"];
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
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                return false;
            }

            return true;
        }

        public bool RequestClosestRegion(
             string gridServerURL, string sendKey, string receiveKey, string regionName, 
             out RegionInfo regionInfo, out string errorMsg)
        {
            regionInfo = null;
            errorMsg = string.Empty;
            try
            {
                Hashtable requestData = new Hashtable();
                requestData["region_name_search"] = regionName;
                requestData["authkey"] = sendKey;
                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);
                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(gridServerURL, 3000);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    errorMsg = (string)responseData["error"];
                    return false;
                }

                regionInfo = BuildRegionInfo(responseData, "");

            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                return false;
            }
            return true;
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
        public bool MapBlockQuery(
            string gridServerURL, int minX, int minY, int maxX, int maxY, out Hashtable respData, out string errorMsg)
        {
            respData = new Hashtable();
            errorMsg = string.Empty;

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
                XmlRpcResponse resp = req.Send(gridServerURL, 10000);
                respData = (Hashtable)resp.Value;
                return true;
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                return false;
            }
        }

        public bool SearchRegionByName(string gridServerURL, IList parameters, out Hashtable respData, out string errorMsg)
        {
            respData = null;
            errorMsg = string.Empty;
            try
            {
                XmlRpcRequest request = new XmlRpcRequest("search_for_region_by_name", parameters);
                XmlRpcResponse resp = request.Send(gridServerURL, 10000);
                respData = (Hashtable)resp.Value;
                if (respData != null && respData.Contains("faultCode"))
                {
                    errorMsg = (string)respData["faultString"];
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                errorMsg = e.Message;
                return false;
            }
        }

        public RegionInfo BuildRegionInfo(Hashtable responseData, string prefix)
        {
            uint regX = Convert.ToUInt32((string)responseData[prefix + "region_locx"]);
            uint regY = Convert.ToUInt32((string)responseData[prefix + "region_locy"]);
            string internalIpStr = (string)responseData[prefix + "sim_ip"];
            uint port = Convert.ToUInt32(responseData[prefix + "sim_port"]);

            IPEndPoint neighbourInternalEndPoint = new IPEndPoint(Util.GetHostFromDNS(internalIpStr), (int)port);

            RegionInfo regionInfo = new RegionInfo(regX, regY, neighbourInternalEndPoint, internalIpStr);
            regionInfo.RemotingPort = Convert.ToUInt32((string)responseData[prefix + "remoting_port"]);
            regionInfo.RemotingAddress = internalIpStr;

            if (responseData.ContainsKey(prefix + "http_port"))
            {
                regionInfo.HttpPort = Convert.ToUInt32((string)responseData[prefix + "http_port"]);
            }

            regionInfo.RegionID = new UUID((string)responseData[prefix + "region_UUID"]);
            regionInfo.RegionName = (string)responseData[prefix + "region_name"];

            regionInfo.RegionSettings.TerrainImageID = new UUID((string)responseData[prefix + "map_UUID"]);
            return regionInfo;
        }
    }
}
