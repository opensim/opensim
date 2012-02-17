// adapted from libomv removing cpu endian adjust
// for prims lowlevel serialization

using System;
using System.IO;
using OpenMetaverse;

namespace OpenSim.Region.Physics.OdePlugin
{
    public class wstreamer
    {
        private MemoryStream st;

        public wstreamer()
        {
            st = new MemoryStream();
        }

        public byte[] close()
        {
            byte[] data = st.ToArray();
            st.Close();
            return data;
        }

        public void Wshort(short value)
        {
            st.Write(BitConverter.GetBytes(value), 0, 2);
        }
        public void Wushort(ushort value)
        {
            byte[] t = BitConverter.GetBytes(value);
            st.Write(BitConverter.GetBytes(value), 0, 2);
        }
        public void Wint(int value)
        {
            st.Write(BitConverter.GetBytes(value), 0, 4);
        }
        public void Wuint(uint value)
        {
            st.Write(BitConverter.GetBytes(value), 0, 4);
        }
        public void Wlong(long value)
        {
            st.Write(BitConverter.GetBytes(value), 0, 8);
        }
        public void Wulong(ulong value)
        {
            st.Write(BitConverter.GetBytes(value), 0, 8);
        }

        public void Wfloat(float value)
        {
            st.Write(BitConverter.GetBytes(value), 0, 4);
        }

        public void Wdouble(double value)
        {
            st.Write(BitConverter.GetBytes(value), 0, 8);
        }

        public void Wvector3(Vector3 value)
        {
            st.Write(BitConverter.GetBytes(value.X), 0, 4);
            st.Write(BitConverter.GetBytes(value.Y), 0, 4);
            st.Write(BitConverter.GetBytes(value.Z), 0, 4);
        }
        public void Wquat(Quaternion value)
        {
            st.Write(BitConverter.GetBytes(value.X), 0, 4);
            st.Write(BitConverter.GetBytes(value.Y), 0, 4);
            st.Write(BitConverter.GetBytes(value.Z), 0, 4);
            st.Write(BitConverter.GetBytes(value.W), 0, 4);
        }
    }

    public class rstreamer
    {
        private byte[] rbuf;
        private int ptr;

        public rstreamer(byte[] data)
        {
            rbuf = data;
            ptr = 0;
        }

        public void close()
        {
        }

        public short Rshort()
        {
            short v = BitConverter.ToInt16(rbuf, ptr);
            ptr += 2;
            return v;
        }
        public ushort Rushort()
        {
            ushort v = BitConverter.ToUInt16(rbuf, ptr);
            ptr += 2;
            return v;
        }
        public int Rint()
        {
            int v = BitConverter.ToInt32(rbuf, ptr);
            ptr += 4;
            return v;
        }
        public uint Ruint()
        {
            uint v = BitConverter.ToUInt32(rbuf, ptr);
            ptr += 4;
            return v;
        }
        public long Rlong()
        {
            long v = BitConverter.ToInt64(rbuf, ptr);
            ptr += 8;
            return v;
        }
        public ulong Rulong()
        {
            ulong v = BitConverter.ToUInt64(rbuf, ptr);
            ptr += 8;
            return v;
        }
        public float Rfloat()
        {
            float v = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            return v;
        }

        public double Rdouble()
        {
            double v = BitConverter.ToDouble(rbuf, ptr);
            ptr += 8;
            return v;
        }

        public Vector3 Rvector3()
        {
            Vector3 v;
            v.X = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            v.Y = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            v.Z = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            return v;
        }
        public Quaternion Rquat()
        {
            Quaternion v;
            v.X = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            v.Y = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            v.Z = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            v.W = BitConverter.ToSingle(rbuf, ptr);
            ptr += 4;
            return v;
        }
    }
}
