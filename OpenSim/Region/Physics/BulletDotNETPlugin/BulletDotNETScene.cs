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
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Threading;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenMetaverse;
using BulletDotNET;

namespace OpenSim.Region.Physics.BulletDotNETPlugin
{
    public class BulletDotNETScene : PhysicsScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private string m_sceneIdentifier = string.Empty;
        
        private List<BulletDotNETCharacter> m_characters = new List<BulletDotNETCharacter>();
        private List<BulletDotNETPrim> m_prims = new List<BulletDotNETPrim>();
        private List<BulletDotNETPrim> m_activePrims = new List<BulletDotNETPrim>();
        private List<PhysicsActor> m_taintedActors = new List<PhysicsActor>();
        private btDiscreteDynamicsWorld m_world;
        private btAxisSweep3 m_broadphase;
        private btCollisionConfiguration m_collisionConfiguration;
        private btConstraintSolver m_solver;
        private btCollisionDispatcher m_dispatcher;
        private btHeightfieldTerrainShape m_terrainShape;
        public btRigidBody TerrainBody;
        private btVector3 m_terrainPosition;
        private btVector3 m_gravity;
        public btMotionState m_terrainMotionState;
        public btTransform m_terrainTransform;
        public btVector3 VectorZero;
        public btQuaternion QuatIdentity;
        public btTransform TransZero;

        public float geomDefaultDensity = 10.000006836f;

        private float avPIDD = 65f;
        private float avPIDP = 21f;
        private float avCapRadius = 0.37f;
        private float avStandupTensor = 2000000f;
        private float avDensity = 80f;
        private float avHeightFudgeFactor = 0.52f;
        private float avMovementDivisorWalk = 1.8f;
        private float avMovementDivisorRun = 0.8f;

        // private float minimumGroundFlightOffset = 3f;

        public bool meshSculptedPrim = true;

        public float meshSculptLOD = 32;
        public float MeshSculptphysicalLOD = 16;

        public float bodyPIDD = 35f;
        public float bodyPIDG = 25;
        internal int geomCrossingFailuresBeforeOutofbounds = 4;

        public float bodyMotorJointMaxforceTensor = 2;

        public int bodyFramesAutoDisable = 20;

        public float WorldTimeStep = 10f/60f;
        public const float WorldTimeComp = 1/60f;
        public float gravityz = -9.8f;

        private float[] _origheightmap;    // Used for Fly height. Kitto Flora
        private bool usingGImpactAlgorithm = false;

        // private IConfigSource m_config;
        private readonly btVector3 worldAabbMin = new btVector3(-10f, -10f, 0);
        private readonly btVector3 worldAabbMax = new btVector3((int)Constants.RegionSize + 10f, (int)Constants.RegionSize + 10f, 9000);

        public IMesher mesher;
        private ContactAddedCallbackHandler m_CollisionInterface;

        public BulletDotNETScene(string sceneIdentifier)
        {
            // m_sceneIdentifier = sceneIdentifier;
            VectorZero = new btVector3(0, 0, 0);
            QuatIdentity = new btQuaternion(0, 0, 0, 1);
            TransZero = new btTransform(QuatIdentity, VectorZero);
            m_gravity = new btVector3(0, 0, gravityz);
            _origheightmap = new float[(int)Constants.RegionSize * (int)Constants.RegionSize];
            
        }

        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
            mesher = meshmerizer;
            // m_config = config;
            /*
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                m_log.Fatal("[BulletDotNET]: This configuration is not supported on *nix currently");
                Thread.Sleep(5000);
                Environment.Exit(0);
            }
            */
            m_broadphase = new btAxisSweep3(worldAabbMin, worldAabbMax, 16000);
            m_collisionConfiguration = new btDefaultCollisionConfiguration();
            m_solver = new btSequentialImpulseConstraintSolver();
            m_dispatcher = new btCollisionDispatcher(m_collisionConfiguration);
            m_world = new btDiscreteDynamicsWorld(m_dispatcher, m_broadphase, m_solver, m_collisionConfiguration);
            m_world.setGravity(m_gravity);
            //EnableCollisionInterface();
            

        }

        public override PhysicsActor AddAvatar(string avName, PhysicsVector position, PhysicsVector size, bool isFlying)
        {
            BulletDotNETCharacter chr = new BulletDotNETCharacter(avName, this, position, size, avPIDD, avPIDP,
                                                                  avCapRadius, avStandupTensor, avDensity,
                                                                  avHeightFudgeFactor, avMovementDivisorWalk,
                                                                  avMovementDivisorRun);
            m_characters.Add(chr);
            AddPhysicsActorTaint(chr);
            return chr;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            BulletDotNETCharacter chr = (BulletDotNETCharacter) actor;

            m_characters.Remove(chr);
            m_world.removeRigidBody(chr.Body);
            m_world.removeCollisionObject(chr.Body);

            chr.Remove();
            AddPhysicsActorTaint(chr);
            //chr = null;
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            if (prim is BulletDotNETPrim)
            {

                BulletDotNETPrim p = (BulletDotNETPrim)prim;

                p.setPrimForRemoval();
                AddPhysicsActorTaint(prim);
                //RemovePrimThreadLocked(p);
                
            }
        }

        private PhysicsActor AddPrim(String name, PhysicsVector position, PhysicsVector size, Quaternion rotation,
                                    IMesh mesh, PrimitiveBaseShape pbs, bool isphysical)
        {
            PhysicsVector pos = new PhysicsVector(position.X, position.Y, position.Z);
            //pos.X = position.X;
            //pos.Y = position.Y;
            //pos.Z = position.Z;
            PhysicsVector siz = new PhysicsVector();
            siz.X = size.X;
            siz.Y = size.Y;
            siz.Z = size.Z;
            Quaternion rot = rotation;

            BulletDotNETPrim newPrim;
            
            newPrim = new BulletDotNETPrim(name, this, pos, siz, rot, mesh, pbs, isphysical);

            //lock (m_prims)
            //    m_prims.Add(newPrim);
            

            return newPrim;
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position, PhysicsVector size, Quaternion rotation)
        {
            return AddPrimShape(primName, pbs, position, size, rotation, false);
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position, PhysicsVector size, Quaternion rotation, bool isPhysical)
        {
            PhysicsActor result;
            IMesh mesh = null;

            //switch (pbs.ProfileShape)
            //{
            //    case ProfileShape.Square:
            //         //support simple box & hollow box now; later, more shapes
            //        if (needsMeshing(pbs))
            //        {
            //            mesh = mesher.CreateMesh(primName, pbs, size, 32f, isPhysical);
            //        }

            //        break;
            //}

            if (needsMeshing(pbs))
                mesh = mesher.CreateMesh(primName, pbs, size, 32f, isPhysical);

            result = AddPrim(primName, position, size, rotation, mesh, pbs, isPhysical);

            return result;
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
            lock (m_taintedActors)
            {
                if (!m_taintedActors.Contains(prim))
                {
                    m_taintedActors.Add(prim);
                }
            }
        }
        internal void SetUsingGImpact()
        {
            if (!usingGImpactAlgorithm)
                btGImpactCollisionAlgorithm.registerAlgorithm(m_dispatcher);
                usingGImpactAlgorithm = true;
        }

        public override float Simulate(float timeStep)
        {
            
            lock (m_taintedActors)
            {
                foreach (PhysicsActor act in m_taintedActors)
                {
                    if (act is BulletDotNETCharacter)
                        ((BulletDotNETCharacter) act).ProcessTaints(timeStep);
                    if (act is BulletDotNETPrim)
                        ((BulletDotNETPrim)act).ProcessTaints(timeStep);
                }
                m_taintedActors.Clear();
            }

            lock (m_characters)
            {
                foreach (BulletDotNETCharacter chr in m_characters)
                {
                    chr.Move(timeStep);
                }
            }

            lock (m_prims)
            {
                foreach (BulletDotNETPrim prim in m_prims)
                {
                    if (prim != null)
                    prim.Move(timeStep);
                }
            }
            float steps = m_world.stepSimulation(timeStep * 1000, 10, WorldTimeComp);

            foreach (BulletDotNETCharacter chr in m_characters)
            {
                chr.UpdatePositionAndVelocity();
            }

            foreach (BulletDotNETPrim prm in m_activePrims)
            {
                /*
                if (prm != null)
                    if (prm.Body != null)
                */
                prm.UpdatePositionAndVelocity();
            }
            if (m_CollisionInterface != null)
            {
                List<int> collisions = m_CollisionInterface.GetContactList();
                lock (collisions)
                {
                    foreach (int pvalue in collisions)
                    {
                        System.Console.Write(string.Format("{0} ", pvalue));
                    }
                }
                m_CollisionInterface.Clear();

            }
            return steps;
        }

        public override void GetResults()
        {
            
        }

        public override void SetTerrain(float[] heightMap)
        {
            if (m_terrainShape != null)
                DeleteTerrain();

            float hfmax = -9000;
            float hfmin = 90000;
            
            for (int i = 0; i <heightMap.Length;i++)
            {
                if (Single.IsNaN(heightMap[i]) || Single.IsInfinity(heightMap[i]))
                {
                    heightMap[i] = 0f;
                }

                hfmin = (heightMap[i] < hfmin) ? heightMap[i] : hfmin;
                hfmax = (heightMap[i] > hfmax) ? heightMap[i] : hfmax;
            }
            // store this for later reference.
            // Note, we're storing it  after we check it for anomolies above
            _origheightmap = heightMap;

            hfmin = 0;
            hfmax = 256;

            m_terrainShape = new btHeightfieldTerrainShape((int)Constants.RegionSize, (int)Constants.RegionSize, heightMap,
                                                           1.0f, hfmin, hfmax, (int)btHeightfieldTerrainShape.UPAxis.Z,
                                                           (int)btHeightfieldTerrainShape.PHY_ScalarType.PHY_FLOAT, false);
            float AabbCenterX = Constants.RegionSize/2f;
            float AabbCenterY = Constants.RegionSize/2f;

            float AabbCenterZ = 0;
            float temphfmin, temphfmax;

            temphfmin = hfmin;
            temphfmax = hfmax;

            if (temphfmin < 0)
            {
                temphfmax = 0 - temphfmin;
                temphfmin = 0 - temphfmin;
            }
            else if (temphfmin > 0)
            {
                temphfmax = temphfmax + (0 - temphfmin);
                //temphfmin = temphfmin + (0 - temphfmin);
            }
            AabbCenterZ = temphfmax/2f;
            
            if (m_terrainPosition == null)
            {
                m_terrainPosition = new btVector3(AabbCenterX, AabbCenterY, AabbCenterZ);
            }
            else
            {
                try
                {
                    m_terrainPosition.setValue(AabbCenterX, AabbCenterY, AabbCenterZ);
                } 
                catch (ObjectDisposedException)
                {
                    m_terrainPosition = new btVector3(AabbCenterX, AabbCenterY, AabbCenterZ);
                }
            }
            if (m_terrainMotionState != null)
            {
                m_terrainMotionState.Dispose();
                m_terrainMotionState = null;
            }
            m_terrainTransform = new btTransform(QuatIdentity, m_terrainPosition);
            m_terrainMotionState = new btDefaultMotionState(m_terrainTransform);
            TerrainBody = new btRigidBody(0, m_terrainMotionState, m_terrainShape);
            m_world.addRigidBody(TerrainBody);


        }

        public override void SetWaterLevel(float baseheight)
        {
            
        }

        public override void DeleteTerrain()
        {
            if (TerrainBody != null)
            {
                m_world.removeRigidBody(TerrainBody);
            }

            if (m_terrainShape != null)
            {
                m_terrainShape.Dispose();
                m_terrainShape = null;
            }

            if (m_terrainMotionState != null)
            {
                m_terrainMotionState.Dispose();
                m_terrainMotionState = null;
            }
            
            if (m_terrainTransform != null)
            {
                m_terrainTransform.Dispose();
                m_terrainTransform = null;
            }

            if (m_terrainPosition != null)
            {
                m_terrainPosition.Dispose();
                m_terrainPosition = null;
            }
        }

        public override void Dispose()
        {
            disposeAllBodies();
            m_world.Dispose();
            m_broadphase.Dispose();
            ((btDefaultCollisionConfiguration) m_collisionConfiguration).Dispose();
            ((btSequentialImpulseConstraintSolver) m_solver).Dispose();
            worldAabbMax.Dispose();
            worldAabbMin.Dispose();
            VectorZero.Dispose();
            QuatIdentity.Dispose();
            m_gravity.Dispose();
            VectorZero = null;
            QuatIdentity = null;
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            return new Dictionary<uint, float>();
        }

        public btDiscreteDynamicsWorld getBulletWorld()
        {
            return m_world;
        }

        private void disposeAllBodies()
        {
            lock (m_prims)
            {
                foreach (BulletDotNETPrim prim in m_prims)
                {
                    if (prim.Body != null)
                        m_world.removeRigidBody(prim.Body);

                    prim.Dispose();
                }
                m_prims.Clear();

                foreach (BulletDotNETCharacter chr in m_characters)
                {
                    if (chr.Body != null)
                        m_world.removeRigidBody(chr.Body);
                    chr.Dispose();
                }
                m_characters.Clear();
            }
        }

        public override bool IsThreaded
        {
            get { return false; }
        }

        internal void addCollisionEventReporting(PhysicsActor bulletDotNETCharacter)
        {
            //TODO: FIXME:
        }

        internal void remCollisionEventReporting(PhysicsActor bulletDotNETCharacter)
        {
            //TODO: FIXME:
        }

        internal void AddRigidBody(btRigidBody Body)
        {
            m_world.addRigidBody(Body);
        }
        [Obsolete("bad!")]
        internal void removeFromWorld(btRigidBody body)
        {
            
            m_world.removeRigidBody(body);
        }

        internal void removeFromWorld(BulletDotNETPrim prm ,btRigidBody body)
        {
            lock (m_prims)
            {
                if (m_prims.Contains(prm))
                {
                    m_world.removeRigidBody(body);
                }
                remActivePrim(prm);
                m_prims.Remove(prm);
            }

        }

        internal float GetWaterLevel()
        {
            throw new NotImplementedException();
        }

        // Recovered for use by fly height. Kitto Flora
        public float GetTerrainHeightAtXY(float x, float y)
        {
            // Teravus: Kitto, this code causes recurring errors that stall physics permenantly unless 
            // the values are checked, so checking below.
            // Is there any reason that we don't do this in ScenePresence?
            // The only physics engine that benefits from it in the physics plugin is this one

            if (x > (int)Constants.RegionSize || y > (int)Constants.RegionSize ||
                x < 0.001f || y < 0.001f)
                return 0;

            return _origheightmap[(int)y * Constants.RegionSize + (int)x];
        }
        // End recovered. Kitto Flora

        /// <summary>
        /// Routine to figure out if we need to mesh this prim with our mesher
        /// </summary>
        /// <param name="pbs"></param>
        /// <returns></returns>
        public bool needsMeshing(PrimitiveBaseShape pbs)
        {
            // most of this is redundant now as the mesher will return null if it cant mesh a prim
            // but we still need to check for sculptie meshing being enabled so this is the most
            // convenient place to do it for now...

            //    //if (pbs.PathCurve == (byte)Primitive.PathCurve.Circle && pbs.ProfileCurve == (byte)Primitive.ProfileCurve.Circle && pbs.PathScaleY <= 0.75f)
            //    //m_log.Debug("needsMeshing: " + " pathCurve: " + pbs.PathCurve.ToString() + " profileCurve: " + pbs.ProfileCurve.ToString() + " pathScaleY: " + Primitive.UnpackPathScale(pbs.PathScaleY).ToString());
            int iPropertiesNotSupportedDefault = 0;

            if (pbs.SculptEntry && !meshSculptedPrim)
            {
#if SPAM
                m_log.Warn("NonMesh");
#endif
                return false;
            }

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
#if SPAM
                    m_log.Warn("NonMesh");
#endif
                    return false;
                }
            }

            if (pbs.ProfileHollow != 0)
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
#if SPAM
                m_log.Warn("NonMesh");
#endif
                return false;
            }
#if SPAM
            m_log.Debug("Mesh");
#endif
            return true;
        }

        internal void addActivePrim(BulletDotNETPrim pPrim)
        {
            lock (m_activePrims)
            {
                if (!m_activePrims.Contains(pPrim))
                {
                    m_activePrims.Add(pPrim);
                }
            }
        }

        public void remActivePrim(BulletDotNETPrim pDeactivatePrim)
        {
            lock (m_activePrims)
            {
                m_activePrims.Remove(pDeactivatePrim);
            }
        }

        internal void AddPrimToScene(BulletDotNETPrim pPrim)
        {
            lock (m_prims)
            {
                if (!m_prims.Contains(pPrim))
                {
                    m_prims.Add(pPrim);
                    m_world.addRigidBody(pPrim.Body);
                    m_log.Debug("ADDED");
                }
            }
        }
        internal void EnableCollisionInterface()
        {
            if (m_CollisionInterface == null)
            {
                m_CollisionInterface = new ContactAddedCallbackHandler();
                m_world.SetCollisionAddedCallback(m_CollisionInterface);
            }
        }
        


    }
}
