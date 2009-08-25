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
    /// Message Server Config - Configuration of the Message Server
    /// </summary>
    public class MessageServerConfig:ConfigBase
    {
        public string DatabaseProvider = String.Empty;
        public string DatabaseConnect = String.Empty;
        public string GridCommsProvider = String.Empty;
        public string GridRecvKey = String.Empty;
        public string GridSendKey = String.Empty;
        public string GridServerURL = String.Empty;
        public uint HttpPort = ConfigSettings.DefaultMessageServerHttpPort;
        public bool HttpSSL = ConfigSettings.DefaultMessageServerHttpSSL;
        public string MessageServerIP = String.Empty;
        public string UserRecvKey = String.Empty;
        public string UserSendKey = String.Empty;
        public string UserServerURL = String.Empty;
        public string ConsoleUser = String.Empty;
        public string ConsolePass = String.Empty;

        public MessageServerConfig(string description, string filename)
        {
            m_configMember =
                new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration, true);
            m_configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            m_configMember.addConfigurationOption("default_user_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default User Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultUserServerHttpPort.ToString() + "/", false);
            m_configMember.addConfigurationOption("user_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to send to user server", "null", false);
            m_configMember.addConfigurationOption("user_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to expect from user server", "null", false);
            m_configMember.addConfigurationOption("default_grid_server",
                                                ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY,
                                                "Default Grid Server URI",
                                                "http://127.0.0.1:" + ConfigSettings.DefaultGridServerHttpPort.ToString() + "/", false);
            m_configMember.addConfigurationOption("grid_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to send to grid server", "null", false);
            m_configMember.addConfigurationOption("grid_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Key to expect from grid server", "null", false);

            m_configMember.addConfigurationOption("database_connect", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Connection String for Database", "", false);

            m_configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "DLL for database provider", "OpenSim.Data.MySQL.dll", false);

            m_configMember.addConfigurationOption("region_comms_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "DLL for comms provider", "OpenSim.Region.Communications.OGS1.dll", false);

            m_configMember.addConfigurationOption("http_port", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Http Listener port", ConfigSettings.DefaultMessageServerHttpPort.ToString(), false);
            m_configMember.addConfigurationOption("http_ssl", ConfigurationOption.ConfigurationTypes.TYPE_BOOLEAN,
                                                "Use SSL? true/false", ConfigSettings.DefaultMessageServerHttpSSL.ToString(), false);
            m_configMember.addConfigurationOption("published_ip", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "My Published IP Address", "127.0.0.1", false);
            m_configMember.addConfigurationOption("console_user", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Remote console access user name [Default: disabled]", "", false);

            m_configMember.addConfigurationOption("console_pass", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Remote console access password [Default: disabled]", "", false);

        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "default_user_server":
                    UserServerURL = (string) configuration_result;
                    break;
                case "user_send_key":
                    UserSendKey = (string) configuration_result;
                    break;
                case "user_recv_key":
                    UserRecvKey = (string) configuration_result;
                    break;
                case "default_grid_server":
                    GridServerURL = (string) configuration_result;
                    break;
                case "grid_send_key":
                    GridSendKey = (string) configuration_result;
                    break;
                case "grid_recv_key":
                    GridRecvKey = (string) configuration_result;
                    break;
                case "database_provider":
                    DatabaseProvider = (string) configuration_result;
                    break;
                case "database_connect":
                    DatabaseConnect = (string)configuration_result;
                    break;
                case "http_port":
                    HttpPort = (uint) configuration_result;
                    break;
                case "http_ssl":
                    HttpSSL = (bool) configuration_result;
                    break;
                case "region_comms_provider":
                    GridCommsProvider = (string) configuration_result;
                    break;
                case "published_ip":
                    MessageServerIP = (string) configuration_result;
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
