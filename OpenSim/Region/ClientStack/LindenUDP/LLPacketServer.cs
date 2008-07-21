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

using System.Net;
using System.Net.Sockets;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.ClientStack.LindenUDP;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLPacketServer
    {
        //private static readonly log4net.ILog m_log
        //    = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly LLClientStackNetworkHandler m_networkHandler;
        private IScene m_scene;

        //private readonly ClientManager m_clientManager = new ClientManager();
        //public ClientManager ClientManager
        //{
        //    get { return m_clientManager; }
        //}

        public LLPacketServer(LLClientStackNetworkHandler networkHandler)
        {
            m_networkHandler = networkHandler;
            m_networkHandler.RegisterPacketServer(this);
        }

        public IScene LocalScene
        {
            set { m_scene = value; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <param name="packet"></param>
        public virtual void InPacket(uint circuitCode, Packet packet)
        {
            m_scene.ClientManager.InPacket(circuitCode, packet);
        }

        protected virtual IClientAPI CreateNewClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack,
                                                     ClientManager clientManager, IScene scene, AssetCache assetCache,
                                                     LLPacketServer packServer, AgentCircuitManager authenSessions,
                                                     LLUUID agentId, LLUUID sessionId, uint circuitCode, EndPoint proxyEP)
        {
            return
                new LLClientView(remoteEP, scene, assetCache, packServer, authenSessions, agentId, sessionId, circuitCode, proxyEP);
        }

        public virtual bool AddNewClient(EndPoint epSender, UseCircuitCodePacket useCircuit, AssetCache assetCache,
                                         AgentCircuitManager authenticateSessionsClass, EndPoint proxyEP)
        {
            IClientAPI newuser;

            if (m_scene.ClientManager.TryGetClient(useCircuit.CircuitCode.Code, out newuser))
            {
                return false;
            }
            else
            {
                newuser = CreateNewClient(epSender, useCircuit, m_scene.ClientManager, m_scene, assetCache, this,
                                          authenticateSessionsClass, useCircuit.CircuitCode.ID,
                                          useCircuit.CircuitCode.SessionID, useCircuit.CircuitCode.Code, proxyEP);

                m_scene.ClientManager.Add(useCircuit.CircuitCode.Code, newuser);

                newuser.OnViewerEffect += m_scene.ClientManager.ViewerEffectHandler;
                newuser.OnLogout += LogoutHandler;
                newuser.OnConnectionClosed += CloseClient;

                return true;
            }
        }

        public void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();

            CloseClient(client);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        /// <param name="flags"></param>
        /// <param name="circuitcode"></param>
        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode)
        {
            m_networkHandler.SendPacketTo(buffer, size, flags, circuitcode);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="circuitcode"></param>
        public virtual void CloseCircuit(uint circuitcode)
        {
            m_networkHandler.RemoveClientCircuit(circuitcode);

            //m_scene.ClientManager.CloseAllAgents(circuitcode);
        }

        /// <summary>
        /// Completely close down the given client.
        /// </summary>
        /// <param name="client"></param>
        public virtual void CloseClient(IClientAPI client)
        {
            //m_log.Info("PacketServer:CloseClient()");

            CloseCircuit(client.CircuitCode);
            m_scene.ClientManager.Remove(client.CircuitCode);
            client.Close(false);
        }
    }
}