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
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using ExtensionLoader;
using ExtensionLoader.Config;
using HttpServer;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Grid.AssetInventoryServer
{
    public class AssetInventoryServer : ServiceBase
    {
        public const string CONFIG_FILE = "AssetInventoryServer.ini";

        public WebServer HttpServer;
        public IniConfigSource ConfigFile;

        public IAssetStorageProvider StorageProvider;
        public IInventoryStorageProvider InventoryProvider;
        public IAuthenticationProvider AuthenticationProvider;
        public IAuthorizationProvider AuthorizationProvider;
        public IMetricsProvider MetricsProvider;

        private List<IAssetInventoryServerPlugin> frontends = new List<IAssetInventoryServerPlugin>();
        private List<IAssetInventoryServerPlugin> backends = new List<IAssetInventoryServerPlugin>();

        public AssetInventoryServer()
        {
            this.ServiceName = "OpenSimAssetInventoryServer";
        }

        public bool Start()
        {
            Logger.Log.Info("Starting Asset Server");
            List<string> extensionList = null;
            int port = 0;
            X509Certificate2 serverCert = null;

            try { ConfigFile = new IniConfigSource(CONFIG_FILE); }
            catch (Exception)
            {
                Logger.Log.Error("Failed to load the config file " + CONFIG_FILE);
                return false;
            }

            try
            {
                IConfig extensionConfig = ConfigFile.Configs["Config"];

                // Load the port number to listen on
                port = extensionConfig.GetInt("ListenPort");

                // Load the server certificate file
                string certFile = extensionConfig.GetString("SSLCertFile");
                if (!String.IsNullOrEmpty(certFile))
                    serverCert = new X509Certificate2(certFile);
            }
            catch (Exception)
            {
                Logger.Log.Error("Failed to load [Config] section from " + CONFIG_FILE);
                return false;
            }

            try
            {
                // Load the extension list (and ordering) from our config file
                IConfig extensionConfig = ConfigFile.Configs["Extensions"];
                extensionList = new List<string>(extensionConfig.GetKeys());
            }
            catch (Exception)
            {
                Logger.Log.Error("Failed to load [Extensions] section from " + CONFIG_FILE);
                return false;
            }

            StorageProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/StorageProvider",  "OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim.dll") as IAssetStorageProvider;
            backends.Add(StorageProvider);

            InventoryProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/InventoryProvider",  "OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim.dll") as IInventoryStorageProvider;
            backends.Add(InventoryProvider);

            MetricsProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/MetricsProvider", String.Empty) as IMetricsProvider;
            backends.Add(MetricsProvider);

            try
            {
                InitHttpServer(port, serverCert);
            }
            catch (Exception ex)
            {
                Logger.Log.Error("Initializing the HTTP server failed, shutting down: " + ex.Message);
                Stop();
                return false;
            }

            frontends.AddRange(LoadAssetInventoryServerPlugins("/OpenSim/AssetInventoryServer/Frontend", String.Empty));

            AuthenticationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthenticationProvider", String.Empty) as IAuthenticationProvider;
            backends.Add(AuthenticationProvider);

            AuthorizationProvider = LoadAssetInventoryServerPlugin("/OpenSim/AssetInventoryServer/AuthorizationProvider", String.Empty) as IAuthorizationProvider;
            backends.Add(AuthorizationProvider);

            return true;
        }

        public void Shutdown()
        {
            foreach (IAssetInventoryServerPlugin plugin in frontends)
            {
                Logger.Log.Debug("Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { Logger.Log.ErrorFormat("Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            foreach (IAssetInventoryServerPlugin plugin in backends)
            {
                Logger.Log.Debug("Disposing plugin " + plugin.Name);
                try { plugin.Dispose(); }
                catch (Exception ex)
                { Logger.Log.ErrorFormat("Failure shutting down plugin {0}: {1}", plugin.Name, ex.Message); }
            }

            if (HttpServer != null)
                HttpServer.Stop();
        }

        void InitHttpServer(int port, X509Certificate serverCert)
        {
            if (serverCert != null)
                HttpServer = new WebServer(IPAddress.Any, port, serverCert, null, false);
            else
                HttpServer = new WebServer(IPAddress.Any, port);

            HttpServer.LogWriter = new log4netLogWriter(Logger.Log);

            HttpServer.Set404Handler(
                delegate(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
                {
                    Logger.Log.Warn("Requested page was not found: " + request.Uri.PathAndQuery);

                    string notFoundString = "<html><head><title>Page Not Found</title></head><body>The requested page or method was not found</body></html>";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(notFoundString);
                    response.Body.Write(buffer, 0, buffer.Length);
                    response.Status = HttpStatusCode.NotFound;
                    return true;
                }
            );

            HttpServer.Start();

            Logger.Log.Info("Asset server is listening on port " + port);
        }

        #region ServiceBase Overrides

        protected override void OnStart(string[] args)
        {
            Start();
        }
        protected override void OnStop()
        {
            Shutdown();
        }

        #endregion

        private IAssetInventoryServerPlugin LoadAssetInventoryServerPlugin(string addinPath, string provider)
        {
            PluginLoader<IAssetInventoryServerPlugin> loader = new PluginLoader<IAssetInventoryServerPlugin>(new AssetInventoryServerPluginInitialiser(this));

            if (provider == String.Empty)
                loader.Add(addinPath);
            else
                loader.Add(addinPath, new PluginProviderFilter(provider));
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
                loader.Add(addinPath, new PluginProviderFilter(provider));
            //loader.Add(addinPath, new PluginCountConstraint(1));

            loader.Load();

            return loader.Plugins;
        }
    }

    public class log4netLogWriter : ILogWriter
    {
        ILog Log;

        public log4netLogWriter(ILog log)
        {
            Log = log;
        }

        public void Write(object source, LogPrio prio, string message)
        {
            switch (prio)
            {
                case LogPrio.Trace:
                case LogPrio.Debug:
                    Log.DebugFormat("{0}: {1}", source, message);
                    break;
                case LogPrio.Info:
                    Log.InfoFormat("{0}: {1}", source, message);
                    break;
                case LogPrio.Warning:
                    Log.WarnFormat("{0}: {1}", source, message);
                    break;
                case LogPrio.Error:
                    Log.ErrorFormat("{0}: {1}", source, message);
                    break;
                case LogPrio.Fatal:
                    Log.FatalFormat("{0}: {1}", source, message);
                    break;
            }
        }
    }
}
