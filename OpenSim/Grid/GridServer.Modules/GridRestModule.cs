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
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;

namespace OpenSim.Grid.GridServer.Modules
{
    public class GridRestModule
    {
         private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private GridDBService m_gridDBService;
        private IGridServiceCore m_gridCore;

        protected GridConfig m_config;

        /// <value>
        /// Used to notify old regions as to which OpenSim version to upgrade to
        /// </value>
        //private string m_opensimVersion;

        protected BaseHttpServer m_httpServer;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="opensimVersion">
        /// Used to notify old regions as to which OpenSim version to upgrade to
        /// </param>
        public GridRestModule()
        {
        }

        public void Initialise(string opensimVersion, GridDBService gridDBService, IGridServiceCore gridCore, GridConfig config)
        {
            //m_opensimVersion = opensimVersion;
            m_gridDBService = gridDBService;
            m_gridCore = gridCore;
            m_config = config;
            RegisterHandlers();
        }

        public void PostInitialise()
        {

        }

        public void RegisterHandlers()
        {
            //have these in separate method as some servers restart the http server and reregister all the handlers.
            m_httpServer = m_gridCore.GetHttpServer();

            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/sims/", RestGetSimMethod));
            m_httpServer.AddStreamHandler(new RestStreamHandler("POST", "/sims/", RestSetSimMethod));

            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/regions/", RestGetRegionMethod));
            m_httpServer.AddStreamHandler(new RestStreamHandler("POST", "/regions/", RestSetRegionMethod));
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

            UUID UUID;
            if (UUID.TryParse(param, out UUID))
            {
                TheSim = m_gridDBService.GetRegion(UUID);

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
            m_log.Info("Processing region update via REST method");
            RegionProfileData theSim;
            theSim = m_gridDBService.GetRegion(new UUID(param));
            if (theSim == null)
            {
                theSim = new RegionProfileData();
                UUID UUID = new UUID(param);
                theSim.UUID = UUID;
                theSim.regionRecvKey = m_config.SimRecvKey;
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
            theSim.regionRecvKey = m_config.SimRecvKey;
            theSim.regionSendKey = m_config.SimSendKey;
            theSim.regionSecret = m_config.SimRecvKey;
            theSim.regionDataURI = String.Empty;
            theSim.regionAssetURI = m_config.DefaultAssetServer;
            theSim.regionAssetRecvKey = m_config.AssetRecvKey;
            theSim.regionAssetSendKey = m_config.AssetSendKey;
            theSim.regionUserURI = m_config.DefaultUserServer;
            theSim.regionUserSendKey = m_config.UserSendKey;
            theSim.regionUserRecvKey = m_config.UserRecvKey;

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
                        theSim.regionHandle = Utils.UIntsToLong((theSim.regionLocX * Constants.RegionSize), (theSim.regionLocY * Constants.RegionSize));
                        break;

                    case "region_locy":
                        theSim.regionLocY = Convert.ToUInt32((string)simnode.ChildNodes[i].InnerText);
                        theSim.regionHandle = Utils.UIntsToLong((theSim.regionLocX * Constants.RegionSize), (theSim.regionLocY * Constants.RegionSize));
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
                           "Updating / adding via " + m_gridDBService.GetNumberOfPlugins() + " storage provider(s) registered.");

                return m_gridDBService.CheckReservations(theSim, authkeynode);
            }
            catch (Exception e)
            {
                return "ERROR! Could not save to database! (" + e.ToString() + ")";
            }
        }
    }
}
