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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;

using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Authentication
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalAuthenticationServicesConnector")]
    public class LocalAuthenticationServicesConnector : ISharedRegionModule, IAuthenticationService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IAuthenticationService m_AuthenticationService;

        private bool m_Enabled = false;

        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalAuthenticationServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AuthenticationServices", "");
                if (name == Name)
                {
                    IConfig userConfig = source.Configs["AuthenticationService"];
                    if (userConfig == null)
                    {
                        m_log.Error("[AUTH CONNECTOR]: AuthenticationService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = userConfig.GetString("LocalServiceModule",
                            String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[AUTH CONNECTOR]: No LocalServiceModule named in section AuthenticationService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_AuthenticationService =
                            ServerUtils.LoadPlugin<IAuthenticationService>(serviceDll,
                            args);

                    if (m_AuthenticationService == null)
                    {
                        m_log.Error("[AUTH CONNECTOR]: Can't load Authentication service");
                        return;
                    }
                    m_Enabled = true;
                    m_log.Info("[AUTH CONNECTOR]: Local Authentication connector enabled");
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

            scene.RegisterModuleInterface<IAuthenticationService>(m_AuthenticationService);
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

        #region IAuthenticationService

        public string Authenticate(UUID principalID, string password, int lifetime, out UUID realID)
        {
            // Not implemented at the regions
            realID = UUID.Zero;
            return string.Empty;
        }

        public string Authenticate(UUID principalID, string password, int lifetime)
        {
            // Not implemented at the regions
            return string.Empty;
        }

        public bool Verify(UUID principalID, string token, int lifetime)
        {
            return m_AuthenticationService.Verify(principalID, token, lifetime);
        }

        public bool Release(UUID principalID, string token)
        {
            return m_AuthenticationService.Release(principalID, token);
        }

        public bool SetPassword(UUID principalID, string passwd)
        {
            return m_AuthenticationService.SetPassword(principalID, passwd);
        }

        public AuthInfo GetAuthInfo(UUID principalID)
        {
            return m_AuthenticationService.GetAuthInfo(principalID);
        }

        public bool SetAuthInfo(AuthInfo info)
        {
            return m_AuthenticationService.SetAuthInfo(info);
        }

        #endregion
    }
}
