using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace OpenSim
{
    public abstract class OpenSimApplication
    {
        public abstract void StartUp();
        public abstract void Shutdown();
        public abstract void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode);// EndPoint packetSender);
        public abstract void RemoveClientCircuit(uint circuitcode);
    }
}
