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
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.Authentication
{
    public class OpenIdServerConnector : ServiceConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IAuthenticationService m_AuthenticationService;
        private IUserAccountService m_UserAccountService;
        private string m_ConfigName = "OpenIdService";

        public OpenIdServerConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string authService = serverConfig.GetString("AuthenticationServiceModule",
                    String.Empty);
            string userService = serverConfig.GetString("UserAccountServiceModule",
                    String.Empty);

            if (authService == String.Empty || userService == String.Empty)
                throw new Exception("No AuthenticationServiceModule or no UserAccountServiceModule in config file for OpenId authentication");

            Object[] args = new Object[] { config };
            m_AuthenticationService = ServerUtils.LoadPlugin<IAuthenticationService>(authService, args);
            m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(userService, args);

            // Handler for OpenID user identity pages
            server.AddStreamHandler(new OpenIdStreamHandler("GET", "/users/", m_UserAccountService, m_AuthenticationService));
            // Handlers for the OpenID endpoint server
            server.AddStreamHandler(new OpenIdStreamHandler("POST", "/openid/server/", m_UserAccountService, m_AuthenticationService));
            server.AddStreamHandler(new OpenIdStreamHandler("GET", "/openid/server/", m_UserAccountService, m_AuthenticationService));

            m_log.Info("[OPENID]: OpenId service enabled");
        }
    }
}
