using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace OpenSim
{
    public interface OpenSimNetworkHandler
    {
        //public abstract void StartUp();
       // public abstract void Shutdown();
        void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode);// EndPoint packetSender);
        void RemoveClientCircuit(uint circuitcode);
    }
}
