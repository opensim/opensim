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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.World.Warp3DMap
{
    public static class TerrainSplat
    {
        #region Constants

        private static readonly UUID DIRT_DETAIL = new UUID("0bc58228-74a0-7e83-89bc-5c23464bcec5");
        private static readonly UUID GRASS_DETAIL = new UUID("63338ede-0037-c4fd-855b-015d77112fc8");
        private static readonly UUID MOUNTAIN_DETAIL = new UUID("303cd381-8560-7579-23f1-f0a880799740");
        private static readonly UUID ROCK_DETAIL = new UUID("53a2f406-4895-1d13-d541-d2e3b86bc19c");

        private static readonly UUID[] DEFAULT_TERRAIN_DETAIL = new UUID[]
        {
            DIRT_DETAIL,
            GRASS_DETAIL,
            MOUNTAIN_DETAIL,
            ROCK_DETAIL
        };

        private static readonly Color[] DEFAULT_TERRAIN_COLOR = new Color[]
        {
            Color.FromArgb(255, 164, 136, 117),
            Color.FromArgb(255, 65, 87, 47),
            Color.FromArgb(255, 157, 145, 131),
            Color.FromArgb(255, 125, 128, 130)
        };

        private static readonly UUID TERRAIN_CACHE_MAGIC = new UUID("2c0c7ef2-56be-4eb8-aacb-76712c535b4b");

        #endregion Constants

        private static readonly ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);
        private static string LogHeader = "[WARP3D TERRAIN SPLAT]";

        /// <summary>
        /// Builds a composited terrain texture given the region texture
        /// and heightmap settings
        /// </summary>
        /// <param name="terrain">Terrain heightmap</param>
        /// <param name="regionInfo">Region information including terrain texture parameters</param>
        /// <returns>A 256x256 square RGB texture ready for rendering</returns>
        /// <remarks>Based on the algorithm described at http://opensimulator.org/wiki/Terrain_Splatting
        /// Note we create a 256x256 dimension texture even if the actual terrain is larger.
        /// </remarks>

        public static Bitmap Splat(ITerrainChannel terrain, UUID[] textureIDs,
                float[] startHeights, float[] heightRanges,
                uint regionPositionX, uint regionPositionY,
                IAssetService assetService, IJ2KDecoder decoder,
                bool textureTerrain, bool averagetextureTerrain,
                int twidth, int theight)
        {
            Debug.Assert(textureIDs.Length == 4);
            Debug.Assert(startHeights.Length == 4);
            Debug.Assert(heightRanges.Length == 4);

            Bitmap[] detailTexture = new Bitmap[4];

            byte[] mapColorsRed = new byte[4];
            byte[] mapColorsGreen = new byte[4];
            byte[] mapColorsBlue = new byte[4];

            bool usecolors = false;

            if (textureTerrain)
            {
                // Swap empty terrain textureIDs with default IDs
                for(int i = 0; i < textureIDs.Length; i++)
                {
                    if(textureIDs[i] == UUID.Zero)
                        textureIDs[i] = DEFAULT_TERRAIN_DETAIL[i];
                }

                #region Texture Fetching

                if(assetService != null)
                {
                    for(int i = 0; i < 4; i++)
                    {
                        AssetBase asset = null;

                        // asset cache indexes are strings
                        string cacheName ="MAP-Patch" + textureIDs[i].ToString();

                        // Try to fetch a cached copy of the decoded/resized version of this texture
                        asset = assetService.GetCached(cacheName);
                        if(asset != null)
                        {
                            try
                            {
                                using(System.IO.MemoryStream stream = new System.IO.MemoryStream(asset.Data))
                                    detailTexture[i] = (Bitmap)Image.FromStream(stream);

                                if(detailTexture[i].PixelFormat != PixelFormat.Format24bppRgb ||
                                     detailTexture[i].Width != 16 || detailTexture[i].Height != 16)
                                {
                                    detailTexture[i].Dispose();
                                    detailTexture[i] = null;
                                }
                            }
                            catch(Exception ex)
                            {
                                m_log.Warn("Failed to decode cached terrain patch texture" + textureIDs[i] + "): " + ex.Message);
                            }
                        }

                        if(detailTexture[i] == null)
                        {
                            // Try to fetch the original JPEG2000 texture, resize if needed, and cache as PNG
                            asset = assetService.Get(textureIDs[i].ToString());
                            if(asset != null)
                            {
                                try
                                {
                                    detailTexture[i] = (Bitmap)decoder.DecodeToImage(asset.Data);
                                }
                                catch(Exception ex)
                                {
                                    m_log.Warn("Failed to decode terrain texture " + asset.ID + ": " + ex.Message);
                                }
                            }

                            if(detailTexture[i] != null)
                            {
                                if(detailTexture[i].PixelFormat != PixelFormat.Format24bppRgb ||
                                   detailTexture[i].Width != 16 || detailTexture[i].Height != 16)
                                    using(Bitmap origBitmap = detailTexture[i])
                                        detailTexture[i] = Util.ResizeImageSolid(origBitmap, 16, 16);

                                // Save the decoded and resized texture to the cache
                                byte[] data;
                                using(System.IO.MemoryStream stream = new System.IO.MemoryStream())
                                {
                                    detailTexture[i].Save(stream, ImageFormat.Png);
                                    data = stream.ToArray();
                                }

                                // Cache a PNG copy of this terrain texture
                                AssetBase newAsset = new AssetBase
                                {
                                    Data = data,
                                    Description = "PNG",
                                    Flags = AssetFlags.Collectable,
                                    FullID = UUID.Zero,
                                    ID = cacheName,
                                    Local = true,
                                    Name = String.Empty,
                                    Temporary = true,
                                    Type = (sbyte)AssetType.Unknown
                                };
                                newAsset.Metadata.ContentType = "image/png";
                                assetService.Store(newAsset);
                            }
                        }
                    }
                }

                #endregion Texture Fetching
                if(averagetextureTerrain)
                {
                    for(int t = 0; t < 4; t++)
                    {
                        usecolors = true;
                        if(detailTexture[t] == null)
                        {
                            mapColorsRed[t] = DEFAULT_TERRAIN_COLOR[t].R;
                            mapColorsGreen[t] = DEFAULT_TERRAIN_COLOR[t].G;
                            mapColorsBlue[t] = DEFAULT_TERRAIN_COLOR[t].B;
                            continue;
                        }

                        int npixeis = 0;
                        int cR = 0;
                        int cG = 0;
                        int cB = 0;

                        BitmapData bmdata = detailTexture[t].LockBits(new Rectangle(0, 0, 16, 16),
                                ImageLockMode.ReadOnly, detailTexture[t].PixelFormat);

                        npixeis = bmdata.Height * bmdata.Width;
                        int ylen = bmdata.Height * bmdata.Stride;

                        unsafe
                        {
                            for(int y = 0; y < ylen; y += bmdata.Stride)
                            {
                                byte* ptrc = (byte*)bmdata.Scan0 + y;
                                for(int x = 0 ; x < bmdata.Width; ++x)
                                {
                                    cR += *(ptrc++);
                                    cG += *(ptrc++);
                                    cB += *(ptrc++);
                                }
                            }

                        }
                        detailTexture[t].UnlockBits(bmdata);
                        detailTexture[t].Dispose();

                        mapColorsRed[t] = (byte)Util.Clamp(cR / npixeis, 0 , 255);
                        mapColorsGreen[t] = (byte)Util.Clamp(cG / npixeis, 0 , 255);
                        mapColorsBlue[t] = (byte)Util.Clamp(cB / npixeis, 0 , 255);
                    }
                }
                else
                {
                    // Fill in any missing textures with a solid color
                    for(int i = 0; i < 4; i++)
                    {
                        if(detailTexture[i] == null)
                        {
                            m_log.DebugFormat("{0} Missing terrain texture for layer {1}. Filling with solid default color", LogHeader, i);

                            // Create a solid color texture for this layer
                            detailTexture[i] = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
                            using(Graphics gfx = Graphics.FromImage(detailTexture[i]))
                            {
                                using(SolidBrush brush = new SolidBrush(DEFAULT_TERRAIN_COLOR[i]))
                                    gfx.FillRectangle(brush, 0, 0, 16, 16);
                            }
                        }
                        else
                        {
                            if(detailTexture[i].Width != 16 || detailTexture[i].Height != 16)
                            {
                                using(Bitmap origBitmap = detailTexture[i])
                                    detailTexture[i] = Util.ResizeImageSolid(origBitmap, 16, 16);
                            }
                        }
                    }
                }
            }
            else
            {
                usecolors = true;
                for(int t = 0; t < 4; t++)
                {
                    mapColorsRed[t] = DEFAULT_TERRAIN_COLOR[t].R;
                    mapColorsGreen[t] = DEFAULT_TERRAIN_COLOR[t].G;
                    mapColorsBlue[t] = DEFAULT_TERRAIN_COLOR[t].B;
                }
            }

            #region Layer Map

            float xFactor = terrain.Width / twidth;
            float yFactor = terrain.Height / theight;

            #endregion Layer Map

            #region Texture Compositing

            Bitmap output = new Bitmap(twidth, theight, PixelFormat.Format24bppRgb);
            BitmapData outputData = output.LockBits(new Rectangle(0, 0, twidth, theight), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            // Unsafe work as we lock down the source textures for quicker access and access the
            //    pixel data directly
            float invtwitdthMinus1 = 1.0f / (twidth - 1);
            float invtheightMinus1 = 1.0f / (theight - 1);
            int ty;
            int tx;
            float pctx;
            float pcty;
            float height;
            float layer;
            float layerDiff;
            int l0;
            int l1;
            uint yglobalpos;

            if(usecolors)
            {
                float a;
                float b;
                unsafe
                {
                    byte* ptrO;
                    for(int y = 0; y < theight; ++y)
                    {
                        pcty = y * invtheightMinus1;
                        ptrO = (byte*)outputData.Scan0 + y * outputData.Stride;
                        ty = (int)(y * yFactor);
                        yglobalpos = (uint)ty + regionPositionY;

                        for(int x = 0; x < twidth; ++x)
                        {
                            tx = (int)(x * xFactor);
                            pctx = x  * invtwitdthMinus1;
                            height = (float)terrain[tx, ty];
                            layer = getLayerTex(height, pctx, pcty,
                                (uint)tx + regionPositionX, yglobalpos,
                                startHeights, heightRanges);

                            // Select two textures
                            l0 = (int)layer;
                            l1 = Math.Min(l0 + 1, 3);

                            layerDiff = layer - l0;

                            a = mapColorsRed[l0];
                            b = mapColorsRed[l1];
                            *(ptrO++) = (byte)(a + layerDiff * (b - a));

                            a = mapColorsGreen[l0];
                            b = mapColorsGreen[l1];
                            *(ptrO++) = (byte)(a + layerDiff * (b - a));

                            a = mapColorsBlue[l0];
                            b = mapColorsBlue[l1];
                            *(ptrO++) = (byte)(a + layerDiff * (b - a));
                        }
                    }
                }
            }
            else
            {
                float aB;
                float aG;
                float aR;
                float bB;
                float bG;
                float bR;

                unsafe
                {
                    // Get handles to all of the texture data arrays
                    BitmapData[] datas = new BitmapData[]
                    {
                        detailTexture[0].LockBits(new Rectangle(0, 0, 16, 16), ImageLockMode.ReadOnly, detailTexture[0].PixelFormat),
                        detailTexture[1].LockBits(new Rectangle(0, 0, 16, 16), ImageLockMode.ReadOnly, detailTexture[1].PixelFormat),
                        detailTexture[2].LockBits(new Rectangle(0, 0, 16, 16), ImageLockMode.ReadOnly, detailTexture[2].PixelFormat),
                        detailTexture[3].LockBits(new Rectangle(0, 0, 16, 16), ImageLockMode.ReadOnly, detailTexture[3].PixelFormat)
                    };

                    byte* ptr;
                    byte* ptrO;
                    for(int y = 0; y < theight; y++)
                    {
                        pcty = y * invtheightMinus1;
                        int ypatch = ((int)(y * yFactor) & 0x0f) * datas[0].Stride;
                        ptrO = (byte*)outputData.Scan0 + y * outputData.Stride;
                        ty = (int)(y * yFactor);
                        yglobalpos = (uint)ty + regionPositionY;

                        for(int x = 0; x < twidth; x++)
                        {
                            tx = (int)(x * xFactor);
                            pctx = x  * invtwitdthMinus1;
                            height = (float)terrain[tx, ty];
                            layer = getLayerTex(height, pctx, pcty,
                                (uint)tx + regionPositionX, yglobalpos,
                                startHeights, heightRanges);

                            // Select two textures
                            l0 = (int)layer;
                            layerDiff = layer - l0;

                            int patchOffset = (tx & 0x0f) * 3 + ypatch;

                            ptr = (byte*)datas[l0].Scan0 + patchOffset;
                            aB = *(ptr++);
                            aG = *(ptr++);
                            aR = *(ptr);

                            l1 = Math.Min(l0 + 1, 3);
                            ptr = (byte*)datas[l1].Scan0 + patchOffset;
                            bB = *(ptr++);
                            bG = *(ptr++);
                            bR = *(ptr);


                            // Interpolate between the two selected textures
                            *(ptrO++) = (byte)(aB + layerDiff * (bB - aB));
                            *(ptrO++) = (byte)(aG + layerDiff * (bG - aG));
                            *(ptrO++) = (byte)(aR + layerDiff * (bR - aR));
                        }
                    }

                    for(int i = 0; i < detailTexture.Length; i++)
                        detailTexture[i].UnlockBits(datas[i]);
                }

                for(int i = 0; i < detailTexture.Length; i++)
                    if(detailTexture[i] != null)
                        detailTexture[i].Dispose();
            }

            output.UnlockBits(outputData);

//output.Save("terr.png",ImageFormat.Png);

            #endregion Texture Compositing

            return output;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static float getLayerTex(float height, float pctX, float pctY, uint X, uint Y,
             float[] startHeights, float[] heightRanges)
        {
            // Use bilinear interpolation between the four corners of start height and
            // height range to select the current values at this position
            float startHeight = ImageUtils.Bilinear(
                startHeights[0], startHeights[2],
                startHeights[1], startHeights[3],
                pctX, pctY);
            if (float.IsNaN(startHeight))
                return 0;

            startHeight = Utils.Clamp(startHeight, 0f, 255f);

            float heightRange = ImageUtils.Bilinear(
                heightRanges[0], heightRanges[2],
                heightRanges[1], heightRanges[3],
                pctX, pctY);
            heightRange = Utils.Clamp(heightRange, 0f, 255f);
            if(heightRange == 0f || float.IsNaN(heightRange))
                return 0;

            // Generate two frequencies of perlin noise based on our global position
            // The magic values were taken from http://opensimulator.org/wiki/Terrain_Splatting
            float sX = X * 0.20319f;
            float sY = Y * 0.20319f;

            float noise = Perlin.noise2(sX * 0.222222f, sY * 0.222222f) * 13.0f;
            noise += Perlin.turbulence2(sX, sY, 2f) * 4.5f;

            // Combine the current height, generated noise, start height, and height range parameters, then scale all of it
            float layer = ((height + noise - startHeight) / heightRange) * 4f;
            return Utils.Clamp(layer, 0f, 3f);
        }
    }
}
