using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Configuration
{
    /// <summary>
    /// UserConfig -- For User Server Configuration
    /// </summary>
    public class AssetConfig
    {
        public string DefaultStartupMsg = "";

        public string DatabaseProvider = "";

        public uint HttpPort = 8003;

        private ConfigurationMember configMember;

        public AssetConfig(string description, string filename)
        {
            configMember = new ConfigurationMember(filename, description, this.loadConfigurationOptions, this.handleIncomingConfiguration);
            configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("default_startup_message", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Default Startup Message", "Welcome to OGS", false);

            configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "DLL for database provider", "OpenSim.Framework.Data.MySQL.dll", false);

            configMember.addConfigurationOption("http_port", ConfigurationOption.ConfigurationTypes.TYPE_UINT32, "Http Listener port", "8003", false);

        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "default_startup_message":
                    this.DefaultStartupMsg = (string)configuration_result;
                    break;
                case "database_provider":
                    this.DatabaseProvider = (string)configuration_result;
                    break;
                case "http_port":
                    HttpPort = (uint)configuration_result;
                    break;
            }

            return true;
        }
    }
}
