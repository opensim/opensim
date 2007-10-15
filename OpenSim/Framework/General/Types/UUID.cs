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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    class UUID
    {
        public LLUUID llUUID;

        public UUID(string uuid)
        {
            llUUID = new LLUUID(uuid);
        }

        public UUID(byte[] uuid)
        {
            llUUID = new LLUUID(uuid, 0);
        }

        public UUID(byte[] uuid, int offset)
        {
            llUUID = new LLUUID(uuid, offset);
        }

        public UUID()
        {
            llUUID = LLUUID.Zero;
        }

        public UUID(ulong uuid)
        {
            llUUID = new LLUUID(uuid);
        }

        public UUID(UInt32 first, UInt32 second, UInt32 third, UInt32 fourth)
        {
            byte[] uuid = new byte[16];

            byte[] n = BitConverter.GetBytes(first);
            n.CopyTo(uuid, 0);
            n = BitConverter.GetBytes(second);
            n.CopyTo(uuid, 4);
            n = BitConverter.GetBytes(third);
            n.CopyTo(uuid, 8);
            n = BitConverter.GetBytes(fourth);
            n.CopyTo(uuid, 12);

            llUUID = new LLUUID(uuid,0);
        }

        public override string ToString()
        {
            return llUUID.ToString();
        }

        public string ToStringHyphenated()
        {
            return llUUID.ToStringHyphenated();
        }

        public byte[] GetBytes()
        {
            return llUUID.GetBytes();
        }

        public UInt32[] GetInts()
        {
            UInt32[] ints = new UInt32[4];
            ints[0] = BitConverter.ToUInt32(llUUID.Data, 0);
            ints[1] = BitConverter.ToUInt32(llUUID.Data, 4);
            ints[2] = BitConverter.ToUInt32(llUUID.Data, 8);
            ints[3] = BitConverter.ToUInt32(llUUID.Data, 12);

            return ints;
        }

        public LLUUID GetLLUUID()
        {
            return llUUID;
        }

        public uint CRC()
        {
            return llUUID.CRC();
        }

        public override int GetHashCode()
        {
            return llUUID.GetHashCode();
        }

        public void Combine(UUID other)
        {
            llUUID.Combine(other.GetLLUUID());
        }

        public void Combine(LLUUID other)
        {
            llUUID.Combine(other);
        }

        public override bool Equals(Object other)
        {
            return llUUID.Equals(other);
        }

        public static bool operator ==(UUID a, UUID b)
        {
            return a.llUUID.Equals(b.GetLLUUID());
        }

        public static bool operator !=(UUID a, UUID b)
        {
            return !a.llUUID.Equals(b.GetLLUUID());
        }

        public static bool operator ==(UUID a, LLUUID b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(UUID a, LLUUID b)
        {
            return !a.Equals(b);
        }
    }
}
