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
using System.Text;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;

namespace OpenSim.Region.CoreModules.World.Terrain.FileLoaders
{
    /// <summary>
    /// Terragen File Format Loader
    /// Built from specification at
    /// http://www.planetside.co.uk/terragen/dev/tgterrain.html
    /// </summary>
    internal class Terragen : ITerrainLoader
    {
        #region ITerrainLoader Members

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

            bool eof = false;

            int fileXPoints = 0;
            int fileYPoints = 0;

            // Terragen file
            while (eof == false)
            {
                string tmp = Encoding.ASCII.GetString(bs.ReadBytes(4));
                switch (tmp)
                {
                    case "SIZE":
                        fileXPoints = bs.ReadInt16() + 1;
                        fileYPoints = fileXPoints;
                        bs.ReadInt16();
                        break;
                    case "XPTS":
                        fileXPoints = bs.ReadInt16();
                        bs.ReadInt16();
                        break;
                    case "YPTS":
                        fileYPoints = bs.ReadInt16();
                        bs.ReadInt16();
                        break;
                    case "ALTW":
                        eof = true;
                        Int16 heightScale = bs.ReadInt16();
                        Int16 baseHeight = bs.ReadInt16();

                        int currFileYOffset = 0;

                        // if our region isn't on the first X section of the areas to be landscaped, then
                        // advance to our section of the file
                        while (currFileYOffset < offsetY)
                        {
                            // read a whole strip of regions
                            int heightsToRead = sectionHeight * fileXPoints;
                            bs.ReadBytes(heightsToRead * 2); // because the shorts are 2 bytes in the file
                            currFileYOffset++;
                        }

                        for (int y = 0; y < sectionHeight; y++)
                        {
                            int currFileXOffset = 0;

                            // if our region isn't the first X section of the areas to be landscaped, then
                            // advance the stream to the X start pos of our section in the file
                            // i.e. eat X upto where we start
                            while (currFileXOffset < offsetX)
                            {
                                bs.ReadBytes(sectionWidth * 2); // 2 bytes = short
                                currFileXOffset++;
                            }

                            // got to our X offset, so write our regions X line
                            for (int x = 0; x < sectionWidth; x++)
                            {
                                // Read a strip and continue
                                retval[x, y] = baseHeight + bs.ReadInt16() * (float)heightScale / 65536.0f;
                            }
                            // record that we wrote it
                            currFileXOffset++;

                            // if our region isn't the last X section of the areas to be landscaped, then
                            // advance the stream to the end of this Y column
                            while (currFileXOffset < fileWidth)
                            {
                                // eat the next regions x line
                                bs.ReadBytes(sectionWidth * 2); // 2 bytes = short
                                currFileXOffset++;
                            }
                            //eat the last additional point
                            bs.ReadInt16();
                        }

                        break;
                    default:
                        bs.ReadInt32();
                        break;
                }
            }

            bs.Close();
            s.Close();

            return retval;
        }

        public ITerrainChannel LoadStream(Stream s)
        {
            // Set to default size
            int w = (int)Constants.RegionSize;
            int h = (int)Constants.RegionSize;

            // create a dummy channel (in case data is bad)
            TerrainChannel retval = new TerrainChannel(w, h);

            BinaryReader bs = new BinaryReader(s);

            bool eof = false;
            if (Encoding.ASCII.GetString(bs.ReadBytes(16)) == "TERRAGENTERRAIN ")
            {

                // Terragen file
                while (eof == false)
                {
                    string tmp = Encoding.ASCII.GetString(bs.ReadBytes(4));
                    switch (tmp)
                    {
                        case "SIZE":
                            w = bs.ReadInt16() + 1;
                            h = w;
                            bs.ReadInt16();
                            break;
                        case "XPTS":
                            w = bs.ReadInt16();
                            bs.ReadInt16();
                            break;
                        case "YPTS":
                            h = bs.ReadInt16();
                            bs.ReadInt16();
                            break;
                        case "ALTW":
                            eof = true;
                            // create new channel of proper size (now that we know it)
                            retval = new TerrainChannel(w, h);
                            float heightScale = bs.ReadInt16() / 65536.0f;
                            float baseHeight = bs.ReadInt16();
                            for (int y = 0; y < h; y++)
                            {
                                for (int x = 0; x < w; x++)
                                {
                                    retval[x, y] = baseHeight + bs.ReadInt16() * heightScale;
                                }
                            }
                            break;
                        default:
                            bs.ReadInt32();
                            break;
                    }
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

        public void SaveStream(Stream stream, ITerrainChannel map)
        {
            BinaryWriter bs = new BinaryWriter(stream);

            //find the max and min heights on the map
            float heightMax = map[0,0];
            float heightMin = map[0,0];

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float current = map[x,y];
                    if (heightMax < current)
                        heightMax = current;
                    if (heightMin > current)
                        heightMin = current;
                }
            }

            float baseHeight = (float)Math.Floor(0.5f * (heightMax + heightMin));

            float horizontalScale = (float) Math.Ceiling((heightMax - heightMin));

            // if we are completely flat add 1cm range to avoid NaN divisions
            if (horizontalScale < 0.01f)
                horizontalScale = 0.01f;

            Encoding enc = Encoding.ASCII;

            bs.Write(enc.GetBytes("TERRAGENTERRAIN "));

            bs.Write(enc.GetBytes("SIZE"));
            bs.Write(Convert.ToInt16(map.Width));
            bs.Write(Convert.ToInt16(0));  // necessary padding

            //The XPTS and YPTS chunks are not needed for square regions
            //but L3DT won't load the terrain file properly without them.
            bs.Write(enc.GetBytes("XPTS"));
            bs.Write(Convert.ToInt16(map.Width));
            bs.Write(Convert.ToInt16(0));  // necessary padding

            bs.Write(enc.GetBytes("YPTS"));
            bs.Write(Convert.ToInt16(map.Height));
            bs.Write(Convert.ToInt16(0));  // necessary padding

            bs.Write(enc.GetBytes("SCAL"));
            bs.Write(ToLittleEndian(1f)); //we're going to say that 1 terrain unit is 1 metre
            bs.Write(ToLittleEndian(1f));
            bs.Write(ToLittleEndian(1f));

            // as we are square and not projected on a sphere then the other
            // header blocks are not required

            // now write the elevation data
            bs.Write(enc.GetBytes("ALTW"));
            bs.Write(Convert.ToInt16(horizontalScale)); // range between max and min
            bs.Write(Convert.ToInt16(baseHeight)); // base height or mid point

            double factor = 65536.0 / horizontalScale; // avoid computing this on each iteration

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float elevation = (float)((map[x,y] - baseHeight) * factor); // see LoadStream for inverse

                    // clamp rounding issues
                    if (elevation > Int16.MaxValue)
                        elevation = Int16.MaxValue;
                    else if (elevation < Int16.MinValue)
                        elevation = Int16.MinValue;

                    bs.Write(Convert.ToInt16(elevation));
                }
            }

            //This is necessary for older versions of Terragen.
            bs.Write(enc.GetBytes("EOF "));

            bs.Close();
        }

        public string FileExtension
        {
            get { return ".ter"; }
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
            return "Terragen";
        }

        //Returns true if this extension is supported for terrain save-tile
        public bool SupportsTileSave()
        {
            return false;
        }

        /// <summary>
        /// terragen SCAL floats need to be written intel ordered regardless of
        /// big or little endian system
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        private byte[] ToLittleEndian( float number)
        {
            byte[] retVal = BitConverter.GetBytes(number);
            if (BitConverter.IsLittleEndian == false)
            {
                byte[] tmp = new byte[4];
                for (int i = 3; i >= 0; i--)
                {
                    tmp[i] = retVal[3 - i];
                }
                retVal = tmp;

            }
            return retVal ;
        }

    }
}
