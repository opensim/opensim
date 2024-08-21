using System;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.Experience
{
    public class ExperienceServiceConnector : ServiceConnector
    {
        private IExperienceService m_ExperienceService;
        private string m_ConfigName = "ExperienceService";

        public ExperienceServiceConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string service = serverConfig.GetString("LocalServiceModule", String.Empty);

            if (service == String.Empty)
                throw new Exception("LocalServiceModule not present in ExperienceService config file ExperienceService section");

            Object[] args = new Object[] { config };
            m_ExperienceService = ServerUtils.LoadPlugin<IExperienceService>(service, args);

            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);

            server.AddStreamHandler(new ExperienceServerPostHandler(m_ExperienceService, auth));
        }
    }
}
