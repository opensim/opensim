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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Inventory;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HGInventoryServiceInConnector : InventoryServiceInConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private static readonly int INVENTORY_DEFAULT_SESSION_TIME = 30; // secs
        //private AuthedSessionCache m_session_cache = new AuthedSessionCache(INVENTORY_DEFAULT_SESSION_TIME);

        private IUserAgentService m_UserAgentService;

        public HGInventoryServiceInConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section '{0}' in config file", m_ConfigName));

            string userAgentService = serverConfig.GetString("UserAgentService", string.Empty);
            string m_userserver_url = serverConfig.GetString("UserAgentURI", String.Empty);
            if (m_userserver_url != string.Empty)
            {
                Object[] args = new Object[] { m_userserver_url };
                m_UserAgentService = ServerUtils.LoadPlugin<IUserAgentService>(userAgentService, args);
            }

            AddHttpHandlers(server);
            m_log.Debug("[HG INVENTORY HANDLER]: handlers initialized");
        }

        /// <summary>
        /// Check that the source of an inventory request for a particular agent is a current session belonging to
        /// that agent.
        /// </summary>
        /// <param name="session_id"></param>
        /// <param name="avatar_id"></param>
        /// <returns></returns>
        public override bool CheckAuthSession(string session_id, string avatar_id)
        {
            //m_log.InfoFormat("[HG INVENTORY IN CONNECTOR]: checking authed session {0} {1}", session_id, avatar_id);
            // This doesn't work

        //    if (m_session_cache.getCachedSession(session_id, avatar_id) == null)
        //    {
        //         //cache miss, ask userserver
        //        m_UserAgentService.VerifyAgent(session_id, ???);
        //    }
        //    else
        //    {
        //        // cache hits
        //        m_log.Info("[HG INVENTORY IN CONNECTOR]: got authed session from cache");
        //        return true;
        //    }

        //        m_log.Warn("[HG INVENTORY IN CONNECTOR]: unknown session_id, request rejected");
        //    return false;
        
            return true;
        }
    }
}
