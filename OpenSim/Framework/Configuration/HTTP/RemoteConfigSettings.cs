using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Configuration;

namespace OpenSim.Framework.Configuration.HTTP
{
    public class RemoteConfigSettings
    {
        private ConfigurationMember configMember;

        public string baseConfigURL = "";
        public RemoteConfigSettings(string filename)
        {
            configMember = new ConfigurationMember(filename, "REMOTE CONFIG SETTINGS", loadConfigurationOptions, handleIncomingConfiguration);
            configMember.forceConfigurationPluginLibrary("OpenSim.Framework.Configuration.XML.dll");
            configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            configMember.addConfigurationOption("base_config_url", ConfigurationOption.ConfigurationTypes.TYPE_STRING_NOT_EMPTY, "URL Containing Configuration Files", "http://localhost/", false);
        }
        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            if (configuration_key == "base_config_url")
            {
                baseConfigURL = (string)configuration_result;
            }
            return true;
        }
    }
}
