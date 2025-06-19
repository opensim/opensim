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
//#define SPAM

using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.PhysicsModules.ConvexDecompositionDotNet;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System.Drawing;
using System.Threading;
using System.IO.Compression;
using PrimMesher;
using log4net;
using Nini.Config;
using System.Reflection;
using System.IO;

using Mono.Addins;
using System.Buffers;

namespace OpenSim.Region.PhysicsModule.ubODEMeshing
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ubODEMeshmerizer")]
    public class ubMeshmerizer : IMesher, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Setting baseDir to a path will enable the dumping of raw files
        // raw files can be imported by blender so a visual inspection of the results can be done

        const float floatPI = MathF.PI;
        private readonly static string cacheControlFilename = "cntr";
        private bool m_Enabled = false;

        public static object diskLock = new();

        public bool doMeshFileCache = true;
        public bool doCacheExpire = true;
        public string cachePath = "MeshCache";
        public TimeSpan CacheExpire;

        //private const string baseDir = "rawFiles";
        private const string baseDir = null; //"rawFiles";

        private bool useMeshiesPhysicsMesh = true;
        private bool doConvexPrims = true;
        private bool doConvexSculpts = true;

        private readonly Dictionary<AMeshKey, Mesh> m_uniqueMeshes = new();
        private readonly Dictionary<AMeshKey, Mesh> m_uniqueReleasedMeshes = new ();

       #region INonSharedRegionModule
        public string Name
        {
            get { return "ubODEMeshmerizer"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource config)
        {
            IConfig start_config = config.Configs["Startup"];

            string mesher = start_config.GetString("meshing", string.Empty);
            if (mesher == Name)
            {
                float fcache = 48.0f;
                //            float fcache = 0.02f;

                IConfig mesh_config = config.Configs["Mesh"];
                if (mesh_config != null)
                {
                    useMeshiesPhysicsMesh = mesh_config.GetBoolean("UseMeshiesPhysicsMesh", useMeshiesPhysicsMesh);
                    doConvexPrims = mesh_config.GetBoolean("ConvexPrims",doConvexPrims);
                    doConvexSculpts = mesh_config.GetBoolean("ConvexSculpts",doConvexPrims);
                    doMeshFileCache = mesh_config.GetBoolean("MeshFileCache", doMeshFileCache);
                    cachePath = mesh_config.GetString("MeshFileCachePath", cachePath);
                    fcache = mesh_config.GetFloat("MeshFileCacheExpireHours", fcache);
                    doCacheExpire = mesh_config.GetBoolean("MeshFileCacheDoExpire", doCacheExpire);

                    m_Enabled = true;
                }

                CacheExpire = TimeSpan.FromHours(fcache);

                if(String.IsNullOrEmpty(cachePath))
                    doMeshFileCache = false;

                if(doMeshFileCache)
                {
                    if(!checkCache())
                    {
                        doMeshFileCache = false;
                        doCacheExpire = false;
                    }
                }
                else
                    doCacheExpire = false;
            }
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IMesher>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.UnregisterModuleInterface<IMesher>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        #endregion

        private void ReportPrimError(string message, string primName, PrimMesh primMesh)
        {
            m_log.Error(message);
            m_log.Error("\nPrim Name: " + primName);
            m_log.Error("****** PrimMesh Parameters ******\n" + primMesh.ParamsToDisplayString());
        }

        /// <summary>
        /// Add a submesh to an existing list of coords and faces.
        /// </summary>
        /// <param name="subMeshData"></param>
        /// <param name="size">Size of entire object</param>
        /// <param name="coords"></param>
        /// <param name="faces"></param>
        private unsafe void  AddSubMesh(OSDMap subMeshData, List<Vector3> coords, List<Face> faces)
        {
            // Console.WriteLine("subMeshMap for {0} - {1}", primName, Util.GetFormattedXml((OSD)subMeshMap));

            // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
            // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
            // geometry for this submesh.
            if (subMeshData.ContainsKey("NoGeometry"))
                return;

            byte[] posBytes = subMeshData["Position"].AsBinary();
            if (posBytes == null || posBytes.Length == 0)
                return;
            byte[] triangleBytes = subMeshData["TriangleList"].AsBinary();
            if (triangleBytes == null || triangleBytes.Length == 0)
                return;

            const float invMaxU16 = 1.0f / 65535f;
            Vector3 posRange;
            Vector3 posMin;
            if(subMeshData.TryGetValue("PositionDomain", out OSD tmp))
            {
                posRange = ((OSDMap)tmp)["Max"].AsVector3();
                posMin = ((OSDMap)tmp)["Min"].AsVector3();
                posRange -= posMin;
                posRange *= invMaxU16;
            }
            else
            {
                posRange = new Vector3(invMaxU16, invMaxU16, invMaxU16);
                posMin = new Vector3(-0.5f, -0.5f, -0.5f);
            }

            int faceIndexOffset = coords.Count;

            fixed (byte* ptrstart = posBytes)
            { 
                byte* end = ptrstart + posBytes.Length;
                byte* ptr = ptrstart;
                while (ptr < end)
                {
                    ushort uX = Utils.BytesToUInt16(ptr);
                    ptr += 2;
                    ushort uY = Utils.BytesToUInt16(ptr);
                    ptr += 2;
                    ushort uZ = Utils.BytesToUInt16(ptr);
                    ptr += 2;

                    coords.Add(new Vector3(
                            uX * posRange.X + posMin.X,
                            uY * posRange.Y + posMin.Y,
                            uZ * posRange.Z + posMin.Z)
                        );
                }
            }

            fixed (byte* ptrstart = triangleBytes)
            {
                byte* end = ptrstart + triangleBytes.Length;
                byte* ptr = ptrstart;
                while (ptr < end)
                {
                    int v1 = Utils.BytesToUInt16(ptr) + faceIndexOffset;
                    ptr += 2;
                    int v2 = Utils.BytesToUInt16(ptr) + faceIndexOffset;
                    ptr += 2;
                    int v3 = Utils.BytesToUInt16(ptr) + faceIndexOffset;
                    ptr += 2;
                    Face f = new (v1, v2, v3);
                    faces.Add(f);
                }
            }
        }

        /// <summary>
        /// Create a physics mesh from data that comes with the prim.  The actual data used depends on the prim type.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="lod"></param>
        /// <returns></returns>
        private Mesh CreateMeshFromPrimMesher(string primName, PrimitiveBaseShape primShape, float lod, bool convex)
        {
//            m_log.DebugFormat(
//                "[MESH]: Creating physics proxy for {0}, shape {1}",
//                primName, (OpenMetaverse.SculptType)primShape.SculptType);

            List<Vector3> coords;
            List<Face> faces;
            bool needsConvexProcessing = convex;

            if (primShape.SculptEntry)
            {
                if (((SculptType)primShape.SculptType) == SculptType.Mesh)
                {
                    if (!useMeshiesPhysicsMesh)
                        return null;
                    try
                    {
                        if (!GenerateCoordsAndFacesFromPrimMeshData(primName, primShape, out coords, out faces, convex))
                            return null;
                        needsConvexProcessing = false;
                    }
                    catch
                    {
                        m_log.ErrorFormat("[MESH]: fail to process mesh asset for prim {0}", primName);
                        return null;
                    }
                }
                else
                {
                    try
                    {
                        if (!GenerateCoordsAndFacesFromPrimSculptData(primName, primShape, lod, out coords, out faces))
                            return null;
                        needsConvexProcessing &= doConvexSculpts;
                    }
                    catch
                    {
                        m_log.ErrorFormat("[MESH]: fail to process sculpt map for prim {0}", primName);
                        return null;
                    }
                }
            }
            else
            {
                try
                {
                    if (!GenerateCoordsAndFacesFromPrimShapeData(primName, primShape, lod, convex, out coords, out faces))
                        return null;
                     needsConvexProcessing &= doConvexPrims;
                }
                catch
                {
                    m_log.ErrorFormat("[MESH]: fail to process shape parameters for prim {0}", primName);
                    return null;
                }
            }

            int numCoords = coords.Count;
            int numFaces = faces.Count;

            if(numCoords < 3 || (!needsConvexProcessing && numFaces < 1))
            {
                m_log.ErrorFormat("[ubODEMesh]: invalid degenerated mesh for prim {0} ignored", primName);
                return null;
            }

            if(needsConvexProcessing)
            {
                 if(CreateBoundingHull(coords, out List<Vector3> convexcoords, out List<Face> convexfaces) && convexcoords != null && convexfaces != null)
                {
                    coords.Clear();
                    coords = convexcoords;
 
                    faces.Clear();
                    faces = convexfaces;
                    numFaces = faces.Count;
                }
                else
                     m_log.ErrorFormat("[ubMESH]: failed to create convex for {0} using normal mesh", primName);
            }

            Mesh mesh = new(true);
            // Add the corresponding triangles to the mesh
            for (int i = 0; i < numFaces; i++)
            {
                Face f = faces[i];
                mesh.Add(new Triangle(coords[f.v1].X, coords[f.v1].Y, coords[f.v1].Z,
                                      coords[f.v2].X, coords[f.v2].Y, coords[f.v2].Z,
                                      coords[f.v3].X, coords[f.v3].Y, coords[f.v3].Z));
            }

            coords.Clear();
            faces.Clear();

            if(mesh.numberVertices() < 3 || mesh.numberTriangles() < 1)
            {
                m_log.ErrorFormat("[ubODEMesh]: invalid degenerated mesh for prim {0} ignored", primName);
                return null;
            }

            primShape.SculptData = Utils.EmptyBytes;

            return mesh;
        }

        /// <summary>
        /// Generate the co-ords and faces necessary to construct a mesh from the mesh data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="coords">Coords are added to this list by the method.</param>
        /// <param name="faces">Faces are added to this list by the method.</param>
        /// <returns>true if coords and faces were successfully generated, false if not</returns>
        private unsafe bool GenerateCoordsAndFacesFromPrimMeshData(
            string primName, PrimitiveBaseShape primShape, out List<Vector3> coords, out List<Face> faces, bool convex)
        {
//            m_log.DebugFormat("[MESH]: experimental mesh proxy generation for {0}", primName);


            // for ubOde we have a diferent mesh use priority
            // priority is to use full mesh then decomposition
            // SL does the oposite
            bool usemesh = false;

            coords = [];
            faces = [];

            if (primShape.SculptData == null || primShape.SculptData.Length <= 0)
            {
//                m_log.InfoFormat("[MESH]: asset data for {0} is zero length", primName);
                return false;
            }
            OSD osd;
            long start = 0;
            using (MemoryStream data = new(primShape.SculptData))
            {
                try
                {
                    osd = OSDParser.DeserializeLLSDBinary(data);
                }
                catch (Exception e)
                {
                    m_log.Error($"[MESH]: Error deserializing mesh asset header: {e.Message} in Prim '{primName}' asset {primShape.SculptTexture}");
                    return false;
                }

                start = data.Position;
            }

            if (osd is not OSDMap map)
            {
                m_log.Warn($"[Mesh]: unable to cast mesh asset to OSDMap prim: {primName} asset {primShape.SculptTexture}");
                return false;
            }

            OSDMap physicsParms = null;

            if (!convex)
            {
                if (map.TryGetValue("physics_shape", out OSD pso))
                    physicsParms = (OSDMap)pso; // old asset format
                else if (map.TryGetValue("physics_mesh", out OSD pm))
                    physicsParms = (OSDMap)pm; // new asset format

                if (physicsParms != null)
                    usemesh = true;
            }

            if(!usemesh && map.TryGetValue("physics_convex",out OSD pc))
                    physicsParms = (OSDMap)pc;

            if (physicsParms == null)
            {
                //m_log.WarnFormat("[MESH]: unknown mesh type for prim {0}",primName);
                return false;
            }

            int physOffset = physicsParms["offset"].AsInteger() + (int)start;
            int physSize = physicsParms["size"].AsInteger();

            if (physOffset < 0 || physSize == 0)
                return false; // no mesh data in asset

            OSD decodedMeshOsd = new();
            try
            {
                using (MemoryStream outMs = new(4 * physSize))
                {
                    using (MemoryStream inMs = new(primShape.SculptData, physOffset + 2 , physSize - 2)) // skip first 2 bytes in header
                    {
                        using DeflateStream decompressionStream = new(inMs, CompressionMode.Decompress);
                        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(8192);
                        int readLen = 0;

                        while ((readLen = decompressionStream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                            outMs.Write(readBuffer, 0, readLen);
                        ArrayPool<byte>.Shared.Return(readBuffer);
                    }
                    outMs.Seek(0, SeekOrigin.Begin);
                    decodedMeshOsd = OSDParser.DeserializeLLSDBinary(outMs);
                }
            }
            catch (Exception e)
            {
                m_log.Error("[MESH]: exception decoding physical mesh prim " + primName +" : " + e.ToString());
                return false;
            }

            if (usemesh)
            {
                // physics_shape is an array of OSDMaps, one for each submesh
                if (decodedMeshOsd is OSDArray decodedMeshOsdArray)
                {
                    //Console.WriteLine("decodedMeshOsd for {0} - {1}", primName, Util.GetFormattedXml(decodedMeshOsd));
                    foreach (OSD subMeshOsd in decodedMeshOsdArray)
                    {
                        if (subMeshOsd is OSDMap subm)
                            AddSubMesh(subm, coords, faces);
                    }
                }
                return true;
            }
            else if(decodedMeshOsd is OSDMap cmap)
            {
                byte[] data;

                List<float3> vs = [];
                PHullResult hullr = new();
                float3 f3;

                const float invMaxU16 = 1.0f / ushort.MaxValue;
                int t1;
                int t2;
                int t3;
                int i;
                int nverts;
                int nindexs;

                if (!cmap.TryGetVector3("Max", out Vector3 range))
                    range = new Vector3(0.5f, 0.5f, 0.5f);

                if (!cmap.TryGetVector3("Min", out Vector3 min))
                    min = new Vector3(-0.5f, -0.5f, -0.5f);

                range -= min;
                range *= invMaxU16;

                if(!convex)
                {
                    // if mesh data not present and not convex then we need convex decomposition data
                    if (cmap.ContainsKey("HullList") && cmap.ContainsKey("Positions"))
                    {
                        List<int> hsizes = [];
                        int totalpoints = 0;
                        data = cmap["HullList"].AsBinary();
                        for (i = 0; i < data.Length; i++)
                        {
                            t1 = data[i];
                            if (t1 == 0)
                                t1 = 256;
                            totalpoints += t1;
                            hsizes.Add(t1);
                        }

                        data = cmap["Positions"].AsBinary();
                        fixed(byte* ptrstart = data)
                        {
                            byte* ptr = ptrstart;

                            int vertsoffset = 0;

                            if (totalpoints == data.Length / 6) // 2 bytes per coord, 3 coords per point
                            {
                                foreach (int hullsize in hsizes)
                                {
                                    if (hullsize < 4)
                                    {
                                        if (hullsize < 3)
                                        {
                                            ptr += 6 * hullsize;
                                            continue;
                                        }

                                        for (i = 0; i < hullsize; i++)
                                        {
                                            t1 = Utils.BytesToUInt16(ptr); ptr += 2;
                                            t2 = Utils.BytesToUInt16(ptr); ptr += 2;
                                            t3 = Utils.BytesToUInt16(ptr); ptr += 2;

                                            coords.Add(new Vector3(
                                                            t1 * range.X + min.X,
                                                            t2 * range.Y + min.Y,
                                                            t3 * range.Z + min.Z)
                                                        );
                                        }

                                        faces.Add(new Face(vertsoffset, vertsoffset + 1, vertsoffset + 2));

                                        vertsoffset += hullsize;
                                        continue;
                                    }

                                    for (i = 0; i < hullsize; i++)
                                    {
                                        t1 = Utils.BytesToUInt16(ptr); ptr += 2;
                                        t2 = Utils.BytesToUInt16(ptr); ptr += 2;
                                        t3 = Utils.BytesToUInt16(ptr); ptr += 2;

                                        f3 = new float3(t1 * range.X + min.X,
                                                        t2 * range.Y + min.Y,
                                                        t3 * range.Z + min.Z);
                                        vs.Add(f3);
                                    }

                                    if (!HullUtils.ComputeHull(vs, out List<int> indices))
                                    {
                                        vs.Clear();
                                        continue;
                                    }

                                    nverts = vs.Count;
                                    nindexs = indices.Count;

                                    if (nindexs % 3 != 0)
                                    {
                                        vs.Clear();
                                        continue;
                                    }

                                    for (i = 0; i < vs.Count; i++)
                                        coords.Add(new Vector3(vs[i].x, vs[i].y, vs[i].z));

                                    for (i = 0; i < indices.Count; i += 3)
                                    {
                                        t1 = indices[i];
                                        if (t1 > nverts)
                                            break;
                                        t2 = indices[i + 1];
                                        if (t2 > nverts)
                                            break;
                                        t3 = indices[i + 2];
                                        if (t3 > nverts)
                                            break;
                                        faces.Add(new Face(vertsoffset + t1, vertsoffset + t2, vertsoffset + t3));
                                    }
                                    vertsoffset += nverts;
                                    vs.Clear();
                                }
                            }
                        }
                        if (coords.Count > 0 && faces.Count > 0)
                            return true;
                    }
                    else
                    {
                        // if neither mesh or decomposition present, warn and use convex
                        //m_log.WarnFormat("[MESH]: Data for PRIM shape type ( mesh or decomposition) not found for prim {0}",primName);
                    }
                }
                vs.Clear();

                if (cmap.TryGetValue("BoundingVerts", out OSD odata))
                {
                    data = odata.AsBinary();
                    if (data.Length < 3 * 6)
                    {
                        vs.Clear();
                        return false;
                    }

                    fixed (byte* ptrstart = data)
                    {
                        byte* end = ptrstart + data.Length;
                        byte* ptr = ptrstart;
                        while(ptr < end)
                        {
                            t1 = Utils.BytesToUInt16(ptr); ptr += 2;
                            t2 = Utils.BytesToUInt16(ptr); ptr += 2;
                            t3 = Utils.BytesToUInt16(ptr); ptr += 2;

                            f3 = new float3((t1 * range.X + min.X),
                                        (t2 * range.Y + min.Y),
                                        (t3 * range.Z + min.Z));
                            vs.Add(f3);
                        }
                    }

                    nverts = vs.Count;

                    if (nverts < 4)
                    {
                        for (i = 0; i < vs.Count; i++)
                            coords.Add(new Vector3(vs[i].x, vs[i].y, vs[i].z));

                        faces.Add(new Face(0, 1, 2));

                        vs.Clear();
                        return true;
                    }

                    if (!HullUtils.ComputeHull(vs, out List<int> indices))
                        return false;

                    nindexs = indices.Count;

                    if (nindexs % 3 != 0)
                        return false;

                    for (i = 0; i < vs.Count; i++)
                        coords.Add(new Vector3(vs[i].x, vs[i].y, vs[i].z));

                    for (i = 0; i < indices.Count; i += 3)
                    {
                        t1 = indices[i];
                        if (t1 > nverts)
                            break;
                        t2 = indices[i + 1];
                        if (t2 > nverts)
                            break;
                        t3 = indices[i + 2];
                        if (t3 > nverts)
                            break;

                        faces.Add(new Face(t1, t2, t3));
                    }
                    vs.Clear();
                    if (coords.Count > 0 && faces.Count > 0)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Generate the co-ords and faces necessary to construct a mesh from the sculpt data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="lod"></param>
        /// <param name="coords">Coords are added to this list by the method.</param>
        /// <param name="faces">Faces are added to this list by the method.</param>
        /// <returns>true if coords and faces were successfully generated, false if not</returns>
        private static bool GenerateCoordsAndFacesFromPrimSculptData(
            string primName, PrimitiveBaseShape primShape, float lod, out List<Vector3> coords, out List<Face> faces)
        {
            coords = new List<Vector3>();
            faces = new List<Face>();
            PrimMesher.SculptMesh sculptMesh;
            Image idata;

            if (primShape.SculptData == null || primShape.SculptData.Length == 0)
                return false;

            try
            {
                OpenMetaverse.Imaging.OpenJPEG.DecodeToImage(primShape.SculptData, out OpenMetaverse.Imaging.ManagedImage unusedData, out idata);

                unusedData = null;

                if (idata == null)
                {
                    // In some cases it seems that the decode can return a null bitmap without throwing
                    // an exception
                    m_log.WarnFormat("[PHYSICS]: OpenJPEG decoded sculpt data for {0} to a null bitmap.  Ignoring.", primName);
                    return false;
                }
            }
            catch (DllNotFoundException e)
            {
                m_log.Error($"[PHYSICS]: OpenJpeg problem: {e.Message}");
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                m_log.Error("[PHYSICS]: OpenJpeg was unable to decode this. Physics Proxy generation failed");
                return false;
            }
            catch (Exception ex)
            {
                m_log.Error("[PHYSICS]: Unable to generate a Sculpty physics proxy. Sculpty texture decode failed: " + ex.Message);
                return false;
            }
            // remove mirror and invert bits
            OpenMetaverse.SculptType pbsSculptType = ((OpenMetaverse.SculptType)(primShape.SculptType & 0x3f));
            var sculptType = pbsSculptType switch
            {
                OpenMetaverse.SculptType.Cylinder => PrimMesher.SculptMesh.SculptType.cylinder,
                OpenMetaverse.SculptType.Plane => PrimMesher.SculptMesh.SculptType.plane,
                OpenMetaverse.SculptType.Torus => PrimMesher.SculptMesh.SculptType.torus,
                OpenMetaverse.SculptType.Sphere => PrimMesher.SculptMesh.SculptType.sphere,
                _ => PrimMesher.SculptMesh.SculptType.plane,
            };
            bool mirror = ((primShape.SculptType & 128) != 0);
            bool invert = ((primShape.SculptType & 64) != 0);

            sculptMesh = new PrimMesher.SculptMesh((Bitmap)idata, sculptType, (int)lod, mirror, invert);

            idata.Dispose();

//            sculptMesh.DumpRaw(baseDir, primName, "primMesh");

            coords = sculptMesh.coords;
            faces = sculptMesh.faces;

            return true;
        }

        /// <summary>
        /// Generate the co-ords and faces necessary to construct a mesh from the shape data the accompanies a prim.
        /// </summary>
        /// <param name="primName"></param>
        /// <param name="primShape"></param>
        /// <param name="size"></param>
        /// <param name="coords">Coords are added to this list by the method.</param>
        /// <param name="faces">Faces are added to this list by the method.</param>
        /// <returns>true if coords and faces were successfully generated, false if not</returns>
        private bool GenerateCoordsAndFacesFromPrimShapeData(
                string primName, PrimitiveBaseShape primShape, float lod, bool convex,
                out List<Vector3> coords, out List<Face> faces)
        {
            PrimMesh primMesh;
            coords = new List<Vector3>();
            faces = new List<Face>();

            float pathShearX = primShape.PathShearX < 128 ? (float)primShape.PathShearX * 0.01f : (float)(primShape.PathShearX - 256) * 0.01f;
            float pathShearY = primShape.PathShearY < 128 ? (float)primShape.PathShearY * 0.01f : (float)(primShape.PathShearY - 256) * 0.01f;
            float pathBegin = (float)primShape.PathBegin * 2.0e-5f;
            float pathEnd = 1.0f - (float)primShape.PathEnd * 2.0e-5f;
            float pathScaleX = (float)(primShape.PathScaleX - 100) * 0.01f;
            float pathScaleY = (float)(primShape.PathScaleY - 100) * 0.01f;

            float profileBegin = (float)primShape.ProfileBegin * 2.0e-5f;
            float profileEnd = 1.0f - (float)primShape.ProfileEnd * 2.0e-5f;

            if (profileBegin < 0.0f)
                profileBegin = 0.0f;

            if (profileEnd < 0.02f)
                profileEnd = 0.02f;
            else if (profileEnd > 1.0f)
                profileEnd = 1.0f;

            if (profileBegin >= profileEnd)
                profileBegin = profileEnd - 0.02f;

            float profileHollow = (float)primShape.ProfileHollow * 2.0e-5f;
            if(convex)
                profileHollow = 0.0f;
            else if (profileHollow > 0.95f)
                profileHollow = 0.95f;

            int sides = 4;
            LevelOfDetail iLOD = (LevelOfDetail)lod;
            byte profshape = (byte)(primShape.ProfileCurve & 0x07);

            if (profshape == (byte)ProfileShape.EquilateralTriangle
                || profshape == (byte)ProfileShape.IsometricTriangle
                || profshape == (byte)ProfileShape.RightTriangle)
                sides = 3;
            else if (profshape == (byte)ProfileShape.Circle)
            {
                sides = iLOD switch
                {
                    LevelOfDetail.High => 24,
                    LevelOfDetail.Medium => 12,
                    LevelOfDetail.Low => 6,
                    LevelOfDetail.VeryLow => 3,
                    _ => 24,
                };
            }
            else if (profshape == (byte)ProfileShape.HalfCircle)
            { // half circle, prim is a sphere
                sides = iLOD switch
                {
                    LevelOfDetail.High => 24,
                    LevelOfDetail.Medium => 12,
                    LevelOfDetail.Low => 6,
                    LevelOfDetail.VeryLow => 3,
                    _ => 24,
                };
                profileBegin = 0.5f * profileBegin + 0.5f;
                profileEnd = 0.5f * profileEnd + 0.5f;
            }

            int hollowSides = sides;
            if (primShape.HollowShape == HollowShape.Circle)
            {
                hollowSides = iLOD switch
                {
                    LevelOfDetail.High => 24,
                    LevelOfDetail.Medium => 12,
                    LevelOfDetail.Low => 6,
                    LevelOfDetail.VeryLow => 3,
                    _ => 24,
                };
            }
            else if (primShape.HollowShape == HollowShape.Square)
                hollowSides = 4;
            else if (primShape.HollowShape == HollowShape.Triangle)
            {
                if (profshape == (byte)ProfileShape.HalfCircle)
                    hollowSides = 6;
                else
                    hollowSides = 3;
            }

            primMesh = new PrimMesh(sides, profileBegin, profileEnd, profileHollow, hollowSides);

            if (primMesh.errorMessage != null)
                if (primMesh.errorMessage.Length > 0)
                    m_log.Error("[ERROR] " + primMesh.errorMessage);

            primMesh.topShearX = pathShearX;
            primMesh.topShearY = pathShearY;
            primMesh.pathCutBegin = pathBegin;
            primMesh.pathCutEnd = pathEnd;
            
            if (primShape.PathCurve == (byte)Extrusion.Straight || primShape.PathCurve == (byte) Extrusion.Flexible)
            {
                primMesh.twistBegin = (float)(primShape.PathTwistBegin * (floatPI * 0.01f));
                primMesh.twistEnd = (float)(primShape.PathTwist * (floatPI * 0.01f));
                primMesh.taperX = pathScaleX;
                primMesh.taperY = pathScaleY;

#if SPAM
            m_log.Debug("****** PrimMesh Parameters (Linear) ******\n" + primMesh.ParamsToDisplayString());
#endif
                try
                {
                    primMesh.Extrude(PathType.Linear);
                }
                catch (Exception ex)
                {
                    ReportPrimError("Extrusion failure: exception: " + ex.ToString(), primName, primMesh);
                    return false;
                }
            }
            else
            {
                primMesh.holeSizeX = (200 - primShape.PathScaleX) * 0.01f;
                primMesh.holeSizeY = (200 - primShape.PathScaleY) * 0.01f;
                primMesh.radius = 0.01f * primShape.PathRadiusOffset;
                primMesh.revolutions = 1.0f + 0.015f * primShape.PathRevolutions;
                primMesh.skew = 0.01f * primShape.PathSkew;
                primMesh.twistBegin = (float)(primShape.PathTwistBegin * (floatPI * 0.02f));
                primMesh.twistEnd = (float)(primShape.PathTwistBegin * (floatPI * 0.02f));
                primMesh.taperX = primShape.PathTaperX * 0.01f;
                primMesh.taperY = primShape.PathTaperY * 0.01f;

                if(profshape == (byte)ProfileShape.HalfCircle)
                {
                    if(primMesh.holeSizeY < 0.01f)
                        primMesh.holeSizeY = 0.01f;
                    else if(primMesh.holeSizeY > 1.0f)
                        primMesh.holeSizeY = 1.0f;
                }

#if SPAM
            m_log.Debug("****** PrimMesh Parameters (Circular) ******\n" + primMesh.ParamsToDisplayString());
#endif
                try
                {
                    primMesh.Extrude(PathType.Circular);
                }
                catch (Exception ex)
                {
                    ReportPrimError("Extrusion failure: exception: " + ex.ToString(), primName, primMesh);
                    return false;
                }
            }

//            primMesh.DumpRaw(baseDir, primName, "primMesh");

            coords = primMesh.coords;
            faces = primMesh.faces;

            return true;
        }

        public static AMeshKey GetMeshUniqueKey(PrimitiveBaseShape primShape, Vector3 size, byte lod, bool convex)
        {
            AMeshKey key = new();
            Byte[] someBytes;

            key.hashB = 5181;
            key.hashC = 5181;
            ulong hash = 5381;

            if (primShape.SculptEntry)
            {
                key.uuid = primShape.SculptTexture;
                key.hashC = mdjb2(key.hashC, primShape.SculptType);
                key.hashC = mdjb2(key.hashC, primShape.PCode);
            }
            else
            {
                hash = mdjb2(hash, primShape.PathCurve);
                hash = mdjb2(hash, (byte)primShape.HollowShape);
                hash = mdjb2(hash, (byte)primShape.ProfileShape);
                hash = mdjb2(hash, primShape.PathBegin);
                hash = mdjb2(hash, primShape.PathEnd);
                hash = mdjb2(hash, primShape.PathScaleX);
                hash = mdjb2(hash, primShape.PathScaleY);
                hash = mdjb2(hash, primShape.PathShearX);
                key.hashA = hash;
                hash = key.hashB;
                hash = mdjb2(hash, primShape.PathShearY);
                hash = mdjb2(hash, (byte)primShape.PathTwist);
                hash = mdjb2(hash, (byte)primShape.PathTwistBegin);
                hash = mdjb2(hash, (byte)primShape.PathRadiusOffset);
                hash = mdjb2(hash, (byte)primShape.PathTaperX);
                hash = mdjb2(hash, (byte)primShape.PathTaperY);
                hash = mdjb2(hash, primShape.PathRevolutions);
                hash = mdjb2(hash, (byte)primShape.PathSkew);
                hash = mdjb2(hash, primShape.ProfileBegin);
                hash = mdjb2(hash, primShape.ProfileEnd);
                hash = mdjb2(hash, primShape.ProfileHollow);
                hash = mdjb2(hash, primShape.PCode);
                key.hashB = hash;
            }

            hash = key.hashC;

            hash = mdjb2(hash, lod);

            if (size == m_MeshUnitSize)
            {
                hash <<= 8;
                hash |= 8;
            }
            else
            {
                someBytes = size.GetBytes();
                for (int i = 0; i < someBytes.Length; i++)
                    hash = mdjb2(hash, someBytes[i]);
                hash <<= 8;
            }

            if (convex)
                hash |= 4;

            if (primShape.SculptEntry)
            {
                hash |= 1;
                if (primShape.SculptType == (byte)SculptType.Mesh)
                    hash |= 2;
            }

            key.hashC = hash;

            return key;
        }

        private static ulong mdjb2(ulong hash, byte c)
        {
            //return ((hash << 5) + hash) + (ulong)c;
            return 33 * hash + c;
        }

        private static ulong mdjb2(ulong hash, ushort c)
        {
            //hash = ((hash << 5) + hash) + (ulong)((byte)c);
            //return ((hash << 5) + hash) + (ulong)(c >> 8);
            return 33 * hash + c;
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod)
        {
            return CreateMesh(primName, primShape, size, lod, false,false,false);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical)
        {
            return CreateMesh(primName, primShape, size, lod, false,false,false);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool shouldCache, bool convex, bool forOde)
        {
            return CreateMesh(primName, primShape, size, lod, false, false, false);
        }

        public IMesh GetMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex)
        {
            Mesh mesh = null;

            if (size.X < 0.01f) size.X = 0.01f;
            if (size.Y < 0.01f) size.Y = 0.01f;
            if (size.Z < 0.01f) size.Z = 0.01f;

            AMeshKey key = GetMeshUniqueKey(primShape, size, (byte)lod, convex);
            lock (m_uniqueMeshes)
            {
                m_uniqueMeshes.TryGetValue(key, out mesh);

                if (mesh != null)
                {
                    mesh.RefCount++;
                    return mesh;
                }

                // try to find a identical mesh on meshs recently released
                lock (m_uniqueReleasedMeshes)
                {
                    m_uniqueReleasedMeshes.TryGetValue(key, out mesh);
                    if (mesh != null)
                    {
                        m_uniqueReleasedMeshes.Remove(key);
                        try
                        {
                            m_uniqueMeshes.Add(key, mesh);
                        }
                        catch { }
                        mesh.RefCount = 1;
                        return mesh;
                    }
                }
            }
            return null;
        }

        private static Vector3 m_MeshUnitSize = new(1.0f, 1.0f, 1.0f);

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex, bool forOde)
        {
#if SPAM
            m_log.DebugFormat("[MESH]: Creating mesh for {0}", primName);
#endif

            Mesh mesh = null;

            if (size.X < 0.01f) size.X = 0.01f;
            if (size.Y < 0.01f) size.Y = 0.01f;
            if (size.Z < 0.01f) size.Z = 0.01f;

            // try to find a identical mesh on meshs in use

            AMeshKey key = GetMeshUniqueKey(primShape,size,(byte)lod, convex);

            lock (m_uniqueMeshes)
            {
                m_uniqueMeshes.TryGetValue(key, out mesh);

                if (mesh != null)
                {
                    mesh.RefCount++;
                    return mesh;
                }

                // try to find a identical mesh on meshs recently released
                lock (m_uniqueReleasedMeshes)
                {
                    m_uniqueReleasedMeshes.TryGetValue(key, out mesh);
                    if (mesh != null)
                    {
                        m_uniqueReleasedMeshes.Remove(key);
                        try
                        {
                            m_uniqueMeshes.Add(key, mesh);
                        }
                        catch { }
                        mesh.RefCount = 1;
                        return mesh;
                    }
                }
            }

            Mesh UnitMesh = null;
            AMeshKey unitKey = GetMeshUniqueKey(primShape, m_MeshUnitSize, (byte)lod, convex);

            lock (m_uniqueReleasedMeshes)
            {
                m_uniqueReleasedMeshes.TryGetValue(unitKey, out UnitMesh);
                if (UnitMesh != null)
                {
                    UnitMesh.RefCount = 1;
                }
            }

            if (UnitMesh == null && primShape.SculptEntry && doMeshFileCache)
                UnitMesh = GetFromFileCache(unitKey);

            if (UnitMesh == null)
            {
                UnitMesh = CreateMeshFromPrimMesher(primName, primShape, lod, convex);

                if (UnitMesh == null)
                    return null;

                UnitMesh.DumpRaw(baseDir, unitKey.ToString(), "Z");

                if (forOde)
                {
                    // force pinned mem allocation
                    UnitMesh.PrepForOde();
                }
                else
                    UnitMesh.TrimExcess();

                UnitMesh.Key = unitKey;
                UnitMesh.RefCount = 1;

                if (doMeshFileCache && primShape.SculptEntry)
                    StoreToFileCache(unitKey, UnitMesh);

                lock (m_uniqueReleasedMeshes)
                {
                    try
                    {
                        m_uniqueReleasedMeshes.Add(unitKey, UnitMesh);
                    }
                    catch { }
                }
            }

            mesh = UnitMesh.Scale(size);
            mesh.Key = key;
            mesh.RefCount = 1;
            lock (m_uniqueMeshes)
            {
                try
                {
                    m_uniqueMeshes.Add(key, mesh);
                }
                catch { }
            }

            return mesh;
        }

        public void ReleaseMesh(IMesh imesh)
        {
            if (imesh == null)
                return;

            Mesh mesh = (Mesh)imesh;

            lock (m_uniqueMeshes)
            {
                int curRefCount = mesh.RefCount;
                curRefCount--;

                if (curRefCount > 0)
                {
                    mesh.RefCount = curRefCount;
                    return;
                }

                mesh.RefCount = 0;
                m_uniqueMeshes.Remove(mesh.Key);
                lock (m_uniqueReleasedMeshes)
                {
                    try
                    {
                        m_uniqueReleasedMeshes.Add(mesh.Key, mesh);
                    }
                    catch { }
                }
            }
        }

        public void ExpireReleaseMeshs()
        {
            if (m_uniqueReleasedMeshes.Count == 0)
                return;

            List<Mesh> meshstodelete = new();
            int refcntr;

            lock (m_uniqueReleasedMeshes)
            {
                foreach (Mesh m in m_uniqueReleasedMeshes.Values)
                {
                    refcntr = m.RefCount;
                    refcntr--;
                    if (refcntr > -6)
                        m.RefCount = refcntr;
                    else
                        meshstodelete.Add(m);
                }

                foreach (Mesh m in meshstodelete)
                {
                    m_uniqueReleasedMeshes.Remove(m.Key);
                    m.releaseBuildingMeshData();
                    m.releasePinned();
                }
            }
        }

        public void FileNames(AMeshKey key, out string dir, out string fullFileName)
        {
            string id = key.ToString();
            string init = id[..1];
            dir = System.IO.Path.Combine(cachePath, init);
            fullFileName = System.IO.Path.Combine(dir, id);
        }

        public string FullFileName(AMeshKey key)
        {
            string id = key.ToString();
            string init = id[..1];
            id = System.IO.Path.Combine(init, id);
            id = System.IO.Path.Combine(cachePath, id);
            return id;
        }

        private Mesh GetFromFileCache(AMeshKey key)
        {
            Mesh mesh = null;
            string filename = FullFileName(key);
            bool ok = true;

            lock (diskLock)
            {
                if (File.Exists(filename))
                {
                    try
                    {
                        using(FileStream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            mesh = Mesh.FromStream(stream,key);
                        }

                    }
                    catch (Exception e)
                    {
                        ok = false;
                        m_log.ErrorFormat(
                            "[MESH CACHE]: Failed to get file {0}.  Exception {1} {2}",
                            filename, e.Message, e.StackTrace);
                    }

                    try
                    {
                        if (mesh == null || !ok)
                            File.Delete(filename);
                        else
                            File.SetLastAccessTimeUtc(filename, DateTime.UtcNow);
                    }
                    catch
                    {
                    }

                }
            }

            return mesh;
        }

        private void StoreToFileCache(AMeshKey key, Mesh mesh)
        {
            bool ok = false;

            // Make sure the target cache directory exists
            FileNames(key, out string dir, out string filename);

            lock (diskLock)
            {
                Stream stream = null;
                try
                {
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    stream = File.Open(filename, FileMode.Create);
                    ok = mesh.ToStream(stream);
                }
                catch (IOException e)
                {
                    m_log.ErrorFormat(
                        "[MESH CACHE]: Failed to write file {0}.  Exception {1} {2}.",
                        filename, e.Message, e.StackTrace);
                    ok = false;
                }
                finally
                {
                    if(stream != null)
                        stream.Dispose();
                }

                if (!ok && File.Exists(filename))
                {
                    try
                    {
                        File.Delete(filename);
                    }
                    catch (IOException)
                    {
                         m_log.ErrorFormat(
                        "[MESH CACHE]: Failed to delete file {0}",filename);
                    }
                }
            }
        }

        private static DateTime lastExpireTime = DateTime.MinValue;
        public void ExpireFileCache()
        {
            if (!doCacheExpire)
                return;

            lock (diskLock)
            {
                try
                {
                    DateTime now = DateTime.UtcNow;
                    if(now.Subtract(lastExpireTime).TotalMinutes < 10.0)
                        return;
                    lastExpireTime = now;
                    string controlfile = System.IO.Path.Combine(cachePath, cacheControlFilename);
                    if (File.Exists(controlfile))
                    {
                        int ndeleted = 0;
                        int totalfiles = 0;
                        int ndirs = 0;
                        DateTime OlderTime = File.GetLastAccessTimeUtc(controlfile) - CacheExpire;
                        File.SetLastAccessTimeUtc(controlfile, now);

                        foreach (string dir in Directory.GetDirectories(cachePath))
                        {
                            try
                            {
                                foreach (string file in Directory.GetFiles(dir))
                                {
                                    try
                                    {
                                        if (File.GetLastAccessTimeUtc(file) < OlderTime)
                                        {
                                            File.Delete(file);
                                            ndeleted++;
                                        }
                                    }
                                    catch { }
                                    totalfiles++;
                                }
                            }
                            catch { }
                            ndirs++;
                        }

                        if (ndeleted == 0)
                            m_log.InfoFormat("[MESH CACHE]: {0} Files in {1} cache folders, no expires",
                                totalfiles,ndirs);
                        else
                            m_log.InfoFormat("[MESH CACHE]: {0} Files in {1} cache folders, expired {2} files accessed before {3}",
                                totalfiles,ndirs, ndeleted, OlderTime.ToString());
                    }
                    else
                    {
                        m_log.Info("[MESH CACHE]: Expire delayed to next startup");
                        FileStream fs = File.Create(controlfile,4096,FileOptions.WriteThrough);
                        fs.Close();
                    }
                }
                catch { }
            }
        }

        public bool checkCache()
        {
            string controlfile = System.IO.Path.Combine(cachePath, cacheControlFilename);
            lock (diskLock)
            {
                try
                {
                    if (!Directory.Exists(cachePath))
                    {
                        Directory.CreateDirectory(cachePath);
                        Thread.Sleep(100);
                        FileStream fs = File.Create(controlfile, 4096, FileOptions.WriteThrough);
                        fs.Close();
                        return true;
                    }
                }
                catch
                {
                    doMeshFileCache = false;
                    doCacheExpire = false;
                    return false;
                }
                finally {}

                if (File.Exists(controlfile))
                    return true;

                try
                {
                    Directory.Delete(cachePath, true);
                    while(Directory.Exists(cachePath))
                        Thread.Sleep(100);
                }
                catch(Exception e)
                {
                    m_log.Error("[MESH CACHE]: failed to delete old version of the cache: " + e.Message);
                    doMeshFileCache = false;
                    doCacheExpire = false;
                    return false;
                } 
                finally {}
                try
                {
                    Directory.CreateDirectory(cachePath);
                    while(!Directory.Exists(cachePath))
                        Thread.Sleep(100);
                }
                catch(Exception e)
                {
                    m_log.Error("[MESH CACHE]: failed to create new cache folder: " + e.Message);
                    doMeshFileCache = false;
                    doCacheExpire = false;
                    return false;
                } 
                finally {}

                try
                {
                    FileStream fs = File.Create(controlfile, 4096, FileOptions.WriteThrough);
                    fs.Close();
                }
                catch(Exception e)
                {
                    m_log.Error("[MESH CACHE]: failed to create new control file: " + e.Message);
                    doMeshFileCache = false;
                    doCacheExpire = false;
                    return false;
                } 
                finally {}
            
                return true;
            }
        }

        public static bool CreateBoundingHull(List<Vector3> inputVertices, out List<Vector3> convexcoords, out List<Face> newfaces)
        {
            convexcoords = null;
            newfaces = null;
            HullDesc desc = new();
            HullResult result = new();

            int nInputVerts = inputVertices.Count;
            int i;

            List<float3> vs = new(nInputVerts);
            float3 f3;

            //useless copy
            for(i = 0 ; i < nInputVerts; i++)
            {
                f3 = new float3(inputVertices[i].X, inputVertices[i].Y, inputVertices[i].Z);
                vs.Add(f3);
            }

            desc.Vertices = vs;
            desc.Flags = HullFlag.QF_TRIANGLES;
            desc.MaxVertices = 256;

            try
            {
                HullError ret = HullUtils.CreateConvexHull(desc, ref result);
                if (ret != HullError.QE_OK)
                    return false;
                int nverts = result.OutputVertices.Count;
                int nindx = result.Indices.Count;
                if(nverts < 3 || nindx< 3)
                    return false;
                if(nindx % 3 != 0)
                    return false;

                convexcoords = new List<Vector3>(nverts);
                Vector3 c;
                vs = result.OutputVertices;

                for(i = 0 ; i < nverts; i++)
                {
                    c = new Vector3(vs[i].x, vs[i].y, vs[i].z);
                    convexcoords.Add(c);
                }

                newfaces = new List<Face>(nindx / 3);
                List<int> indxs = result.Indices;
                int k, l, m;
                Face f;
                for(i = 0 ; i < nindx;)
                {
                    k = indxs[i++];
                    l = indxs[i++];
                    m = indxs[i++];
                    if(k > nInputVerts)
                        continue;
                    if(l > nInputVerts)
                        continue;
                    if(m > nInputVerts)
                        continue;
                    f = new Face(k,l,m);
                    newfaces.Add(f);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
