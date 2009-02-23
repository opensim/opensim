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
using System.Reflection;
using OpenSim.Framework;
using log4net;
using Nini.Config;

namespace OpenSim.Grid.AssetInventoryServer
{
    public static class AssetInventoryConfig
    {
        public const string CONFIG_FILE = "AssetInventoryServer.ini";
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static IConfigSource LoadConfig()
        {
            IConfigSource configSource = new IniConfigSource();
            configSource.AddConfig("Startup");
            return LoadConfig(configSource);
        }

        public static IConfigSource LoadConfig(IConfigSource source)
        {
            string iniFileName = source.Configs["Startup"].GetString("inifile", CONFIG_FILE);
            string iniFilePath = Path.Combine(Util.configDir(), iniFileName);

            source.Merge(DefaultConfig());

            if (!File.Exists(iniFilePath))
            {
                m_log.FatalFormat("[CONFIG]: File {0} not found, could not load any configuration.", iniFilePath);
                m_log.FatalFormat("[CONFIG]: Did you copy the AssetInventoryServer.ini.example file to AssetInventoryServer.ini?");
                Environment.Exit(1);
            }

            source.Merge(new IniConfigSource(iniFilePath));
            return source;
        }

        private static IConfigSource DefaultConfig()
        {
            IConfigSource result = new IniConfigSource();

            {
                IConfig config = result.AddConfig("Config");
                config.Set("listen_port", 8003);
                config.Set("assetset_location", String.Format(".{0}assets{0}AssetSets.xml", Path.DirectorySeparatorChar));
            }

            {
                IConfig config = result.AddConfig("Plugins");
                config.Set("asset_storage_provider", "OpenSimAssetStorage");
                config.Set("inventory_storage_provider", "OpenSimInventoryStorage");
                config.Set("authentication_provider", "NullAuthentication");
                config.Set("authorization_provider", "AuthorizeAll");
                config.Set("metrics_provider", "NullMetrics");
                config.Set("frontends", "ReferenceFrontend,OpenSimAssetFrontend,OpenSimInventoryFrontend,BrowseFrontend");
            }

            {
                IConfig config = result.AddConfig("OpenSim");
                config.Set("asset_database_provider", "OpenSim.Data.MySQL.dll");
                config.Set("inventory_database_provider", "OpenSim.Data.MySQL.dll");
                config.Set("asset_database_connect", String.Empty);
                config.Set("inventory_database_connect", String.Empty);
            }

            return result;
        }
    }
}
