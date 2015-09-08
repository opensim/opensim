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
using System.Reflection;
using System.IO;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using PrimMesher;
using log4net;
using Nini.Config;
using Mono.Addins;

namespace OpenSim.Region.PhysicsModule.Meshing
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "Meshmerizer")]
    public class Meshmerizer : IMesher, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[MESH]";

        // Setting baseDir to a path will enable the dumping of raw files
        // raw files can be imported by blender so a visual inspection of the results can be done
#if SPAM
        const string baseDir = "rawFiles";
#else
        private const string baseDir = null; //"rawFiles";
#endif
        private bool m_Enabled = false;

        // If 'true', lots of DEBUG logging of asset parsing details
        private bool debugDetail = false;

        private bool cacheSculptMaps = true;
        private string decodedSculptMapPath = null;
        private bool useMeshiesPhysicsMesh = false;

        private float minSizeForComplexMesh = 0.2f; // prims with all dimensions smaller than this will have a bounding box mesh

        private List<List<Vector3>> mConvexHulls = null;
        private List<Vector3> mBoundingHull = null;

        // Mesh cache. Static so it can be shared across instances of this class
        private static Dictionary<ulong, Mesh> m_uniqueMeshes = new Dictionary<ulong, Mesh>();

        #region INonSharedRegionModule
        public string Name
        {
            get { return "Meshmerizer"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["Startup"];
            if (config != null)
            {
                string mesher = config.GetString("meshing", string.Empty);
                if (mesher == Name)
                {
                    m_Enabled = true;

                    IConfig mesh_config = source.Configs["Mesh"];

                    decodedSculptMapPath = config.GetString("DecodedSculptMapPath", "j2kDecodeCache");
                    cacheSculptMaps = config.GetBoolean("CacheSculptMaps", cacheSculptMaps);
                    if (mesh_config != null)
                    {
                        useMeshiesPhysicsMesh = mesh_config.GetBoolean("UseMeshiesPhysicsMesh", useMeshiesPhysicsMesh);
                        debugDetail = mesh_config.GetBoolean("LogMeshDetails", debugDetail);
                    }

                    try
                    {
                        if (!Directory.Exists(decodedSculptMapPath))
                            Directory.CreateDirectory(decodedSculptMapPath);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[SCULPT]: Unable to create {0} directory: ", decodedSculptMapPath, e.Message);
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
            Mesh box = new Mesh();
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
        private void AddSubMesh(OSDMap subMeshData, Vector3 size, List<Coord> coords, List<Face> faces)
        {
    //                                    Console.WriteLine("subMeshMap for {0} - {1}", primName, Util.GetFormattedXml((OSD)subMeshMap));
    
            // As per http://wiki.secondlife.com/wiki/Mesh/Mesh_Asset_Format, some Mesh Level
            // of Detail Blocks (maps) contain just a NoGeometry key to signal there is no
            // geometry for this submesh.
            if (subMeshData.ContainsKey("NoGeometry") && ((OSDBoolean)subMeshData["NoGeometry"]))
                return;
    
            OpenMetaverse.Vector3 posMax = ((OSDMap)subMeshData["PositionDomain"])["Max"].AsVector3();
            OpenMetaverse.Vector3 posMin = ((OSDMap)subMeshData["PositionDomain"])["Min"].AsVector3();
            ushort faceIndexOffset = (ushort)coords.Count;

            byte[] posBytes = subMeshData["Position"].AsBinary();
            for (int i = 0; i < posBytes.Length; i += 6)
            {
                ushort uX = Utils.BytesToUInt16(posBytes, i);
                ushort uY = Utils.BytesToUInt16(posBytes, i + 2);
                ushort uZ = Utils.BytesToUInt16(posBytes, i + 4);
    
                Coord c = new Coord(
                Utils.UInt16ToFloat(uX, posMin.X, posMax.X) * size.X,
                Utils.UInt16ToFloat(uY, posMin.Y, posMax.Y) * size.Y,
                Utils.UInt16ToFloat(uZ, posMin.Z, posMax.Z) * size.Z);
    
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
        private Mesh CreateMeshFromPrimMesher(string primName, PrimitiveBaseShape primShape, Vector3 size, float lod)
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

                    if (!GenerateCoordsAndFacesFromPrimMeshData(primName, primShape, size, out coords, out faces))
                        return null;
                }
                else
                {
                    if (!GenerateCoordsAndFacesFromPrimSculptData(primName, primShape, size, lod, out coords, out faces))
                        return null;
                }
            }
            else
            {
                if (!GenerateCoordsAndFacesFromPrimShapeData(primName, primShape, size, lod, out coords, out faces))
                    return null;
            }

            // Remove the reference to any JPEG2000 sculpt data so it can be GCed
            primShape.SculptData = Utils.EmptyBytes;

            int numCoords = coords.Count;
            int numFaces = faces.Count;

            // Create the list of vertices
            List<Vertex> vertices = new List<Vertex>();
            for (int i = 0; i < numCoords; i++)
            {
                Coord c = coords[i];
                vertices.Add(new Vertex(c.X, c.Y, c.Z));
            }

            Mesh mesh = new Mesh();
            // Add the corresponding triangles to the mesh
            for (int i = 0; i < numFaces; i++)
            {
                Face f = faces[i];
                mesh.Add(new Triangle(vertices[f.v1], vertices[f.v2], vertices[f.v3]));
            }

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
            string primName, PrimitiveBaseShape primShape, Vector3 size, out List<Coord> coords, out List<Face> faces)
        {
//            m_log.DebugFormat("[MESH]: experimental mesh proxy generation for {0}", primName);

            coords = new List<Coord>();
            faces = new List<Face>();
            OSD meshOsd = null;

            mConvexHulls = null;
            mBoundingHull = null;

            if (primShape.SculptData.Length <= 0)
            {
                // XXX: At the moment we can not log here since ODEPrim, for instance, ends up triggering this
                // method twice - once before it has loaded sculpt data from the asset service and once afterwards.
                // The first time will always call with unloaded SculptData if this needs to be uploaded.
//                m_log.ErrorFormat("[MESH]: asset data for {0} is zero length", primName);
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
                        m_log.Warn("[Mesh}: unable to cast mesh asset to OSDMap");
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
                if (map.ContainsKey("physics_shape"))
                {
                    physicsParms = (OSDMap)map["physics_shape"]; // old asset format
                    if (debugDetail) m_log.DebugFormat("{0} prim='{1}': using 'physics_shape' mesh data", LogHeader, primName);
                }
                else if (map.ContainsKey("physics_mesh"))
                {
                    physicsParms = (OSDMap)map["physics_mesh"]; // new asset format
                    if (debugDetail) m_log.DebugFormat("{0} prim='{1}':using 'physics_mesh' mesh data", LogHeader, primName);
                }
                else if (map.ContainsKey("medium_lod"))
                {
                    physicsParms = (OSDMap)map["medium_lod"]; // if no physics mesh, try to fall back to medium LOD display mesh
                    if (debugDetail) m_log.DebugFormat("{0} prim='{1}':using 'medium_lod' mesh data", LogHeader, primName);
                }
                else if (map.ContainsKey("high_lod"))
                {
                    physicsParms = (OSDMap)map["high_lod"]; // if all else fails, use highest LOD display mesh and hope it works :)
                    if (debugDetail) m_log.DebugFormat("{0} prim='{1}':using 'high_lod' mesh data", LogHeader, primName);
                }

                if (map.ContainsKey("physics_convex"))
                { // pull this out also in case physics engine can use it
                    OSD convexBlockOsd = null;
                    try
                    {
                        OSDMap convexBlock = (OSDMap)map["physics_convex"];
                        {
                            int convexOffset = convexBlock["offset"].AsInteger() + (int)start;
                            int convexSize = convexBlock["size"].AsInteger();

                            byte[] convexBytes = new byte[convexSize];
                            
                            System.Buffer.BlockCopy(primShape.SculptData, convexOffset, convexBytes, 0, convexSize);
                            
                            try
                            {
                                convexBlockOsd = DecompressOsd(convexBytes);
                            }
                            catch (Exception e)
                            {
                                m_log.ErrorFormat("{0} prim='{1}': exception decoding convex block: {2}", LogHeader, primName, e);
                                //return false;
                            }
                        }
                        
                        if (convexBlockOsd != null && convexBlockOsd is OSDMap)
                        {
                            convexBlock = convexBlockOsd as OSDMap;

                            if (debugDetail)
                            {
                                string keys = LogHeader + " keys found in convexBlock: ";
                                foreach (KeyValuePair<string, OSD> kvp in convexBlock)
                                    keys += "'" + kvp.Key + "' ";
                                m_log.Debug(keys);
                            }

                            Vector3 min = new Vector3(-0.5f, -0.5f, -0.5f);
                            if (convexBlock.ContainsKey("Min")) min = convexBlock["Min"].AsVector3();
                            Vector3 max = new Vector3(0.5f, 0.5f, 0.5f);
                            if (convexBlock.ContainsKey("Max")) max = convexBlock["Max"].AsVector3();

                            List<Vector3> boundingHull = null;

                            if (convexBlock.ContainsKey("BoundingVerts"))
                            {
                                byte[] boundingVertsBytes = convexBlock["BoundingVerts"].AsBinary();
                                boundingHull = new List<Vector3>();
                                for (int i = 0; i < boundingVertsBytes.Length; )
                                {
                                    ushort uX = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;
                                    ushort uY = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;
                                    ushort uZ = Utils.BytesToUInt16(boundingVertsBytes, i); i += 2;

                                    Vector3 pos = new Vector3(
                                        Utils.UInt16ToFloat(uX, min.X, max.X),
                                        Utils.UInt16ToFloat(uY, min.Y, max.Y),
                                        Utils.UInt16ToFloat(uZ, min.Z, max.Z)
                                    );

                                    boundingHull.Add(pos);
                                }

                                mBoundingHull = boundingHull;
                                if (debugDetail) m_log.DebugFormat("{0} prim='{1}': parsed bounding hull. nVerts={2}", LogHeader, primName, mBoundingHull.Count);
                            }

                            if (convexBlock.ContainsKey("HullList"))
                            {
                                byte[] hullList = convexBlock["HullList"].AsBinary();

                                byte[] posBytes = convexBlock["Positions"].AsBinary();

                                List<List<Vector3>> hulls = new List<List<Vector3>>();
                                int posNdx = 0;

                                foreach (byte cnt in hullList)
                                {
                                    int count = cnt == 0 ? 256 : cnt;
                                    List<Vector3> hull = new List<Vector3>();

                                    for (int i = 0; i < count; i++)
                                    {
                                        ushort uX = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;
                                        ushort uY = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;
                                        ushort uZ = Utils.BytesToUInt16(posBytes, posNdx); posNdx += 2;

                                        Vector3 pos = new Vector3(
                                            Utils.UInt16ToFloat(uX, min.X, max.X),
                                            Utils.UInt16ToFloat(uY, min.Y, max.Y),
                                            Utils.UInt16ToFloat(uZ, min.Z, max.Z)
                                        );

                                        hull.Add(pos);
                                    }

                                    hulls.Add(hull);
                                }

                                mConvexHulls = hulls;
                                if (debugDetail) m_log.DebugFormat("{0} prim='{1}': parsed hulls. nHulls={2}", LogHeader, primName, mConvexHulls.Count);
                            }
                            else
                            {
                                if (debugDetail) m_log.DebugFormat("{0} prim='{1}' has physics_convex but no HullList", LogHeader, primName);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("{0} exception decoding convex block: {1}", LogHeader, e);
                    }
                }

                if (physicsParms == null)
                {
                    m_log.WarnFormat("[MESH]: No recognized physics mesh found in mesh asset for {0}", primName);
                    return false;
                }

                int physOffset = physicsParms["offset"].AsInteger() + (int)start;
                int physSize = physicsParms["size"].AsInteger();

                if (physOffset < 0 || physSize == 0)
                    return false; // no mesh data in asset

                OSD decodedMeshOsd = new OSD();
                byte[] meshBytes = new byte[physSize];
                System.Buffer.BlockCopy(primShape.SculptData, physOffset, meshBytes, 0, physSize);
                //                        byte[] decompressed = new byte[physSize * 5];
                try
                {
                    decodedMeshOsd = DecompressOsd(meshBytes);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("{0} prim='{1}': exception decoding physical mesh: {2}", LogHeader, primName, e);
                    return false;
                }

                OSDArray decodedMeshOsdArray = null;

                // physics_shape is an array of OSDMaps, one for each submesh
                if (decodedMeshOsd is OSDArray)
                {
                    //                            Console.WriteLine("decodedMeshOsd for {0} - {1}", primName, Util.GetFormattedXml(decodedMeshOsd));

                    decodedMeshOsdArray = (OSDArray)decodedMeshOsd;
                    foreach (OSD subMeshOsd in decodedMeshOsdArray)
                    {
                        if (subMeshOsd is OSDMap)
                            AddSubMesh(subMeshOsd as OSDMap, size, coords, faces);
                    }
                    if (debugDetail)
                        m_log.DebugFormat("{0} {1}: mesh decoded. offset={2}, size={3}, nCoords={4}, nFaces={5}",
                                            LogHeader, primName, physOffset, physSize, coords.Count, faces.Count);
                }
            }

            return true;
        }

        /// <summary>
        /// decompresses a gzipped OSD object
        /// </summary>
        /// <param name="decodedOsd"></param> the OSD object
        /// <param name="meshBytes"></param>
        /// <returns></returns>
        private static OSD DecompressOsd(byte[] meshBytes)
        {
            OSD decodedOsd = null;

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

                        decodedOsd = OSDParser.DeserializeLLSDBinary(decompressedBuf);
                    }
                }
            }
            return decodedOsd;
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
            string primName, PrimitiveBaseShape primShape, Vector3 size, float lod, out List<Coord> coords, out List<Face> faces)
        {
            coords = new List<Coord>();
            faces = new List<Face>();
            PrimMesher.SculptMesh sculptMesh;
            Image idata = null;
            string decodedSculptFileName = "";

            if (cacheSculptMaps && primShape.SculptTexture != UUID.Zero)
            {
                decodedSculptFileName = System.IO.Path.Combine(decodedSculptMapPath, "smap_" + primShape.SculptTexture.ToString());
                try
                {
                    if (File.Exists(decodedSculptFileName))
                    {
                        idata = Image.FromFile(decodedSculptFileName);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[SCULPT]: unable to load cached sculpt map " + decodedSculptFileName + " " + e.Message);

                }
                //if (idata != null)
                //    m_log.Debug("[SCULPT]: loaded cached map asset for map ID: " + primShape.SculptTexture.ToString());
            }

            if (idata == null)
            {
                if (primShape.SculptData == null || primShape.SculptData.Length == 0)
                    return false;

                try
                {
                    OpenMetaverse.Imaging.ManagedImage managedImage;

                    OpenMetaverse.Imaging.OpenJPEG.DecodeToImage(primShape.SculptData, out managedImage);

                    if (managedImage == null)
                    {
                        // In some cases it seems that the decode can return a null bitmap without throwing
                        // an exception
                        m_log.WarnFormat("[PHYSICS]: OpenJPEG decoded sculpt data for {0} to a null bitmap.  Ignoring.", primName);

                        return false;
                    }

                    if ((managedImage.Channels & OpenMetaverse.Imaging.ManagedImage.ImageChannels.Alpha) != 0)
                        managedImage.ConvertChannels(managedImage.Channels & ~OpenMetaverse.Imaging.ManagedImage.ImageChannels.Alpha);

                    Bitmap imgData = OpenMetaverse.Imaging.LoadTGAClass.LoadTGA(new MemoryStream(managedImage.ExportTGA()));
                    idata = (Image)imgData;
                    managedImage = null;

                    if (cacheSculptMaps)
                    {
                        try { idata.Save(decodedSculptFileName, ImageFormat.MemoryBmp); }
                        catch (Exception e) { m_log.Error("[SCULPT]: unable to cache sculpt map " + decodedSculptFileName + " " + e.Message); }
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
            }

            PrimMesher.SculptMesh.SculptType sculptType;
            switch ((OpenMetaverse.SculptType)primShape.SculptType)
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

            sculptMesh = new PrimMesher.SculptMesh((Bitmap)idata, sculptType, (int)lod, false, mirror, invert);

            idata.Dispose();

            sculptMesh.DumpRaw(baseDir, primName, "primMesh");

            sculptMesh.Scale(size.X, size.Y, size.Z);

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
            string primName, PrimitiveBaseShape primShape, Vector3 size, float lod, out List<Coord> coords, out List<Face> faces)
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
            float profileHollow = (float)primShape.ProfileHollow * 2.0e-5f;
            if (profileHollow > 0.95f)
                profileHollow = 0.95f;

            int sides = 4;
            LevelOfDetail iLOD = (LevelOfDetail)lod;
            if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
                sides = 3;
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
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
            else if ((primShape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
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
                hollowSides = 3;

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
                primMesh.twistBegin = primShape.PathTwistBegin * 18 / 10;
                primMesh.twistEnd = primShape.PathTwist * 18 / 10;
                primMesh.taperX = pathScaleX;
                primMesh.taperY = pathScaleY;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    ReportPrimError("*** CORRUPT PRIM!! ***", primName, primMesh);
                    if (profileBegin < 0.0f) profileBegin = 0.0f;
                    if (profileEnd > 1.0f) profileEnd = 1.0f;
                }
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
                primMesh.twistBegin = primShape.PathTwistBegin * 36 / 10;
                primMesh.twistEnd = primShape.PathTwist * 36 / 10;
                primMesh.taperX = primShape.PathTaperX * 0.01f;
                primMesh.taperY = primShape.PathTaperY * 0.01f;

                if (profileBegin < 0.0f || profileBegin >= profileEnd || profileEnd > 1.0f)
                {
                    ReportPrimError("*** CORRUPT PRIM!! ***", primName, primMesh);
                    if (profileBegin < 0.0f) profileBegin = 0.0f;
                    if (profileEnd > 1.0f) profileEnd = 1.0f;
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

            primMesh.DumpRaw(baseDir, primName, "primMesh");

            primMesh.Scale(size.X, size.Y, size.Z);

            coords = primMesh.coords;
            faces = primMesh.faces;

            return true;
        }

        /// <summary>
        /// temporary prototype code - please do not use until the interface has been finalized!
        /// </summary>
        /// <param name="size">value to scale the hull points by</param>
        /// <returns>a list of vertices in the bounding hull if it exists and has been successfully decoded, otherwise null</returns>
        public List<Vector3> GetBoundingHull(Vector3 size)
        {
            if (mBoundingHull == null)
                return null;

            List<Vector3> verts = new List<Vector3>();
            foreach (var vert in mBoundingHull)
                verts.Add(vert * size);

            return verts;
        }

        /// <summary>
        /// temporary prototype code - please do not use until the interface has been finalized!
        /// </summary>
        /// <param name="size">value to scale the hull points by</param>
        /// <returns>a list of hulls if they exist and have been successfully decoded, otherwise null</returns>
        public List<List<Vector3>> GetConvexHulls(Vector3 size)
        {
            if (mConvexHulls == null)
                return null;

            List<List<Vector3>> hulls = new List<List<Vector3>>();
            foreach (var hull in mConvexHulls)
            {
                List<Vector3> verts = new List<Vector3>();
                foreach (var vert in hull)
                    verts.Add(vert * size);
                hulls.Add(verts);
            }

            return hulls;
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod)
        {
            return CreateMesh(primName, primShape, size, lod, false, true);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool shouldCache, bool convex, bool forOde)
        {
            return CreateMesh(primName, primShape, size, lod, false);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical)
        {
            return CreateMesh(primName, primShape, size, lod, isPhysical, true);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex, bool forOde)
        {
            return CreateMesh(primName, primShape, size, lod, isPhysical, true);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool shouldCache)
        {
#if SPAM
            m_log.DebugFormat("[MESH]: Creating mesh for {0}", primName);
#endif

            Mesh mesh = null;
            ulong key = 0;

            // If this mesh has been created already, return it instead of creating another copy
            // For large regions with 100k+ prims and hundreds of copies of each, this can save a GB or more of memory
            if (shouldCache)
            {
                key = primShape.GetMeshKey(size, lod);
                lock (m_uniqueMeshes)
                {
                    if (m_uniqueMeshes.TryGetValue(key, out mesh))
                        return mesh;
                }
            }

            if (size.X < 0.01f) size.X = 0.01f;
            if (size.Y < 0.01f) size.Y = 0.01f;
            if (size.Z < 0.01f) size.Z = 0.01f;

            mesh = CreateMeshFromPrimMesher(primName, primShape, size, lod);

            if (mesh != null)
            {
                if ((!isPhysical) && size.X < minSizeForComplexMesh && size.Y < minSizeForComplexMesh && size.Z < minSizeForComplexMesh)
                {
#if SPAM
                m_log.Debug("Meshmerizer: prim " + primName + " has a size of " + size.ToString() + " which is below threshold of " + 
                            minSizeForComplexMesh.ToString() + " - creating simple bounding box");
#endif
                    mesh = CreateBoundingBoxMesh(mesh);
                    mesh.DumpRaw(baseDir, primName, "Z extruded");
                }

                // trim the vertex and triangle lists to free up memory
                mesh.TrimExcess();

                if (shouldCache)
                {
                    lock (m_uniqueMeshes)
                    {
                        m_uniqueMeshes.Add(key, mesh);
                    }
                }
            }

            return mesh;
        }
        public IMesh GetMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex)
        {
            return null;
        }

        public void ReleaseMesh(IMesh imesh) { }
        public void ExpireReleaseMeshs() { }
        public void ExpireFileCache() { }
    }
}
