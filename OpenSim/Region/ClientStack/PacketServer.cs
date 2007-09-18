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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Communications.Caches;
using libsecondlife;

namespace OpenSim.Region.ClientStack
{
    public class PacketServer
    {
        private ClientStackNetworkHandler m_networkHandler;
        private IScene _localScene;
        private readonly ClientManager m_clientManager = new ClientManager();
        public ClientManager ClientManager
        {
            get { return m_clientManager; }
        }

        public PacketServer(ClientStackNetworkHandler networkHandler)
        {
            m_networkHandler = networkHandler;
            m_networkHandler.RegisterPacketServer(this);
        }

        public IScene LocalScene
        {
            set
            {
                this._localScene = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <param name="packet"></param>
        public virtual void InPacket(uint circuitCode, Packet packet)
        {
            m_clientManager.InPacket(circuitCode, packet);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="circuitCode"></param>
        /// <returns></returns>
        public virtual bool AddNewCircuitCodeClient(uint circuitCode)
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packet"></param>
        public virtual void SendPacketToAllClients(Packet packet)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="simClient"></param>
        public virtual void SendPacketToAllExcept(Packet packet, IClientAPI simClient)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packetType"></param>
        /// <param name="handler"></param>
        public virtual void AddClientPacketHandler(PacketType packetType, PacketMethod handler)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void RegisterClientPacketHandlers()
        {
            
        }

        protected virtual IClientAPI CreateNewClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, ClientManager clientManager, IScene scene, AssetCache assetCache, PacketServer packServer, AgentCircuitManager authenSessions)
        {
            return new ClientView(remoteEP, initialcirpack, clientManager, scene, assetCache, packServer, authenSessions );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="epSender"></param>
        /// <param name="useCircuit"></param>
        /// <param name="assetCache"></param>
        /// <param name="inventoryCache"></param>
        /// <param name="authenticateSessionsClass"></param>
        /// <returns></returns>
        public virtual bool AddNewClient(EndPoint epSender, UseCircuitCodePacket useCircuit, AssetCache assetCache, AgentCircuitManager authenticateSessionsClass)
        {
            IClientAPI newuser =
                CreateNewClient(epSender, useCircuit, m_clientManager, _localScene, assetCache, this,
                                authenticateSessionsClass);

            this.m_clientManager.Add(useCircuit.CircuitCode.Code, newuser);

            newuser.OnViewerEffect += m_clientManager.ViewerEffectHandler;
            newuser.OnLogout += LogoutHandler;
            newuser.OnConnectionClosed += CloseClient;

            return true;
        }

        public void LogoutHandler(IClientAPI client)
        {
            client.SendLogoutPacket();

            CloseClient( client );
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
            this.m_networkHandler.SendPacketTo(buffer, size, flags, circuitcode);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="circuitcode"></param>
        public virtual void CloseCircuit(uint circuitcode)
        {
            m_networkHandler.RemoveClientCircuit( circuitcode );
            m_clientManager.CloseAllAgents( circuitcode );
        }

        public virtual void CloseClient( IClientAPI client )
        {
            CloseCircuit( client.CircuitCode );
        }
    }
}
