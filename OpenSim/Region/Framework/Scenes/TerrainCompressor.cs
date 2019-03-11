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

/* Freely adapted from the Aurora version of the terrain compressor.
 * Copyright (c) Contributors, http://aurora-sim.org/, http://opensimulator.org/
 * Aurora version created from libOpenMetaverse Library terrain compressor
 */

// terrain patchs must be 16mx16m

using System;
using System.Collections.Generic;

using log4net;

using OpenSim.Framework;

using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public static class OpenSimTerrainCompressor
    {
        private const float OO_SQRT2 = 0.7071068f;
        private const int END_OF_PATCHES = 97;
        private const int STRIDE = 264;
        private const int ZERO_CODE = 0x0;
        private const int ZERO_EOB = 0x2;
        private const int POSITIVE_VALUE = 0x6;
        private const int NEGATIVE_VALUE = 0x7;

        private static readonly int[] CopyMatrix16 = new int[256];
        private static readonly float[] QuantizeTable16 = new float[256];
        private static readonly float[] DequantizeTable16 = new float[256];

        static OpenSimTerrainCompressor()
        {
            if(Constants.TerrainPatchSize != 16)
                throw new Exception("Terrain patch size must be 16m x 16m");

            // Initialize the decompression tables
            BuildDequantizeTable16();
            BuildCopyMatrix16();
            BuildQuantizeTable16();
        }

        // Used to send cloud and wind patches
        public static LayerDataPacket CreateLayerDataPacketStandardSize(TerrainPatch[] patches, byte type)
        {
            LayerDataPacket layer = new LayerDataPacket { LayerID = { Type = type } };
            TerrainPatch.GroupHeader header = new TerrainPatch.GroupHeader { Stride = STRIDE, PatchSize = 16 };

            // Should be enough to fit even the most poorly packed data
            byte[] data = new byte[patches.Length * 256 * 2];

            BitPack bitpack = new BitPack(data, 0);
            bitpack.PackBits(header.Stride, 16);
            bitpack.PackBits(header.PatchSize, 8);
            bitpack.PackBits(type, 8);

            foreach (TerrainPatch t in patches)
                CreatePatchtStandardSize(bitpack, t.Data, t.X, t.Y);

            bitpack.PackBits(END_OF_PATCHES, 8);

            layer.LayerData.Data = new byte[bitpack.BytePos + 1];
            Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);

            return layer;
        }

        public static void CreatePatchtStandardSize(BitPack output, float[] patchData, int x, int y)
        {
            TerrainPatch.Header header = PrescanPatch(patchData);
            header.QuantWBits = 136;

            header.PatchIDs = (y & 0x1F);
            header.PatchIDs += (x << 5);

            int wbits;
            int[] patch = CompressPatch(patchData, header, 10, out wbits);
            EncodePatchHeader(output, header, false, ref wbits);
            EncodePatch(output, patch, 0, wbits);
        }

        private static TerrainPatch.Header PrescanPatch(float[] patch)
        {
            TerrainPatch.Header header = new TerrainPatch.Header();
            float zmax = -99999999.0f;
            float zmin = 99999999.0f;

            for (int i = 0; i < 256; i++)
            {
                float val = patch[i];
                if (val > zmax) zmax = val;
                if (val < zmin) zmin = val;
            }

            header.DCOffset = zmin;
            header.Range = (int)((zmax - zmin) + 1.0f);

            return header;
        }

        private static int[] CompressPatch(float[] patchData, TerrainPatch.Header header, int prequant, out int wbits)
        {
            float[] block = new float[256];
            float oozrange = 1.0f / header.Range;
            float range = (1 << prequant);
            float premult = oozrange * range;

            float sub = 0.5f * header.Range + header.DCOffset;

            int wordsize = (prequant - 2) & 0x0f;
            header.QuantWBits = wordsize;
            header.QuantWBits |= wordsize << 4;

            int k = 0;
            for (int j = 0; j < 16; j++)
            {
                for (int i = 0; i < 16; i++)
                    block[k++] = (patchData[j * 16 + i] - sub) * premult;
            }

            wbits = (prequant >> 1);
            int[] iout = new int[256];

            dct16x16(block, iout, ref wbits);

            return iout;
        }

/*
        // new using terrain data and patchs indexes
        public static List<LayerDataPacket> CreateLayerDataPackets(TerrainData terrData, int[] map)
        {
            List<LayerDataPacket> ret = new List<LayerDataPacket>();

            int numberPatchs = map.Length / 2;
            byte[] data = new byte[numberPatchs * 256 * 2];

            //create packet and global header
            LayerDataPacket layer = new LayerDataPacket();

            byte landPacketType;
            if (terrData.SizeX > Constants.RegionSize || terrData.SizeY > Constants.RegionSize)
                landPacketType = (byte)TerrainPatch.LayerType.LandExtended;
            else
                landPacketType = (byte)TerrainPatch.LayerType.Land;

            layer.LayerID.Type = landPacketType;

            BitPack bitpack = new BitPack(data, 0);
            bitpack.PackBits(STRIDE, 16);
            bitpack.PackBitsFromByte(16);
            bitpack.PackBitsFromByte(landPacketType);

            int s;
            for (int i = 0; i < numberPatchs; i++)
            {
                s = 2 * i;
                CreatePatchFromTerrainData(bitpack, terrData, map[s], map[s + 1]);

                if (bitpack.BytePos > 950 && i != numberPatchs - 1)
                {
                    //finish this packet
                    bitpack.PackBitsFromByte(END_OF_PATCHES);

                    layer.LayerData.Data = new byte[bitpack.BytePos + 1];
                    Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);
                    ret.Add(layer);

                    // start another
                    layer = new LayerDataPacket();
                    layer.LayerID.Type = landPacketType;

                    bitpack = new BitPack(data, 0);
                    bitpack.PackBits(STRIDE, 16);
                    bitpack.PackBitsFromByte(16);
                    bitpack.PackBitsFromByte(landPacketType);
                }
            }

            bitpack.PackBitsFromByte(END_OF_PATCHES);

            layer.LayerData.Data = new byte[bitpack.BytePos + 1];
            Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);
            ret.Add(layer);

            return ret;
        }
*/

        public static void CreatePatchFromTerrainData(BitPack output, TerrainData terrData, int patchX, int patchY)
        {
            float frange;

            TerrainPatch.Header header = PrescanPatch(terrData, patchX, patchY, out frange);
            header.QuantWBits = 130;

            bool largeRegion = false;
            // If larger than legacy region size, pack patch X and Y info differently.
            if (terrData.SizeX > Constants.RegionSize || terrData.SizeY > Constants.RegionSize)
            {
                header.PatchIDs = (patchY & 0xFFFF);
                header.PatchIDs += (patchX << 16);
                largeRegion = true;
            }
            else
            {
                header.PatchIDs = (patchY & 0x1F);
                header.PatchIDs += (patchX << 5);
            }

            if (Math.Round(frange, 2) == 1.0)
            {
                // flat terrain speed up things
                output.PackBitsFromByte(0); //QuantWBits
                output.PackFloat(header.DCOffset - 0.5f);
                output.PackBitsFromByte(1); //range low
                output.PackBitsFromByte(0); //range high
                if (largeRegion)
                    output.PackBits(header.PatchIDs, 32);
                else
                    output.PackBits(header.PatchIDs, 10);

                // and thats all
                output.PackBits(ZERO_EOB, 2);
                return;
            }

            int wbits;
            int[] patch = CompressPatch(terrData, patchX, patchY, header, 10, out wbits);
            EncodePatchHeader(output, header, largeRegion, ref wbits);
            EncodePatch(output, patch, 0, wbits);
        }

        // Scan the height info we're returning and return a patch packet header for this patch.
        private static TerrainPatch.Header PrescanPatch(TerrainData terrData, int patchX, int patchY, out float frange)
        {
            TerrainPatch.Header header = new TerrainPatch.Header();
            float zmax = float.MinValue;
            float zmin = float.MaxValue;

            terrData.GetPatchMinMax(patchX, patchY, out zmin, out zmax);

            header.DCOffset = zmin;
            frange = ((zmax - zmin) + 1.0f);
            header.Range = (int)frange;

            return header;
        }

        private static void EncodePatchHeader(BitPack output, TerrainPatch.Header header, bool largeRegion, ref int wbits)
        {
            if (wbits > 17)
                wbits = 17;
            else if (wbits < 2)
                wbits = 2;

            header.QuantWBits &= 0xf0;
            header.QuantWBits |= (wbits - 2);

            output.PackBitsFromByte((byte)header.QuantWBits);
            output.PackFloat(header.DCOffset);
            output.PackBits(header.Range, 16);
            if (largeRegion)
                output.PackBits(header.PatchIDs, 32);
            else
                output.PackBits(header.PatchIDs, 10);
        }

        private unsafe static void EncodePatch(BitPack output, int[] _patch, int postquant, int wbits)
        {
            int maxwbitssize = (1 << wbits) - 1;

            if (postquant > 256 || postquant < 0)
            {
                Logger.Log("Postquant is outside the range of allowed values in EncodePatch()", Helpers.LogLevel.Error);
                return;
            }

            int lastZeroindx = 256 - postquant;

            fixed(int * patch = _patch)
            {
                if (lastZeroindx != 256)
                    patch[lastZeroindx] = 0;

                int i = 0;
                while(i < 256)
                {
                    int temp = patch[i];

                    if (temp == 0)
                    {
                        int j = i + 1;
                        while(j < lastZeroindx)
                        {
                            if (patch[j] != 0)
                                break;
                            ++j;
                        }

                        if (j == lastZeroindx)
                        {
                            output.PackBits(ZERO_EOB, 2);
                            return;
                        }

                        i = j - i;
                        while(i > 8)
                        {
                            output.PackBitsFromByte(ZERO_CODE);
                            i -= 8;
                        }
                        if( i > 0)
                            output.PackBitsFromByte(ZERO_CODE, i);
                        i = j;
                        continue;
                    }

                    if (temp < 0)
                    {
                        temp *= -1;
                        if (temp > maxwbitssize)
                            temp = maxwbitssize;

                        output.PackBits(NEGATIVE_VALUE, 3);
                        output.PackBits(temp, wbits);
                    }
                    else
                    {
                        if (temp > maxwbitssize)
                            temp = maxwbitssize;

                        output.PackBits(POSITIVE_VALUE, 3);
                        output.PackBits(temp, wbits);
                    }
                    ++i;
                }
            }
        }

        private static int[] CompressPatch(TerrainData terrData, int patchX, int patchY, TerrainPatch.Header header,
                                                               int prequant, out int wbits)
        {
            float[] block = new float[256];
            int[] iout = new int[256];

            float oozrange = 1.0f / header.Range;
            float invprequat = (1 << prequant);
            float premult = oozrange * invprequat;

            float sub = 0.5f * header.Range + header.DCOffset;

            int wordsize = (prequant - 2) & 0x0f;
            header.QuantWBits = wordsize;
            header.QuantWBits |= wordsize << 4;

            terrData.GetPatchBlock(block, patchX, patchY, sub, premult);

            wbits = (prequant >> 1);

            dct16x16(block, iout, ref wbits);
            return iout;
        }

        #region Initialization

        private static void BuildDequantizeTable16()
        {
            for (int j = 0; j < 16; j++)
            {
                int c = j * 16;
                for (int i = 0; i < 16; i++)
                {
                    DequantizeTable16[c + i] = 1.0f + 2.0f * (i + j);
                }
            }
        }

        private unsafe static void BuildQuantizeTable16()
        {
            const float oosob = 2.0f / 16;
            fixed(float* fQuantizeTable16 = QuantizeTable16)
            {
                for (int j = 0; j < 16; j++)
                {
                    int c = j * 16;
                    for (int i = 0; i < 16; i++)
                    {
                        fQuantizeTable16[c + i] = oosob / (1.0f + 2.0f * (i + j));
                    }
                }
            }
        }

        private unsafe static void BuildCopyMatrix16()
        {
            bool diag = false;
            bool right = true;
            int i = 0;
            int j = 0;
            int count = 0;

            fixed (int* fCopyMatrix16 = CopyMatrix16)
            {
                while (i < 16 && j < 16)
                {
                    fCopyMatrix16[j * 16 + i] = count++;

                    if (!diag)
                    {
                        if (right)
                        {
                            if (i < 15) i++;
                            else j++;

                            right = false;
                            diag = true;
                        }
                        else
                        {
                            if (j < 15 ) j++;
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
                            if (i == 15 || j == 0) diag = false;
                        }
                        else
                        {
                            i--;
                            j++;
                            if (j == 15 || i == 0) diag = false;
                        }
                    }
                }
            }
        }

        #endregion Initialization

        #region DCT

        /* DCT (Discrete Cosine Transform)
        adaptation from
        General Purpose 2D,3D FFT (Fast Fourier Transform) Package
        by Takuya OOURA (email: ooura@kurims.kyoto-u.ac.jp)

        -------- 16x16 DCT (Discrete Cosine Transform) / Inverse of DCT --------
            [definition]
                <case1> Normalized 16x16 IDCT
                    C[k1 + k2] = (1/8) * sum_j1=0^15 sum_j2=0^15
                                    tmp[j1 + j2] * s[j1] * s[j2] *
                                    cos(pi*j1*(k1+1/2)/16) *
                                    cos(pi*j2*(k2+1/2)/16), 0<=k1<16, 0<=k2<16
                                    (s[0] = 1/sqrt(2), s[j] = 1, j > 0)
                <case2> Normalized 16x16 DCT
                    C[k1 + k2] = (1/8) * s[k1] * s[k2] * sum_j1=0^15 sum_j2=0^15
                                    tmp[j1 + j2] *
                                    cos(pi*(j1+1/2)*k1/16) *
                                    cos(pi*(j2+1/2)*k2/16), 0<=k1<16, 0<=k2<16
                                    (s[0] = 1/sqrt(2), s[j] = 1, j > 0)
        */

        /* Cn_kR = sqrt(2.0/n) * cos(pi/2*k/n) */
        /* Cn_kI = sqrt(2.0/n) * sin(pi/2*k/n) */
        /* Wn_kR = cos(pi/2*k/n) */
        /* Wn_kI = sin(pi/2*k/n) */

        const float C16_1R = 0.35185093438159561476f * 2.82842712474619f;
        const float C16_1I = 0.03465429229977286565f * 2.82842712474619f;
        const float C16_2R = 0.34675996133053686546f * 2.82842712474619f;
        const float C16_2I = 0.06897484482073575308f * 2.82842712474619f;
        const float C16_3R = 0.33832950029358816957f * 2.82842712474619f;
        const float C16_3I = 0.10263113188058934529f * 2.82842712474619f;
        const float C16_4R = 0.32664074121909413196f * 2.82842712474619f;
        const float C16_4I = 0.13529902503654924610f * 2.82842712474619f;
        const float C16_5R = 0.31180625324666780814f * 2.82842712474619f;
        const float C16_5I = 0.16666391461943662432f * 2.82842712474619f;
        const float C16_6R = 0.29396890060483967924f * 2.82842712474619f;
        const float C16_6I = 0.19642373959677554532f * 2.82842712474619f;
        const float C16_7R = 0.27330046675043937206f * 2.82842712474619f;
        const float C16_7I = 0.22429189658565907106f * 2.82842712474619f;
        const float C16_8R = 0.25f * 2.82842712474619f;
        const float W16_4R = 0.92387953251128675613f;
        const float W16_4I = 0.38268343236508977173f;
        const float W16_8R = 0.70710678118654752440f;


        unsafe static void dct16x16(float[] _a, int[] _iout, ref int wbits)
        {
            float[] _tmp = new float[256];

            float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;
            float x4r, x4i, x5r, x5i, x6r, x6i, x7r, x7i;
            float xr, xi;
            float ftmp;

            int itmp;
            int j, k;
            int indx;

            const int maxwbits = 17; // per header encoding
            int wbitsMaxValue = 1 << wbits;
            bool dowbits = wbits < 17;

            fixed (float* a = _a, tmp = _tmp, fQuantizeTable16 = QuantizeTable16)
            fixed (int* iout = _iout, fCopyMatrix16 = CopyMatrix16)
            {
                for (j = 0, k = 0; j < 256; j += 16, k++)
                {
                    x4r = a[0 + j] - a[15 + j];
                    xr = a[0 + j] + a[15 + j];
                    x4i = a[8 + j] - a[7 + j];
                    xi = a[8 + j] + a[7 + j];
                    x0r = xr + xi;
                    x0i = xr - xi;
                    x5r = a[2 + j] - a[13 + j];
                    xr = a[2 + j] + a[13 + j];
                    x5i = a[10 + j] - a[5 + j];
                    xi = a[10 + j] + a[5 + j];
                    x1r = xr + xi;
                    x1i = xr - xi;
                    x6r = a[4 + j] - a[11 + j];
                    xr = a[4 + j] + a[11 + j];
                    x6i = a[12 + j] - a[3 + j];
                    xi = a[12 + j] + a[3 + j];
                    x2r = xr + xi;
                    x2i = xr - xi;
                    x7r = a[6 + j] - a[9 + j];
                    xr = a[6 + j] + a[9 + j];
                    x7i = a[14 + j] - a[1 + j];
                    xi = a[14 + j] + a[1 + j];
                    x3r = xr + xi;
                    x3i = xr - xi;
                    xr = x0r + x2r;
                    xi = x1r + x3r;
                    tmp[k] = C16_8R * (xr + xi); //
                    tmp[8 * 16 + k] = C16_8R * (xr - xi); //
                    xr = x0r - x2r;
                    xi = x1r - x3r;
                    tmp[4 * 16 + k] = C16_4R * xr - C16_4I * xi; //
                    tmp[12 * 16 + k] = C16_4R * xi + C16_4I * xr;  //
                    x0r = W16_8R * (x1i - x3i);
                    x2r = W16_8R * (x1i + x3i);
                    xr = x0i + x0r;
                    xi = x2r + x2i;
                    tmp[2 * 16 + k] = C16_2R * xr - C16_2I * xi;  //
                    tmp[14 * 16 + k] = C16_2R * xi + C16_2I * xr;  //
                    xr = x0i - x0r;
                    xi = x2r - x2i;
                    tmp[6 * 16 + k] = C16_6R * xr - C16_6I * xi;  //
                    tmp[10 * 16 + k] = C16_6R * xi + C16_6I * xr; //
                    xr = W16_8R * (x6r - x6i);
                    xi = W16_8R * (x6i + x6r);
                    x6r = x4r - xr;
                    x6i = x4i - xi;
                    x4r += xr;
                    x4i += xi;
                    xr = W16_4I * x7r - W16_4R * x7i;
                    xi = W16_4I * x7i + W16_4R * x7r;
                    x7r = W16_4R * x5r - W16_4I * x5i;
                    x7i = W16_4R * x5i + W16_4I * x5r;
                    x5r = x7r + xr;
                    x5i = x7i + xi;
                    x7r -= xr;
                    x7i -= xi;
                    xr = x4r + x5r;
                    xi = x5i + x4i;
                    tmp[16 + k] = C16_1R * xr - C16_1I * xi;  //
                    tmp[15 * 16 + k] = C16_1R * xi + C16_1I * xr;  //
                    xr = x4r - x5r;
                    xi = x5i - x4i;
                    tmp[7 * 16 + k] = C16_7R * xr - C16_7I * xi;  //
                    tmp[9 * 16 + k] = C16_7R * xi + C16_7I * xr;  //
                    xr = x6r - x7i;
                    xi = x7r + x6i;
                    tmp[5 * 16 + k] = C16_5R * xr - C16_5I * xi;  //
                    tmp[11 * 16 + k] = C16_5R * xi + C16_5I * xr;  //
                    xr = x6r + x7i;
                    xi = x7r - x6i;
                    tmp[3 * 16 + k] = C16_3R * xr - C16_3I * xi;  //
                    tmp[13 * 16 + k] = C16_3R * xi + C16_3I * xr;  //
                }

                for (j = 0, k = 0; j < 256; j += 16, k++)
                {
                    x4r = tmp[0 + j] - tmp[15 + j];
                    xr = tmp[0 + j] + tmp[15 + j];
                    x4i = tmp[8 + j] - tmp[7 + j];
                    xi = tmp[8 + j] + tmp[7 + j];
                    x0r = xr + xi;
                    x0i = xr - xi;
                    x5r = tmp[2 + j] - tmp[13 + j];
                    xr = tmp[2 + j] + tmp[13 + j];
                    x5i = tmp[10 + j] - tmp[5 + j];
                    xi = tmp[10 + j] + tmp[5 + j];
                    x1r = xr + xi;
                    x1i = xr - xi;
                    x6r = tmp[4 + j] - tmp[11 + j];
                    xr = tmp[4 + j] + tmp[11 + j];
                    x6i = tmp[12 + j] - tmp[3 + j];
                    xi = tmp[12 + j] + tmp[3 + j];
                    x2r = xr + xi;
                    x2i = xr - xi;
                    x7r = tmp[6 + j] - tmp[9 + j];
                    xr = tmp[6 + j] + tmp[9 + j];
                    x7i = tmp[14 + j] - tmp[1 + j];
                    xi = tmp[14 + j] + tmp[1 + j];
                    x3r = xr + xi;
                    x3i = xr - xi;
                    xr = x0r + x2r;
                    xi = x1r + x3r;

                    //tmp[0 + k] = C16_8R * (xr + xi); //
                    ftmp = C16_8R * (xr + xi);
                    itmp = (int)(ftmp * fQuantizeTable16[k]);
                    iout[fCopyMatrix16[k]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[8 * Constants.TerrainPatchSize + k] = C16_8R * (xr - xi); //
                    ftmp = C16_8R * (xr - xi);
                    indx = 8 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    xr = x0r - x2r;
                    xi = x1r - x3r;

                    //tmp[4 * Constants.TerrainPatchSize + k] = C16_4R * xr - C16_4I * xi; //
                    ftmp = C16_4R * xr - C16_4I * xi;
                    indx = 4 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[12 * Constants.TerrainPatchSize + k] = C16_4R * xi + C16_4I * xr;  //
                    ftmp = C16_4R * xi + C16_4I * xr;
                    indx = 12 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    x0r = W16_8R * (x1i - x3i);
                    x2r = W16_8R * (x1i + x3i);
                    xr = x0i + x0r;
                    xi = x2r + x2i;

                    //tmp[2 * Constants.TerrainPatchSize + k] = C16_2R * xr - C16_2I * xi;  //
                    ftmp = C16_2R * xr - C16_2I * xi;
                    indx = 2 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[14 * Constants.TerrainPatchSize + k] = C16_2R * xi + C16_2I * xr;  //
                    ftmp = C16_2R * xi + C16_2I * xr;
                    indx = 14 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    xr = x0i - x0r;
                    xi = x2r - x2i;

                    //tmp[6 * Constants.TerrainPatchSize + k] = C16_6R * xr - C16_6I * xi;  //
                    ftmp = C16_6R * xr - C16_6I * xi;
                    indx = 6 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[10 * Constants.TerrainPatchSize + k] = C16_6R * xi + C16_6I * xr; //
                    ftmp = C16_6R * xi + C16_6I * xr;
                    indx = 10 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    xr = W16_8R * (x6r - x6i);
                    xi = W16_8R * (x6i + x6r);
                    x6r = x4r - xr;
                    x6i = x4i - xi;
                    x4r += xr;
                    x4i += xi;
                    xr = W16_4I * x7r - W16_4R * x7i;
                    xi = W16_4I * x7i + W16_4R * x7r;
                    x7r = W16_4R * x5r - W16_4I * x5i;
                    x7i = W16_4R * x5i + W16_4I * x5r;
                    x5r = x7r + xr;
                    x5i = x7i + xi;
                    x7r -= xr;
                    x7i -= xi;
                    xr = x4r + x5r;
                    xi = x5i + x4i;

                    //tmp[1 * Constants.TerrainPatchSize + k] = C16_1R * xr - C16_1I * xi;  //
                    ftmp = C16_1R * xr - C16_1I * xi;
                    indx = 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[15 * Constants.TerrainPatchSize + k] = C16_1R * xi + C16_1I * xr;  //
                    ftmp = C16_1R * xi + C16_1I * xr;
                    indx = 15 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    xr = x4r - x5r;
                    xi = x5i - x4i;

                    //tmp[7 * Constants.TerrainPatchSize + k] = C16_7R * xr - C16_7I * xi;  //
                    ftmp = C16_7R * xr - C16_7I * xi;
                    indx = 7 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[9 * Constants.TerrainPatchSize + k] = C16_7R * xi + C16_7I * xr;  //
                    ftmp = C16_7R * xi + C16_7I * xr;
                    indx = 9 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    xr = x6r - x7i;
                    xi = x7r + x6i;

                    //tmp[5 * Constants.TerrainPatchSize + k] = C16_5R * xr - C16_5I * xi;  //
                    ftmp = C16_5R * xr - C16_5I * xi;
                    indx = 5 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[11 * Constants.TerrainPatchSize + k] = C16_5R * xi + C16_5I * xr;  //
                    ftmp = C16_5R * xi + C16_5I * xr;
                    indx = 11 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    xr = x6r + x7i;
                    xi = x7r - x6i;

                    //tmp[3 * Constants.TerrainPatchSize + k] = C16_3R * xr - C16_3I * xi;  //
                    ftmp = C16_3R * xr - C16_3I * xi;
                    indx = 3 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }

                    //tmp[13 * Constants.TerrainPatchSize + k] = C16_3R * xi + C16_3I * xr;  //
                    ftmp = C16_3R * xi + C16_3I * xr;
                    indx = 13 * 16 + k;
                    itmp = (int)(ftmp * fQuantizeTable16[indx]);
                    iout[fCopyMatrix16[indx]] = itmp;

                    if (dowbits)
                    {
                        if (itmp < 0) itmp *= -1;
                        while (itmp > wbitsMaxValue)
                        {
                            wbits++;
                            wbitsMaxValue = 1 << wbits;
                            if (wbits == maxwbits)
                            {
                                dowbits = false;
                                break;
                            }
                        }
                    }
                }
            }
        }

        #endregion DCT

        #region IDCT
        /* not in use
        private static void IDCTColumn16(float[] linein, float[] lineout, int column)
        {
            for (int n = 0; n < Constants.TerrainPatchSize; n++)
            {
                float total = OO_SQRT2 * linein[column];

                for (int u = 1; u < Constants.TerrainPatchSize; u++)
                {
                    int usize = u * Constants.TerrainPatchSize;
                    total += linein[usize + column] * CosineTable16[usize + n];
                }

                lineout[Constants.TerrainPatchSize * n + column] = total;
            }
        }

        private static void IDCTLine16(float[] linein, float[] lineout, int line)
        {
            const float oosob = 2.0f / Constants.TerrainPatchSize;
            int lineSize = line * Constants.TerrainPatchSize;

            for (int n = 0; n < Constants.TerrainPatchSize; n++)
            {
                float total = OO_SQRT2 * linein[lineSize];

                for (int u = 1; u < Constants.TerrainPatchSize; u++)
                {
                    total += linein[lineSize + u] * CosineTable16[u * Constants.TerrainPatchSize + n];
                }

                lineout[lineSize + n] = total * oosob;
            }
        }

        //not in use, and still not fixed
        /*
                static void idct16x16(float[] a)
                        {
                            int j;
                            float x0r, x0i, x1r, x1i, x2r, x2i, x3r, x3i;
                            float x4r, x4i, x5r, x5i, x6r, x6i, x7r, x7i;
                            float xr, xi;

                            int fullSize = Constants.TerrainPatchSize * Constants.TerrainPatchSize;

                            for (j = 0; j < fullSize; j += Constants.TerrainPatchSize)
                            {
                                x5r = C16_1R * tmp[1 + j] + C16_1I * tmp[15 + j];
                                x5i = C16_1R * tmp[15 + j] - C16_1I * tmp[1 + j];
                                xr = C16_7R * tmp[7 + j] + C16_7I * tmp[9 + j];
                                xi = C16_7R * tmp[9 + j] - C16_7I * tmp[7 + j];
                                x4r = x5r + xr;
                                x4i = x5i - xi;
                                x5r -= xr;
                                x5i += xi;
                                x7r = C16_5R * tmp[5 + j] + C16_5I * tmp[11 + j];
                                x7i = C16_5R * tmp[11 + j] - C16_5I * tmp[5 + j];
                                xr = C16_3R * tmp[3 + j] + C16_3I * tmp[13 + j];
                                xi = C16_3R * tmp[13 + j] - C16_3I * tmp[3 + j];
                                x6r = x7r + xr;
                                x6i = x7i - xi;
                                x7r -= xr;
                                x7i += xi;
                                xr = x4r - x6r;
                                xi = x4i - x6i;
                                x4r += x6r;
                                x4i += x6i;
                                x6r = W16_8R * (xi + xr);
                                x6i = W16_8R * (xi - xr);
                                xr = x5r + x7i;
                                xi = x5i - x7r;
                                x5r -= x7i;
                                x5i += x7r;
                                x7r = W16_4I * x5r + W16_4R * x5i;
                                x7i = W16_4I * x5i - W16_4R * x5r;
                                x5r = W16_4R * xr + W16_4I * xi;
                                x5i = W16_4R * xi - W16_4I * xr;
                                xr = C16_4R * tmp[4 + j] + C16_4I * tmp[12 + j];
                                xi = C16_4R * tmp[12 + j] - C16_4I * tmp[4 + j];
                                x2r = C16_8R * (tmp[0 + j] + tmp[8 + j]);
                                x3r = C16_8R * (tmp[0 + j] - tmp[8 + j]);
                                x0r = x2r + xr;
                                x1r = x3r + xi;
                                x2r -= xr;
                                x3r -= xi;
                                x0i = C16_2R * tmp[2 + j] + C16_2I * tmp[14 + j];
                                x2i = C16_2R * tmp[14 + j] - C16_2I * tmp[2 + j];
                                x1i = C16_6R * tmp[6 + j] + C16_6I * tmp[10 + j];
                                x3i = C16_6R * tmp[10 + j] - C16_6I * tmp[6 + j];
                                xr = x0i - x1i;
                                xi = x2i + x3i;
                                x0i += x1i;
                                x2i -= x3i;
                                x1i = W16_8R * (xi + xr);
                                x3i = W16_8R * (xi - xr);
                                xr = x0r + x0i;
                                xi = x0r - x0i;
                                tmp[0 + j] = xr + x4r;
                                tmp[15 + j] = xr - x4r;
                                tmp[8 + j] = xi + x4i;
                                tmp[7 + j] = xi - x4i;
                                xr = x1r + x1i;
                                xi = x1r - x1i;
                                tmp[2 + j] = xr + x5r;
                                tmp[13 + j] = xr - x5r;
                                tmp[10 + j] = xi + x5i;
                                tmp[5 + j] = xi - x5i;
                                xr = x2r + x2i;
                                xi = x2r - x2i;
                                tmp[4 + j] = xr + x6r;
                                tmp[11 + j] = xr - x6r;
                                tmp[12 + j] = xi + x6i;
                                tmp[3 + j] = xi - x6i;
                                xr = x3r + x3i;
                                xi = x3r - x3i;
                                tmp[6 + j] = xr + x7r;
                                tmp[9 + j] = xr - x7r;
                                tmp[14 + j] = xi + x7i;
                                tmp[1 + j] = xi - x7i;
                            }
                            for (j = 0; j < fullSize; j += Constants.TerrainPatchSize)
                            {
                                x5r = C16_1R * tmp[j + 1] + C16_1I * tmp[j + 15];
                                x5i = C16_1R * tmp[j + 15] - C16_1I * tmp[j + 1];
                                xr = C16_7R * tmp[j + 7] + C16_7I * tmp[j + 9];
                                xi = C16_7R * tmp[j + 9] - C16_7I * tmp[j + 7];
                                x4r = x5r + xr;
                                x4i = x5i - xi;
                                x5r -= xr;
                                x5i += xi;
                                x7r = C16_5R * tmp[j + 5] + C16_5I * tmp[j + 11];
                                x7i = C16_5R * tmp[j + 11] - C16_5I * tmp[j + 5];
                                xr = C16_3R * tmp[j + 3] + C16_3I * tmp[j + 13];
                                xi = C16_3R * tmp[j + 13] - C16_3I * tmp[j + 3];
                                x6r = x7r + xr;
                                x6i = x7i - xi;
                                x7r -= xr;
                                x7i += xi;
                                xr = x4r - x6r;
                                xi = x4i - x6i;
                                x4r += x6r;
                                x4i += x6i;
                                x6r = W16_8R * (xi + xr);
                                x6i = W16_8R * (xi - xr);
                                xr = x5r + x7i;
                                xi = x5i - x7r;
                                x5r -= x7i;
                                x5i += x7r;
                                x7r = W16_4I * x5r + W16_4R * x5i;
                                x7i = W16_4I * x5i - W16_4R * x5r;
                                x5r = W16_4R * xr + W16_4I * xi;
                                x5i = W16_4R * xi - W16_4I * xr;
                                xr = C16_4R * tmp[j + 4] + C16_4I * tmp[j + 12];
                                xi = C16_4R * tmp[j + 12] - C16_4I * tmp[j + 4];
                                x2r = C16_8R * (tmp[j + 0] + tmp[j + 8]);
                                x3r = C16_8R * (tmp[j + 0] - tmp[j + 8]);
                                x0r = x2r + xr;
                                x1r = x3r + xi;
                                x2r -= xr;
                                x3r -= xi;
                                x0i = C16_2R * tmp[j + 2] + C16_2I * tmp[j + 14];
                                x2i = C16_2R * tmp[j + 14] - C16_2I * tmp[j + 2];
                                x1i = C16_6R * tmp[j + 6] + C16_6I * tmp[j + 10];
                                x3i = C16_6R * tmp[j + 10] - C16_6I * tmp[j + 6];
                                xr = x0i - x1i;
                                xi = x2i + x3i;
                                x0i += x1i;
                                x2i -= x3i;
                                x1i = W16_8R * (xi + xr);
                                x3i = W16_8R * (xi - xr);
                                xr = x0r + x0i;
                                xi = x0r - x0i;
                                tmp[j + 0] = xr + x4r;
                                tmp[j + 15] = xr - x4r;
                                tmp[j + 8] = xi + x4i;
                                tmp[j + 7] = xi - x4i;
                                xr = x1r + x1i;
                                xi = x1r - x1i;
                                tmp[j + 2] = xr + x5r;
                                tmp[j + 13] = xr - x5r;
                                tmp[j + 10] = xi + x5i;
                                tmp[j + 5] = xi - x5i;
                                xr = x2r + x2i;
                                xi = x2r - x2i;
                                tmp[j + 4] = xr + x6r;
                                tmp[j + 11] = xr - x6r;
                                tmp[j + 12] = xi + x6i;
                                tmp[j + 3] = xi - x6i;
                                xr = x3r + x3i;
                                xi = x3r - x3i;
                                tmp[j + 6] = xr + x7r;
                                tmp[j + 9] = xr - x7r;
                                tmp[j + 14] = xi + x7i;
                                tmp[j + 1] = xi - x7i;
                            }
                        }
                   */
        #endregion IDCT
    }
}
