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

                    if(pbs.SculptType != (byte)SculptType.Mesh)
                    { // check for sculpt decoded image on cache)
                        if (File.Exists(System.IO.Path.Combine("j2kDecodeCache", "smap_" + pbs.SculptTexture.ToString())))
                            return m_mesher.CreateMesh(actor.Name, pbs, size, clod, true, convex);
                    }

                    if(pbs.SculptData != null && pbs.SculptData.Length >0)
                        return m_mesher.CreateMesh(actor.Name, pbs, size, clod, true, convex);

                    ODEAssetRequest asr;
                    RequestAssetDelegate assetProvider = m_scene.RequestAssetMethod;
                    if (assetProvider != null)
                        asr = new ODEAssetRequest(this, assetProvider, actor, pbs);
                    return null;
                }
            }
            return mesh;
        }
    }

    public class ODEAssetRequest
    {
        PhysicsActor m_actor;
        ODEMeshWorker m_worker;
        PrimitiveBaseShape m_pbs;

        public ODEAssetRequest(ODEMeshWorker pWorker, RequestAssetDelegate provider, PhysicsActor pActor, PrimitiveBaseShape ppbs)
        {
            m_actor = pActor;
            m_worker = pWorker;
            m_pbs = ppbs;

            if (provider == null)
                return;

            UUID assetID = m_pbs.SculptTexture;
            if (assetID == UUID.Zero)
                return;

            provider(assetID, ODEassetReceived);
        }

        void ODEassetReceived(AssetBase asset)
        {
            if (m_actor != null && m_pbs != null && asset != null && asset.Data != null && asset.Data.Length > 0)
            {
                m_pbs.SculptData = asset.Data;
                m_actor.Shape = m_pbs;
            }
        }
    }
}