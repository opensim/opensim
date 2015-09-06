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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;

namespace OpenSim.ApplicationPlugins.LoadRegions
{
    public class RegionLoaderFileSystem : IRegionLoader
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private IConfigSource m_configSource;

        public void SetIniConfigSource(IConfigSource configSource)
        {
            m_configSource = configSource;
        }

        public RegionInfo[] LoadRegions()
        {
            string regionConfigPath = Path.Combine(Util.configDir(), "Regions");
            bool allowRegionless = false;

            try
            {
                IConfig startupConfig = (IConfig)m_configSource.Configs["Startup"];
                regionConfigPath = startupConfig.GetString("regionload_regionsdir", regionConfigPath).Trim();
                allowRegionless = startupConfig.GetBoolean("allow_regionless", false);
            }
            catch (Exception)
            {
                // No INI setting recorded.
            }

            if (!Directory.Exists(regionConfigPath))
            {
                Directory.CreateDirectory(regionConfigPath);
            }

            string[] configFiles = Directory.GetFiles(regionConfigPath, "*.xml");
            string[] iniFiles = Directory.GetFiles(regionConfigPath, "*.ini");

            // Create an empty Regions.ini if there are no existing config files.
            if (!allowRegionless && configFiles.Length == 0 && iniFiles.Length == 0)
            {                
                new RegionInfo("DEFAULT REGION CONFIG", Path.Combine(regionConfigPath, "Regions.ini"), false, m_configSource);
                iniFiles = Directory.GetFiles(regionConfigPath, "*.ini");
            }
            
            m_log.InfoFormat("[REGION LOADER FILE SYSTEM]: Loading config files from {0}", regionConfigPath);

            List<RegionInfo> regionInfos = new List<RegionInfo>();

            int i = 0;
            foreach (string file in iniFiles)
            {
                m_log.InfoFormat("[REGION LOADER FILE SYSTEM]: Loading config file {0}", file);
                
                IConfigSource source = new IniConfigSource(file);

                foreach (IConfig config in source.Configs)
                {                   
                    RegionInfo regionInfo = new RegionInfo("REGION CONFIG #" + (i + 1), file, false, m_configSource, config.Name);
                    regionInfos.Add(regionInfo);
                    
                    m_log.InfoFormat("[REGION LOADER FILE SYSTEM]: Loaded config for region {0}", regionInfo.RegionName);
                    
                    i++;
                }
            }

            foreach (string file in configFiles)
            {
                m_log.InfoFormat("[REGION LOADER FILE SYSTEM]: Loading config file {0}", file);
                
                RegionInfo regionInfo = new RegionInfo("REGION CONFIG #" + (i + 1), file, false, m_configSource);
                regionInfos.Add(regionInfo);
                
                m_log.InfoFormat("[REGION LOADER FILE SYSTEM]: Loaded config for region {0}", regionInfo.RegionName);
                
                i++;
            }

            return regionInfos.ToArray();
        }
    }
}