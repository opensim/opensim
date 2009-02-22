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
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Region.Communications.Hypergrid;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Hypergrid;

namespace OpenSim
{
    public class HGOpenSimNode : OpenSim
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IHyperlink HGServices = null;

        private uint m_autoMappingX = 0;
        private uint m_autoMappingY = 0;
        private bool m_enableAutoMapping = false;

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

            MainConsole.Instance.Commands.AddCommand("hypergrid", false, "link-mapping", "link-mapping [<x> <y>] <cr>", "Set local coordinate to map HG regions to", RunCommand);
            MainConsole.Instance.Commands.AddCommand("hypergrid", false, "link-region", "link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>] <cr>", "Link a hypergrid region", RunCommand);
        
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
                userService, userService, inventoryService, gridService, userService, libraryRootFolder, m_configSettings.DumpAssetsToFile);

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
                new HGScene(
                    regionInfo, circuitManager, m_commsManager, sceneGridService, storageManager,
                    m_moduleLoader, m_configSettings.DumpAssetsToFile, m_configSettings.PhysicalPrim, 
                    m_configSettings.See_into_region_from_neighbor, m_config.Source, m_version);
        }

        new void RunCommand(string module, string[] cp)
        {
            List<string> cmdparams = new List<string>(cp);
            if (cmdparams.Count < 1)
                return;

            string command = cmdparams[0];
            cmdparams.RemoveAt(0);

            if (command.Equals("link-mapping"))
            {
                if (cmdparams.Count == 2)
                {
                    try
                    {
                        m_autoMappingX = Convert.ToUInt32(cmdparams[0]);
                        m_autoMappingY = Convert.ToUInt32(cmdparams[1]);
                        m_enableAutoMapping = true;
                    }
                    catch (Exception)
                    {
                        m_autoMappingX = 0;
                        m_autoMappingY = 0;
                        m_enableAutoMapping = false;
                    }
                }
            }
            else if (command.Equals("link-region"))
            {
                if (cmdparams.Count < 3)
                {
                    if ((cmdparams.Count == 1) || (cmdparams.Count == 2))
                    {
                        LoadXmlLinkFile(cmdparams);
                    }
                    else
                    {
                        LinkRegionCmdUsage();
                    }
                    return;
                }

                if (cmdparams[2].Contains(":"))
                { // New format
                    uint xloc, yloc;
                    string mapName;
                    try
                    {
                        xloc = Convert.ToUInt32(cmdparams[0]);
                        yloc = Convert.ToUInt32(cmdparams[1]);
                        mapName = cmdparams[2];
                        if (cmdparams.Count > 3)
                            for (int i = 3; i < cmdparams.Count; i++)
                                mapName += " " + cmdparams[i];

                        m_log.Info(">> MapName: " + mapName);
                        //internalPort = Convert.ToUInt32(cmdparams[4]);
                        //remotingPort = Convert.ToUInt32(cmdparams[5]);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[HGrid] Wrong format for link-region command: " + e.Message);
                        LinkRegionCmdUsage();
                        return;
                    }

                    HGHyperlink.TryLinkRegionToCoords(m_sceneManager.CurrentOrFirstScene, null, mapName, xloc, yloc);
                }
                else
                { // old format
                    RegionInfo regInfo;
                    uint xloc, yloc;
                    uint externalPort;
                    string externalHostName;
                    try
                    {
                        xloc = Convert.ToUInt32(cmdparams[0]);
                        yloc = Convert.ToUInt32(cmdparams[1]);
                        externalPort = Convert.ToUInt32(cmdparams[3]);
                        externalHostName = cmdparams[2];
                        //internalPort = Convert.ToUInt32(cmdparams[4]);
                        //remotingPort = Convert.ToUInt32(cmdparams[5]);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[HGrid] Wrong format for link-region command: " + e.Message);
                        LinkRegionCmdUsage();
                        return;
                    }

                    //if (TryCreateLink(xloc, yloc, externalPort, externalHostName, out regInfo))
                    if (HGHyperlink.TryCreateLink(m_sceneManager.CurrentOrFirstScene, null, xloc, yloc, "", externalPort, externalHostName, out regInfo))
                    {
                        if (cmdparams.Count >= 5)
                        {
                            regInfo.RegionName = "";
                            for (int i = 4; i < cmdparams.Count; i++)
                                regInfo.RegionName += cmdparams[i] + " ";
                        }
                    }
                }
                return;
            }
        }

        private void LoadXmlLinkFile(List<string> cmdparams)
        {
            //use http://www.hgurl.com/hypergrid.xml for test
            try
            {
                XmlReader r = XmlReader.Create(cmdparams[0]);
                XmlConfigSource cs = new XmlConfigSource(r);
                string[] excludeSections = null;

                if (cmdparams.Count == 2)
                {
                    if (cmdparams[1].ToLower().StartsWith("excludelist:"))
                    {
                        string excludeString = cmdparams[1].ToLower();
                        excludeString = excludeString.Remove(0, 12);
                        char[] splitter = { ';' };

                        excludeSections = excludeString.Split(splitter);
                    }
                }

                for (int i = 0; i < cs.Configs.Count; i++)
                {
                    bool skip = false;
                    if ((excludeSections != null) && (excludeSections.Length > 0))
                    {
                        for (int n = 0; n < excludeSections.Length; n++)
                        {
                            if (excludeSections[n] == cs.Configs[i].Name.ToLower())
                            {
                                skip = true;
                                break;
                            }
                        }
                    }
                    if (!skip)
                    {
                        ReadLinkFromConfig(cs.Configs[i]);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }
        

        private void ReadLinkFromConfig(IConfig config)
        {
            RegionInfo regInfo;
            uint xloc, yloc;
            uint externalPort;
            string externalHostName;
            uint realXLoc, realYLoc;

            xloc = Convert.ToUInt32(config.GetString("xloc", "0"));
            yloc = Convert.ToUInt32(config.GetString("yloc", "0"));
            externalPort = Convert.ToUInt32(config.GetString("externalPort", "0"));
            externalHostName = config.GetString("externalHostName", "");
            realXLoc = Convert.ToUInt32(config.GetString("real-xloc", "0"));
            realYLoc = Convert.ToUInt32(config.GetString("real-yloc", "0"));

            if (m_enableAutoMapping)
            {
                xloc = (uint)((xloc % 100) + m_autoMappingX);
                yloc = (uint)((yloc % 100) + m_autoMappingY);
            }

            if (((realXLoc == 0) && (realYLoc == 0)) || (((realXLoc - xloc < 3896) || (xloc - realXLoc < 3896)) && ((realYLoc - yloc < 3896) || (yloc - realYLoc < 3896))))
            {
                if (HGHyperlink.TryCreateLink(m_sceneManager.CurrentOrFirstScene, null, xloc, yloc, "", externalPort, externalHostName, out regInfo))
                {
                    regInfo.RegionName = config.GetString("localName", "");
                }
            }
        }
        

        private void LinkRegionCmdUsage()
        {
            m_log.Info("Usage: link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>]");
            m_log.Info("Usage: link-region <Xloc> <Yloc> <HostName> <HttpPort> [<LocalName>]");
            m_log.Info("Usage: link-region <URI_of_xml> [<exclude>]");
        }
    }
}
