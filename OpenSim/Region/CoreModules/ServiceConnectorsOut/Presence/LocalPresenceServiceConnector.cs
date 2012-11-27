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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Presence
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalPresenceServicesConnector")]
    public class LocalPresenceServicesConnector : BasePresenceServiceConnector, ISharedRegionModule, IPresenceService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region ISharedRegionModule

        public string Name
        {
            get { return "LocalPresenceServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("PresenceServices", "");
                if (name == Name)
                {
                    IConfig inventoryConfig = source.Configs["PresenceService"];
                    if (inventoryConfig == null)
                    {
                        m_log.Error("[LOCAL PRESENCE CONNECTOR]: PresenceService missing from OpenSim.ini");
                        return;
                    }

                    string serviceDll = inventoryConfig.GetString("LocalServiceModule", String.Empty);

                    if (serviceDll == String.Empty)
                    {
                        m_log.Error("[LOCAL PRESENCE CONNECTOR]: No LocalServiceModule named in section PresenceService");
                        return;
                    }

                    Object[] args = new Object[] { source };
                    m_log.DebugFormat("[LOCAL PRESENCE CONNECTOR]: Service dll = {0}", serviceDll);

                    m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(serviceDll, args);

                    if (m_PresenceService == null)
                    {
                        m_log.Error("[LOCAL PRESENCE CONNECTOR]: Can't load presence service");
                        //return;
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    //Init(source);

                    m_PresenceDetector = new PresenceDetector(this);

                    m_Enabled = true;
                    m_log.Info("[LOCAL PRESENCE CONNECTOR]: Local presence connector enabled");
                }
            }
        }

        #endregion
    }
}