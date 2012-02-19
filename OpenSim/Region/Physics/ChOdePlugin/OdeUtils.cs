/* Ubit 2012
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

// no endian conversion. So can't be use to pass information around diferent cpus with diferent endian

using System;
using System.IO;
using OpenMetaverse;

namespace OpenSim.Region.Physics.OdePlugin
{
   
    unsafe public class wstreamer
    {
        byte[] buf;
        int index;
        byte* src;

        public wstreamer()
        {
            buf = new byte[1024];
            index = 0;
        }
        public wstreamer(int size)
        {
            buf = new byte[size];
            index = 0;
        }

        public byte[] close()
        {
            byte[] data = new byte[index];
            Buffer.BlockCopy(buf, 0, data, 0, index); 
            return data;
        }

        public void Seek(int pos)
        {
            index = pos;
        }

        public void Seekrel(int pos)
        {
            index += pos;
        }

        public void Wbyte(byte value)
        {
            buf[index++] = value;
        }
        public void Wshort(short value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src;
        }
        public void Wushort(ushort value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src;
        }
        public void Wint(int value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }
        public void Wuint(uint value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }
        public void Wlong(long value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }
        public void Wulong(ulong value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }

        public void Wfloat(float value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }

        public void Wdouble(double value)
        {
            src = (byte*)&value;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }

        public void Wvector3(Vector3 value)
        {
            src = (byte*)&value.X;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
            src = (byte*)&value.Y; // it may have padding ??
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
            src = (byte*)&value.Z;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }
        public void Wquat(Quaternion value)
        {
            src = (byte*)&value.X;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
            src = (byte*)&value.Y; // it may have padding ??
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
            src = (byte*)&value.Z;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
            src = (byte*)&value.W;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src++;
            buf[index++] = *src;
        }
    }

    unsafe public class rstreamer
    {
        private byte[] rbuf;
        private int ptr;
        private byte* dst;

        public rstreamer(byte[] data)
        {
            rbuf = data;
            ptr = 0;
        }

        public void close()
        {
        }

        public void Seek(int pos)
        {
            ptr = pos;
        }

        public void Seekrel(int pos)
        {
            ptr += pos;
        }

        public byte Rbyte()
        {
            return (byte)rbuf[ptr++];
        }

        public short Rshort()
        {
            short v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }
        public ushort Rushort()
        {
            ushort v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }
        public int Rint()
        {
            int v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }
        public uint Ruint()
        {
            uint v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }
        public long Rlong()
        {
            long v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }
        public ulong Rulong()
        {
            ulong v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }
        public float Rfloat()
        {
            float v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }

        public double Rdouble()
        {
            double v;
            dst = (byte*)&v;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }

        public Vector3 Rvector3()
        {
            Vector3 v;
            dst = (byte*)&v.X;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];

            dst = (byte*)&v.Y;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];

            dst = (byte*)&v.Z;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];
            return v;
        }

        public Quaternion Rquat()
        {
            Quaternion v;
            dst = (byte*)&v.X;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];

            dst = (byte*)&v.Y;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];

            dst = (byte*)&v.Z;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];

            dst = (byte*)&v.W;
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst++ = rbuf[ptr++];
            *dst = rbuf[ptr++];

            return v;
        }
    }
}
