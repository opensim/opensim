using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    class KillPacket : Packet
    {
        private Header header;
        public override void FromBytes(Header header, byte[] bytes, ref int i, ref int packetEnd, byte[] zeroBuffer)
        {
            
        }

        public override void FromBytes(byte[] bytes, ref int i, ref int packetEnd, byte[] zeroBuffer)
        {
            
        }
 
        public override Header Header { get { return header; } set { header = value; }}

        public override byte[] ToBytes()
        {
            return new byte[0];
        }
        public KillPacket()
        {
            Header = new LowHeader();
            Header.ID = 65531;
            Header.Reliable = true;
        }

        public override PacketType Type
        {
            get
            {
                return PacketType.UseCircuitCode;
            }
        }
    }
}
