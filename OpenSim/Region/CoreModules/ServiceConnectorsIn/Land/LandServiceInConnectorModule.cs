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
using System.Reflection;
using System.Collections.Generic;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using OpenMetaverse;


namespace OpenSim.Region.CoreModules.ServiceConnectorsIn.Land
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LandServiceInConnectorModule")]
    public class LandServiceInConnectorModule : ISharedRegionModule, ILandService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool m_Enabled = false;
        private static bool m_Registered = false;

        private IConfigSource m_Config;
        private List<Scene> m_Scenes = new List<Scene>();

        #region Region Module interface

        public void Initialise(IConfigSource config)
        {
            m_Config = config;

            IConfig moduleConfig = config.Configs["Modules"];
            if (moduleConfig != null)
            {
                m_Enabled = moduleConfig.GetBoolean("LandServiceInConnector", false);
                if (m_Enabled)
                {
                    m_log.Info("[LAND IN CONNECTOR]: LandServiceInConnector enabled");
                }

            }

        }

        public void PostInitialise()
        {
            if (!m_Enabled)
                return;

//            m_log.Info("[LAND IN CONNECTOR]: Starting...");
        }

        public void Close()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LandServiceInConnectorModule"; }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Registered)
            {
                m_Registered = true;
                Object[] args = new Object[] { m_Config, MainServer.Instance, this, scene };
                ServerUtils.LoadPlugin<IServiceConnector>("OpenSim.Server.Handlers.dll:LandServiceInConnector", args);
            }

            m_Scenes.Add(scene);

        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled && m_Scenes.Contains(scene))
                m_Scenes.Remove(scene);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

        #region ILandService

        public LandData GetLandData(UUID scopeID, ulong regionHandle, uint x, uint y, out byte regionAccess)
        {
//            m_log.DebugFormat("[LAND IN CONNECTOR]: GetLandData for {0}. Count = {1}",
//                regionHandle, m_Scenes.Count);

            uint rx = 0, ry = 0;
            Util.RegionHandleToWorldLoc(regionHandle, out rx, out ry);
            rx += x;
            ry += y;
            foreach (Scene s in m_Scenes)
            {
                uint t = s.RegionInfo.WorldLocX;
                if( rx < t)
                    continue;
                t += s.RegionInfo.RegionSizeX;
                if( rx >= t)
                    continue;
                t = s.RegionInfo.WorldLocY;
                if( ry < t)
                    continue;
                t += s.RegionInfo.RegionSizeY;
                if( ry  < t)
                {
//                    m_log.Debug("[LAND IN CONNECTOR]: Found region to GetLandData from");
                    x = rx - s.RegionInfo.WorldLocX;
                    y = ry - s.RegionInfo.WorldLocY;
                    regionAccess = s.RegionInfo.AccessLevel;
                    return s.GetLandData(x, y);
                }
            }
            m_log.DebugFormat("[LAND IN CONNECTOR]: region handle {0} not found", regionHandle);
            regionAccess = 42;
            return null;
        }

        #endregion ILandService
    }
}
