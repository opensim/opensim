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
 *     * Neither the name of the OpenSim Project nor the
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
using System.IO;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.World.Terrain.FileLoaders
{
    public class LLRAW : ITerrainLoader
    {
        public struct HeightmapLookupValue : IComparable<HeightmapLookupValue>
        {
            public int Index;
            public double Value;

            public HeightmapLookupValue(int index, double value)
            {
                Index = index;
                Value = value;
            }

            public int CompareTo(HeightmapLookupValue val)
            {
                return Value.CompareTo(val.Value);
            }
        }

        /// <summary>Lookup table to speed up terrain exports</summary>
        HeightmapLookupValue[] LookupHeightTable;

        public LLRAW()
        {
            LookupHeightTable = new HeightmapLookupValue[256 * 256];

            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < 256; j++)
                {
                    LookupHeightTable[i + (j * 256)] = new HeightmapLookupValue(i + (j * 256), ((double)i * ((double)j / 127.0d)));
                }
            }
            Array.Sort<HeightmapLookupValue>(LookupHeightTable);
        }

        #region ITerrainLoader Members

        public ITerrainChannel LoadFile(string filename)
        {
            TerrainChannel retval = new TerrainChannel();

            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);
            int y;
            for (y = 0; y < retval.Height; y++)
            {
                int x;
                for (x = 0; x < retval.Width; x++)
                {
                    retval[x, y] = bs.ReadByte() * (bs.ReadByte() / 127.0);
                    bs.ReadBytes(11); // Advance the stream to next bytes.
                }
            }

            bs.Close();
            s.Close();

            return retval;
        }

        public ITerrainChannel LoadFile(string filename, int x, int y, int fileWidth, int fileHeight, int w, int h)
        {
            throw new NotImplementedException();
        }

        public void SaveFile(string filename, ITerrainChannel map)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.CreateNew, FileAccess.Write);
            BinaryWriter binStream = new BinaryWriter(s);

            // Output the calculated raw
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    double t = map[x, y];
                    int index = 0;

                    // The lookup table is pre-sorted, so we either find an exact match or
                    // the next closest (smaller) match with a binary search
                    index = Array.BinarySearch<HeightmapLookupValue>(LookupHeightTable, new HeightmapLookupValue(0, t));
                    if (index < 0)
                        index = ~index - 1;

                    index = LookupHeightTable[index].Index;

                    byte red = (byte) (index & 0xFF);
                    byte green = (byte) ((index >> 8) & 0xFF);
                    const byte blue = 20;
                    const byte alpha1 = 0;
                    const byte alpha2 = 0;
                    const byte alpha3 = 0;
                    const byte alpha4 = 0;
                    const byte alpha5 = 255;
                    const byte alpha6 = 255;
                    const byte alpha7 = 255;
                    const byte alpha8 = 255;
                    byte alpha9 = red;
                    byte alpha10 = green;

                    binStream.Write(red);
                    binStream.Write(green);
                    binStream.Write(blue);
                    binStream.Write(alpha1);
                    binStream.Write(alpha2);
                    binStream.Write(alpha3);
                    binStream.Write(alpha4);
                    binStream.Write(alpha5);
                    binStream.Write(alpha6);
                    binStream.Write(alpha7);
                    binStream.Write(alpha8);
                    binStream.Write(alpha9);
                    binStream.Write(alpha10);
                }
            }

            binStream.Close();
            s.Close();
        }


        public string FileExtension
        {
            get { return ".raw"; }
        }

        #endregion

        public override string ToString()
        {
            return "LL/SL RAW";
        }
    }
}
