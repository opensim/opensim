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
 *     * Neither the name of the OpenSimulator Project nor the
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
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// This class sets up new client stacks.  It also handles the immediate distribution of incoming packets to
    /// client stacks
    /// </summary>
    public class LLPacketServer
    {
//        private static readonly log4net.ILog m_log
//            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected readonly ILLClientStackNetworkHandler m_networkHandler;
        protected IScene m_scene;
        
        /// <summary>
        /// Tweakable user settings
        /// </summary>
        private ClientStackUserSettings m_userSettings;

        public LLPacketServer(ILLClientStackNetworkHandler networkHandler, ClientStackUserSettings userSettings)
        {
            m_userSettings = userSettings;
            m_networkHandler = networkHandler;
            
            m_networkHandler.RegisterPacketServer(this);
        }

        public IScene LocalScene
        {
            set { m_scene = value; }
        }

        /// <summary>
        /// Process an incoming packet.
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <param name="packet"></param>
        public virtual void InPacket(uint circuitCode, Packet packet)
        {
            m_scene.ClientManager.InPacket(circuitCode, packet);
        }

        /// <summary>
        /// Create a new client circuit
        /// </summary>
        /// <param name="remoteEP"></param>
        /// <param name="scene"></param>
        /// <param name="assetCache"></param>
        /// <param name="packServer"></param>
        /// <param name="sessionInfo"></param>
        /// <param name="agentId"></param>
        /// <param name="sessionId"></param>
        /// <param name="circuitCode"></param>
        /// <param name="proxyEP"></param>
        /// <returns></returns>
        protected virtual IClientAPI CreateNewCircuit(
            EndPoint remoteEP, IScene scene, 
            LLPacketServer packServer, AuthenticateResponse sessionInfo,
             UUID agentId, UUID sessionId, uint circuitCode, EndPoint proxyEP)
        {
            return
                new LLClientView(
                     remoteEP, scene, packServer, sessionInfo, agentId, sessionId, circuitCode, proxyEP,
                     m_userSettings);
        }

        /// <summary>
        /// Check whether a given client is authorized to connect.
        /// </summary>
        /// <param name="useCircuit"></param>
        /// <param name="circuitManager"></param>
        /// <param name="sessionInfo"></param>
        /// <returns></returns>
        public virtual bool IsClientAuthorized(
            UseCircuitCodePacket useCircuit, AgentCircuitManager circuitManager, out AuthenticateResponse sessionInfo)
        {
            UUID agentId = useCircuit.CircuitCode.ID;
            UUID sessionId = useCircuit.CircuitCode.SessionID;
            uint circuitCode = useCircuit.CircuitCode.Code;

            sessionInfo = circuitManager.AuthenticateSession(sessionId, agentId, circuitCode); 

            if (!sessionInfo.Authorised)
                return false;
            
            return true;
        }

        /// <summary>
        /// Add a new client circuit.  We assume that is has already passed an authorization check
        /// </summary>
        /// <param name="epSender"></param>
        /// <param name="useCircuit"></param>
        /// <param name="assetCache"></param>
        /// <param name="sessionInfo"></param>
        /// <param name="proxyEP"></param>
        /// <returns>
        /// true if a new circuit was created, false if a circuit with the given circuit code already existed
        /// </returns>
        public virtual bool AddNewClient(
            EndPoint epSender, UseCircuitCodePacket useCircuit, 
            AuthenticateResponse sessionInfo, EndPoint proxyEP)
        {
            IClientAPI newuser;
            uint circuitCode = useCircuit.CircuitCode.Code;
            
            if (m_scene.ClientManager.TryGetClient(circuitCode, out newuser))
            {
                // The circuit is already known to the scene.  This not actually a problem since this will currently
                // occur if a client is crossing borders (hence upgrading its circuit).  However, we shouldn't 
                // really by trying to add a new client if this is the case.
                return false;
            }
            
            UUID agentId = useCircuit.CircuitCode.ID;
            UUID sessionId = useCircuit.CircuitCode.SessionID;
            
            newuser 
                = CreateNewCircuit(
                    epSender, m_scene, this, sessionInfo, agentId, sessionId, circuitCode, proxyEP);

            m_scene.ClientManager.Add(circuitCode, newuser);

            newuser.OnViewerEffect += m_scene.ClientManager.ViewerEffectHandler;
            newuser.OnLogout += LogoutHandler;
            newuser.OnConnectionClosed += CloseClient;
            
            newuser.Start();

            return true;
        }

        public void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();
            CloseClient(client);
        }

        /// <summary>
        /// Send a packet to the given circuit
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
        /// Close a client circuit only
        /// </summary>
        /// <param name="circuitcode"></param>
        public virtual void CloseCircuit(uint circuitcode)
        {
            m_networkHandler.RemoveClientCircuit(circuitcode);
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
