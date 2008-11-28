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
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;
using OpenSim.Region.ClientStack;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.Hypergrid;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Hypergrid;

using Timer = System.Timers.Timer;

namespace OpenSim
{
    public class HGOpenSimNode : OpenSim
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IHyperlink HGServices = null;

        public HGOpenSimNode(IConfigSource configSource) : base(configSource)
        {
        }


        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        protected override void StartupSpecific()
        {
            m_log.Info("====================================================================");
            m_log.Info("=================== STARTING HYPERGRID NODE ========================");
            m_log.Info("====================================================================");

            base.StartupSpecific();
        }


        protected override void InitialiseStandaloneServices(LibraryRootFolder libraryRootFolder)
        {
            // Standalone mode

            HGInventoryService inventoryService = new HGInventoryService(m_networkServersInfo.InventoryURL, null, false);
            inventoryService.AddPlugin(m_configSettings.StandaloneInventoryPlugin, m_configSettings.StandaloneInventorySource);

            LocalUserServices userService =
                new LocalUserServices(
                    m_networkServersInfo.DefaultHomeLocX, m_networkServersInfo.DefaultHomeLocY, inventoryService);
            userService.AddPlugin(m_configSettings.StandaloneUserPlugin, m_configSettings.StandaloneUserSource);

            //LocalBackEndServices backendService = new LocalBackEndServices();
            HGGridServicesStandalone gridService = new HGGridServicesStandalone(m_networkServersInfo, m_httpServer, m_assetCache, m_sceneManager);

            LocalLoginService loginService =
                new LocalLoginService(
                    userService, m_configSettings.StandaloneWelcomeMessage, inventoryService, gridService.LocalBackend, m_networkServersInfo,
                    m_configSettings.StandaloneAuthenticate, libraryRootFolder);


            m_commsManager = new HGCommunicationsStandalone(m_networkServersInfo, m_httpServer, m_assetCache,
                userService, userService, inventoryService, gridService, gridService, userService, libraryRootFolder, m_configSettings.DumpAssetsToFile);

            inventoryService.UserProfileCache = m_commsManager.UserProfileCacheService;
            HGServices = gridService;

            // set up XMLRPC handler for client's initial login request message
            m_httpServer.AddXmlRPCHandler("login_to_simulator", loginService.XmlRpcLoginMethod);

            // provides the web form login
            m_httpServer.AddHTTPHandler("login", loginService.ProcessHTMLLogin);

            // Provides the LLSD login
            m_httpServer.SetDefaultLLSDHandler(loginService.LLSDLoginMethod);

            // provide grid info
            // m_gridInfoService = new GridInfoService(m_config.Source.Configs["Startup"].GetString("inifile", Path.Combine(Util.configDir(), "OpenSim.ini")));
            m_gridInfoService = new GridInfoService(m_config.Source);
            m_httpServer.AddXmlRPCHandler("get_grid_info", m_gridInfoService.XmlRpcGridInfoMethod);
            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/get_grid_info", m_gridInfoService.RestGetGridInfoMethod));
        }

        protected override void InitialiseGridServices(LibraryRootFolder libraryRootFolder)
        {
            m_commsManager = new HGCommunicationsGridMode(m_networkServersInfo, m_httpServer, m_assetCache, m_sceneManager, libraryRootFolder);

            HGServices = ((HGCommunicationsGridMode)m_commsManager).HGServices;

            m_httpServer.AddStreamHandler(new SimStatusHandler());
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager,
                                     AgentCircuitManager circuitManager)
        {
            HGSceneCommunicationService sceneGridService = new HGSceneCommunicationService(m_commsManager, HGServices);
            return
                new HGScene(regionInfo, circuitManager, m_commsManager, sceneGridService, m_assetCache,
                          storageManager, m_httpServer,
                          m_moduleLoader, m_configSettings.DumpAssetsToFile, m_configSettings.PhysicalPrim, m_configSettings.See_into_region_from_neighbor, m_config.Source,
                          m_version);
        }

        public override void RunCmd(string command, string[] cmdparams)
        {
            if (command.Equals("link-region"))
            {
                // link-region <Xloc> <Yloc> <HostName> <HttpPort> <LocalName>
                if (cmdparams.Length < 4)
                {
                    LinkRegionCmdUsage();
                    return;
                }

                RegionInfo regInfo = new RegionInfo();
                uint xloc, yloc;
                uint externalPort;
                try
                {
                    xloc = Convert.ToUInt32(cmdparams[0]);
                    yloc = Convert.ToUInt32(cmdparams[1]);
                    externalPort = Convert.ToUInt32(cmdparams[3]);
                    //internalPort = Convert.ToUInt32(cmdparams[4]);
                    //remotingPort = Convert.ToUInt32(cmdparams[5]);
                }
                catch (Exception e)
                {
                    m_log.Warn("[HGrid] Wrong format for link-region command: " + e.Message);
                    LinkRegionCmdUsage();
                    return;
                }
                regInfo.RegionLocX = xloc;
                regInfo.RegionLocY = yloc;
                regInfo.ExternalHostName = cmdparams[2];
                regInfo.HttpPort = externalPort;
                //regInfo.RemotingPort = remotingPort;
                try
                {
                    regInfo.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)0);
                }
                catch (Exception e)
                {
                    m_log.Warn("[HGrid] Wrong format for link-region command: " + e.Message);
                    LinkRegionCmdUsage();
                    return;
                }
                regInfo.RemotingAddress = regInfo.ExternalEndPoint.Address.ToString();

                // Finally, link it
                try
                {
                    m_sceneManager.CurrentOrFirstScene.CommsManager.GridService.RegisterRegion(regInfo);
                }
                catch (Exception e)
                {
                    m_log.Warn("[HGrid] Unable to link region: " + e.StackTrace);
                }
                if (cmdparams.Length >= 5)
                {
                    regInfo.RegionName = "";
                    for (int i = 4; i < cmdparams.Length; i++)
                        regInfo.RegionName += cmdparams[i] + " ";
                }
            }

            base.RunCmd(command, cmdparams);

        }

        private void LinkRegionCmdUsage()
        {
            Console.WriteLine("Usage: link-region <Xloc> <Yloc> <HostName> <HttpPort> [<LocalName>]");
        }
    }
}
