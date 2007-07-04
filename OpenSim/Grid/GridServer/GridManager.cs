/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Utilities;

namespace OpenSim.Grid.GridServer
{
    class GridManager
    {
        Dictionary<string, IGridData> _plugins = new Dictionary<string, IGridData>();
        Dictionary<string, ILogData> _logplugins = new Dictionary<string, ILogData>();

        public GridConfig config;

        /// <summary>
        /// Adds a new grid server plugin - grid servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the grid server plugin DLL</param>
        public void AddPlugin(string FileName)
		{
            MainLog.Instance.Verbose("Storage: Attempting to load " + FileName);
			Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            MainLog.Instance.Verbose("Storage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
			foreach (Type pluginType in pluginAssembly.GetTypes())
			{
                if (!pluginType.IsAbstract)
                {
                    // Regions go here
                    Type typeInterface = pluginType.GetInterface("IGridData", true);

                    if (typeInterface != null)
                    {
                        IGridData plug = (IGridData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise();
                        this._plugins.Add(plug.getName(), plug);
                        MainLog.Instance.Verbose("Storage: Added IGridData Interface");
                    }

                    typeInterface = null;

                    // Logs go here
                    typeInterface = pluginType.GetInterface("ILogData", true);

                    if (typeInterface != null)
                    {
                        ILogData plug = (ILogData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise();
                        this._logplugins.Add(plug.getName(), plug);
                        MainLog.Instance.Verbose( "Storage: Added ILogData Interface");
                    }

                    typeInterface = null;
                }
			}
			
			pluginAssembly = null; 
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
            foreach (KeyValuePair<string, ILogData> kvp in _logplugins)
            {
                try
                {
                    kvp.Value.saveLog("Gridserver", target, method, args, priority, message);
                }
                catch (Exception)
                {
                    MainLog.Instance.Warn("Storage: unable to write log via " + kvp.Key);
                }
            }
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A UUID key of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public SimProfileData getRegion(LLUUID uuid)
        {
            foreach(KeyValuePair<string,IGridData> kvp in _plugins) {
                try
                {
                    return kvp.Value.GetProfileByLLUUID(uuid);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Warn("Message from Storage: " + e.Message);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A regionHandle of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public SimProfileData getRegion(ulong handle)
        {
            foreach (KeyValuePair<string, IGridData> kvp in _plugins)
            {
                try
                {
                    return kvp.Value.GetProfileByHandle(handle);
                }
                catch
                {
                    MainLog.Instance.Warn("Storage: Unable to find region " + handle.ToString() + " via " + kvp.Key);
                }
            }
            return null;
        }

        public Dictionary<ulong, SimProfileData> getRegions(uint xmin, uint ymin, uint xmax, uint ymax)
        {
            Dictionary<ulong, SimProfileData> regions = new Dictionary<ulong, SimProfileData>();

            SimProfileData[] neighbours;

            foreach (KeyValuePair<string, IGridData> kvp in _plugins)
            {
                try
                {
                    neighbours = kvp.Value.GetProfilesInRange(xmin, ymin, xmax, ymax);
                    foreach (SimProfileData neighbour in neighbours)
                    {
                        regions[neighbour.regionHandle] = neighbour;
                    }
                }
                catch
                {
                    MainLog.Instance.Warn("Storage: Unable to query regionblock via " + kvp.Key);
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
            string response = "";
            SimProfileData central_region = getRegion(reqhandle);
            SimProfileData neighbour;
            for (int x = -1; x < 2; x++) for (int y = -1; y < 2; y++)
                {
                    if (getRegion(Util.UIntsToLong((uint)((central_region.regionLocX + x) * 256), (uint)(central_region.regionLocY + y) * 256)) != null)
                    {
                        neighbour = getRegion(Util.UIntsToLong((uint)((central_region.regionLocX + x) * 256), (uint)(central_region.regionLocY + y) * 256));
                        response += "<neighbour>";
                        response += "<sim_ip>" + neighbour.serverIP + "</sim_ip>";
                        response += "<sim_port>" + neighbour.serverPort.ToString() + "</sim_port>";
                        response += "<locx>" + neighbour.regionLocX.ToString() + "</locx>";
                        response += "<locy>" + neighbour.regionLocY.ToString() + "</locy>";
                        response += "<regionhandle>" + neighbour.regionHandle.ToString() + "</regionhandle>";
                        response += "</neighbour>";

                    }
                }
            return response;
        }

        /// <summary>
        /// Performed when a region connects to the grid server initially.
        /// </summary>
        /// <param name="request">The XMLRPC Request</param>
        /// <returns>Startup parameters</returns>
        public XmlRpcResponse XmlRpcSimulatorLoginMethod(XmlRpcRequest request)
        {

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            SimProfileData TheSim = null;
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.ContainsKey("UUID"))
            {
                TheSim = getRegion(new LLUUID((string)requestData["UUID"]));
                
                logToDB((new LLUUID((string)requestData["UUID"])).ToStringHyphenated(),"XmlRpcSimulatorLoginMethod","", 5,"Region attempting login with UUID.");
            }
            else if (requestData.ContainsKey("region_handle"))
            {

                TheSim = getRegion((ulong)Convert.ToUInt64(requestData["region_handle"]));
                logToDB((string)requestData["region_handle"], "XmlRpcSimulatorLoginMethod", "", 5, "Region attempting login with regionHandle.");
            }
            else
            {
                responseData["error"] = "No UUID or region_handle passed to grid server - unable to connect you";
                return response;
            }

            if (TheSim == null) // Shouldnt this be in the REST Simulator Set method?
            {
                //NEW REGION
                TheSim = new SimProfileData();

                TheSim.regionRecvKey = config.SimRecvKey;
                TheSim.regionSendKey = config.SimSendKey;
                TheSim.regionSecret = config.SimRecvKey;
                TheSim.regionDataURI = "";
                TheSim.regionAssetURI = config.DefaultAssetServer;
                TheSim.regionAssetRecvKey = config.AssetRecvKey;
                TheSim.regionAssetSendKey = config.AssetSendKey;
                TheSim.regionUserURI = config.DefaultUserServer;
                TheSim.regionUserSendKey = config.UserSendKey;
                TheSim.regionUserRecvKey = config.UserRecvKey;

                TheSim.serverIP = (string)requestData["sim_ip"];
                TheSim.serverPort = Convert.ToUInt32((string)requestData["sim_port"]);
                TheSim.regionLocX = Convert.ToUInt32((string)requestData["region_locx"]);
                TheSim.regionLocY = Convert.ToUInt32((string)requestData["region_locy"]);
                TheSim.regionLocZ = 0;

                TheSim.regionHandle = Helpers.UIntsToLong((TheSim.regionLocX * 256), (TheSim.regionLocY * 256));
                TheSim.serverURI = "http://" + TheSim.serverIP + ":" + TheSim.serverPort + "/";

                TheSim.regionName = (string)requestData["sim_name"];
                TheSim.UUID = new LLUUID((string)requestData["UUID"]);

                foreach (KeyValuePair<string, IGridData> kvp in _plugins)
                {
                    try
                    {
                        DataResponse insertResponse = kvp.Value.AddProfile(TheSim);
                        switch(insertResponse)
                        {
                            case DataResponse.RESPONSE_OK:
                                OpenSim.Framework.Console.MainLog.Instance.Verbose("New sim creation successful: " + TheSim.regionName);
                                break;
                            case DataResponse.RESPONSE_ERROR:
                                OpenSim.Framework.Console.MainLog.Instance.Warn("New sim creation failed (Error): " + TheSim.regionName);
                                break;
                            case DataResponse.RESPONSE_INVALIDCREDENTIALS:
                                OpenSim.Framework.Console.MainLog.Instance.Warn("New sim creation failed (Invalid Credentials): " + TheSim.regionName);
                                break;
                            case DataResponse.RESPONSE_AUTHREQUIRED:
                                OpenSim.Framework.Console.MainLog.Instance.Warn("New sim creation failed (Authentication Required): " + TheSim.regionName);
                                break;
                        }
                        
                    }
                    catch (Exception e)
                    {
                        OpenSim.Framework.Console.MainLog.Instance.Warn("Storage: Unable to add region " + TheSim.UUID.ToStringHyphenated() + " via " + kvp.Key);
                    }
                }


                if (getRegion(TheSim.regionHandle) == null)
                {
                    responseData["error"] = "Unable to add new region";
                    return response;
                }
            }
           

            ArrayList SimNeighboursData = new ArrayList();

            SimProfileData neighbour;
            Hashtable NeighbourBlock;

            bool fastMode = false; // Only compatible with MySQL right now

            if (fastMode)
            {
                Dictionary<ulong, SimProfileData> neighbours = getRegions(TheSim.regionLocX - 1, TheSim.regionLocY - 1, TheSim.regionLocX + 1, TheSim.regionLocY + 1);

                foreach (KeyValuePair<ulong, SimProfileData> aSim in neighbours)
                {
                    NeighbourBlock = new Hashtable();
                    NeighbourBlock["sim_ip"] = aSim.Value.serverIP.ToString();
                    NeighbourBlock["sim_port"] = aSim.Value.serverPort.ToString();
                    NeighbourBlock["region_locx"] = aSim.Value.regionLocX.ToString();
                    NeighbourBlock["region_locy"] = aSim.Value.regionLocY.ToString();
                    NeighbourBlock["UUID"] = aSim.Value.UUID.ToString();

                    if (aSim.Value.UUID != TheSim.UUID)
                        SimNeighboursData.Add(NeighbourBlock);
                }
            }
            else
            {
                for (int x = -1; x < 2; x++) for (int y = -1; y < 2; y++)
                    {
                        if (getRegion(Helpers.UIntsToLong((uint)((TheSim.regionLocX + x) * 256), (uint)(TheSim.regionLocY + y) * 256)) != null)
                        {
                            neighbour = getRegion(Helpers.UIntsToLong((uint)((TheSim.regionLocX + x) * 256), (uint)(TheSim.regionLocY + y) * 256));

                            NeighbourBlock = new Hashtable();
                            NeighbourBlock["sim_ip"] = neighbour.serverIP;
                            NeighbourBlock["sim_port"] = neighbour.serverPort.ToString();
                            NeighbourBlock["region_locx"] = neighbour.regionLocX.ToString();
                            NeighbourBlock["region_locy"] = neighbour.regionLocY.ToString();
                            NeighbourBlock["UUID"] = neighbour.UUID.ToString();

                            if (neighbour.UUID != TheSim.UUID) SimNeighboursData.Add(NeighbourBlock);
                        }
                    }
            }

            responseData["UUID"] = TheSim.UUID.ToString();
            responseData["region_locx"] = TheSim.regionLocX.ToString();
            responseData["region_locy"] = TheSim.regionLocY.ToString();
            responseData["regionname"] = TheSim.regionName;
            responseData["estate_id"] = "1";
            responseData["neighbours"] = SimNeighboursData;

            responseData["sim_ip"] = TheSim.serverIP;
            responseData["sim_port"] = TheSim.serverPort.ToString();
            responseData["asset_url"] = TheSim.regionAssetURI;
            responseData["asset_sendkey"] = TheSim.regionAssetSendKey;
            responseData["asset_recvkey"] = TheSim.regionAssetRecvKey;
            responseData["user_url"] = TheSim.regionUserURI;
            responseData["user_sendkey"] = TheSim.regionUserSendKey;
            responseData["user_recvkey"] = TheSim.regionUserRecvKey;
            responseData["authkey"] = TheSim.regionSecret;

            // New! If set, use as URL to local sim storage (ie http://remotehost/region.yap)
            responseData["data_uri"] = TheSim.regionDataURI;
        

        return response;
        }

        public XmlRpcResponse XmlRpcMapBlockMethod(XmlRpcRequest request)
        {
            int xmin=980, ymin=980, xmax=1020, ymax=1020;

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

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;
            IList simProfileList = new ArrayList();

            bool fastMode = true; // MySQL Only

            if (fastMode)
            {
                Dictionary<ulong, SimProfileData> neighbours = getRegions((uint)xmin, (uint)ymin, (uint)xmax, (uint)ymax);

                foreach (KeyValuePair<ulong, SimProfileData> aSim in neighbours)
                {
                    Hashtable simProfileBlock = new Hashtable();
                    simProfileBlock["x"] = aSim.Value.regionLocX.ToString();
                    simProfileBlock["y"] = aSim.Value.regionLocY.ToString();
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
                    simProfileBlock["uuid"] = aSim.Value.UUID.ToStringHyphenated();

                    simProfileList.Add(simProfileBlock);
                }
                MainLog.Instance.Verbose("World map request processed, returned " + simProfileList.Count.ToString() + " region(s) in range via FastMode");
            }
            else
            {
                SimProfileData simProfile;
                for (int x = xmin; x < xmax; x++)
                {
                    for (int y = ymin; y < ymax; y++)
                    {
                        simProfile = getRegion(Helpers.UIntsToLong((uint)(x * 256), (uint)(y * 256)));
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
                            simProfileBlock["uuid"] = simProfile.UUID.ToStringHyphenated();

                            simProfileList.Add(simProfileBlock);
                        }
                    }
                }
                MainLog.Instance.Verbose("World map request processed, returned " + simProfileList.Count.ToString() + " region(s) in range via Standard Mode");
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
        /// <returns></returns>
        public string RestGetRegionMethod(string request, string path, string param)
        {
            return RestGetSimMethod("", "/sims/", param);
        }

        /// <summary>
        /// Performs a REST Set Operation
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string RestSetRegionMethod(string request, string path, string param)
        {
            return RestSetSimMethod("", "/sims/", param);
        }

        /// <summary>
        /// Returns information about a sim via a REST Request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns>Information about the sim in XML</returns>
        public string RestGetSimMethod(string request, string path, string param)
        {
            string respstring = String.Empty;

            SimProfileData TheSim;
            LLUUID UUID = new LLUUID(param);
            TheSim = getRegion(UUID);

            if (!(TheSim == null))
            {
                respstring = "<Root>";
                respstring += "<authkey>" + TheSim.regionSendKey + "</authkey>";
                respstring += "<sim>";
                respstring += "<uuid>" + TheSim.UUID.ToString() + "</uuid>";
                respstring += "<regionname>" + TheSim.regionName + "</regionname>";
                respstring += "<sim_ip>" + TheSim.serverIP + "</sim_ip>";
                respstring += "<sim_port>" + TheSim.serverPort.ToString() + "</sim_port>";
                respstring += "<region_locx>" + TheSim.regionLocX.ToString() + "</region_locx>";
                respstring += "<region_locy>" + TheSim.regionLocY.ToString() + "</region_locy>";
                respstring += "<estate_id>1</estate_id>";
                respstring += "</sim>";
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
        /// <returns>"OK" or an error</returns>
        public string RestSetSimMethod(string request, string path, string param)
        {
            Console.WriteLine("Processing region update via REST method");
            SimProfileData TheSim;
            TheSim = getRegion(new LLUUID(param));
            if ((TheSim) == null)
            {
                TheSim = new SimProfileData();
                LLUUID UUID = new LLUUID(param);
                TheSim.UUID = UUID;
                TheSim.regionRecvKey = config.SimRecvKey;
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

            if (authkeynode.InnerText != TheSim.regionRecvKey)
            {
                MainLog.Instance.Warn("Invalid Key Attempt on region update");
                return "ERROR! invalid key";
            }

            //TheSim.regionSendKey = Cfg;
            TheSim.regionRecvKey = config.SimRecvKey;
            TheSim.regionSendKey = config.SimSendKey;
            TheSim.regionSecret = config.SimRecvKey;
            TheSim.regionDataURI = "";
            TheSim.regionAssetURI = config.DefaultAssetServer;
            TheSim.regionAssetRecvKey = config.AssetRecvKey;
            TheSim.regionAssetSendKey = config.AssetSendKey;
            TheSim.regionUserURI = config.DefaultUserServer;
            TheSim.regionUserSendKey = config.UserSendKey;
            TheSim.regionUserRecvKey = config.UserRecvKey;


            for (int i = 0; i < simnode.ChildNodes.Count; i++)
            {
                switch (simnode.ChildNodes[i].Name)
                {
                    case "regionname":
                        TheSim.regionName = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_ip":
                        TheSim.serverIP = simnode.ChildNodes[i].InnerText;
                        break;

                    case "sim_port":
                        TheSim.serverPort = Convert.ToUInt32(simnode.ChildNodes[i].InnerText);
                        break;

                    case "region_locx":
                        TheSim.regionLocX = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        TheSim.regionHandle = Helpers.UIntsToLong((TheSim.regionLocX * 256), (TheSim.regionLocY * 256));
                        break;

                    case "region_locy":
                        TheSim.regionLocY = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        TheSim.regionHandle = Helpers.UIntsToLong((TheSim.regionLocX * 256), (TheSim.regionLocY * 256));
                        break;
                }
            }

            TheSim.serverURI = "http://" + TheSim.serverIP + ":" + TheSim.serverPort + "/";

            bool requirePublic = false;

            if (requirePublic && (TheSim.serverIP.StartsWith("172.16") || TheSim.serverIP.StartsWith("192.168") || TheSim.serverIP.StartsWith("10.") || TheSim.serverIP.StartsWith("0.") || TheSim.serverIP.StartsWith("255.")))
            {
                return "ERROR! Servers must register with public addresses.";
            }

            
            try
            {
                MainLog.Instance.Verbose("Updating / adding via " + _plugins.Count + " storage provider(s) registered.");
                foreach (KeyValuePair<string, IGridData> kvp in _plugins)
                {
                    try
                    {
                        //Check reservations
                        ReservationData reserveData = kvp.Value.GetReservationAtPoint(TheSim.regionLocX, TheSim.regionLocY);
                        if ((reserveData != null && reserveData.gridRecvKey == TheSim.regionRecvKey) || (reserveData == null))
                        {
                            kvp.Value.AddProfile(TheSim);
                            MainLog.Instance.Verbose("New sim added to grid (" + TheSim.regionName + ")");
                            logToDB(TheSim.UUID.ToStringHyphenated(), "RestSetSimMethod", "", 5, "Region successfully updated and connected to grid.");
                        }
                        else
                        {
                            MainLog.Instance.Warn("Unable to update region (RestSetSimMethod): Incorrect reservation auth key.");// Wanted: " + reserveData.gridRecvKey + ", Got: " + TheSim.regionRecvKey + ".");
                            return "Unable to update region (RestSetSimMethod): Incorrect auth key.";
                        }
                    }
                    catch (Exception e)
                    {
                        MainLog.Instance.Verbose("getRegionPlugin Handle " + kvp.Key + " unable to add new sim: " + e.ToString());
                    }
                }
                return "OK";
            }
            catch (Exception e)
            {
                return "ERROR! Could not save to database! (" + e.ToString() + ")";
            }
        }

    }
}
