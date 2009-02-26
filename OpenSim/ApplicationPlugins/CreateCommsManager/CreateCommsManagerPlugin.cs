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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.RegionLoader.Filesystem;
using OpenSim.Framework.RegionLoader.Web;
using OpenSim.Region.CoreModules.Agent.AssetTransaction;
using OpenSim.Region.CoreModules.Avatar.InstantMessage;
using OpenSim.Region.CoreModules.Scripting.DynamicTexture;
using OpenSim.Region.CoreModules.Scripting.LoadImageURL;
using OpenSim.Region.CoreModules.Scripting.XMLRPC;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Hypergrid;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Framework.Servers;
using OpenSim.ApplicationPlugins.LoadRegions;

namespace OpenSim.ApplicationPlugins.CreateCommsManager
{
    public class CreateCommsManagerPlugin : IApplicationPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region IApplicationPlugin Members

        // TODO: required by IPlugin, but likely not at all right
        string m_name = "CreateCommsManagerPlugin";
        string m_version = "0.0";

        public string Version { get { return m_version; } }
        public string Name { get { return m_name; } }

        protected OpenSimBase m_openSim;

        protected BaseHttpServer m_httpServer;

        protected CommunicationsManager m_commsManager;
        protected GridInfoService m_gridInfoService;
        protected IHyperlink HGServices = null;

        protected LoadRegionsPlugin m_loadRegionsPlugin;

        public void Initialise()
        {
            m_log.Info("[LOADREGIONS]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        public void Initialise(OpenSimBase openSim)
        {
            m_openSim = openSim;
            m_httpServer = openSim.HttpServer;

            InitialiseCommsManager(openSim);
            if (m_commsManager != null)
            {
                m_openSim.ApplicationRegistry.RegisterInterface<IUserService>(m_commsManager.UserService);
            }
        }

        public void PostInitialise()
        {
            if (m_openSim.ApplicationRegistry.TryGet<LoadRegionsPlugin>(out m_loadRegionsPlugin))
            {
                m_loadRegionsPlugin.OnNewRegionCreated += RegionCreated;
            }
        }

        public void Dispose()
        {
        }

        #endregion

        private void RegionCreated(IScene scene)
        {
            if (m_commsManager != null)
            {
                scene.RegisterModuleInterface<IUserService>(m_commsManager.UserService);
            }
        }

        private void InitialiseCommsManager(OpenSimBase openSim)
        {
            LibraryRootFolder libraryRootFolder = new LibraryRootFolder(m_openSim.ConfigurationSettings.LibrariesXMLFile);

            bool hgrid = m_openSim.ConfigSource.Source.Configs["Startup"].GetBoolean("hypergrid", false);

            if (hgrid)
            {
                HGOpenSimNode hgNode = (HGOpenSimNode)openSim;

                // Standalone mode is determined by !startupConfig.GetBoolean("gridmode", false)
                if (m_openSim.ConfigurationSettings.Standalone)
                {
                    InitialiseHGStandaloneServices(libraryRootFolder);
                }
                else
                {
                    // We are in grid mode
                    InitialiseHGGridServices(libraryRootFolder);
                }
                hgNode.HGServices = HGServices;
            }
            else
            {
                // Standalone mode is determined by !startupConfig.GetBoolean("gridmode", false)
                if (m_openSim.ConfigurationSettings.Standalone)
                {
                    InitialiseStandaloneServices(libraryRootFolder);
                }
                else
                {
                    // We are in grid mode
                    InitialiseGridServices(libraryRootFolder);
                }
            }

            openSim.CommunicationsManager = m_commsManager;
        }

        /// <summary>
        /// Initialises the backend services for standalone mode, and registers some http handlers
        /// </summary>
        /// <param name="libraryRootFolder"></param>
        protected virtual void InitialiseStandaloneServices(LibraryRootFolder libraryRootFolder)
        {
            LocalInventoryService inventoryService = new LocalInventoryService();
            inventoryService.AddPlugin(m_openSim.ConfigurationSettings.StandaloneInventoryPlugin, m_openSim.ConfigurationSettings.StandaloneInventorySource);

            LocalUserServices userService =
                new LocalUserServices(
                    m_openSim.NetServersInfo.DefaultHomeLocX, m_openSim.NetServersInfo.DefaultHomeLocY, inventoryService);
            userService.AddPlugin(m_openSim.ConfigurationSettings.StandaloneUserPlugin, m_openSim.ConfigurationSettings.StandaloneUserSource);

            LocalBackEndServices backendService = new LocalBackEndServices();

            LocalLoginService loginService =
                new LocalLoginService(
                    userService, m_openSim.ConfigurationSettings.StandaloneWelcomeMessage, inventoryService, backendService, m_openSim.NetServersInfo,
                    m_openSim.ConfigurationSettings.StandaloneAuthenticate, libraryRootFolder);

            m_commsManager
                = new CommunicationsLocal(
                    m_openSim.NetServersInfo, m_httpServer, m_openSim.AssetCache, userService, userService,
                    inventoryService, backendService, userService,
                    libraryRootFolder, m_openSim.ConfigurationSettings.DumpAssetsToFile);

            // set up XMLRPC handler for client's initial login request message
            m_httpServer.AddXmlRPCHandler("login_to_simulator", loginService.XmlRpcLoginMethod);

            // provides the web form login
            m_httpServer.AddHTTPHandler("login", loginService.ProcessHTMLLogin);

            // Provides the LLSD login
            m_httpServer.SetDefaultLLSDHandler(loginService.LLSDLoginMethod);

            // provide grid info
            // m_gridInfoService = new GridInfoService(m_config.Source.Configs["Startup"].GetString("inifile", Path.Combine(Util.configDir(), "OpenSim.ini")));
            m_gridInfoService = new GridInfoService(m_openSim.ConfigSource.Source);
            m_httpServer.AddXmlRPCHandler("get_grid_info", m_gridInfoService.XmlRpcGridInfoMethod);
            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/get_grid_info", m_gridInfoService.RestGetGridInfoMethod));
        }

        protected virtual void InitialiseGridServices(LibraryRootFolder libraryRootFolder)
        {
            m_commsManager
                = new CommunicationsOGS1(m_openSim.NetServersInfo, m_httpServer, m_openSim.AssetCache, libraryRootFolder);

            m_httpServer.AddStreamHandler(new OpenSim.SimStatusHandler());
        }

        protected virtual void InitialiseHGStandaloneServices(LibraryRootFolder libraryRootFolder)
        {
            // Standalone mode

            HGInventoryService inventoryService = new HGInventoryService(m_openSim.NetServersInfo.InventoryURL, null, false);
            inventoryService.AddPlugin(m_openSim.ConfigurationSettings.StandaloneInventoryPlugin, m_openSim.ConfigurationSettings.StandaloneInventorySource);

            LocalUserServices userService =
                new LocalUserServices(
                     m_openSim.NetServersInfo.DefaultHomeLocX, m_openSim.NetServersInfo.DefaultHomeLocY, inventoryService);
            userService.AddPlugin(m_openSim.ConfigurationSettings.StandaloneUserPlugin, m_openSim.ConfigurationSettings.StandaloneUserSource);

            //LocalBackEndServices backendService = new LocalBackEndServices();
            HGGridServicesStandalone gridService = new HGGridServicesStandalone(m_openSim.NetServersInfo, m_httpServer, m_openSim.AssetCache, m_openSim.SceneManager);

            LocalLoginService loginService =
                new LocalLoginService(
                    userService, m_openSim.ConfigurationSettings.StandaloneWelcomeMessage, inventoryService, gridService.LocalBackend, m_openSim.NetServersInfo,
                    m_openSim.ConfigurationSettings.StandaloneAuthenticate, libraryRootFolder);


            m_commsManager = new HGCommunicationsStandalone(m_openSim.NetServersInfo, m_httpServer, m_openSim.AssetCache,
                userService, userService, inventoryService, gridService, userService, libraryRootFolder, m_openSim.ConfigurationSettings.DumpAssetsToFile);

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
            m_gridInfoService = new GridInfoService(m_openSim.ConfigSource.Source);
            m_httpServer.AddXmlRPCHandler("get_grid_info", m_gridInfoService.XmlRpcGridInfoMethod);
            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/get_grid_info", m_gridInfoService.RestGetGridInfoMethod));
        }

        protected virtual void InitialiseHGGridServices(LibraryRootFolder libraryRootFolder)
        {
            m_commsManager = new HGCommunicationsGridMode(m_openSim.NetServersInfo, m_httpServer, m_openSim.AssetCache, m_openSim.SceneManager, libraryRootFolder);

            HGServices = ((HGCommunicationsGridMode)m_commsManager).HGServices;

            m_httpServer.AddStreamHandler(new OpenSim.SimStatusHandler());
        }
    }
}
