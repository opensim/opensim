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
using System.IO;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Terrain.FileLoaders
{
    public class RAW32 : ITerrainLoader
    {
        #region ITerrainLoader Members

        public string FileExtension
        {
            get { return ".r32"; }
        }

        public int SupportedHeight
        {
            get { return 4096; }
        }

        public ITerrainChannel LoadFile(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            ITerrainChannel retval = LoadStream(s);

            s.Close();

            return retval;
        }

        public ITerrainChannel LoadFile(string filename, int offsetX, int offsetY, int fileWidth, int fileHeight, int sectionWidth, int sectionHeight)
        {
            TerrainChannel retval = new TerrainChannel(sectionWidth, sectionHeight);

            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);

            int currFileYOffset = 0;

            // if our region isn't on the first Y section of the areas to be landscaped, then
            // advance to our section of the file
            while (currFileYOffset < offsetY)
            {
                // read a whole strip of regions
                int heightsToRead = sectionHeight * (fileWidth * sectionWidth);
                bs.ReadBytes(heightsToRead * 4); // because the floats are 4 bytes in the file
                currFileYOffset++;
            }

            // got to the Y start offset within the file of our region
            // so read the file bits associated with our region
            int y;
            // for each Y within our Y offset
            for (y = 0; y < sectionHeight; y++)
            {
                int currFileXOffset = 0;

                // if our region isn't the first X section of the areas to be landscaped, then
                // advance the stream to the X start pos of our section in the file
                // i.e. eat X upto where we start
                while (currFileXOffset < offsetX)
                {
                    bs.ReadBytes(sectionWidth * 4); // 4 bytes = single
                    currFileXOffset++;
                }

                // got to our X offset, so write our regions X line
                int x;
                for (x = 0; x < sectionWidth; x++)
                {
                    // Read a strip and continue
                    retval[x, y] = bs.ReadSingle();
                }
                // record that we wrote it
                currFileXOffset++;

                // if our region isn't the last X section of the areas to be landscaped, then
                // advance the stream to the end of this Y column
                while (currFileXOffset < fileWidth)
                {
                    // eat the next regions x line
                    bs.ReadBytes(sectionWidth * 4); // 4 bytes = single
                    currFileXOffset++;
                }
            }

            bs.Close();
            s.Close();

            return retval;
        }

        public ITerrainChannel LoadStream(Stream s)
        {
            // The raw format doesn't contain any dimension information.
            // Guess the square dimensions by using the length of the raw file.
            double dimension = Math.Sqrt((double)(s.Length / 4));
            // Regions are always multiples of 256.
            int trimmedDimension = (int)dimension - ((int)dimension % (int)Constants.RegionSize);
            if (trimmedDimension < Constants.RegionSize)
                trimmedDimension = (int)Constants.RegionSize;

            TerrainChannel retval = new TerrainChannel(trimmedDimension, trimmedDimension);

            BinaryReader bs = new BinaryReader(s);
            int y;
            for (y = 0; y < retval.Height; y++)
            {
                int x;
                for (x = 0; x < retval.Width; x++)
                {
                    retval[x, y] = bs.ReadSingle();
                }
            }

            bs.Close();

            return retval;
        }

        public void SaveFile(string filename, ITerrainChannel map)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Create, FileAccess.Write);
            SaveStream(s, map);

            s.Close();
        }

        public void SaveStream(Stream s, ITerrainChannel map)
        {
            BinaryWriter bs = new BinaryWriter(s);

            int y;
            for (y = 0; y < map.Height; y++)
            {
                int x;
                for (x = 0; x < map.Width; x++)
                {
                    bs.Write((float) map[x, y]);
                }
            }

            bs.Close();
        }

        public virtual void SaveFile(ITerrainChannel m_channel, string filename,
                                     int offsetX, int offsetY,
                                     int fileWidth, int fileHeight,
                                     int regionSizeX, int regionSizeY)
        {
            throw new System.Exception("Not Implemented");
        }
        #endregion

        public override string ToString()
        {
            return "RAW32";
        }

        //Returns true if this extension is supported for terrain save-tile
        public bool SupportsTileSave()
        {
            return false;
        }

        public bool SupportsExtendedTileSave()
        {
            return false;
        }

        public void SaveFile(ITerrainChannel map, string filename, int fileWidth, int fileHeight, int startX, int startY, int stopX, int stopY, int offsetX, int offsetY)
        {
            throw new NotImplementedException();
        }
    }
}
