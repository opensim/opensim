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
    public class GridConfig:ConfigBase
    {
        public string AllowForcefulBanlines = "TRUE";
        public bool AllowRegionRegistration = true;
        public string AssetRecvKey = String.Empty;
        public string AssetSendKey = String.Empty;

        public string DatabaseProvider = String.Empty;
        public string DatabaseConnect = String.Empty;
        public string DefaultAssetServer = String.Empty;
        public string DefaultUserServer = String.Empty;
        public uint HttpPort = ConfigSettings.DefaultGridServerHttpPort;
        public string SimRecvKey = String.Empty;
        public string SimSendKey = String.Empty;
        public string UserRecvKey = String.Empty;
        public string UserSendKey = String.Empty;
        public string ConsoleUser = String.Empty;
        public string ConsolePass = String.Empty;

        public GridConfig(string description, string filename)
        {
            m_configMember =
                new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration, true);
            m_configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            m_configMember.addConfigurationOption("default_asset_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default Asset Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultAssetServerHttpPort.ToString() + "/",
                                                false);
            m_configMember.addConfigurationOption("asset_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to send to asset server", "null", false);
            m_configMember.addConfigurationOption("asset_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to expect from asset server", "null", false);

            m_configMember.addConfigurationOption("default_user_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default User Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultUserServerHttpPort.ToString() + "/", false);
            m_configMember.addConfigurationOption("user_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to send to user server", "null", false);
            m_configMember.addConfigurationOption("user_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to expect from user server", "null", false);

            m_configMember.addConfigurationOption("sim_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to send to a simulator", "null", false);
            m_configMember.addConfigurationOption("sim_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to expect from a simulator", "null", false);
            m_configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "DLL for database provider", "OpenSim.Data.MySQL.dll", false);
            m_configMember.addConfigurationOption("database_connect", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Database connect string", "", false);

            m_configMember.addConfigurationOption("http_port", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Http Listener port", ConfigSettings.DefaultGridServerHttpPort.ToString(), false);

            m_configMember.addConfigurationOption("allow_forceful_banlines",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Allow Forceful Banlines", "TRUE", true);
            
            m_configMember.addConfigurationOption("allow_region_registration", 
                                                ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Allow regions to register immediately upon grid server startup? true/false", 
                                                "True", 
                                                false);
            m_configMember.addConfigurationOption("console_user", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                 "Remote console access user name [Default: disabled]", "", false);

            m_configMember.addConfigurationOption("console_pass", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                 "Remote console access password [Default: disabled]", "", false);

        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "default_asset_server":
                    DefaultAssetServer = (string) configuration_result;
                    break;
                case "asset_send_key":
                    AssetSendKey = (string) configuration_result;
                    break;
                case "asset_recv_key":
                    AssetRecvKey = (string) configuration_result;
                    break;
                case "default_user_server":
                    DefaultUserServer = (string) configuration_result;
                    break;
                case "user_send_key":
                    UserSendKey = (string) configuration_result;
                    break;
                case "user_recv_key":
                    UserRecvKey = (string) configuration_result;
                    break;
                case "sim_send_key":
                    SimSendKey = (string) configuration_result;
                    break;
                case "sim_recv_key":
                    SimRecvKey = (string) configuration_result;
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
                case "allow_forceful_banlines":
                    AllowForcefulBanlines = (string) configuration_result;
                    break;
                case "allow_region_registration":
                    AllowRegionRegistration = (bool)configuration_result;
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
