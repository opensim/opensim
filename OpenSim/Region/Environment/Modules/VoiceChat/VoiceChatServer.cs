/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
        List<Scene> m_scenes = new List<Scene>();
        Socket m_server;
        Socket m_selectCancel;
        bool m_enabled = false;

        Dictionary<Socket, VoiceClient> m_clients = new Dictionary<Socket,VoiceClient>();
        Dictionary<LLUUID, VoiceClient> m_uuidToClient = new Dictionary<LLUUID,VoiceClient>();


        #region IRegionModule Members

        public void Initialise(Scene scene, Nini.Config.IConfigSource source)
        {
            try
            {
                m_enabled = source.Configs["Voice"].GetBoolean("enabled", m_enabled);
            }
            catch (Exception)
            { }

            if (m_enabled)
            {
                if (!m_scenes.Contains(scene))
                    m_scenes.Add(scene);

                scene.EventManager.OnNewClient += NewClient;
                scene.EventManager.OnRemovePresence += RemovePresence;
            }
        }

        public void PostInitialise()
        {
            if (m_enabled != true)
                return;

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
                    catch(ObjectDisposedException)
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

        public LLVector3 getScenePresencePosition(LLUUID clientID)
        {
            foreach (Scene scene in m_scenes)
            {
                ScenePresence x;
                if ((x = scene.GetScenePresence(clientID)) != null)
                {
                    return x.AbsolutePosition + new LLVector3(Constants.RegionSize * scene.RegionInfo.RegionLocX,
                        Constants.RegionSize * scene.RegionInfo.RegionLocY, 0);
                }
            }
            return LLVector3.Zero;
        }

        public void BroadcastVoice(VoicePacket packet)
        {
            libsecondlife.LLVector3 origPos = getScenePresencePosition(packet.m_clientId);

            byte[] bytes = packet.GetBytes();
            foreach (VoiceClient client in m_clients.Values)
            {
                if (client.IsEnabled() && client.m_clientId != packet.m_clientId &&
                    client.m_authenticated && client.IsCodecSupported(packet.m_codec))
                {
                    LLVector3 presenceLoc = getScenePresencePosition(client.m_clientId);

                    if (presenceLoc != LLVector3.Zero && Util.GetDistanceTo(presenceLoc, origPos) < 20)
                    {
                        client.SendTo(bytes);
                    }
                }
            }
        }
    }
}
