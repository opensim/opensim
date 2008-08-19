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
 *     * Neither the name of the OpenSim Project nor the
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

using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.RegionLoader.Filesystem;
using OpenSim.Framework.RegionLoader.Web;

namespace OpenSim.ApplicationPlugins.LoadRegions
{
    public class LoadRegionsPlugin : IApplicationPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region IApplicationPlugin Members

        // TODO: required by IPlugin, but likely not at all right
        string m_name = "LoadRegionsPlugin";
        string m_version = "0.0";

        public string Version { get { return m_version; } }
        public string Name { get { return m_name; } }

        public void Initialise()
        {
            m_log.Info("[LOADREGIONS]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        public void Initialise(OpenSimBase openSim)
        {
            m_log.Info("[LOADREGIONS]: Load Regions addin being initialised");

            IRegionLoader regionLoader;
            if (openSim.ConfigSource.Source.Configs["Startup"].GetString("region_info_source", "filesystem") == "filesystem")
            {
                m_log.Info("[LOADREGIONS]: Loading Region Info from filesystem");
                regionLoader = new RegionLoaderFileSystem();
            }
            else
            {
                m_log.Info("[LOADREGIONSPLUGIN]: Loading Region Info from web");
                regionLoader = new RegionLoaderWebServer();
            }

            regionLoader.SetIniConfigSource(openSim.ConfigSource.Source);
            RegionInfo[] regionsToLoad = regionLoader.LoadRegions();

            openSim.ModuleLoader.LoadDefaultSharedModules();

            if (!CheckRegionsForSanity(regionsToLoad))
            {
                m_log.Error("[LOADREGIONS]: Halting startup due to conflicts in region configurations");
                System.Environment.Exit(1);
            }

            for (int i = 0; i < regionsToLoad.Length; i++)
            {
                m_log.Debug("[LOADREGIONS]: Creating Region: " + regionsToLoad[i].RegionName + " (ThreadID: " + Thread.CurrentThread.ManagedThreadId.ToString() +
                            ")");
                openSim.CreateRegion(regionsToLoad[i], true);
            }

            openSim.ModuleLoader.PostInitialise();
            openSim.ModuleLoader.ClearCache();
        }

        public void Dispose()
        {
        }

        #endregion

        /// <summary>
        /// Check that region configuration information makes sense.
        /// </summary>
        /// <param name="regions"></param>
        /// <returns>True if we're sane, false if we're insane</returns>
        private bool CheckRegionsForSanity(RegionInfo[] regions)
        {
            if (regions.Length <= 0)
                return true;

            List<RegionInfo> checkedRegions = new List<RegionInfo>();
            checkedRegions.Add(regions[0]);

            for (int i = 1; i < regions.Length; i++)
            {
                RegionInfo region = regions[i];

                foreach (RegionInfo checkedRegion in checkedRegions)
                {
                    if (region.RegionID == checkedRegion.RegionID)
                    {
                        m_log.ErrorFormat(
                             "[LOADREGIONS]: Regions {0} and {1} have the same UUID {2}",
                             region.RegionName, checkedRegion.RegionName, region.RegionID);
                        return false;
                    }
                    else if (region.RegionLocX == checkedRegion.RegionLocX && region.RegionLocY == checkedRegion.RegionLocY)
                    {
                        m_log.ErrorFormat(
                             "[LOADREGIONS]: Regions {0} and {1} have the same location {2} {3}",
                             region.RegionName, checkedRegion.RegionName, region.RegionLocX, region.RegionLocY);
                        return false;
                    }
                    else if (region.InternalEndPoint.Port == checkedRegion.InternalEndPoint.Port)
                    {
                        m_log.ErrorFormat(
                             "[LOADREGIONS]: Regions {0} and {1} have the same internal IP port {2}",
                             region.RegionName, checkedRegion.RegionName, region.InternalEndPoint.Port);
                        return false;
                    }
                }
            }

            return true;
        }

        public void LoadRegionFromConfig(OpenSimBase openSim, ulong regionhandle)
        {
            m_log.Info("[LOADREGIONS]: Load Regions addin being initialised");

            IRegionLoader regionLoader;
            if (openSim.ConfigSource.Source.Configs["Startup"].GetString("region_info_source", "filesystem") == "filesystem")
            {
                m_log.Info("[LOADREGIONS]: Loading Region Info from filesystem");
                regionLoader = new RegionLoaderFileSystem();
            }
            else
            {
                m_log.Info("[LOADREGIONS]: Loading Region Info from web");
                regionLoader = new RegionLoaderWebServer();
            }

            regionLoader.SetIniConfigSource(openSim.ConfigSource.Source);
            RegionInfo[] regionsToLoad = regionLoader.LoadRegions();
            for (int i = 0; i < regionsToLoad.Length; i++)
            {
                if (regionhandle == regionsToLoad[i].RegionHandle)
                {
                    m_log.Debug("[LOADREGIONS]: Creating Region: " + regionsToLoad[i].RegionName + " (ThreadID: " +
                                Thread.CurrentThread.ManagedThreadId.ToString() + ")");
                    openSim.CreateRegion(regionsToLoad[i], true);
                }
            }
        }
    }
}
