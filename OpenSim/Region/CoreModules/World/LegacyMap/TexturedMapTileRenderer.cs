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
using System.Drawing;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.LegacyMap
{
    // Hue, Saturation, Value; used for color-interpolation
    struct HSV {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public float h;
        public float s;
        public float v;

        public HSV(float h, float s, float v)
        {
            this.h = h;
            this.s = s;
            this.v = v;
        }

        // (for info about algorithm, see http://en.wikipedia.org/wiki/HSL_and_HSV)
        public HSV(Color c)
        {
            float r = c.R / 255f;
            float g = c.G / 255f;
            float b = c.B / 255f;
            float max = Math.Max(Math.Max(r, g), b);
            float min = Math.Min(Math.Min(r, g), b);
            float diff = max - min;

            if (max == min) h = 0f;
            else if (max == r) h = (g - b) / diff * 60f;
            else if (max == g) h = (b - r) / diff * 60f + 120f;
            else h = (r - g) / diff * 60f + 240f;
            if (h < 0f) h += 360f;

            if (max == 0f) s = 0f;
            else s = diff / max;

            v = max;
        }

        // (for info about algorithm, see http://en.wikipedia.org/wiki/HSL_and_HSV)
        public Color toColor()
        {
            if (s < 0f) m_log.Debug("S < 0: " + s);
            else if (s > 1f) m_log.Debug("S > 1: " + s);
            if (v < 0f) m_log.Debug("V < 0: " + v);
            else if (v > 1f) m_log.Debug("V > 1: " + v);

            float f = h / 60f;
            int sector = (int)f % 6;
            f = f - (int)f;
            int pi = (int)(v * (1f - s) * 255f);
            int qi = (int)(v * (1f - s * f) * 255f);
            int ti = (int)(v * (1f - (1f - f) * s) * 255f);
            int vi = (int)(v * 255f);

            if (pi < 0) pi = 0;
            if (pi > 255) pi = 255;
            if (qi < 0) qi = 0;
            if (qi > 255) qi = 255;
            if (ti < 0) ti = 0;
            if (ti > 255) ti = 255;
            if (vi < 0) vi = 0;
            if (vi > 255) vi = 255;

            switch (sector)
            {
            case 0:
                return Color.FromArgb(vi, ti, pi);
            case 1:
                return Color.FromArgb(qi, vi, pi);
            case 2:
                return Color.FromArgb(pi, vi, ti);
            case 3:
                return Color.FromArgb(pi, qi, vi);
            case 4:
                return Color.FromArgb(ti, pi, vi);
            default:
                return Color.FromArgb(vi, pi, qi);
            }
        }
    }

    public class TexturedMapTileRenderer : IMapTileTerrainRenderer
    {
        #region Constants

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[TEXTURED MAPTILE RENDERER]";

        // some hardcoded terrain UUIDs that work with SL 1.20 (the four default textures and "Blank").
        // The color-values were choosen because they "look right" (at least to me) ;-)
        private static readonly UUID defaultTerrainTexture1 = new UUID("0bc58228-74a0-7e83-89bc-5c23464bcec5");
        private static readonly Color defaultColor1 = Color.FromArgb(165, 137, 118);
        private static readonly UUID defaultTerrainTexture2 = new UUID("63338ede-0037-c4fd-855b-015d77112fc8");
        private static readonly Color defaultColor2 = Color.FromArgb(69, 89, 49);
        private static readonly UUID defaultTerrainTexture3 = new UUID("303cd381-8560-7579-23f1-f0a880799740");
        private static readonly Color defaultColor3 = Color.FromArgb(162, 154, 141);
        private static readonly UUID defaultTerrainTexture4 = new UUID("53a2f406-4895-1d13-d541-d2e3b86bc19c");
        private static readonly Color defaultColor4 = Color.FromArgb(200, 200, 200);

        private static readonly Color WATER_COLOR = Color.FromArgb(29, 71, 95);

        #endregion


        private Scene m_scene;
        // private IConfigSource m_config; // not used currently

        // mapping from texture UUIDs to averaged color. This will contain 5-9 values, in general; new values are only
        // added when the terrain textures are changed in the estate dialog and a new map is generated (and will stay in
        // that map until the region-server restarts. This could be considered a memory-leak, but it's a *very* small one.
        // TODO does it make sense to use a "real" cache and regenerate missing entries on fetch?
        private Dictionary<UUID, Color> m_mapping;


        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            // m_config = source; // not used currently
            m_mapping = new Dictionary<UUID,Color>();
            m_mapping.Add(defaultTerrainTexture1, defaultColor1);
            m_mapping.Add(defaultTerrainTexture2, defaultColor2);
            m_mapping.Add(defaultTerrainTexture3, defaultColor3);
            m_mapping.Add(defaultTerrainTexture4, defaultColor4);
            m_mapping.Add(Util.BLANK_TEXTURE_UUID, Color.White);
        }

        #region Helpers
        // This fetches the texture from the asset server synchroneously. That should be ok, as we
        // call map-creation only in those places:
        // - on start: We can wait here until the asset server returns the texture
        // TODO (- on "map" command: We are in the command-line thread, we will wait for completion anyway)
        // TODO (- on "automatic" update after some change: We are called from the mapUpdateTimer here and
        //   will wait anyway)
        private Bitmap fetchTexture(UUID id)
        {
            AssetBase asset = m_scene.AssetService.Get(id.ToString());
            m_log.DebugFormat("{0} Fetched texture {1}, found: {2}", LogHeader, id, asset != null);
            if (asset == null) return null;

            ManagedImage managedImage;
            Image image;

            try
            {
                if (OpenJPEG.DecodeToImage(asset.Data, out managedImage, out image))
                    return new Bitmap(image);
                else
                    return null;
            }
            catch (DllNotFoundException)
            {
                m_log.ErrorFormat("{0} OpenJpeg is not installed correctly on this system.   Asset Data is empty for {1}", LogHeader, id);
            }
            catch (IndexOutOfRangeException)
            {
                m_log.ErrorFormat("{0} OpenJpeg was unable to encode this.   Asset Data is empty for {1}", LogHeader, id);
            }
            catch (Exception)
            {
                m_log.ErrorFormat("{0} OpenJpeg was unable to encode this.   Asset Data is empty for {1}", LogHeader, id);
            }
            return null;

        }

        // Compute the average color of a texture.
        private Color computeAverageColor(Bitmap bmp)
        {
            // we have 256 x 256 pixel, each with 256 possible color-values per
            // color-channel, so 2^24 is the maximum value we can get, adding everything.
            // int is be big enough for that.
            int r = 0, g = 0, b = 0;
            for (int y = 0; y < bmp.Height; ++y)
            {
                for (int x = 0; x < bmp.Width; ++x)
                {
                    Color c = bmp.GetPixel(x, y);
                    r += (int)c.R & 0xff;
                    g += (int)c.G & 0xff;
                    b += (int)c.B & 0xff;
                }
            }

            int pixels = bmp.Width * bmp.Height;
            return Color.FromArgb(r / pixels, g / pixels, b / pixels);
        }

        // return either the average color of the texture, or the defaultColor if the texturID is invalid
        // or the texture couldn't be found
        private Color computeAverageColor(UUID textureID, Color defaultColor) {
            if (textureID == UUID.Zero) return defaultColor; // not set
            if (m_mapping.ContainsKey(textureID)) return m_mapping[textureID]; // one of the predefined textures

            Color color;

            using (Bitmap bmp = fetchTexture(textureID))
            {
                color = bmp == null ? defaultColor : computeAverageColor(bmp);
                // store it for future reference
                m_mapping[textureID] = color;
            }

            return color;
        }

        // S-curve: f(x) = 3x² - 2x³:
        // f(0) = 0, f(0.5) = 0.5, f(1) = 1,
        // f'(x) = 0 at x = 0 and x = 1; f'(0.5) = 1.5,
        // f''(0.5) = 0, f''(x) != 0 for x != 0.5
        private float S(float v) {
            return (v * v * (3f - 2f * v));
        }

        // interpolate two colors in HSV space and return the resulting color
        private HSV interpolateHSV(ref HSV c1, ref HSV c2, float ratio) {
            if (ratio <= 0f) return c1;
            if (ratio >= 1f) return c2;

            // make sure we are on the same side on the hue-circle for interpolation
            // We change the hue of the parameters here, but we don't change the color
            // represented by that value
            if (c1.h - c2.h > 180f) c1.h -= 360f;
            else if (c2.h - c1.h > 180f) c1.h += 360f;

            return new HSV(c1.h * (1f - ratio) + c2.h * ratio,
                           c1.s * (1f - ratio) + c2.s * ratio,
                           c1.v * (1f - ratio) + c2.v * ratio);
        }

        // the heigthfield might have some jumps in values. Rendered land is smooth, though,
        // as a slope is rendered at that place. So average 4 neighbour values to emulate that.
        private float getHeight(ITerrainChannel hm, int x, int y) {
            if (x < (hm.Width - 1) && y < (hm.Height - 1))
                return (float)(hm[x, y] * .444 + (hm[x + 1, y] + hm[x, y + 1]) * .222 + hm[x + 1, y +1] * .112);
            else
                return (float)hm[x, y];
        }
        #endregion

        public void TerrainToBitmap(Bitmap mapbmp)
        {
            int tc = Environment.TickCount;
            m_log.DebugFormat("{0} Generating Maptile Step 1: Terrain", LogHeader);

            ITerrainChannel hm = m_scene.Heightmap;

            if (mapbmp.Width != hm.Width || mapbmp.Height != hm.Height)
            {
                m_log.ErrorFormat("{0} TerrainToBitmap. Passed bitmap wrong dimensions. passed=<{1},{2}>, size=<{3},{4}>",
                    "[TEXTURED MAP TILE RENDERER]", mapbmp.Width, mapbmp.Height, hm.Width, hm.Height);
            }

            // These textures should be in the AssetCache anyway, as every client conneting to this
            // region needs them. Except on start, when the map is recreated (before anyone connected),
            // and on change of the estate settings (textures and terrain values), when the map should
            // be recreated.
            RegionSettings settings = m_scene.RegionInfo.RegionSettings;

            // the four terrain colors as HSVs for interpolation
            HSV hsv1 = new HSV(computeAverageColor(settings.TerrainTexture1, defaultColor1));
            HSV hsv2 = new HSV(computeAverageColor(settings.TerrainTexture2, defaultColor2));
            HSV hsv3 = new HSV(computeAverageColor(settings.TerrainTexture3, defaultColor3));
            HSV hsv4 = new HSV(computeAverageColor(settings.TerrainTexture4, defaultColor4));

            float levelNElow = (float)settings.Elevation1NE;
            float levelNEhigh = (float)settings.Elevation2NE;

            float levelNWlow = (float)settings.Elevation1NW;
            float levelNWhigh = (float)settings.Elevation2NW;

            float levelSElow = (float)settings.Elevation1SE;
            float levelSEhigh = (float)settings.Elevation2SE;

            float levelSWlow = (float)settings.Elevation1SW;
            float levelSWhigh = (float)settings.Elevation2SW;

            float waterHeight = (float)settings.WaterHeight;

            for (int x = 0; x < hm.Width; x++)
            {
                float columnRatio = x / (hm.Width - 1); // 0 - 1, for interpolation
                for (int y = 0; y < hm.Height; y++)
                {
                    float rowRatio = y / (hm.Height - 1); // 0 - 1, for interpolation

                    // Y flip the cordinates for the bitmap: hf origin is lower left, bm origin is upper left
                    int yr = (hm.Height - 1) - y;

                    float heightvalue = getHeight(m_scene.Heightmap, x, y);
                    if (Single.IsInfinity(heightvalue) || Single.IsNaN(heightvalue))
                        heightvalue = 0;

                    if (heightvalue > waterHeight)
                    {
                        // add a bit noise for breaking up those flat colors:
                        // - a large-scale noise, for the "patches" (using an doubled s-curve for sharper contrast)
                        // - a small-scale noise, for bringing in some small scale variation
                        //float bigNoise = (float)TerrainUtil.InterpolatedNoise(x / 8.0, y / 8.0) * .5f + .5f; // map to 0.0 - 1.0
                        //float smallNoise = (float)TerrainUtil.InterpolatedNoise(x + 33, y + 43) * .5f + .5f;
                        //float hmod = heightvalue + smallNoise * 3f + S(S(bigNoise)) * 10f;
                        float hmod =
                            heightvalue +
                            (float)TerrainUtil.InterpolatedNoise(x + 33, y + 43) * 1.5f + 1.5f + // 0 - 3
                            S(S((float)TerrainUtil.InterpolatedNoise(x / 8.0, y / 8.0) * .5f + .5f)) * 10f; // 0 - 10

                        // find the low/high values for this point (interpolated bilinearily)
                        // (and remember, x=0,y=0 is SW)
                        float low = levelSWlow * (1f - rowRatio) * (1f - columnRatio) +
                            levelSElow * (1f - rowRatio) * columnRatio +
                            levelNWlow * rowRatio * (1f - columnRatio) +
                            levelNElow * rowRatio * columnRatio;
                        float high = levelSWhigh * (1f - rowRatio) * (1f - columnRatio) +
                            levelSEhigh * (1f - rowRatio) * columnRatio +
                            levelNWhigh * rowRatio * (1f - columnRatio) +
                            levelNEhigh * rowRatio * columnRatio;
                        if (high < low)
                        {
                            // someone tried to fool us. High value should be higher than low every time
                            float tmp = high;
                            high = low;
                            low = tmp;
                        }

                        HSV hsv;
                        if (hmod <= low) hsv = hsv1; // too low
                        else if (hmod >= high) hsv = hsv4; // too high
                        else
                        {
                            // HSV-interpolate along the colors
                            // first, rescale h to 0.0 - 1.0
                            hmod = (hmod - low) / (high - low);
                            // now we have to split: 0.00 => color1, 0.33 => color2, 0.67 => color3, 1.00 => color4
                            if (hmod < 1f / 3f) hsv = interpolateHSV(ref hsv1, ref hsv2, hmod * 3f);
                            else if (hmod < 2f / 3f) hsv = interpolateHSV(ref hsv2, ref hsv3, (hmod * 3f) - 1f);
                            else hsv = interpolateHSV(ref hsv3, ref hsv4, (hmod * 3f) - 2f);
                        }

                        // Shade the terrain for shadows
                        if (x < (hm.Width - 1) && y < (hm.Height - 1))
                        {
                            float hfvaluecompare = getHeight(m_scene.Heightmap, x + 1, y + 1); // light from north-east => look at land height there
                            if (Single.IsInfinity(hfvaluecompare) || Single.IsNaN(hfvaluecompare))
                                hfvaluecompare = 0f;

                            float hfdiff = heightvalue - hfvaluecompare;  // => positive if NE is lower, negative if here is lower
                            hfdiff *= 0.06f; // some random factor so "it looks good"
                            if (hfdiff > 0.02f)
                            {
                                float highlightfactor = 0.18f;
                                // NE is lower than here
                                // We have to desaturate and lighten the land at the same time
                                hsv.s = (hsv.s - (hfdiff * highlightfactor) > 0f) ? hsv.s - (hfdiff * highlightfactor) : 0f;
                                hsv.v = (hsv.v + (hfdiff * highlightfactor) < 1f) ? hsv.v + (hfdiff * highlightfactor) : 1f;
                            }
                            else if (hfdiff < -0.02f)
                            {
                                // here is lower than NE:
                                // We have to desaturate and blacken the land at the same time
                                hsv.s = (hsv.s + hfdiff > 0f) ? hsv.s + hfdiff : 0f;
                                hsv.v = (hsv.v + hfdiff > 0f) ? hsv.v + hfdiff : 0f;
                            }
                        }
                        mapbmp.SetPixel(x, yr, hsv.toColor());
                    }
                    else
                    {
                        // We're under the water level with the terrain, so paint water instead of land

                        heightvalue = waterHeight - heightvalue;
                        if (Single.IsInfinity(heightvalue) || Single.IsNaN(heightvalue))
                            heightvalue = 0f;
                        else if (heightvalue > 19f)
                            heightvalue = 19f;
                        else if (heightvalue < 0f)
                            heightvalue = 0f;

                        heightvalue = 100f - (heightvalue * 100f) / 19f;  // 0 - 19 => 100 - 0

                        mapbmp.SetPixel(x, yr, WATER_COLOR);
                    }
                }
            }

            m_log.Debug("[TEXTURED MAP TILE RENDERER]: Generating Maptile Step 1: Done in " + (Environment.TickCount - tc) + " ms");
        }
    }
}
