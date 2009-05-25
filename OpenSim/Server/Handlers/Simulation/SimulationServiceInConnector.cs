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
                base(config, server)
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
