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
