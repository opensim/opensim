using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Configuration
{
    /// <summary>
    /// UserConfig -- For User Server Configuration
    /// </summary>
    public class UserConfig
    {
        public string DefaultStartupMsg = "";
        public string GridServerURL = "";
        public string GridSendKey = "";
        public string GridRecvKey = "";

        public string DatabaseProvider = "";

        private ConfigurationMember configMember;

        public UserConfig(string description, string filename)
        {
            configMember = new ConfigurationMember(filename, description, this.loadConfigurationOptions, this.handleIncomingConfiguration);
            configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("default_startup_message", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Default Startup Message", "Welcome to OGS",false);

            configMember.addConfigurationOption("default_grid_server", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Default Grid Server URI", "http://127.0.0.1:8001/", false);
            configMember.addConfigurationOption("grid_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to send to grid server", "null", false);
            configMember.addConfigurationOption("grid_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to expect from grid server", "null", false);
            configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "DLL for database provider", "OpenSim.Framework.Data.MySQL.dll", false);

        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "default_startup_message":
                    this.DefaultStartupMsg = (string)configuration_result;
                    break;
                case "default_grid_server":
                    this.GridServerURL = (string)configuration_result;
                    break;
                case "grid_send_key":
                    this.GridSendKey = (string)configuration_result;
                    break;
                case "grid_recv_key":
                    this.GridRecvKey = (string)configuration_result;
                    break;
                case "database_provider":
                    this.DatabaseProvider = (string)configuration_result;
                    break;
            }

            return true;
        }
    }
}
