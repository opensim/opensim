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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System.Drawing;
using System.Drawing.Imaging;

namespace libTerrain
{
    internal class Raster
    {
        private int w;
        private int h;
        private Bitmap bmp;

        /// <summary>
        /// Creates a new Raster channel for use with bitmap or GDI functions
        /// </summary>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        public Raster(int width, int height)
        {
            w = width;
            h = height;
            bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        }

        /// <summary>
        /// Converts a raster image to a channel by averaging the RGB values to a single 0..1 heightmap
        /// </summary>
        /// <returns>A libTerrain Channel</returns>
        public Channel ToChannel()
        {
            Channel chan = new Channel(bmp.Width, bmp.Height);

            int x, y;
            for (x = 0; x < bmp.Width; x++)
            {
                for (y = 0; y < bmp.Height; y++)
                {
                    Color val = bmp.GetPixel(x, y);
                    chan.map[x, y] = (((double) val.R + (double) val.G + (double) val.B)/3.0)/255.0;
                }
            }

            return chan;
        }

        /// <summary>
        /// Draws a piece of text into the specified raster
        /// </summary>
        /// <param name="txt">The text string to print</param>
        /// <param name="font">The font to use to draw the specified image</param>
        /// <param name="size">Font size (points) to use</param>
        public void DrawText(string txt, string font, double size)
        {
            Rectangle area = new Rectangle(0, 0, 256, 256);
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            Graphics gd = Graphics.FromImage(bmp);
            gd.DrawString(txt, new Font(font, (float) size), new SolidBrush(Color.White), area, sf);
        }
    }
}