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
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using OpenMetaverse;

using log4net;

namespace OpenSim.Framework
{
    public abstract class TerrainData
    {
        // Terrain always is a square
        public int SizeX { get; protected set; }
        public int SizeY { get; protected set; }
        public int SizeZ { get; protected set; }

        // A height used when the user doesn't specify anything
        public const float DefaultTerrainHeight = 21f;

        public abstract float this[int x, int y] { get; set; }
        // Someday terrain will have caves
        public abstract float this[int x, int y, int z] { get; set; }

        public bool IsTainted { get; protected set; }
        public abstract bool IsTaintedAt(int xx, int yy);
        public abstract void ClearTaint();

        public abstract void ClearLand();
        public abstract void ClearLand(float height);

        // Return a representation of this terrain for storing as a blob in the database.
        // Returns 'true' to say blob was stored in the 'out' locations.
        public abstract bool GetDatabaseBlob(out int DBFormatRevisionCode, out Array blob);

        // Given a revision code and a blob from the database, create and return the right type of TerrainData.
        // The sizes passed are the expected size of the region. The database info will be used to
        //     initialize the heightmap of that sized region with as much data is in the blob.
        // Return created TerrainData or 'null' if unsuccessful.
        public static TerrainData CreateFromDatabaseBlobFactory(int pSizeX, int pSizeY, int pSizeZ, int pFormatCode, byte[] pBlob)
        {
            // For the moment, there is only one implementation class
            return new HeightmapTerrainData(pSizeX, pSizeY, pSizeZ, pFormatCode, pBlob);
        }

        // return a special compressed representation of the heightmap in shorts
        public abstract short[] GetCompressedMap();
        public abstract float CompressionFactor { get; }

        public abstract double[,] GetDoubles();
        public abstract TerrainData Clone();
    }

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
        // Terrain is 'int32, int32, int32, int16[]' where the ints are X and Y dimensions
        //   and third int is the 'compression factor'. The heights are compressed as
        //   "short compressedHeight = (short)(height * compressionFactor);"
        // The dimensions are presumed to be multiples of 16 and, more likely, multiples of 256.
        Compressed2D = 27,
        // A revision that is not listed above or any revision greater than this value is 'Legacy256'.
        RevisionHigh = 1234
    }

    // Version of terrain that is a heightmap.
    // This should really be 'LLOptimizedHeightmapTerrainData' as it includes knowledge
    //    of 'patches' which are 16x16 terrain areas which can be sent separately to the viewer.
    // The heighmap is kept as an array of short integers. The integer values are converted to
    //    and from floats by TerrainCompressionFactor. Shorts are used to limit storage used.
    public class HeightmapTerrainData : TerrainData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[TERRAIN DATA]";

        // TerrainData.this[x, y]
        public override float this[int x, int y]
        {
            get { return FromCompressedHeight(m_heightmap[x, y]); }
            set {
                short newVal = ToCompressedHeight(value);
                if (m_heightmap[x, y] != newVal)
                {
                    m_heightmap[x, y] = newVal;
                    m_taint[x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize] = true;
                    // m_log.DebugFormat("{0} set[{1},{2}] to {3} ({4})", LogHeader, x, y, value, newVal);
                }
            }
        }

        // TerrainData.this[x, y, z]
        public override float this[int x, int y, int z]
        {
            get { return this[x, y]; }
            set { this[x, y] = value; }
        }

        // TerrainData.ClearTaint
        public override void ClearTaint()
        {
            IsTainted = false;
            for (int ii = 0; ii < m_taint.GetLength(0); ii++)
                for (int jj = 0; jj < m_taint.GetLength(1); jj++)
                    m_taint[ii, jj] = false;
        }

        // TerrainData.ClearLand
        public override void ClearLand()
        {
            ClearLand(DefaultTerrainHeight);
        }
        // TerrainData.ClearLand(float)
        public override void ClearLand(float pHeight)
        {
            short flatHeight = ToCompressedHeight(pHeight);
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    m_heightmap[xx, yy] = flatHeight;
        }

        public override bool IsTaintedAt(int xx, int yy)
        {
            return m_taint[xx / Constants.TerrainPatchSize, yy / Constants.TerrainPatchSize];
        }

        // TerrainData.GetDatabaseBlob
        // The user wants something to store in the database.
        public override bool GetDatabaseBlob(out int DBRevisionCode, out Array blob)
        {
            DBRevisionCode = (int)DBTerrainRevision.Legacy256;
            blob = ToLegacyTerrainSerialization();
            return false;
        }

        // TerrainData.CompressionFactor
        private float m_compressionFactor = 100.0f;
        public override float CompressionFactor { get { return m_compressionFactor; } }

        // TerrainData.GetCompressedMap
        public override short[] GetCompressedMap()
        {
            short[] newMap = new short[SizeX * SizeY];

            int ind = 0;
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    newMap[ind++] = m_heightmap[xx, yy];

            return newMap;

        }
        // TerrainData.Clone
        public override TerrainData Clone()
        {
            HeightmapTerrainData ret = new HeightmapTerrainData(SizeX, SizeY, SizeZ);
            ret.m_heightmap = (short[,])this.m_heightmap.Clone();
            return ret;
        }

        // TerrainData.GetDoubles
        public override double[,] GetDoubles()
        {
            double[,] ret = new double[SizeX, SizeY];
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    ret[xx, yy] = FromCompressedHeight(m_heightmap[xx, yy]);

            return ret;
        }


        // =============================================================

        private short[,] m_heightmap;
        // Remember subregions of the heightmap that has changed.
        private bool[,] m_taint;

        // To save space (especially for large regions), keep the height as a short integer
        //    that is coded as the float height times the compression factor (usually '100'
        //    to make for two decimal points).
        public short ToCompressedHeight(double pHeight)
        {
            return (short)(pHeight * CompressionFactor);
        }

        public float FromCompressedHeight(short pHeight)
        {
            return ((float)pHeight) / CompressionFactor;
        }

        // To keep with the legacy theme, create an instance of this class based on the
        //     way terrain used to be passed around.
        public HeightmapTerrainData(double[,] pTerrain)
        {
            SizeX = pTerrain.GetLength(0);
            SizeY = pTerrain.GetLength(1);
            SizeZ = (int)Constants.RegionHeight;
            m_compressionFactor = 100.0f;

            m_heightmap = new short[SizeX, SizeY];
            for (int ii = 0; ii < SizeX; ii++)
            {
                for (int jj = 0; jj < SizeY; jj++)
                {
                    m_heightmap[ii, jj] = ToCompressedHeight(pTerrain[ii, jj]);

                }
            }

            m_taint = new bool[SizeX / Constants.TerrainPatchSize, SizeY / Constants.TerrainPatchSize];
            ClearTaint();
        }

        // Create underlying structures but don't initialize the heightmap assuming the caller will immediately do that
        public HeightmapTerrainData(int pX, int pY, int pZ)
        {
            SizeX = pX;
            SizeY = pY;
            SizeZ = pZ;
            m_compressionFactor = 100.0f;
            m_heightmap = new short[SizeX, SizeY];
            m_taint = new bool[SizeX / Constants.TerrainPatchSize, SizeY / Constants.TerrainPatchSize];
            ClearTaint();
        }

        public HeightmapTerrainData(short[] cmap, float pCompressionFactor, int pX, int pY, int pZ) : this(pX, pY, pZ)
        {
            m_compressionFactor = pCompressionFactor;
            int ind = 0;
            for (int xx = 0; xx < SizeX; xx++)
                for (int yy = 0; yy < SizeY; yy++)
                    m_heightmap[xx, yy] = cmap[ind++];
        }

        // Create a heighmap from a database blob
        public HeightmapTerrainData(int pSizeX, int pSizeY, int pSizeZ, int pFormatCode, byte[] pBlob) : this(pSizeX, pSizeY, pSizeZ)
        {
            switch ((DBTerrainRevision)pFormatCode)
            {
                case DBTerrainRevision.Compressed2D:
                    FromCompressedTerrainSerialization(pBlob);
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

        // Just create an array of doubles. Presumes the caller implicitly knows the size.
        public void FromLegacyTerrainSerialization(byte[] pBlob)
        {
            // In case database info doesn't match real terrain size, initialize the whole terrain.
            ClearLand();

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
                                m_heightmap[xx, yy] = ToCompressedHeight(val);
                        }
                    }
                }
                ClearTaint();
            }
        }
        
        // See the reader below.
        public Array ToCompressedTerrainSerialization()
        {
            Array ret = null;
            using (MemoryStream str = new MemoryStream((3 * sizeof(Int32)) + (SizeX * SizeY * sizeof(Int16))))
            {
                using (BinaryWriter bw = new BinaryWriter(str))
                {
                    bw.Write((Int32)DBTerrainRevision.Compressed2D);
                    bw.Write((Int32)SizeX);
                    bw.Write((Int32)SizeY);
                    bw.Write((Int32)CompressionFactor);
                    for (int yy = 0; yy < SizeY; yy++)
                        for (int xx = 0; xx < SizeX; xx++)
                        {
                            bw.Write((Int16)m_heightmap[xx, yy]);
                        }
                }
                ret = str.ToArray();
            }
            return ret;
        }

        // Initialize heightmap from blob consisting of:
        //    int32, int32, int32, int32, int16[]
        //    where the first int32 is format code, next two int32s are the X and y of heightmap data and
        //    the forth int is the compression factor for the following int16s
        // This is just sets heightmap info. The actual size of the region was set on this instance's
        //    creation and any heights not initialized by theis blob are set to the default height.
        public void FromCompressedTerrainSerialization(byte[] pBlob)
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

                    m_compressionFactor = hmCompressionFactor;

                    // In case database info doesn't match real terrain size, initialize the whole terrain.
                    ClearLand();

                    for (int yy = 0; yy < hmSizeY; yy++)
                    {
                        for (int xx = 0; xx < hmSizeX; xx++)
                        {
                            Int16 val = br.ReadInt16();
                            if (xx < SizeX && yy < SizeY)
                                m_heightmap[xx, yy] = ToCompressedHeight(val);
                        }
                    }
                }
                ClearTaint();

                m_log.InfoFormat("{0} Read compressed 2d heightmap. Heightmap size=<{1},{2}>. Region size={<{3},{4}>. CompFact={5}", LogHeader,
                                                        hmSizeX, hmSizeY, SizeX, SizeY, hmCompressionFactor);
            }
        }
    }
}
