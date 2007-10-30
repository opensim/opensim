using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// UserConfig -- For User Server Configuration
    /// </summary>
    public class InventoryConfig
    {
        public string DefaultStartupMsg = "";
        public string UserServerURL = "";
        public string UserSendKey = "";
        public string UserRecvKey = "";

        public string DatabaseProvider = "";
        public static uint DefaultHttpPort = 8004;

        public int HttpPort = 8004;

        private ConfigurationMember configMember;

        public InventoryConfig(string description, string filename)
        {
            configMember = new ConfigurationMember(filename, description, this.loadConfigurationOptions, this.handleIncomingConfiguration);
            configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("default_startup_message", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Default Startup Message", "Welcome to OGS", false);
            configMember.addConfigurationOption("default_user_server", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Default User Server URI", "http://127.0.0.1:" + UserConfig.DefaultHttpPort.ToString(), false);
            configMember.addConfigurationOption("user_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to send to user server", "null", false);
            configMember.addConfigurationOption("user_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to expect from user server", "null", false);
            configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "DLL for database provider", "OpenSim.Framework.Data.MySQL.dll", false);
            configMember.addConfigurationOption("http_port", ConfigurationOption.ConfigurationTypes.TYPE_INT32, "Http Listener port", DefaultHttpPort.ToString(), false);
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "default_startup_message":
                    this.DefaultStartupMsg = (string)configuration_result;
                    break;
                case "default_user_server":
                    this.UserServerURL = (string)configuration_result;
                    break;
                case "user_send_key":
                    this.UserSendKey = (string)configuration_result;
                    break;
                case "user_recv_key":
                    this.UserRecvKey = (string)configuration_result;
                    break;
                case "database_provider":
                    this.DatabaseProvider = (string)configuration_result;
                    break;
                case "http_port":
                    HttpPort = (int)configuration_result;
                    break;
            }

            return true;
        }
    }
}
