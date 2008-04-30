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
        #region ITerrainLoader Members

        public ITerrainChannel LoadFile(string filename)
        {
            TerrainChannel retval = new TerrainChannel();

            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);
            int x, y;
            for (y = 0; y < retval.Height; y++)
            {
                for (x = 0; x < retval.Width; x++)
                {
                    retval[x, y] = (double) bs.ReadByte() * ((double) bs.ReadByte() / 127.0);
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

            // Generate a smegging big lookup table to speed the operation up (it needs it)
            double[] lookupHeightTable = new double[65536];
            int i, j, x, y;
            for (i = 0; i < 256; i++)
            {
                for (j = 0; j < 256; j++)
                {
                    lookupHeightTable[i + (j * 256)] = ((double) i * ((double) j / 127.0));
                }
            }

            // Output the calculated raw
            for (y = 0; y < map.Height; y++)
            {
                for (x = 0; x < map.Width; x++)
                {
                    double t = map[x, y];
                    double min = double.MaxValue;
                    int index = 0;

                    for (i = 0; i < 65536; i++)
                    {
                        if (Math.Abs(t - lookupHeightTable[i]) < min)
                        {
                            min = Math.Abs(t - lookupHeightTable[i]);
                            index = i;
                        }
                    }

                    byte red = (byte) (index & 0xFF);
                    byte green = (byte) ((index >> 8) & 0xFF);
                    byte blue = 20;
                    byte alpha1 = 0; // Land Parcels
                    byte alpha2 = 0; // For Sale Land
                    byte alpha3 = 0; // Public Edit Object
                    byte alpha4 = 0; // Public Edit Land
                    byte alpha5 = 255; // Safe Land
                    byte alpha6 = 255; // Flying Allowed
                    byte alpha7 = 255; // Create Landmark
                    byte alpha8 = 255; // Outside Scripts
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