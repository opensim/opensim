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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Nwc.XmlRpc;


namespace OpenSim.Region.CoreModules.Framework.Services
{
    public class RegionMapService : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool initialized = false;
        private static bool enabled = false;

        Scene m_scene;
        //AssetService m_assetService;

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;
                m_scene = scene;

                // This module is only on for hypergrid mode
                enabled = config.Configs["Startup"].GetBoolean("hypergrid", false);
            }
        }

        public void PostInitialise()
        {
            if (enabled)
            {
                m_log.Info("[RegionMapService]: Starting...");

                //m_assetService = new AssetService(m_scene);
                new GridService(m_scene);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "RegionMapService"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

    }

    public class GridService
    {
//        private IUserService m_userService;
        private IGridServices m_gridService;
        private bool m_doLookup = false;

        public bool DoLookup
        {
            get { return m_doLookup; }
            set { m_doLookup = value; }
        }
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public GridService(Scene m_scene)
        {
            AddHandlers(m_scene);
//            m_userService = m_scene.CommsManager.UserService;
            m_gridService = m_scene.CommsManager.GridService;
        }

        protected void AddHandlers(Scene m_scene)
        {
//            IAssetDataPlugin m_assetProvider 
//                = ((AssetServerBase)m_scene.CommsManager.AssetCache.AssetServer).AssetProviderPlugin;

            IHttpServer httpServer = m_scene.CommsManager.HttpServer;
            httpServer.AddXmlRPCHandler("simulator_data_request", XmlRpcSimulatorDataRequestMethod);
            //m_httpServer.AddXmlRPCHandler("map_block", XmlRpcMapBlockMethod);
            //m_httpServer.AddXmlRPCHandler("search_for_region_by_name", XmlRpcSearchForRegionMethod);

        }

        /// <summary>
        /// Returns an XML RPC response to a simulator profile request
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse XmlRpcSimulatorDataRequestMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            RegionInfo simData = null;
            if (requestData.ContainsKey("region_UUID"))
            {
                UUID regionID = new UUID((string)requestData["region_UUID"]);
                simData = m_gridService.RequestNeighbourInfo(regionID); //.GetRegion(regionID);
                if (simData == null)
                {
                    m_log.WarnFormat("[HGGridService] didn't find region for regionID {0} from {1}",
                                     regionID, request.Params.Count > 1 ? request.Params[1] : "unknwon source");
                }
            }
            else if (requestData.ContainsKey("region_handle"))
            {
                //CFK: The if/else below this makes this message redundant.
                //CFK: m_log.Info("requesting data for region " + (string) requestData["region_handle"]);
                ulong regionHandle = Convert.ToUInt64((string)requestData["region_handle"]);
                simData = m_gridService.RequestNeighbourInfo(regionHandle); //m_gridDBService.GetRegion(regionHandle);
                if (simData == null)
                {
                    m_log.WarnFormat("[HGGridService] didn't find region for regionHandle {0} from {1}",
                                     regionHandle, request.Params.Count > 1 ? request.Params[1] : "unknwon source");
                }
            }
            else if (requestData.ContainsKey("region_name_search"))
            {
                string regionName = (string)requestData["region_name_search"];
                List<RegionInfo> regInfos = m_gridService.RequestNamedRegions(regionName, 1);//m_gridDBService.GetRegion(regionName);
                if (regInfos != null)
                    simData = regInfos[0];

                if (simData == null)
                {
                    m_log.WarnFormat("[HGGridService] didn't find region for regionName {0} from {1}",
                                     regionName, request.Params.Count > 1 ? request.Params[1] : "unknwon source");
                }
            }
            else m_log.Warn("[HGGridService] regionlookup without regionID, regionHandle or regionHame");

            if (simData == null)
            {
                //Sim does not exist
                responseData["error"] = "Sim does not exist";
            }
            else
            {
                m_log.Debug("[HGGridService]: found " + (string)simData.RegionName + " regionHandle = " +
                           (string)requestData["region_handle"]);
                responseData["sim_ip"] = simData.ExternalEndPoint.Address.ToString();
                responseData["sim_port"] = simData.ExternalEndPoint.Port.ToString();
                //responseData["server_uri"] = simData.serverURI;
                responseData["http_port"] = simData.HttpPort.ToString();
                //responseData["remoting_port"] = simData.remotingPort.ToString();
                responseData["region_locx"] = simData.RegionLocX.ToString();
                responseData["region_locy"] = simData.RegionLocY.ToString();
                responseData["region_UUID"] = simData.RegionID.ToString();
                responseData["region_name"] = simData.RegionName;
                responseData["region_secret"] = simData.regionSecret;
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;
            return response;
        }

    }
}
