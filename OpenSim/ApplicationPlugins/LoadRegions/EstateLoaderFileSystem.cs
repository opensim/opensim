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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.ApplicationPlugins.LoadRegions
{
    public class EstateLoaderFileSystem : IEstateLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IConfigSource m_configSource;

        private OpenSimBase m_application;

        public EstateLoaderFileSystem(OpenSimBase openSim)
        {
            m_application = openSim;
        }

        public void SetIniConfigSource(IConfigSource configSource)
        {
            m_configSource = configSource;
        }

        public void LoadEstates()
        {
            string estateConfigPath = Path.Combine(Util.configDir(), "Estates");

            try
            {
                IConfig startupConfig = m_configSource.Configs["Startup"];
                estateConfigPath = startupConfig.GetString("regionload_estatesdir", estateConfigPath).Trim();
            }
            catch (Exception)
            {
                // No INI setting recorded.
            }

            if (Directory.Exists(estateConfigPath) == false)
            {
                Directory.CreateDirectory(estateConfigPath);
            }

            string[] iniFiles = Directory.GetFiles(estateConfigPath, "*.ini");

            // No Estate.ini Found
            if (iniFiles.Length == 0)
                return;

            m_log.InfoFormat("[ESTATE LOADER FILE SYSTEM]: Loading estate config files from {0}", estateConfigPath);

            List<int> existingEstates;

            int i = 0;
            foreach (string file in iniFiles)
            {
                m_log.InfoFormat("[ESTATE LOADER FILE SYSTEM]: Loading config file {0}", file);

                IConfigSource source = new IniConfigSource(file);

                foreach (IConfig config in source.Configs)
                {
                    // Read Estate Config From Source File
                    string estateName = config.Name;
                    string ownerString = config.GetString("Owner", string.Empty);

                    if (UUID.TryParse(ownerString, out UUID estateOwner) == false)
                        estateOwner = UUID.Zero;

                    // Check Name Is Valid
                    if (estateName == string.Empty || estateOwner == UUID.Zero)
                        continue;

                    // Check If Estate Exists (Skip If So)
                    existingEstates = m_application.EstateDataService.GetEstates(estateName);

                    if (existingEstates.Count > 0)
                        continue;

                    //### Should check Estate Owner ID but no Scene object available at this point

                    // Create a new estate with the name provided
                    EstateSettings estateSettings = m_application.EstateDataService.CreateNewEstate();

                    estateSettings.EstateName = estateName;
                    estateSettings.EstateOwner = estateOwner;

                    // Persistence does not seem to effect the need to save a new estate
                    m_application.EstateDataService.StoreEstateSettings(estateSettings);

                    m_log.InfoFormat("[ESTATE LOADER FILE SYSTEM]: Loaded config for estate {0}", estateName);

                    i++;
                }
            }

        }
    }
}
