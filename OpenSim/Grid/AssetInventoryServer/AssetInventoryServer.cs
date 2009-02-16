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
        public IInventoryProvider InventoryProvider;
        public IAuthenticationProvider AuthenticationProvider;
        public IAuthorizationProvider AuthorizationProvider;
        public IMetricsProvider MetricsProvider;

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

            //try
            //{
            //    // Create a reference list for C# extensions compiled at runtime
            //    List<string> references = new List<string>();
            //    references.Add("OpenMetaverseTypes.dll");
            //    references.Add("OpenMetaverse.dll");
            //    references.Add("OpenMetaverse.StructuredData.dll");
            //    references.Add("OpenMetaverse.Http.dll");
            //    references.Add("ExtensionLoader.dll");
            //    references.Add("AssetServer.exe");

            //    // Get a list of all of the members of AssetServer that are interfaces
            //    List<FieldInfo> assignables = ExtensionLoader<AssetServer>.GetInterfaces(this);

            //    // Load all of the extensions
            //    ExtensionLoader<AssetServer>.LoadAllExtensions(
            //        Assembly.GetExecutingAssembly(),
            //        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            //        extensionList,
            //        references,
            //        "AssetServer.*.dll",
            //        "AssetServer.*.cs",
            //        this,
            //        assignables);
            //}
            //catch (ExtensionException ex)
            //{
            //    Logger.Log.Error("Interface loading failed, shutting down: " + ex.Message);
            //    if (ex.InnerException != null)
            //        Logger.Log.Error(ex.InnerException.Message, ex.InnerException);
            //    Stop();
            //    return false;
            //}

            StorageProvider = LoadAssetInventoryServerPlugin() as IAssetStorageProvider;

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

            // Start all of the extensions
            //foreach (IExtension<AssetServer> extension in ExtensionLoader<AssetServer>.Extensions)
            //{
            //    Logger.Log.Info("Starting extension " + extension.GetType().Name);
            //    extension.Start(this);
            //}

            return true;
        }

        public void Shutdown()
        {
            foreach (IExtension<AssetInventoryServer> extension in ExtensionLoader<AssetInventoryServer>.Extensions)
            {
                Logger.Log.Debug("Disposing extension " + extension.GetType().Name);
                try { extension.Stop(); }
                catch (Exception ex)
                { Logger.Log.ErrorFormat("Failure shutting down extension {0}: {1}", extension.GetType().Name, ex.Message); }
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

        private IAssetInventoryServerPlugin LoadAssetInventoryServerPlugin()
        {
            PluginLoader<IAssetInventoryServerPlugin> loader = new PluginLoader<IAssetInventoryServerPlugin>(new AssetInventoryServerPluginInitialiser(this));

            //loader.Add ("/OpenSim/AssetInventoryServer/StorageProvider", new PluginProviderFilter (provider));
            //loader.Add("/OpenSim/AssetInventoryServer/StorageProvider", new PluginCountConstraint(1));
            loader.Add("/OpenSim/AssetInventoryServer/StorageProvider");
            loader.Load();

            return loader.Plugin;
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
