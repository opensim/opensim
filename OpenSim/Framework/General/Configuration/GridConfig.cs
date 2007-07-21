using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Configuration
{
    public class GridConfig
    {
        public string GridOwner = "";
        public string DefaultAssetServer = "";
        public string AssetSendKey = "";
        public string AssetRecvKey = "";

        public string DefaultUserServer = "";
        public string UserSendKey = "";
        public string UserRecvKey = "";

        public string SimSendKey = "";
        public string SimRecvKey = "";

        public string DatabaseProvider = "";
        
        private ConfigurationMember configMember;
        public GridConfig(string description, string filename)
        {
            configMember = new ConfigurationMember(filename, description, this.loadConfigurationOptions, this.handleIncomingConfiguration);
            configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("grid_owner", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "OGS Grid Owner", "OGS development team", false);
            configMember.addConfigurationOption("default_asset_server", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Default Asset Server URI", "http://127.0.0.1:8003/", false);
            configMember.addConfigurationOption("asset_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to send to asset server", "null", false);
            configMember.addConfigurationOption("asset_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to expect from asset server", "null", false);

            configMember.addConfigurationOption("default_user_server", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "Default User Server URI", "http://127.0.0.1:8002/", false);
            configMember.addConfigurationOption("user_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to send to user server", "null", false);
            configMember.addConfigurationOption("user_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to expect from user server", "null", false);

            configMember.addConfigurationOption("sim_send_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to send to a simulator", "null", false);
            configMember.addConfigurationOption("sim_recv_key", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "Key to expect from a simulator", "null", false);
            configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING, "DLL for database provider", "OpenSim.Framework.Data.MySQL.dll", false);
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "grid_owner":
                    this.GridOwner = (string)configuration_result;
                    break;
                case "default_asset_server":
                    this.DefaultAssetServer = (string)configuration_result;
                    break;
                case "asset_send_key":
                    this.AssetSendKey = (string)configuration_result;
                    break;
                case "asset_recv_key":
                    this.AssetRecvKey = (string)configuration_result;
                    break;
                case "default_user_server":
                    this.DefaultUserServer = (string)configuration_result;
                    break;
                case "user_send_key":
                    this.UserSendKey = (string)configuration_result;
                    break;
                case "user_recv_key":
                    this.UserRecvKey = (string)configuration_result;
                    break;
                case "sim_send_key":
                    this.SimSendKey = (string)configuration_result;
                    break;
                case "sim_recv_key":
                    this.SimRecvKey = (string)configuration_result;
                    break;
                case "database_provider":
                    this.DatabaseProvider = (string)configuration_result;
                    break;
            }

            return true;
        }
    }
}
