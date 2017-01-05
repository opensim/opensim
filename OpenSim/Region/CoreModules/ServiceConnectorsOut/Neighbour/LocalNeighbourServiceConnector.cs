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
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Neighbour
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalNeighbourServicesConnector")]
    public class LocalNeighbourServicesConnector :
            ISharedRegionModule, INeighbourService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_Scenes = new List<Scene>();

        private bool m_Enabled = false;

        public LocalNeighbourServicesConnector()
        {
        }

        public LocalNeighbourServicesConnector(List<Scene> scenes)
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
            get { return "LocalNeighbourServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("NeighbourServices", this.Name);
                if (name == Name)
                {
                    // m_Enabled rules whether this module registers as INeighbourService or not
                    m_Enabled = true;
                    m_log.Info("[NEIGHBOUR CONNECTOR]: Local neighbour connector enabled");
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

            scene.RegisterModuleInterface<INeighbourService>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            m_log.Info("[NEIGHBOUR CONNECTOR]: Local neighbour connector enabled for region " + scene.RegionInfo.RegionName);
        }

        public void PostInitialise()
        {
        }

        public void RemoveRegion(Scene scene)
        {
            // Always remove
            if (m_Scenes.Contains(scene))
                m_Scenes.Remove(scene);
        }

        #endregion ISharedRegionModule

        #region INeighbourService

        public OpenSim.Services.Interfaces.GridRegion HelloNeighbour(ulong regionHandle, RegionInfo thisRegion)
        {
            uint x, y;
            Util.RegionHandleToRegionLoc(regionHandle, out x, out y);

            foreach (Scene s in m_Scenes)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    m_log.DebugFormat("[LOCAL NEIGHBOUR SERVICE CONNECTOR]: HelloNeighbour from region {0} to neighbour {1} at {2}-{3}",
                                                thisRegion.RegionName, s.Name, x, y );

                    //m_log.Debug("[NEIGHBOUR CONNECTOR]: Found region to SendHelloNeighbour");
                    return s.IncomingHelloNeighbour(thisRegion);
                }
            }
            //m_log.DebugFormat("[NEIGHBOUR CONNECTOR]: region handle {0} not found", regionHandle);
            return null;
        }

        #endregion INeighbourService
    }
}
