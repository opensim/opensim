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
using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Services.Connectors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Neighbour
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RemoteNeighbourServicesConnector")]
    public class RemoteNeighbourServicesConnector :
            NeighbourServicesConnector, ISharedRegionModule, INeighbourService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private LocalNeighbourServicesConnector m_LocalService;
        //private string serviceDll;
        //private List<Scene> m_Scenes = new List<Scene>();

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteNeighbourServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("NeighbourServices");
                if (name == Name)
                {
                    m_LocalService = new LocalNeighbourServicesConnector();

                    //IConfig neighbourConfig = source.Configs["NeighbourService"];
                    //if (neighbourConfig == null)
                    //{
                    //    m_log.Error("[NEIGHBOUR CONNECTOR]: NeighbourService missing from OpenSim.ini");
                    //    return;
                    //}
                    //serviceDll = neighbourConfig.GetString("LocalServiceModule", String.Empty);
                    //if (serviceDll == String.Empty)
                    //{
                    //    m_log.Error("[NEIGHBOUR CONNECTOR]: No LocalServiceModule named in section NeighbourService");
                    //    return;
                    //}

                    m_Enabled = true;

                    m_log.Info("[NEIGHBOUR CONNECTOR]: Remote Neighbour connector enabled");
                }
            }
        }

        public void PostInitialise()
        {
            //if (m_Enabled)
            //{
            //    Object[] args = new Object[] { m_Scenes };
            //    m_LocalService =
            //            ServerUtils.LoadPlugin<INeighbourService>(serviceDll,
            //            args);

            //    if (m_LocalService == null)
            //    {
            //        m_log.Error("[NEIGHBOUR CONNECTOR]: Can't load neighbour service");
            //        Unregister();
            //        return;
            //    }
            //}
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_LocalService.AddRegion(scene);
            scene.RegisterModuleInterface<INeighbourService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
                m_LocalService.RemoveRegion(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_GridService = scene.GridService;

            m_log.InfoFormat("[NEIGHBOUR CONNECTOR]: Enabled remote neighbours for region {0}", scene.RegionInfo.RegionName);

        }

        #region INeighbourService

        public override GridRegion HelloNeighbour(ulong regionHandle, RegionInfo thisRegion)
        {
            GridRegion region = m_LocalService.HelloNeighbour(regionHandle, thisRegion);
            if (region != null)
                return region;

            return base.HelloNeighbour(regionHandle, thisRegion);
        }

        #endregion INeighbourService
    }
}
