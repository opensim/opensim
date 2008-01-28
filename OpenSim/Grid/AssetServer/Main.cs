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
* 
*/

using System;
using System.IO;
using System.Reflection;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.AssetLoader.Filesystem;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;

namespace OpenSim.Grid.AssetServer
{
    /// <summary>
    /// An asset server
    /// </summary>
    public class OpenAsset_Main : conscmd_callback
    {
        public AssetConfig m_config;

        public static OpenAsset_Main assetserver;
        
        private LogBase m_console;        
        
        // Temporarily hardcoded - should be a plugin
        protected IAssetLoader assetLoader = new AssetLoaderFileSystem();        
        
        private IAssetProvider m_assetProvider;
        
        protected AssetStatsReporter m_stats;      

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...\n");

            assetserver = new OpenAsset_Main();
            assetserver.Startup();

            assetserver.Work();
        }

        private void Work()
        {
            m_console.Notice("Enter help for a list of commands");

            while (true)
            {
                m_console.MainLogPrompt();
            }
        }

        private OpenAsset_Main()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }
            m_console =
                new LogBase((Path.Combine(Util.logDir(), "opengrid-AssetServer-console.log")), "OpenAsset", this, true);
            MainLog.Instance = m_console;
        }

        public void Startup()
        {
            m_config = new AssetConfig("ASSET SERVER", (Path.Combine(Util.configDir(), "AssetServer_Config.xml")));

            m_console.Verbose("ASSET", "Setting up asset DB");
            setupDB(m_config);

            m_console.Verbose("ASSET", "Loading default asset set..");
            LoadDefaultAssets();

            m_console.Verbose("ASSET", "Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer(m_config.HttpPort);
            
            m_stats = new AssetStatsReporter();

            httpServer.AddStreamHandler(new GetAssetStreamHandler(this, m_assetProvider, m_stats));
            httpServer.AddStreamHandler(new PostAssetStreamHandler(this, m_assetProvider));

            httpServer.Start();
        }

        public byte[] GetAssetData(LLUUID assetID, bool isTexture)
        {
            return null;
        }

        public IAssetProvider LoadDatabasePlugin(string FileName)
        {
            MainLog.Instance.Verbose("ASSET SERVER", "LoadDatabasePlugin: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);
            IAssetProvider assetPlugin = null;
            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (!pluginType.IsAbstract)
                {
                    Type typeInterface = pluginType.GetInterface("IAssetProvider", true);

                    if (typeInterface != null)
                    {
                        IAssetProvider plug =
                            (IAssetProvider) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        assetPlugin = plug;
                        assetPlugin.Initialise();

                        MainLog.Instance.Verbose("ASSET SERVER", "Added " + assetPlugin.Name + " " + assetPlugin.Version);
                        break;
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
            return assetPlugin;
        }

        public void setupDB(AssetConfig config)
        {
            try
            {
                m_assetProvider = LoadDatabasePlugin(config.DatabaseProvider);
                if (m_assetProvider == null)
                {
                    MainLog.Instance.Error("ASSET", "Failed to load a database plugin, server halting");
                    Environment.Exit(-1);
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("ASSET", "setupDB() - Exception occured");
                MainLog.Instance.Warn("ASSET", e.ToString());
            }
        }

        public void LoadDefaultAssets()
        {
            assetLoader.ForEachDefaultXmlAsset(StoreAsset);            
        }

        protected void StoreAsset(AssetBase asset)
        {
            m_assetProvider.CreateAsset(asset);
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.Notice(
                        @"shutdown - shutdown this asset server (USE CAUTION!)
                 stats    - statistical information for this server");                    
                    
                    break;
                    
                case "stats":
                    MainLog.Instance.Notice("STATS", Environment.NewLine + m_stats.Report());
                    break;

                case "shutdown":
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        public void Show(string ShowWhat)
        {
        }
    }
}
