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
using Warp3D;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Services.Interfaces;

using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;
using OpenMetaverse.StructuredData;

using WarpRenderer = global::Warp3D.Warp3D;

namespace OpenSim.Region.CoreModules.World.Warp3DMap
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "Warp3DImageModule")]
    public class Warp3DImageModule : IMapImageGenerator, INonSharedRegionModule
    {
        private static readonly UUID TEXTURE_METADATA_MAGIC = new UUID("802dc0e0-f080-4931-8b57-d1be8611c4f3");
        private static readonly Color4 WATER_COLOR = new Color4(29, 72, 96, 216);

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

#pragma warning disable 414
        private static string LogHeader = "[WARP 3D IMAGE MODULE]";
#pragma warning restore 414

        private Scene m_scene;
        private IRendering m_primMesher;
        private Dictionary<UUID, Color4> m_colors = new Dictionary<UUID, Color4>();

        private IConfigSource m_config;
        private bool m_drawPrimVolume = true;   // true if should render the prims on the tile
        private bool m_textureTerrain = true;   // true if to create terrain splatting texture
        private bool m_textureAvegareTerrain = false; // replace terrain textures by their average color
        private bool m_texturePrims = true;     // true if should texture the rendered prims
        private float m_texturePrimSize = 48f;  // size of prim before we consider texturing it
        private bool m_renderMeshes = false;    // true if to render meshes rather than just bounding boxes

        private bool m_Enabled = false;

//        private Bitmap lastImage = null;
        private DateTime lastImageTime = DateTime.MinValue;

        #region Region Module interface

        public void Initialise(IConfigSource source)
        {
            m_config = source;

            string[] configSections = new string[] { "Map", "Startup" };

            if (Util.GetConfigVarFromSections<string>(
                m_config, "MapImageModule", configSections, "MapImageModule") != "Warp3DImageModule")
                return;

            m_Enabled = true;

            m_drawPrimVolume =
                Util.GetConfigVarFromSections<bool>(m_config, "DrawPrimOnMapTile", configSections, m_drawPrimVolume);
            m_textureTerrain =
                Util.GetConfigVarFromSections<bool>(m_config, "TextureOnMapTile", configSections, m_textureTerrain);
            m_textureAvegareTerrain =
                Util.GetConfigVarFromSections<bool>(m_config, "AverageTextureColorOnMapTile", configSections, m_textureAvegareTerrain);
            if(m_textureAvegareTerrain)
                m_textureTerrain = true;
            m_texturePrims =
                Util.GetConfigVarFromSections<bool>(m_config, "TexturePrims", configSections, m_texturePrims);
            m_texturePrimSize =
                Util.GetConfigVarFromSections<float>(m_config, "TexturePrimSize", configSections, m_texturePrimSize);
            m_renderMeshes =
                Util.GetConfigVarFromSections<bool>(m_config, "RenderMeshes", configSections, m_renderMeshes);

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

        private Vector3 cameraPos;
        private Vector3 cameraDir;
        private int viewWitdh = 256;
        private int viewHeigth = 256;
        private float fov;
        private bool orto;

        public Bitmap CreateMapTile()
        {
            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
            {
                m_primMesher = RenderingLoader.LoadRenderer(renderers[0]);
            }

            cameraPos = new Vector3(
                            (m_scene.RegionInfo.RegionSizeX) * 0.5f,
                            (m_scene.RegionInfo.RegionSizeY) * 0.5f,
                            4096f);

            cameraDir = -Vector3.UnitZ;
            viewWitdh = (int)m_scene.RegionInfo.RegionSizeX;
            viewHeigth = (int)m_scene.RegionInfo.RegionSizeY;
            orto = true;

//            fov = warp_Math.rad2deg(2f * (float)Math.Atan2(viewWitdh,4096f));
//            orto = false;

            Bitmap tile = GenMapTile();
            m_primMesher = null;
            return tile;
        }

        public Bitmap CreateViewImage(Vector3 camPos, Vector3 camDir, float pfov, int width, int height, bool useTextures)
        {
            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
            {
                m_primMesher = RenderingLoader.LoadRenderer(renderers[0]);
            }

            cameraPos = camPos;
            cameraDir = camDir;
            viewWitdh = width;
            viewHeigth = height;
            fov = pfov;
            orto = false;

            Bitmap tile = GenMapTile();
            m_primMesher = null;
            return tile;
        }

        private Bitmap GenMapTile()
        {
            m_colors.Clear();

            WarpRenderer renderer = new WarpRenderer();

            if(!renderer.CreateScene(viewWitdh, viewHeigth))
                return new Bitmap(viewWitdh, viewHeigth);

            #region Camera

            warp_Vector pos = ConvertVector(cameraPos);
            warp_Vector lookat = warp_Vector.add(pos, ConvertVector(cameraDir));

            if(orto)
                renderer.Scene.defaultCamera.setOrthographic(true, viewWitdh, viewHeigth);
            else
               renderer.Scene.defaultCamera.setFov(fov);
 
            renderer.Scene.defaultCamera.setPos(pos);
            renderer.Scene.defaultCamera.lookAt(lookat);
            #endregion Camera

            renderer.Scene.setAmbient(warp_Color.getColor(192,191,173));
            renderer.Scene.addLight("Light1", new warp_Light(new warp_Vector(0f, 1f, 8f), 0xffffff, 0, 320, 40));

            CreateWater(renderer);
            CreateTerrain(renderer);
            if (m_drawPrimVolume)
                CreateAllPrims(renderer);

            renderer.Render();
            Bitmap bitmap = renderer.Scene.getImage();

            renderer.Scene.destroy();
            renderer.Reset();
            renderer = null;

            m_colors.Clear();
            GC.Collect();
//            m_log.Debug("[WARP 3D IMAGE MODULE]: GC.Collect()");

            return bitmap;
        }

        public byte[] WriteJpeg2000Image()
        {
            try
            {
                using (Bitmap mapbmp = CreateMapTile())
                    return OpenJPEG.EncodeFromImage(mapbmp, false);
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

        // Add a water plane to the renderer.
        private void CreateWater(WarpRenderer renderer)
        {
            float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;

            renderer.AddPlane("Water", m_scene.RegionInfo.RegionSizeX * 0.5f);
            renderer.Scene.sceneobject("Water").setPos(m_scene.RegionInfo.RegionSizeX * 0.5f,
                                                       waterHeight,
                                                       m_scene.RegionInfo.RegionSizeY * 0.5f);

            warp_Material waterColorMaterial = new warp_Material(ConvertColor(WATER_COLOR));
            waterColorMaterial.setReflectivity(0);  // match water color with standard map module thanks lkalif
            waterColorMaterial.setTransparency((byte)((1f - WATER_COLOR.A) * 255f));
            renderer.Scene.addMaterial("WaterColor", waterColorMaterial);
            renderer.SetObjectMaterial("Water", "WaterColor");
        }

        // Add a terrain to the renderer.
        // Note that we create a 'low resolution' 257x257 vertex terrain rather than trying for
        //    full resolution. This saves a lot of memory especially for very large regions.
        private void CreateTerrain(WarpRenderer renderer)
        {
            ITerrainChannel terrain = m_scene.Heightmap;

            float regionsx = m_scene.RegionInfo.RegionSizeX;
            float regionsy = m_scene.RegionInfo.RegionSizeY;

            // 'diff' is the difference in scale between the real region size and the size of terrain we're buiding
            float diff = regionsx / 256f;

            int npointsx = (int)(regionsx / diff);
            int npointsy = (int)(regionsy / diff);

            float invsx = 1.0f / (npointsx * diff);
            float invsy = 1.0f / (npointsy * diff);

            npointsx++;
            npointsy++;

            // Create all the vertices for the terrain
            // for texture fliped on Y
            warp_Object obj = new warp_Object();
            warp_Vector pos;
            float x, y;
            float tv;
            for (y = 0; y < regionsy; y += diff)
            {
                tv = y * invsy;
                for (x = 0; x < regionsx; x += diff)
                {
                    pos = ConvertVector(x , y , (float)terrain[(int)x, (int)y]);
                    obj.addVertex(new warp_Vertex(pos, x *  invsx, tv ));
                }
                pos = ConvertVector(x , y , (float)terrain[(int)(x - diff), (int)y]);
                obj.addVertex(new warp_Vertex(pos, 1.0f, tv));
            }

            int lastY = (int)(y - diff);
            for (x = 0; x < regionsx; x += diff)
            {
                pos = ConvertVector(x , y , (float)terrain[(int)x, lastY]);
                obj.addVertex(new warp_Vertex(pos, x *  invsx, 1.0f));
            }
            pos = ConvertVector(x , y , (float)terrain[(int)(x - diff), lastY]);
            obj.addVertex(new warp_Vertex(pos, 1.0f, 1.0f));

            // Now that we have all the vertices, make another pass and
            // create the list of triangle indices.
            int limx = npointsx - 1;
            int limy = npointsy - 1;
            for (int j = 0; j < limy; j++)
            {
                for (int i = 0; i < limx; i++)
                {
                    int v = j * npointsx + i;

                    // Make two triangles for each of the squares in the grid of vertices
                    obj.addTriangle(
                        v,
                        v + 1,
                        v + npointsx);

                    obj.addTriangle(
                        v + npointsx + 1,
                        v + npointsx,
                        v + 1);
                }
            }

            renderer.Scene.addObject("Terrain", obj);

            UUID[] textureIDs = new UUID[4];
            float[] startHeights = new float[4];
            float[] heightRanges = new float[4];

            OpenSim.Framework.RegionSettings regionInfo = m_scene.RegionInfo.RegionSettings;

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
            Util.RegionHandleToWorldLoc(m_scene.RegionInfo.RegionHandle, out globalX, out globalY);

            warp_Texture texture;
            // get texture fliped on Y
            using (Bitmap image = TerrainSplat.Splat(
                        terrain, textureIDs, startHeights, heightRanges,
                        m_scene.RegionInfo.WorldLocX, m_scene.RegionInfo.WorldLocY,
                        m_scene.AssetService, m_textureTerrain, m_textureAvegareTerrain, true))
                texture = new warp_Texture(image);

            warp_Material material = new warp_Material(texture);
            material.setColor(warp_Color.getColor(255,255,255));
            renderer.Scene.addMaterial("TerrainColor", material);
            renderer.SetObjectMaterial("Terrain", "TerrainColor");
        }

        private void CreateAllPrims(WarpRenderer renderer)
        {
            if (m_primMesher == null)
                return;

            m_scene.ForEachSOG(
                delegate(SceneObjectGroup group)
                {
                    foreach (SceneObjectPart child in group.Parts)
                        CreatePrim(renderer, child);
                }
            );
        }

        private void CreatePrim(WarpRenderer renderer, SceneObjectPart prim)
        {
            const float MIN_SIZE_SQUARE = 4f;

            if ((PCode)prim.Shape.PCode != PCode.Prim)
                return;
            float primScaleLenSquared = prim.Scale.LengthSquared();

            if (primScaleLenSquared < MIN_SIZE_SQUARE)
                return;

            FacetedMesh renderMesh = null;
            Primitive omvPrim = prim.Shape.ToOmvPrimitive(prim.OffsetPosition, prim.RotationOffset);

            if (m_renderMeshes)
            {
                if (omvPrim.Sculpt != null && omvPrim.Sculpt.SculptTexture != UUID.Zero)
                {
                    // Try fetchinng the asset
                    byte[] sculptAsset = m_scene.AssetService.GetData(omvPrim.Sculpt.SculptTexture.ToString());
                    if (sculptAsset != null)
                    {
                        // Is it a mesh?
                        if (omvPrim.Sculpt.Type == SculptType.Mesh)
                        {
                            AssetMesh meshAsset = new AssetMesh(omvPrim.Sculpt.SculptTexture, sculptAsset);
                            FacetedMesh.TryDecodeFromAsset(omvPrim, meshAsset, DetailLevel.Highest, out renderMesh);
                            meshAsset = null;
                        }
                        else // It's sculptie
                        {
                            IJ2KDecoder imgDecoder = m_scene.RequestModuleInterface<IJ2KDecoder>();
                            if(imgDecoder != null)
                            {
                                Image sculpt = imgDecoder.DecodeToImage(sculptAsset);
                                if(sculpt != null)
                                {
                                    renderMesh = m_primMesher.GenerateFacetedSculptMesh(omvPrim,(Bitmap)sculpt, DetailLevel.High);
                                    sculpt.Dispose();
                                }
                            }
                        }
                    }
                }
            }

            // If not a mesh or sculptie, try the regular mesher
            if (renderMesh == null)
            {
                renderMesh = m_primMesher.GenerateFacetedMesh(omvPrim, DetailLevel.High);
            }

            if (renderMesh == null)
                return;

            string primID = prim.UUID.ToString();

            warp_Vector primPos = ConvertVector(prim.GetWorldPosition());
            warp_Quaternion primRot = ConvertQuaternion(prim.GetWorldRotation());
            warp_Matrix m = warp_Matrix.quaternionMatrix(primRot);

            // Create the prim faces
            // TODO: Implement the useTextures flag behavior
            for (int i = 0; i < renderMesh.Faces.Count; i++)
            {
                Face face = renderMesh.Faces[i];
                string meshName = primID + i.ToString();

                // Avoid adding duplicate meshes to the scene
                if (renderer.Scene.objectData.ContainsKey(meshName))
                    continue;

                warp_Object faceObj = new warp_Object();
                for (int j = 0; j < face.Vertices.Count; j++)
                {
                    Vertex v = face.Vertices[j];
                    warp_Vector pos = ConvertVector(v.Position);
                    warp_Vertex vert = new warp_Vertex(pos, v.TexCoord.X, 1.0f - v.TexCoord.Y);
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
                Color4 faceColor = teFace.RGBA;
                string materialName = String.Empty;

                if (m_texturePrims && primScaleLenSquared > m_texturePrimSize*m_texturePrimSize)
                    materialName = GetOrCreateMaterial(renderer, faceColor, teFace.TextureID);
                else
                    materialName = GetOrCreateMaterial(renderer,  GetFaceColor(teFace));

                faceObj.scaleSelf(prim.Scale.X, prim.Scale.Z, prim.Scale.Y);
                faceObj.transform(m);
                faceObj.setPos(primPos);

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

        public string GetOrCreateMaterial(WarpRenderer renderer, Color4 faceColor, UUID textureID)
        {
            string materialName = "Color-" + faceColor.ToString() + "-Texture-" + textureID.ToString();

            if (renderer.Scene.material(materialName) == null)
            {
                renderer.AddMaterial(materialName, ConvertColor(faceColor));
                if (faceColor.A < 1f)
                {
                    renderer.Scene.material(materialName).setTransparency((byte) ((1f - faceColor.A)*255f));
                }
                warp_Texture texture = GetTexture(textureID);
                if (texture != null)
                    renderer.Scene.material(materialName).setTexture(texture);
            }

            return materialName;
        }

        private warp_Texture GetTexture(UUID id)
        {
            warp_Texture ret = null;

            byte[] asset = m_scene.AssetService.GetData(id.ToString());

            if (asset != null)
            {
                IJ2KDecoder imgDecoder = m_scene.RequestModuleInterface<IJ2KDecoder>();

                try
                {
                    using (Bitmap img = (Bitmap)imgDecoder.DecodeToImage(asset))
                        ret = new warp_Texture(img);
                }
                catch (Exception e)
                {
                    m_log.Warn(string.Format("[WARP 3D IMAGE MODULE]: Failed to decode asset {0}, exception  ", id), e);
                }
            }

            return ret;
        }

        #endregion Rendering Methods

        #region Static Helpers
        // Note: axis change.
        private static warp_Vector ConvertVector(float x, float y, float z)
        {
            return new warp_Vector(x, z, y);
        }

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
    }
}
