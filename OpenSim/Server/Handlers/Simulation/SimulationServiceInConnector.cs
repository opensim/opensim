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
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.Simulation
{
    public class SimulationServiceInConnector : ServiceConnector
    {
        private ISimulationService m_SimulationService;
        private IAuthenticationService m_AuthenticationService;

        public SimulationServiceInConnector(IConfigSource config, IHttpServer server, IScene scene) :
                base(config, server, String.Empty)
        {
            IConfig serverConfig = config.Configs["SimulationService"];
            if (serverConfig == null)
                throw new Exception("No section 'SimulationService' in config file");

            bool authentication = serverConfig.GetBoolean("RequireAuthentication", false);

            if (authentication)
                m_AuthenticationService = scene.RequestModuleInterface<IAuthenticationService>();

            bool foreignGuests = serverConfig.GetBoolean("AllowForeignGuests", false);

            //string simService = serverConfig.GetString("LocalServiceModule",
            //        String.Empty);

            //if (simService == String.Empty)
            //    throw new Exception("No SimulationService in config file");

            //Object[] args = new Object[] { config };
            m_SimulationService = scene.RequestModuleInterface<ISimulationService>();
                    //ServerUtils.LoadPlugin<ISimulationService>(simService, args);
            if (m_SimulationService == null)
                throw new Exception("No Local ISimulationService Module");



            //System.Console.WriteLine("XXXXXXXXXXXXXXXXXXX m_AssetSetvice == null? " + ((m_AssetService == null) ? "yes" : "no"));
            server.AddStreamHandler(new AgentGetHandler(m_SimulationService, m_AuthenticationService));
            server.AddStreamHandler(new AgentPostHandler(m_SimulationService, m_AuthenticationService, foreignGuests));
            server.AddStreamHandler(new AgentPutHandler(m_SimulationService, m_AuthenticationService));
            server.AddStreamHandler(new AgentDeleteHandler(m_SimulationService, m_AuthenticationService));
            //server.AddStreamHandler(new ObjectPostHandler(m_SimulationService, authentication));
            //server.AddStreamHandler(new NeighborPostHandler(m_SimulationService, authentication));
        }
    }
}
