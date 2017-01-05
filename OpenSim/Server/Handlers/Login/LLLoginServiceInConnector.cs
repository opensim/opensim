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
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.Login
{
    public class LLLoginServiceInConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ILoginService m_LoginService;
        private bool m_Proxy;
        private BasicDosProtectorOptions m_DosProtectionOptions;

        public LLLoginServiceInConnector(IConfigSource config, IHttpServer server, IScene scene) :
                base(config, server, String.Empty)
        {
            m_log.Debug("[LLLOGIN IN CONNECTOR]: Starting...");
            string loginService = ReadLocalServiceFromConfig(config);

            ISimulationService simService = scene.RequestModuleInterface<ISimulationService>();
            ILibraryService libService = scene.RequestModuleInterface<ILibraryService>();

            Object[] args = new Object[] { config, simService, libService };
            m_LoginService = ServerUtils.LoadPlugin<ILoginService>(loginService, args);

            InitializeHandlers(server);
        }

        public LLLoginServiceInConnector(IConfigSource config, IHttpServer server, string configName) :
            base(config, server, configName)
        {
            string loginService = ReadLocalServiceFromConfig(config);

            Object[] args = new Object[] { config };

            m_LoginService = ServerUtils.LoadPlugin<ILoginService>(loginService, args);

            InitializeHandlers(server);
        }

        public LLLoginServiceInConnector(IConfigSource config, IHttpServer server) :
            this(config, server, String.Empty)
        {
        }

        private string ReadLocalServiceFromConfig(IConfigSource config)
        {
            IConfig serverConfig = config.Configs["LoginService"];
            if (serverConfig == null)
                throw new Exception(String.Format("No section LoginService in config file"));

            string loginService = serverConfig.GetString("LocalServiceModule", String.Empty);
            if (loginService == string.Empty)
                throw new Exception(String.Format("No LocalServiceModule for LoginService in config file"));

            m_Proxy = serverConfig.GetBoolean("HasProxy", false);
            m_DosProtectionOptions = new BasicDosProtectorOptions();
            // Dos Protection Options
            m_DosProtectionOptions.AllowXForwardedFor = serverConfig.GetBoolean("DOSAllowXForwardedForHeader", false);
            m_DosProtectionOptions.RequestTimeSpan =
                TimeSpan.FromMilliseconds(serverConfig.GetInt("DOSRequestTimeFrameMS", 10000));
            m_DosProtectionOptions.MaxRequestsInTimeframe = serverConfig.GetInt("DOSMaxRequestsInTimeFrame", 5);
            m_DosProtectionOptions.ForgetTimeSpan =
                TimeSpan.FromMilliseconds(serverConfig.GetInt("DOSForgiveClientAfterMS", 120000));
            m_DosProtectionOptions.ReportingName = "LOGINDOSPROTECTION";


            return loginService;
        }

        private void InitializeHandlers(IHttpServer server)
        {
            LLLoginHandlers loginHandlers = new LLLoginHandlers(m_LoginService, m_Proxy);
            server.AddXmlRPCHandler("login_to_simulator",
                new XmlRpcBasicDOSProtector(loginHandlers.HandleXMLRPCLogin,loginHandlers.HandleXMLRPCLoginBlocked,
                    m_DosProtectionOptions).Process, false);
            server.AddXmlRPCHandler("set_login_level", loginHandlers.HandleXMLRPCSetLoginLevel, false);
            server.SetDefaultLLSDHandler(loginHandlers.HandleLLSDLogin);
            server.AddWebSocketHandler("/WebSocket/GridLogin", loginHandlers.HandleWebSocketLoginEvents);
        }
    }
}
