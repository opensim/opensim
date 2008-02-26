using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Interfaces;
using Nini;
using libsecondlife;

namespace OpenSim.Region.Environment.Modules.VoiceChat
{
    public class VoiceChatServer : IRegionModule
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        int m_dummySocketPort = 53134;

        Thread m_listenerThread;
        Thread m_mainThread;
        List<Scene> m_scenes;
        Socket m_server;
        Socket m_selectCancel;
        bool m_enabled = false;

        Dictionary<Socket, VoiceClient> m_clients;
        Dictionary<LLUUID, VoiceClient> m_uuidToClient;


        #region IRegionModule Members

        public void Initialise(Scene scene, Nini.Config.IConfigSource source)
        {
            if (!m_scenes.Contains(scene))
                m_scenes.Add(scene);

            scene.EventManager.OnNewClient += NewClient;
            scene.EventManager.OnRemovePresence += RemovePresence;

            try
            {
                m_enabled = source.Configs["Voice"].GetBoolean("enabled", m_enabled);
            }
            catch (Exception)
            { }
        }

        public void PostInitialise()
        {
            if (m_enabled != true)
                return;

            m_clients = new Dictionary<Socket, VoiceClient>();
            m_uuidToClient = new Dictionary<LLUUID, VoiceClient>();

            try
            {
                CreateListeningSocket();
            }
            catch (Exception)
            {
                m_log.Error("[VOICECHAT]: Unable to start listening");
                return;
            }

            m_listenerThread = new Thread(new ThreadStart(ListenIncomingConnections));
            m_listenerThread.IsBackground = true;
            m_listenerThread.Start();

            m_mainThread = new Thread(new ThreadStart(RunVoiceChat));
            m_mainThread.IsBackground = true;
            m_mainThread.Start();

            Thread.Sleep(500);
            m_selectCancel = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_selectCancel.Connect("localhost", m_dummySocketPort);
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public string Name
        {
            get { return "VoiceChatModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; } // I think we can share this one.
        }

        #endregion

        public void NewClient(IClientAPI client)
        {
            m_log.Info("[VOICECHAT]: New client: " + client.AgentId);
            lock (m_uuidToClient)
            {
                m_uuidToClient[client.AgentId] = null;
            }
        }

        public void RemovePresence(LLUUID uuid)
        {
            lock (m_uuidToClient)
            {
                if (m_uuidToClient.ContainsKey(uuid))
                {
                    if (m_uuidToClient[uuid] != null)
                    {
                        RemoveClient(m_uuidToClient[uuid].m_socket);
                    }
                    m_uuidToClient.Remove(uuid);
                }
                else
                {
                    m_log.Error("[VOICECHAT]: Presence not found on RemovePresence: " + uuid);
                }
            }
        }

        public bool AddClient(VoiceClient client, LLUUID uuid)
        {
            lock (m_uuidToClient)
            {
                if (m_uuidToClient.ContainsKey(uuid))
                {
                    if (m_uuidToClient[uuid] != null) {
                        m_log.Warn("[VOICECHAT]: Multiple login attempts for " + uuid);
                        return false;
                    }
                    m_uuidToClient[uuid] = client;
                    return true;
                } 
            }
            return false;
        }

        public void RemoveClient(Socket socket)
        {
            m_log.Info("[VOICECHAT]: Removing client");
            lock(m_clients)
            {
                VoiceClient client = m_clients[socket];

                lock(m_uuidToClient)
                {
                    if (m_uuidToClient.ContainsKey(client.m_clientId))
                    {
                        m_uuidToClient[client.m_clientId] = null;
                    }
                }

                m_clients.Remove(socket);
                client.m_socket.Close();
            }
        }

        protected void CreateListeningSocket()
        {
            IPEndPoint listenEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 12000);
            m_server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_server.Bind(listenEndPoint);
            m_server.Listen(50);
        }

        void ListenIncomingConnections()
        {
            m_log.Info("[VOICECHAT]: Listening connections...");
            //ServerStatus.ReportThreadName("VoiceChat: Connection listener");

            byte[] dummyBuffer = new byte[1];

            while (true)
            {
                try
                {
                    Socket connection = m_server.Accept();
                    lock (m_clients)
                    {
                        m_clients[connection] = new VoiceClient(connection, this);
                        m_selectCancel.Send(dummyBuffer);
                        m_log.Info("[VOICECHAT]: Voicechat connection from " + connection.RemoteEndPoint.ToString());
                    }
                }
                catch (SocketException e)
                {
                    m_log.Error("[VOICECHAT]: During accept: " + e.ToString());
                }
            }
        }

        Socket ListenLoopbackSocket()
        {
            IPEndPoint listenEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), m_dummySocketPort);
            Socket dummyListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            dummyListener.Bind(listenEndPoint);
            dummyListener.Listen(1);
            Socket socket = dummyListener.Accept();
            dummyListener.Close();
            return socket;
        }

        void RunVoiceChat()
        {
            m_log.Info("[VOICECHAT]: Connection handler started...");
            //ServerStatus.ReportThreadName("VoiceChat: Connection handler");

            //Listen a loopback socket for aborting select call
            Socket dummySocket = ListenLoopbackSocket();
            
            List<Socket> sockets = new List<Socket>();
            byte[] buffer = new byte[65536];

            while (true)
            {
                if (m_clients.Count == 0)
                {
                    Thread.Sleep(100);
                    continue;
                }

                lock (m_clients)
                {
                    foreach (Socket s in m_clients.Keys)
                    {
                        sockets.Add(s);
                    }
                }
                sockets.Add(dummySocket);

                try
                {
                    Socket.Select(sockets, null, null, 200000);
                }
                catch (SocketException e)
                {
                    m_log.Warn("[VOICECHAT]: " + e.Message);
                }

                foreach (Socket s in sockets)
                {
                    try
                    {
                        if (s.RemoteEndPoint != dummySocket.RemoteEndPoint)
                        {
                            ReceiveFromSocket(s, buffer);
                        }
                        else
                        {
                            //Receive data and check if there was an error with select abort socket
                            if (s.Receive(buffer) <= 0)
                            {
                                //Just give a warning for now
                                m_log.Error("[VOICECHAT]: Select abort socket was closed");
                            }
                        }
                    }
                    catch(ObjectDisposedException e)
                    {
                        m_log.Warn("[VOICECHAT]: Connection has been already closed");
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[VOICECHAT]: Exception: " + e.Message);

                        RemoveClient(s);
                    }
                }

                sockets.Clear();
            }
        }

        private void ReceiveFromSocket( Socket s, byte[] buffer )
        {
            int byteCount = s.Receive(buffer);
            if (byteCount <= 0)
            {
                m_log.Info("[VOICECHAT]: Connection lost to " + s.RemoteEndPoint);
                lock (m_clients)
                {
                    RemoveClient(s);
                }
            }
            else
            {
                //ServerStatus.ReportInPacketTcp(byteCount);
                lock (m_clients)
                {
                    if (m_clients.ContainsKey(s))
                    {
                        m_clients[s].OnDataReceived(buffer, byteCount);
                    }
                    else
                    {
                        m_log.Warn("[VOICECHAT]: Got data from " + s.RemoteEndPoint +
                                   ", but source is not a valid voice client");
                    }
                }
            }
        }

        public ScenePresence getScenePresence(LLUUID clientID)
        {
            foreach (Scene scene in m_scenes)
            {
                ScenePresence x;
                if ((x = scene.GetScenePresence(clientID)) != null)
                {
                    return x;
                }
            }
            return null;
        }

        public void BroadcastVoice(VoicePacket packet)
        {
            libsecondlife.LLVector3 origPos = getScenePresence(packet.m_clientId).AbsolutePosition;

            byte[] bytes = packet.GetBytes();
            foreach (VoiceClient client in m_clients.Values)
            {
                if (client.IsEnabled() && client.m_clientId != packet.m_clientId &&
                    client.m_authenticated && client.IsCodecSupported(packet.m_codec))
                {
                    ScenePresence presence = getScenePresence(client.m_clientId);

                    if (presence != null && Util.GetDistanceTo(presence.AbsolutePosition, origPos) < 20)
                    {
                        client.SendTo(bytes);
                    }
                }
            }
        }
    }
}
