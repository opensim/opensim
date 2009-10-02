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
using System.IO;

namespace OpenSim.Framework
{
    /// <summary>
    /// UserConfig -- For User Server Configuration
    /// </summary>
    public class UserConfig:ConfigBase
    {
        public string DatabaseProvider = String.Empty;
        public string DatabaseConnect = String.Empty;
        public string DefaultStartupMsg = String.Empty;
        public uint DefaultX = 1000;
        public uint DefaultY = 1000;
        public string GridRecvKey = String.Empty;
        public string GridSendKey = String.Empty;
        public uint HttpPort = ConfigSettings.DefaultUserServerHttpPort;
        public bool HttpSSL = ConfigSettings.DefaultUserServerHttpSSL;
        public uint DefaultUserLevel = 0;
        public string LibraryXmlfile = "";
        public string ConsoleUser = String.Empty;
        public string ConsolePass = String.Empty;

        private Uri m_inventoryUrl;

        public Uri InventoryUrl
        {
            get
            {
                return m_inventoryUrl;
            }
            set
            {
                m_inventoryUrl = value;
            }
        }

        private Uri m_authUrl;
        public Uri AuthUrl
        {
            get
            {
                return m_authUrl;
            }
            set
            {
                m_authUrl = value;
            }
        }

        private Uri m_gridServerURL;

        public Uri GridServerURL
        {
            get
            {
                return m_gridServerURL;
            }
            set
            {
                m_gridServerURL = value;
            }
        }

        public bool EnableLLSDLogin = true;

        public bool EnableHGLogin = true;

        public UserConfig()
        {
            // weird, but UserManagerBase needs this.
        }
        public UserConfig(string description, string filename)
        {
            m_configMember =
                new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration, true);
            m_configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            m_configMember.addConfigurationOption("default_startup_message",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default Startup Message", "Welcome to OGS", false);

            m_configMember.addConfigurationOption("default_grid_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default Grid Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultGridServerHttpPort + "/", false);
            m_configMember.addConfigurationOption("grid_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to send to grid server", "null", false);
            m_configMember.addConfigurationOption("grid_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to expect from grid server", "null", false);

            m_configMember.addConfigurationOption("default_inventory_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default Inventory Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultInventoryServerHttpPort + "/",
                                                false);
            m_configMember.addConfigurationOption("default_authentication_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "User Server (this) External URI for authentication keys",
                                                "http://localhost:" + ConfigSettings.DefaultUserServerHttpPort + "/",
                                                false);
            m_configMember.addConfigurationOption("library_location",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Path to library control file",
                                                string.Format(".{0}inventory{0}Libraries.xml", Path.DirectorySeparatorChar), false);
            
            m_configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "DLL for database provider", "OpenSim.Data.MySQL.dll", false);
            m_configMember.addConfigurationOption("database_connect", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Connection String for Database", "", false);

            m_configMember.addConfigurationOption("http_port", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Http Listener port", ConfigSettings.DefaultUserServerHttpPort.ToString(), false);
            m_configMember.addConfigurationOption("http_ssl", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Use SSL? true/false", ConfigSettings.DefaultUserServerHttpSSL.ToString(), false);
            m_configMember.addConfigurationOption("default_X", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Known good region X", "1000", false);
            m_configMember.addConfigurationOption("default_Y", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Known good region Y", "1000", false);
            m_configMember.addConfigurationOption("enable_llsd_login", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                    "Enable LLSD login support [Currently used by libsl based clients/bots]? true/false", true.ToString(), false);

            m_configMember.addConfigurationOption("enable_hg_login", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                    "Enable Hypergrid login support [Currently used by GridSurfer-proxied clients]? true/false", true.ToString(), false);

            m_configMember.addConfigurationOption("default_loginLevel", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Minimum Level a user should have to login [0 default]", "0", false);
            
            m_configMember.addConfigurationOption("console_user", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Remote console access user name [Default: disabled]", "", false);
            
            m_configMember.addConfigurationOption("console_pass", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Remote console access password [Default: disabled]", "", false);
            
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "default_startup_message":
                    DefaultStartupMsg = (string) configuration_result;
                    break;
                case "default_grid_server":
                    GridServerURL = new Uri((string) configuration_result);
                    break;
                case "grid_send_key":
                    GridSendKey = (string) configuration_result;
                    break;
                case "grid_recv_key":
                    GridRecvKey = (string) configuration_result;
                    break;
                case "default_inventory_server":
                    InventoryUrl = new Uri((string) configuration_result);
                    break;
                case "default_authentication_server":
                    AuthUrl = new Uri((string)configuration_result);
                    break;
                case "database_provider":
                    DatabaseProvider = (string) configuration_result;
                    break;
                case "database_connect":
                    DatabaseConnect = (string) configuration_result;
                    break;
                case "http_port":
                    HttpPort = (uint) configuration_result;
                    break;
                case "http_ssl":
                    HttpSSL = (bool) configuration_result;
                    break;
                case "default_X":
                    DefaultX = (uint) configuration_result;
                    break;
                case "default_Y":
                    DefaultY = (uint) configuration_result;
                    break;
                case "enable_llsd_login":
                    EnableLLSDLogin = (bool)configuration_result;
                    break;
                case "enable_hg_login":
                    EnableHGLogin = (bool)configuration_result;
                    break;
                case "default_loginLevel":
                    DefaultUserLevel = (uint)configuration_result;
                    break;
                case "library_location":
                    LibraryXmlfile = (string)configuration_result;
                    break;
                case "console_user":
                    ConsoleUser = (string)configuration_result;
                    break;
                case "console_pass":
                    ConsolePass = (string)configuration_result;
                    break;
            }

            return true;
        }
    }
}
