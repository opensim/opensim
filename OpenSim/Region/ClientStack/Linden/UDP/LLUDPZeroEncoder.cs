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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Text;
using OpenSim.Framework;
using Nini.Config;
using OpenMetaverse;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public sealed class LLUDPZeroEncoder
    {
        private byte[] m_tmp = new byte[16];
        private byte[] m_dest;
        private int zerocount;
        private int pos;

        public LLUDPZeroEncoder()
        {
        }

        public LLUDPZeroEncoder(byte[] data)
        {
            m_dest = data;
            zerocount = 0;
        }

        public byte[] Data
        {
            get
            {
                return m_dest;
            }
            set
            {
                m_dest = value;
            }
        }

        public int ZeroCount
        {
            get
            {
                return zerocount;
            }
            set
            {
                zerocount = value;
            }
        }

        public int Position
        {
            get
            {
                return pos;
            }
            set
            {
                pos = value;
            }
        }

        public unsafe void AddZeros(int len)
        {
            zerocount += len;
            while (zerocount > 255)
            {
                m_dest[pos++] = 0x00;
                m_dest[pos++] = 0xff;
                zerocount -= 256;
            }
        }

        public unsafe int Finish()
        {
            if(zerocount > 0)
            {
                m_dest[pos++] = 0x00;
                m_dest[pos++] = (byte)zerocount;
            }
            return pos;
        }

        public unsafe void AddBytes(byte[] src, int srclen)
        {
            for (int i = 0; i < srclen; ++i)
            {
                if (src[i] == 0x00)
                {
                    zerocount++;
                    if (zerocount == 0)
                    {
                        m_dest[pos++] = 0x00;
                        m_dest[pos++] = 0xff;
                        zerocount++;
                    }
                }
                else
                {
                    if (zerocount != 0)
                    {
                        m_dest[pos++] = 0x00;
                        m_dest[pos++] = (byte)zerocount;
                        zerocount = 0;
                    }

                    m_dest[pos++] = src[i];
                }
            }
        }

        public unsafe void AddByte(byte v)
        {
            if (v == 0x00)
            {
                zerocount++;
                if (zerocount == 0)
                {
                    m_dest[pos++] = 0x00;
                    m_dest[pos++] = 0xff;
                    zerocount++;
                }
            }
            else
            {
                if (zerocount != 0)
                {
                    m_dest[pos++] = 0x00;
                    m_dest[pos++] = (byte)zerocount;
                    zerocount = 0;
                }

                m_dest[pos++] = v;
            }
        }

        public void AddInt16(short v)
        {
            if (v == 0)
                AddZeros(2);
            else
            {
                Utils.Int16ToBytes(v, m_tmp, 0);
                AddBytes(m_tmp, 2);
            }
        }

        public void AddUInt16(ushort v)
        {
            if (v == 0)
                AddZeros(2);
            else
            {
                Utils.UInt16ToBytes(v, m_tmp, 0);
                AddBytes(m_tmp, 2);
            }
        }

        public void AddInt(int v)
        {
            if (v == 0)
                AddZeros(4);
            else
            {
                Utils.IntToBytesSafepos(v, m_tmp, 0);
                AddBytes(m_tmp, 4);
            }
        }

        public unsafe void AddUInt(uint v)
        {
            if (v == 0)
                AddZeros(4);
            else
            {
                Utils.UIntToBytesSafepos(v, m_tmp, 0);
                AddBytes(m_tmp, 4);
            }
        }

        public void AddFloatToUInt16(float v, float range)
        {
            Utils.FloatToUInt16Bytes(v, range, m_tmp, 0);
            AddBytes(m_tmp, 2);
        }

        public void AddFloat(float v)
        {
            if (v == 0f)
                AddZeros(4);
            else
            {
                Utils.FloatToBytesSafepos(v, m_tmp, 0);
                AddBytes(m_tmp, 4);
            }
        }

        public void AddInt64(long v)
        {
            if (v == 0)
                AddZeros(8);
            else
            {
                Utils.Int64ToBytesSafepos(v, m_tmp, 0);
                AddBytes(m_tmp, 8);
            }
        }

        public void AddUInt64(ulong v)
        {
            if (v == 0)
                AddZeros(8);
            else
            {
                Utils.UInt64ToBytesSafepos(v, m_tmp, 0);
                AddBytes(m_tmp, 8);
            }
        }

        public void AddVector3(Vector3 v)
        {
            if (v == Vector3.Zero)
                AddZeros(12);
            else
            {
                v.ToBytes(m_tmp, 0);
                AddBytes(m_tmp, 12);
            }
        }

        public void AddVector4(Vector4 v)
        {
            if (v == Vector4.Zero)
                AddZeros(16);
            else
            {
                v.ToBytes(m_tmp, 0);
                AddBytes(m_tmp, 16);
            }
        }

        public void AddNormQuat(Quaternion v)
        {
            v.ToBytes(m_tmp, 0);
            AddBytes(m_tmp, 12);
        }

        public void AddUUID(UUID v)
        {
            v.ToBytes(m_tmp, 0);
            AddBytes(m_tmp, 16);
        }

        // maxlen <= 255 and includes null termination byte
        public void AddShortString(string str, int maxlen)
        {
            if (String.IsNullOrEmpty(str))
            {
                AddZeros(1);
                return;
            }

            --maxlen; // account for null term
            bool NullTerm = str.EndsWith("\0");

            byte[] data = Util.UTF8.GetBytes(str);
            int len = data.Length;
            if(NullTerm)
                --len;

            if(len <= maxlen)
            {
                AddByte((byte)(len + 1));
                AddBytes(data, len);
                AddZeros(1);
                return;
            }

            if ((data[maxlen] & 0x80) != 0)
            {
                while (maxlen > 0 && (data[maxlen] & 0xc0) != 0xc0)
                    maxlen--;
            }
            AddByte((byte)(maxlen + 1));
            AddBytes(data, maxlen);
            AddZeros(1);
        }
        // maxlen <= 255 and includes null termination byte, maxchars == max len of utf8 source
        public void AddShortString(string str, int maxchars, int maxlen)
        {
            if (String.IsNullOrEmpty(str))
            {
                AddZeros(1);
                return;
            }

            --maxlen; // account for null term
            bool NullTerm = false;
            byte[] data;

            if (str.Length > maxchars)
            {
                data = Util.UTF8.GetBytes(str.Substring(0,maxchars));
            }
            else
            {
                NullTerm = str.EndsWith("\0");
                data = Util.UTF8.GetBytes(str);
            }

            int len = data.Length;
            if (NullTerm)
                --len;

            if (len <= maxlen)
            {
                AddByte((byte)(len + 1));
                AddBytes(data, len);
                AddZeros(1);
                return;
            }

            if ((data[maxlen] & 0x80) != 0)
            {
                while (maxlen > 0 && (data[maxlen] & 0xc0) != 0xc0)
                    maxlen--;
            }

            AddByte((byte)(maxlen + 1));
            AddBytes(data, maxlen);
            AddZeros(1);
        }

    }
}
