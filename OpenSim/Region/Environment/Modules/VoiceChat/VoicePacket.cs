using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Environment.Modules.VoiceChat
{
    public enum VoiceCodec
    {
        None = 0,
        PCM8 = 1 << 0,
        PCM16 = 1 << 1,
        PCM32 = 1 << 2,
        Speex = 1 << 3,
    }

    public class VoicePacket
    {
        public LLUUID m_clientId;
        byte[] m_audioData;
        public int m_codec;

        public VoicePacket(byte[] data)
        {
            int pos = 0;
            m_codec = data[pos++];
            m_codec |= data[pos++] << 8;
            m_codec |= data[pos++] << 16;
            m_codec |= data[pos++] << 24;

            m_audioData = new byte[data.Length - pos];
            Buffer.BlockCopy(data, pos, m_audioData, 0, data.Length - pos);
        }

        public byte[] GetBytes()
        {
            VoicePacketHeader header = new VoicePacketHeader();
            byte[] bytes = new byte[5+16+4+m_audioData.Length];

            header.length = bytes.Length-5;
            
            //ToClient packets are type 2
            header.type = 2;

            int pos = 0;
            header.CopyTo(bytes, pos); pos += 5;
            m_clientId.GetBytes().CopyTo(bytes, pos); pos += 16;

            bytes[pos++] = (byte)((m_codec) % 256);
            bytes[pos++] = (byte)((m_codec << 8) % 256);
            bytes[pos++] = (byte)((m_codec << 16) % 256);
            bytes[pos++] = (byte)((m_codec << 24) % 256);

            m_audioData.CopyTo(bytes, pos);
            return bytes;
        }
    }
}
