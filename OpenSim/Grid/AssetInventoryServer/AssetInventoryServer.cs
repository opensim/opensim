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
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Console;
using OpenSim.Framework.AssetLoader.Filesystem;
using Nini.Config;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer
{
    public class AssetInventoryServer : BaseOpenSimServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public IConfigSource ConfigFile;

        public IAssetStorageProvider StorageProvider;
        public IInventoryStorageProvider InventoryProvider;
        public IAuthenticationProvider AuthenticationProvider;
        public IAuthorizationProvider AuthorizationProvider;
        public IMetricsProvider MetricsProvider;

        private List<IAssetInventoryServerPlugin> m_frontends = new List<IAssetInventoryServerPlugin>();
        private List<IAssetInventoryServerPlugin> m_backends = new List<IAssetInventoryServerPlugin>();

        public AssetInventoryServer(IConfigSource config)
        {
            ConfigFile = config;

            m_console = new LocalConsole("AssetInventory");
            MainConsole.Instance = m_console;
        }

        public bool Start()
        {
            Startup();
            m_log.Info("[ASSETINVENTORY]: Starting AssetInventory Server");

            try
            {
                ConfigFile = AssetInventoryConfig.LoadConfig(ConfigFile);
            }
            catch (Exception)
            {
                m_log.Error("[ASSETINVENTORY]: Failed to load the config.");
                return false;
            }

            StorageProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AssetStorageProvider",
                                                             "asset_storage_provider", false) as IAssetStorageProvider;
            m_backends.Add(StorageProvider);

            InventoryProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/InventoryStorageProvider",
                                                               "inventory_storage_provider", false) as IInventoryStorageProvider;
            m_backends.Add(InventoryProvider);

            MetricsProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/MetricsProvider",
                                                             "metrics_provider", false) as IMetricsProvider;
            m_backends.Add(MetricsProvider);

            try
            {
                InitHttpServer((uint) ConfigFile.Configs["Config"].GetInt("listen_port"));
            }
            catch (Exception ex)
            {
                m_log.Error("[ASSETINVENTORY]: Initializing the HTTP server failed, shutting down: " + ex.Message);
                Shutdown();
                return false;
            }

            LoadDefaultAssets(); 

            AuthenticationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthenticationProvider",
                                                                    "authentication_provider", false) as IAuthenticationProvider;
            m_backends.Add(AuthenticationProvider);

            AuthorizationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthorizationProvider",
                                                                   "authorization_provider", false) as IAuthorizationProvider;
            m_backends.Add(AuthorizationProvider);

            m_frontends.AddRange(LoadAssetInventoryServerPlugins("/OpenSim/AssetInventoryServer/Frontend", "frontends"));

            // Inform the user if we don't have any frontends at this point.
            if (m_frontends.Count == 0)
                m_log.Info("[ASSETINVENTORY]: Starting with no frontends loaded, which isn't extremely useful. Did you set the 'frontends' configuration parameter?");

            return true;
        }

        public void Work()
        {
            m_console.Output("Enter help for a list of commands");

            while (true)
            {
                m_console.Prompt();
            }
        }

        public override void ShutdownSpecific()
        {
            foreach (IAssetInventoryServerPlugin plugin in m_frontends)
            {
                m_log.Debug("[ASSETINVENTORY]: Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { m_log.ErrorFormat("[ASSETINVENTORY]: Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            foreach (IAssetInventoryServerPlugin plugin in m_backends)
            {
                m_log.Debug("[ASSETINVENTORY]: Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { m_log.ErrorFormat("[ASSETINVENTORY]: Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            if (HttpServer != null)
                HttpServer.Stop();
        }

        void InitHttpServer(uint port)
        {
            m_httpServer = new BaseHttpServer(port);
            m_httpServer.Start();

            m_log.Info("[ASSETINVENTORY]: AssetInventory server is listening on port " + port);
        }

        private IAssetInventoryServerPlugin LoadAssetInventoryServerPlugin(string addinPath, string configParam, bool optional)
        {
            IAssetInventoryServerPlugin result = null;
            List<IAssetInventoryServerPlugin> plugins = LoadAssetInventoryServerPlugins(addinPath, configParam);

            if (plugins.Count == 1)
            {
                result = plugins[0];
            }
            else if (plugins.Count > 1)
            {
                m_log.ErrorFormat("[ASSETINVENTORY]: Only 1 plugin expected for extension point '{0}', {1} plugins loaded. Check the '{2}' parameter in the config file.",
                                  addinPath, plugins.Count, configParam);
                Shutdown();
                Environment.Exit(0);
            }
            else if (!optional)
            {
                m_log.ErrorFormat("[ASSETINVENTORY]: The extension point '{0}' is not optional. Check the '{1}' parameter in the config file.", addinPath, configParam);
                Shutdown();
                Environment.Exit(0);
            }

            return result;
        }

        private List<IAssetInventoryServerPlugin> LoadAssetInventoryServerPlugins(string addinPath, string configParam)
        {
            PluginLoader<IAssetInventoryServerPlugin> loader = new PluginLoader<IAssetInventoryServerPlugin>(new AssetInventoryServerPluginInitialiser(this));
            loader.Add(addinPath, new PluginIdFilter(ConfigFile.Configs["Plugins"].GetString(configParam)));

            try
            {
                loader.Load();
            }
            catch (PluginNotInitialisedException e)
            {
                m_log.ErrorFormat("[ASSETINVENTORY]: Error initialising plugin '{0}' for extension point '{1}'.", e.Message, addinPath);
                Shutdown();
                Environment.Exit(0);
            }

            return loader.Plugins;
        }

        private void LoadDefaultAssets()
        {
            AssetLoaderFileSystem assetLoader = new AssetLoaderFileSystem();
            assetLoader.ForEachDefaultXmlAsset(ConfigFile.Configs["Config"].GetString("assetset_location"), StoreAsset);
        }

        private void StoreAsset(AssetBase asset)
        {
            StorageProvider.TryCreateAsset(asset);
        }
    }
}
