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
using OpenMetaverse;


namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Land
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RemoteLandServicesConnector")]
    public class RemoteLandServicesConnector :
            LandServicesConnector, ISharedRegionModule, ILandService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private LocalLandServicesConnector m_LocalService;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteLandServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("LandServices", "");
                if (name == Name)
                {
                    m_LocalService = new LocalLandServicesConnector();

                    m_Enabled = true;

                    m_log.Info("[LAND CONNECTOR]: Remote Land connector enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_LocalService.AddRegion(scene);
            scene.RegisterModuleInterface<ILandService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
                m_LocalService.RemoveRegion(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled)
                m_GridService = scene.GridService;
        }


        #region ILandService

        public override LandData GetLandData(UUID scopeID, ulong regionHandle, uint x, uint y, out byte regionAccess)
        {
            LandData land = m_LocalService.GetLandData(scopeID, regionHandle, x, y, out regionAccess);
            if (land != null)
                return land;

            return base.GetLandData(scopeID, regionHandle, x, y, out regionAccess);

        }
        #endregion ILandService
    }
}
