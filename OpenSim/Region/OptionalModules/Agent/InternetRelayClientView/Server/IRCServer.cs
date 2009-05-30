using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server
{
    /// <summary>
    /// Adam's completely hacked up not-probably-compliant RFC1459 server class.
    /// </summary>
    class IRCServer
    {
        private TcpListener m_listener;
        private bool m_running = true;

        public IRCServer(IPAddress listener, int port)
        {
            m_listener = new TcpListener(listener, port);

            m_listener.Start(50);

            Thread thread = new Thread(ListenLoop);
            thread.Start();
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
            
        }
    }
}
