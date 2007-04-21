using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nwc.XmlRpc;
using System.Collections;

namespace OpenSim.Servers
{
    public class OpenGridProtocolServer
    {
        
        private Thread m_workerThread;
        private TcpListener m_tcpListener;
	private int m_port;
	private ArrayList m_clients;

	private class ClientHandler {
		private Thread m_clientThread;
		private TcpClient m_socketHandle;

		public ClientHandler(TcpClient clientSocketHandle) {
			m_socketHandle=clientSocketHandle;
			m_clientThread = new Thread(new ThreadStart(DoWork));
			m_clientThread.IsBackground = true;
			m_clientThread.Start();
		}
		
		private void DoWork() {
                	OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenGridProtocol.cs: ClientHandler.DoWork() - Got new client");
		}
	}

        public OpenGridProtocolServer(int port)
        {
            m_port = port;
        }

        public void Start()
        {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenGridProtocol.cs: Start() - Opening server socket");

                m_workerThread = new Thread(new ThreadStart(StartServerSocket));
                m_workerThread.IsBackground = true;
                m_workerThread.Start();
        }

        private void StartServerSocket()
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenGridProtocol.cs: StartServerSocket() - Spawned main thread OK");
                

		m_tcpListener = new TcpListener(m_port);
                m_tcpListener.Start();

		TcpClient sockethandle;
                while (true)
                {
                    sockethandle = m_tcpListener.AcceptTcpClient();
		    m_clients.Add(new OpenGridProtocolServer.ClientHandler(sockethandle));
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(e.Message);
            }
        }
    }
}
