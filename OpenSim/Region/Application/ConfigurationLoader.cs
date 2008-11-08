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
using System.Text;
using System.Threading;
using System.IO;
using OpenSim.Framework;
using Nini;
using Nini.Config;

namespace OpenSim
{
    public class ConfigurationLoader
    {
        protected ConfigSettings m_configSettings;
        protected OpenSimConfigSource m_config;
        protected NetworkServersInfo m_networkServersInfo;

        public ConfigurationLoader()
        {
           
        }

        public OpenSimConfigSource LoadConfigSettings(IConfigSource configSource, out ConfigSettings configSettings, out NetworkServersInfo networkInfo)
        {
            m_configSettings = configSettings = new ConfigSettings();
            m_networkServersInfo = networkInfo = new NetworkServersInfo();
            bool iniFileExists = false;

            IConfig startupConfig = configSource.Configs["Startup"];

            string iniFileName = startupConfig.GetString("inifile", "OpenSim.ini");
            Application.iniFilePath = Path.Combine(Util.configDir(), iniFileName);

            string masterFileName = startupConfig.GetString("inimaster", "");
            string masterfilePath = Path.Combine(Util.configDir(), masterFileName);

            m_config = new OpenSimConfigSource();
            m_config.Source = new IniConfigSource();
            m_config.Source.Merge(DefaultConfig());

            //check for .INI file (either default or name passed in command line)
            if (File.Exists(masterfilePath))
            {
                m_config.Source.Merge(new IniConfigSource(masterfilePath));
            }

            if (File.Exists(Application.iniFilePath))
            {
                iniFileExists = true;

                // From reading Nini's code, it seems that later merged keys replace earlier ones.                
                m_config.Source.Merge(new IniConfigSource(Application.iniFilePath));
            }
            else
            {
                // check for a xml config file                
                Application.iniFilePath = Path.Combine(Util.configDir(), "OpenSim.xml");

                if (File.Exists(Application.iniFilePath))
                {
                    iniFileExists = true;

                    m_config.Source = new XmlConfigSource();
                    m_config.Source.Merge(new XmlConfigSource(Application.iniFilePath));
                }
            }

            m_config.Source.Merge(configSource);

            if (!iniFileExists)
                m_config.Save("OpenSim.ini");

            ReadConfigSettings();

            return m_config;
        }

        /// <summary>
        /// Setup a default config values in case they aren't present in the ini file
        /// </summary>
        /// <returns></returns>
        public static IConfigSource DefaultConfig()
        {
            IConfigSource defaultConfig = new IniConfigSource();

            {
                IConfig config = defaultConfig.Configs["Startup"];

                if (null == config)
                    config = defaultConfig.AddConfig("Startup");

                config.Set("gridmode", false);
                config.Set("physics", "basicphysics");
                config.Set("meshing", "ZeroMesher");
                config.Set("physical_prim", true);
                config.Set("see_into_this_sim_from_neighbor", true);
                config.Set("serverside_object_permissions", false);
                config.Set("storage_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("storage_connection_string", "URI=file:OpenSim.db,version=3");
                config.Set("storage_prim_inventories", true);
                config.Set("startup_console_commands_file", String.Empty);
                config.Set("shutdown_console_commands_file", String.Empty);
                config.Set("DefaultScriptEngine", "ScriptEngine.DotNetEngine");
                config.Set("asset_database", "sqlite");
                config.Set("clientstack_plugin", "OpenSim.Region.ClientStack.LindenUDP.dll");
            }

            {
                IConfig config = defaultConfig.Configs["StandAlone"];

                if (null == config)
                    config = defaultConfig.AddConfig("StandAlone");

                config.Set("accounts_authenticate", false);
                config.Set("welcome_message", "Welcome to OpenSimulator");
                config.Set("inventory_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("inventory_source", "");
                config.Set("userDatabase_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("user_source", "");
                config.Set("asset_plugin", "OpenSim.Data.SQLite.dll");
                config.Set("asset_source", "");
                config.Set("dump_assets_to_file", false);
            }

            {
                IConfig config = defaultConfig.Configs["Network"];

                if (null == config)
                    config = defaultConfig.AddConfig("Network");

                config.Set("default_location_x", 1000);
                config.Set("default_location_y", 1000);
                config.Set("http_listener_port", NetworkServersInfo.DefaultHttpListenerPort);
                config.Set("remoting_listener_port", NetworkServersInfo.RemotingListenerPort);
                config.Set("grid_server_url", "http://127.0.0.1:" + GridConfig.DefaultHttpPort.ToString());
                config.Set("grid_send_key", "null");
                config.Set("grid_recv_key", "null");
                config.Set("user_server_url", "http://127.0.0.1:" + UserConfig.DefaultHttpPort.ToString());
                config.Set("user_send_key", "null");
                config.Set("user_recv_key", "null");
                config.Set("asset_server_url", "http://127.0.0.1:" + AssetConfig.DefaultHttpPort.ToString());
                config.Set("inventory_server_url", "http://127.0.0.1:" + InventoryConfig.DefaultHttpPort.ToString());
                config.Set("secure_inventory_server", "true");
            }

            return defaultConfig;
        }

        protected virtual void ReadConfigSettings()
        {
            IConfig startupConfig = m_config.Source.Configs["Startup"];

            if (startupConfig != null)
            {
                m_configSettings.Standalone = !startupConfig.GetBoolean("gridmode");
                m_configSettings.PhysicsEngine = startupConfig.GetString("physics");
                m_configSettings.MeshEngineName = startupConfig.GetString("meshing");

                m_configSettings.PhysicalPrim = startupConfig.GetBoolean("physical_prim");

                m_configSettings.See_into_region_from_neighbor = startupConfig.GetBoolean("see_into_this_sim_from_neighbor");

                m_configSettings.StorageDll = startupConfig.GetString("storage_plugin");
                if (m_configSettings.StorageDll == "OpenSim.DataStore.MonoSqlite.dll")
                {
                    m_configSettings.StorageDll = "OpenSim.Data.SQLite.dll";
                    Console.WriteLine("WARNING: OpenSim.DataStore.MonoSqlite.dll is deprecated. Set storage_plugin to OpenSim.Data.SQLite.dll.");
                    Thread.Sleep(3000);
                }

                m_configSettings.StorageConnectionString = startupConfig.GetString("storage_connection_string");
                m_configSettings.EstateConnectionString = startupConfig.GetString("estate_connection_string", m_configSettings.StorageConnectionString);
                m_configSettings.AssetStorage = startupConfig.GetString("asset_database");
                m_configSettings.ClientstackDll = startupConfig.GetString("clientstack_plugin");
            }

            IConfig standaloneConfig = m_config.Source.Configs["StandAlone"];
            if (standaloneConfig != null)
            {
                m_configSettings.StandaloneAuthenticate = standaloneConfig.GetBoolean("accounts_authenticate");
                m_configSettings.StandaloneWelcomeMessage = standaloneConfig.GetString("welcome_message");

                m_configSettings.StandaloneInventoryPlugin = standaloneConfig.GetString("inventory_plugin");
                m_configSettings.StandaloneInventorySource = standaloneConfig.GetString("inventory_source");
                m_configSettings.StandaloneUserPlugin = standaloneConfig.GetString("userDatabase_plugin");
                m_configSettings.StandaloneUserSource = standaloneConfig.GetString("user_source");
                m_configSettings.StandaloneAssetPlugin = standaloneConfig.GetString("asset_plugin");
                m_configSettings.StandaloneAssetSource = standaloneConfig.GetString("asset_source");

                m_configSettings.DumpAssetsToFile = standaloneConfig.GetBoolean("dump_assets_to_file");
            }

            m_networkServersInfo.loadFromConfiguration(m_config.Source);
        }
    }
}
