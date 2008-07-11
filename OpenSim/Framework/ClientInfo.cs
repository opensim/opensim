using System;
using System.Collections.Generic;
using System.Net;

namespace OpenSim.Framework
{
    [Serializable]
    public class ClientInfo
    {
        public sAgentCircuitData agentcircuit;

        public Dictionary<uint, byte[]> needAck;

        public List<byte[]> out_packets;
        public Dictionary<uint, uint> pendingAcks;
        public EndPoint proxyEP;

        public uint sequence;
        public byte[] usecircuit;
        public EndPoint userEP;
    }
}