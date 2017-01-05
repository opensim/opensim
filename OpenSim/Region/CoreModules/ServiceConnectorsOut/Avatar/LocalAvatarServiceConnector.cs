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
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;

using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Avatar
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalAvatarServicesConnector")]
    public class LocalAvatarServicesConnector : ISharedRegionModule, IAvatarService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IAvatarService m_AvatarService;

        private bool m_Enabled = false;

        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalAvatarServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AvatarServices", "");
                if (name == Name)
                {
                    IConfig userConfig = source.Configs["AvatarService"];
                    if (userConfig == null)
                    {
                        m_log.Error("[AVATAR CONNECTOR]: AvatarService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = userConfig.GetString("LocalServiceModule",
                            String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[AVATAR CONNECTOR]: No LocalServiceModule named in section AvatarService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_AvatarService =
                            ServerUtils.LoadPlugin<IAvatarService>(serviceDll,
                            args);

                    if (m_AvatarService == null)
                    {
                        m_log.Error("[AVATAR CONNECTOR]: Can't load user account service");
                        return;
                    }
                    m_Enabled = true;
                    m_log.Info("[AVATAR CONNECTOR]: Local avatar connector enabled");
                }
            }
        }

        public void PostInitialise()
        {
            if (!m_Enabled)
                return;
        }

        public void Close()
        {
            if (!m_Enabled)
                return;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IAvatarService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        #endregion

        #region IAvatarService

        public AvatarAppearance GetAppearance(UUID userID)
        {
            return m_AvatarService.GetAppearance(userID);
        }

        public bool SetAppearance(UUID userID, AvatarAppearance appearance)
        {
            return m_AvatarService.SetAppearance(userID,appearance);
        }

        public AvatarData GetAvatar(UUID userID)
        {
            return m_AvatarService.GetAvatar(userID);
        }

        public bool SetAvatar(UUID userID, AvatarData avatar)
        {
            return m_AvatarService.SetAvatar(userID, avatar);
        }

        public bool ResetAvatar(UUID userID)
        {
            return m_AvatarService.ResetAvatar(userID);
        }

        public bool SetItems(UUID userID, string[] names, string[] values)
        {
            return m_AvatarService.SetItems(userID, names, values);
        }

        public bool RemoveItems(UUID userID, string[] names)
        {
            return m_AvatarService.RemoveItems(userID, names);
        }

        #endregion

    }
}
