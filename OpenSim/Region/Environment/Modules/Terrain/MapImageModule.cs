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
using System.Drawing;
using Nini.Config;
using OpenJPEGNet;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Terrain
{
    internal class MapImageModule : IMapImageGenerator, IRegionModule
    {
        private Scene m_scene;

        #region IMapImageGenerator Members

        public byte[] WriteJpeg2000Image(string gradientmap)
        {
            byte[] imageData = null;

            Bitmap bmp = TerrainToBitmap(gradientmap);

            try
            {
                imageData = OpenJPEG.EncodeFromImage(bmp, true);
            }
            catch (Exception e) // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                Console.WriteLine("Failed generating terrain map: " + e);
            }

            return imageData;
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IMapImageGenerator>(this);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "MapImageModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        private void ShadeBuildings(ref Bitmap map)
        {
            lock (map)
            {
                lock (m_scene.Entities)
                {
                    foreach (EntityBase entity in m_scene.Entities.Values)
                    {
                        if (entity is SceneObjectGroup)
                        {
                            SceneObjectGroup sog = (SceneObjectGroup) entity;

                            foreach (SceneObjectPart primitive in sog.Children.Values)
                            {
                                int x, y, w, h;
                                x = (int) (primitive.AbsolutePosition.X - (primitive.Scale.X / 2));
                                y = (int) (primitive.AbsolutePosition.Y - (primitive.Scale.Y / 2));
                                w = (int) primitive.Scale.X;
                                h = (int) primitive.Scale.Y;

                                int dx;
                                for (dx = x; dx < x + w; dx++)
                                {
                                    int dy;
                                    for (dy = y; dy < y + h; dy++)
                                    {
                                        if (x < 0 || y < 0)
                                            continue;
                                        if (x >= map.Width || y >= map.Height)
                                            continue;

                                        map.SetPixel(dx, dy, Color.DarkGray);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private Bitmap TerrainToBitmap(string gradientmap)
        {
            Bitmap gradientmapLd = new Bitmap(gradientmap);

            int pallete = gradientmapLd.Height;

            Bitmap bmp = new Bitmap(m_scene.Heightmap.Width, m_scene.Heightmap.Height);
            Color[] colours = new Color[pallete];

            for (int i = 0; i < pallete; i++)
            {
                colours[i] = gradientmapLd.GetPixel(0, i);
            }

            lock (m_scene.Heightmap)
            {
                ITerrainChannel copy = m_scene.Heightmap;
                for (int y = 0; y < copy.Height; y++)
                {
                    for (int x = 0; x < copy.Width; x++)
                    {
                        // 512 is the largest possible height before colours clamp
                        int colorindex = (int) (Math.Max(Math.Min(1.0, copy[x, y] / 512.0), 0.0) * (pallete - 1));

                        // Handle error conditions
                        if (colorindex > pallete - 1 || colorindex < 0)
                            bmp.SetPixel(x, copy.Height - y - 1, Color.Red);
                        else
                            bmp.SetPixel(x, copy.Height - y - 1, colours[colorindex]);
                    }
                }
                ShadeBuildings(ref bmp);
                return bmp;
            }
        }
    }
}