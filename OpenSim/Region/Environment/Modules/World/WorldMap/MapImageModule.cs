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
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Axiom.Math;
using Nini.Config;
using log4net;
using OpenJPEGNet;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;

namespace OpenSim.Region.Environment.Modules.World.WorldMap
{
    public class MapImageModule : IMapImageGenerator, IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IConfigSource m_config;

        #region IMapImageGenerator Members

        public byte[] WriteJpeg2000Image(string gradientmap)
        {
            byte[] imageData = null;
            Bitmap mapbmp = new Bitmap(256, 256);

            //Bitmap bmp = TerrainToBitmap(gradientmap);
            mapbmp = TerrainToBitmap2(m_scene,mapbmp);

            bool drawPrimVolume = true;

            try
            {
                IConfig startupConfig = m_config.Configs["Startup"];
                drawPrimVolume = startupConfig.GetBoolean("DrawPrimOnMapTile", true);
            }
            catch (Exception)
            {
                m_log.Warn("Failed to load StartupConfig");
            }

            if (drawPrimVolume)
            {
                DrawObjectVolume(m_scene, mapbmp);
            }


            try
            {
                imageData = OpenJPEG.EncodeFromImage(mapbmp, true);
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
            m_config = source;
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

        private void ShadeBuildings(Bitmap map)
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
                                int x = (int) (primitive.AbsolutePosition.X - (primitive.Scale.X / 2));
                                int y = (int) (primitive.AbsolutePosition.Y - (primitive.Scale.Y / 2));
                                int w = (int) primitive.Scale.X;
                                int h = (int) primitive.Scale.Y;

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

        private Bitmap TerrainToBitmap2(Scene whichScene, Bitmap mapbmp)
        {
            int tc = System.Environment.TickCount;
            m_log.Info("[MAPTILE]: Generating Maptile Step 1: Terrain");

            double[,] hm = whichScene.Heightmap.GetDoubles();
            bool ShadowDebugContinue = true;
            //Color prim = Color.FromArgb(120, 120, 120);
            //LLVector3 RayEnd = new LLVector3(0, 0, 0);
            //LLVector3 RayStart = new LLVector3(0, 0, 0);
            //LLVector3 direction = new LLVector3(0, 0, -1);
            //Vector3 AXOrigin = new Vector3();
            //Vector3 AXdirection = new Vector3();
            //Ray testRay = new Ray();
            //EntityIntersection rt = new EntityIntersection();
            bool terraincorruptedwarningsaid = false;

            float low = 255;
            float high = 0;
            for (int x = 0; x < 256; x++)
            {
                for (int y = 0; y < 256; y++)
                {
                    float hmval = (float)hm[x, y];
                    if (hmval < low)
                        low = hmval;
                    if (hmval > high)
                        high = hmval;
                }
            }

            float mid = (high + low) * 0.5f;

            // temporary initializer
            float hfvalue = (float)whichScene.RegionInfo.RegionSettings.WaterHeight;
            float hfvaluecompare = hfvalue;
            float hfdiff = hfvalue;
            int hfdiffi = 0;


            for (int x = 0; x < 256; x++)
            {
                //int tc = System.Environment.TickCount;
                for (int y = 0; y < 256; y++)
                {
                    //RayEnd = new LLVector3(x, y, 0);
                    //RayStart = new LLVector3(x, y, 255);

                    //direction = LLVector3.Norm(RayEnd - RayStart);
                    //AXOrigin = new Vector3(RayStart.X, RayStart.Y, RayStart.Z);
                    //AXdirection = new Vector3(direction.X, direction.Y, direction.Z);

                    //testRay = new Ray(AXOrigin, AXdirection);
                    //rt = m_innerScene.GetClosestIntersectingPrim(testRay);

                    //if (rt.HitTF)
                    //{
                    //mapbmp.SetPixel(x, y, prim);
                    //}
                    //else
                    //{
                    //float tmpval = (float)hm[x, y];
                    float heightvalue = (float)hm[x, y];


                    if (heightvalue > (float)whichScene.RegionInfo.RegionSettings.WaterHeight)
                    {

                        // scale height value
                        heightvalue = low + mid * (heightvalue - low) / mid;

                        if (heightvalue > 255)
                            heightvalue = 255;

                        if (heightvalue < 0)
                            heightvalue = 0;

                        if (Single.IsInfinity(heightvalue) || Single.IsNaN(heightvalue))
                            heightvalue = 0;
                        try
                        {
                            Color green = Color.FromArgb((int)heightvalue, 100, (int)heightvalue);

                            // Y flip the cordinates
                            mapbmp.SetPixel(x, (256 - y) - 1, green);

                            //X
                            // .
                            //
                            // Shade the terrain for shadows
                            if ((x - 1 > 0) && (y - 1 > 0))
                            {
                                hfvalue = (float)hm[x, y];
                                hfvaluecompare = (float)hm[x - 1, y - 1];

                                if (Single.IsInfinity(hfvalue) || Single.IsNaN(hfvalue))
                                    hfvalue = 0;

                                if (Single.IsInfinity(hfvaluecompare) || Single.IsNaN(hfvaluecompare))
                                    hfvaluecompare = 0;

                                hfdiff = hfvaluecompare - hfvalue;

                                if (hfdiff > 0.3f)
                                {

                                }
                                else if (hfdiff < -0.3f)
                                {
                                    // We have to desaturate and blacken the land at the same time
                                    // we use floats, colors use bytes, so shrink are space down to
                                    // 0-255


                                    try
                                    {
                                        hfdiffi = Math.Abs((int)((hfdiff * 4) + (hfdiff * 0.5))) + 1;
                                        if (hfdiff % 1 != 0)
                                        {
                                            hfdiffi = hfdiffi + Math.Abs((int)(((hfdiff % 1) * 0.5f) * 10f) - 1);
                                        }
                                    }
                                    catch (System.OverflowException)
                                    {
                                        m_log.Debug("[MAPTILE]: Shadow failed at value: " + hfdiff.ToString());
                                        ShadowDebugContinue = false;
                                    }

                                    if (ShadowDebugContinue)
                                    {
                                        if ((256 - y) - 1 > 0)
                                        {
                                            Color Shade = mapbmp.GetPixel(x - 1, (256 - y) - 1);

                                            int r = Shade.R;

                                            int g = Shade.G;
                                            int b = Shade.B;
                                            Shade = Color.FromArgb((r - hfdiffi > 0) ? r - hfdiffi : 0, (g - hfdiffi > 0) ? g - hfdiffi : 0, (b - hfdiffi > 0) ? b - hfdiffi : 0);
                                            //Console.WriteLine("d:" + hfdiff.ToString() + ", i:" + hfdiffi + ", pos: " + x + "," + y + " - R:" + Shade.R.ToString() + ", G:" + Shade.G.ToString() + ", B:" + Shade.G.ToString());
                                            mapbmp.SetPixel(x - 1, (256 - y) - 1, Shade);
                                        }
                                    }


                                }

                            }




                        }
                        catch (System.ArgumentException)
                        {
                            if (!terraincorruptedwarningsaid)
                            {
                                m_log.WarnFormat("[MAPIMAGE]: Your terrain is corrupted in region {0}, it might take a few minutes to generate the map image depending on the corruption level", whichScene.RegionInfo.RegionName);
                                terraincorruptedwarningsaid = true;
                            }
                            Color black = Color.Black;
                            mapbmp.SetPixel(x, (256 - y) - 1, black);
                        }
                    }
                    else
                    {
                        // Y flip the cordinates
                        heightvalue = (float)whichScene.RegionInfo.RegionSettings.WaterHeight - heightvalue;
                        if (heightvalue > 19)
                            heightvalue = 19;
                        if (heightvalue < 0)
                            heightvalue = 0;

                        heightvalue = 100 - (heightvalue * 100) / 19;

                        if (heightvalue > 255)
                            heightvalue = 255;

                        if (heightvalue < 0)
                            heightvalue = 0;

                        if (Single.IsInfinity(heightvalue) || Single.IsNaN(heightvalue))
                            heightvalue = 0;

                        try
                        {
                            Color water = Color.FromArgb((int)heightvalue, (int)heightvalue, 255);
                            mapbmp.SetPixel(x, (256 - y) - 1, water);
                        }
                        catch (System.ArgumentException)
                        {
                            if (!terraincorruptedwarningsaid)
                            {
                                m_log.WarnFormat("[MAPIMAGE]: Your terrain is corrupted in region {0}, it might take a few minutes to generate the map image depending on the corruption level", whichScene.RegionInfo.RegionName);
                                terraincorruptedwarningsaid = true;
                            }
                            Color black = Color.Black;
                            mapbmp.SetPixel(x, (256 - y) - 1, black);
                        }
                    }
                }
                //}

                //tc = System.Environment.TickCount - tc;
                //m_log.Info("[MAPTILE]: Completed One row in " + tc + " ms");
            }
            m_log.Info("[MAPTILE]: Generating Maptile Step 1: Done in " + (System.Environment.TickCount - tc) + " ms");

            return mapbmp;
        }


        private Bitmap DrawObjectVolume(Scene whichScene, Bitmap mapbmp)
        {
            int tc = 0;
            double[,] hm = whichScene.Heightmap.GetDoubles();
            tc = System.Environment.TickCount;
            m_log.Info("[MAPTILE]: Generating Maptile Step 2: Object Volume Profile");
            List<EntityBase> objs = whichScene.GetEntities();

            lock (objs)
            {
                foreach (EntityBase obj in objs)
                {
                    // Only draw the contents of SceneObjectGroup
                    if (obj is SceneObjectGroup)
                    {
                        SceneObjectGroup mapdot = (SceneObjectGroup)obj;
                        Color mapdotspot = Color.Gray; // Default color when prim color is white
                        // Loop over prim in group
                        foreach (SceneObjectPart part in mapdot.Children.Values)
                        {
                            if (part == null)
                                continue;


                            // Draw if the object is at least 1 meter wide in any direction
                            if (part.Scale.X > 1f || part.Scale.Y > 1f || part.Scale.Z > 1f)
                            {
                                // Try to get the RGBA of the default texture entry..
                                //
                                try
                                {
                                    if (part == null)
                                        continue;

                                    if (part.Shape == null)
                                        continue;

                                    if (part.Shape.PCode == (byte)PCode.Tree || part.Shape.PCode == (byte)PCode.NewTree)
                                        continue; // eliminates trees from this since we don't really have a good tree representation
                                    // if you want tree blocks on the map comment the above line and uncomment the below line
                                    //mapdotspot = Color.PaleGreen;

                                    if (part.Shape.Textures == null)
                                        continue;

                                    if (part.Shape.Textures.DefaultTexture == null)
                                        continue;

                                    LLColor texcolor = part.Shape.Textures.DefaultTexture.RGBA;

                                    // Not sure why some of these are null, oh well.

                                    int colorr = 255 - (int)(texcolor.R * 255f);
                                    int colorg = 255 - (int)(texcolor.G * 255f);
                                    int colorb = 255 - (int)(texcolor.B * 255f);

                                    if (!(colorr == 255 && colorg == 255 && colorb == 255))
                                    {
                                        //Try to set the map spot color
                                        try
                                        {
                                            // If the color gets goofy somehow, skip it *shakes fist at LLColor
                                            mapdotspot = Color.FromArgb(colorr, colorg, colorb);
                                        }
                                        catch (ArgumentException)
                                        {
                                        }
                                    }
                                }
                                catch (IndexOutOfRangeException)
                                {
                                    // Windows Array
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    // Mono Array
                                }

                                LLVector3 pos = part.GetWorldPosition();

                                // skip prim outside of retion
                                if (pos.X < 0f || pos.X > 256f || pos.Y < 0f || pos.Y > 256f)
                                    continue;

                                // skip prim in non-finite position
                                if (Single.IsNaN(pos.X) || Single.IsNaN(pos.Y) || Single.IsInfinity(pos.X)
                                                        || Single.IsInfinity(pos.Y))
                                    continue;

                                // Figure out if object is under 256m above the height of the terrain
                                bool isBelow256AboveTerrain = false;

                                try
                                {
                                    isBelow256AboveTerrain = (pos.Z < ((float)hm[(int)pos.X, (int)pos.Y] + 256f));
                                }
                                catch (Exception)
                                {
                                }

                                if (isBelow256AboveTerrain)
                                {
                                    // Translate scale by rotation so scale is represented properly when object is rotated
                                    Vector3 scale = new Vector3(part.Shape.Scale.X, part.Shape.Scale.Y, part.Shape.Scale.Z);
                                    LLQuaternion llrot = part.GetWorldRotation();
                                    Quaternion rot = new Quaternion(llrot.W, llrot.X, llrot.Y, llrot.Z);
                                    scale = rot * scale;

                                    // negative scales don't work in this situation
                                    scale.x = Math.Abs(scale.x);
                                    scale.y = Math.Abs(scale.y);
                                    scale.z = Math.Abs(scale.z);

                                    // This scaling isn't very accurate and doesn't take into account the face rotation :P
                                    int mapdrawstartX = (int)(pos.X - scale.x);
                                    int mapdrawstartY = (int)(pos.Y - scale.y);
                                    int mapdrawendX = (int)(pos.X + scale.x);
                                    int mapdrawendY = (int)(pos.Y + scale.y);

                                    // If object is beyond the edge of the map, don't draw it to avoid errors
                                    if (mapdrawstartX < 0 || mapdrawstartX > 255 || mapdrawendX < 0 || mapdrawendX > 255
                                                          || mapdrawstartY < 0 || mapdrawstartY > 255 || mapdrawendY < 0
                                                          || mapdrawendY > 255)
                                        continue;

                                    int wy = 0;

                                    bool breakYN = false; // If we run into an error drawing, break out of the
                                    // loop so we don't lag to death on error handling
                                    for (int wx = mapdrawstartX; wx < mapdrawendX; wx++)
                                    {
                                        for (wy = mapdrawstartY; wy < mapdrawendY; wy++)
                                        {
                                            //m_log.InfoFormat("[MAPDEBUG]: {0},{1}({2})", wx, (255 - wy),wy);
                                            try
                                            {
                                                // Remember, flip the y!
                                                mapbmp.SetPixel(wx, (255 - wy), mapdotspot);
                                            }
                                            catch (ArgumentException)
                                            {
                                                breakYN = true;
                                            }

                                            if (breakYN)
                                                break;
                                        }

                                        if (breakYN)
                                            break;
                                    }
                                } // Object is within 256m Z of terrain
                            } // object is at least a meter wide
                        } // loop over group children
                    } // entitybase is sceneobject group
                } // foreach loop over entities
            } // lock entities objs

            m_log.Info("[MAPTILE]: Generating Maptile Step 2: Done in " + (System.Environment.TickCount - tc) + " ms");
            return mapbmp;
        }

        # region Depreciated Maptile Generation.  Adam may update this
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
                ShadeBuildings(bmp);
                return bmp;
            }
        }
        #endregion
    }
}
