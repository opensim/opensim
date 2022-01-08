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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;

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

            IConfig startupConfig = m_configSource.Configs["Startup"];
            if(startupConfig == null)
                return;

            estateConfigPath = startupConfig.GetString("regionload_estatesdir", estateConfigPath).Trim();
            if(string.IsNullOrWhiteSpace(estateConfigPath))
                return;

            if (!Directory.Exists(estateConfigPath))
                return; // if nothing there, don't bother

            string[] iniFiles;
            try
            {
                iniFiles = Directory.GetFiles(estateConfigPath, "*.ini");
            }
            catch
            {
                m_log.Error("[ESTATE LOADER FILE SYSTEM]: could not open " + estateConfigPath);
                return;
            }

            // No Estate.ini Found
            if (iniFiles == null || iniFiles.Length == 0)
                return;

            m_log.InfoFormat("[ESTATE LOADER FILE SYSTEM]: Loading estate config files from {0}", estateConfigPath);

            List<int> existingEstates;

            List<int> existingEstateIDs = m_application.EstateDataService.GetEstatesAll();

            foreach (string file in iniFiles)
            {
                m_log.InfoFormat("[ESTATE LOADER FILE SYSTEM]: Loading config file {0}", file);

                IConfigSource source = null;
                try
                {
                    source = new IniConfigSource(file);
                }
                catch
                {
                    m_log.WarnFormat("[ESTATE LOADER FILE SYSTEM]: failed to parse file {0}", file);
                }

                if(source == null)
                    continue;

                foreach (IConfig config in source.Configs)
                {
                    // Read Estate Config From Source File
                    string estateName = config.Name;
                    if (string.IsNullOrWhiteSpace(estateName))
                        continue;

                    if (estateName.Length > 64) // need check this and if utf8 is valid
                    {
                        m_log.WarnFormat("[ESTATE LOADER FILE SYSTEM]: Estate name {0} is too large, ignoring", estateName);
                        continue;
                    }

                    string ownerString = config.GetString("Owner", string.Empty);
                    if (string.IsNullOrWhiteSpace(ownerString))
                        continue;

                    if (!UUID.TryParse(ownerString, out UUID estateOwner) || estateOwner.IsZero())
                        continue;

                    // Check If Estate Exists (Skip If So)
                    existingEstates = m_application.EstateDataService.GetEstates(estateName);

                    if (existingEstates.Count > 0)
                        continue;

                    //### Should check Estate Owner ID but no Scene object available at this point

                    // Does Config Specify EstateID (0 Defaults To AutoIncrement)
                    int EstateID = config.GetInt("EstateID", 0);

                    if (EstateID > 0)
                    {
                        if (EstateID < 100)
                        {
                            // EstateID Cannot be less than 100
                            m_log.WarnFormat("[ESTATE LOADER FILE SYSTEM]: Estate name {0} specified estateID that is less that 100, ignoring", estateName);
                            continue;
                        }
                        else if(existingEstateIDs.Contains(EstateID))
                        {
                            // Specified EstateID Exists
                            m_log.WarnFormat("[ESTATE LOADER FILE SYSTEM]: Estate name {0} specified estateID that is already in use, ignoring", estateName);
                            continue;
                        }
                    }

                    // Create a new estate with the name provided
                    EstateSettings estateSettings = m_application.EstateDataService.CreateNewEstate(EstateID);

                    estateSettings.EstateName = estateName;
                    estateSettings.EstateOwner = estateOwner;

                    // Persistence does not seem to effect the need to save a new estate
                    m_application.EstateDataService.StoreEstateSettings(estateSettings);

                    m_log.InfoFormat("[ESTATE LOADER FILE SYSTEM]: Loaded config for estate {0}", estateName);
                }
            }
        }
    }
}
