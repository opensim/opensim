using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using libsecondlife;


namespace OpenSim
{

    public interface OpenSimNetworkHandler
    {
        void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode);// EndPoint packetSender);
        void RemoveClientCircuit(uint circuitcode);
        void RegisterPacketServer(PacketServer server);
    }

}
