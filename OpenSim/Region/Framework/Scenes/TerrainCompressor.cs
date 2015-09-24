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

using System;
using System.Collections.Generic;
using System.Reflection;

using log4net;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public static class OpenSimTerrainCompressor
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

#pragma warning disable 414
        private static string LogHeader = "[TERRAIN COMPRESSOR]";
#pragma warning restore 414

        public const int END_OF_PATCHES = 97;

        private const float OO_SQRT2 = 0.7071067811865475244008443621049f;
        private const int STRIDE = 264;

        private const int ZERO_CODE = 0x0;
        private const int ZERO_EOB = 0x2;
        private const int POSITIVE_VALUE = 0x6;
        private const int NEGATIVE_VALUE = 0x7;


         private static readonly float[] CosineTable16 = new float[Constants.TerrainPatchSize*Constants.TerrainPatchSize];
        private static readonly int[] CopyMatrix16 = new int[Constants.TerrainPatchSize*Constants.TerrainPatchSize];

        private static readonly float[] QuantizeTable16 =
            new float[Constants.TerrainPatchSize * Constants.TerrainPatchSize];
        private static readonly float[] DequantizeTable16 =
            new float[Constants.TerrainPatchSize * Constants.TerrainPatchSize];

        static OpenSimTerrainCompressor()
        {
            // Initialize the decompression tables
            BuildDequantizeTable16();
            SetupCosines16();
            BuildCopyMatrix16();
            BuildQuantizeTable16();
        }

        // Used to send cloud and wind patches
        public static LayerDataPacket CreateLayerDataPacketStandardSize(TerrainPatch[] patches, byte type)
        {
            LayerDataPacket layer = new LayerDataPacket {LayerID = {Type = type}};

            TerrainPatch.GroupHeader header = new TerrainPatch.GroupHeader
                                                  {Stride = STRIDE, PatchSize = Constants.TerrainPatchSize};

            // Should be enough to fit even the most poorly packed data
            byte[] data = new byte[patches.Length*Constants.TerrainPatchSize*Constants.TerrainPatchSize*2];
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
            EncodePatchHeader(output, header, patch, false, ref wbits);
            EncodePatch(output, patch, 0, wbits);
        }

        private static TerrainPatch.Header PrescanPatch(float[] patch)
        {
            TerrainPatch.Header header = new TerrainPatch.Header();
            float zmax = -99999999.0f;
            float zmin = 99999999.0f;

            for (int i = 0; i < Constants.TerrainPatchSize * Constants.TerrainPatchSize; i++)
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
            float[] block = new float[Constants.TerrainPatchSize * Constants.TerrainPatchSize];
            float oozrange = 1.0f / header.Range;
            float range = (1 << prequant);
            float premult = oozrange * range;


            float sub = 0.5f * header.Range + header.DCOffset;

            int wordsize = (prequant - 2) & 0x0f;
            header.QuantWBits = wordsize;
            header.QuantWBits |= wordsize << 4;

            int k = 0;
            for (int j = 0; j < Constants.TerrainPatchSize; j++)
            {
                for (int i = 0; i < Constants.TerrainPatchSize; i++)
                    block[k++] = (patchData[j * Constants.TerrainPatchSize + i] - sub) * premult;
            }

            float[] ftemp = new float[Constants.TerrainPatchSize * Constants.TerrainPatchSize];
            int[] itemp = new int[Constants.TerrainPatchSize * Constants.TerrainPatchSize];

            wbits = (prequant >> 1);

            for (int o = 0; o < Constants.TerrainPatchSize; o++)
                DCTLine16(block, ftemp, o);
            for (int o = 0; o < Constants.TerrainPatchSize; o++)
                wbits = DCTColumn16Wbits(ftemp, itemp, o, wbits);

            return itemp;
        }

        // new using terrain data and patchs indexes
        public static List<LayerDataPacket> CreateLayerDataPackets(TerrainData terrData, int[] x, int[] y, byte landPacketType)
        {
            List<LayerDataPacket> ret = new List<LayerDataPacket>();

            //create packet and global header
            LayerDataPacket layer = new LayerDataPacket();

            layer.LayerID.Type = landPacketType;

            byte[] data = new byte[x.Length * Constants.TerrainPatchSize * Constants.TerrainPatchSize * 2];
            BitPack bitpack = new BitPack(data, 0);
            bitpack.PackBits(STRIDE, 16);
            bitpack.PackBits(Constants.TerrainPatchSize, 8);
            bitpack.PackBits(landPacketType, 8);

            for (int i = 0; i < x.Length; i++)
            {
                CreatePatchFromHeightmap(bitpack, terrData, x[i], y[i]);
                if (bitpack.BytePos > 1000 && i != x.Length - 1)
                {
                    //finish this packet
                    bitpack.PackBits(END_OF_PATCHES, 8);

                    layer.LayerData.Data = new byte[bitpack.BytePos + 1];
                    Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);
                    ret.Add(layer);

                    // start another
                    layer = new LayerDataPacket();
                    layer.LayerID.Type = landPacketType;
                    
                    bitpack = new BitPack(data, 0);
                    bitpack.PackBits(STRIDE, 16);
                    bitpack.PackBits(Constants.TerrainPatchSize, 8);
                    bitpack.PackBits(landPacketType, 8);
                }
            }

            bitpack.PackBits(END_OF_PATCHES, 8);

            layer.LayerData.Data = new byte[bitpack.BytePos + 1];
            Buffer.BlockCopy(bitpack.Data, 0, layer.LayerData.Data, 0, bitpack.BytePos + 1);
            ret.Add(layer);

            return ret;
        }

        /// <summary>
        ///     Add a patch of terrain to a BitPacker
        /// </summary>
        /// <param name="output">BitPacker to write the patch to</param>
        /// <param name="heightmap">
        ///     Heightmap of the simulator. Presumed to be an sizeX*sizeY array.
        /// </param>
        /// <param name="patchX">
        ///     X offset of the patch to create.
        /// </param>
        /// <param name="patchY">
        ///     Y offset of the patch to create.
        /// </param>
        /// <param name="pRegionSizeX"></param>
        /// <param name="pRegionSizeY"></param>
        public static void CreatePatchFromHeightmap(BitPack output, TerrainData terrData, int patchX, int patchY)
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

            if(Math.Round((double)frange,2) == 1.0)
            {
                // flat terrain speed up things

                header.DCOffset -= 0.5f;

                header.QuantWBits = 0x00;
                output.PackBits(header.QuantWBits, 8);
                output.PackFloat(header.DCOffset);
                output.PackBits(1, 16);
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
            EncodePatchHeader(output, header, patch, largeRegion, ref wbits);
            EncodePatch(output, patch, 0, wbits);
        }



        // Scan the height info we're returning and return a patch packet header for this patch.
        private static TerrainPatch.Header PrescanPatch(TerrainData terrData, int patchX, int patchY, out float frange)
        {
            TerrainPatch.Header header = new TerrainPatch.Header();
            float zmax = -99999999.0f;
            float zmin = 99999999.0f;
            int startx = patchX * Constants.TerrainPatchSize;
            int starty = patchY * Constants.TerrainPatchSize;

            for (int j = starty; j < starty + Constants.TerrainPatchSize; j++)
            {
                for (int i = startx; i < startx + Constants.TerrainPatchSize; i++)
                {
                    float val = terrData[i, j];
                    if (val > zmax) zmax = val;
                    if (val < zmin) zmin = val;
                }
            }

            header.DCOffset = zmin;
            frange = ((zmax - zmin) + 1.0f);
            header.Range = (int)frange;

            return header;
        }

        public static TerrainPatch.Header DecodePatchHeader(BitPack bitpack)
        {
            TerrainPatch.Header header = new TerrainPatch.Header {QuantWBits = bitpack.UnpackBits(8)};

            // Quantized word bits
            if (header.QuantWBits == END_OF_PATCHES)
                return header;

            // DC offset
            header.DCOffset = bitpack.UnpackFloat();

            // Range
            header.Range = bitpack.UnpackBits(16);

            // Patch IDs (10 bits)
            header.PatchIDs = bitpack.UnpackBits(10);

            // Word bits
            header.WordBits = (uint) ((header.QuantWBits & 0x0f) + 2);

            return header;
        }

        private static void EncodePatchHeader(BitPack output, TerrainPatch.Header header, int[] patch, bool largeRegion, ref int wbits)
        {
            if (wbits > 17)
                wbits = 17;
            else if (wbits < 2)
                wbits = 2;

            header.QuantWBits &= 0xf0;
            header.QuantWBits |= (wbits - 2);

            output.PackBits(header.QuantWBits, 8);
            output.PackFloat(header.DCOffset);
            output.PackBits(header.Range, 16);
            if (largeRegion)
                output.PackBits(header.PatchIDs, 32);
            else
                output.PackBits(header.PatchIDs, 10);
        }

        private static void IDCTColumn16(float[] linein, float[] lineout, int column)
        {
            for (int n = 0; n < Constants.TerrainPatchSize; n++)
            {
                float total = OO_SQRT2*linein[column];

                for (int u = 1; u < Constants.TerrainPatchSize; u++)
                {
                    int usize = u*Constants.TerrainPatchSize;
                    total += linein[usize + column]*CosineTable16[usize + n];
                }

                lineout[Constants.TerrainPatchSize*n + column] = total;
            }
        }

        private static void IDCTLine16(float[] linein, float[] lineout, int line)
        {
            const float oosob = 2.0f/Constants.TerrainPatchSize;
            int lineSize = line*Constants.TerrainPatchSize;

            for (int n = 0; n < Constants.TerrainPatchSize; n++)
            {
                float total = OO_SQRT2*linein[lineSize];

                for (int u = 1; u < Constants.TerrainPatchSize; u++)
                {
                    total += linein[lineSize + u]*CosineTable16[u*Constants.TerrainPatchSize + n];
                }

                lineout[lineSize + n] = total*oosob;
            }
        }

        private static void DCTLine16(float[] linein, float[] lineout, int line)
        {
            // outputs transpose data

            float total = 0.0f;
            int lineSize = line*Constants.TerrainPatchSize;

            for (int n = 0; n < Constants.TerrainPatchSize; n++)
            {
                total += linein[lineSize + n];
            }

            lineout[line] = OO_SQRT2*total;

            for (int u = Constants.TerrainPatchSize;
                 u < Constants.TerrainPatchSize*Constants.TerrainPatchSize;
                 u += Constants.TerrainPatchSize)
            {
                total = 0.0f;
                for (int ptrn = lineSize, ptru = u; ptrn < lineSize + Constants.TerrainPatchSize; ptrn++,ptru++)
                {
                    total += linein[ptrn]*CosineTable16[ptru];
                }

                lineout[line + u] = total;
            }
        }

        private static int DCTColumn16Wbits(float[] linein, int[] lineout, int column, int wbits)
        {
            // input columns are in fact stored in lines now

            const int maxwbits = 17; // per header encoding

            bool dowbits = wbits < 17;

            int wbitsMaxValue = 1 << wbits;

            float total = 0.0f;
            //            const float oosob = 2.0f / Constants.TerrainPatchSize;
            int inlinesptr = Constants.TerrainPatchSize*column;

            for (int n = 0; n < Constants.TerrainPatchSize; n++)
            {
                total += linein[inlinesptr + n];
            }

            int tmp = (int) (OO_SQRT2*total*QuantizeTable16[column]);
            lineout[CopyMatrix16[column]] = tmp;

            if (dowbits)
            {
                if (tmp < 0) tmp *= -1;
                while (tmp > wbitsMaxValue)
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

            for (int uptr = Constants.TerrainPatchSize;
                 uptr < Constants.TerrainPatchSize*Constants.TerrainPatchSize;
                 uptr += Constants.TerrainPatchSize)
            {
                total = 0.0f;

                for (int n = inlinesptr, ptru = uptr; n < inlinesptr + Constants.TerrainPatchSize; n++, ptru++)
                {
                    total += linein[n]*CosineTable16[ptru];
                }

                tmp = (int) (total*QuantizeTable16[uptr + column]);
                lineout[CopyMatrix16[uptr + column]] = tmp;

                if (dowbits)
                {
                    if (tmp < 0) tmp *= -1;
                    while (tmp > wbitsMaxValue)
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
            return wbits;
        }

        public static void DecodePatch(int[] patches, BitPack bitpack, TerrainPatch.Header header, int size)
        {
            for (int n = 0; n < size*size; n++)
            {
                // ?
                int temp = bitpack.UnpackBits(1);
                if (temp != 0)
                {
                    // Value or EOB
                    temp = bitpack.UnpackBits(1);
                    if (temp != 0)
                    {
                        // Value
                        temp = bitpack.UnpackBits(1);
                        if (temp != 0)
                        {
                            // Negative
                            temp = bitpack.UnpackBits((int) header.WordBits);
                            patches[n] = temp*-1;
                        }
                        else
                        {
                            // Positive
                            temp = bitpack.UnpackBits((int) header.WordBits);
                            patches[n] = temp;
                        }
                    }
                    else
                    {
                        // Set the rest to zero
                        // TODO: This might not be necessary
                        for (int o = n; o < size*size; o++)
                        {
                            patches[o] = 0;
                        }
                        break;
                    }
                }
                else
                {
                    patches[n] = 0;
                }
            }
        }

        private static void EncodePatch(BitPack output, int[] patch, int postquant, int wbits)
        {
            int maxwbitssize = (1 << wbits) - 1;
            int fullSize = Constants.TerrainPatchSize * Constants.TerrainPatchSize;

            if (postquant > fullSize || postquant < 0)
            {
                Logger.Log("Postquant is outside the range of allowed values in EncodePatch()", Helpers.LogLevel.Error);
                return;
            }

            if (postquant != 0)
                patch[fullSize - postquant] = 0;

            int lastZeroindx = fullSize - postquant;

            for (int i = 0; i < fullSize; i++)
            {
                int temp = patch[i];

                if (temp == 0)
                {
                    bool eob = true;

                    for (int j = i; j < lastZeroindx ; j++)
                    {
                        if (patch[j] != 0)
                        {
                            eob = false;
                            break;
                        }
                    }

                    if (eob)
                    {
                        output.PackBits(ZERO_EOB, 2);
                        return;
                    }
                    output.PackBits(ZERO_CODE, 1);
                }
                else
                {
                    if (temp < 0)
                    {
                        temp *= -1;

                        if (temp > maxwbitssize) temp = maxwbitssize;

                        output.PackBits(NEGATIVE_VALUE, 3);
                        output.PackBits(temp, wbits);
                    }
                    else
                    {
                        if (temp > maxwbitssize) temp = maxwbitssize;

                        output.PackBits(POSITIVE_VALUE, 3);
                        output.PackBits(temp, wbits);
                    }
                }
            }
        }


        private static int[] CompressPatch(float[,] patchData, TerrainPatch.Header header, int prequant, out int wbits)
        {
            float[] block = new float[Constants.TerrainPatchSize*Constants.TerrainPatchSize];
            float oozrange = 1.0f/header.Range;
            float range = (1 << prequant);
            float premult = oozrange*range;

            float sub = 0.5f * header.Range + header.DCOffset;

            int wordsize = (prequant - 2) & 0x0f;
            header.QuantWBits = wordsize;
            header.QuantWBits |= wordsize << 4;

            int k = 0;
            for (int j = 0; j < Constants.TerrainPatchSize; j++)
            {
                for (int i = 0; i < Constants.TerrainPatchSize; i++)
                    block[k++] = (patchData[j, i] - sub) * premult;
            }

            float[] ftemp = new float[Constants.TerrainPatchSize*Constants.TerrainPatchSize];
            int[] itemp = new int[Constants.TerrainPatchSize*Constants.TerrainPatchSize];

            wbits = (prequant >> 1);

            for (int o = 0; o < Constants.TerrainPatchSize; o++)
                DCTLine16(block, ftemp, o);
            for (int o = 0; o < Constants.TerrainPatchSize; o++)
                wbits = DCTColumn16Wbits(ftemp, itemp, o, wbits);

            return itemp;
        }

        private static int[] CompressPatch(TerrainData terrData, int patchX, int patchY, TerrainPatch.Header header,
                                                               int prequant, out int wbits)
        {
            float[] block = new float[Constants.TerrainPatchSize*Constants.TerrainPatchSize];
           
            float oozrange = 1.0f/header.Range;
            float invprequat = (1 << prequant);
            float premult = oozrange* invprequat;

            float sub = 0.5f * header.Range + header.DCOffset;

            int wordsize = (prequant -2 ) & 0x0f;
            header.QuantWBits = wordsize;
            header.QuantWBits |= wordsize << 4;

            int k = 0;

            int yPatchLimit = patchY >= (terrData.SizeY / Constants.TerrainPatchSize) ?
                            (terrData.SizeY - Constants.TerrainPatchSize) / Constants.TerrainPatchSize : patchY;
            yPatchLimit = (yPatchLimit + 1) * Constants.TerrainPatchSize;

            int xPatchLimit = patchX >= (terrData.SizeX / Constants.TerrainPatchSize) ?
                            (terrData.SizeX - Constants.TerrainPatchSize) / Constants.TerrainPatchSize : patchX;
            xPatchLimit = (xPatchLimit + 1) * Constants.TerrainPatchSize;

            for (int yy = patchY * Constants.TerrainPatchSize; yy < yPatchLimit; yy++)
            {
                for (int xx = patchX * Constants.TerrainPatchSize; xx < xPatchLimit; xx++)
                {
                    block[k++] = (terrData[xx, yy]  - sub) * premult;
                }
            }

            float[] ftemp = new float[Constants.TerrainPatchSize*Constants.TerrainPatchSize];
            int[] itemp = new int[Constants.TerrainPatchSize*Constants.TerrainPatchSize];

            wbits = (prequant >> 1);

            for (int o = 0; o < Constants.TerrainPatchSize; o++)
                DCTLine16(block, ftemp, o);
            for (int o = 0; o < Constants.TerrainPatchSize; o++)
                wbits = DCTColumn16Wbits(ftemp, itemp, o, wbits);

            return itemp;
        }

        #region Initialization

        private static void BuildDequantizeTable16()
        {
            for (int j = 0; j < Constants.TerrainPatchSize; j++)
            {
                for (int i = 0; i < Constants.TerrainPatchSize; i++)
                {
                    DequantizeTable16[j*Constants.TerrainPatchSize + i] = 1.0f + 2.0f*(i + j);
                }
            }
        }

        private static void BuildQuantizeTable16()
        {
            const float oosob = 2.0f/Constants.TerrainPatchSize;
            for (int j = 0; j < Constants.TerrainPatchSize; j++)
            {
                for (int i = 0; i < Constants.TerrainPatchSize; i++)
                {
                    QuantizeTable16[j*Constants.TerrainPatchSize + i] = oosob/(1.0f + 2.0f*(i + (float) j));
                }
            }
        }

        private static void SetupCosines16()
        {
            const float hposz = (float) Math.PI*0.5f/Constants.TerrainPatchSize;

            for (int u = 0; u < Constants.TerrainPatchSize; u++)
            {
                for (int n = 0; n < Constants.TerrainPatchSize; n++)
                {
                    CosineTable16[u*Constants.TerrainPatchSize + n] = (float) Math.Cos((2.0f*n + 1.0f)*u*hposz);
                }
            }
        }

        private static void BuildCopyMatrix16()
        {
            bool diag = false;
            bool right = true;
            int i = 0;
            int j = 0;
            int count = 0;

            while (i < Constants.TerrainPatchSize && j < Constants.TerrainPatchSize)
            {
                CopyMatrix16[j*Constants.TerrainPatchSize + i] = count++;

                if (!diag)
                {
                    if (right)
                    {
                        if (i < Constants.TerrainPatchSize - 1) i++;
                        else j++;

                        right = false;
                        diag = true;
                    }
                    else
                    {
                        if (j < Constants.TerrainPatchSize - 1) j++;
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
                        if (i == Constants.TerrainPatchSize - 1 || j == 0) diag = false;
                    }
                    else
                    {
                        i--;
                        j++;
                        if (j == Constants.TerrainPatchSize - 1 || i == 0) diag = false;
                    }
                }
            }
        }

        #endregion Initialization
    }
}
