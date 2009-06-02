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
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;

namespace OpenSim.Grid.GridServer.Modules
{
    public class GridXmlRpcModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IRegionProfileService m_gridDBService;
        private IGridServiceCore m_gridCore;

        protected GridConfig m_config;

        protected IMessagingServerDiscovery m_messagingServerMapper;
        /// <value>
        /// Used to notify old regions as to which OpenSim version to upgrade to
        /// </value>
        private string m_opensimVersion;

        protected BaseHttpServer m_httpServer;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="opensimVersion">
        /// Used to notify old regions as to which OpenSim version to upgrade to
        /// </param>
        public GridXmlRpcModule()
        {
        }

        public void Initialise(string opensimVersion, IRegionProfileService gridDBService, IGridServiceCore gridCore, GridConfig config)
        {
            m_opensimVersion = opensimVersion;
            m_gridDBService = gridDBService;
            m_gridCore = gridCore;
            m_config = config;
            RegisterHandlers();
        }

        public void PostInitialise()
        {
            IMessagingServerDiscovery messagingModule;
            if (m_gridCore.TryGet<IMessagingServerDiscovery>(out messagingModule))
            {
                m_messagingServerMapper = messagingModule;
            }
        }

        public void RegisterHandlers()
        {
            //have these in separate method as some servers restart the http server and reregister all the handlers.
            m_httpServer = m_gridCore.GetHttpServer();

            m_httpServer.AddXmlRPCHandler("simulator_login", XmlRpcSimulatorLoginMethod);
            m_httpServer.AddXmlRPCHandler("simulator_data_request", XmlRpcSimulatorDataRequestMethod);
            m_httpServer.AddXmlRPCHandler("simulator_after_region_moved", XmlRpcDeleteRegionMethod);
            m_httpServer.AddXmlRPCHandler("map_block", XmlRpcMapBlockMethod);
            m_httpServer.AddXmlRPCHandler("search_for_region_by_name", XmlRpcSearchForRegionMethod);
        }

        /// <summary>
        /// Returns a XML String containing a list of the neighbouring regions
        /// </summary>
        /// <param name="reqhandle">The regionhandle for the center sim</param>
        /// <returns>An XML string containing neighbour entities</returns>
        public string GetXMLNeighbours(ulong reqhandle)
        {
            string response = String.Empty;
            RegionProfileData central_region = m_gridDBService.GetRegion(reqhandle);
            RegionProfileData neighbour;
            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    if (
                        m_gridDBService.GetRegion(
                            Util.UIntsToLong((uint)((central_region.regionLocX + x) * Constants.RegionSize),
                                             (uint)(central_region.regionLocY + y) * Constants.RegionSize)) != null)
                    {
                        neighbour =
                            m_gridDBService.GetRegion(
                                Util.UIntsToLong((uint)((central_region.regionLocX + x) * Constants.RegionSize),
                                                 (uint)(central_region.regionLocY + y) * Constants.RegionSize));

                        response += "<neighbour>";
                        response += "<sim_ip>" + neighbour.serverIP + "</sim_ip>";
                        response += "<sim_port>" + neighbour.serverPort.ToString() + "</sim_port>";
                        response += "<locx>" + neighbour.regionLocX.ToString() + "</locx>";
                        response += "<locy>" + neighbour.regionLocY.ToString() + "</locy>";
                        response += "<regionhandle>" + neighbour.regionHandle.ToString() + "</regionhandle>";
                        response += "</neighbour>";
                    }
                }
            }
            return response;
        }

        /// <summary>
        /// Checks that it's valid to replace the existing region data with new data
        ///
        /// Currently, this means ensure that the keys passed in by the new region
        /// match those in the original region.  (XXX Is this correct?  Shouldn't we simply check
        /// against the keys in the current configuration?)
        /// </summary>
        /// <param name="sim"></param>
        /// <returns></returns>
        protected virtual void ValidateOverwriteKeys(RegionProfileData sim, RegionProfileData existingSim)
        {
            if (!(existingSim.regionRecvKey == sim.regionRecvKey && existingSim.regionSendKey == sim.regionSendKey))
            {
                throw new LoginException(
                    String.Format(
                        "Authentication failed when trying to login existing region {0} at location {1} {2} currently occupied by {3}"
                            + " with the region's send key {4} (expected {5}) and the region's receive key {6} (expected {7})",
                            sim.regionName, sim.regionLocX, sim.regionLocY, existingSim.regionName,
                            sim.regionSendKey, existingSim.regionSendKey, sim.regionRecvKey, existingSim.regionRecvKey),
                    "The keys required to login your region did not match the grid server keys.  Please check your grid send and receive keys.");
            }
        }

        /// <summary>
        /// Checks that the new region data is valid.
        ///
        /// Currently, this means checking that the keys passed in by the new region
        /// match those in the grid server's configuration.
        /// </summary>
        ///
        /// <param name="sim"></param>
        /// <exception cref="LoginException">Thrown if region login failed</exception>
        protected virtual void ValidateNewRegionKeys(RegionProfileData sim)
        {
            if (!(sim.regionRecvKey == m_config.SimSendKey && sim.regionSendKey == m_config.SimRecvKey))
            {
                throw new LoginException(
                    String.Format(
                        "Authentication failed when trying to login new region {0} at location {1} {2}"
                            + " with the region's send key {3} (expected {4}) and the region's receive key {5} (expected {6})",
                            sim.regionName, sim.regionLocX, sim.regionLocY,
                            sim.regionSendKey, m_config.SimRecvKey, sim.regionRecvKey, m_config.SimSendKey),
                    "The keys required to login your region did not match your existing region keys.  Please check your grid send and receive keys.");
            }
        }

        /// <summary>
        /// Check that a region's http uri is externally contactable.
        /// </summary>
        /// <param name="sim"></param>
        /// <exception cref="LoginException">Thrown if the region is not contactable</exception>
        protected virtual void ValidateRegionContactable(RegionProfileData sim)
        {
            string regionStatusUrl = String.Format("{0}{1}", sim.httpServerURI, "simstatus/");
            string regionStatusResponse;

            RestClient rc = new RestClient(regionStatusUrl);
            rc.RequestMethod = "GET";

            m_log.DebugFormat("[LOGIN]: Contacting {0} for status of region {1}", regionStatusUrl, sim.regionName);

            try
            {
                Stream rs = rc.Request();
                StreamReader sr = new StreamReader(rs);
                regionStatusResponse = sr.ReadToEnd();
                sr.Close();
            }
            catch (Exception e)
            {
                throw new LoginException(
                   String.Format("Region status request to {0} failed", regionStatusUrl),
                   String.Format(
                       "The grid service could not contact the http url {0} at your region.  Please make sure this url is reachable by the grid service",
                       regionStatusUrl),
                   e);
            }

            if (!regionStatusResponse.Equals("OK"))
            {
                throw new LoginException(
                    String.Format(
                        "Region {0} at {1} returned status response {2} rather than {3}",
                        sim.regionName, regionStatusUrl, regionStatusResponse, "OK"),
                    String.Format(
                        "When the grid service asked for the status of your region, it received the response {0} rather than {1}.  Please check your status",
                        regionStatusResponse, "OK"));
            }
        }

        /// <summary>
        /// Construct an XMLRPC error response
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public static XmlRpcResponse ErrorResponse(string error)
        {
            XmlRpcResponse errorResponse = new XmlRpcResponse();
            Hashtable errorResponseData = new Hashtable();
            errorResponse.Value = errorResponseData;
            errorResponseData["error"] = error;
            return errorResponse;
        }

        /// <summary>
        /// Performed when a region connects to the grid server initially.
        /// </summary>
        /// <param name="request">The XML RPC Request</param>
        /// <returns>Startup parameters</returns>
        public XmlRpcResponse XmlRpcSimulatorLoginMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            RegionProfileData sim;
            RegionProfileData existingSim;

            Hashtable requestData = (Hashtable)request.Params[0];
            UUID uuid;

            if (!requestData.ContainsKey("UUID") || !UUID.TryParse((string)requestData["UUID"], out uuid))
            {
                m_log.Debug("[LOGIN PRELUDE]: Region connected without a UUID, sending back error response.");
                return ErrorResponse("No UUID passed to grid server - unable to connect you");
            }

            try
            {
                sim = RegionFromRequest(requestData);
            }
            catch (FormatException e)
            {
                m_log.Debug("[LOGIN PRELUDE]: Invalid login parameters, sending back error response.");
                return ErrorResponse("Wrong format in login parameters. Please verify parameters." + e.ToString());
            }

            m_log.InfoFormat("[LOGIN BEGIN]: Received login request from simulator: {0}", sim.regionName);

            if (!m_config.AllowRegionRegistration)
            {
                m_log.DebugFormat(
                    "[LOGIN END]: Disabled region registration blocked login request from simulator: {0}",
                    sim.regionName);

                return ErrorResponse("This grid is currently not accepting region registrations.");
            }

            int majorInterfaceVersion = 0;
            if (requestData.ContainsKey("major_interface_version"))
                int.TryParse((string)requestData["major_interface_version"], out majorInterfaceVersion);

            if (majorInterfaceVersion != VersionInfo.MajorInterfaceVersion)
            {
                return ErrorResponse(
                    String.Format(
                        "Your region service implements OGS1 interface version {0}"
                        + " but this grid requires that the region implement OGS1 interface version {1} to connect."
                        + "  Try changing to OpenSimulator {2}",
                        majorInterfaceVersion, VersionInfo.MajorInterfaceVersion, m_opensimVersion));
            }

            existingSim = m_gridDBService.GetRegion(sim.regionHandle);

            if (existingSim == null || existingSim.UUID == sim.UUID || sim.UUID != sim.originUUID)
            {
                try
                {
                    if (existingSim == null)
                    {
                        ValidateNewRegionKeys(sim);
                    }
                    else
                    {
                        ValidateOverwriteKeys(sim, existingSim);
                    }

                    ValidateRegionContactable(sim);
                }
                catch (LoginException e)
                {
                    string logMsg = e.Message;
                    if (e.InnerException != null)
                        logMsg += ", " + e.InnerException.Message;

                    m_log.WarnFormat("[LOGIN END]: {0}", logMsg);

                    return e.XmlRpcErrorResponse;
                }

                DataResponse insertResponse = m_gridDBService.AddUpdateRegion(sim, existingSim);

                switch (insertResponse)
                {
                    case DataResponse.RESPONSE_OK:
                        m_log.Info("[LOGIN END]: " + (existingSim == null ? "New" : "Existing") + " sim login successful: " + sim.regionName);
                        break;
                    case DataResponse.RESPONSE_ERROR:
                        m_log.Warn("[LOGIN END]: Sim login failed (Error): " + sim.regionName);
                        break;
                    case DataResponse.RESPONSE_INVALIDCREDENTIALS:
                        m_log.Warn("[LOGIN END]: " +
                                              "Sim login failed (Invalid Credentials): " + sim.regionName);
                        break;
                    case DataResponse.RESPONSE_AUTHREQUIRED:
                        m_log.Warn("[LOGIN END]: " +
                                              "Sim login failed (Authentication Required): " +
                                              sim.regionName);
                        break;
                }

                XmlRpcResponse response = CreateLoginResponse(sim);

                return response;
            }
            else
            {
                m_log.Warn("[LOGIN END]: Failed to login region " + sim.regionName + " at location " + sim.regionLocX + " " + sim.regionLocY + " currently occupied by " + existingSim.regionName);
                return ErrorResponse("Another region already exists at that location.  Please try another.");
            }
        }

        /// <summary>
        /// Construct a successful response to a simulator's login attempt.
        /// </summary>
        /// <param name="sim"></param>
        /// <returns></returns>
        private XmlRpcResponse CreateLoginResponse(RegionProfileData sim)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            ArrayList SimNeighboursData = GetSimNeighboursData(sim);

            responseData["UUID"] = sim.UUID.ToString();
            responseData["region_locx"] = sim.regionLocX.ToString();
            responseData["region_locy"] = sim.regionLocY.ToString();
            responseData["regionname"] = sim.regionName;
            responseData["estate_id"] = "1";
            responseData["neighbours"] = SimNeighboursData;

            responseData["sim_ip"] = sim.serverIP;
            responseData["sim_port"] = sim.serverPort.ToString();
            responseData["asset_url"] = sim.regionAssetURI;
            responseData["asset_sendkey"] = sim.regionAssetSendKey;
            responseData["asset_recvkey"] = sim.regionAssetRecvKey;
            responseData["user_url"] = sim.regionUserURI;
            responseData["user_sendkey"] = sim.regionUserSendKey;
            responseData["user_recvkey"] = sim.regionUserRecvKey;
            responseData["authkey"] = sim.regionSecret;

            // New! If set, use as URL to local sim storage (ie http://remotehost/region.Yap)
            responseData["data_uri"] = sim.regionDataURI;

            responseData["allow_forceful_banlines"] = m_config.AllowForcefulBanlines;

            // Instead of sending a multitude of message servers to the registering sim
            // we should probably be sending a single one and parhaps it's backup
            // that has responsibility over routing it's messages.

            // The Sim won't be contacting us again about any of the message server stuff during it's time up.

            responseData["messageserver_count"] = 0;

           // IGridMessagingModule messagingModule;
           // if (m_gridCore.TryGet<IGridMessagingModule>(out messagingModule))
            //{
            if (m_messagingServerMapper != null)
            {
                List<MessageServerInfo> messageServers = m_messagingServerMapper.GetMessageServersList();
                responseData["messageserver_count"] = messageServers.Count;

                for (int i = 0; i < messageServers.Count; i++)
                {
                    responseData["messageserver_uri" + i] = messageServers[i].URI;
                    responseData["messageserver_sendkey" + i] = messageServers[i].sendkey;
                    responseData["messageserver_recvkey" + i] = messageServers[i].recvkey;
                }
            }
            return response;
        }

        private ArrayList GetSimNeighboursData(RegionProfileData sim)
        {
            ArrayList SimNeighboursData = new ArrayList();

            RegionProfileData neighbour;
            Hashtable NeighbourBlock;

            //First use the fast method. (not implemented in SQLLite)
            List<RegionProfileData> neighbours = m_gridDBService.GetRegions(sim.regionLocX - 1, sim.regionLocY - 1, sim.regionLocX + 1, sim.regionLocY + 1);

            if (neighbours.Count > 0)
            {
                foreach (RegionProfileData aSim in neighbours)
                {
                    NeighbourBlock = new Hashtable();
                    NeighbourBlock["sim_ip"] = aSim.serverIP;
                    NeighbourBlock["sim_port"] = aSim.serverPort.ToString();
                    NeighbourBlock["region_locx"] = aSim.regionLocX.ToString();
                    NeighbourBlock["region_locy"] = aSim.regionLocY.ToString();
                    NeighbourBlock["UUID"] = aSim.ToString();
                    NeighbourBlock["regionHandle"] = aSim.regionHandle.ToString();

                    if (aSim.UUID != sim.UUID)
                    {
                        SimNeighboursData.Add(NeighbourBlock);
                    }
                }
            }
            else
            {
                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        if (
                            m_gridDBService.GetRegion(
                                Utils.UIntsToLong((uint)((sim.regionLocX + x) * Constants.RegionSize),
                                                    (uint)(sim.regionLocY + y) * Constants.RegionSize)) != null)
                        {
                            neighbour =
                                m_gridDBService.GetRegion(
                                    Utils.UIntsToLong((uint)((sim.regionLocX + x) * Constants.RegionSize),
                                                        (uint)(sim.regionLocY + y) * Constants.RegionSize));

                            NeighbourBlock = new Hashtable();
                            NeighbourBlock["sim_ip"] = neighbour.serverIP;
                            NeighbourBlock["sim_port"] = neighbour.serverPort.ToString();
                            NeighbourBlock["region_locx"] = neighbour.regionLocX.ToString();
                            NeighbourBlock["region_locy"] = neighbour.regionLocY.ToString();
                            NeighbourBlock["UUID"] = neighbour.UUID.ToString();
                            NeighbourBlock["regionHandle"] = neighbour.regionHandle.ToString();

                            if (neighbour.UUID != sim.UUID) SimNeighboursData.Add(NeighbourBlock);
                        }
                    }
                }
            }
            return SimNeighboursData;
        }

        /// <summary>
        /// Loads the grid's own RegionProfileData object with data from the XMLRPC simulator_login request from a region
        /// </summary>
        /// <param name="requestData"></param>
        /// <returns></returns>
        private RegionProfileData RegionFromRequest(Hashtable requestData)
        {
            RegionProfileData sim;
            sim = new RegionProfileData();

            sim.UUID = new UUID((string)requestData["UUID"]);
            sim.originUUID = new UUID((string)requestData["originUUID"]);

            sim.regionRecvKey = String.Empty;
            sim.regionSendKey = String.Empty;

            if (requestData.ContainsKey("region_secret"))
            {
                string regionsecret = (string)requestData["region_secret"];
                if (regionsecret.Length > 0)
                    sim.regionSecret = regionsecret;
                else
                    sim.regionSecret = m_config.SimRecvKey;

            }
            else
            {
                sim.regionSecret = m_config.SimRecvKey;
            }

            sim.regionDataURI = String.Empty;
            sim.regionAssetURI = m_config.DefaultAssetServer;
            sim.regionAssetRecvKey = m_config.AssetRecvKey;
            sim.regionAssetSendKey = m_config.AssetSendKey;
            sim.regionUserURI = m_config.DefaultUserServer;
            sim.regionUserSendKey = m_config.UserSendKey;
            sim.regionUserRecvKey = m_config.UserRecvKey;

            sim.serverIP = (string)requestData["sim_ip"];
            sim.serverPort = Convert.ToUInt32((string)requestData["sim_port"]);
            sim.httpPort = Convert.ToUInt32((string)requestData["http_port"]);
            sim.remotingPort = Convert.ToUInt32((string)requestData["remoting_port"]);
            sim.regionLocX = Convert.ToUInt32((string)requestData["region_locx"]);
            sim.regionLocY = Convert.ToUInt32((string)requestData["region_locy"]);
            sim.regionLocZ = 0;

            UUID textureID;
            if (UUID.TryParse((string)requestData["map-image-id"], out textureID))
            {
                sim.regionMapTextureID = textureID;
            }

            // part of an initial brutish effort to provide accurate information (as per the xml region spec)
            // wrt the ownership of a given region
            // the (very bad) assumption is that this value is being read and handled inconsistently or
            // not at all. Current strategy is to put the code in place to support the validity of this information
            // and to roll forward debugging any issues from that point
            //
            // this particular section of the mod attempts to receive a value from the region's xml file by way of
            // OSG1GridServices for the region's owner
            sim.owner_uuid = (UUID)(string)requestData["master_avatar_uuid"];

            try
            {
                sim.regionRecvKey = (string)requestData["recvkey"];
                sim.regionSendKey = (string)requestData["authkey"];
            }
            catch (KeyNotFoundException) { }

            sim.regionHandle = Utils.UIntsToLong((sim.regionLocX * Constants.RegionSize), (sim.regionLocY * Constants.RegionSize));
            sim.serverURI = (string)requestData["server_uri"];

            sim.httpServerURI = "http://" + sim.serverIP + ":" + sim.httpPort + "/";

            sim.regionName = (string)requestData["sim_name"];


            try
            {

                sim.maturity = Convert.ToUInt32((string)requestData["maturity"]);
            }
            catch (KeyNotFoundException)
            {
                //older region not providing this key - so default to Mature
                sim.maturity = 1; 
            }

            return sim;
        }

        /// <summary>
        /// Returns an XML RPC response to a simulator profile request
        /// Performed after moving a region.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        /// <param name="request">The XMLRPC Request</param>
        /// <returns>Processing parameters</returns>
        public XmlRpcResponse XmlRpcDeleteRegionMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            //RegionProfileData TheSim = null;
            string uuid;
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.ContainsKey("UUID"))
            {
                //TheSim = GetRegion(new UUID((string) requestData["UUID"]));
                uuid = requestData["UUID"].ToString();
                m_log.InfoFormat("[LOGOUT]: Logging out region: {0}", uuid);
                //                logToDB((new LLUUID((string)requestData["UUID"])).ToString(),"XmlRpcDeleteRegionMethod","", 5,"Attempting delete with UUID.");
            }
            else
            {
                responseData["error"] = "No UUID or region_handle passed to grid server - unable to delete";
                return response;
            }

            DataResponse insertResponse = m_gridDBService.DeleteRegion(uuid);

            string insertResp = "";
            switch (insertResponse)
            {
                case DataResponse.RESPONSE_OK:
                    //MainLog.Instance.Verbose("grid", "Deleting region successful: " + uuid);
                    insertResp = "Deleting region successful: " + uuid;
                    break;
                case DataResponse.RESPONSE_ERROR:
                    //MainLog.Instance.Warn("storage", "Deleting region failed (Error): " + uuid);
                    insertResp = "Deleting region failed (Error): " + uuid;
                    break;
                case DataResponse.RESPONSE_INVALIDCREDENTIALS:
                    //MainLog.Instance.Warn("storage", "Deleting region failed (Invalid Credentials): " + uuid);
                    insertResp = "Deleting region (Invalid Credentials): " + uuid;
                    break;
                case DataResponse.RESPONSE_AUTHREQUIRED:
                    //MainLog.Instance.Warn("storage", "Deleting region failed (Authentication Required): " + uuid);
                    insertResp = "Deleting region (Authentication Required): " + uuid;
                    break;
            }

            responseData["status"] = insertResp;

            return response;
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
            RegionProfileData simData = null;
            if (requestData.ContainsKey("region_UUID"))
            {
                UUID regionID = new UUID((string)requestData["region_UUID"]);
                simData = m_gridDBService.GetRegion(regionID);
                if (simData == null)
                {
                    m_log.WarnFormat("[DATA] didn't find region for regionID {0} from {1}",
                                     regionID, request.Params.Count > 1 ? request.Params[1] : "unknwon source");
                }
            }
            else if (requestData.ContainsKey("region_handle"))
            {
                //CFK: The if/else below this makes this message redundant.
                //CFK: m_log.Info("requesting data for region " + (string) requestData["region_handle"]);
                ulong regionHandle = Convert.ToUInt64((string)requestData["region_handle"]);
                simData = m_gridDBService.GetRegion(regionHandle);
                if (simData == null)
                {
                    m_log.WarnFormat("[DATA] didn't find region for regionHandle {0} from {1}",
                                     regionHandle, request.Params.Count > 1 ? request.Params[1] : "unknwon source");
                }
            }
            else if (requestData.ContainsKey("region_name_search"))
            {
                string regionName = (string)requestData["region_name_search"];
                simData = m_gridDBService.GetRegion(regionName);
                if (simData == null)
                {
                    m_log.WarnFormat("[DATA] didn't find region for regionName {0} from {1}",
                                     regionName, request.Params.Count > 1 ? request.Params[1] : "unknwon source");
                }
            }
            else m_log.Warn("[DATA] regionlookup without regionID, regionHandle or regionHame");

            if (simData == null)
            {
                //Sim does not exist
                responseData["error"] = "Sim does not exist";
            }
            else
            {
                m_log.Info("[DATA]: found " + (string)simData.regionName + " regionHandle = " +
                           (string)requestData["region_handle"]);
                responseData["sim_ip"] = simData.serverIP;
                responseData["sim_port"] = simData.serverPort.ToString();
                responseData["server_uri"] = simData.serverURI;
                responseData["http_port"] = simData.httpPort.ToString();
                responseData["remoting_port"] = simData.remotingPort.ToString();
                responseData["region_locx"] = simData.regionLocX.ToString();
                responseData["region_locy"] = simData.regionLocY.ToString();
                responseData["region_UUID"] = simData.UUID.Guid.ToString();
                responseData["region_name"] = simData.regionName;
                responseData["regionHandle"] = simData.regionHandle.ToString();
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcMapBlockMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            int xmin = 980, ymin = 980, xmax = 1020, ymax = 1020;

            Hashtable requestData = (Hashtable)request.Params[0];
            if (requestData.ContainsKey("xmin"))
            {
                xmin = (Int32)requestData["xmin"];
            }
            if (requestData.ContainsKey("ymin"))
            {
                ymin = (Int32)requestData["ymin"];
            }
            if (requestData.ContainsKey("xmax"))
            {
                xmax = (Int32)requestData["xmax"];
            }
            if (requestData.ContainsKey("ymax"))
            {
                ymax = (Int32)requestData["ymax"];
            }
            //CFK: The second log is more meaningful and either standard or fast generally occurs.
            //CFK: m_log.Info("[MAP]: World map request for range (" + xmin + "," + ymin + ")..(" + xmax + "," + ymax + ")");

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;
            IList simProfileList = new ArrayList();

            bool fastMode = (m_config.DatabaseProvider == "OpenSim.Data.MySQL.dll" || m_config.DatabaseProvider == "OpenSim.Data.MSSQL.dll");

            if (fastMode)
            {
                List<RegionProfileData> neighbours = m_gridDBService.GetRegions((uint)xmin, (uint)ymin, (uint)xmax, (uint)ymax);

                foreach (RegionProfileData aSim in neighbours)
                {
                    Hashtable simProfileBlock = new Hashtable();
                    simProfileBlock["x"] = aSim.regionLocX.ToString();
                    simProfileBlock["y"] = aSim.regionLocY.ToString();
                    //m_log.DebugFormat("[MAP]: Sending neighbour info for {0},{1}", aSim.regionLocX, aSim.regionLocY);
                    simProfileBlock["name"] = aSim.regionName;
                    simProfileBlock["access"] = aSim.AccessLevel.ToString();
                    simProfileBlock["region-flags"] = 512;
                    simProfileBlock["water-height"] = 0;
                    simProfileBlock["agents"] = 1;
                    simProfileBlock["map-image-id"] = aSim.regionMapTextureID.ToString();

                    // For Sugilite compatibility
                    simProfileBlock["regionhandle"] = aSim.regionHandle.ToString();
                    simProfileBlock["sim_ip"] = aSim.serverIP;
                    simProfileBlock["sim_port"] = aSim.serverPort.ToString();
                    simProfileBlock["sim_uri"] = aSim.serverURI.ToString();
                    simProfileBlock["uuid"] = aSim.UUID.ToString();
                    simProfileBlock["remoting_port"] = aSim.remotingPort.ToString();
                    simProfileBlock["http_port"] = aSim.httpPort.ToString();

                    simProfileList.Add(simProfileBlock);
                }
                m_log.Info("[MAP]: Fast map " + simProfileList.Count.ToString() +
                           " regions @ (" + xmin + "," + ymin + ")..(" + xmax + "," + ymax + ")");
            }
            else
            {
                RegionProfileData simProfile;
                for (int x = xmin; x < xmax + 1; x++)
                {
                    for (int y = ymin; y < ymax + 1; y++)
                    {
                        ulong regHandle = Utils.UIntsToLong((uint)(x * Constants.RegionSize), (uint)(y * Constants.RegionSize));
                        simProfile = m_gridDBService.GetRegion(regHandle);
                        if (simProfile != null)
                        {
                            Hashtable simProfileBlock = new Hashtable();
                            simProfileBlock["x"] = x;
                            simProfileBlock["y"] = y;
                            simProfileBlock["name"] = simProfile.regionName;
                            simProfileBlock["access"] = simProfile.AccessLevel.ToString();
                            simProfileBlock["region-flags"] = 0;
                            simProfileBlock["water-height"] = 20;
                            simProfileBlock["agents"] = 1;
                            simProfileBlock["map-image-id"] = simProfile.regionMapTextureID.ToString();

                            // For Sugilite compatibility
                            simProfileBlock["regionhandle"] = simProfile.regionHandle.ToString();
                            simProfileBlock["sim_ip"] = simProfile.serverIP.ToString();
                            simProfileBlock["sim_port"] = simProfile.serverPort.ToString();
                            simProfileBlock["sim_uri"] = simProfile.serverURI.ToString();
                            simProfileBlock["uuid"] = simProfile.UUID.ToString();
                            simProfileBlock["remoting_port"] = simProfile.remotingPort.ToString();
                            simProfileBlock["http_port"] = simProfile.httpPort;

                            simProfileList.Add(simProfileBlock);
                        }
                    }
                }
                m_log.Info("[MAP]: Std map " + simProfileList.Count.ToString() +
                           " regions @ (" + xmin + "," + ymin + ")..(" + xmax + "," + ymax + ")");
            }

            responseData["sim-profiles"] = simProfileList;

            return response;
        }

        /// <summary>
        /// Returns up to <code>maxNumber</code> profiles of regions that have a name starting with <code>name</code>
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse XmlRpcSearchForRegionMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            if (!requestData.ContainsKey("name") || !requestData.Contains("maxNumber"))
            {
                m_log.Warn("[DATA] Invalid region-search request; missing name or maxNumber");
                return new XmlRpcResponse(500, "Missing name or maxNumber in region search request");
            }

            Hashtable responseData = new Hashtable();

            string name = (string)requestData["name"];
            int maxNumber = Convert.ToInt32((string)requestData["maxNumber"]);
            if (maxNumber == 0 || name.Length < 3)
            {
                // either we didn't want any, or we were too unspecific
                responseData["numFound"] = 0;
            }
            else
            {
                List<RegionProfileData> sims = m_gridDBService.GetRegions(name, maxNumber);

                responseData["numFound"] = sims.Count;
                for (int i = 0; i < sims.Count; ++i)
                {
                    RegionProfileData sim = sims[i];
                    string prefix = "region" + i + ".";
                    responseData[prefix + "region_name"] = sim.regionName;
                    responseData[prefix + "region_UUID"] = sim.UUID.ToString();
                    responseData[prefix + "region_locx"] = sim.regionLocX.ToString();
                    responseData[prefix + "region_locy"] = sim.regionLocY.ToString();
                    responseData[prefix + "sim_ip"] = sim.serverIP.ToString();
                    responseData[prefix + "sim_port"] = sim.serverPort.ToString();
                    responseData[prefix + "remoting_port"] = sim.remotingPort.ToString();
                    responseData[prefix + "http_port"] = sim.httpPort.ToString();
                    responseData[prefix + "map_UUID"] = sim.regionMapTextureID.ToString();
                }
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;
            return response;
        }

        /// <summary>
        /// Construct an XMLRPC registration disabled response
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public static XmlRpcResponse XmlRPCRegionRegistrationDisabledResponse(string error)
        {
            XmlRpcResponse errorResponse = new XmlRpcResponse();
            Hashtable errorResponseData = new Hashtable();
            errorResponse.Value = errorResponseData;
            errorResponseData["restricted"] = error;
            return errorResponse;
        }
    }

    /// <summary>
    /// Exception generated when a simulator fails to login to the grid
    /// </summary>
    public class LoginException : Exception
    {
        /// <summary>
        /// Return an XmlRpcResponse version of the exception message suitable for sending to a client
        /// </summary>
        /// <param name="message"></param>
        /// <param name="xmlRpcMessage"></param>
        public XmlRpcResponse XmlRpcErrorResponse
        {
            get { return m_xmlRpcErrorResponse; }
        }
        private XmlRpcResponse m_xmlRpcErrorResponse;

        public LoginException(string message, string xmlRpcMessage)
            : base(message)
        {
            // FIXME: Might be neater to refactor and put the method inside here
            m_xmlRpcErrorResponse = GridXmlRpcModule.ErrorResponse(xmlRpcMessage);
        }

        public LoginException(string message, string xmlRpcMessage, Exception e)
            : base(message, e)
        {
            // FIXME: Might be neater to refactor and put the method inside here
            m_xmlRpcErrorResponse = GridXmlRpcModule.ErrorResponse(xmlRpcMessage);
        }
    }
}
