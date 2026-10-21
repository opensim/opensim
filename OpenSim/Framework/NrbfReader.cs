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

// Simple decoder for NRBF, ie binaryformater data
// Avoids use of System.Formats.Nrbf that does not work on dotnet8
// Maybe replaced by it when we move to dotnet >= 10
// Only to use to try do legacy data. Not for new uses.

// AI generated code, not the best

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// A decoded NRBF class instance. Values are primitives, strings, byte[], object[],
    /// NrbfObject, NrbfRef or null
    /// </summary>
    public sealed class NrbfObject
    {
        public readonly string TypeName;
        public readonly string[] Names;
        public readonly object[] Values;

        public NrbfObject(string typeName, string[] names)
        {
            TypeName = typeName;
            Names = names;
            Values = new object[names.Length];
        }

        public object Get(string name)
        {
            int i = Array.IndexOf(Names, name);
            return i < 0 ? null : Values[i];
        }
    }

    /// <summary>
    /// A reference to a object in the payload, use NrbfPayload.Resolve to get it
    /// </summary>
    public sealed record NrbfRef(int Id);

    /// <summary>
    /// A decoded NRBF (BinaryFormatter) payload
    /// </summary>
    public sealed class NrbfPayload
    {
        public readonly int RootId;
        public readonly Dictionary<int, object> Objects;

        public NrbfPayload(int rootId, Dictionary<int, object> objects)
        {
            RootId = rootId;
            Objects = objects;
        }

        public object Root
        {
            get { return Objects.TryGetValue(RootId, out object o) ? o : null; }
        }

        public object Resolve(object v)
        {
            if (v is NrbfRef r)
                return Objects.TryGetValue(r.Id, out object o) ? o : null;
            return v;
        }

        public object Get(NrbfObject obj, string name)
        {
            return Resolve(obj.Get(name));
        }

        /// <summary>
        /// get a OpenMetaverse.UUID, serialized as a System.Guid field named Guid
        /// </summary>
        public bool TryGetUUID(object v, out UUID uuid)
        {
            //yeack...
            uuid = UUID.Zero;
            if (Resolve(v) is not NrbfObject u || Get(u, "Guid") is not NrbfObject g)
                return false;
            if (g.Get("_a") is not int ga || g.Get("_b") is not short gb || g.Get("_c") is not short gc)
                return false;

            Span<byte> b = stackalloc byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(b, ga);
            BinaryPrimitives.WriteInt16LittleEndian(b[4..], gb);
            BinaryPrimitives.WriteInt16LittleEndian(b[6..], gc);
            for (int i = 0; i < 8; ++i)
            {
                if (g.Get("_" + (char)('d' + i)) is not byte gx)
                    return false;
                b[8 + i] = gx;
            }
            uuid = new UUID(new Guid(b));
            return true;
        }
    }

    /// <summary>
    /// minimal MS-NRBF (BinaryFormatter) reader. Builds a graph of NrbfObject, arrays, strings and primitives.
    /// References are kept as NrbfRef, since they can point to objects defined later.
    /// Nothing is instanciated except primitives, strings and arrays.
    /// </summary>
    public ref struct NrbfReader
    {
        private const int MAXDEPTH = 64;

        private record ClassInfo(string Name, string[] Names, byte[] BinaryTypes, object[] Extra);

        private readonly ReadOnlySpan<byte> m_data;
        private int m_pos;
        private int m_nulls;
        private readonly Dictionary<int, object> m_objects;
        private readonly Dictionary<int, ClassInfo> m_classes;

        private NrbfReader(ReadOnlySpan<byte> data)
        {
            m_data = data;
            m_pos = 0;
            m_nulls = 0;
            m_objects = new();
            m_classes = new();
        }

        /// <returns>false if data is not a valid or supported NRBF payload</returns>
        public static bool TryRead(ReadOnlySpan<byte> data, out NrbfPayload payload)
        {
            try
            {
                NrbfReader reader = new(data);
                payload = reader.Read();
                return true;
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        private NrbfPayload Read()
        {
            if (ReadByte() != 0) // SerializedStreamHeader
                throw new FormatException();
            int rootId = ReadInt32();
            ReadInt32(); // headerId
            if (ReadInt32() != 1 || ReadInt32() != 0) // version 1.0
                throw new FormatException();

            while (true)
            {
                byte recordType = ReadByte();
                if (recordType == 11) // MessageEnd
                    return new NrbfPayload(rootId, m_objects);
                ReadRecord(recordType, 0);
                m_nulls = 0;
            }
        }

        private object ReadRecord(byte recordType, int depth)
        {
            if (++depth > MAXDEPTH)
                throw new FormatException();

            int id;
            switch (recordType)
            {
                case 1: // ClassWithId
                {
                    id = ReadInt32();
                    if (!m_classes.TryGetValue(ReadInt32(), out ClassInfo ci))
                        throw new FormatException();
                    return ReadClassValues(id, ci, depth);
                }
                case 2: // SystemClassWithMembers
                case 3: // ClassWithMembers
                case 4: // SystemClassWithMembersAndTypes
                case 5: // ClassWithMembersAndTypes
                {
                    id = ReadInt32();
                    string name = ReadString();
                    string[] names = new string[ReadCount()];
                    for (int i = 0; i < names.Length; ++i)
                        names[i] = ReadString();

                    byte[] binaryTypes = null;
                    object[] extra = null;
                    if (recordType >= 4)
                    {
                        binaryTypes = Take(names.Length).ToArray();
                        extra = new object[names.Length];
                        for (int i = 0; i < names.Length; ++i)
                            extra[i] = ReadAdditionalInfo(binaryTypes[i]);
                    }
                    if (recordType == 3 || recordType == 5)
                        ReadInt32(); // libraryId

                    ClassInfo ci = new(name, names, binaryTypes, extra);
                    m_classes[id] = ci;
                    return ReadClassValues(id, ci, depth);
                }
                case 6: // BinaryObjectString
                {
                    id = ReadInt32();
                    string s = ReadString();
                    m_objects[id] = s;
                    return s;
                }
                case 7: // BinaryArray, only single dimension
                {
                    id = ReadInt32();
                    byte arrayType = ReadByte();
                    if (arrayType != 0 || ReadInt32() != 1)
                        throw new FormatException();
                    int len = ReadCount();
                    byte binaryType = ReadByte();
                    object info = ReadAdditionalInfo(binaryType);
                    object arr = binaryType == 0 ?
                        ReadPrimitiveArray((byte)info, len) :
                        ReadObjectArrayValues(len, depth);
                    m_objects[id] = arr;
                    return arr;
                }
                case 8: // MemberPrimitiveTyped
                    return ReadPrimitive(ReadByte());
                case 9: // MemberReference
                    return new NrbfRef(ReadInt32());
                case 10: // ObjectNull
                    return null;
                case 12: // BinaryLibrary, followed by the record that uses it
                    ReadInt32();
                    ReadString();
                    return ReadRecord(ReadByte(), depth);
                case 13: // ObjectNullMultiple256
                case 14: // ObjectNullMultiple
                {
                    int count = recordType == 13 ? ReadByte() : ReadInt32();
                    if (count < 1)
                        throw new FormatException();
                    m_nulls = count - 1;
                    return null;
                }
                case 15: // ArraySinglePrimitive
                {
                    id = ReadInt32();
                    int len = ReadCount();
                    object arr = ReadPrimitiveArray(ReadByte(), len);
                    m_objects[id] = arr;
                    return arr;
                }
                case 16: // ArraySingleObject
                case 17: // ArraySingleString
                {
                    id = ReadInt32();
                    object[] arr = ReadObjectArrayValues(ReadCount(), depth);
                    m_objects[id] = arr;
                    return arr;
                }
                default:
                    throw new FormatException();
            }
        }

        private NrbfObject ReadClassValues(int id, ClassInfo ci, int depth)
        {
            NrbfObject obj = new(ci.Name, ci.Names);
            m_objects[id] = obj;
            for (int i = 0; i < ci.Names.Length; ++i)
            {
                if (m_nulls > 0)
                {
                    --m_nulls;
                    continue;
                }
                if (ci.BinaryTypes is not null && ci.BinaryTypes[i] == 0)
                    obj.Values[i] = ReadPrimitive((byte)ci.Extra[i]);
                else
                    obj.Values[i] = ReadRecord(ReadByte(), depth);
            }
            m_nulls = 0;
            return obj;
        }

        private object[] ReadObjectArrayValues(int len, int depth)
        {
            object[] arr = new object[len];
            for (int i = 0; i < len; ++i)
            {
                if (m_nulls > 0)
                {
                    --m_nulls;
                    continue;
                }
                arr[i] = ReadRecord(ReadByte(), depth);
            }
            m_nulls = 0;
            return arr;
        }

        private object ReadPrimitiveArray(byte primitiveType, int len)
        {
            if (primitiveType == 2) // Byte
                return Take(len).ToArray();

            object[] arr = new object[len];
            for (int i = 0; i < len; ++i)
                arr[i] = ReadPrimitive(primitiveType);
            return arr;
        }

        private object ReadAdditionalInfo(byte binaryType)
        {
            switch (binaryType)
            {
                case 0: // Primitive
                case 7: // PrimitiveArray
                    return ReadByte();
                case 3: // SystemClass
                    return ReadString();
                case 4: // Class
                    string name = ReadString();
                    ReadInt32(); // libraryId
                    return name;
                case 1: // String
                case 2: // Object
                case 5: // ObjectArray
                case 6: // StringArray
                    return null;
                default:
                    throw new FormatException();
            }
        }

        private object ReadPrimitive(byte primitiveType)
        {
            switch (primitiveType)
            {
                case 1: return ReadByte() != 0;
                case 2: return ReadByte();
                case 3:
                {
                    if (m_pos >= m_data.Length)
                        throw new FormatException();
                    byte c = m_data[m_pos];
                    int len = c < 0x80 ? 1 : c < 0xE0 ? 2 : c < 0xF0 ? 3 : 4;
                    return Encoding.UTF8.GetString(Take(len))[0];
                }
                case 5: return decimal.Parse(ReadString(), CultureInfo.InvariantCulture);
                case 6: return BinaryPrimitives.ReadDoubleLittleEndian(Take(8));
                case 7: return BinaryPrimitives.ReadInt16LittleEndian(Take(2));
                case 8: return ReadInt32();
                case 9: return BinaryPrimitives.ReadInt64LittleEndian(Take(8));
                case 10: return (sbyte)ReadByte();
                case 11: return BinaryPrimitives.ReadSingleLittleEndian(Take(4));
                case 12: return new TimeSpan(BinaryPrimitives.ReadInt64LittleEndian(Take(8)));
                case 13:
                {
                    ulong v = BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
                    ulong kind = v >> 62;
                    return new DateTime((long)(v & 0x3FFFFFFFFFFFFFFFUL),
                        kind == 0 ? DateTimeKind.Unspecified : kind == 1 ? DateTimeKind.Utc : DateTimeKind.Local);
                }
                case 14: return BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
                case 15: return BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
                case 16: return BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
                case 17: return null;
                case 18: return ReadString();
                default:
                    throw new FormatException();
            }
        }

        private ReadOnlySpan<byte> Take(int len)
        {
            if (len < 0 || len > m_data.Length - m_pos)
                throw new FormatException();
            ReadOnlySpan<byte> s = m_data.Slice(m_pos, len);
            m_pos += len;
            return s;
        }

        private byte ReadByte()
        {
            if (m_pos >= m_data.Length)
                throw new FormatException();
            return m_data[m_pos++];
        }

        private int ReadInt32()
        {
            return BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        }

        // element counts, each element uses at least one byte
        private int ReadCount()
        {
            int n = ReadInt32();
            if (n < 0 || n > m_data.Length - m_pos)
                throw new FormatException();
            return n;
        }

        // 7 bit encoded length prefixed UTF8 string
        private string ReadString()
        {
            int len = 0;
            for (int shift = 0; ; shift += 7)
            {
                if (shift > 28)
                    throw new FormatException();
                byte b = ReadByte();
                len |= (b & 0x7f) << shift;
                if ((b & 0x80) == 0)
                    break;
            }
            return Encoding.UTF8.GetString(Take(len));
        }
    }
}
