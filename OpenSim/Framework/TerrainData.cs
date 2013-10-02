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

using OpenMetaverse;

namespace OpenSim.Framework
{
    public abstract class TerrainData
    {
        // Terrain always is a square
        public int SizeX { get; protected set; }
        public int SizeY { get; protected set; }
        public int SizeZ { get; protected set; }

        public abstract float this[int x, int y] { get; set; }
        // Someday terrain will have caves
        public abstract float this[int x, int y, int z] { get; set; }

        // Return a representation of this terrain for storing as a blob in the database.
        // Returns 'true' to say blob was stored in the 'out' locations.
        public abstract bool GetDatabaseBlob(out int DBFormatRevisionCode, out Array blob);
    }

    // The terrain is stored as a blob in the database with a 'revision' field.
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
        // Terrain is 'int32, int32, float[,]' where the shorts are X and Y dimensions
        // The dimensions are presumed to be multiples of 16 and, more likely, multiples of 256.
        Variable2D = 22,
        // A revision that is not listed above or any revision greater than this value is 'Legacy256'.
        RevisionHigh = 1234
    }

    // Version of terrain that is a heightmap.
    // This should really be 'LLOptimizedHeightmapTerrainData' as it includes knowledge
    //    of 'patches' which are 16x16 terrain areas which can be sent separately to the viewer.
    public class HeightmapTerrainData : TerrainData
    {
        // TerrainData.this[x, y]
        public override float this[int x, int y]
        {
            get { return m_heightmap[x * SizeX + y]; }
            set { m_heightmap[x * SizeX + y] = value; }
        }

        // TerrainData.this[x, y, z]
        public override float this[int x, int y, int z]
        {
            get { return this[x, y]; }
            set { this[x, y] = value; }
        }

        // TerrainData.GetDatabaseBlob
        // The user wants something to store in the database.
        public override bool GetDatabaseBlob(out int DBRevisionCode, out Array blob)
        {
            DBRevisionCode = (int)DBTerrainRevision.Legacy256;
            blob = LegacyTerrainSerialization();
            return false;
        }
        private float[] m_heightmap;

        // To keep with the legacy theme, this can be created with the way terrain
        //     used to passed around as.
        public HeightmapTerrainData(double[,] pTerrain)
        {
            SizeX = pTerrain.GetLength(0);
            SizeY = pTerrain.GetLength(1);
            SizeZ = (int)Constants.RegionHeight;

            int idx = 0;
            m_heightmap = new float[SizeX * SizeY];
            for (int ii = 0; ii < SizeX; ii++)
            {
                for (int jj = 0; jj < SizeY; jj++)
                {
                    m_heightmap[idx++] = (float)pTerrain[ii, jj];

                }
            }
        }

        public HeightmapTerrainData(float[] pHeightmap, int pX, int pY, int pZ)
        {
            m_heightmap = pHeightmap;
            SizeX = pX;
            SizeY = pY;
            SizeZ = pZ;
        }

        // Just create an array of doubles. Presumes the caller implicitly knows the size.
        public Array LegacyTerrainSerialization()
        {
            Array ret = null;
            using (MemoryStream str = new MemoryStream(SizeX * SizeY * sizeof(double)))
            {
                using (BinaryWriter bw = new BinaryWriter(str))
                {
                    // TODO: COMPATIBILITY - Add byte-order conversions
                    for (int ii = 0; ii < m_heightmap.Length; ii++)
                    {
                        double height = (double)m_heightmap[ii];
                        if (height == 0.0)
                            height = double.Epsilon;

                        bw.Write(height);
                    }
                }
                ret = str.ToArray();
            }
            return ret;
        }
    }
}
