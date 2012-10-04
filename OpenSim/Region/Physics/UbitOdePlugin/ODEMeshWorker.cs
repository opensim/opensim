/*
 * AJLDuarte 2012
 */

using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OdeAPI;
using log4net;
using Nini.Config;
using OpenMetaverse;

namespace OpenSim.Region.Physics.OdePlugin
{
    public class ODEMeshWorker
    {
        private ILog m_log;
        private OdeScene m_scene;
        private IMesher m_mesher;


        public bool meshSculptedPrim = true;
        public bool forceSimplePrimMeshing = false;
        public float meshSculptLOD = 32;
        public float MeshSculptphysicalLOD = 32;

        private IntPtr m_workODEspace = IntPtr.Zero;

        public ODEMeshWorker(OdeScene pScene, ILog pLog, IMesher pMesher, IntPtr pWorkSpace, IConfig pConfig)
        {
            m_scene = pScene;
            m_log = pLog;
            m_mesher = pMesher;
            m_workODEspace = pWorkSpace;

            if (pConfig != null)
            {
                forceSimplePrimMeshing = pConfig.GetBoolean("force_simple_prim_meshing", forceSimplePrimMeshing);
                meshSculptedPrim = pConfig.GetBoolean("mesh_sculpted_prim", meshSculptedPrim);
                meshSculptLOD = pConfig.GetFloat("mesh_lod", meshSculptLOD);
                MeshSculptphysicalLOD = pConfig.GetFloat("mesh_physical_lod", MeshSculptphysicalLOD);
            }
        }

        /// <summary>
        /// Routine to figure out if we need to mesh this prim with our mesher
        /// </summary>
        /// <param name="pbs"></param>
        /// <returns></returns>
        public bool needsMeshing(PrimitiveBaseShape pbs)
        {
            // check sculpts or meshs 
            if (pbs.SculptEntry)
            {
                if (meshSculptedPrim)
                    return true;

                if (pbs.SculptType == (byte)SculptType.Mesh) // always do meshs
                    return true;

                return false;
            }

            if (forceSimplePrimMeshing)
                return true;

            // if it's a standard box or sphere with no cuts, hollows, twist or top shear, return false since ODE can use an internal representation for the prim

            if ((pbs.ProfileShape == ProfileShape.Square && pbs.PathCurve == (byte)Extrusion.Straight)
                    || (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1
                    && pbs.Scale.X == pbs.Scale.Y && pbs.Scale.Y == pbs.Scale.Z))
            {

                if (pbs.ProfileBegin == 0 && pbs.ProfileEnd == 0
                    && pbs.ProfileHollow == 0
                    && pbs.PathTwist == 0 && pbs.PathTwistBegin == 0
                    && pbs.PathBegin == 0 && pbs.PathEnd == 0
                    && pbs.PathTaperX == 0 && pbs.PathTaperY == 0
                    && pbs.PathScaleX == 100 && pbs.PathScaleY == 100
                    && pbs.PathShearX == 0 && pbs.PathShearY == 0)
                {
                    return false;
                }
            }

            //  following code doesn't give meshs to boxes and spheres ever
            // and it's odd..  so for now just return true if asked to force meshs
            // hopefully mesher will fail if doesn't suport so things still get basic boxes

            int iPropertiesNotSupportedDefault = 0;

            if (pbs.ProfileHollow != 0)
                iPropertiesNotSupportedDefault++;

            if ((pbs.PathBegin != 0) || pbs.PathEnd != 0)
                iPropertiesNotSupportedDefault++;

            if ((pbs.PathTwistBegin != 0) || (pbs.PathTwist != 0))
                iPropertiesNotSupportedDefault++;

            if ((pbs.ProfileBegin != 0) || pbs.ProfileEnd != 0)
                iPropertiesNotSupportedDefault++;

            if ((pbs.PathScaleX != 100) || (pbs.PathScaleY != 100))
                iPropertiesNotSupportedDefault++;

            if ((pbs.PathShearX != 0) || (pbs.PathShearY != 0))
                iPropertiesNotSupportedDefault++;

            if (pbs.ProfileShape == ProfileShape.Circle && pbs.PathCurve == (byte)Extrusion.Straight)
                iPropertiesNotSupportedDefault++;

            if (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1 && (pbs.Scale.X != pbs.Scale.Y || pbs.Scale.Y != pbs.Scale.Z || pbs.Scale.Z != pbs.Scale.X))
                iPropertiesNotSupportedDefault++;

            if (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1)
                iPropertiesNotSupportedDefault++;

            // test for torus
            if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.Square)
            {
                if (pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }
            else if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
            {
                if (pbs.PathCurve == (byte)Extrusion.Straight)
                {
                    iPropertiesNotSupportedDefault++;
                }

                // ProfileCurve seems to combine hole shape and profile curve so we need to only compare against the lower 3 bits
                else if (pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }
            else if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
            {
                if (pbs.PathCurve == (byte)Extrusion.Curve1 || pbs.PathCurve == (byte)Extrusion.Curve2)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }
            else if ((pbs.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
            {
                if (pbs.PathCurve == (byte)Extrusion.Straight)
                {
                    iPropertiesNotSupportedDefault++;
                }
                else if (pbs.PathCurve == (byte)Extrusion.Curve1)
                {
                    iPropertiesNotSupportedDefault++;
                }
            }

            if (iPropertiesNotSupportedDefault == 0)
            {
                return false;
            }
            return true;
        }

        public IMesh getMesh(PhysicsActor actor, PrimitiveBaseShape ppbs, Vector3 psize, byte pshapetype)
        {
            if (!(actor is OdePrim))
                return null;

            IMesh mesh = null;
            PrimitiveBaseShape pbs = ppbs;
            Vector3 size = psize;
            byte shapetype = pshapetype;

            if (needsMeshing(pbs))
            {
                bool convex;
                int clod = (int)LevelOfDetail.High;
                if (shapetype == 0)
                    convex = false;
                else
                {
                    convex = true;
                    if (pbs.SculptType != (byte)SculptType.Mesh)
                        clod = (int)LevelOfDetail.Low;
                }
                mesh = m_mesher.GetMesh(actor.Name, pbs, size, clod, true, convex);
                if (mesh == null)
                {
                    if (!pbs.SculptEntry)
                        return m_mesher.CreateMesh(actor.Name, pbs, size, clod, true, convex);

                    if (pbs.SculptTexture == UUID.Zero)
                        return null;

                    if (pbs.SculptType != (byte)SculptType.Mesh)
                    { // check for sculpt decoded image on cache)
                        if (File.Exists(System.IO.Path.Combine("j2kDecodeCache", "smap_" + pbs.SculptTexture.ToString())))
                            return m_mesher.CreateMesh(actor.Name, pbs, size, clod, true, convex);
                    }

                    if (pbs.SculptData != null && pbs.SculptData.Length > 0)
                        return m_mesher.CreateMesh(actor.Name, pbs, size, clod, true, convex);

                    ODEAssetRequest asr;
                    RequestAssetDelegate assetProvider = m_scene.RequestAssetMethod;
                    if (assetProvider != null)
                        asr = new ODEAssetRequest(this, assetProvider, actor, pbs, m_log);

                    return null;
                }
            }
            return mesh;
        }

        private bool GetTriMeshGeo(ODEPhysRepData repData)
        {
            IntPtr vertices, indices;
            IntPtr triMeshData = IntPtr.Zero;
            IntPtr geo = IntPtr.Zero;
            int vertexCount, indexCount;
            int vertexStride, triStride;

            PhysicsActor actor = repData.actor;

            IMesh mesh = repData.mesh;

            if (mesh == null)
            {
                mesh = getMesh(repData.actor, repData.pbs, repData.size, repData.shapetype);
            }

            if (mesh == null)
                return false;

            mesh.getVertexListAsPtrToFloatArray(out vertices, out vertexStride, out vertexCount); // Note, that vertices are fixed in unmanaged heap
            mesh.getIndexListAsPtrToIntArray(out indices, out triStride, out indexCount); // Also fixed, needs release after usage

            if (vertexCount == 0 || indexCount == 0)
            {
                m_log.WarnFormat("[PHYSICS]: Invalid mesh data on prim {0} mesh UUID {1}",
                    actor.Name, repData.pbs.SculptTexture.ToString());
                mesh.releaseSourceMeshData();
                return false;
            }

            repData.OBBOffset = mesh.GetCentroid();
            repData.OBB = mesh.GetOBB();
            repData.hasOBB = true;
            repData.physCost = 0.0013f * (float)indexCount;

            mesh.releaseSourceMeshData();

            try
            {
                triMeshData = d.GeomTriMeshDataCreate();

                d.GeomTriMeshDataBuildSimple(triMeshData, vertices, vertexStride, vertexCount, indices, indexCount, triStride);
                d.GeomTriMeshDataPreprocess(triMeshData);

                m_scene.waitForSpaceUnlock(m_workODEspace);
                geo = d.CreateTriMesh(m_workODEspace, triMeshData, null, null, null);
            }

            catch (Exception e)
            {
                m_log.ErrorFormat("[PHYSICS]: SetGeom Mesh failed for {0} exception: {1}", actor.Name, e);
                if (triMeshData != IntPtr.Zero)
                {
                    d.GeomTriMeshDataDestroy(triMeshData);
                    repData.triMeshData = IntPtr.Zero;
                }
                repData.geo = IntPtr.Zero;
                return false;
            }

            repData.geo = geo;
            repData.triMeshData = triMeshData;
            repData.curSpace = m_workODEspace;
            return true;
        }

        public ODEPhysRepData CreateActorPhysRep(PhysicsActor actor, PrimitiveBaseShape pbs, IMesh pMesh, Vector3 size, byte shapetype)
        {
            ODEPhysRepData repData = new ODEPhysRepData();

            repData.actor = actor;
            repData.pbs = pbs;
            repData.mesh = pMesh;
            repData.size = size;
            repData.shapetype = shapetype;

            IntPtr geo = IntPtr.Zero;
            bool hasMesh = false;
            if (needsMeshing(pbs))
            {
                if (GetTriMeshGeo(repData))
                    hasMesh = true;
                else
                    repData.canColide = false;
            }

            if (!hasMesh)
            {
                if (pbs.ProfileShape == ProfileShape.HalfCircle && pbs.PathCurve == (byte)Extrusion.Curve1
                    && size.X == size.Y && size.Y == size.Z)
                { // it's a sphere
                    m_scene.waitForSpaceUnlock(m_workODEspace);
                    try
                    {
                        geo = d.CreateSphere(m_workODEspace, size.X * 0.5f);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[PHYSICS]: Create sphere failed: {0}", e);
                        return null;
                    }
                }
                else
                {// do it as a box
                    m_scene.waitForSpaceUnlock(m_workODEspace);
                    try
                    {
                        //Console.WriteLine("  CreateGeom 4");
                        geo = d.CreateBox(m_workODEspace, size.X, size.Y, size.Z);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[PHYSICS]: Create box failed: {0}", e);
                        return null;
                    }
                }

                repData.physCost = 0.1f;
                repData.streamCost = 1.0f;
                repData.geo = geo;
            }
            
            repData.curSpace = m_workODEspace;

            CalcVolumeData(repData);

            return repData;
        }

        private void CalculateBasicPrimVolume(ODEPhysRepData repData)
        {
            PrimitiveBaseShape _pbs = repData.pbs;
            Vector3 _size = repData.size;

            float volume = _size.X * _size.Y * _size.Z; // default
            float tmp;

            float hollowAmount = (float)_pbs.ProfileHollow * 2.0e-5f;
            float hollowVolume = hollowAmount * hollowAmount;

            switch (_pbs.ProfileShape)
            {
                case ProfileShape.Square:
                    // default box

                    if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        if (hollowAmount > 0.0)
                        {
                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Square:
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Circle:

                                    hollowVolume *= 0.78539816339f;
                                    break;

                                case HollowShape.Triangle:

                                    hollowVolume *= (0.5f * .5f);
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        //a tube 

                        volume *= 0.78539816339e-2f * (float)(200 - _pbs.PathScaleX);
                        tmp = 1.0f - 2.0e-2f * (float)(200 - _pbs.PathScaleY);
                        volume -= volume * tmp * tmp;

                        if (hollowAmount > 0.0)
                        {
                            hollowVolume *= hollowAmount;

                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Square:
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Circle:
                                    hollowVolume *= 0.78539816339f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= 0.5f * 0.5f;
                                    break;
                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    break;

                case ProfileShape.Circle:

                    if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        volume *= 0.78539816339f; // elipse base

                        if (hollowAmount > 0.0)
                        {
                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Circle:
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.5f * 2.5984480504799f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= .5f * 1.27323954473516f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }

                    else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.61685027506808491367715568749226e-2f * (float)(200 - _pbs.PathScaleX);
                        tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                        volume *= (1.0f - tmp * tmp);

                        if (hollowAmount > 0.0)
                        {

                            // calculate the hollow volume by it's shape compared to the prim shape
                            hollowVolume *= hollowAmount;

                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Circle:
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.5f * 2.5984480504799f;
                                    break;

                                case HollowShape.Triangle:
                                    hollowVolume *= .5f * 1.27323954473516f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

                case ProfileShape.HalfCircle:
                    if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.5236f;

                        if (hollowAmount > 0.0)
                        {
                            hollowVolume *= hollowAmount;

                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Circle:
                                case HollowShape.Triangle:  // diference in sl is minor and odd
                                case HollowShape.Same:
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.909f;
                                    break;

                                //                                case HollowShape.Triangle:
                                //                                    hollowVolume *= .827f;
                                //                                    break;
                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }

                    }
                    break;

                case ProfileShape.EquilateralTriangle:

                    if (_pbs.PathCurve == (byte)Extrusion.Straight)
                    {
                        volume *= 0.32475953f;

                        if (hollowAmount > 0.0)
                        {

                            // calculate the hollow volume by it's shape compared to the prim shape
                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Triangle:
                                    hollowVolume *= .25f;
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.499849f * 3.07920140172638f;
                                    break;

                                case HollowShape.Circle:
                                    // Hollow shape is a perfect cyllinder in respect to the cube's scale
                                    // Cyllinder hollow volume calculation

                                    hollowVolume *= 0.1963495f * 3.07920140172638f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    else if (_pbs.PathCurve == (byte)Extrusion.Curve1)
                    {
                        volume *= 0.32475953f;
                        volume *= 0.01f * (float)(200 - _pbs.PathScaleX);
                        tmp = 1.0f - .02f * (float)(200 - _pbs.PathScaleY);
                        volume *= (1.0f - tmp * tmp);

                        if (hollowAmount > 0.0)
                        {

                            hollowVolume *= hollowAmount;

                            switch (_pbs.HollowShape)
                            {
                                case HollowShape.Same:
                                case HollowShape.Triangle:
                                    hollowVolume *= .25f;
                                    break;

                                case HollowShape.Square:
                                    hollowVolume *= 0.499849f * 3.07920140172638f;
                                    break;

                                case HollowShape.Circle:

                                    hollowVolume *= 0.1963495f * 3.07920140172638f;
                                    break;

                                default:
                                    hollowVolume = 0;
                                    break;
                            }
                            volume *= (1.0f - hollowVolume);
                        }
                    }
                    break;

                default:
                    break;
            }

            float taperX1;
            float taperY1;
            float taperX;
            float taperY;
            float pathBegin;
            float pathEnd;
            float profileBegin;
            float profileEnd;

            if (_pbs.PathCurve == (byte)Extrusion.Straight || _pbs.PathCurve == (byte)Extrusion.Flexible)
            {
                taperX1 = _pbs.PathScaleX * 0.01f;
                if (taperX1 > 1.0f)
                    taperX1 = 2.0f - taperX1;
                taperX = 1.0f - taperX1;

                taperY1 = _pbs.PathScaleY * 0.01f;
                if (taperY1 > 1.0f)
                    taperY1 = 2.0f - taperY1;
                taperY = 1.0f - taperY1;
            }
            else
            {
                taperX = _pbs.PathTaperX * 0.01f;
                if (taperX < 0.0f)
                    taperX = -taperX;
                taperX1 = 1.0f - taperX;

                taperY = _pbs.PathTaperY * 0.01f;
                if (taperY < 0.0f)
                    taperY = -taperY;
                taperY1 = 1.0f - taperY;
            }

            volume *= (taperX1 * taperY1 + 0.5f * (taperX1 * taperY + taperX * taperY1) + 0.3333333333f * taperX * taperY);

            pathBegin = (float)_pbs.PathBegin * 2.0e-5f;
            pathEnd = 1.0f - (float)_pbs.PathEnd * 2.0e-5f;
            volume *= (pathEnd - pathBegin);

            // this is crude aproximation
            profileBegin = (float)_pbs.ProfileBegin * 2.0e-5f;
            profileEnd = 1.0f - (float)_pbs.ProfileEnd * 2.0e-5f;
            volume *= (profileEnd - profileBegin);           

            repData.volume = volume;
        }

        private void CalcVolumeData(ODEPhysRepData repData)
        {
            float volume;
            Vector3 OBB = repData.size;
            Vector3 OBBoffset;
            IntPtr geo = repData.geo;

            if (geo == IntPtr.Zero || repData.triMeshData == IntPtr.Zero)
            {
                OBB.X *= 0.5f;
                OBB.Y *= 0.5f;
                OBB.Z *= 0.5f;

                repData.OBB = OBB;
                repData.OBBOffset = Vector3.Zero;
            }
            else if (!repData.hasOBB) // should this happen?
            {
                d.AABB AABB;
                d.GeomGetAABB(geo, out AABB); // get the AABB from engine geom

                OBB.X = (AABB.MaxX - AABB.MinX) * 0.5f;
                OBB.Y = (AABB.MaxY - AABB.MinY) * 0.5f;
                OBB.Z = (AABB.MaxZ - AABB.MinZ) * 0.5f;
                repData.OBB = OBB;
                OBBoffset.X = (AABB.MaxX + AABB.MinX) * 0.5f;
                OBBoffset.Y = (AABB.MaxY + AABB.MinY) * 0.5f;
                OBBoffset.Z = (AABB.MaxZ + AABB.MinZ) * 0.5f;
                repData.OBBOffset = Vector3.Zero;
            }

            // also its own inertia and mass
            // keep using basic shape mass for now
            CalculateBasicPrimVolume(repData);

            if (repData.hasOBB)
            {
                OBB = repData.OBB;
                float pc = repData.physCost;
                float psf = OBB.X * (OBB.Y + OBB.Z) + OBB.Y * OBB.Z;
                psf *= 1.33f * .2f;

                pc *= psf;
                if (pc < 0.1f)
                    pc = 0.1f;

                repData.physCost = pc;
            }
            else
                repData.physCost = 0.1f;
        }
    }

    public class ODEAssetRequest
    {
        PhysicsActor m_actor;
        ODEMeshWorker m_worker;
        PrimitiveBaseShape m_pbs;
        private ILog m_log;

        public ODEAssetRequest(ODEMeshWorker pWorker, RequestAssetDelegate provider,
            PhysicsActor pActor, PrimitiveBaseShape ppbs, ILog plog)
        {
            m_actor = pActor;
            m_worker = pWorker;
            m_pbs = ppbs;
            m_log = plog;

            if (provider == null)
                return;

            UUID assetID = m_pbs.SculptTexture;
            if (assetID == UUID.Zero)
                return;

            provider(assetID, ODEassetReceived);
        }

        void ODEassetReceived(AssetBase asset)
        {
            if (m_actor != null && m_pbs != null)
            {
                if (asset != null)
                {
                    if (asset.Data != null && asset.Data.Length > 0)
                    {
                        m_pbs.SculptData = asset.Data;
                        m_actor.Shape = m_pbs;
                    }
                    else
                        m_log.WarnFormat("[PHYSICS]: asset provider returned invalid mesh data for prim {0} asset UUID {1}.",
                            m_actor.Name, asset.ID.ToString());
                }
                else
                    m_log.WarnFormat("[PHYSICS]: asset provider returned null asset fo mesh of prim {0}.",
                        m_actor.Name);
            }
        }
    }
}