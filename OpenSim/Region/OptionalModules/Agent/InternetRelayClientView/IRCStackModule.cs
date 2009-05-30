using System.Net;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView
{
    class IRCStackModule : IRegionModule 
    {
        private IRCServer m_server;
        private Scene m_scene;

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_server = new IRCServer(IPAddress.Parse("0.0.0.0"),6666, scene);
            m_server.OnNewIRCClient += m_server_OnNewIRCClient;
        }

        void m_server_OnNewIRCClient(IRCClientView user)
        {
            m_scene.AddNewClient(user);
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
