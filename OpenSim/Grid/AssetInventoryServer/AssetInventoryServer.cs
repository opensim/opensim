/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Console;
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

            m_console = new ConsoleBase("AssetInventory");
            MainConsole.Instance = m_console;
        }

        public bool Start()
        {
            Startup();
            m_log.Info("[ASSETINVENTORY] Starting AssetInventory Server");

            try
            {
                ConfigFile = AssetInventoryConfig.LoadConfig(ConfigFile);
            }
            catch (Exception)
            {
                m_log.Error("[ASSETINVENTORY] Failed to load the config.");
                return false;
            }

            IConfig pluginConfig = ConfigFile.Configs["Plugins"];

            StorageProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/StorageProvider", pluginConfig.GetString("asset_storage_provider")) as IAssetStorageProvider;
            m_backends.Add(StorageProvider);

            InventoryProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/InventoryProvider", pluginConfig.GetString("inventory_storage_provider")) as IInventoryStorageProvider;
            m_backends.Add(InventoryProvider);

            MetricsProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/MetricsProvider", pluginConfig.GetString("metrics_provider")) as IMetricsProvider;
            m_backends.Add(MetricsProvider);

            try
            {
                InitHttpServer((uint) ConfigFile.Configs["Config"].GetInt("listen_port"));
            }
            catch (Exception ex)
            {
                m_log.Error("[ASSETINVENTORY] Initializing the HTTP server failed, shutting down: " + ex.Message);
                Shutdown();
                return false;
            }

            AuthenticationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthenticationProvider", pluginConfig.GetString("authentication_provider")) as IAuthenticationProvider;
            m_backends.Add(AuthenticationProvider);

            AuthorizationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthorizationProvider", pluginConfig.GetString("authorization_provider")) as IAuthorizationProvider;
            m_backends.Add(AuthorizationProvider);

            m_frontends.AddRange(LoadAssetInventoryServerPlugins("/OpenSim/AssetInventoryServer/Frontend", pluginConfig.GetString("frontends")));

            return true;
        }

        public void Work()
        {
            m_console.Notice("Enter help for a list of commands");

            while (true)
            {
                m_console.Prompt();
            }
        }

        public override void ShutdownSpecific()
        {
            foreach (IAssetInventoryServerPlugin plugin in m_frontends)
            {
                m_log.Debug("[ASSETINVENTORY] Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { m_log.ErrorFormat("[ASSETINVENTORY] Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            foreach (IAssetInventoryServerPlugin plugin in m_backends)
            {
                m_log.Debug("[ASSETINVENTORY] Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { m_log.ErrorFormat("[ASSETINVENTORY] Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            if (HttpServer != null)
                HttpServer.Stop();
        }

        void InitHttpServer(uint port)
        {
            m_httpServer = new BaseHttpServer(port);
            m_httpServer.Start();

            m_log.Info("[ASSETINVENTORY] AssetInventory server is listening on port " + port);
        }

        private IAssetInventoryServerPlugin LoadAssetInventoryServerPlugin(string addinPath, string provider)
        {
            PluginLoader<IAssetInventoryServerPlugin> loader = new PluginLoader<IAssetInventoryServerPlugin>(new AssetInventoryServerPluginInitialiser(this));

            if (provider == String.Empty)
                loader.Add(addinPath);
            else
                loader.Add(addinPath, new PluginIdFilter(provider));
            //loader.Add(addinPath, new PluginCountConstraint(1));

            loader.Load();

            return loader.Plugin;
        }

        private List<IAssetInventoryServerPlugin> LoadAssetInventoryServerPlugins(string addinPath, string provider)
        {
            PluginLoader<IAssetInventoryServerPlugin> loader = new PluginLoader<IAssetInventoryServerPlugin>(new AssetInventoryServerPluginInitialiser(this));

            if (provider == String.Empty)
                loader.Add(addinPath);
            else
                loader.Add(addinPath, new PluginIdFilter(provider));
            //loader.Add(addinPath, new PluginCountConstraint(1));

            loader.Load();

            return loader.Plugins;
        }
    }
}
