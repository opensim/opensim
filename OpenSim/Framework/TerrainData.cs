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

using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;

using log4net;

namespace OpenSim.Framework
{
    // The terrain is stored in the database as a blob with a 'revision' field.
    // Some implementations of terrain storage would fill the revision field with
    //    the time the terrain was stored. When real revisions were added and this
    //    feature removed, that left some old entries with the time in the revision
    //    field.
    // Thus, if revision is greater than 'RevisionHigh' then terrain db entry is
    //    left over and it is presumed to be 'Legacy256'.
    // Numbers are arbitrary and are chosen to to reduce possible mis-interpretation.
    // If a revision does not match any of these, it is assumed to be Legacy256.
    public enum DBTerrainRevision
    {
        // Terrain is 'double[256,256]'
        Legacy256 = 11,

        // Terrain is 'int32, int32, float[,]' where the ints are X and Y dimensions
        // The dimensions are presumed to be multiples of 16 and, more likely, multiples of 256.
        Variable2D = 22,
        Variable2DGzip = 23,

        // Terrain is 'int32, int32, int32, int16[]' where the ints are X and Y dimensions
        //   and third int is the 'compression factor'. The heights are compressed as
        //   "ushort compressedHeight = (ushort)(height * compressionFactor);"
        // The dimensions are presumed to be multiples of 16 and, more likely, multiples of 256.
        Compressed2D = 27,

        // A revision that is not listed above or any revision greater than this value is 'Legacy256'.
        RevisionHigh = 1234
    }

    public class TerrainData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[TERRAIN DATA]";

        private float[,] m_heightmap;
        // Remember subregions of the heightmap that has changed.

        private TerrainTaintsArray m_taints;
        private readonly int m_taintSizeX;
        private readonly int m_taintSizeY;
        private readonly int m_mapStride;
        private readonly int m_mapPatchsStride;

        // legacy CompressionFactor
        public float CompressionFactor { get; private set; }

        public int SizeX { get; protected set; }
        public int SizeY { get; protected set; }
        public const int SizeZ = 0;

        // A height used when the user doesn't specify anything
        public const float DefaultTerrainHeight = 21f;

        // Given a revision code and a blob from the database, create and return the right type of TerrainData.
        // The sizes passed are the expected size of the region. The database info will be used to
        //     initialize the heightmap of that sized region with as much data is in the blob.
        // Return created TerrainData or 'null' if unsuccessful.
        public static TerrainData CreateFromDatabaseBlobFactory(int pSizeX, int pSizeY, int pSizeZ, int pFormatCode, byte[] pBlob)
        {
            // For the moment, there is only one implementation class
            return new TerrainData(pSizeX, pSizeY, pSizeZ, pFormatCode, pBlob);
        }

        public float this[int x, int y]
        {
            get { return m_heightmap[x, y]; }
            set
            {
                if (m_heightmap[x, y] != value)
                {
                    m_heightmap[x, y] = value;
                    int yy = y / Constants.TerrainPatchSize;
                    m_taints.Set(x / Constants.TerrainPatchSize + yy * m_taintSizeX, true);
                }
            }
        }

        public float this[int x, int y, int z]
        {
            get { return this[x, y]; }
            set { this[x, y] = value; }
        }

        public float GetHeight(float x, float y)
        {
            // integer indexs
            int ix, mix;
            int iy, miy;
            // interpolators offset
            float dx;
            float dy;

            if (x <= 0)
            {
                if(y <= 0)
                    return m_heightmap[0, 0];

                iy = (int)y;
                miy = SizeY - 1;
                if (iy >= miy)
                    return m_heightmap[0, miy];

                dy = y - iy;

                float h = m_heightmap[0, iy];
                ++iy;
                return h + (m_heightmap[0, iy] - h) * dy;
            }

            ix = (int)x;
            mix = SizeX - 1;

            if (ix >= mix)
            {
                if(y <= 0)
                    return m_heightmap[mix, 0];

                iy = (int)y;
                miy = SizeY - 1;

                if (y >= miy)
                    return m_heightmap[mix, miy];

                dy = y - iy;

                float h = m_heightmap[mix, iy];
                ++iy;
                return h + (m_heightmap[mix, iy] - h) * dy;
            }

            dx = x - ix;

            if (y <= 0)
            {
                float h = m_heightmap[ix, 0];
                ++ix;
                return h + (m_heightmap[ix, 0] - h) * dx;
            }

            iy = (int)y;
            miy = SizeY - 1;

            if (iy >= miy)
            {
                float h = m_heightmap[ix, miy];
                ++ix;
                return h + (m_heightmap[ix, miy] - h) * dx;
            }

            dy = y - iy;

            float h0 = m_heightmap[ix, iy]; // 0,0 vertice
            float h1;
            float h2;

            if (dy > dx)
            {
                ++iy;
                h2 = m_heightmap[ix, iy]; // 0,1 vertice
                h1 = (h2 - h0) * dy; // 0,1 vertice minus 0,0
                ++ix;
                h2 = (m_heightmap[ix, iy] - h2) * dx; // 1,1 vertice minus 0,1
            }
            else
            {
                ++ix;
                h2 = m_heightmap[ix, iy]; // vertice 1,0
                h1 = (h2 - h0) * dx; // 1,0 vertice minus 0,0
                ++iy;
                h2 = (m_heightmap[ix, iy] - h2) * dy; // 1,1 vertice minus 1,0
            }
            return h0 + h1 + h2;
        }

        public TerrainTaintsArray GetTaints()
        {
            return m_taints;
        }

        public void ClearTaint()
        {
            m_taints.SetAll(false);
        }

        public void TaintAllTerrain()
        {
            m_taints.SetAll(true);
        }

        private void SetAllTaint(bool setting)
        {
            m_taints.SetAll(setting);
        }

        public void ClearLand()
        {
            ClearLand(DefaultTerrainHeight);
        }

        public void ClearLand(float pHeight)
        {
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    m_heightmap[xx, yy] = pHeight;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTainted()
        {
            return m_taints.IsTaited();
        }

        // Return 'true' of the patch that contains these region coordinates has been modified.
        // Note that checking the taint clears it.
        // There is existing code that relies on this feature.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTaintedAt(int xx, int yy, bool clearOnTest)
        {
            yy /= Constants.TerrainPatchSize;
            int indx = xx / Constants.TerrainPatchSize + yy * m_taintSizeX;
            return m_taints.Get(indx, clearOnTest);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTaintedAt(int xx, int yy)
        {
            yy /= Constants.TerrainPatchSize;
            return m_taints.Get(xx / Constants.TerrainPatchSize + yy * m_taintSizeX);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTaintedAtPatch(int xx, int yy, bool clearOnTest)
        {
            int indx = xx + yy * m_taintSizeX;
            return m_taints.Get(indx, clearOnTest);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTaintedAtPatch(int xx, int yy)
        {
            return m_taints.Get(xx + yy * m_taintSizeX);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTaintedAtPatch(int indx, bool clearOnTest)
        {
            return m_taints.Get(indx, clearOnTest);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTaintedAtPatchWithClear(int indx)
        {
            return m_taints.GetAndClear(indx);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsTaintedAtPatch(int indx)
        {
            return m_taints.Get(indx);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int GetAndClearNextTaint(int startIndex)
        {
            return m_taints.GetAndClearNextTrue(startIndex);
        }
        // TerrainData.GetDatabaseBlob
        // The user wants something to store in the database.
        public bool GetDatabaseBlob(out int DBRevisionCode, out Array blob)
        {
            DBRevisionCode = (int)DBTerrainRevision.Variable2DGzip;
            blob = ToCompressedTerrainSerializationV2DGzip();
            return true;
        }

        // TerrainData.GetCompressedMap
        public float[] GetCompressedMap()
        {
            float[] newMap = new float[SizeX * SizeY];

            int ind = 0;
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    newMap[ind++] = m_heightmap[xx, yy];

            return newMap;
        }

        public TerrainData Clone()
        {
            TerrainData ret = new TerrainData(SizeX, SizeY, SizeZ);
            ret.m_heightmap = (float[,])this.m_heightmap.Clone();

            return ret;
        }

        public float[] GetFloatsSerialized()
        {
            int points = SizeX * SizeY;
            float[] heights = new float[points];

            int idx = 0;
            for (int jj = 0; jj < SizeY; jj++)
                for (int ii = 0; ii < SizeX; ii++)
                {
                    heights[idx++] = m_heightmap[ii, jj];
                }

            return heights;
        }

        public double[,] GetDoubles()
        {
            double[,] ret = new double[SizeX, SizeY];
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    ret[xx, yy] = (double)m_heightmap[xx, yy];

            return ret;
        }

        public unsafe void GetPatchMinMax(int px, int py, out float zmin, out float zmax)
        {
            float min, max;
            fixed (float* map = m_heightmap)
            {
                float* p = map + px * m_mapPatchsStride + Constants.TerrainPatchSize * py;
                float* endp = p + m_mapPatchsStride;
                min = max = *p;
                float* y = p;
                int j = Constants.TerrainPatchSize - 1;

                do
                {
                    do
                    {
                        float val = *y++;
                        if (val > max)
                            max = val;
                        else if (val < min)
                            min = val;
                    }
                    while(--j > 0);

                    p += m_mapStride;
                    y = p;
                    j =  Constants.TerrainPatchSize;
                }
                while (p < endp);
            }
            zmin = min;
            zmax = max;
        }

        public unsafe void GetPatchBlock(float* block, int px, int py, float sub, float premult)
        {
            int k = 0;
            int startX = px * m_mapPatchsStride;
            int endX = startX + m_mapPatchsStride;
            int mpy = py * Constants.TerrainPatchSize;
            fixed (float* map = m_heightmap)
            {
                float* yp = map + mpy;
                float* yend = yp + 16;

                while (yp < yend)
                {
                    for (int x = startX; x < endX; x += m_mapStride)
                    {
                        block[k++] = (yp[x] - sub) * premult;
                    }
                    ++yp;
                }
            }
        }

/*
 //    that is coded as the float height times the compression factor (usually '100'
        //    to make for two decimal points).
        public short ToCompressedHeightshort(float pHeight)
        {
            // clamp into valid range
            pHeight *= CompressionFactor;
            if (pHeight < short.MinValue)
                return short.MinValue;
            else if (pHeight > short.MaxValue)
                return short.MaxValue;
            return (short)pHeight;
        }

        public ushort ToCompressedHeightushort(float pHeight)
        {
            // clamp into valid range
            pHeight *= CompressionFactor;
            if (pHeight < ushort.MinValue)
                return ushort.MinValue;
            else if (pHeight > ushort.MaxValue)
                return ushort.MaxValue;
            return (ushort)pHeight;
        }
*/

        public float FromCompressedHeight(short pHeight)
        {
            return ((float)pHeight) / CompressionFactor;
        }

        public float FromCompressedHeight(ushort pHeight)
        {
            return ((float)pHeight) / CompressionFactor;
        }

        // To keep with the legacy theme, create an instance of this class based on the
        //     way terrain used to be passed around.
        public TerrainData(double[,] pTerrain)
        {
            SizeX = pTerrain.GetLength(0);
            SizeY = pTerrain.GetLength(1);
            m_taintSizeX = SizeX / Constants.TerrainPatchSize;
            m_taintSizeY = SizeY / Constants.TerrainPatchSize;

            m_mapStride = SizeY;
            m_mapPatchsStride = m_mapStride * Constants.TerrainPatchSize;

            CompressionFactor = 100.0f;


            m_heightmap = new float[SizeX, SizeY];
            for (int ii = 0; ii < SizeX; ii++)
            {
                for (int jj = 0; jj < SizeY; jj++)
                {
                    m_heightmap[ii, jj] = (float)pTerrain[ii, jj];
                }
            }
            // m_log.DebugFormat("{0} new by doubles. sizeX={1}, sizeY={2}, sizeZ={3}", LogHeader, SizeX, SizeY, SizeZ);

            m_taints = new TerrainTaintsArray(m_taintSizeX * m_taintSizeY);
        }

        // Create underlying structures but don't initialize the heightmap assuming the caller will immediately do that
        public TerrainData(int pX, int pY, int pZ)
        {
            SizeX = pX;
            SizeY = pY;
            m_taintSizeX = SizeX / Constants.TerrainPatchSize;
            m_taintSizeY = SizeY / Constants.TerrainPatchSize;
            m_mapStride = SizeY;
            m_mapPatchsStride = m_mapStride * Constants.TerrainPatchSize;

            CompressionFactor = 100.0f;
            m_heightmap = new float[SizeX, SizeY];
            m_taints = new TerrainTaintsArray(m_taintSizeX * m_taintSizeY);

            // m_log.DebugFormat("{0} new by dimensions. sizeX={1}, sizeY={2}, sizeZ={3}", LogHeader, SizeX, SizeY, SizeZ);
            ClearLand(0f);
        }

        public TerrainData(float[] cmap, float pCompressionFactor, int pX, int pY, int pZ)
            : this(pX, pY, pZ)
        {
            CompressionFactor = pCompressionFactor;
            int ind = 0;
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    m_heightmap[xx, yy] = cmap[ind++];
            // m_log.DebugFormat("{0} new by compressed map. sizeX={1}, sizeY={2}, sizeZ={3}", LogHeader, SizeX, SizeY, SizeZ);
        }

        // Create a heighmap from a database blob
        public TerrainData(int pSizeX, int pSizeY, int pSizeZ, int pFormatCode, byte[] pBlob)
            : this(pSizeX, pSizeY, pSizeZ)
        {
            switch ((DBTerrainRevision)pFormatCode)
            {
                case DBTerrainRevision.Variable2DGzip:
                    FromCompressedTerrainSerializationV2DGZip(pBlob);
                    m_log.DebugFormat("{0} HeightmapTerrainData create from Variable2DGzip serialization. Size=<{1},{2}>", LogHeader, SizeX, SizeY);
                    break;

                case DBTerrainRevision.Variable2D:
                    FromCompressedTerrainSerializationV2D(pBlob);
                    m_log.DebugFormat("{0} HeightmapTerrainData create from Variable2D serialization. Size=<{1},{2}>", LogHeader, SizeX, SizeY);
                    break;
                case DBTerrainRevision.Compressed2D:
                    FromCompressedTerrainSerialization2D(pBlob);
                    m_log.DebugFormat("{0} HeightmapTerrainData create from Compressed2D serialization. Size=<{1},{2}>", LogHeader, SizeX, SizeY);
                    break;
                default:
                    FromLegacyTerrainSerialization(pBlob);
                    m_log.DebugFormat("{0} HeightmapTerrainData create from legacy serialization. Size=<{1},{2}>", LogHeader, SizeX, SizeY);
                    break;
            }
        }

        // Just create an array of doubles. Presumes the caller implicitly knows the size.
        public Array ToLegacyTerrainSerialization()
        {
            Array ret = null;

            using (MemoryStream str = new MemoryStream((int)Constants.RegionSize * (int)Constants.RegionSize * sizeof(double)))
            {
                using (BinaryWriter bw = new BinaryWriter(str))
                {
                    for (int xx = 0; xx < Constants.RegionSize; xx++)
                    {
                        for (int yy = 0; yy < Constants.RegionSize; yy++)
                        {
                            double height = this[xx, yy];
                            if (height == 0.0)
                                height = double.Epsilon;
                            bw.Write(height);
                        }
                    }
                }
                ret = str.ToArray();
            }
            return ret;
        }

        // Presumes the caller implicitly knows the size.
        public void FromLegacyTerrainSerialization(byte[] pBlob)
        {
            // In case database info doesn't match real terrain size, initialize the whole terrain.
            ClearLand();

            try
            {
                using (MemoryStream mstr = new MemoryStream(pBlob))
                {
                    using (BinaryReader br = new BinaryReader(mstr))
                    {
                        for (int xx = 0; xx < (int)Constants.RegionSize; xx++)
                        {
                            for (int yy = 0; yy < (int)Constants.RegionSize; yy++)
                            {
                                float val = (float)br.ReadDouble();

                                if (xx < SizeX && yy < SizeY)
                                    m_heightmap[xx, yy] = val;
                            }
                        }
                    }
                }
            }
            catch
            {
                ClearLand();
            }
            ClearTaint();
        }


        // stores as variable2D
        // int32 sizeX
        // int32 sizeY
        // float[,] array

        public Array ToCompressedTerrainSerializationV2D()
        {
            Array ret = null;
            try
            {
                using (MemoryStream str = new MemoryStream((2 * sizeof(Int32)) + (SizeX * SizeY * sizeof(float))))
                {
                    using (BinaryWriter bw = new BinaryWriter(str))
                    {
                        bw.Write((Int32)SizeX);
                        bw.Write((Int32)SizeY);
                        for (int yy = 0; yy < SizeY; yy++)
                            for (int xx = 0; xx < SizeX; xx++)
                            {
                                // reduce to 1mm resolution
                                float val = MathF.Round(m_heightmap[xx, yy],3,MidpointRounding.AwayFromZero);
                                bw.Write(val);
                            }
                    }
                    ret = str.ToArray();
                }
            }
            catch {}

            m_log.DebugFormat("{0} V2D {1} bytes", LogHeader, ret.Length);

            return ret;
        }

        // as above with Gzip compression
        public Array ToCompressedTerrainSerializationV2DGzip()
        {
            Array ret = null;
            try
            {
                using (MemoryStream inp = new MemoryStream((2 * sizeof(int)) + (SizeX * SizeY * sizeof(float))))
                {
                    using (BinaryWriter bw = new BinaryWriter(inp))
                    {
                        bw.Write(SizeX);
                        bw.Write(SizeY);
                        for (int yy = 0; yy < SizeY; yy++)
                            for (int xx = 0; xx < SizeX; xx++)
                            {
                                bw.Write(MathF.Round(m_heightmap[xx, yy], 3, MidpointRounding.AwayFromZero));
                            }

                        bw.Flush();
                        inp.Seek(0, SeekOrigin.Begin);

                        using MemoryStream outputStream = new MemoryStream();
                        using GZipStream compressionStream = new(outputStream, CompressionMode.Compress);
                        inp.CopyTo(compressionStream);
                        compressionStream.Flush();
                        ret = outputStream.ToArray();
                    }
                }
                m_log.Debug($"{LogHeader} V2DGzip {ret.Length} bytes");
            }
            catch (Exception ex)
            {
                m_log.Error($"{LogHeader} V2DGzip error: {ex.Message}");
            }
            return ret;
        }

        // Initialize heightmap from blob consisting of:
        //    int32, int32, int32, int32, int16[]
        //    where the first int32 is format code, next two int32s are the X and y of heightmap data and
        //    the forth int is the compression factor for the following int16s
        // This is just sets heightmap info. The actual size of the region was set on this instance's
        //    creation and any heights not initialized by theis blob are set to the default height.
        public void FromCompressedTerrainSerialization2D(byte[] pBlob)
        {
            Int32 hmFormatCode, hmSizeX, hmSizeY, hmCompressionFactor;

            using (MemoryStream mstr = new MemoryStream(pBlob))
            {
                using (BinaryReader br = new BinaryReader(mstr))
                {
                    hmFormatCode = br.ReadInt32();
                    hmSizeX = br.ReadInt32();
                    hmSizeY = br.ReadInt32();
                    hmCompressionFactor = br.ReadInt32();

                    CompressionFactor = hmCompressionFactor;

                    // In case database info doesn't match real terrain size, initialize the whole terrain.
                    bool needClear = false;
                    if (hmSizeX > SizeX)
                        hmSizeX = SizeX;
                    else if (hmSizeX < SizeX)
                        needClear = true;

                    if (hmSizeY > SizeY)
                        hmSizeY = SizeY;
                    else if (hmSizeY < SizeY)
                        needClear = true;

                    if (needClear)
                        ClearLand();

                    for (int yy = 0; yy < hmSizeY; yy++)
                    {
                        for (int xx = 0; xx < hmSizeX; xx++)
                        {
                            float val = FromCompressedHeight(br.ReadInt16());
                            m_heightmap[xx, yy] = val;
                        }
                    }
                }
                ClearTaint();

                m_log.DebugFormat("{0} Read (compressed2D) heightmap. Heightmap size=<{1},{2}>. Region size=<{3},{4}>. CompFact={5}",
                                LogHeader, hmSizeX, hmSizeY, SizeX, SizeY, hmCompressionFactor);
            }
        }

        // Initialize heightmap from blob consisting of:
        //    int32, int32, int32, float[]
        //    where the first int32 is format code, next two int32s are the X and y of heightmap data
        // This is just sets heightmap info. The actual size of the region was set on this instance's
        //    creation and any heights not initialized by theis blob are set to the default height.
        public void FromCompressedTerrainSerializationV2D(byte[] pBlob)
        {
            Int32 hmSizeX, hmSizeY;
            try
            {
                using (MemoryStream mstr = new MemoryStream(pBlob))
                {
                    using (BinaryReader br = new BinaryReader(mstr))
                    {
                        hmSizeX = br.ReadInt32();
                        hmSizeY = br.ReadInt32();

                        bool needClear = false;
                        if (hmSizeX > SizeX)
                            hmSizeX = SizeX;
                        else if (hmSizeX < SizeX)
                            needClear = true;

                        if (hmSizeY > SizeY)
                            hmSizeY = SizeY;
                        else if (hmSizeY < SizeY)
                            needClear = true;

                        if (needClear)
                            ClearLand();

                        for (int yy = 0; yy < hmSizeY; yy++)
                        {
                            for (int xx = 0; xx < hmSizeX; xx++)
                            {
                                float val = br.ReadSingle();
                                m_heightmap[xx, yy] = val;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ClearTaint();
                m_log.ErrorFormat("{0} 2D error: {1} - terrain may be damaged",
                                LogHeader, e.Message);
                return;
            }
            ClearTaint();

            m_log.DebugFormat("{0} V2D Heightmap size=<{1},{2}>. Region size=<{3},{4}>",
                            LogHeader, hmSizeX, hmSizeY, SizeX, SizeY);

        }

        // as above but Gzip compressed
        public void FromCompressedTerrainSerializationV2DGZip(byte[] pBlob)
        {
            m_log.InfoFormat("{0} VD2Gzip {1} bytes input",
                            LogHeader, pBlob.Length);
            int hmSizeX, hmSizeY;

            try
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    using (MemoryStream inputStream = new MemoryStream(pBlob))
                    {
                        using (GZipStream decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress))
                        {
                            decompressionStream.CopyTo(outputStream);
                        }
                    }

                    outputStream.Seek(0, SeekOrigin.Begin);

                    using (BinaryReader br = new BinaryReader(outputStream))
                    {
                        hmSizeX = br.ReadInt32();
                        hmSizeY = br.ReadInt32();

                        bool needClear = false;
                        if(hmSizeX > SizeX) 
                            hmSizeX = SizeX;
                        else if (hmSizeX < SizeX)
                            needClear = true;

                        if (hmSizeY > SizeY)
                            hmSizeY = SizeY;
                        else if (hmSizeY < SizeY)
                            needClear = true;

                        if (needClear)
                            ClearLand();

                        for (int yy = 0; yy < hmSizeY; yy++)
                        {
                            for (int xx = 0; xx < hmSizeX; xx++)
                            {
                                float val = br.ReadSingle();
                                m_heightmap[xx, yy] = val;
                            }
                        }
                    }
                }
            }
            catch( Exception e)
            {
                ClearTaint();
                m_log.ErrorFormat("{0} V2DGzip error: {1} - terrain may be damaged",
                                LogHeader, e.Message);
                return;
            }

            ClearTaint();
            m_log.DebugFormat("{0} V2DGzip. Heightmap size=<{1},{2}>. Region size=<{3},{4}>",
                            LogHeader, hmSizeX, hmSizeY, SizeX, SizeY);

        }
    }
}
