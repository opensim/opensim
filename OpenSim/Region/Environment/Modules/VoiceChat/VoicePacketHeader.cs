using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Environment.Modules.VoiceChat
{
    public class VoicePacketHeader
    {
        public byte type;
        public int length;

        public void Parse(byte[] data)
        {
            int offset = 0;
            type = data[offset++];

            length = data[offset++];
            length |= data[offset++] << 8;
            length |= data[offset++] << 16;
            length |= data[offset++] << 24;
        }

        public void CopyTo(byte[] data, int offset)
        {
            data[offset + 0] = type;
            
            data[offset + 1] = (byte)(length & 0x000000FF);
            data[offset + 2] = (byte)((length & 0x0000FF00) >> 8);
            data[offset + 3] = (byte)((length & 0x00FF0000) >> 16);
            data[offset + 4] = (byte)((length & 0xFF000000) >> 24);
        }

        public int GetLength()
        {
            return 5;
        }
    }
}
