using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.types
{
    /* New Method
     * 
     * 1. Get all the individual bytes and their bitlength, put them in a dictionary
     * 2. Mash together when wanted.
     * 
     * */
    public class Bits {
        public byte[] data;
        public int len;
    }

    public class InverseBitPack
    {
        private List<Bits> bits;

        public InverseBitPack()
        {
            bits = new List<Bits>();
        }
    }

    public class BitPack
    {
        private const int MAX_BITS = 8;

        private byte[] Data;
        private int bytePos;
        private int bitPos;

        public BitPack(byte[] data, int pos) // For libsl compatibility
        {
            Data = data;
            bytePos = pos;
        }

        public BitPack() // Encoding version
        {

        }

        public void LoadData(byte[] data, int pos) {
            Data = data;
            bytePos = pos;
            bitPos = 0;
        }

        private void PackBitsArray(byte[] bits, int bitLen)
        {
            int offset = bitPos % MAX_BITS;
            int i;
            byte temp1;
            byte temp2;

            for (i = 0; i < bits.Length; i++)
            {
                int Byte = bits[i];
                Byte <<= offset;
                temp1 = (byte)(Byte & 0xFF);
                temp2 = (byte)((Byte >> 8) & 0xFF);

                Data[Data.Length - 1] |= temp1;
//                Data

                bitPos += bitLen;
            }
        }

        public float UnpackFloat()
        {
            byte[] output = UnpackBitsArray(32);

            if (!BitConverter.IsLittleEndian) Array.Reverse(output);
            return BitConverter.ToSingle(output, 0);
        }

        public int UnpackBits(int totalCount)
        {
            byte[] output = UnpackBitsArray(totalCount);

            if (!BitConverter.IsLittleEndian) Array.Reverse(output);
            return BitConverter.ToInt32(output, 0);
        }

        private byte[] UnpackBitsArray(int totalCount)
        {
            int count = 0;
            byte[] output = new byte[4];
            int curBytePos = 0;
            int curBitPos = 0;

            while (totalCount > 0)
            {
                if (totalCount > MAX_BITS)
                {
                    count = MAX_BITS;
                    totalCount -= MAX_BITS;
                }
                else
                {
                    count = totalCount;
                    totalCount = 0;
                }

                while (count > 0)
                {
                    // Shift the previous bits
                    output[curBytePos] <<= 1;

                    // Grab one bit
                    if ((Data[bytePos] & (0x80 >> bitPos++)) != 0)
                        ++output[curBytePos];

                    --count;
                    ++curBitPos;

                    if (bitPos >= MAX_BITS)
                    {
                        bitPos = 0;
                        ++bytePos;
                    }
                    if (curBitPos >= MAX_BITS)
                    {
                        curBitPos = 0;
                        ++curBytePos;
                    }
                }
            }

            return output;
        }
    }
}
