using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.ClientStack;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Client.Linden
{
    public class LLClientStackModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region IRegionModule Members

        protected Scene m_scene;
        protected bool m_createClientStack = false;
        protected IClientNetworkServer m_clientServer;
        protected ClientStackManager m_clientStackManager;
        protected IConfigSource m_source;

        protected string m_clientStackDll = "OpenSim.Region.ClientStack.LindenUDP.dll";

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_scene == null)
            {
                m_scene = scene;
                m_source = source;

                IConfig startupConfig = m_source.Configs["Startup"];
                if (startupConfig != null)
                {
                    m_clientStackDll = startupConfig.GetString("clientstack_plugin", "OpenSim.Region.ClientStack.LindenUDP.dll");
                }
            }
        }

        public void PostInitialise()
        {
            if ((m_scene != null) && (m_createClientStack))
            {
                m_log.Info("[LLClientStackModule] Starting up LLClientStack.");
                uint port = (uint)m_scene.RegionInfo.InternalEndPoint.Port;
                m_clientStackManager = new ClientStackManager(m_clientStackDll);

                m_clientServer
                   = m_clientStackManager.CreateServer(m_scene.RegionInfo.InternalEndPoint.Address,
                     ref port, m_scene.RegionInfo.ProxyOffset, m_scene.RegionInfo.m_allow_alternate_ports, m_source,
                       m_scene.CommsManager.AssetCache, m_scene.AuthenticateHandler);

                m_clientServer.AddScene(m_scene);

                m_clientServer.Start();
            }
        }

        public void Close()
        {

        }

        public string Name
        {
            get { return "LLClientStackModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}
