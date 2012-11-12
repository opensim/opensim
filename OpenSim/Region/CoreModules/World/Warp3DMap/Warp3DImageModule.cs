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
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using CSJ2K;
using Nini.Config;
using log4net;
using Rednettle.Warp3D;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Services.Interfaces;

using WarpRenderer = global::Warp3D.Warp3D;

namespace OpenSim.Region.CoreModules.World.Warp3DMap
{
    public class Warp3DImageModule : IMapImageGenerator, INonSharedRegionModule
    {
        private static readonly UUID TEXTURE_METADATA_MAGIC = new UUID("802dc0e0-f080-4931-8b57-d1be8611c4f3");
        private static readonly Color4 WATER_COLOR = new Color4(29, 72, 96, 216);

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IRendering m_primMesher;
        private IConfigSource m_config;
        private Dictionary<UUID, Color4> m_colors = new Dictionary<UUID, Color4>();
        private bool m_useAntiAliasing = false; // TODO: Make this a config option
        private bool m_Enabled = false;

        private Bitmap lastImage = null;
        private DateTime lastImageTime = DateTime.MinValue;

        #region Region Module interface

        public void Initialise(IConfigSource source)
        {
            m_config = source;

            IConfig startupConfig = m_config.Configs["Startup"];
            if (startupConfig.GetString("MapImageModule", "MapImageModule") != "Warp3DImageModule")
                return;

            m_Enabled = true;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene = scene;

            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
                m_log.Info("[MAPTILE]: Loaded prim mesher " + renderers[0]);
            else
                m_log.Info("[MAPTILE]: No prim mesher loaded, prim rendering will be disabled");

            m_scene.RegisterModuleInterface<IMapImageGenerator>(this);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "Warp3DImageModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region IMapImageGenerator Members

        public Bitmap CreateMapTile()
        {
            if ((DateTime.Now - lastImageTime).TotalSeconds < 3600)
            {
                return lastImage.Clone(new Rectangle(0, 0, 256, 256), lastImage.PixelFormat);
            }

            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
            {
                m_primMesher = RenderingLoader.LoadRenderer(renderers[0]);
            }

            Vector3 camPos = new Vector3(127.5f, 127.5f, 221.7025033688163f);
            Viewport viewport = new Viewport(camPos, -Vector3.UnitZ, 1024f, 0.1f, (int)Constants.RegionSize, (int)Constants.RegionSize, (float)Constants.RegionSize, (float)Constants.RegionSize);
            Bitmap tile = CreateMapTile(viewport, false);
            m_primMesher = null;

            lastImage = tile;
            lastImageTime = DateTime.Now;
            return lastImage.Clone(new Rectangle(0, 0, 256, 256), lastImage.PixelFormat);
        }

        public Bitmap CreateViewImage(Vector3 camPos, Vector3 camDir, float fov, int width, int height, bool useTextures)
        {
            Viewport viewport = new Viewport(camPos, camDir, fov, (float)Constants.RegionSize, 0.1f, width, height);
            return CreateMapTile(viewport, useTextures);
        }

        public Bitmap CreateMapTile(Viewport viewport, bool useTextures)
        {
            bool drawPrimVolume = true;
            bool textureTerrain = true;

            try
            {
                IConfig startupConfig = m_config.Configs["Startup"];
                drawPrimVolume = startupConfig.GetBoolean("DrawPrimOnMapTile", drawPrimVolume);
                textureTerrain = startupConfig.GetBoolean("TextureOnMapTile", textureTerrain);
            }
            catch
            {
                m_log.Warn("[WARP 3D IMAGE MODULE]: Failed to load StartupConfig");
            }

            m_colors.Clear();

            int width = viewport.Width;
            int height = viewport.Height;

            if (m_useAntiAliasing)
            {
                width *= 2;
                height *= 2;
            }

            WarpRenderer renderer = new WarpRenderer();
            renderer.CreateScene(width, height);
            renderer.Scene.autoCalcNormals = false;

            #region Camera

            warp_Vector pos = ConvertVector(viewport.Position);
            pos.z -= 0.001f; // Works around an issue with the Warp3D camera
            warp_Vector lookat = warp_Vector.add(ConvertVector(viewport.Position), ConvertVector(viewport.LookDirection));

            renderer.Scene.defaultCamera.setPos(pos);
            renderer.Scene.defaultCamera.lookAt(lookat);

            if (viewport.Orthographic)
            {
                renderer.Scene.defaultCamera.isOrthographic = true;
                renderer.Scene.defaultCamera.orthoViewWidth = viewport.OrthoWindowWidth;
                renderer.Scene.defaultCamera.orthoViewHeight = viewport.OrthoWindowHeight;
            }
            else
            {
                float fov = viewport.FieldOfView;
                fov *= 1.75f; // FIXME: ???
                renderer.Scene.defaultCamera.setFov(fov);
            }

            #endregion Camera

            renderer.Scene.addLight("Light1", new warp_Light(new warp_Vector(1.0f, 0.5f, 1f), 0xffffff, 0, 320, 40));
            renderer.Scene.addLight("Light2", new warp_Light(new warp_Vector(-1f, -1f, 1f), 0xffffff, 0, 100, 40));

            CreateWater(renderer);
            CreateTerrain(renderer, textureTerrain);
            if (drawPrimVolume)
                CreateAllPrims(renderer, useTextures);

            renderer.Render();
            Bitmap bitmap = renderer.Scene.getImage();

            if (m_useAntiAliasing)
            {
                using (Bitmap origBitmap = bitmap)
                    bitmap = ImageUtils.ResizeImage(origBitmap, viewport.Width, viewport.Height);
            }

            // XXX: It shouldn't really be necesary to force a GC here as one should occur anyway pretty shortly
            // afterwards.  It's generally regarded as a bad idea to manually GC.  If Warp3D is using lots of memory
            // then this may be some issue with the Warp3D code itself, though it's also quite possible that generating
            // this map tile simply takes a lot of memory.
            GC.Collect();
            m_log.Debug("[WARP 3D IMAGE MODULE]: GC.Collect()");

            return bitmap;
        }

        public byte[] WriteJpeg2000Image()
        {
            try
            {
                using (Bitmap mapbmp = CreateMapTile())
                    return OpenJPEG.EncodeFromImage(mapbmp, true);
            }
            catch (Exception e)
            {
                // JPEG2000 encoder failed
                m_log.Error("[WARP 3D IMAGE MODULE]: Failed generating terrain map: ", e);
            }

            return null;
        }

        #endregion

        #region Rendering Methods

        private void CreateWater(WarpRenderer renderer)
        {
            float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;

            renderer.AddPlane("Water", 256f * 0.5f);
            renderer.Scene.sceneobject("Water").setPos(127.5f, waterHeight, 127.5f);

            renderer.AddMaterial("WaterColor", ConvertColor(WATER_COLOR));
			renderer.Scene.material("WaterColor").setReflectivity(0);  // match water color with standard map module thanks lkalif
            renderer.Scene.material("WaterColor").setTransparency((byte)((1f - WATER_COLOR.A) * 255f));
            renderer.SetObjectMaterial("Water", "WaterColor");
        }

        private void CreateTerrain(WarpRenderer renderer, bool textureTerrain)
        {
            ITerrainChannel terrain = m_scene.Heightmap;
            float[] heightmap = terrain.GetFloatsSerialised();

            warp_Object obj = new warp_Object(256 * 256, 255 * 255 * 2);

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    int v = y * 256 + x;
                    float height = heightmap[v];

                    warp_Vector pos = ConvertVector(new Vector3(x, y, height));
                    obj.addVertex(new warp_Vertex(pos, (float)x / 255f, (float)(255 - y) / 255f));
                }
            }

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    if (x < 255 && y < 255)
                    {
                        int v = y * 256 + x;

                        // Normal
                        Vector3 v1 = new Vector3(x, y, heightmap[y * 256 + x]);
                        Vector3 v2 = new Vector3(x + 1, y, heightmap[y * 256 + x + 1]);
                        Vector3 v3 = new Vector3(x, y + 1, heightmap[(y + 1) * 256 + x]);
                        warp_Vector norm = ConvertVector(SurfaceNormal(v1, v2, v3));
                        norm = norm.reverse();
                        obj.vertex(v).n = norm;

                        // Triangle 1
                        obj.addTriangle(
                            v,
                            v + 1,
                            v + 256);

                        // Triangle 2
                        obj.addTriangle(
                            v + 256 + 1,
                            v + 256,
                            v + 1);
                    }
                }
            }

            renderer.Scene.addObject("Terrain", obj);

            UUID[] textureIDs = new UUID[4];
            float[] startHeights = new float[4];
            float[] heightRanges = new float[4];

            RegionSettings regionInfo = m_scene.RegionInfo.RegionSettings;

            textureIDs[0] = regionInfo.TerrainTexture1;
            textureIDs[1] = regionInfo.TerrainTexture2;
            textureIDs[2] = regionInfo.TerrainTexture3;
            textureIDs[3] = regionInfo.TerrainTexture4;

            startHeights[0] = (float)regionInfo.Elevation1SW;
            startHeights[1] = (float)regionInfo.Elevation1NW;
            startHeights[2] = (float)regionInfo.Elevation1SE;
            startHeights[3] = (float)regionInfo.Elevation1NE;

            heightRanges[0] = (float)regionInfo.Elevation2SW;
            heightRanges[1] = (float)regionInfo.Elevation2NW;
            heightRanges[2] = (float)regionInfo.Elevation2SE;
            heightRanges[3] = (float)regionInfo.Elevation2NE;

            uint globalX, globalY;
            Utils.LongToUInts(m_scene.RegionInfo.RegionHandle, out globalX, out globalY);

            warp_Texture texture;

            using (
                Bitmap image
                    = TerrainSplat.Splat(
                        heightmap, textureIDs, startHeights, heightRanges,
                        new Vector3d(globalX, globalY, 0.0), m_scene.AssetService, textureTerrain))
            {
                texture = new warp_Texture(image);
            }

            warp_Material material = new warp_Material(texture);
            material.setReflectivity(50);
            renderer.Scene.addMaterial("TerrainColor", material);
			renderer.Scene.material("TerrainColor").setReflectivity(0); // reduces tile seams a bit thanks lkalif
            renderer.SetObjectMaterial("Terrain", "TerrainColor");
        }

        private void CreateAllPrims(WarpRenderer renderer, bool useTextures)
        {
            if (m_primMesher == null)
                return;

            m_scene.ForEachSOG(
                delegate(SceneObjectGroup group)
                {
                    CreatePrim(renderer, group.RootPart, useTextures);
                    foreach (SceneObjectPart child in group.Parts)
                        CreatePrim(renderer, child, useTextures);
                }
            );
        }

        private void CreatePrim(WarpRenderer renderer, SceneObjectPart prim,
                bool useTextures)
        {
            const float MIN_SIZE = 2f;

            if ((PCode)prim.Shape.PCode != PCode.Prim)
                return;
            if (prim.Scale.LengthSquared() < MIN_SIZE * MIN_SIZE)
                return;

            Primitive omvPrim = prim.Shape.ToOmvPrimitive(prim.OffsetPosition, prim.RotationOffset);
            FacetedMesh renderMesh = m_primMesher.GenerateFacetedMesh(omvPrim, DetailLevel.Medium);
            if (renderMesh == null)
                return;

            warp_Vector primPos = ConvertVector(prim.GetWorldPosition());
            warp_Quaternion primRot = ConvertQuaternion(prim.RotationOffset);

            warp_Matrix m = warp_Matrix.quaternionMatrix(primRot);

            if (prim.ParentID != 0)
            {
                SceneObjectGroup group = m_scene.SceneGraph.GetGroupByPrim(prim.LocalId);
                if (group != null)
                    m.transform(warp_Matrix.quaternionMatrix(ConvertQuaternion(group.RootPart.RotationOffset)));
            }

            warp_Vector primScale = ConvertVector(prim.Scale);

            string primID = prim.UUID.ToString();

            // Create the prim faces
            // TODO: Implement the useTextures flag behavior
            for (int i = 0; i < renderMesh.Faces.Count; i++)
            {
                Face face = renderMesh.Faces[i];
                string meshName = primID + "-Face-" + i.ToString();

                // Avoid adding duplicate meshes to the scene
                if (renderer.Scene.objectData.ContainsKey(meshName))
                {
                    continue;
                }

                warp_Object faceObj = new warp_Object(face.Vertices.Count, face.Indices.Count / 3);

                for (int j = 0; j < face.Vertices.Count; j++)
                {
                    Vertex v = face.Vertices[j];

                    warp_Vector pos = ConvertVector(v.Position);
                    warp_Vector norm = ConvertVector(v.Normal);
                    
                    if (prim.Shape.SculptTexture == UUID.Zero)
                        norm = norm.reverse();
                    warp_Vertex vert = new warp_Vertex(pos, norm, v.TexCoord.X, v.TexCoord.Y);

                    faceObj.addVertex(vert);
                }

                for (int j = 0; j < face.Indices.Count; j += 3)
                {
                    faceObj.addTriangle(
                        face.Indices[j + 0],
                        face.Indices[j + 1],
                        face.Indices[j + 2]);
                }

                Primitive.TextureEntryFace teFace = prim.Shape.Textures.GetFace((uint)i);
                Color4 faceColor = GetFaceColor(teFace);
                string materialName = GetOrCreateMaterial(renderer, faceColor);

                faceObj.transform(m);
                faceObj.setPos(primPos);
                faceObj.scaleSelf(primScale.x, primScale.y, primScale.z);

                renderer.Scene.addObject(meshName, faceObj);

                renderer.SetObjectMaterial(meshName, materialName);
            }
        }

        private Color4 GetFaceColor(Primitive.TextureEntryFace face)
        {
            Color4 color;

            if (face.TextureID == UUID.Zero)
                return face.RGBA;

            if (!m_colors.TryGetValue(face.TextureID, out color))
            {
                bool fetched = false;

                // Attempt to fetch the texture metadata
                UUID metadataID = UUID.Combine(face.TextureID, TEXTURE_METADATA_MAGIC);
                AssetBase metadata = m_scene.AssetService.GetCached(metadataID.ToString());
                if (metadata != null)
                {
                    OSDMap map = null;
                    try { map = OSDParser.Deserialize(metadata.Data) as OSDMap; } catch { }

                    if (map != null)
                    {
                        color = map["X-JPEG2000-RGBA"].AsColor4();
                        fetched = true;
                    }
                }

                if (!fetched)
                {
                    // Fetch the texture, decode and get the average color, 
                    // then save it to a temporary metadata asset
                    AssetBase textureAsset = m_scene.AssetService.Get(face.TextureID.ToString());
                    if (textureAsset != null)
                    {
                        int width, height;
                        color = GetAverageColor(textureAsset.FullID, textureAsset.Data, out width, out height);

                        OSDMap data = new OSDMap { { "X-JPEG2000-RGBA", OSD.FromColor4(color) } };
                        metadata = new AssetBase
                        {
                            Data = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(data)),
                            Description = "Metadata for JPEG2000 texture " + face.TextureID.ToString(),
                            Flags = AssetFlags.Collectable,
                            FullID = metadataID,
                            ID = metadataID.ToString(),
                            Local = true,
                            Temporary = true,
                            Name = String.Empty,
                            Type = (sbyte)AssetType.Unknown
                        };
                        m_scene.AssetService.Store(metadata);
                    }
                    else
                    {
                        color = new Color4(0.5f, 0.5f, 0.5f, 1.0f);
                    }
                }

                m_colors[face.TextureID] = color;
            }

            return color * face.RGBA;
        }

        private string GetOrCreateMaterial(WarpRenderer renderer, Color4 color)
        {
            string name = color.ToString();

            warp_Material material = renderer.Scene.material(name);
            if (material != null)
                return name;

            renderer.AddMaterial(name, ConvertColor(color));
            if (color.A < 1f)
                renderer.Scene.material(name).setTransparency((byte)((1f - color.A) * 255f));
            return name;
        }

        #endregion Rendering Methods

        #region Static Helpers

        private static warp_Vector ConvertVector(Vector3 vector)
        {
            return new warp_Vector(vector.X, vector.Z, vector.Y);
        }

        private static warp_Quaternion ConvertQuaternion(Quaternion quat)
        {
            return new warp_Quaternion(quat.X, quat.Z, quat.Y, -quat.W);
        }

        private static int ConvertColor(Color4 color)
        {
            int c = warp_Color.getColor((byte)(color.R * 255f), (byte)(color.G * 255f), (byte)(color.B * 255f));
            if (color.A < 1f)
                c |= (byte)(color.A * 255f) << 24;

            return c;
        }

        private static Vector3 SurfaceNormal(Vector3 c1, Vector3 c2, Vector3 c3)
        {
            Vector3 edge1 = new Vector3(c2.X - c1.X, c2.Y - c1.Y, c2.Z - c1.Z);
            Vector3 edge2 = new Vector3(c3.X - c1.X, c3.Y - c1.Y, c3.Z - c1.Z);

            Vector3 normal = Vector3.Cross(edge1, edge2);
            normal.Normalize();

            return normal;
        }

        public static Color4 GetAverageColor(UUID textureID, byte[] j2kData, out int width, out int height)
        {
            ulong r = 0;
            ulong g = 0;
            ulong b = 0;
            ulong a = 0;

            using (MemoryStream stream = new MemoryStream(j2kData))
            {
                try
                {
                    int pixelBytes;

                    using (Bitmap bitmap = (Bitmap)J2kImage.FromStream(stream))
                    {
                        width = bitmap.Width;
                        height = bitmap.Height;
    
                        BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                        pixelBytes = (bitmap.PixelFormat == PixelFormat.Format24bppRgb) ? 3 : 4;
    
                        // Sum up the individual channels
                        unsafe
                        {
                            if (pixelBytes == 4)
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
    
                                    for (int x = 0; x < width; x++)
                                    {
                                        b += row[x * pixelBytes + 0];
                                        g += row[x * pixelBytes + 1];
                                        r += row[x * pixelBytes + 2];
                                        a += row[x * pixelBytes + 3];
                                    }
                                }
                            }
                            else
                            {
                                for (int y = 0; y < height; y++)
                                {
                                    byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
    
                                    for (int x = 0; x < width; x++)
                                    {
                                        b += row[x * pixelBytes + 0];
                                        g += row[x * pixelBytes + 1];
                                        r += row[x * pixelBytes + 2];
                                    }
                                }
                            }
                        }
                    }

                    // Get the averages for each channel
                    const decimal OO_255 = 1m / 255m;
                    decimal totalPixels = (decimal)(width * height);

                    decimal rm = ((decimal)r / totalPixels) * OO_255;
                    decimal gm = ((decimal)g / totalPixels) * OO_255;
                    decimal bm = ((decimal)b / totalPixels) * OO_255;
                    decimal am = ((decimal)a / totalPixels) * OO_255;

                    if (pixelBytes == 3)
                        am = 1m;

                    return new Color4((float)rm, (float)gm, (float)bm, (float)am);
                }
                catch (Exception ex)
                {
                    m_log.WarnFormat(
                        "[WARP 3D IMAGE MODULE]: Error decoding JPEG2000 texture {0} ({1} bytes): {2}",
                        textureID, j2kData.Length, ex.Message);

                    width = 0;
                    height = 0;
                    return new Color4(0.5f, 0.5f, 0.5f, 1.0f);
                }
            }
        }

        #endregion Static Helpers
    }

    public static class ImageUtils
    {
        /// <summary>
        /// Performs bilinear interpolation between four values
        /// </summary>
        /// <param name="v00">First, or top left value</param>
        /// <param name="v01">Second, or top right value</param>
        /// <param name="v10">Third, or bottom left value</param>
        /// <param name="v11">Fourth, or bottom right value</param>
        /// <param name="xPercent">Interpolation value on the X axis, between 0.0 and 1.0</param>
        /// <param name="yPercent">Interpolation value on fht Y axis, between 0.0 and 1.0</param>
        /// <returns>The bilinearly interpolated result</returns>
        public static float Bilinear(float v00, float v01, float v10, float v11, float xPercent, float yPercent)
        {
            return Utils.Lerp(Utils.Lerp(v00, v01, xPercent), Utils.Lerp(v10, v11, xPercent), yPercent);
        }

        /// <summary>
        /// Performs a high quality image resize
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        /// <returns>Resized image</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            Bitmap result = new Bitmap(width, height);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, result.Width, result.Height);
            }

            return result;
        }
    }
}
