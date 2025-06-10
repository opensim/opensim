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
using System.Runtime.CompilerServices;
using OpenSim.Framework;
using OpenMetaverse;
using System.Runtime.InteropServices;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLUDPZeroEncoder
    {
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddZeros(int len)
        {
            zerocount += len;
            ref byte dst = ref MemoryMarshal.GetArrayDataReference(m_dest);
            while (zerocount > 0xff)
            {
                Unsafe.Add(ref dst, pos) = 0x00;
                pos++;
                Unsafe.Add(ref dst, pos) = 0xff;
                pos++;
                zerocount -= 256;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Finish()
        {
            if (zerocount > 0)
            {
                ref byte dst = ref MemoryMarshal.GetArrayDataReference(m_dest);
                Unsafe.Add(ref dst, pos) = 0x00;
                pos++;
                Unsafe.Add(ref dst, pos) = (byte)zerocount;
                pos++;
            }
            return pos;
        }

        public unsafe void AddBytes(byte[] src, int srclen)
        {
            ref byte dst = ref MemoryMarshal.GetArrayDataReference(m_dest);
            for (int i = 0; i < srclen; ++i)
            {
                byte b = src[i];
                if (b == 0x00)
                {
                    if (zerocount != 0xff)
                        zerocount++;
                    else
                    {
                        Unsafe.Add(ref dst, pos) = 0x00;
                        pos++;
                        Unsafe.Add(ref dst, pos) = 0xff;
                        pos++;
                        zerocount = 1;
                    }
                }
                else
                {
                    if (zerocount != 0)
                    {
                        Unsafe.Add(ref dst, pos) = 0x00;
                        pos++;
                        Unsafe.Add(ref dst, pos) = (byte)zerocount;
                        pos++;
                        zerocount = 0;
                    }
                    Unsafe.Add(ref dst, pos) = b;
                    pos++;
                }
            }
        }

        public unsafe void AddBytes(byte* src, int srclen)
        {
            ref byte dst = ref MemoryMarshal.GetArrayDataReference(m_dest);
            for (int i = 0; i < srclen; ++i)
            {
                if (src[i] == 0x00)
                {
                    if (zerocount != 0xff)
                        zerocount++;
                    else
                    {
                        Unsafe.Add(ref dst, pos) = 0x00;
                        pos++;
                        Unsafe.Add(ref dst, pos) = 0xff;
                        pos++;
                        zerocount = 1;
                    }
                }
                else
                {
                    if (zerocount != 0)
                    {
                        Unsafe.Add(ref dst, pos) = 0x00;
                        pos++;
                        Unsafe.Add(ref dst, pos) = (byte)zerocount;
                        pos++;
                        zerocount = 0;
                    }
                    Unsafe.Add(ref dst, pos) = src[i];
                    pos++;
                }
            }
        }

        public void AddByte(byte v)
        {
            if (v == 0x00)
            {
                if (zerocount != 0xff)
                    zerocount++;
                else
                {
                    ref byte dst = ref MemoryMarshal.GetArrayDataReference(m_dest);
                    Unsafe.Add(ref dst, pos) = 0x00;
                    pos++;
                    Unsafe.Add(ref dst, pos) = 0xff;
                    pos++;
                    zerocount = 1;
                }
            }
            else
            {
                ref byte dst = ref MemoryMarshal.GetArrayDataReference(m_dest);
                if (zerocount != 0)
                {
                    Unsafe.Add(ref dst, pos) = 0x00;
                    pos++;
                    Unsafe.Add(ref dst, pos) = (byte)zerocount;
                    pos++;
                    zerocount = 0;
                }
                Unsafe.Add(ref dst, pos) = v;
                pos++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddInt16(short v)
        {
            if (v == 0)
                AddZeros(2);
            else
            {
                byte* b = stackalloc byte[2];
                Utils.Int16ToBytes(v, b);
                AddBytes(b, 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddUInt16(ushort v)
        {
            if (v == 0)
                AddZeros(2);
            else
            {
                byte* b = stackalloc byte[2];
                Utils.UInt16ToBytes(v, b);
                AddBytes(b, 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddInt(int v)
        {
            if (v == 0)
                AddZeros(4);
            else
            {
                byte* b = stackalloc byte[4];
                Utils.IntToBytes(v, b);
                AddBytes(b, 4);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddUInt(uint v)
        {
            if (v == 0)
                AddZeros(4);
            else
            {
                byte* b = stackalloc byte[4];
                Utils.UIntToBytes(v, b);
                AddBytes(b, 4);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddFloatToUInt16(float v, float range)
        {
            byte* b = stackalloc byte[2];
            Utils.FloatToUInt16Bytes(v, range, b);
            AddBytes(b, 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddFloat(float v)
        {
            if (v == 0f)
                AddZeros(4);
            else
            {
                byte* b = stackalloc byte[4];
                Utils.FloatToBytes(v, b);
                AddBytes(b, 4);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddInt64(long v)
        {
            if (v == 0)
                AddZeros(8);
            else
            {
                byte* b = stackalloc byte[8];
                Utils.Int64ToBytes(v, b);
                AddBytes(b, 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddUInt64(ulong v)
        {
            if (v == 0)
                AddZeros(8);
            else
            {
                byte* b = stackalloc byte[8];
                Utils.UInt64ToBytes(v, b);
                AddBytes(b, 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddVector3(Vector3 v)
        {
            if (v.IsZero())
                AddZeros(12);
            else
            {
                byte* b = stackalloc byte[12];
                v.ToBytes(b);
                AddBytes(b, 12);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddVector4(Vector4 v)
        {
            if (v.IsZero())
                AddZeros(16);
            else
            {
                byte* b = stackalloc byte[16];
                v.ToBytes(b);
                AddBytes(b, 16);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddNormQuat(Quaternion v)
        {
            byte* b = stackalloc byte[12];
            v.ToBytes(b);
            AddBytes(b, 12);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddUUID(UUID v)
        {
            byte* b = stackalloc byte[16];
            v.ToBytes(b);
            AddBytes(b, 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddColorArgb(int argb)
        {
            uint ua = (uint)argb ^ 0xff000000;
            if(ua == 0)
            {
                AddZeros(4);
            }
            else
            {
                AddByte((byte)(ua >> 16));
                AddByte((byte)(ua >> 8));
                AddByte((byte)ua);
                AddByte((byte)(ua >> 24));
            }

        }
        // maxlen <= 255 and includes null termination byte
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddShortString(string str, int maxlen)
        {
            if (String.IsNullOrEmpty(str))
            {
                AddZeros(1);
                return;
            }

            byte* data = stackalloc byte[maxlen];
            int len = Util.osUTF8Getbytes(str, data, maxlen, true);

            if (len == 0)
            {
                AddZeros(1);
                return;
            }

            AddByte((byte)(len));
            AddBytes(data, len);
        }

        // maxlen <= 255 and includes null termination byte, maxchars == max len of utf16 source
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddShortString(string str, int maxchars, int maxlen)
        {
            if (String.IsNullOrEmpty(str))
            {
                AddZeros(1);
                return;
            }

            if (str.Length > maxchars)
                str = str.Substring(0, maxchars);

            byte* data = stackalloc byte[maxlen];
            int len = Util.osUTF8Getbytes(str, data, maxlen, true);

            if (len == 0)
            {
                AddZeros(1);
                return;
            }

            AddByte((byte)(len));
            AddBytes(data, len);
        }

        // maxlen <= 254 because null termination byte
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddShortLimitedUTF8(osUTF8 str)
        {
            if (str == null)
            {
                AddZeros(1);
                return;
            }

            int len = str.Length;
            if (len == 0)
            {
                AddZeros(1);
                return;
            }

            AddByte((byte)(len + 1)); // add null
            AddBytes(str.GetArray(), len);
            AddZeros(1);
        }
    }
    public unsafe class LLUDPUnsafeZeroEncoder
    {
        private byte* m_destStart;
        private int m_zerocount;
        private byte* m_dest;

        public LLUDPUnsafeZeroEncoder()
        {
        }

        public LLUDPUnsafeZeroEncoder(byte* data)
        {
            m_destStart = data;
            m_dest = data;
            m_zerocount = 0;
        }

        public byte* Data
        {
            get
            {
                return m_destStart;
            }
            set
            {
                m_destStart = value;
                m_dest = value;
            }
        }

        public int ZeroCount
        {
            get
            {
                return m_zerocount;
            }
            set
            {
                m_zerocount = value;
            }
        }

        public int Position
        {
            get
            {
                return (int)(m_dest - m_destStart);
            }
            set
            {
                m_dest = m_destStart + value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddZeros(int len)
        {
            m_zerocount += len;
            while (m_zerocount > 0xff)
            {
                *m_dest++ = 0x00;
                *m_dest++ = 0xff;
                m_zerocount -= 256;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe int Finish()
        {
            if (m_zerocount > 0)
            {
                *m_dest++ = 0x00;
                *m_dest++ = (byte)m_zerocount;
            }
            return (int)(m_dest - m_destStart);
        }

        public unsafe void AddBytes(byte[] src, int srclen)
        {
            for (int i = 0; i < srclen; ++i)
            {
                if (src[i] == 0x00)
                {
                    if (m_zerocount != 0xff)
                        m_zerocount++;
                    else
                    {
                        *m_dest++ = 0x00;
                        *m_dest++ = 0xff;
                        m_zerocount = 1;
                    }
                }
                else
                {
                    if (m_zerocount != 0)
                    {
                        *m_dest++ = 0x00;
                        *m_dest++ = (byte)m_zerocount;
                        m_zerocount = 0;
                    }
                    *m_dest++ = src[i];
                }
            }
        }

        public unsafe void AddBytes(byte* src, int srclen)
        {
            for (int i = 0; i < srclen; ++i)
            {
                if (src[i] == 0x00)
                {
                    if (m_zerocount != 0xff)
                        m_zerocount++;
                    else
                    {
                        *m_dest++ = 0x00;
                        *m_dest++ = 0xff;
                        m_zerocount = 1;
                    }
                }
                else
                {
                    if (m_zerocount != 0)
                    {
                        *m_dest++ = 0x00;
                        *m_dest++ = (byte)m_zerocount;
                        m_zerocount = 0;
                    }
                    *m_dest++ = src[i];
                }
            }
        }

        public unsafe void AddByte(byte v)
        {
            if (v == 0x00)
            {
                if (m_zerocount != 0xff)
                    m_zerocount++;
                else
                {
                    *m_dest++ = 0x00;
                    *m_dest++ = 0xff;
                    m_zerocount = 1;
                }
            }
            else
            {
                if (m_zerocount != 0)
                {
                    *m_dest++ = 0x00;
                    *m_dest++ = (byte)m_zerocount;
                    m_zerocount = 0;
                }
                *m_dest++ = v;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInt16(short v)
        {
            if (v == 0)
                AddZeros(2);
            else
            {
                byte* b = stackalloc byte[2];
                Utils.Int16ToBytes(v, b);
                AddBytes(b, 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddUInt16(ushort v)
        {
            if (v == 0)
                AddZeros(2);
            else
            {
                byte* b = stackalloc byte[2];
                Utils.UInt16ToBytes(v, b);
                AddBytes(b, 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInt(int v)
        {
            if (v == 0)
                AddZeros(4);
            else
            {
                byte* b = stackalloc byte[4];
                Utils.IntToBytes(v, b);
                AddBytes(b, 4);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddUInt(uint v)
        {
            if (v == 0)
                AddZeros(4);
            else
            {
                byte* b = stackalloc byte[4];
                Utils.UIntToBytes(v, b);
                AddBytes(b, 4);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFloatToUInt16(float v, float range)
        {
            byte* b = stackalloc byte[2];
            Utils.FloatToUInt16Bytes(v, range, b);
            AddBytes(b, 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddFloat(float v)
        {
            if (v == 0f)
                AddZeros(4);
            else
            {
                byte* b = stackalloc byte[4];
                Utils.FloatToBytes(v, b);
                AddBytes(b, 4);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddInt64(long v)
        {
            if (v == 0)
                AddZeros(8);
            else
            {
                byte* b = stackalloc byte[8];
                Utils.Int64ToBytes(v, b);
                AddBytes(b, 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddUInt64(ulong v)
        {
            if (v == 0)
                AddZeros(8);
            else
            {
                byte* b = stackalloc byte[8];
                Utils.UInt64ToBytes(v, b);
                AddBytes(b, 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddVector3(Vector3 v)
        {
            if (v.IsZero())
                AddZeros(12);
            else
            {
                byte* b = stackalloc byte[12];
                v.ToBytes(b);
                AddBytes(b, 12);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddVector4(Vector4 v)
        {
            if (v.IsZero())
                AddZeros(16);
            else
            {
                byte* b = stackalloc byte[16];
                v.ToBytes(b);
                AddBytes(b, 16);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNormQuat(Quaternion v)
        {
            byte* b = stackalloc byte[12];
            v.ToBytes(b);
            AddBytes(b, 12);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddUUID(UUID v)
        {
            byte* b = stackalloc byte[16];
            v.ToBytes(b);
            AddBytes(b, 16);
        }

        // maxlen <= 255 and includes null termination byte
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddShortString(string str, int maxlen)
        {
            if (String.IsNullOrEmpty(str))
            {
                AddZeros(1);
                return;
            }

            byte* data = stackalloc byte[maxlen];
            int len = Util.osUTF8Getbytes(str, data, maxlen, true);

            if (len == 0)
            {
                AddZeros(1);
                return;
            }

            AddByte((byte)(len));
            AddBytes(data, len);
        }

        // maxlen <= 255 and includes null termination byte, maxchars == max len of utf16 source
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddShortString(string str, int maxchars, int maxlen)
        {
            if (String.IsNullOrEmpty(str))
            {
                AddZeros(1);
                return;
            }

            if (str.Length > maxchars)
                str = str.Substring(0, maxchars);

            byte* data = stackalloc byte[maxlen];
            int len = Util.osUTF8Getbytes(str, data, maxlen, true);

            if (len == 0)
            {
                AddZeros(1);
                return;
            }

            AddByte((byte)(len));
            AddBytes(data, len);
        }

        // maxlen <= 254 because null termination byte
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddShortLimitedUTF8(osUTF8 str)
        {
            if (str == null)
            {
                AddZeros(1);
                return;
            }

            int len = str.Length;
            if (len == 0)
            {
                AddZeros(1);
                return;
            }

            AddByte((byte)(len + 1)); // add null
            AddBytes(str.GetArray(), len);
            AddZeros(1);
        }
    }
}