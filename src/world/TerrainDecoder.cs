/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
//using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
	/// <summary>
	/// Description of TerrainDecoder.
	/// </summary>
	public class TerrainDecode
	{
		
        public enum LayerType : byte
        {
            Land = 0x4C,
            Water = 0x57,
            Wind = 0x37,
            Cloud = 0x38
        }

        public struct GroupHeader
        {
            public int Stride;
            public int PatchSize;
            public LayerType Type;
        }

        public struct PatchHeader
        {
            public float DCOffset;
            public int Range;
            public int QuantWBits;
            public int PatchIDs;
            public uint WordBits;
        }

        public class Patch
        {
            public float[] Heightmap;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="simulator"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="data"></param>
       // public delegate void LandPatchCallback(Simulator simulator, int x, int y, int width, float[] data);


        /// <summary>
        /// 
        /// </summary>
        //public event LandPatchCallback OnLandPatch;

		private Random RandomClass = new Random();
		
        private const byte END_OF_PATCHES = 97;
        private const int PATCHES_PER_EDGE = 16;
        private const float OO_SQRT2 = 0.7071067811865475244008443621049f;

        //private SecondLife Client;
        private Dictionary<ulong, Patch[]> SimPatches = new Dictionary<ulong, Patch[]>();
        private float[] DequantizeTable16 = new float[16 * 16];
        private float[] DequantizeTable32 = new float[32 * 32];
        private float[] ICosineTable16 = new float[16 * 16];
        private float[] ICosineTable32 = new float[32 * 32];
        private int[] DeCopyMatrix16 = new int[16 * 16];
        private int[] DeCopyMatrix32 = new int[32 * 32];


        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public TerrainDecode()
        {
         
            // Initialize the decompression tables
            BuildDequantizeTable16();
            BuildDequantizeTable32();
            SetupICosines16();
            SetupICosines32();
            BuildDecopyMatrix16();
            BuildDecopyMatrix32();

       }

        
        private void BuildDequantizeTable16()
        {
            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                {
                    DequantizeTable16[j * 16 + i] = 1.0f + 2.0f * (float)(i + j);
                }
            }
        }

        private void BuildDequantizeTable32()
        {
            for (int j = 0; j < 32; j++)
            {
                for (int i = 0; i < 32; i++)
                {
                    DequantizeTable32[j * 32 + i] = 1.0f + 2.0f * (float)(i + j);
                }
            }
        }

        private void SetupICosines16()
        {
            const float hposz = (float)Math.PI * 0.5f / 16.0f;

            for (int u = 0; u < 16; u++)
            {
                for (int n = 0; n < 16; n++)
                {
                    ICosineTable16[u * 16 + n] = (float)Math.Cos((2.0f * (float)n + 1.0f) * (float)u * hposz);
                }
            }
        }

        private void SetupICosines32()
        {
            const float hposz = (float)Math.PI * 0.5f / 32.0f;

            for (int u = 0; u < 32; u++)
            {
                for (int n = 0; n < 32; n++)
                {
                    ICosineTable32[u * 32 + n] = (float)Math.Cos((2.0f * (float)n + 1.0f) * (float)u * hposz);
                }
            }
        }

        private void BuildDecopyMatrix16()
        {
            bool diag = false;
            bool right = true;
            int i = 0;
            int j = 0;
            int count = 0;

            while (i < 16 && j < 16)
            {
                DeCopyMatrix16[j * 16 + i] = count++;

                if (!diag)
                {
                    if (right)
                    {
                        if (i < 16 - 1) i++;
                        else j++;

                        right = false;
                        diag = true;
                    }
                    else
                    {
                        if (j < 16 - 1) j++;
                        else i++;

                        right = true;
                        diag = true;
                    }
                }
                else
                {
                    if (right)
                    {
                        i++;
                        j--;
                        if (i == 16 - 1 || j == 0) diag = false;
                    }
                    else
                    {
                        i--;
                        j++;
                        if (j == 16 - 1 || i == 0) diag = false;
                    }
                }
            }
        }

        private void BuildDecopyMatrix32()
        {
            bool diag = false;
            bool right = true;
            int i = 0;
            int j = 0;
            int count = 0;

            while (i < 32 && j < 32)
            {
                DeCopyMatrix32[j * 32 + i] = count++;

                if (!diag)
                {
                    if (right)
                    {
                        if (i < 32 - 1) i++;
                        else j++;

                        right = false;
                        diag = true;
                    }
                    else
                    {
                        if (j < 32 - 1) j++;
                        else i++;

                        right = true;
                        diag = true;
                    }
                }
                else
                {
                    if (right)
                    {
                        i++;
                        j--;
                        if (i == 32 - 1 || j == 0) diag = false;
                    }
                    else
                    {
                        i--;
                        j++;
                        if (j == 32 - 1 || i == 0) diag = false;
                    }
                }
            }
        }
        
        private void EncodePatchHeader(BitPacker bitpack, PatchHeader header)
        {
        	bitpack.PackBits(header.QuantWBits,8);
        	
        	if (header.QuantWBits == END_OF_PATCHES)
                return;
        	
        	bitpack.PackFloat(header.DCOffset);
        	bitpack.PackBits(header.Range,16);
        	bitpack.PackBits(header.PatchIDs,10);
        	
        }
		
        public  void DCTLine16(float[] In, float[] Out, int line)
        {
        	int N =16;
			int lineSize = line * 16;
        
        	for(int k = 0; k < N;k++)
        	{
        		float sum = 0.0f;
        		for(int n = 0; n < N; n++)
        		{
        			float num = (float)(Math.PI*k*(2.0f*n+1)/(2*N));
        			float cosine = (float)Math.Cos(num);
        			float product = In[lineSize +n] * cosine;
        			sum += product;
        		}

        		float alpha;
        		if(k == 0)
        		{
        			alpha = (float)(1.0f/Math.Sqrt(2));
        		}
        		else
        		{
        			alpha = 1;
        		}
        		Out[lineSize + k] =(float)( sum * alpha );
        		
        	}
        }
        public  void DCTColumn16(float[] In, float[] Out, int Column)
        {
        	int N =16;
        	int uSize;
        	
        	for(int k = 0; k < N; k++){
        		float sum = 0.0f;
        		for(int n = 0; n < N; n++)
        		{
        			uSize = n * 16;
        			float num = (float)(Math.PI*k*(2.0f*n+1)/(2*N));
        			float cosine = (float)Math.Cos(num);
        			float product = In[uSize + Column] * cosine;
        			sum += product;
        		}

        		float alpha;
        		if(k == 0)
        		{
        			alpha = (float)(1.0f/Math.Sqrt(2));
        		}
        		else
        		{
        			alpha = 1;
        		}
        		Out[16 * k  + Column] = (float)( sum * alpha * (2.0f /N));
        		
        	}
        }
        
        private void EncodePatch(int[] patches, BitPacker bitpack, int size)
        {
        	int lastnum =0; 
        	for(int n = 0; n < size * size; n++)
        	{
        		if(patches[n]!=0)
        		   lastnum=n;
        	}	 
        	for (int n = 0; n < lastnum+1; n++)
            {
        		if(patches[n] != 0)
        		{
        			bitpack.PackBits(1,1); //value or EOB
        			bitpack.PackBits(1,1); //value
        			if(patches[n] > 0)
        			{
        				
        				bitpack.PackBits(0,1); // positive
        				bitpack.PackBits(patches[n],13);
        				
        			}
        			else
        			{
        				bitpack.PackBits(1,1); // negative
        				
        				int temp = patches[n] * -1;
        				bitpack.PackBits(temp,13);
        				
        			}
        		}
        		else
        		{
        			bitpack.PackBits(0,1); // no value
        		}
        	}
        	
        	bitpack.PackBits(1,1); //value or EOB
        	bitpack.PackBits(0,1); // EOB
        }
      
		public int[] CompressPatch(float[] patches)
        {
			int size = 16;
            float[] block = new float[size * size];
            int[] output = new int[size * size];
            int prequant = (139 >> 4) + 2;
            int quantize = 1 << prequant;
            float ooq = 1.0f / (float)quantize;
            float mult = ooq * (float)1;
            float addval = mult * (float)(1 << (prequant - 1)) + 20.4989f;
			
            if (size == 16) 
            {
                for (int n = 0; n < 16 * 16; n++)
                {
                	block[n] = (float)((patches[n] - addval)/ mult);
                }

                float[] ftemp = new float[32 * 32];

                for (int o = 0; o < 16; o++)
                    this.DCTColumn16(block, ftemp, o);
                for (int o = 0; o < 16; o++)
                    this.DCTLine16(ftemp, block, o);
            }
          
            for (int j = 0; j < block.Length; j++)
            {
                output[DeCopyMatrix16[j]] = (int)(block[j] / DequantizeTable16[j]);
            }
			
         	return output;
        }

		public Packet CreateLayerPacket(float[] heightmap, int minX, int minY, int maxX, int maxY)
        {
        	//int minX = 0, maxX = 2, minY = 0, maxY = 1; //these should be passed to this function
        	LayerDataPacket layer = new LayerDataPacket();
        	byte[] Encoded = new byte[2048];
        	layer.LayerID.Type = 76;
        	GroupHeader header = new GroupHeader();
        	header.Stride = 264;
        	header.PatchSize = 16;
        	header.Type = LayerType.Land;
        	BitPacker newpack = new BitPacker(Encoded,0);
        	newpack.PackBits(header.Stride,16);
        	newpack.PackBits(header.PatchSize,8);
        	newpack.PackBits((int)header.Type,8);
        	
        	
        	float[] height;
        	for(int y = minY; y< maxY; y++)
        	{
        		for(int x = minX ; x < maxX ; x++)
        		{
        			height = new float[256];
        			Array.Copy(heightmap, (4096 *y) +(x *256), height, 0, 256);
        			
        			this.CreatePatch(height, newpack, x, y);
        		}
        	}
        	
        	PatchHeader headers = new PatchHeader();
        	headers.QuantWBits = END_OF_PATCHES;
        	this.EncodePatchHeader(newpack, headers);
        	
        	int lastused=0;
        	for(int i = 0; i < 1024 ; i++)
        	{
        		if(Encoded[i] !=0)
        			lastused = i;
        	}
        	
        	byte[] data = new byte[lastused+1];
        	Array.Copy(Encoded, data, lastused+1);
        	layer.LayerData.Data =data;
        	
        	return(layer);
        }
        public void CreatePatch(float[] heightmap, BitPacker newpack, int x, int y)
        {
        	PatchHeader header = new PatchHeader();
        	header.DCOffset = 20.4989f;
        	header.QuantWBits = 139;
        	header.Range = 1;
        	header.PatchIDs = (y & 0x1F);
        	header.PatchIDs += x <<5 ;
        	
        	this.EncodePatchHeader(newpack, header);
        	
        	int[] newpatch = this.CompressPatch(heightmap);
            this.EncodePatch(newpatch, newpack, 16);
        	
        }
    }
	
	//***************************************************
	public class BitPacker
    {
        private const int MAX_BITS = 8;

        private byte[] Data;
        public int bytePos;
        public int bitPos;

        /// <summary>
        /// Default constructor, initialize the bit packer / bit unpacker
        /// with a byte array and starting position
        /// </summary>
        /// <param name="data">Byte array to pack bits in to or unpack from</param>
        /// <param name="pos">Starting position in the byte array</param>
        public BitPacker(byte[] data, int pos)
        {
            Data = data;
            bytePos = pos;
        }

        /// <summary>
        /// Pack a floating point value in to the data
        /// </summary>
        /// <param name="data">Floating point value to pack</param>
        public void PackFloat(float data)
        {
            byte[] input = BitConverter.GetBytes(data);
            PackBitArray(input, 32);
        }

        /// <summary>
        /// Pack part or all of an integer in to the data
        /// </summary>
        /// <param name="data">Integer containing the data to pack</param>
        /// <param name="totalCount">Number of bits of the integer to pack</param>
        public void PackBits(int data, int totalCount)
        {
            byte[] input = BitConverter.GetBytes(data);
            PackBitArray(input, totalCount);
        }

        /// <summary>
        /// Unpacking a floating point value from the data
        /// </summary>
        /// <returns>Unpacked floating point value</returns>
        public float UnpackFloat()
        {
            byte[] output = UnpackBitsArray(32);

            if (!BitConverter.IsLittleEndian) Array.Reverse(output);
            return BitConverter.ToSingle(output, 0);
        }

        /// <summary>
        /// Unpack a variable number of bits from the data in to integer format
        /// </summary>
        /// <param name="totalCount">Number of bits to unpack</param>
        /// <returns>An integer containing the unpacked bits</returns>
        /// <remarks>This function is only useful up to 32 bits</remarks>
        public int UnpackBits(int totalCount)
        {
            byte[] output = UnpackBitsArray(totalCount);

            if (!BitConverter.IsLittleEndian) Array.Reverse(output);
            return BitConverter.ToInt32(output, 0);
        }

        private void PackBitArray(byte[] data, int totalCount)
        {
            int count = 0;
            int curBytePos = 0;
            int curBitPos = 0;
			
            while (totalCount > 0)
            {
            	if (totalCount > (MAX_BITS ))
                {
                    count = MAX_BITS ;
                    totalCount -= MAX_BITS ;
                }
                else
                {
                    count = totalCount;
                    totalCount = 0;
                }

                while (count > 0)
                {
                    switch(count)
                    {
                    	case 1:
                    		 if ((data[curBytePos] & (0x01)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    	case 2:
                    		 if ((data[curBytePos] & (0x02)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    	case 3:
                    		 if ((data[curBytePos] & (0x04)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    	case 4:
                    		 if ((data[curBytePos] & (0x08)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    	case 5:
                    		 if ((data[curBytePos] & (0x10)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    	case 6:
                    		 if ((data[curBytePos] & (0x20)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    	case 7:
                    		 if ((data[curBytePos] & (0x40)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    	case 8:
                    		 if ((data[curBytePos] & (0x80)) != 0)
                    		 {
                    		 	 Data[bytePos] |= (byte)(0x80 >> bitPos);
                    		 }
                    		 break;
                    }
                   
                    bitPos++;
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
