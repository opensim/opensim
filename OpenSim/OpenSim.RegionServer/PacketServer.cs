using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.world;
using libsecondlife.Packets;

namespace OpenSim
{
    public class PacketServer
    {
        private OpenSimNetworkHandler _networkHandler;
        private World _localWorld;
        public Dictionary<uint, ClientView> ClientThreads = new Dictionary<uint, ClientView>();

        public PacketServer(OpenSimNetworkHandler networkHandler)
        {
            _networkHandler = networkHandler;
            _networkHandler.RegisterPacketServer(this);
        }

        public World LocalWorld
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

        #region Client Packet Handlers

        public bool RequestUUIDName(ClientView simClient, Packet packet)
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            Console.WriteLine(packet.ToString());
            UUIDNameRequestPacket nameRequest = (UUIDNameRequestPacket)packet;
            UUIDNameReplyPacket nameReply = new UUIDNameReplyPacket();
            nameReply.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[nameRequest.UUIDNameBlock.Length];

            for (int i = 0; i < nameRequest.UUIDNameBlock.Length; i++)
            {
                nameReply.UUIDNameBlock[i] = new UUIDNameReplyPacket.UUIDNameBlockBlock();
                nameReply.UUIDNameBlock[i].ID = nameRequest.UUIDNameBlock[i].ID;
                nameReply.UUIDNameBlock[i].FirstName = enc.GetBytes("Who\0");  //for now send any name
                nameReply.UUIDNameBlock[i].LastName = enc.GetBytes("Knows\0");	   //in future need to look it up		
            }
            simClient.OutPacket(nameReply);
            return true;
        }

        #endregion
    }
}
