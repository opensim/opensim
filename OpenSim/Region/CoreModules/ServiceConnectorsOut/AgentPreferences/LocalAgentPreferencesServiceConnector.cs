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

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.AgentPreferences
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalAgentPreferencesServicesConnector")]
    public class LocalAgentPreferencesServicesConnector : ISharedRegionModule, IAgentPreferencesService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAgentPreferencesService m_AgentPreferencesService;
        private bool m_Enabled = false;

        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalAgentPreferencesServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AgentPreferencesServices", "");
                if (name == Name)
                {
                    IConfig userConfig = source.Configs["AgentPreferencesService"];
                    if (userConfig == null)
                    {
                        m_log.Error("[AGENT PREFERENCES CONNECTOR]: AgentPreferencesService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = userConfig.GetString("LocalServiceModule", String.Empty);

                    if (String.IsNullOrEmpty(serviceDll))
                    {
                        m_log.Error("[AGENT PREFERENCES CONNECTOR]: No AgentPreferencesModule named in section AgentPreferencesService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_AgentPreferencesService = ServerUtils.LoadPlugin<IAgentPreferencesService>(serviceDll, args);

                    if (m_AgentPreferencesService == null)
                    {
                        m_log.Error("[AGENT PREFERENCES CONNECTOR]: Can't load agent preferences service");
                        return;
                    }
                    m_Enabled = true;
                    m_log.Info("[AGENT PREFERENCES CONNECTOR]: Local agent preferences connector enabled");
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

            scene.RegisterModuleInterface<IAgentPreferencesService>(this);
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

        #endregion ISharedRegionModule

        #region IAgentPreferencesService

        public AgentPrefs GetAgentPreferences(UUID principalID)
        {
            return m_AgentPreferencesService.GetAgentPreferences(principalID);
        }

        public bool StoreAgentPreferences(AgentPrefs data)
        {
            return m_AgentPreferencesService.StoreAgentPreferences(data);
        }

        public string GetLang(UUID principalID)
        {
            return m_AgentPreferencesService.GetLang(principalID);
        }

        #endregion IAgentPreferencesService
    }
}
