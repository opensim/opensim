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
    public enum meshWorkerCmnds : byte
    {
        nop = 0,
        addnew,
        changefull,
        changesize,
        changeshapetype,
        getmesh,
    }

    public class ODEPhysRepData
    {
        public PhysicsActor actor;
        public PrimitiveBaseShape pbs;
        public IMesh mesh;

        public Vector3 size;
        public Vector3 OBB;
        public Vector3 OBBOffset;

        public float volume;

        public float physCost;
        public float streamCost;
        public byte shapetype;
        public bool hasOBB;
        public bool hasMeshVolume;
        public AssetState assetState;
        public UUID? assetID;
        public meshWorkerCmnds comand;
    }



    public class ODEMeshWorker
    {

        private ILog m_log;
        private OdeScene m_scene;
        private IMesher m_mesher;

        public bool meshSculptedPrim = true;
        public bool forceSimplePrimMeshing = false;
        public float meshSculptLOD = 32;
        public float MeshSculptphysicalLOD = 32;


        private OpenSim.Framework.BlockingQueue<ODEPhysRepData> createqueue = new OpenSim.Framework.BlockingQueue<ODEPhysRepData>();
        private bool m_running;

        private Thread m_thread;

        public ODEMeshWorker(OdeScene pScene, ILog pLog, IMesher pMesher, IConfig pConfig)
        {
            m_scene = pScene;
            m_log = pLog;
            m_mesher = pMesher;

            if (pConfig != null)
            {
                forceSimplePrimMeshing = pConfig.GetBoolean("force_simple_prim_meshing", forceSimplePrimMeshing);
                meshSculptedPrim = pConfig.GetBoolean("mesh_sculpted_prim", meshSculptedPrim);
                meshSculptLOD = pConfig.GetFloat("mesh_lod", meshSculptLOD);
                MeshSculptphysicalLOD = pConfig.GetFloat("mesh_physical_lod", MeshSculptphysicalLOD);
            }
            m_running = true;
            m_thread = new Thread(DoWork);
            m_thread.Start();
        }

        private void DoWork()
        {
            while(m_running)
            {
                 ODEPhysRepData nextRep = createqueue.Dequeue();
                if(!m_running)
                    return;
                if (nextRep == null)
                    continue;
                if (m_scene.haveActor(nextRep.actor))
                {
                    switch (nextRep.comand)
                    {
                        case meshWorkerCmnds.changefull:
                        case meshWorkerCmnds.changeshapetype:
                        case meshWorkerCmnds.changesize:
                            if (CreateActorPhysRep(nextRep) && m_scene.haveActor(nextRep.actor))
                                m_scene.AddChange(nextRep.actor, changes.PhysRepData, nextRep);
                            break;
                        case meshWorkerCmnds.addnew:
                            if (CreateActorPhysRep(nextRep))
                                m_scene.AddChange(nextRep.actor, changes.AddPhysRep, nextRep);
                            break;
                        case meshWorkerCmnds.getmesh:
                            DoRepDataGetMesh(nextRep);
                            break;
                    }
                }
            }
        }

        public void Stop()
        {
            m_running = false;
            m_thread.Abort();
        }

        public void ChangeActorPhysRep(PhysicsActor actor, PrimitiveBaseShape pbs,
                                        Vector3 size, byte shapetype)
        {
            ODEPhysRepData repData = new ODEPhysRepData();
            repData.actor = actor;
            repData.pbs = pbs;
            repData.size = size;
            repData.shapetype = shapetype;

            //            if (CheckMeshDone(repData))
            {
                CheckMeshDone(repData);
                CalcVolumeData(repData);
                m_scene.AddChange(actor, changes.PhysRepData, repData);
                return;
            }

//            repData.comand = meshWorkerCmnds.changefull;
//            createqueue.Enqueue(repData);
        }

        public void NewActorPhysRep(PhysicsActor actor, PrimitiveBaseShape pbs,
                                        Vector3 size, byte shapetype)
        {
            ODEPhysRepData repData = new ODEPhysRepData();
            repData.actor = actor;
            repData.pbs = pbs;
            repData.size = size;
            repData.shapetype = shapetype;

            //            bool done = CheckMeshDone(repData);

            CheckMeshDone(repData);
            CalcVolumeData(repData);
            m_scene.AddChange(actor, changes.AddPhysRep, repData);
//            if (done)
                return;

//            repData.comand = meshWorkerCmnds.addnew;
//            createqueue.Enqueue(repData);
        }

        public void RequestMeshAsset(ODEPhysRepData repData)
        {
            if (repData.assetState != AssetState.needAsset)
                return;

            if (repData.assetID == null || repData.assetID == UUID.Zero)
                return;

            repData.mesh = null;

            repData.assetState = AssetState.loadingAsset;

            repData.comand = meshWorkerCmnds.getmesh;
            createqueue.Enqueue(repData);
        }

        public bool CreateActorPhysRep(ODEPhysRepData repData)
        {
            getMesh(repData);
            IMesh mesh = repData.mesh;

            if (mesh != null)
            {
                IntPtr vertices, indices;
                int vertexCount, indexCount;
                int vertexStride, triStride;

                mesh.getVertexListAsPtrToFloatArray(out vertices, out vertexStride, out vertexCount);
                mesh.getIndexListAsPtrToIntArray(out indices, out triStride, out indexCount);

                if (vertexCount == 0 || indexCount == 0)
                {
                    m_log.WarnFormat("[PHYSICS]: Invalid mesh data on prim {0} mesh UUID {1}",
                        repData.actor.Name, repData.pbs.SculptTexture.ToString());
                    repData.assetState = AssetState.AssetFailed;
                    repData.hasOBB = false;
                    repData.mesh = null;
                    m_scene.mesher.ReleaseMesh(mesh);
                }
                else
                {
                    repData.OBBOffset = mesh.GetCentroid();
                    repData.OBB = mesh.GetOBB();
                    repData.hasOBB = true;
                    repData.physCost = 0.0013f * (float)indexCount;
                    // todo
                    repData.streamCost = 1.0f;
                    mesh.releaseSourceMeshData();
                }
            }
            CalcVolumeData(repData);
            return true;
        }

        public void AssetLoaded(ODEPhysRepData repData)
        {
            if (m_scene.haveActor(repData.actor))
            {
                if (needsMeshing(repData.pbs)) // no need for pbs now?
                {
                    repData.comand = meshWorkerCmnds.changefull;
                    createqueue.Enqueue(repData);
                }
            }
        }

        public void DoRepDataGetMesh(ODEPhysRepData repData)
        {
            if (!repData.pbs.SculptEntry)
                return;

            if (repData.assetState != AssetState.loadingAsset)
                return;

            if (repData.assetID == null || repData.assetID == UUID.Zero)
                return;

            if (repData.assetID != repData.pbs.SculptTexture)
                return;

            RequestAssetDelegate assetProvider = m_scene.RequestAssetMethod;
            if (assetProvider == null)
                return;
            ODEAssetRequest asr = new ODEAssetRequest(this, assetProvider, repData, m_log);
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

        public bool CheckMeshDone(ODEPhysRepData repData)
        {
            PhysicsActor actor = repData.actor;
            PrimitiveBaseShape pbs = repData.pbs;

            repData.mesh = null;
            repData.hasOBB = false;

            if (!needsMeshing(pbs))
            {
                repData.assetState = AssetState.noNeedAsset;
                return true;
            }

            if (pbs.SculptEntry)
            {
                if (repData.assetState == AssetState.AssetFailed)
                {
                    if (pbs.SculptTexture == repData.assetID)
                        return true;
                }
            }
            else
            {
                repData.assetState = AssetState.noNeedAsset;
                repData.assetID = null;
            }

            IMesh mesh = null;

            Vector3 size = repData.size;
            byte shapetype = repData.shapetype;

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
                if (pbs.SculptEntry)
                {
                    if (pbs.SculptTexture != null && pbs.SculptTexture != UUID.Zero)
                    {
                        repData.assetID = pbs.SculptTexture;
                        repData.assetState = AssetState.needAsset;
                    }
                    else
                        repData.assetState = AssetState.AssetFailed;
                }
                return false;
            }

            repData.mesh = mesh;
            if (pbs.SculptEntry)
            {
                repData.assetState = AssetState.AssetOK;
                repData.assetID = pbs.SculptTexture;
                pbs.SculptData = Utils.EmptyBytes;
            }
            return true;
        }


        public bool getMesh(ODEPhysRepData repData)
        {
            PhysicsActor actor = repData.actor;

            PrimitiveBaseShape pbs = repData.pbs;

            repData.mesh = null;
            repData.hasOBB = false;

            if (!needsMeshing(pbs))
                return false;

            if (pbs.SculptEntry)
            {
                if (repData.assetState == AssetState.AssetFailed)
                {
                    if (pbs.SculptTexture == repData.assetID)
                        return true;
                }
            }

            repData.assetState = AssetState.noNeedAsset;

            IMesh mesh = null;
            Vector3 size = repData.size;
            byte shapetype = repData.shapetype;

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
                if (pbs.SculptEntry)
                {
                    if (pbs.SculptTexture == UUID.Zero)
                        return false;

                    repData.assetID = pbs.SculptTexture;
                    repData.assetState = AssetState.AssetOK;

                    if (pbs.SculptData == null || pbs.SculptData.Length == 0)
                    {
                        repData.assetState = AssetState.needAsset;
                        return false;
                    }
                }

                mesh = m_mesher.CreateMesh(actor.Name, pbs, size, clod, true, convex);

            }

            repData.mesh = mesh;
            repData.pbs.SculptData = Utils.EmptyBytes;

            if (mesh == null)
            {
                if (pbs.SculptEntry)
                    repData.assetState = AssetState.AssetFailed;

                return false;
            }

            if (pbs.SculptEntry)
                repData.assetState = AssetState.AssetOK;

            return true;
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
            if (repData.hasOBB)
            {
                Vector3 OBB = repData.OBB;
                float pc = repData.physCost;
                float psf = OBB.X * (OBB.Y + OBB.Z) + OBB.Y * OBB.Z;
                psf *= 1.33f * .2f;
                pc *= psf;
                if (pc < 0.1f)
                    pc = 0.1f;

                repData.physCost = pc;
            }
            else
            {
                Vector3 OBB = repData.size;
                OBB.X *= 0.5f;
                OBB.Y *= 0.5f;
                OBB.Z *= 0.5f;

                repData.OBB = OBB;
                repData.OBBOffset = Vector3.Zero;

                repData.physCost = 0.1f;
                repData.streamCost = 1.0f;
            }

            CalculateBasicPrimVolume(repData);
        }
    }

    public class ODEAssetRequest
    {
        ODEMeshWorker m_worker;
        private ILog m_log;
        ODEPhysRepData repData;

        public ODEAssetRequest(ODEMeshWorker pWorker, RequestAssetDelegate provider,
            ODEPhysRepData pRepData, ILog plog)
        {
            m_worker = pWorker;
            m_log = plog;
            repData = pRepData;

            repData.assetState = AssetState.AssetFailed;
            if (provider == null)
                return;

            if (repData.assetID == null)
                return;

            UUID assetID = (UUID) repData.assetID;
            if (assetID == UUID.Zero)
                return;

            repData.assetState = AssetState.loadingAsset;
            provider(assetID, ODEassetReceived);
        }

        void ODEassetReceived(AssetBase asset)
        {
            repData.assetState = AssetState.AssetFailed;
            if (asset != null)
            {
                if (asset.Data != null && asset.Data.Length > 0)
                {
                    if (!repData.pbs.SculptEntry)
                        return;
                    if (repData.pbs.SculptTexture != repData.assetID)
                        return;

                    // asset get may return a pointer to the same asset data
                    // for similar prims and we destroy with it
                    // so waste a lot of time stressing gc and hoping it clears things
                    // TODO avoid this
                    repData.pbs.SculptData = new byte[asset.Data.Length];
                    asset.Data.CopyTo(repData.pbs.SculptData,0);
                    repData.assetState = AssetState.AssetOK;
                    m_worker.AssetLoaded(repData);
                }
                else
                    m_log.WarnFormat("[PHYSICS]: asset provider returned invalid mesh data for prim {0} asset UUID {1}.",
                        repData.actor.Name, asset.ID.ToString());
            }
            else
                m_log.WarnFormat("[PHYSICS]: asset provider returned null asset fo mesh of prim {0}.",
                    repData.actor.Name);
        }
    }
}