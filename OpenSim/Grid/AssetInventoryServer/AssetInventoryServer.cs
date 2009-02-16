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
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Console;

namespace OpenSim.Grid.AssetInventoryServer
{
    public class AssetInventoryServer : BaseOpenSimServer
    {
        public const string CONFIG_FILE = "AssetInventoryServer.ini";

        public AssetInventoryConfig ConfigFile;

        public IAssetStorageProvider StorageProvider;
        public IInventoryStorageProvider InventoryProvider;
        public IAuthenticationProvider AuthenticationProvider;
        public IAuthorizationProvider AuthorizationProvider;
        public IMetricsProvider MetricsProvider;

        private List<IAssetInventoryServerPlugin> m_frontends = new List<IAssetInventoryServerPlugin>();
        private List<IAssetInventoryServerPlugin> m_backends = new List<IAssetInventoryServerPlugin>();

        public AssetInventoryServer()
        {
            m_console = new ConsoleBase("Asset");
            MainConsole.Instance = m_console;
        }

        public bool Start()
        {
            Logger.Log.Info("Starting Asset Server");
            uint port = 0;

            try { ConfigFile = new AssetInventoryConfig("AssetInventory Server", (Path.Combine(Util.configDir(), "AssetInventoryServer.ini"))); }
            catch (Exception)
            {
                Logger.Log.Error("Failed to load the config file " + CONFIG_FILE);
                return false;
            }

            StorageProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/StorageProvider",  ConfigFile.AssetStorageProvider) as IAssetStorageProvider;
            m_backends.Add(StorageProvider);

            InventoryProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/InventoryProvider", ConfigFile.InventoryStorageProvider) as IInventoryStorageProvider;
            m_backends.Add(InventoryProvider);

            MetricsProvider = LoadAssetInventoryServerPlugins("/OpenSim/AssetInventoryServer/MetricsProvider", ConfigFile.MetricsProvider) as IMetricsProvider;
            m_backends.Add(MetricsProvider);

            try
            {
                InitHttpServer(ConfigFile.HttpPort);
            }
            catch (Exception ex)
            {
                Logger.Log.Error("Initializing the HTTP server failed, shutting down: " + ex.Message);
                Shutdown();
                return false;
            }

            AuthenticationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthenticationProvider", ConfigFile.AuthenticationProvider) as IAuthenticationProvider;
            m_backends.Add(AuthenticationProvider);

            AuthorizationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthorizationProvider", ConfigFile.AuthorizationProvider) as IAuthorizationProvider;
            m_backends.Add(AuthorizationProvider);

            m_frontends.AddRange(LoadAssetInventoryServerPlugins("/OpenSim/AssetInventoryServer/Frontend", ConfigFile.Frontends));

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
                Logger.Log.Debug("Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { Logger.Log.ErrorFormat("Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            foreach (IAssetInventoryServerPlugin plugin in m_backends)
            {
                Logger.Log.Debug("Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { Logger.Log.ErrorFormat("Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            if (HttpServer != null)
                HttpServer.Stop();
        }

        void InitHttpServer(uint port)
        {
            m_httpServer = new BaseHttpServer(port);
            m_httpServer.Start();

            Logger.Log.Info("Asset server is listening on port " + port);
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
