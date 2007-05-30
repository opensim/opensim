using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
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

        public PacketServer(OpenSimNetworkHandler networkHandler)
        {
            _networkHandler = networkHandler;
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

        public virtual bool AddNewClient(EndPoint epSender, UseCircuitCodePacket useCircuit, AssetCache assetCache, InventoryCache inventoryCache, AuthenticateSessionsBase authenticateSessionsClass)
        {
            ClientView newuser = new ClientView(epSender, useCircuit, this.ClientThreads, this._localWorld, assetCache, this, inventoryCache, authenticateSessionsClass);
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
