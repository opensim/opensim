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

namespace OpenSim.Framework
{
    /// <summary>
    /// Defines and handles inventory grid server configuration
    /// </summary>
    public class InventoryConfig:ConfigBase
    {
        public string DatabaseConnect = String.Empty;
        public string DatabaseProvider = String.Empty;
        public string DefaultStartupMsg = String.Empty;
        public uint HttpPort = ConfigSettings.DefaultInventoryServerHttpPort;
        public string InventoryServerURL = String.Empty;
        public string UserServerURL = String.Empty;
        public string AssetServerURL = String.Empty;
        public bool SessionLookUp = true;
        public bool RegionAccessToAgentsInventory = true;

        public InventoryConfig(string description, string filename)
        {
            m_configMember =
                new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration, true);
            m_configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            m_configMember.addConfigurationOption("default_inventory_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default Inventory Server URI (this server's external name)",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultInventoryServerHttpPort, false);
            m_configMember.addConfigurationOption("default_user_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default User Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultUserServerHttpPort, false);
            m_configMember.addConfigurationOption("default_asset_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default Asset Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultAssetServerHttpPort, false);
            m_configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "DLL for database provider", "OpenSim.Data.MySQL.dll", false);
            m_configMember.addConfigurationOption("database_connect", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Database Connect String", "", false);
            m_configMember.addConfigurationOption("http_port", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Http Listener port", ConfigSettings.DefaultInventoryServerHttpPort.ToString(), false);
            m_configMember.addConfigurationOption("session_lookup", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Enable session lookup security", "False", false);
            m_configMember.addConfigurationOption("region_access", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Allow direct region access to users inventories? (Keep True if you don't know what this is about)", "True", false);
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "default_inventory_server":
                    InventoryServerURL = (string)configuration_result;
                    break;
                case "default_user_server":
                    UserServerURL = (string) configuration_result;
                    break;
                case "default_asset_server":
                    AssetServerURL = (string)configuration_result;
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
                case "session_lookup":
                    SessionLookUp = (bool)configuration_result;
                    break;
                case "region_access":
                    RegionAccessToAgentsInventory = (bool)configuration_result;
                    break;
            }

            return true;
        }
    }
}
