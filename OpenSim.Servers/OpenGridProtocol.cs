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
        private Socket m_listenerSocket;
        private IPEndPoint m_IPendpoint;

        private int m_port;
        private ArrayList m_clients;

        private class ClientHandler
        {
            private Thread m_clientThread;
            private Socket m_socketHandle;

            public ClientHandler(Socket clientSocketHandle)
            {
                m_socketHandle = clientSocketHandle;
                m_clientThread = new Thread(new ThreadStart(DoWork));
                m_clientThread.IsBackground = true;
                m_clientThread.Start();
            }

            private void DoWork()
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenGridProtocol.cs: ClientHandler.DoWork() - Got new client");
                this.WriteLine("OpenSim 0.1, running OGS protocol 1.0");

            }

            private void WriteLine(string theline)
            {
                theline += "\n";
                byte[] thelinebuffer = System.Text.Encoding.ASCII.GetBytes(theline.ToCharArray());
                m_socketHandle.Send(thelinebuffer, theline.Length, 0);
            }
        }

        public OpenGridProtocolServer(int port)
        {
            m_port = port;
        }

        public void Start()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenGridProtocol.cs: Start() - Opening server socket");

            m_clients = new ArrayList();
            m_workerThread = new Thread(new ThreadStart(StartServerSocket));
            m_workerThread.IsBackground = true;
            m_workerThread.Start();
        }

        private void StartServerSocket()
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenGridProtocol.cs: StartServerSocket() - Spawned main thread OK");


                m_IPendpoint = new IPEndPoint(IPAddress.Any, m_port);
                m_listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                m_listenerSocket.Bind(m_IPendpoint);
                m_listenerSocket.Listen(4);

                Socket sockethandle;
                while (true)
                {
                    sockethandle = m_listenerSocket.Accept();
                    lock (m_clients.SyncRoot)
                    {
                        m_clients.Add(new OpenGridProtocolServer.ClientHandler(sockethandle));
                    }
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(e.Message);
            }
        }
    }
}
