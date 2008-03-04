/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
