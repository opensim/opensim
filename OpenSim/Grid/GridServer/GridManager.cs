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
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using libsecondlife;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Data;
using OpenSim.Data.MySQL;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.GridServer
{
    public class GridManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<IGridDataPlugin> _plugins = new List<IGridDataPlugin>();
        private List<ILogDataPlugin> _logplugins = new List<ILogDataPlugin>();

        // This is here so that the grid server can hand out MessageServer settings to regions on registration
        private List<MessageServerInfo> _MessageServers = new List<MessageServerInfo>();

        public GridConfig Config;

        /// <summary>
        /// Adds a new grid server plugin - grid servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="provider">The name of the grid server plugin DLL</param>
        public void AddPlugin(string provider, string connect)
        {
            // FIXME: convert "provider" DLL file name to Mono.Addins "id", 
            // which unless it is changed in the source code, is the .NET namespace.
            // In the future, the "provider" should be changed to "id" in the 
            // config files, and is independent of filenames or namespaces.
            string[] s = provider.Split ('.');
            int len = s.Length;
            if ((len >= 2) && (s [len-1] == "dll"))
                s [len-1] = s [len-2];

            provider = String.Join (".", s); 

            PluginLoader<IGridDataPlugin> gridloader = 
                new PluginLoader<IGridDataPlugin> (new GridDataInitialiser (connect));

            PluginLoader<ILogDataPlugin> logloader = 
                new PluginLoader<ILogDataPlugin> (new LogDataInitialiser (connect));

            gridloader.AddExtensionPoint ("/OpenSim/GridData");
            logloader.AddExtensionPoint ("/OpenSim/LogData");
            
            // loader will try to load all providers (MySQL, MSSQL, etc) 
            // unless it is constrainted to the correct "id"
            //gridloader.AddFilter ("/OpenSim/GridData", new PluginIdFilter (provider + "GridData"));
            //logloader.AddFilter ("/OpenSim/LogData", new PluginIdFilter (provider + "LogData"));
            
            gridloader.Load();
            logloader.Load();
            
            _plugins = gridloader.Plugins;
            _logplugins = logloader.Plugins;
        }

        /// <summary>
        /// Logs a piece of information to the database
        /// </summary>
        /// <param name="target">What you were operating on (in grid server, this will likely be the region UUIDs)</param>
        /// <param name="method">Which method is being called?</param>
        /// <param name="args">What arguments are being passed?</param>
        /// <param name="priority">How high priority is this? 1 = Max, 6 = Verbose</param>
        /// <param name="message">The message to log</param>
        private void logToDB(string target, string method, string args, int priority, string message)
        {
            foreach (ILogDataPlugin plugin in _logplugins)
            {
                try
                {
                    plugin.saveLog("Gridserver", target, method, args, priority, message);
                }
                catch (Exception)
                {
                    m_log.Warn("[storage]: Unable to write log via " + plugin.Name);
                }
            }
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A UUID key of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public RegionProfileData GetRegion(LLUUID uuid)
        {
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetProfileByLLUUID(uuid);
                }
                catch (Exception e)
                {
                    m_log.Warn("[storage]: GetRegion - " + e.Message);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A regionHandle of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public RegionProfileData GetRegion(ulong handle)
        {
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetProfileByHandle(handle);
                }
                catch
                {
                    m_log.Warn("[storage]: Unable to find region " + handle.ToString() + " via " + plugin.Name);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="regionName">A partial regionName of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public RegionProfileData GetRegion(string regionName)
        {
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetProfileByString(regionName);
                }
                catch
                {
                    m_log.Warn("[storage]: Unable to find region " + regionName + " via " + plugin.Name);
                }
            }
            return null;
        }

        public Dictionary<ulong, RegionProfileData> GetRegions(uint xmin, uint ymin, uint xmax, uint ymax)
        {
            Dictionary<ulong, RegionProfileData> regions = new Dictionary<ulong, RegionProfileData>();

            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    RegionProfileData[] neighbours = plugin.GetProfilesInRange(xmin, ymin, xmax, ymax);
                    foreach (RegionProfileData neighbour in neighbours)
                    {
                        regions[neighbour.regionHandle] = neighbour;
                    }
                }
                catch
                {
                    m_log.Warn("[storage]: Unable to query regionblock via " + plugin.Name);
                }
            }

            return regions;
        }

        /// <summary>
        /// Returns a XML String containing a list of the neighbouring regions
        /// </summary>
        /// <param name="reqhandle">The regionhandle for the center sim</param>
        /// <returns>An XML string containing neighbour entities</returns>
        public string GetXMLNeighbours(ulong reqhandle)
        {
            string response = String.Empty;
            RegionProfileData central_region = GetRegion(reqhandle);
            RegionProfileData neighbour;
            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    if (
                        GetRegion(
                            Util.UIntsToLong((uint)((central_region.regionLocX + x) * Constants.RegionSize),
                                             (uint)(central_region.regionLocY + y) * Constants.RegionSize)) != null)
                    {
                        neighbour =
                            GetRegion(
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
            if (!(sim.regionRecvKey == Config.SimSendKey && sim.regionSendKey == Config.SimRecvKey))
            {
                throw new LoginException(
                    String.Format(
                        "Authentication failed when trying to login new region {0} at location {1} {2}"
                            + " with the region's send key {3} (expected {4}) and the region's receive key {5} (expected {6})",
                            sim.regionName, sim.regionLocX, sim.regionLocY,
                            sim.regionSendKey, Config.SimRecvKey, sim.regionRecvKey, Config.SimSendKey),
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
        public XmlRpcResponse XmlRpcSimulatorLoginMethod(XmlRpcRequest request)
        {
            RegionProfileData sim;
            RegionProfileData existingSim;

            Hashtable requestData = (Hashtable)request.Params[0];
            LLUUID uuid;

            if (!requestData.ContainsKey("UUID") || !LLUUID.TryParse((string)requestData["UUID"], out uuid))
            {
                m_log.Warn("[LOGIN PRELUDE]: Region connected without a UUID, sending back error response.");
                return ErrorResponse("No UUID passed to grid server - unable to connect you");
            }

            try
            {
                sim = RegionFromRequest(requestData);
            }
            catch (FormatException e)
            {
                m_log.Warn("[LOGIN PRELUDE]: Invalid login parameters, sending back error response.");
                return ErrorResponse("Wrong format in login parameters. Please verify parameters." + e.ToString());
            }

            m_log.InfoFormat("[LOGIN BEGIN]: Received login request from simulator: {0}", sim.regionName);

            existingSim = GetRegion(sim.regionHandle);

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

                foreach (IGridDataPlugin plugin in _plugins)
                {
                    try
                    {
                        DataResponse insertResponse;

                        if (existingSim == null)
                        {
                            insertResponse = plugin.AddProfile(sim);
                        }
                        else
                        {
                            insertResponse = plugin.UpdateProfile(sim);
                        }

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
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[LOGIN END]: " +
                                              "Unable to login region " + sim.UUID.ToString() + " via " + plugin.Name);
                        m_log.Warn("[LOGIN END]: " + e.ToString());
                    }
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

            // New! If set, use as URL to local sim storage (ie http://remotehost/region.yap)
            responseData["data_uri"] = sim.regionDataURI;

            responseData["allow_forceful_banlines"] = Config.AllowForcefulBanlines;

            // Instead of sending a multitude of message servers to the registering sim
            // we should probably be sending a single one and parhaps it's backup
            // that has responsibility over routing it's messages.

            // The Sim won't be contacting us again about any of the message server stuff during it's time up.

            responseData["messageserver_count"] = _MessageServers.Count;

            for (int i = 0; i < _MessageServers.Count; i++)
            {
                responseData["messageserver_uri" + i] = _MessageServers[i].URI;
                responseData["messageserver_sendkey" + i] = _MessageServers[i].sendkey;
                responseData["messageserver_recvkey" + i] = _MessageServers[i].recvkey;
            }
            return response;
        }

        private ArrayList GetSimNeighboursData(RegionProfileData sim)
        {
            ArrayList SimNeighboursData = new ArrayList();

            RegionProfileData neighbour;
            Hashtable NeighbourBlock;

            bool fastMode = false; // Only compatible with MySQL right now

            if (fastMode)
            {
                Dictionary<ulong, RegionProfileData> neighbours =
                    GetRegions(sim.regionLocX - 1, sim.regionLocY - 1, sim.regionLocX + 1,
                               sim.regionLocY + 1);

                foreach (KeyValuePair<ulong, RegionProfileData> aSim in neighbours)
                {
                    NeighbourBlock = new Hashtable();
                    NeighbourBlock["sim_ip"] = Util.GetHostFromDNS(aSim.Value.serverIP.ToString()).ToString();
                    NeighbourBlock["sim_port"] = aSim.Value.serverPort.ToString();
                    NeighbourBlock["region_locx"] = aSim.Value.regionLocX.ToString();
                    NeighbourBlock["region_locy"] = aSim.Value.regionLocY.ToString();
                    NeighbourBlock["UUID"] = aSim.Value.UUID.ToString();
                    NeighbourBlock["regionHandle"] = aSim.Value.regionHandle.ToString();

                    if (aSim.Value.UUID != sim.UUID)
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
                            GetRegion(
                                Helpers.UIntsToLong((uint)((sim.regionLocX + x) * Constants.RegionSize),
                                                    (uint)(sim.regionLocY + y) * Constants.RegionSize)) != null)
                        {
                            neighbour =
                                GetRegion(
                                    Helpers.UIntsToLong((uint)((sim.regionLocX + x) * Constants.RegionSize),
                                                        (uint)(sim.regionLocY + y) * Constants.RegionSize));

                            NeighbourBlock = new Hashtable();
                            NeighbourBlock["sim_ip"] = Util.GetHostFromDNS(neighbour.serverIP).ToString();
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

            sim.UUID = new LLUUID((string)requestData["UUID"]);
            sim.originUUID = new LLUUID((string)requestData["originUUID"]);

            sim.regionRecvKey = String.Empty;
            sim.regionSendKey = String.Empty;

            if (requestData.ContainsKey("region_secret"))
            {
                string regionsecret = (string)requestData["region_secret"];
                if (regionsecret.Length > 0)
                    sim.regionSecret = regionsecret;
                else
                    sim.regionSecret = Config.SimRecvKey;

            }
            else
            {
                sim.regionSecret = Config.SimRecvKey;
            }

            sim.regionDataURI = String.Empty;
            sim.regionAssetURI = Config.DefaultAssetServer;
            sim.regionAssetRecvKey = Config.AssetRecvKey;
            sim.regionAssetSendKey = Config.AssetSendKey;
            sim.regionUserURI = Config.DefaultUserServer;
            sim.regionUserSendKey = Config.UserSendKey;
            sim.regionUserRecvKey = Config.UserRecvKey;

            sim.serverIP = (string)requestData["sim_ip"];
            sim.serverPort = Convert.ToUInt32((string)requestData["sim_port"]);
            sim.httpPort = Convert.ToUInt32((string)requestData["http_port"]);
            sim.remotingPort = Convert.ToUInt32((string)requestData["remoting_port"]);
            sim.regionLocX = Convert.ToUInt32((string)requestData["region_locx"]);
            sim.regionLocY = Convert.ToUInt32((string)requestData["region_locy"]);
            sim.regionLocZ = 0;

            LLUUID textureID;
            if (LLUUID.TryParse((string)requestData["map-image-id"], out textureID))
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
            sim.owner_uuid = (string)requestData["master_avatar_uuid"];

            try
            {
                sim.regionRecvKey = (string)requestData["recvkey"];
                sim.regionSendKey = (string)requestData["authkey"];
            }
            catch (KeyNotFoundException) { }

            sim.regionHandle = Helpers.UIntsToLong((sim.regionLocX * Constants.RegionSize), (sim.regionLocY * Constants.RegionSize));
            sim.serverURI = (string)requestData["server_uri"];

            sim.httpServerURI = "http://" + sim.serverIP + ":" + sim.httpPort + "/";

            sim.regionName = (string)requestData["sim_name"];
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
        public XmlRpcResponse XmlRpcDeleteRegionMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            //RegionProfileData TheSim = null;
            string uuid;
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.ContainsKey("UUID"))
            {
                //TheSim = GetRegion(new LLUUID((string) requestData["UUID"]));
                uuid = requestData["UUID"].ToString();
                m_log.InfoFormat("[LOGOUT]: Logging out region: {0}", uuid);

                //                logToDB((new LLUUID((string)requestData["UUID"])).ToString(),"XmlRpcDeleteRegionMethod","", 5,"Attempting delete with UUID.");
            }
            else
            {
                responseData["error"] = "No UUID or region_handle passed to grid server - unable to delete";
                return response;
            }

            foreach (IGridDataPlugin plugin in _plugins)
            {
                //OpenSim.Data.MySQL.MySQLGridData dbengine = new OpenSim.Data.MySQL.MySQLGridData();
                try
                {
                    MySQLGridData mysqldata = (MySQLGridData)(plugin);
                    //DataResponse insertResponse = mysqldata.DeleteProfile(TheSim);
                    DataResponse insertResponse = mysqldata.DeleteProfile(uuid);
                    switch (insertResponse)
                    {
                        case DataResponse.RESPONSE_OK:
                            //MainLog.Instance.Verbose("grid", "Deleting region successful: " + uuid);
                            responseData["status"] = "Deleting region successful: " + uuid;
                            break;
                        case DataResponse.RESPONSE_ERROR:
                            //MainLog.Instance.Warn("storage", "Deleting region failed (Error): " + uuid);
                            responseData["status"] = "Deleting region failed (Error): " + uuid;
                            break;
                        case DataResponse.RESPONSE_INVALIDCREDENTIALS:
                            //MainLog.Instance.Warn("storage", "Deleting region failed (Invalid Credentials): " + uuid);
                            responseData["status"] = "Deleting region (Invalid Credentials): " + uuid;
                            break;
                        case DataResponse.RESPONSE_AUTHREQUIRED:
                            //MainLog.Instance.Warn("storage", "Deleting region failed (Authentication Required): " + uuid);
                            responseData["status"] = "Deleting region (Authentication Required): " + uuid;
                            break;
                    }
                }
                catch (Exception)
                {
                    m_log.Error("storage Unable to delete region " + uuid + " via MySQL");
                    //MainLog.Instance.Warn("storage", e.ToString());
                }
            }

            return response;
        }

        /// <summary>
        /// Returns an XML RPC response to a simulator profile request
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse XmlRpcSimulatorDataRequestMethod(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            RegionProfileData simData = null;
            if (requestData.ContainsKey("region_UUID"))
            {
                simData = GetRegion(new LLUUID((string)requestData["region_UUID"]));
            }
            else if (requestData.ContainsKey("region_handle"))
            {
                //CFK: The if/else below this makes this message redundant.
                //CFK: Console.WriteLine("requesting data for region " + (string) requestData["region_handle"]);
                simData = GetRegion(Convert.ToUInt64((string)requestData["region_handle"]));
            }
            else if (requestData.ContainsKey("region_name_search"))
            {
                simData = GetRegion((string)requestData["region_name_search"]);
            }

            if (simData == null)
            {
                //Sim does not exist
                Console.WriteLine("region not found");
                responseData["error"] = "Sim does not exist";
            }
            else
            {
                m_log.Info("[DATA]: found " + (string)simData.regionName + " regionHandle = " +
                           (string)requestData["region_handle"]);
                responseData["sim_ip"] = Util.GetHostFromDNS(simData.serverIP).ToString();
                responseData["sim_port"] = simData.serverPort.ToString();
                responseData["server_uri"] = simData.serverURI;
                responseData["http_port"] = simData.httpPort.ToString();
                responseData["remoting_port"] = simData.remotingPort.ToString();
                responseData["region_locx"] = simData.regionLocX.ToString();
                responseData["region_locy"] = simData.regionLocY.ToString();
                responseData["region_UUID"] = simData.UUID.UUID.ToString();
                responseData["region_name"] = simData.regionName;
                responseData["regionHandle"] = simData.regionHandle.ToString();
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcMapBlockMethod(XmlRpcRequest request)
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

            bool fastMode = (Config.DatabaseProvider == "OpenSim.Data.MySQL.dll");

            if (fastMode)
            {
                Dictionary<ulong, RegionProfileData> neighbours =
                    GetRegions((uint)xmin, (uint)ymin, (uint)xmax, (uint)ymax);

                foreach (KeyValuePair<ulong, RegionProfileData> aSim in neighbours)
                {
                    Hashtable simProfileBlock = new Hashtable();
                    simProfileBlock["x"] = aSim.Value.regionLocX.ToString();
                    simProfileBlock["y"] = aSim.Value.regionLocY.ToString();
                    //m_log.DebugFormat("[MAP]: Sending neighbour info for {0},{1}", aSim.Value.regionLocX, aSim.Value.regionLocY);
                    simProfileBlock["name"] = aSim.Value.regionName;
                    simProfileBlock["access"] = 21;
                    simProfileBlock["region-flags"] = 512;
                    simProfileBlock["water-height"] = 0;
                    simProfileBlock["agents"] = 1;
                    simProfileBlock["map-image-id"] = aSim.Value.regionMapTextureID.ToString();

                    // For Sugilite compatibility
                    simProfileBlock["regionhandle"] = aSim.Value.regionHandle.ToString();
                    simProfileBlock["sim_ip"] = aSim.Value.serverIP.ToString();
                    simProfileBlock["sim_port"] = aSim.Value.serverPort.ToString();
                    simProfileBlock["sim_uri"] = aSim.Value.serverURI.ToString();
                    simProfileBlock["uuid"] = aSim.Value.UUID.ToString();
                    simProfileBlock["remoting_port"] = aSim.Value.remotingPort;

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
                        ulong regHandle = Helpers.UIntsToLong((uint)(x * Constants.RegionSize), (uint)(y * Constants.RegionSize));
                        simProfile = GetRegion(regHandle);
                        if (simProfile != null)
                        {
                            Hashtable simProfileBlock = new Hashtable();
                            simProfileBlock["x"] = x;
                            simProfileBlock["y"] = y;
                            simProfileBlock["name"] = simProfile.regionName;
                            simProfileBlock["access"] = 0;
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
        /// Performs a REST Get Operation
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns></returns>
        public string RestGetRegionMethod(string request, string path, string param,
                                          OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            return RestGetSimMethod(String.Empty, "/sims/", param, httpRequest, httpResponse);
        }

        /// <summary>
        /// Performs a REST Set Operation
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns></returns>
        public string RestSetRegionMethod(string request, string path, string param,
                                          OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            return RestSetSimMethod(String.Empty, "/sims/", param, httpRequest, httpResponse);
        }

        /// <summary>
        /// Returns information about a sim via a REST Request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param">A string representing the sim's UUID</param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns>Information about the sim in XML</returns>
        public string RestGetSimMethod(string request, string path, string param,
                                       OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string respstring = String.Empty;

            RegionProfileData TheSim;

            LLUUID UUID;
            if (LLUUID.TryParse(param, out UUID))
            {
                TheSim = GetRegion(UUID);

                if (!(TheSim == null))
                {
                    respstring = "<Root>";
                    respstring += "<authkey>" + TheSim.regionSendKey + "</authkey>";
                    respstring += "<sim>";
                    respstring += "<uuid>" + TheSim.UUID.ToString() + "</uuid>";
                    respstring += "<regionname>" + TheSim.regionName + "</regionname>";
                    respstring += "<sim_ip>" + Util.GetHostFromDNS(TheSim.serverIP).ToString() + "</sim_ip>";
                    respstring += "<sim_port>" + TheSim.serverPort.ToString() + "</sim_port>";
                    respstring += "<region_locx>" + TheSim.regionLocX.ToString() + "</region_locx>";
                    respstring += "<region_locy>" + TheSim.regionLocY.ToString() + "</region_locy>";
                    respstring += "<estate_id>1</estate_id>";
                    respstring += "</sim>";
                    respstring += "</Root>";
                }
            }
            else
            {
                respstring = "<Root>";
                respstring += "<error>Param must be a UUID</error>";
                respstring += "</Root>";
            }

            return respstring;
        }

        /// <summary>
        /// Creates or updates a sim via a REST Method Request
        /// BROKEN with SQL Update
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns>"OK" or an error</returns>
        public string RestSetSimMethod(string request, string path, string param,
                                       OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            Console.WriteLine("Processing region update via REST method");
            RegionProfileData theSim;
            theSim = GetRegion(new LLUUID(param));
            if (theSim == null)
            {
                theSim = new RegionProfileData();
                LLUUID UUID = new LLUUID(param);
                theSim.UUID = UUID;
                theSim.regionRecvKey = Config.SimRecvKey;
            }

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(request);
            XmlNode rootnode = doc.FirstChild;
            XmlNode authkeynode = rootnode.ChildNodes[0];
            if (authkeynode.Name != "authkey")
            {
                return "ERROR! bad XML - expected authkey tag";
            }

            XmlNode simnode = rootnode.ChildNodes[1];
            if (simnode.Name != "sim")
            {
                return "ERROR! bad XML - expected sim tag";
            }

            //theSim.regionSendKey = Cfg;
            theSim.regionRecvKey = Config.SimRecvKey;
            theSim.regionSendKey = Config.SimSendKey;
            theSim.regionSecret = Config.SimRecvKey;
            theSim.regionDataURI = String.Empty;
            theSim.regionAssetURI = Config.DefaultAssetServer;
            theSim.regionAssetRecvKey = Config.AssetRecvKey;
            theSim.regionAssetSendKey = Config.AssetSendKey;
            theSim.regionUserURI = Config.DefaultUserServer;
            theSim.regionUserSendKey = Config.UserSendKey;
            theSim.regionUserRecvKey = Config.UserRecvKey;

            for (int i = 0; i < simnode.ChildNodes.Count; i++)
            {
                switch (simnode.ChildNodes[i].Name)
                {
                    case "regionname":
                        theSim.regionName = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_ip":
                        theSim.serverIP = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_port":
                        theSim.serverPort = Convert.ToUInt32(simnode.ChildNodes[i].InnerText);
                        break;

                    case "region_locx":
                        theSim.regionLocX = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        theSim.regionHandle = Helpers.UIntsToLong((theSim.regionLocX * Constants.RegionSize), (theSim.regionLocY * Constants.RegionSize));
                        break;

                    case "region_locy":
                        theSim.regionLocY = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        theSim.regionHandle = Helpers.UIntsToLong((theSim.regionLocX * Constants.RegionSize), (theSim.regionLocY * Constants.RegionSize));
                        break;
                }
            }

            theSim.serverURI = "http://" + theSim.serverIP + ":" + theSim.serverPort + "/";
            bool requirePublic = false;
            bool requireValid = true;

            if (requirePublic &&
                (theSim.serverIP.StartsWith("172.16") || theSim.serverIP.StartsWith("192.168") ||
                 theSim.serverIP.StartsWith("10.") || theSim.serverIP.StartsWith("0.") ||
                 theSim.serverIP.StartsWith("255.")))
            {
                return "ERROR! Servers must register with public addresses.";
            }

            if (requireValid && (theSim.serverIP.StartsWith("0.") || theSim.serverIP.StartsWith("255.")))
            {
                return "ERROR! 0.*.*.* / 255.*.*.* Addresses are invalid, please check your server config and try again";
            }

            try
            {
                m_log.Info("[DATA]: " +
                           "Updating / adding via " + _plugins.Count + " storage provider(s) registered.");

                foreach (IGridDataPlugin plugin in _plugins)
                {
                    try
                    {
                        //Check reservations
                        ReservationData reserveData =
                            plugin.GetReservationAtPoint(theSim.regionLocX, theSim.regionLocY);
                        if ((reserveData != null && reserveData.gridRecvKey == theSim.regionRecvKey) ||
                            (reserveData == null && authkeynode.InnerText != theSim.regionRecvKey))
                        {
                            plugin.AddProfile(theSim);
                            m_log.Info("[grid]: New sim added to grid (" + theSim.regionName + ")");
                            logToDB(theSim.UUID.ToString(), "RestSetSimMethod", String.Empty, 5,
                                    "Region successfully updated and connected to grid.");
                        }
                        else
                        {
                            m_log.Warn("[grid]: " +
                                       "Unable to update region (RestSetSimMethod): Incorrect reservation auth key.");
                            // Wanted: " + reserveData.gridRecvKey + ", Got: " + theSim.regionRecvKey + ".");
                            return "Unable to update region (RestSetSimMethod): Incorrect auth key.";
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[GRID]: GetRegionPlugin Handle " + plugin.Name + " unable to add new sim: " +
                                                      e.ToString());
                    }
                }
                return "OK";
            }
            catch (Exception e)
            {
                return "ERROR! Could not save to database! (" + e.ToString() + ")";
            }
        }

        public XmlRpcResponse XmlRPCRegisterMessageServer(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["URI"];
                string sendkey = (string)requestData["sendkey"];
                string recvkey = (string)requestData["recvkey"];
                MessageServerInfo m = new MessageServerInfo();
                m.URI = URI;
                m.sendkey = sendkey;
                m.recvkey = recvkey;
                if (!_MessageServers.Contains(m))
                    _MessageServers.Add(m);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }

        public XmlRpcResponse XmlRPCDeRegisterMessageServer(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["uri"];
                string sendkey = (string)requestData["sendkey"];
                string recvkey = (string)requestData["recvkey"];
                MessageServerInfo m = new MessageServerInfo();
                m.URI = URI;
                m.sendkey = sendkey;
                m.recvkey = recvkey;
                if (_MessageServers.Contains(m))
                    _MessageServers.Remove(m);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
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

        public LoginException(string message, string xmlRpcMessage) : base(message)
        {
            // FIXME: Might be neater to refactor and put the method inside here
            m_xmlRpcErrorResponse = GridManager.ErrorResponse(xmlRpcMessage);
        }

        public LoginException(string message, string xmlRpcMessage, Exception e) : base(message, e)
        {
            // FIXME: Might be neater to refactor and put the method inside here
            m_xmlRpcErrorResponse = GridManager.ErrorResponse(xmlRpcMessage);
        }
    }
}
