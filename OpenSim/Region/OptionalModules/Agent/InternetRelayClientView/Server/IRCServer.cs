using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server
{
    public delegate void OnNewIRCUserDelegate(IRCClientView user);

    /// <summary>
    /// Adam's completely hacked up not-probably-compliant RFC1459 server class.
    /// </summary>
    class IRCServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event OnNewIRCUserDelegate OnNewIRCClient;

        private readonly TcpListener m_listener;
        private readonly Scene m_baseScene;
        private bool m_running = true;

        public IRCServer(IPAddress listener, int port, Scene baseScene)
        {
            m_listener = new TcpListener(listener, port);

            m_listener.Start(50);

            Thread thread = new Thread(ListenLoop);
            thread.Start();
            m_baseScene = baseScene;
        }

        public void Stop()
        {
            m_running = false;
            m_listener.Stop();
        }

        private void ListenLoop()
        {
            while(m_running)
            {
                AcceptClient(m_listener.AcceptTcpClient());
            }
        }

        private void AcceptClient(TcpClient client)
        {
            IRCClientView cv = new IRCClientView(client, m_baseScene);

            if (OnNewIRCClient != null)
                OnNewIRCClient(cv);
        }
    }
}
