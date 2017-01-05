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
using Mono.Addins;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Land
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalLandServicesConnector")]
    public class LocalLandServicesConnector : ISharedRegionModule, ILandService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_Scenes = new List<Scene>();

        private bool m_Enabled = false;

        public LocalLandServicesConnector()
        {
        }

        public LocalLandServicesConnector(List<Scene> scenes)
        {
            m_Scenes = scenes;
        }

        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalLandServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("LandServices", this.Name);
                if (name == Name)
                {
                    m_Enabled = true;
                    m_log.Info("[LAND CONNECTOR]: Local land connector enabled");
                }
            }
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_Scenes.Add(scene);

            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<ILandService>(this);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Scenes.Contains(scene))
                m_Scenes.Remove(scene);
        }

        #endregion ISharedRegionModule

        #region ILandService

        public LandData GetLandData(UUID scopeID, ulong regionHandle, uint x, uint y, out byte regionAccess)
        {
            regionAccess = 2;
//            m_log.DebugFormat("[LAND CONNECTOR]: request for land data in {0} at {1}, {2}",
//                  regionHandle, x, y);

            uint rx = 0, ry = 0;
            Util.RegionHandleToWorldLoc(regionHandle, out rx, out ry);

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
                    LandData land = s.GetLandData(x, y);
                    regionAccess = s.RegionInfo.AccessLevel;
                    return land;
                }
            }

            m_log.Debug("[LAND CONNECTOR]: didn't find land data locally.");
            return null;

        }

        #endregion ILandService
    }
}
