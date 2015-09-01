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
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

using log4net;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class GatekeeperServiceInConnector : ServiceConnector
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        private IGatekeeperService m_GatekeeperService;
        public IGatekeeperService GateKeeper
        {
            get { return m_GatekeeperService; }
        }

        bool m_Proxy = false;

        public GatekeeperServiceInConnector(IConfigSource config, IHttpServer server, ISimulationService simService) :
                base(config, server, String.Empty)
        {
            IConfig gridConfig = config.Configs["GatekeeperService"];
            if (gridConfig != null)
            {
                string serviceDll = gridConfig.GetString("LocalServiceModule", string.Empty);
                Object[] args = new Object[] { config, simService };
                m_GatekeeperService = ServerUtils.LoadPlugin<IGatekeeperService>(serviceDll, args);

            }
            if (m_GatekeeperService == null)
                throw new Exception("Gatekeeper server connector cannot proceed because of missing service");

            m_Proxy = gridConfig.GetBoolean("HasProxy", false);

            HypergridHandlers hghandlers = new HypergridHandlers(m_GatekeeperService);
            server.AddXmlRPCHandler("link_region", hghandlers.LinkRegionRequest, false);
            server.AddXmlRPCHandler("get_region", hghandlers.GetRegion, false);

            server.AddStreamHandler(new GatekeeperAgentHandler(m_GatekeeperService, m_Proxy));
        }

        public GatekeeperServiceInConnector(IConfigSource config, IHttpServer server, string configName)
            : this(config, server, (ISimulationService)null)
        {
        }

        public GatekeeperServiceInConnector(IConfigSource config, IHttpServer server)
            : this(config, server, String.Empty)
        {
        }
    }
}
