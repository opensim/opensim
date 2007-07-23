/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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

using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace libTerrain
{
    partial class Channel
    {
        public Channel LoadImage(string filename)
        {
            Bitmap bit = new Bitmap(filename);
            Channel chan = new Channel(bit.Width, bit.Height);

            int x, y;
            for (x = 0; x < bit.Width; x++)
            {
                for (y = 0; y < bit.Height; y++)
                {
                    Color val = bit.GetPixel(x, y);
                    chan.map[x, y] = (((double)val.R + (double)val.G + (double)val.B) / 3.0) / 255.0;
                }
            }

            return chan;
        }

        public void SaveImage(string filename)
        {
            Bitmap bit = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    int val = Math.Min(255, (int)(map[x,y] * 255));
                    Color col = Color.FromArgb(val,val,val);
                    bit.SetPixel(x, y, col);
                }
            }
            bit.Save(filename);
        }
    }
}
