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


namespace OpenSim.Region.CoreModules.ServiceConnectorsIn.Neighbour
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "NeighbourServiceInConnectorModule")]
    public class NeighbourServiceInConnectorModule : ISharedRegionModule, INeighbourService
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
                m_Enabled = moduleConfig.GetBoolean("NeighbourServiceInConnector", false);
                if (m_Enabled)
                {
                    m_log.Info("[NEIGHBOUR IN CONNECTOR]: NeighbourServiceInConnector enabled");
                }

            }

        }

        public void PostInitialise()
        {
            if (!m_Enabled)
                return;

//            m_log.Info("[NEIGHBOUR IN CONNECTOR]: Starting...");
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
            get { return "NeighbourServiceInConnectorModule"; }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Registered)
            {
                m_Registered = true;
                Object[] args = new Object[] { m_Config, MainServer.Instance, this, scene };
                ServerUtils.LoadPlugin<IServiceConnector>("OpenSim.Server.Handlers.dll:NeighbourServiceInConnector", args);
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

        #region INeighbourService

        public GridRegion HelloNeighbour(ulong regionHandle, RegionInfo thisRegion)
        {
            foreach (Scene s in m_Scenes)
            {
                if (s.RegionInfo.RegionHandle == regionHandle)
                {
                    //m_log.DebugFormat("[NEIGHBOUR IN CONNECTOR]: HelloNeighbour from {0} to {1}", thisRegion.RegionName, s.RegionInfo.RegionName);
                    return s.IncomingHelloNeighbour(thisRegion);
                }
            }
            return null;
        }

        #endregion INeighbourService
    }
}
