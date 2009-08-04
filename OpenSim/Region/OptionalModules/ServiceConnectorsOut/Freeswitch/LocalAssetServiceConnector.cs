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

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset
{
    public class FreeswitchServicesConnector :
            ISharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IFreeswitchService m_FreeswitchService;

        private bool m_Enabled = false;

        public Type ReplacableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "FreeswitchServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("FreeswitchServices", "");
                if (name == Name)
                {
                    IConfig freeswitchConfig = source.Configs["FreeswitchService"];
                    if (freeswitchConfig == null)
                    {
                        m_log.Error("[FREESWITCH CONNECTOR]: FreeswitchService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = freeswitchConfig.GetString("LocalServiceModule",
                            String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[FREESWITCH CONNECTOR]: No LocalServiceModule named in section FreeswitchService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_FreeswitchService =
                            ServerUtils.LoadPlugin<IFreeswitchService>(serviceDll,
                            args);

                    if (m_FreeswitchService == null)
                    {
                        m_log.Error("[FREESWITCH CONNECTOR]: Can't load freeswitch service");
                        return;
                    }
                    m_Enabled = true;
                    m_log.Info("[FREESWITCH CONNECTOR]: Freeswitch connector enabled");
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

            m_log.InfoFormat("[FREESWITCH CONNECTOR]: Enabled freeswitch for region {0}", scene.RegionInfo.RegionName);

            scene.RegisterModuleInterface<IFreeswitchService>(m_FreeswitchService);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }
    }
}
