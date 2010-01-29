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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.WorldMap
{
    public enum DrawRoutine
    {
        Rectangle,
        Polygon,
        Ellipse
    }

    public struct face
    {
        public Point[] pts;
    }

    public struct DrawStruct
    {
        public DrawRoutine dr;
        public Rectangle rect;
        public SolidBrush brush;
        public face[] trns;
    }

    public class MapImageModule : IMapImageGenerator, IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IConfigSource m_config;
        private IMapTileTerrainRenderer terrainRenderer;

        #region IMapImageGenerator Members

        public byte[] WriteJpeg2000Image(string gradientmap)
        {
            byte[] imageData = null;

            bool drawPrimVolume = true;
            bool textureTerrain = false;

            try
            {
                IConfig startupConfig = m_config.Configs["Startup"];
                drawPrimVolume = startupConfig.GetBoolean("DrawPrimOnMapTile", drawPrimVolume);
                textureTerrain = startupConfig.GetBoolean("TextureOnMapTile", textureTerrain);
            }
            catch
            {
                m_log.Warn("[MAPTILE]: Failed to load StartupConfig");
            }

            if (textureTerrain)
            {
                terrainRenderer = new TexturedMapTileRenderer();
            }
            else
            {
                terrainRenderer = new ShadedMapTileRenderer();
            }
            terrainRenderer.Initialise(m_scene, m_config);

            using (Bitmap mapbmp = new Bitmap((int)Constants.RegionSize, (int)Constants.RegionSize))
            {
                //long t = System.Environment.TickCount;
                //for (int i = 0; i < 10; ++i) {
                terrainRenderer.TerrainToBitmap(mapbmp);
                //}
                //t = System.Environment.TickCount - t;
                //m_log.InfoFormat("[MAPTILE] generation of 10 maptiles needed {0} ms", t);


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
                    m_log.Error("Failed generating terrain map: " + e);
                }
            }

            return imageData;
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_config = source;

            IConfig startupConfig = m_config.Configs["Startup"];
            if (startupConfig.GetString("MapImageModule", "MapImageModule") !=
                    "MapImageModule")
                return;

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

// TODO: unused:
//         private void ShadeBuildings(Bitmap map)
//         {
//             lock (map)
//             {
//                 lock (m_scene.Entities)
//                 {
//                     foreach (EntityBase entity in m_scene.Entities.Values)
//                     {
//                         if (entity is SceneObjectGroup)
//                         {
//                             SceneObjectGroup sog = (SceneObjectGroup) entity;
//
//                             foreach (SceneObjectPart primitive in sog.Children.Values)
//                             {
//                                 int x = (int) (primitive.AbsolutePosition.X - (primitive.Scale.X / 2));
//                                 int y = (int) (primitive.AbsolutePosition.Y - (primitive.Scale.Y / 2));
//                                 int w = (int) primitive.Scale.X;
//                                 int h = (int) primitive.Scale.Y;
//
//                                 int dx;
//                                 for (dx = x; dx < x + w; dx++)
//                                 {
//                                     int dy;
//                                     for (dy = y; dy < y + h; dy++)
//                                     {
//                                         if (x < 0 || y < 0)
//                                             continue;
//                                         if (x >= map.Width || y >= map.Height)
//                                             continue;
//
//                                         map.SetPixel(dx, dy, Color.DarkGray);
//                                     }
//                                 }
//                             }
//                         }
//                     }
//                 }
//             }
//         }

        private Bitmap DrawObjectVolume(Scene whichScene, Bitmap mapbmp)
        {
            int tc = 0;
            double[,] hm = whichScene.Heightmap.GetDoubles();
            tc = Environment.TickCount;
            m_log.Info("[MAPTILE]: Generating Maptile Step 2: Object Volume Profile");
            List<EntityBase> objs = whichScene.GetEntities();
            Dictionary<uint, DrawStruct> z_sort = new Dictionary<uint, DrawStruct>();
            //SortedList<float, RectangleDrawStruct> z_sort = new SortedList<float, RectangleDrawStruct>();
            List<float> z_sortheights = new List<float>();
            List<uint> z_localIDs = new List<uint>();

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
                                    // get the null checks out of the way
                                    // skip the ones that break
                                    if (part == null)
                                        continue;

                                    if (part.Shape == null)
                                        continue;

                                    if (part.Shape.PCode == (byte)PCode.Tree || part.Shape.PCode == (byte)PCode.NewTree || part.Shape.PCode == (byte)PCode.Grass)
                                        continue; // eliminates trees from this since we don't really have a good tree representation
                                    // if you want tree blocks on the map comment the above line and uncomment the below line
                                    //mapdotspot = Color.PaleGreen;

                                    if (part.Shape.Textures == null)
                                        continue;

                                    if (part.Shape.Textures.DefaultTexture == null)
                                        continue;

                                    Color4 texcolor = part.Shape.Textures.DefaultTexture.RGBA;

                                    // Not sure why some of these are null, oh well.

                                    int colorr = 255 - (int)(texcolor.R * 255f);
                                    int colorg = 255 - (int)(texcolor.G * 255f);
                                    int colorb = 255 - (int)(texcolor.B * 255f);

                                    if (!(colorr == 255 && colorg == 255 && colorb == 255))
                                    {
                                        //Try to set the map spot color
                                        try
                                        {
                                            // If the color gets goofy somehow, skip it *shakes fist at Color4
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

                                Vector3 pos = part.GetWorldPosition();

                                // skip prim outside of retion
                                if (pos.X < 0f || pos.X > 256f || pos.Y < 0f || pos.Y > 256f)
                                    continue;

                                // skip prim in non-finite position
                                if (Single.IsNaN(pos.X) || Single.IsNaN(pos.Y) ||
                                    Single.IsInfinity(pos.X) || Single.IsInfinity(pos.Y))
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
                                    Vector3 lscale = new Vector3(part.Shape.Scale.X, part.Shape.Scale.Y, part.Shape.Scale.Z);
                                    Vector3 scale = new Vector3();
                                    Vector3 tScale = new Vector3();
                                    Vector3 axPos = new Vector3(pos.X,pos.Y,pos.Z);

                                    Quaternion llrot = part.GetWorldRotation();
                                    Quaternion rot = new Quaternion(llrot.W, llrot.X, llrot.Y, llrot.Z);
                                    scale = lscale * rot;

                                    // negative scales don't work in this situation
                                    scale.X = Math.Abs(scale.X);
                                    scale.Y = Math.Abs(scale.Y);
                                    scale.Z = Math.Abs(scale.Z);

                                    // This scaling isn't very accurate and doesn't take into account the face rotation :P
                                    int mapdrawstartX = (int)(pos.X - scale.X);
                                    int mapdrawstartY = (int)(pos.Y - scale.Y);
                                    int mapdrawendX = (int)(pos.X + scale.X);
                                    int mapdrawendY = (int)(pos.Y + scale.Y);

                                    // If object is beyond the edge of the map, don't draw it to avoid errors
                                    if (mapdrawstartX < 0 || mapdrawstartX > ((int)Constants.RegionSize - 1) || mapdrawendX < 0 || mapdrawendX > ((int)Constants.RegionSize - 1)
                                                          || mapdrawstartY < 0 || mapdrawstartY > ((int)Constants.RegionSize - 1) || mapdrawendY < 0
                                                          || mapdrawendY > ((int)Constants.RegionSize - 1))
                                        continue;

#region obb face reconstruction part duex
                                    Vector3[] vertexes = new Vector3[8];

                                    // float[] distance = new float[6];
                                    Vector3[] FaceA = new Vector3[6]; // vertex A for Facei
                                    Vector3[] FaceB = new Vector3[6]; // vertex B for Facei
                                    Vector3[] FaceC = new Vector3[6]; // vertex C for Facei
                                    Vector3[] FaceD = new Vector3[6]; // vertex D for Facei

                                    tScale = new Vector3(lscale.X, -lscale.Y, lscale.Z);
                                    scale = ((tScale * rot));
                                    vertexes[0] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));
                                    // vertexes[0].x = pos.X + vertexes[0].x;
                                    //vertexes[0].y = pos.Y + vertexes[0].y;
                                    //vertexes[0].z = pos.Z + vertexes[0].z;

                                    FaceA[0] = vertexes[0];
                                    FaceB[3] = vertexes[0];
                                    FaceA[4] = vertexes[0];

                                    tScale = lscale;
                                    scale = ((tScale * rot));
                                    vertexes[1] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                    // vertexes[1].x = pos.X + vertexes[1].x;
                                    // vertexes[1].y = pos.Y + vertexes[1].y;
                                    //vertexes[1].z = pos.Z + vertexes[1].z;

                                    FaceB[0] = vertexes[1];
                                    FaceA[1] = vertexes[1];
                                    FaceC[4] = vertexes[1];

                                    tScale = new Vector3(lscale.X, -lscale.Y, -lscale.Z);
                                    scale = ((tScale * rot));

                                    vertexes[2] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                    //vertexes[2].x = pos.X + vertexes[2].x;
                                    //vertexes[2].y = pos.Y + vertexes[2].y;
                                    //vertexes[2].z = pos.Z + vertexes[2].z;

                                    FaceC[0] = vertexes[2];
                                    FaceD[3] = vertexes[2];
                                    FaceC[5] = vertexes[2];

                                    tScale = new Vector3(lscale.X, lscale.Y, -lscale.Z);
                                    scale = ((tScale * rot));
                                    vertexes[3] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                    //vertexes[3].x = pos.X + vertexes[3].x;
                                    // vertexes[3].y = pos.Y + vertexes[3].y;
                                    // vertexes[3].z = pos.Z + vertexes[3].z;

                                    FaceD[0] = vertexes[3];
                                    FaceC[1] = vertexes[3];
                                    FaceA[5] = vertexes[3];

                                    tScale = new Vector3(-lscale.X, lscale.Y, lscale.Z);
                                    scale = ((tScale * rot));
                                    vertexes[4] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                    // vertexes[4].x = pos.X + vertexes[4].x;
                                    // vertexes[4].y = pos.Y + vertexes[4].y;
                                    // vertexes[4].z = pos.Z + vertexes[4].z;

                                    FaceB[1] = vertexes[4];
                                    FaceA[2] = vertexes[4];
                                    FaceD[4] = vertexes[4];

                                    tScale = new Vector3(-lscale.X, lscale.Y, -lscale.Z);
                                    scale = ((tScale * rot));
                                    vertexes[5] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                    // vertexes[5].x = pos.X + vertexes[5].x;
                                    // vertexes[5].y = pos.Y + vertexes[5].y;
                                    // vertexes[5].z = pos.Z + vertexes[5].z;

                                    FaceD[1] = vertexes[5];
                                    FaceC[2] = vertexes[5];
                                    FaceB[5] = vertexes[5];

                                    tScale = new Vector3(-lscale.X, -lscale.Y, lscale.Z);
                                    scale = ((tScale * rot));
                                    vertexes[6] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                    // vertexes[6].x = pos.X + vertexes[6].x;
                                    // vertexes[6].y = pos.Y + vertexes[6].y;
                                    // vertexes[6].z = pos.Z + vertexes[6].z;

                                    FaceB[2] = vertexes[6];
                                    FaceA[3] = vertexes[6];
                                    FaceB[4] = vertexes[6];

                                    tScale = new Vector3(-lscale.X, -lscale.Y, -lscale.Z);
                                    scale = ((tScale * rot));
                                    vertexes[7] = (new Vector3((pos.X + scale.X), (pos.Y + scale.Y), (pos.Z + scale.Z)));

                                    // vertexes[7].x = pos.X + vertexes[7].x;
                                    // vertexes[7].y = pos.Y + vertexes[7].y;
                                    // vertexes[7].z = pos.Z + vertexes[7].z;

                                    FaceD[2] = vertexes[7];
                                    FaceC[3] = vertexes[7];
                                    FaceD[5] = vertexes[7];
#endregion

                                    //int wy = 0;

                                    //bool breakYN = false; // If we run into an error drawing, break out of the
                                    // loop so we don't lag to death on error handling
                                    DrawStruct ds = new DrawStruct();
                                    ds.brush = new SolidBrush(mapdotspot);
                                    //ds.rect = new Rectangle(mapdrawstartX, (255 - mapdrawstartY), mapdrawendX - mapdrawstartX, mapdrawendY - mapdrawstartY);

                                    ds.trns = new face[FaceA.Length];

                                    for (int i = 0; i < FaceA.Length; i++)
                                    {
                                        Point[] working = new Point[5];
                                        working[0] = project(FaceA[i], axPos);
                                        working[1] = project(FaceB[i], axPos);
                                        working[2] = project(FaceD[i], axPos);
                                        working[3] = project(FaceC[i], axPos);
                                        working[4] = project(FaceA[i], axPos);

                                        face workingface = new face();
                                        workingface.pts = working;

                                        ds.trns[i] = workingface;
                                    }

                                    z_sort.Add(part.LocalId, ds);
                                    z_localIDs.Add(part.LocalId);
                                    z_sortheights.Add(pos.Z);

                                    //for (int wx = mapdrawstartX; wx < mapdrawendX; wx++)
                                    //{
                                        //for (wy = mapdrawstartY; wy < mapdrawendY; wy++)
                                        //{
                                            //m_log.InfoFormat("[MAPDEBUG]: {0},{1}({2})", wx, (255 - wy),wy);
                                            //try
                                            //{
                                                // Remember, flip the y!
                                            //    mapbmp.SetPixel(wx, (255 - wy), mapdotspot);
                                            //}
                                            //catch (ArgumentException)
                                            //{
                                            //    breakYN = true;
                                            //}

                                            //if (breakYN)
                                            //    break;
                                        //}

                                        //if (breakYN)
                                        //    break;
                                    //}
                                } // Object is within 256m Z of terrain
                            } // object is at least a meter wide
                        } // loop over group children
                    } // entitybase is sceneobject group
                } // foreach loop over entities

                float[] sortedZHeights = z_sortheights.ToArray();
                uint[] sortedlocalIds = z_localIDs.ToArray();

                // Sort prim by Z position
                Array.Sort(sortedZHeights, sortedlocalIds);

                Graphics g = Graphics.FromImage(mapbmp);

                for (int s = 0; s < sortedZHeights.Length; s++)
                {
                    if (z_sort.ContainsKey(sortedlocalIds[s]))
                    {
                        DrawStruct rectDrawStruct = z_sort[sortedlocalIds[s]];
                        for (int r = 0; r < rectDrawStruct.trns.Length; r++)
                        {
                            g.FillPolygon(rectDrawStruct.brush,rectDrawStruct.trns[r].pts);
                        }
                        //g.FillRectangle(rectDrawStruct.brush , rectDrawStruct.rect);
                    }
                }

                g.Dispose();
            } // lock entities objs

            m_log.Info("[MAPTILE]: Generating Maptile Step 2: Done in " + (Environment.TickCount - tc) + " ms");
            return mapbmp;
        }

        private Point project(Vector3 point3d, Vector3 originpos)
        {
            Point returnpt = new Point();
            //originpos = point3d;
            //int d = (int)(256f / 1.5f);

            //Vector3 topos = new Vector3(0, 0, 0);
           // float z = -point3d.z - topos.z;

            returnpt.X = (int)point3d.X;//(int)((topos.x - point3d.x) / z * d);
            returnpt.Y = (int)(((int)Constants.RegionSize - 1) - point3d.Y);//(int)(255 - (((topos.y - point3d.y) / z * d)));

            return returnpt;
        }

// TODO: unused:
//         #region Deprecated Maptile Generation.  Adam may update this
//         private Bitmap TerrainToBitmap(string gradientmap)
//         {
//             Bitmap gradientmapLd = new Bitmap(gradientmap);
//
//             int pallete = gradientmapLd.Height;
//
//             Bitmap bmp = new Bitmap(m_scene.Heightmap.Width, m_scene.Heightmap.Height);
//             Color[] colours = new Color[pallete];
//
//             for (int i = 0; i < pallete; i++)
//             {
//                 colours[i] = gradientmapLd.GetPixel(0, i);
//             }
//
//             lock (m_scene.Heightmap)
//             {
//                 ITerrainChannel copy = m_scene.Heightmap;
//                 for (int y = 0; y < copy.Height; y++)
//                 {
//                     for (int x = 0; x < copy.Width; x++)
//                     {
//                         // 512 is the largest possible height before colours clamp
//                         int colorindex = (int) (Math.Max(Math.Min(1.0, copy[x, y] / 512.0), 0.0) * (pallete - 1));
//
//                         // Handle error conditions
//                         if (colorindex > pallete - 1 || colorindex < 0)
//                             bmp.SetPixel(x, copy.Height - y - 1, Color.Red);
//                         else
//                             bmp.SetPixel(x, copy.Height - y - 1, colours[colorindex]);
//                     }
//                 }
//                 ShadeBuildings(bmp);
//                 return bmp;
//             }
//         }
//         #endregion
    }
}
