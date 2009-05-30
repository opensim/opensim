using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView
{
    public class IRCStackModule : IRegionModule 
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IRCServer m_server;
        private Scene m_scene;

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (source.Configs.Contains("IRCd") &&
                source.Configs["IRCd"].GetBoolean("Enabled",false))
            {
                m_scene = scene;
                m_server = new IRCServer(IPAddress.Parse("0.0.0.0"), 6666, scene);
                m_server.OnNewIRCClient += m_server_OnNewIRCClient;
            }
        }

        void m_server_OnNewIRCClient(IRCClientView user)
        {
            user.OnIRCReady += user_OnIRCReady;
        }

        void user_OnIRCReady(IRCClientView cv)
        {
            m_log.Info("[IRCd] Adding user...");
            m_scene.ClientManager.Add(cv.CircuitCode, cv);
            cv.Start();
            m_log.Info("[IRCd] Added user to Scene");
        }

        public void PostInitialise()
        {

        }

        public void Close()
        {
            
        }

        public string Name
        {
            get { return "IRCClientStackModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}
