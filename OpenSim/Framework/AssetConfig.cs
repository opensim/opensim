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
    /// AssetConfig -- For Asset Server Configuration
    /// </summary>
    public class AssetConfig:ConfigBase
    {
        public string DatabaseConnect = String.Empty;
        public string DatabaseProvider = String.Empty;
        public uint HttpPort = ConfigSettings.DefaultAssetServerHttpPort;
        public string AssetSetsLocation = string.Empty;

        public AssetConfig(string description, string filename)
        {
            m_configMember =
                new ConfigurationMember(filename, description, loadConfigurationOptions, handleIncomingConfiguration, true);
            m_configMember.performConfigurationRetrieve();
        }

        public void loadConfigurationOptions()
        {
            m_configMember.addConfigurationOption("database_provider", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "DLL for database provider", "OpenSim.Data.MySQL.dll", false);

            m_configMember.addConfigurationOption("database_connect", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                                "Database connection string", "", false);

            m_configMember.addConfigurationOption("http_port", ConfigurationOption.ConfigurationTypes.TYPE_UINT32,
                                                "Http Listener port", ConfigSettings.DefaultAssetServerHttpPort.ToString(), false);

            m_configMember.addConfigurationOption("assetset_location", ConfigurationOption.ConfigurationTypes.TYPE_STRING,
                                        "Location of 'AssetSets.xml'",
                                        string.Format(".{0}assets{0}AssetSets.xml", Path.DirectorySeparatorChar), false);
        }

        public bool handleIncomingConfiguration(string configuration_key, object configuration_result)
        {
            switch (configuration_key)
            {
                case "database_provider":
                    DatabaseProvider = (string) configuration_result;
                    break;
                case "database_connect":
                    DatabaseConnect = (string) configuration_result;
                    break;
                case "assetset_location":
                    AssetSetsLocation = (string) configuration_result;
                    break;
                case "http_port":
                    HttpPort = (uint) configuration_result;
                    break;
            }

            return true;
        }
    }
}
