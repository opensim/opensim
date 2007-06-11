/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework;
using System.Net;
using System.Net.Sockets;
using OpenSim.Assets;

namespace OpenSim
{
    public class PacketServer
    {
        private OpenSimNetworkHandler _networkHandler;
        private IWorld _localWorld;
        public Dictionary<uint, ClientView> ClientThreads = new Dictionary<uint, ClientView>();
        public Dictionary<uint, IClientAPI> ClientAPIs = new Dictionary<uint, IClientAPI>();
        protected uint serverPort;

        public PacketServer(OpenSimNetworkHandler networkHandler, uint port)
        {
            _networkHandler = networkHandler;
            this.serverPort = port;
            _networkHandler.RegisterPacketServer(this);
        }

        public IWorld LocalWorld
        {
            set
            {
                this._localWorld = value;
            }
        }

        public virtual void ClientInPacket(uint circuitCode, Packet packet)
        {
            if (this.ClientThreads.ContainsKey(circuitCode))
            {
                ClientThreads[circuitCode].InPacket(packet);
            }
        }

        public virtual bool AddNewCircuitCodeClient(uint circuitCode)
        {
            return false;
        }

        public virtual void SendPacketToAllClients(Packet packet)
        {

        }

        public virtual void SendPacketToAllExcept(Packet packet, ClientView simClient)
        {

        }

        public virtual void AddClientPacketHandler(PacketType packetType, PacketMethod handler)
        {

        }

        public virtual void RegisterClientPacketHandlers()
        {
            
        }

        protected virtual ClientView CreateNewClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, Dictionary<uint, ClientView> clientThreads, IWorld world, AssetCache assetCache, PacketServer packServer, InventoryCache inventoryCache, AuthenticateSessionsBase authenSessions)
        {
            return new ClientView(remoteEP, initialcirpack, clientThreads, world, assetCache, packServer, inventoryCache, authenSessions );
        }

        public virtual bool AddNewClient(EndPoint epSender, UseCircuitCodePacket useCircuit, AssetCache assetCache, InventoryCache inventoryCache, AuthenticateSessionsBase authenticateSessionsClass)
        {
            ClientView newuser =
                CreateNewClient(epSender, useCircuit, ClientThreads, _localWorld, assetCache, this, inventoryCache,
                                authenticateSessionsClass);
            
            this.ClientThreads.Add(useCircuit.CircuitCode.Code, newuser);
            this.ClientAPIs.Add(useCircuit.CircuitCode.Code, (IClientAPI)newuser);

            return true;
        }

        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode)
        {
            this._networkHandler.SendPacketTo(buffer, size, flags, circuitcode);
        }

        public virtual void RemoveClientCircuit(uint circuitcode)
        {
            this._networkHandler.RemoveClientCircuit(circuitcode);
        }
    }
}
