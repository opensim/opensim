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

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.MuteList
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalMuteListServicesConnector")]
    public class LocalMuteListServicesConnector : ISharedRegionModule, IMuteListService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_Scenes = new List<Scene>();
        protected IMuteListService m_service = null;

        private bool m_Enabled = false;

         #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalMuteListServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            // only active for core mute lists module
            IConfig moduleConfig = source.Configs["Messaging"];
            if (moduleConfig == null)
                return;

            if (moduleConfig.GetString("MuteListModule", "None") != "MuteListModule")
                return;

            moduleConfig = source.Configs["Modules"];

            if (moduleConfig == null)
                return;

            string name = moduleConfig.GetString("MuteListService", "");
            if(name != Name)
                return;

            IConfig userConfig = source.Configs["MuteListService"];
            if (userConfig == null)
            {
                m_log.Error("[MuteList LOCALCONNECTOR]: MuteListService missing from configuration");
                return;
            }

            string serviceDll = userConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (serviceDll == String.Empty)
            {
                m_log.Error("[MuteList LOCALCONNECTOR]: No LocalServiceModule named in section MuteListService");
                return;
            }

            Object[] args = new Object[] { source };
            try
            {
                m_service = ServerUtils.LoadPlugin<IMuteListService>(serviceDll, args);
            }
            catch
            {
                m_log.Error("[MuteList LOCALCONNECTOR]: Failed to load mute service");
                return;
            }

            if (m_service == null)
            {
                m_log.Error("[MuteList LOCALCONNECTOR]: Can't load MuteList service");
                return;
            }

            m_Enabled = true;
            m_log.Info("[MuteList LOCALCONNECTOR]: enabled");
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock(m_Scenes)
            {
                m_Scenes.Add(scene);
                scene.RegisterModuleInterface<IMuteListService>(this);
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock(m_Scenes)
            {
                if (m_Scenes.Contains(scene))
                {
                    m_Scenes.Remove(scene);
                    scene.UnregisterModuleInterface<IMuteListService>(this);
                }
            }
        }

        #endregion ISharedRegionModule

        #region IMuteListService
        public Byte[] MuteListRequest(UUID agentID, uint crc)
        {
            if (!m_Enabled)
                return null;
            return m_service.MuteListRequest(agentID, crc);
        }

        public bool UpdateMute(MuteData mute)
        {
            if (!m_Enabled)
                return false;
            return m_service.UpdateMute(mute);
        }

        public bool RemoveMute(UUID agentID, UUID muteID, string muteName)
        {
            if (!m_Enabled)
                return false;
            return m_service.RemoveMute(agentID, muteID, muteName);
        }

        #endregion IMuteListService
    }
}
