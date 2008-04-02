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
using System.Data;
using System.IO;

namespace OpenSim.Framework.Data.Base
{
    public abstract class BaseDataReader
    {
        private readonly IDataReader m_source;

        public BaseDataReader(IDataReader source)
        {
            m_source = source;
        }

        public object Get(string name)
        {
            return m_source[name];
        }

        public ushort GetUShort(string name)
        {
            return (ushort)m_source.GetInt32(m_source.GetOrdinal(name));
        }

        public byte GetByte(string name)
        {
            int ordinal = m_source.GetOrdinal(name);
            byte value = (byte)m_source.GetInt16(ordinal);
            return value;
        }

        public sbyte GetSByte(string name)
        {
            return (sbyte)m_source.GetInt16(m_source.GetOrdinal(name));
        }

        public float GetFloat(string name)
        {
            return m_source.GetFloat(m_source.GetOrdinal(name));
        }

        public byte[] GetBytes(string name)
        {
            int ordinal = m_source.GetOrdinal(name);

            if (m_source.GetValue(ordinal) == DBNull.Value)
            {
                return null;
            }

            byte[] buffer = new byte[16384];

            MemoryStream memStream = new MemoryStream();

            long totalRead = 0;

            int bytesRead;
            do
            {
                bytesRead = (int)m_source.GetBytes(ordinal, totalRead, buffer, 0, buffer.Length);
                totalRead += bytesRead;

                memStream.Write(buffer, 0, bytesRead);
            } while (bytesRead == buffer.Length);

            return memStream.ToArray();
        }

        public string GetString(string name)
        {
            int ordinal = m_source.GetOrdinal(name);
            object value = m_source.GetValue(ordinal);

            if (value is DBNull)
            {
                return null;
            }

            return (string)value;
        }

        public bool Read()
        {
            return m_source.Read();
        }

        public virtual Guid GetGuid(string name)
        {
            return m_source.GetGuid(m_source.GetOrdinal(name));
        }

        public UInt32 GetUInt32(string name )
        {
            return (UInt32)GetInt32(name);
        }

        private Int32 GetInt32(string name)
        {
            int ordinal = m_source.GetOrdinal(name);
            int int32 = m_source.GetInt32(ordinal);
            return int32;            
        }

        public Int64 GetInt64(string  name)
        {
            int ordinal = m_source.GetOrdinal( name );
            long int64 = m_source.GetInt64(ordinal);
            return int64;
        }
    }
}
