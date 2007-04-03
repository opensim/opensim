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
        public Dictionary<uint, SimClient> ClientThreads = new Dictionary<uint, SimClient>();

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

        public void ClientInPacket(uint circuitCode, Packet packet)
        {
            if (this.ClientThreads.ContainsKey(circuitCode))
            {
                ClientThreads[circuitCode].InPacket(packet);
            }
        }

        public bool AddNewCircuitCodeClient(uint circuitCode)
        {
            return false;
        }

        public void SendPacketToAllClients(Packet packet)
        {

        }

        public void SendPacketToAllExcept(Packet packet, SimClient simClient)
        {

        }

        public virtual void RegisterClientPacketHandlers()
        {
            if (this._localWorld != null)
            {
                SimClient.AddPacketHandler(PacketType.ModifyLand, _localWorld.ModifyTerrain);
                SimClient.AddPacketHandler(PacketType.ChatFromViewer, _localWorld.SimChat);
                SimClient.AddPacketHandler(PacketType.RezObject, _localWorld.RezObject);
                SimClient.AddPacketHandler(PacketType.DeRezObject, _localWorld.DeRezObject);
                SimClient.AddPacketHandler(PacketType.UUIDNameRequest, this.RequestUUIDName);
            }
        }

        #region Client Packet Handlers

        public bool RequestUUIDName(SimClient simClient, Packet packet)
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
