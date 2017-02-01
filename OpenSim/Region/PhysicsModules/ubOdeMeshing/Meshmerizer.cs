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
using System.Drawing.Imaging;
using System.IO.Compression;
using PrimMesher;
using log4net;
using Nini.Config;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Mono.Addins;

namespace OpenSim.Region.PhysicsModule.ubODEMeshing
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ubODEMeshmerizer")]
    public class ubMeshmerizer : IMesher, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Setting baseDir to a path will enable the dumping of raw files
        // raw files can be imported by blender so a visual inspection of the results can be done

        private bool m_Enabled = false;

        public static object diskLock = new object();

        public bool doMeshFileCache = true;

        public string cachePath = "MeshCache";
        public TimeSpan CacheExpire;
        public bool doCacheExpire = true;

//        const string baseDir = "rawFiles";
        private const string baseDir = null; //"rawFiles";

        private bool useMeshiesPhysicsMesh = false;

        private float minSizeForComplexMesh = 0.2f; // prims with all dimensions smaller than this will have a bounding box mesh

        private Dictionary<AMeshKey, Mesh> m_uniqueMeshes = new Dictionary<AMeshKey, Mesh>();
        private Dictionary<AMeshKey, Mesh> m_uniqueReleasedMeshes = new Dictionary<AMeshKey, Mesh>();

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
                    if (useMeshiesPhysicsMesh)
                    {
                        doMeshFileCache = mesh_config.GetBoolean("MeshFileCache", doMeshFileCache);
                        cachePath = mesh_config.GetString("MeshFileCachePath", cachePath);
                        fcache = mesh_config.GetFloat("MeshFileCacheExpireHours", fcache);
                        doCacheExpire = mesh_config.GetBoolean("MeshFileCacheDoExpire", doCacheExpire);
                    }
                    else
                    {
                        doMeshFileCache = false;
                        doCacheExpire = false;
                    }

                    m_Enabled = true;
                }

                CacheExpire = TimeSpan.FromHours(fcache);

                lock (diskLock)
                {
                    if(doMeshFileCache && cachePath != "")
                    {
                        try
                        {
                            if (!Directory.Exists(cachePath))
                                Directory.CreateDirectory(cachePath);
                        }
                        catch
                        {
                            doMeshFileCache = false;
                            doCacheExpire = false;
                        }
                    }
                }
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

        /// <summary>
        /// creates a simple box mesh of the specified size. This mesh is of very low vertex count and may
        /// be useful as a backup proxy when level of detail is not needed or when more complex meshes fail
        /// for some reason
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <param name="minZ"></param>
        /// <param name="maxZ"></param>
        /// <returns></returns>
        private static Mesh CreateSimpleBoxMesh(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            Mesh box = new Mesh(true);
            List<Vertex> vertices = new List<Vertex>();
            // bottom

            vertices.Add(new Vertex(minX, maxY, minZ));
            vertices.Add(new Vertex(maxX, maxY, minZ));
            vertices.Add(new Vertex(maxX, minY, minZ));
            vertices.Add(new Vertex(minX, minY, minZ));

            box.Add(new Triangle(vertices[0], vertices[1], vertices[2]));
            box.Add(new Triangle(vertices[0], vertices[2], vertices[3]));

            // top

            vertices.Add(new Vertex(maxX, maxY, maxZ));
            vertices.Add(new Vertex(minX, maxY, maxZ));
            vertices.Add(new Vertex(minX, minY, maxZ));
            vertices.Add(new Vertex(maxX, minY, maxZ));

            box.Add(new Triangle(vertices[4], vertices[5], vertices[6]));
            box.Add(new Triangle(vertices[4], vertices[6], vertices[7]));

            // sides

            box.Add(new Triangle(vertices[5], vertices[0], vertices[3]));
            box.Add(new Triangle(vertices[5], vertices[3], vertices[6]));

            box.Add(new Triangle(vertices[1], vertices[0], vertices[5]));
            box.Add(new Triangle(vertices[1], vertices[5], vertices[4]));

            box.Add(new Triangle(vertices[7], vertices[1], vertices[4]));
            box.Add(new Triangle(vertices[7], vertices[2], vertices[1]));

            box.Add(new Triangle(vertices[3], vertices[2], vertices[7]));
            box.Add(new Triangle(vertices[3], vertices[7], vertices[6]));

            return box;
        }

        /// <summary>
        /// Creates a simple bounding box mesh for a complex input mesh
        /// </summary>
        /// <param name="meshIn"></param>
        /// <returns></returns>
        private static Mesh CreateBoundingBoxMesh(Mesh meshIn)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (Vector3 v in meshIn.getVertexList())
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;

                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            return CreateSimpleBoxMesh(minX, maxX, minY, maxY, minZ, maxZ);
        }

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
        private void AddSubMesh(OSDMap subMeshData, List<Coord> coords, List<Face> faces)
        {
    //                                    Console.WriteLine("subMeshMap for {0} - {1}", primName, Util.GetFormattedXml((OSD)subMeshMap));

            // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
            // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
            // geometry for this submesh.
            if (subMeshData.ContainsKey("NoGeometry") && ((OSDBoolean)subMeshData["NoGeometry"]))
                return;

            OpenMetaverse.Vector3 posMax;
            OpenMetaverse.Vector3 posMin;
            if (subMeshData.ContainsKey("PositionDomain"))
            {
                posMax = ((OSDMap)subMeshData["PositionDomain"])["Max"].AsVector3();
                posMin = ((OSDMap)subMeshData["PositionDomain"])["Min"].AsVector3();
            }
            else
            {
                posMax = new Vector3(0.5f, 0.5f, 0.5f);
                posMin = new Vector3(-0.5f, -0.5f, -0.5f);
            }

            ushort faceIndexOffset = (ushort)coords.Count;

            byte[] posBytes = subMeshData["Position"].AsBinary();
            for (int i = 0; i < posBytes.Length; i += 6)
            {
                ushort uX = Utils.BytesToUInt16(posBytes, i);
                ushort uY = Utils.BytesToUInt16(posBytes, i + 2);
                ushort uZ = Utils.BytesToUInt16(posBytes, i + 4);

                Coord c = new Coord(
                Utils.UInt16ToFloat(uX, posMin.X, posMax.X),
                Utils.UInt16ToFloat(uY, posMin.Y, posMax.Y),
                Utils.UInt16ToFloat(uZ, posMin.Z, posMax.Z));

                coords.Add(c);
            }

            byte[] triangleBytes = subMeshData["TriangleList"].AsBinary();
            for (int i = 0; i < triangleBytes.Length; i += 6)
            {
                ushort v1 = (ushort)(Utils.BytesToUInt16(triangleBytes, i) + faceIndexOffset);
                ushort v2 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 2) + faceIndexOffset);
                ushort v3 = (ushort)(Utils.BytesToUInt16(triangleBytes, i + 4) + faceIndexOffset);
                Face f = new Face(v1, v2, v3);
                faces.Add(f);
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

            List<Coord> coords;
            List<Face> faces;

            if (primShape.SculptEntry)
            {
                if (((OpenMetaverse.SculptType)primShape.SculptType) == SculptType.Mesh)
                {
                    if (!useMeshiesPhysicsMesh)
                        return null;

                    if (!GenerateCoordsAndFacesFromPrimMeshData(primName, primShape, out coords, out faces, convex))
                        return null;
                }
                else
                {
                    if (!GenerateCoordsAndFacesFromPrimSculptData(primName, primShape, lod, out coords, out faces))
                        return null;
                }
            }
            else
            {
                if (!GenerateCoordsAndFacesFromPrimShapeData(primName, primShape, lod, convex, out coords, out faces))
                    return null;
            }


            int numCoords = coords.Count;
            int numFaces = faces.Count;

            Mesh mesh = new Mesh(true);
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
                m_log.ErrorFormat("[MESH]: invalid degenerated mesh for prim " + primName + " ignored");
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
        private bool GenerateCoordsAndFacesFromPrimMeshData(
            string primName, PrimitiveBaseShape primShape, out List<Coord> coords, out List<Face> faces, bool convex)
        {
//            m_log.DebugFormat("[MESH]: experimental mesh proxy generation for {0}", primName);


            // for ubOde we have a diferent mesh use priority
            // priority is to use full mesh then decomposition
            // SL does the oposite
            bool usemesh = false;

            coords = new List<Coord>();
            faces = new List<Face>();
            OSD meshOsd = null;

            if (primShape.SculptData.Length <= 0)
            {
//                m_log.InfoFormat("[MESH]: asset data for {0} is zero length", primName);
                return false;
            }

            long start = 0;
            using (MemoryStream data = new MemoryStream(primShape.SculptData))
            {
                try
                {
                    OSD osd = OSDParser.DeserializeLLSDBinary(data);
                    if (osd is OSDMap)
                        meshOsd = (OSDMap)osd;
                    else
                    {
                        m_log.WarnFormat("[Mesh}: unable to cast mesh asset to OSDMap prim: {0}",primName);
                        return false;
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[MESH]: Exception deserializing mesh asset header:" + e.ToString());
                }

                start = data.Position;
            }

            if (meshOsd is OSDMap)
            {
                OSDMap physicsParms = null;
                OSDMap map = (OSDMap)meshOsd;

                if (!convex)
                {
                    if (map.ContainsKey("physics_shape"))
                        physicsParms = (OSDMap)map["physics_shape"]; // old asset format
                    else if (map.ContainsKey("physics_mesh"))
                        physicsParms = (OSDMap)map["physics_mesh"]; // new asset format

                    if (physicsParms != null)
                        usemesh = true;
                }

                if(!usemesh && (map.ContainsKey("physics_convex")))
                        physicsParms = (OSDMap)map["physics_convex"];

                if (physicsParms == null)
                {
                    //m_log.WarnFormat("[MESH]: unknown mesh type for prim {0}",primName);
                    return false;
                }

                int physOffset = physicsParms["offset"].AsInteger() + (int)start;
                int physSize = physicsParms["size"].AsInteger();

                if (physOffset < 0 || physSize == 0)
                    return false; // no mesh data in asset

                OSD decodedMeshOsd = new OSD();
                byte[] meshBytes = new byte[physSize];
                System.Buffer.BlockCopy(primShape.SculptData, physOffset, meshBytes, 0, physSize);

                try
                {
                    using (MemoryStream inMs = new MemoryStream(meshBytes))
                    {
                        using (MemoryStream outMs = new MemoryStream())
                        {
                            using (DeflateStream decompressionStream = new DeflateStream(inMs, CompressionMode.Decompress))
                            {
                                byte[] readBuffer = new byte[2048];
                                inMs.Read(readBuffer, 0, 2); // skip first 2 bytes in header
                                int readLen = 0;

                                while ((readLen = decompressionStream.Read(readBuffer, 0, readBuffer.Length)) > 0)
                                    outMs.Write(readBuffer, 0, readLen);

                                outMs.Flush();
                                outMs.Seek(0, SeekOrigin.Begin);

                                byte[] decompressedBuf = outMs.GetBuffer();

                                decodedMeshOsd = OSDParser.DeserializeLLSDBinary(decompressedBuf);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[MESH]: exception decoding physical mesh prim " + primName +" : " + e.ToString());
                    return false;
                }

                if (usemesh)
                {
                    OSDArray decodedMeshOsdArray = null;

                    // physics_shape is an array of OSDMaps, one for each submesh
                    if (decodedMeshOsd is OSDArray)
                    {
//                      Console.WriteLine("decodedMeshOsd for {0} - {1}", primName, Util.GetFormattedXml(decodedMeshOsd));

                        decodedMeshOsdArray = (OSDArray)decodedMeshOsd;
                        foreach (OSD subMeshOsd in decodedMeshOsdArray)
                        {
                            if (subMeshOsd is OSDMap)
                                AddSubMesh(subMeshOsd as OSDMap, coords, faces);
                        }
                    }
                }
                else
                {
                    OSDMap cmap = (OSDMap)decodedMeshOsd;
                    if (cmap == null)
                        return false;

                    byte[] data;

                    List<float3> vs = new List<float3>();
                    PHullResult hullr = new PHullResult();
                    float3 f3;
                    Coord c;
                    Face f;
                    Vector3 range;
                    Vector3 min;

                    const float invMaxU16 = 1.0f / 65535f;
                    int t1;
                    int t2;
                    int t3;
                    int i;
                    int nverts;
                    int nindexs;

                    if (cmap.ContainsKey("Max"))
                        range = cmap["Max"].AsVector3();
                    else
                        range = new Vector3(0.5f, 0.5f, 0.5f);

                    if (cmap.ContainsKey("Min"))
                        min = cmap["Min"].AsVector3();
                    else
                        min = new Vector3(-0.5f, -0.5f, -0.5f);

                    range = range - min;
                    range *= invMaxU16;

                    if(!convex)
                    {
                        // if mesh data not present and not convex then we need convex decomposition data
                        if (cmap.ContainsKey("HullList") && cmap.ContainsKey("Positions"))
                        {
                            List<int> hsizes = new List<int>();
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
                            int ptr = 0;
                            int vertsoffset = 0;

                            if (totalpoints == data.Length / 6) // 2 bytes per coord, 3 coords per point
                            {
                                foreach (int hullsize in hsizes)
                                {
                                    for (i = 0; i < hullsize; i++ )
                                    {
                                        t1 = data[ptr++];
                                        t1 += data[ptr++] << 8;
                                        t2 = data[ptr++];
                                        t2 += data[ptr++] << 8;
                                        t3 = data[ptr++];
                                        t3 += data[ptr++] << 8;

                                        f3 = new float3((t1 * range.X + min.X),
                                                  (t2 * range.Y + min.Y),
                                                  (t3 * range.Z + min.Z));
                                        vs.Add(f3);
                                    }

                                    if(hullsize <3)
                                    {
                                        vs.Clear();
                                        continue;
                                    }

                                    if (hullsize <5)
                                    {
                                        foreach (float3 point in vs)
                                        {
                                            c.X = point.x;
                                            c.Y = point.y;
                                            c.Z = point.z;
                                            coords.Add(c);
                                        }
                                        f = new Face(vertsoffset, vertsoffset + 1, vertsoffset + 2);
                                        faces.Add(f);

                                        if (hullsize == 4)
                                        {
                                            // not sure about orientation..
                                            f = new Face(vertsoffset, vertsoffset + 2, vertsoffset + 3);
                                            faces.Add(f);
                                            f = new Face(vertsoffset, vertsoffset + 3, vertsoffset + 1);
                                            faces.Add(f);
                                            f = new Face(vertsoffset + 3, vertsoffset + 2, vertsoffset + 1);
                                            faces.Add(f);
                                        }
                                        vertsoffset += vs.Count;
                                        vs.Clear();
                                        continue;
                                    }
    /*
                                    if (!HullUtils.ComputeHull(vs, ref hullr, 0, 0.0f))
                                    {
                                        vs.Clear();
                                        continue;
                                    }

                                    nverts = hullr.Vertices.Count;
                                    nindexs = hullr.Indices.Count;

                                    if (nindexs % 3 != 0)
                                    {
                                        vs.Clear();
                                        continue;
                                    }

                                    for (i = 0; i < nverts; i++)
                                    {
                                        c.X = hullr.Vertices[i].x;
                                        c.Y = hullr.Vertices[i].y;
                                        c.Z = hullr.Vertices[i].z;
                                        coords.Add(c);
                                    }

                                    for (i = 0; i < nindexs; i += 3)
                                    {
                                        t1 = hullr.Indices[i];
                                        if (t1 > nverts)
                                            break;
                                        t2 = hullr.Indices[i + 1];
                                        if (t2 > nverts)
                                            break;
                                        t3 = hullr.Indices[i + 2];
                                        if (t3 > nverts)
                                            break;
                                        f = new Face(vertsoffset + t1, vertsoffset + t2, vertsoffset + t3);
                                        faces.Add(f);
                                    }
    */
                                    List<int> indices;
                                    if (!HullUtils.ComputeHull(vs, out indices))
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

                                    for (i = 0; i < nverts; i++)
                                    {
                                        c.X = vs[i].x;
                                        c.Y = vs[i].y;
                                        c.Z = vs[i].z;
                                        coords.Add(c);
                                    }

                                    for (i = 0; i < nindexs; i += 3)
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
                                        f = new Face(vertsoffset + t1, vertsoffset + t2, vertsoffset + t3);
                                        faces.Add(f);
                                    }
                                    vertsoffset += nverts;
                                    vs.Clear();
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

                    if (cmap.ContainsKey("BoundingVerts"))
                    {
                        data = cmap["BoundingVerts"].AsBinary();

                        for (i = 0; i < data.Length; )
                        {
                            t1 = data[i++];
                            t1 += data[i++] << 8;
                            t2 = data[i++];
                            t2 += data[i++] << 8;
                            t3 = data[i++];
                            t3 += data[i++] << 8;

                            f3 = new float3((t1 * range.X + min.X),
                                      (t2 * range.Y + min.Y),
                                      (t3 * range.Z + min.Z));
                            vs.Add(f3);
                        }

                        nverts = vs.Count;

                        if (nverts < 3)
                        {
                            vs.Clear();
                            return false;
                        }

                        if (nverts < 5)
                        {
                            foreach (float3 point in vs)
                            {
                                c.X = point.x;
                                c.Y = point.y;
                                c.Z = point.z;
                                coords.Add(c);
                            }

                            f = new Face(0, 1, 2);
                            faces.Add(f);

                            if (nverts == 4)
                            {
                                f = new Face(0, 2, 3);
                                faces.Add(f);
                                f = new Face(0, 3, 1);
                                faces.Add(f);
                                f = new Face( 3, 2, 1);
                                faces.Add(f);
                            }
                            vs.Clear();
                            return true;
                        }
/*
                        if (!HullUtils.ComputeHull(vs, ref hullr, 0, 0.0f))
                            return false;

                        nverts = hullr.Vertices.Count;
                        nindexs = hullr.Indices.Count;

                        if (nindexs % 3 != 0)
                            return false;

                        for (i = 0; i < nverts; i++)
                        {
                            c.X = hullr.Vertices[i].x;
                            c.Y = hullr.Vertices[i].y;
                            c.Z = hullr.Vertices[i].z;
                            coords.Add(c);
                        }
                        for (i = 0; i < nindexs; i += 3)
                        {
                            t1 = hullr.Indices[i];
                            if (t1 > nverts)
                                break;
                            t2 = hullr.Indices[i + 1];
                            if (t2 > nverts)
                                break;
                            t3 = hullr.Indices[i + 2];
                            if (t3 > nverts)
                                break;
                            f = new Face(t1, t2, t3);
                            faces.Add(f);
                        }
*/
                        List<int> indices;
                        if (!HullUtils.ComputeHull(vs, out indices))
                            return false;

                        nindexs = indices.Count;

                        if (nindexs % 3 != 0)
                            return false;

                        for (i = 0; i < nverts; i++)
                        {
                            c.X = vs[i].x;
                            c.Y = vs[i].y;
                            c.Z = vs[i].z;
                            coords.Add(c);
                        }
                        for (i = 0; i < nindexs; i += 3)
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
                            f = new Face(t1, t2, t3);
                            faces.Add(f);
                        }
                        vs.Clear();
                        if (coords.Count > 0 && faces.Count > 0)
                            return true;
                    }
                    else
                        return false;
                }
            }

            return true;
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
        private bool GenerateCoordsAndFacesFromPrimSculptData(
            string primName, PrimitiveBaseShape primShape, float lod, out List<Coord> coords, out List<Face> faces)
        {
            coords = new List<Coord>();
            faces = new List<Face>();
            PrimMesher.SculptMesh sculptMesh;
            Image idata = null;

                if (primShape.SculptData == null || primShape.SculptData.Length == 0)
                    return false;

                try
                {
                    OpenMetaverse.Imaging.ManagedImage unusedData;
                    OpenMetaverse.Imaging.OpenJPEG.DecodeToImage(primShape.SculptData, out unusedData, out idata);

                    unusedData = null;

                    if (idata == null)
                    {
                        // In some cases it seems that the decode can return a null bitmap without throwing
                        // an exception
                        m_log.WarnFormat("[PHYSICS]: OpenJPEG decoded sculpt data for {0} to a null bitmap.  Ignoring.", primName);
                        return false;
                    }
                }
                catch (DllNotFoundException)
                {
                    m_log.Error("[PHYSICS]: OpenJpeg is not installed correctly on this system. Physics Proxy generation failed.  Often times this is because of an old version of GLIBC.  You must have version 2.4 or above!");
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

            PrimMesher.SculptMesh.SculptType sculptType;
            // remove mirror and invert bits
            OpenMetaverse.SculptType pbsSculptType = ((OpenMetaverse.SculptType)(primShape.SculptType & 0x3f));
            switch (pbsSculptType)
            {
                case OpenMetaverse.SculptType.Cylinder:
                    sculptType = PrimMesher.SculptMesh.SculptType.cylinder;
                    break;
                case OpenMetaverse.SculptType.Plane:
                    sculptType = PrimMesher.SculptMesh.SculptType.plane;
                    break;
                case OpenMetaverse.SculptType.Torus:
                    sculptType = PrimMesher.SculptMesh.SculptType.torus;
                    break;
                case OpenMetaverse.SculptType.Sphere:
                    sculptType = PrimMesher.SculptMesh.SculptType.sphere;
                    break;
                default:
                    sculptType = PrimMesher.SculptMesh.SculptType.plane;
                    break;
            }

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
                out List<Coord> coords, out List<Face> faces)
        {
            PrimMesh primMesh;
            coords = new List<Coord>();
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
                switch (iLOD)
                {
                    case LevelOfDetail.High:    sides = 24;     break;
                    case LevelOfDetail.Medium:  sides = 12;     break;
                    case LevelOfDetail.Low:     sides = 6;      break;
                    case LevelOfDetail.VeryLow: sides = 3;      break;
                    default:                    sides = 24;     break;
                }
            }
            else if (profshape == (byte)ProfileShape.HalfCircle)
            { // half circle, prim is a sphere
                switch (iLOD)
                {
                    case LevelOfDetail.High:    sides = 24;     break;
                    case LevelOfDetail.Medium:  sides = 12;     break;
                    case LevelOfDetail.Low:     sides = 6;      break;
                    case LevelOfDetail.VeryLow: sides = 3;      break;
                    default:                    sides = 24;     break;
                }

                profileBegin = 0.5f * profileBegin + 0.5f;
                profileEnd = 0.5f * profileEnd + 0.5f;
            }

            int hollowSides = sides;
            if (primShape.HollowShape == HollowShape.Circle)
            {
                switch (iLOD)
                {
                    case LevelOfDetail.High:    hollowSides = 24;     break;
                    case LevelOfDetail.Medium:  hollowSides = 12;     break;
                    case LevelOfDetail.Low:     hollowSides = 6;      break;
                    case LevelOfDetail.VeryLow: hollowSides = 3;      break;
                    default:                    hollowSides = 24;     break;
                }
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
                primMesh.twistBegin = (primShape.PathTwistBegin * 18) / 10;
                primMesh.twistEnd = (primShape.PathTwist * 18) / 10;
                primMesh.taperX = pathScaleX;
                primMesh.taperY = pathScaleY;

#if SPAM
            m_log.Debug("****** PrimMesh Parameters (Linear) ******\n" + primMesh.ParamsToDisplayString());
#endif
                try
                {
                    primMesh.ExtrudeLinear();
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
                primMesh.twistBegin = (primShape.PathTwistBegin * 36) / 10;
                primMesh.twistEnd = (primShape.PathTwist * 36) / 10;
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
                    primMesh.ExtrudeCircular();
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

        public AMeshKey GetMeshUniqueKey(PrimitiveBaseShape primShape, Vector3 size, byte lod, bool convex)
        {
            AMeshKey key = new AMeshKey();
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
                hash = hash << 8;
                hash |= 8;
            }
            else
            {
                someBytes = size.GetBytes();
                for (int i = 0; i < someBytes.Length; i++)
                    hash = mdjb2(hash, someBytes[i]);
                hash = hash << 8;
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

        private ulong mdjb2(ulong hash, byte c)
        {
            return ((hash << 5) + hash) + (ulong)c;
        }

        private ulong mdjb2(ulong hash, ushort c)
        {
            hash = ((hash << 5) + hash) + (ulong)((byte)c);
            return ((hash << 5) + hash) + (ulong)(c >> 8);
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

        private static Vector3 m_MeshUnitSize = new Vector3(1.0f, 1.0f, 1.0f);

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

            List<Mesh> meshstodelete = new List<Mesh>();
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

        public void FileNames(AMeshKey key, out string dir,out string fullFileName)
        {
            string id = key.ToString();
            string init = id.Substring(0, 1);
            dir = System.IO.Path.Combine(cachePath, init);
            fullFileName = System.IO.Path.Combine(dir, id);
        }

        public string FullFileName(AMeshKey key)
        {
            string id = key.ToString();
            string init = id.Substring(0,1);
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
//                            BinaryFormatter bformatter = new BinaryFormatter();
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
            string dir = String.Empty;
            string filename = String.Empty;

            FileNames(key, out dir, out filename);

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
                    catch (IOException e)
                    {
                         m_log.ErrorFormat(
                        "[MESH CACHE]: Failed to delete file {0}",filename);
                    }
                }
            }
        }

        public void ExpireFileCache()
        {
            if (!doCacheExpire)
                return;

            string controlfile = System.IO.Path.Combine(cachePath, "cntr");

            lock (diskLock)
            {
                try
                {
                    if (File.Exists(controlfile))
                    {
                        int ndeleted = 0;
                        int totalfiles = 0;
                        int ndirs = 0;
                        DateTime OlderTime = File.GetLastAccessTimeUtc(controlfile) - CacheExpire;
                        File.SetLastAccessTimeUtc(controlfile, DateTime.UtcNow);

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
    }
}
