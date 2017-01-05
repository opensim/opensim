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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.World.Terrain.FileLoaders
{
    public class JPEG : ITerrainLoader
    {
        #region ITerrainLoader Members

        public string FileExtension
        {
            get { return ".jpg"; }
        }

        public ITerrainChannel LoadFile(string filename)
        {
            throw new NotImplementedException();
        }

        public ITerrainChannel LoadFile(string filename, int x, int y, int fileWidth, int fileHeight, int w, int h)
        {
            throw new NotImplementedException();
        }

        public ITerrainChannel LoadStream(Stream stream)
        {
            throw new NotImplementedException();
        }

        public void SaveFile(string filename, ITerrainChannel map)
        {
            using(Bitmap colours = CreateBitmapFromMap(map))
                colours.Save(filename,ImageFormat.Jpeg);
        }

        /// <summary>
        /// Exports a stream using a System.Drawing exporter.
        /// </summary>
        /// <param name="stream">The target stream</param>
        /// <param name="map">The terrain channel being saved</param>
        public void SaveStream(Stream stream, ITerrainChannel map)
        {
            using(Bitmap colours = CreateBitmapFromMap(map))
                colours.Save(stream,ImageFormat.Jpeg);
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
            return "JPEG";
        }

        //Returns true if this extension is supported for terrain save-tile
        public bool SupportsTileSave()
        {
            return false;
        }

        private static Bitmap CreateBitmapFromMap(ITerrainChannel map)
        {
            int pallete;
            Bitmap bmp;
            Color[] colours;

            using (Bitmap gradientmapLd = new Bitmap("defaultstripe.png"))
            {
                pallete = gradientmapLd.Height;

                bmp = new Bitmap(map.Width, map.Height);
                colours = new Color[pallete];

                for (int i = 0; i < pallete; i++)
                {
                    colours[i] = gradientmapLd.GetPixel(0, i);
                }
            }

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    // 512 is the largest possible height before colours clamp
                    int colorindex = (int) (Math.Max(Math.Min(1.0, map[x, y] / 512.0), 0.0) * (pallete - 1));
                    bmp.SetPixel(x, map.Height - y - 1, colours[colorindex]);
                }
            }
            return bmp;
        }
    }
}
