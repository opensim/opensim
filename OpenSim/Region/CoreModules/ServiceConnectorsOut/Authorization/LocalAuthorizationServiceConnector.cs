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

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Authorization
{
    public class LocalAuthorizationServicesConnector :
            ISharedRegionModule, IAuthorizationService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IAuthorizationService m_AuthorizationService;

        private bool m_Enabled = false;

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalAuthorizationServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            m_log.Info("[AUTHORIZATION CONNECTOR]: Initialise");
            
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AuthorizationServices", string.Empty);
                if (name == Name)
                {
                    IConfig authorizationConfig = source.Configs["AuthorizationService"];
                    if (authorizationConfig == null)
                    {
                        m_log.Error("[AUTHORIZATION CONNECTOR]: AuthorizationService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = authorizationConfig.GetString("LocalServiceModule",
                            String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[AUTHORIZATION CONNECTOR]: No LocalServiceModule named in section AuthorizationService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_AuthorizationService =
                            ServerUtils.LoadPlugin<IAuthorizationService>(serviceDll,
                            args);

                    if (m_AuthorizationService == null)
                    {
                        m_log.Error("[AUTHORIZATION CONNECTOR]: Can't load authorization service");
                        return;
                    }
                    m_Enabled = true;
                    m_log.Info("[AUTHORIZATION CONNECTOR]: Local authorization connector enabled");
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

            scene.RegisterModuleInterface<IAuthorizationService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat("[AUTHORIZATION CONNECTOR]: Enabled local authorization for region {0}", scene.RegionInfo.RegionName);

           
        }

        public bool IsAuthorizedForRegion(string userID, string regionID, out string message)
        {
            return m_AuthorizationService.IsAuthorizedForRegion(userID, regionID, out message);
        }

    }
}
