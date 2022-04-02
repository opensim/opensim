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

using System.Runtime.CompilerServices;
using System.Threading;

namespace OpenSim.Framework
{
    public class TerrainTaintsArray
    {
        public const int VectorNumberBits = 32;
        public const int VectorNumberBitsLog2 = 5;
        public const int FALSEWORD = 0;
        public const int TRUEWORD = ~FALSEWORD;

        private int[] m_data;
        private readonly int m_nbits;

        private volatile int m_ntainted;

        private object m_mainlock = new object();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TerrainTaintsArray(int lenght) : this(lenght, false) { }

        public TerrainTaintsArray(int lenght, bool preset)
        {
            m_nbits = lenght;
            int nInts = calclen(m_nbits);

            m_data = new int[nInts];
            if (preset)
            {
                for (int i = 0; i < m_data.Length; i++)
                    m_data[i] = TRUEWORD;
                m_ntainted = m_nbits;
            }
            else
                m_ntainted = 0;
        }

        public int Length
        {
            get
            {
                return m_nbits;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int TaitedCount()
        {
            return m_ntainted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsTaited()
        {
            return m_ntainted > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int bitindex)
        {
            int indexh = bitindex >> VectorNumberBitsLog2;
            int mask = 1 << (bitindex & (VectorNumberBits - 1));
            return (m_data[indexh] & mask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int bitindex, bool clear)
        {
            int indexh = bitindex >> VectorNumberBitsLog2;
            int mask = 1 << (bitindex & (VectorNumberBits - 1));

            lock (m_mainlock)
            {
                if ((m_data[indexh] & mask) != 0)
                {
                    if (clear)
                    {
                        m_data[indexh] ^= mask;
                        --m_ntainted;
                    }
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool GetAndClear(int bitindex)
        {
            int indexh = bitindex >> VectorNumberBitsLog2;
            int mask = 1 << (bitindex & (VectorNumberBits - 1));
            lock (m_mainlock)
            {
                if ((m_data[indexh] & mask) != 0)
                {
                    m_data[indexh] ^= mask;
                    --m_ntainted;
                    return true;
                }
            }
            return false;
        }

        public void Set(int bitindex, bool val)
        {
            int indexh = bitindex >> VectorNumberBitsLog2;
            int mask = 1 << (bitindex & (VectorNumberBits - 1));
            lock (m_mainlock)
            {
                if (val)
                {
                    if ((m_data[indexh] & mask) == 0)
                    {
                        m_data[indexh] |= mask;
                        ++m_ntainted;
                    }
                }
                else
                {
                    if ((m_data[indexh] & mask) != 0)
                    {
                        m_data[indexh] ^= mask;
                        --m_ntainted;
                    }
                }
            }
        }

        public bool this[int bitindex]
        {
            get
            {
                return Get(bitindex);
            }
            set
            {
                int indexh = bitindex >> VectorNumberBitsLog2;
                int mask = 1 << (bitindex & (VectorNumberBits - 1));
                lock (m_mainlock)
                {
                    if (value)
                    {
                        if ((m_data[indexh] & mask) == 0)
                        {
                            m_data[indexh] |= mask;
                            ++m_ntainted;
                        }
                    }
                    else
                    {
                        if ((m_data[indexh] & mask) != 0)
                        {
                            m_data[indexh] ^= mask;
                            --m_ntainted;
                        }
                    }
                }
            }
        }

        public void SetAll(bool val)
        {
            lock (m_mainlock)
            {
                if (val)
                {
                    for (int i = 0; i < m_data.Length; ++i)
                        m_data[i] = TRUEWORD;
                    m_ntainted = m_nbits;

                }
                else
                {
                    for (int i = 0; i < m_data.Length; ++i)
                        m_data[i] = 0;
                    m_ntainted = 0;
                }
            }
        }

        public bool IsVectorOfFalse(int vectorIndex)
        {
            return m_data[vectorIndex] == 0;
        }

        public bool IsVectorOfBitFalse(int bitindex)
        {
            return m_data[(bitindex >> VectorNumberBitsLog2)] == 0;
        }


        public bool IsVectorTrue(int vectorIndex)
        {
            return m_data[vectorIndex] == unchecked(((int)0xffffffff));
        }

        public bool IsVectorOfBitTrue(int bitindex)
        {
            return m_data[(bitindex >> VectorNumberBitsLog2)] == unchecked(((int)0xffffffff));
        }

        public void Or(TerrainTaintsArray other)
        {
            if (m_nbits != other.m_nbits)
                return;
            lock (m_mainlock)
            {
                lock (other.m_mainlock)
                {
                    for (int i = 0; i < m_data.Length; ++i)
                    {
                        int tr = other.m_data[i];
                        if (tr == 0)
                            continue;

                        int tt = m_data[i] | tr;
                        tr ^= tt;
                        if (tr != 0)
                        {
                            m_data[i] = tt;
                            for (int j = 1; j != 0; j <<= 1)
                            {
                                if ((tr & j) != 0)
                                    ++m_ntainted;
                            }
                        }
                    }
                }
            }
        }

        public int GetNextTrue(int startBitIndex)
        {
            if (startBitIndex < 0 || startBitIndex >= m_nbits)
                return -1;

            int indexh = startBitIndex >> VectorNumberBitsLog2;
            int j = startBitIndex & (VectorNumberBits - 1);

            int cur = m_data[indexh];
            if (cur != 0)
            {
                for (; j < VectorNumberBits; ++j)
                {
                    if ((cur & (1 << j)) != 0)
                        return (indexh << VectorNumberBitsLog2) | j;
                }
            }
            while (++indexh < m_data.Length)
            {
                cur = m_data[indexh];
                if (cur != 0)
                {
                    for (j = 0; j < VectorNumberBits; ++j)
                    {
                        if ((cur & (1 << j)) != 0)
                            return (indexh << VectorNumberBitsLog2) | j;
                    }
                }
            }
            return -1;
        }

        public int GetAndClearNextTrue(int startBitIndex)
        {
            if (m_ntainted <= 0 || startBitIndex < 0 || startBitIndex >= m_nbits)
                return -1;

            int indexh = startBitIndex >> VectorNumberBitsLog2;
            int j = startBitIndex & (VectorNumberBits - 1);
            lock (m_mainlock)
            {
                int cur = m_data[indexh];
                if (cur != 0)
                {
                    for (; j < VectorNumberBits; ++j)
                    {
                        int mask = (1 << j);
                        if ((cur & mask) != 0)
                        {
                            m_data[indexh] ^= mask;
                            --m_ntainted;
                            return (indexh << VectorNumberBitsLog2) | j;
                        }
                    }
                }

                while (++indexh < m_data.Length)
                {
                    cur = m_data[indexh];
                    if (cur != 0)
                    {
                        for (j = 0; j < VectorNumberBits; ++j)
                        {
                            int mask = (1 << j);
                            if ((cur & mask) != 0)
                            {
                                m_data[indexh] ^= mask;
                                --m_ntainted;
                                return (indexh << VectorNumberBitsLog2) | j;
                            }
                        }
                    }
                }
            }
            return -1;
        }


        private int calclen(int bitsLen)
        {
            return bitsLen > 0 ? ((bitsLen - 1) >> VectorNumberBitsLog2) + 1 : 0;
        }
    }
}