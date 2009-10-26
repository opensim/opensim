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

#region References

using System;
using System.Collections.Generic;
using OpenMetaverse;
using MonoXnaCompactMaths;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using XnaDevRu.BulletX;
using XnaDevRu.BulletX.Dynamics;
using Nini.Config;
using Vector3 = MonoXnaCompactMaths.Vector3;
using Quaternion = MonoXnaCompactMaths.Quaternion;

#endregion

namespace OpenSim.Region.Physics.BulletXPlugin
{
    /// <summary>
    /// BulletXConversions are called now BulletXMaths
    /// This Class converts objects and types for BulletX and give some operations
    /// </summary>
    public class BulletXMaths
    {
        //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //Vector3
        public static Vector3 PhysicsVectorToXnaVector3(OpenMetaverse.Vector3 physicsVector)
        {
            return new Vector3(physicsVector.X, physicsVector.Y, physicsVector.Z);
        }

        public static OpenMetaverse.Vector3 XnaVector3ToPhysicsVector(Vector3 xnaVector3)
        {
            return new OpenMetaverse.Vector3(xnaVector3.X, xnaVector3.Y, xnaVector3.Z);
        }

        //Quaternion
        public static Quaternion QuaternionToXnaQuaternion(OpenMetaverse.Quaternion quaternion)
        {
            return new Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        public static OpenMetaverse.Quaternion XnaQuaternionToQuaternion(Quaternion xnaQuaternion)
        {
            return new OpenMetaverse.Quaternion(xnaQuaternion.W, xnaQuaternion.X, xnaQuaternion.Y, xnaQuaternion.Z);
        }

        //Next methods are extracted from XnaDevRu.BulletX(See 3rd party license):
        //- SetRotation (class MatrixOperations)
        //- GetRotation (class MatrixOperations)
        //- GetElement (class MathHelper)
        //- SetElement (class MathHelper)
        internal static void SetRotation(ref Matrix m, Quaternion q)
        {
            float d = q.LengthSquared();
            float s = 2f/d;
            float xs = q.X*s, ys = q.Y*s, zs = q.Z*s;
            float wx = q.W*xs, wy = q.W*ys, wz = q.W*zs;
            float xx = q.X*xs, xy = q.X*ys, xz = q.X*zs;
            float yy = q.Y*ys, yz = q.Y*zs, zz = q.Z*zs;
            m = new Matrix(1 - (yy + zz), xy - wz, xz + wy, 0,
                           xy + wz, 1 - (xx + zz), yz - wx, 0,
                           xz - wy, yz + wx, 1 - (xx + yy), 0,
                           m.M41, m.M42, m.M43, 1);
        }

        internal static Quaternion GetRotation(Matrix m)
        {
            Quaternion q;

            float trace = m.M11 + m.M22 + m.M33;

            if (trace > 0)
            {
                float s = (float) Math.Sqrt(trace + 1);
                q.W = s*0.5f;
                s = 0.5f/s;

                q.X = (m.M32 - m.M23)*s;
                q.Y = (m.M13 - m.M31)*s;
                q.Z = (m.M21 - m.M12)*s;
            }
            else
            {
                q.X = q.Y = q.Z = q.W = 0f;

                int i = m.M11 < m.M22
                            ?
                                (m.M22 < m.M33 ? 2 : 1)
                            :
                                (m.M11 < m.M33 ? 2 : 0);
                int j = (i + 1)%3;
                int k = (i + 2)%3;

                float s = (float) Math.Sqrt(GetElement(m, i, i) - GetElement(m, j, j) - GetElement(m, k, k) + 1);
                SetElement(ref q, i, s*0.5f);
                s = 0.5f/s;

                q.W = (GetElement(m, k, j) - GetElement(m, j, k))*s;
                SetElement(ref q, j, (GetElement(m, j, i) + GetElement(m, i, j))*s);
                SetElement(ref q, k, (GetElement(m, k, i) + GetElement(m, i, k))*s);
            }

            return q;
        }

        internal static float SetElement(ref Quaternion q, int index, float value)
        {
            switch (index)
            {
                case 0:
                    q.X = value;
                    break;
                case 1:
                    q.Y = value;
                    break;
                case 2:
                    q.Z = value;
                    break;
                case 3:
                    q.W = value;
                    break;
            }

            return 0;
        }

        internal static float GetElement(Matrix mat, int row, int col)
        {
            switch (row)
            {
                case 0:
                    switch (col)
                    {
                        case 0:
                            return mat.M11;
                        case 1:
                            return mat.M12;
                        case 2:
                            return mat.M13;
                    }
                    break;
                case 1:
                    switch (col)
                    {
                        case 0:
                            return mat.M21;
                        case 1:
                            return mat.M22;
                        case 2:
                            return mat.M23;
                    }
                    break;
                case 2:
                    switch (col)
                    {
                        case 0:
                            return mat.M31;
                        case 1:
                            return mat.M32;
                        case 2:
                            return mat.M33;
                    }
                    break;
            }

            return 0;
        }
    }

    /// <summary>
    /// PhysicsPlugin Class for BulletX
    /// </summary>
    public class BulletXPlugin : IPhysicsPlugin
    {
        private BulletXScene _mScene;

        public BulletXPlugin()
        {
        }

        public bool Init()
        {
            return true;
        }

        public PhysicsScene GetScene(string sceneIdentifier)
        {
            if (_mScene == null)
            {
                _mScene = new BulletXScene(sceneIdentifier);
            }
            return (_mScene);
        }

        public string GetName()
        {
            return ("modified_BulletX"); //Changed!! "BulletXEngine" To "modified_BulletX"
        }

        public void Dispose()
        {
        }
    }


    // Class to detect and debug collisions
    // Mainly used for debugging purposes
    internal class CollisionDispatcherLocal : CollisionDispatcher
    {
        private BulletXScene relatedScene;

        public CollisionDispatcherLocal(BulletXScene s)
            : base()
        {
            relatedScene = s;
        }

        public override bool NeedsCollision(CollisionObject bodyA, CollisionObject bodyB)
        {
            RigidBody rb;
            BulletXCharacter bxcA = null;
            BulletXPrim bxpA = null;
            Type t = bodyA.GetType();
            if (t == typeof (RigidBody))
            {
                rb = (RigidBody) bodyA;
                relatedScene._characters.TryGetValue(rb, out bxcA);
                relatedScene._prims.TryGetValue(rb, out bxpA);
            }
//             String nameA;
//             if (bxcA != null)
//                 nameA = bxcA._name;
//             else if (bxpA != null)
//                 nameA = bxpA._name;
//             else
//                 nameA = "null";



            BulletXCharacter bxcB = null;
            BulletXPrim bxpB = null;
            t = bodyB.GetType();
            if (t == typeof (RigidBody))
            {
                rb = (RigidBody) bodyB;
                relatedScene._characters.TryGetValue(rb, out bxcB);
                relatedScene._prims.TryGetValue(rb, out bxpB);
            }

//             String nameB;
//             if (bxcB != null)
//                 nameB = bxcB._name;
//             else if (bxpB != null)
//                 nameB = bxpB._name;
//             else
            //                 nameB = "null";
            bool needsCollision;// = base.NeedsCollision(bodyA, bodyB);
            int c1 = 3;
            int c2 = 3;

            ////////////////////////////////////////////////////////
            //BulletX Mesh Collisions
            //added by Jed zhu
            //data: May 07,2005
            ////////////////////////////////////////////////////////
            #region BulletXMeshCollisions Fields


            if (bxcA != null && bxpB != null)
                c1 = Collision(bxcA, bxpB);
            if (bxpA != null && bxcB != null)
                c2 = Collision(bxcB, bxpA);
            if (c1 < 2)
                needsCollision = (c1 > 0) ? true : false;
            else if (c2 < 2)
                needsCollision = (c2 > 0) ? true : false;
            else
                needsCollision = base.NeedsCollision(bodyA, bodyB);


            #endregion


            //m_log.DebugFormat("[BulletX]: A collision was detected between {0} and {1} --> {2}", nameA, nameB,
                                   //needsCollision);


            return needsCollision;
        }
        //added by jed zhu
        //calculas the collision between the Prim and Actor
        //
        private int Collision(BulletXCharacter actorA, BulletXPrim primB)
        {
            int[] indexBase;
            Vector3[] vertexBase;
            Vector3 vNormal;
            // Vector3 vP1;
            // Vector3 vP2;
            // Vector3 vP3;
            IMesh mesh = primB.GetMesh();

            float fdistance;
            if (primB == null)
                return 3;
            if (mesh == null)
                return 2;
            if (actorA == null)
                return 3;

            int iVertexCount = mesh.getVertexList().Count;
            int iIndexCount = mesh.getIndexListAsInt().Length;
            if (iVertexCount == 0)
                return 3;
            if (iIndexCount == 0)
                return 3;
            lock (BulletXScene.BulletXLock)
            {
                indexBase = mesh.getIndexListAsInt();
                vertexBase = new Vector3[iVertexCount];
                for (int i = 0; i < iVertexCount; i++)
                {
                    OpenMetaverse.Vector3 v = mesh.getVertexList()[i];
                    if (v != null) // Note, null has special meaning. See meshing code for details
                        vertexBase[i] = BulletXMaths.PhysicsVectorToXnaVector3(v);
                    else
                        vertexBase[i] = Vector3.Zero;
                }
                for (int ix = 0; ix < iIndexCount; ix += 3)
                {
                    int ia = indexBase[ix + 0];
                    int ib = indexBase[ix + 1];
                    int ic = indexBase[ix + 2];
                    //
                    Vector3 v1 = vertexBase[ib] - vertexBase[ia];
                    Vector3 v2 = vertexBase[ic] - vertexBase[ia];

                    Vector3.Cross(ref v1, ref v2, out vNormal);
                    Vector3.Normalize(ref vNormal, out vNormal);

                    fdistance = Vector3.Dot(vNormal, vertexBase[ia]) + 0.50f;
                    if (preCheckCollision(actorA, vNormal, fdistance) == 1)
                    {
                        if (CheckCollision(actorA, ia, ib, ic, vNormal, vertexBase) == 1)
                        {
                            //PhysicsVector v = actorA.Position;
                            //Vector3 v3 = BulletXMaths.PhysicsVectorToXnaVector3(v);
                            //Vector3 vp = vNormal * (fdistance - Vector3.Dot(vNormal, v3) + 0.2f);
                            //actorA.Position += BulletXMaths.XnaVector3ToPhysicsVector(vp);
                            return 1;
                        }
                    }
                }
            }


            return 0;
        }
        //added by jed zhu
        //return value 1: need second check
        //return value 0: no need check

        private int preCheckCollision(BulletXActor actA, Vector3 vNormal, float fDist)
        {
            float fstartSide;
            OpenMetaverse.Vector3 v = actA.Position;
            Vector3 v3 = BulletXMaths.PhysicsVectorToXnaVector3(v);

            fstartSide = Vector3.Dot(vNormal, v3) - fDist;
            if (fstartSide > 0) return 0;
            else return 1;
        }
        //added by jed zhu
        private int CheckCollision(BulletXActor actA, int ia, int ib, int ic, Vector3 vNormal, Vector3[] vertBase)
        {
            Vector3 perPlaneNormal;
            float fPerPlaneDist;
            OpenMetaverse.Vector3 v = actA.Position;
            Vector3 v3 = BulletXMaths.PhysicsVectorToXnaVector3(v);
            //check AB
            Vector3 v1;
            v1 = vertBase[ib] - vertBase[ia];
            Vector3.Cross(ref vNormal, ref v1, out perPlaneNormal);
            Vector3.Normalize(ref perPlaneNormal, out perPlaneNormal);

            if (Vector3.Dot((vertBase[ic] - vertBase[ia]), perPlaneNormal) < 0)
                perPlaneNormal = -perPlaneNormal;
            fPerPlaneDist = Vector3.Dot(perPlaneNormal, vertBase[ia]) - 0.50f;



            if ((Vector3.Dot(perPlaneNormal, v3) - fPerPlaneDist) < 0)
                return 0;
            fPerPlaneDist = Vector3.Dot(perPlaneNormal, vertBase[ic]) + 0.50f;
            if ((Vector3.Dot(perPlaneNormal, v3) - fPerPlaneDist) > 0)
                return 0;

            //check BC

            v1 = vertBase[ic] - vertBase[ib];
            Vector3.Cross(ref vNormal, ref v1, out perPlaneNormal);
            Vector3.Normalize(ref perPlaneNormal, out perPlaneNormal);

            if (Vector3.Dot((vertBase[ia] - vertBase[ib]), perPlaneNormal) < 0)
                perPlaneNormal = -perPlaneNormal;
            fPerPlaneDist = Vector3.Dot(perPlaneNormal, vertBase[ib]) - 0.50f;


            if ((Vector3.Dot(perPlaneNormal, v3) - fPerPlaneDist) < 0)
                return 0;
            fPerPlaneDist = Vector3.Dot(perPlaneNormal, vertBase[ia]) + 0.50f;
            if ((Vector3.Dot(perPlaneNormal, v3) - fPerPlaneDist) > 0)
                return 0;
            //check CA
            v1 = vertBase[ia] - vertBase[ic];
            Vector3.Cross(ref vNormal, ref v1, out perPlaneNormal);
            Vector3.Normalize(ref perPlaneNormal, out perPlaneNormal);

            if (Vector3.Dot((vertBase[ib] - vertBase[ic]), perPlaneNormal) < 0)
                perPlaneNormal = -perPlaneNormal;
            fPerPlaneDist = Vector3.Dot(perPlaneNormal, vertBase[ic]) - 0.50f;


            if ((Vector3.Dot(perPlaneNormal, v3) - fPerPlaneDist) < 0)
                return 0;
            fPerPlaneDist = Vector3.Dot(perPlaneNormal, vertBase[ib]) + 0.50f;
            if ((Vector3.Dot(perPlaneNormal, v3) - fPerPlaneDist) > 0)
                return 0;

            return 1;

        }
    }

    /// <summary>
    /// PhysicsScene Class for BulletX
    /// </summary>
    public class BulletXScene : PhysicsScene
    {
        #region BulletXScene Fields

        public DiscreteDynamicsWorld ddWorld;
        private CollisionDispatcher cDispatcher;
        private OverlappingPairCache opCache;
        private SequentialImpulseConstraintSolver sicSolver;
        public static Object BulletXLock = new Object();

        private const int minXY = 0;
        private const int minZ = 0;
        private const int maxXY = (int)Constants.RegionSize;
        private const int maxZ = 4096;
        private const int maxHandles = 32766; //Why? I don't know
        private const float gravity = 9.8f;
        private const float heightLevel0 = 77.0f;
        private const float heightLevel1 = 200.0f;
        private const float lowGravityFactor = 0.2f;
        //OpenSim calls Simulate 10 times per seconds. So FPS = "Simulate Calls" * simulationSubSteps = 100 FPS
        private const int simulationSubSteps = 10;
        //private float[] _heightmap;
        private BulletXPlanet _simFlatPlanet;
        internal Dictionary<RigidBody, BulletXCharacter> _characters = new Dictionary<RigidBody, BulletXCharacter>();
        internal Dictionary<RigidBody, BulletXPrim> _prims = new Dictionary<RigidBody, BulletXPrim>();

        public IMesher mesher;
        // private IConfigSource m_config;

        // protected internal String identifier;

        public BulletXScene(String sceneIdentifier)
        {
            //identifier = sceneIdentifier;
        }

        public static float Gravity
        {
            get { return gravity; }
        }

        public static float HeightLevel0
        {
            get { return heightLevel0; }
        }

        public static float HeightLevel1
        {
            get { return heightLevel1; }
        }

        public static float LowGravityFactor
        {
            get { return lowGravityFactor; }
        }

        public static int MaxXY
        {
            get { return maxXY; }
        }

        public static int MaxZ
        {
            get { return maxZ; }
        }

        private List<RigidBody> _forgottenRigidBodies = new List<RigidBody>();
        internal string is_ex_message = "Can't remove rigidBody!: ";

        #endregion

        public BulletXScene()
        {
            cDispatcher = new CollisionDispatcherLocal(this);
            Vector3 worldMinDim = new Vector3((float) minXY, (float) minXY, (float) minZ);
            Vector3 worldMaxDim = new Vector3((float) maxXY, (float) maxXY, (float) maxZ);
            opCache = new AxisSweep3(worldMinDim, worldMaxDim, maxHandles);
            sicSolver = new SequentialImpulseConstraintSolver();

            lock (BulletXLock)
            {
                ddWorld = new DiscreteDynamicsWorld(cDispatcher, opCache, sicSolver);
                ddWorld.Gravity = new Vector3(0, 0, -gravity);
            }
            //this._heightmap = new float[65536];
        }

        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
            mesher = meshmerizer;
            // m_config = config;
        }

        public override void Dispose()
        {

        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            Dictionary<uint, float> returncolliders = new Dictionary<uint, float>();
            return returncolliders;
        }

        public override void SetWaterLevel(float baseheight)
        {

        }

        public override PhysicsActor AddAvatar(string avName, OpenMetaverse.Vector3 position, OpenMetaverse.Vector3 size, bool isFlying)
        {
            OpenMetaverse.Vector3 pos = OpenMetaverse.Vector3.Zero;
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z + 20;
            BulletXCharacter newAv = null;
            newAv.Flying = isFlying;
            lock (BulletXLock)
            {
                newAv = new BulletXCharacter(avName, this, pos);
                _characters.Add(newAv.RigidBody, newAv);
            }
            return newAv;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            if (actor is BulletXCharacter)
            {
                lock (BulletXLock)
                {
                    try
                    {
                        ddWorld.RemoveRigidBody(((BulletXCharacter) actor).RigidBody);
                    }
                    catch (Exception ex)
                    {
                        BulletXMessage(is_ex_message + ex.Message, true);
                        ((BulletXCharacter) actor).RigidBody.ActivationState = ActivationState.DisableSimulation;
                        AddForgottenRigidBody(((BulletXCharacter) actor).RigidBody);
                    }
                    _characters.Remove(((BulletXCharacter) actor).RigidBody);
                }
                GC.Collect();
            }
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, OpenMetaverse.Vector3 position,
                                                  OpenMetaverse.Vector3 size, OpenMetaverse.Quaternion rotation)
        {
            return AddPrimShape(primName, pbs, position, size, rotation, false);
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, OpenMetaverse.Vector3 position,
                                                  OpenMetaverse.Vector3 size, OpenMetaverse.Quaternion rotation, bool isPhysical)
        {
            PhysicsActor result;

            switch (pbs.ProfileShape)
            {
                case ProfileShape.Square:
                    /// support simple box & hollow box now; later, more shapes
                    if (pbs.ProfileHollow == 0)
                    {
                        result = AddPrim(primName, position, size, rotation, null, null, isPhysical);
                    }
                    else
                    {
                        IMesh mesh = mesher.CreateMesh(primName, pbs, size, 32f, isPhysical);
                        result = AddPrim(primName, position, size, rotation, mesh, pbs, isPhysical);
                    }
                    break;

                default:
                    result = AddPrim(primName, position, size, rotation, null, null, isPhysical);
                    break;
            }

            return result;
        }

        public PhysicsActor AddPrim(String name, OpenMetaverse.Vector3 position, OpenMetaverse.Vector3 size, OpenMetaverse.Quaternion rotation,
                                    IMesh mesh, PrimitiveBaseShape pbs, bool isPhysical)
        {
            BulletXPrim newPrim = null;
            lock (BulletXLock)
            {
                newPrim = new BulletXPrim(name, this, position, size, rotation, mesh, pbs, isPhysical);
                _prims.Add(newPrim.RigidBody, newPrim);
            }
            return newPrim;
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            if (prim is BulletXPrim)
            {
                lock (BulletXLock)
                {
                    try
                    {
                        ddWorld.RemoveRigidBody(((BulletXPrim) prim).RigidBody);
                    }
                    catch (Exception ex)
                    {
                        BulletXMessage(is_ex_message + ex.Message, true);
                        ((BulletXPrim) prim).RigidBody.ActivationState = ActivationState.DisableSimulation;
                        AddForgottenRigidBody(((BulletXPrim) prim).RigidBody);
                    }
                    _prims.Remove(((BulletXPrim) prim).RigidBody);
                }
                GC.Collect();
            }
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        public override float Simulate(float timeStep)
        {
            float fps = 0;
            lock (BulletXLock)
            {
                //Try to remove garbage
                RemoveForgottenRigidBodies();
                //End of remove
                MoveAPrimitives(timeStep);


                fps = (timeStep*simulationSubSteps);

                ddWorld.StepSimulation(timeStep, simulationSubSteps, timeStep);
                //Extra Heightmap Validation: BulletX's HeightFieldTerrain somestimes doesn't work so fine.
                ValidateHeightForAll();
                //End heightmap validation.
                UpdateKineticsForAll();
            }
            return fps;
        }

        private void MoveAPrimitives(float timeStep)
        {
            foreach (BulletXCharacter actor in _characters.Values)
            {
                actor.Move(timeStep);
            }
        }

        private void ValidateHeightForAll()
        {
            float _height;
            foreach (BulletXCharacter actor in _characters.Values)
            {
                //_height = HeightValue(actor.RigidBodyPosition);
                _height = _simFlatPlanet.HeightValue(actor.RigidBodyPosition);
                actor.ValidateHeight(_height);
                //if (_simFlatPlanet.heightIsNotValid(actor.RigidBodyPosition, out _height)) actor.ValidateHeight(_height);
            }
            foreach (BulletXPrim prim in _prims.Values)
            {
                //_height = HeightValue(prim.RigidBodyPosition);
                _height = _simFlatPlanet.HeightValue(prim.RigidBodyPosition);
                prim.ValidateHeight(_height);
                //if (_simFlatPlanet.heightIsNotValid(prim.RigidBodyPosition, out _height)) prim.ValidateHeight(_height);
            }
            //foreach (BulletXCharacter actor in _characters)
            //{
            //    actor.ValidateHeight(0);
            //}
            //foreach (BulletXPrim prim in _prims)
            //{
            //    prim.ValidateHeight(0);
            //}
        }

        private void UpdateKineticsForAll()
        {
            //UpdatePosition > UpdateKinetics.
            //Not only position will be updated, also velocity cause acceleration.
            foreach (BulletXCharacter actor in _characters.Values)
            {
                actor.UpdateKinetics();
            }
            foreach (BulletXPrim prim in _prims.Values)
            {
                prim.UpdateKinetics();
            }
            //if (this._simFlatPlanet!=null) this._simFlatPlanet.Restore();
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            get
            {
                return (false); // for now we won't be multithreaded
            }
        }

        public override void SetTerrain(float[] heightMap)
        {
            ////As the same as ODE, heightmap (x,y) must be swapped for BulletX
            //for (int i = 0; i < 65536; i++)
            //{
            //    // this._heightmap[i] = (double)heightMap[i];
            //    // dbm (danx0r) -- heightmap x,y must be swapped for Ode (should fix ODE, but for now...)
            //    int x = i & 0xff;
            //    int y = i >> 8;
            //    this._heightmap[i] = heightMap[x * 256 + y];
            //}

            //float[] swappedHeightMap = new float[65536];
            ////As the same as ODE, heightmap (x,y) must be swapped for BulletX
            //for (int i = 0; i < 65536; i++)
            //{
            //    // this._heightmap[i] = (double)heightMap[i];
            //    // dbm (danx0r) -- heightmap x,y must be swapped for Ode (should fix ODE, but for now...)
            //    int x = i & 0xff;
            //    int y = i >> 8;
            //    swappedHeightMap[i] = heightMap[x * 256 + y];
            //}
            DeleteTerrain();
            //There is a BulletXLock inside the constructor of BulletXPlanet
            //this._simFlatPlanet = new BulletXPlanet(this, swappedHeightMap);
            _simFlatPlanet = new BulletXPlanet(this, heightMap);
            //this._heightmap = heightMap;
        }

        public override void DeleteTerrain()
        {
            if (_simFlatPlanet != null)
            {
                lock (BulletXLock)
                {
                    try
                    {
                        ddWorld.RemoveRigidBody(_simFlatPlanet.RigidBody);
                    }
                    catch (Exception ex)
                    {
                        BulletXMessage(is_ex_message + ex.Message, true);
                        _simFlatPlanet.RigidBody.ActivationState = ActivationState.DisableSimulation;
                        AddForgottenRigidBody(_simFlatPlanet.RigidBody);
                    }
                }
                _simFlatPlanet = null;
                GC.Collect();
                BulletXMessage("Terrain erased!", false);
            }

            

            //this._heightmap = null;
        }

        

        internal void AddForgottenRigidBody(RigidBody forgottenRigidBody)
        {
            _forgottenRigidBodies.Add(forgottenRigidBody);
        }

        private void RemoveForgottenRigidBodies()
        {
            RigidBody forgottenRigidBody;
            int nRigidBodies = _forgottenRigidBodies.Count;
            for (int i = nRigidBodies - 1; i >= 0; i--)
            {
                forgottenRigidBody = _forgottenRigidBodies[i];
                try
                {
                    ddWorld.RemoveRigidBody(forgottenRigidBody);
                    _forgottenRigidBodies.Remove(forgottenRigidBody);
                    BulletXMessage("Forgotten Rigid Body Removed", false);
                }
                catch (Exception ex)
                {
                    BulletXMessage("Can't remove forgottenRigidBody!: " + ex.Message, false);
                }
            }
            GC.Collect();
        }

        internal static void BulletXMessage(string message, bool isWarning)
        {
            PhysicsPluginManager.PhysicsPluginMessage("[Modified BulletX]:\t" + message, isWarning);
        }

        //temp
        //private float HeightValue(MonoXnaCompactMaths.Vector3 position)
        //{
        //    int li_x, li_y;
        //    float height;
        //    li_x = (int)Math.Round(position.X); if (li_x < 0) li_x = 0;
        //    li_y = (int)Math.Round(position.Y); if (li_y < 0) li_y = 0;

        //    height = this._heightmap[li_y * 256 + li_x];
        //    if (height < 0) height = 0;
        //    else if (height > maxZ) height = maxZ;

        //    return height;
        //}
    }

    /// <summary>
    /// Generic Physics Actor for BulletX inherit from PhysicActor
    /// </summary>
    public class BulletXActor : PhysicsActor
    {
        protected bool flying = false;
        protected bool _physical = false;
        protected OpenMetaverse.Vector3 _position;
        protected OpenMetaverse.Vector3 _velocity;
        protected OpenMetaverse.Vector3 _size;
        protected OpenMetaverse.Vector3 _acceleration;
        protected OpenMetaverse.Quaternion _orientation;
        protected OpenMetaverse.Vector3 m_rotationalVelocity;
        protected RigidBody rigidBody;
        protected int m_PhysicsActorType;
        private Boolean iscolliding = false;
        internal string _name;

        public BulletXActor(String name)
        {
            _name = name;
        }

        public override bool Stopped
        {
            get { return false; }
        }

        public override OpenMetaverse.Vector3 Position
        {
            get { return _position; }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _position = value;
                    Translate();
                }
            }
        }

        public override OpenMetaverse.Vector3 RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }

        public override OpenMetaverse.Vector3 Velocity
        {
            get { return _velocity; }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    //Static objects don' have linear velocity
                    if (_physical)
                    {
                        _velocity = value;
                        Speed();
                    }
                    else
                    {
                        _velocity = OpenMetaverse.Vector3.Zero;
                    }
                }
            }
        }
        public override float CollisionScore
        {
            get { return 0f; }
            set { }
        }
        public override OpenMetaverse.Vector3 Size
        {
            get { return _size; }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _size = value;
                }
            }
        }

        public override OpenMetaverse.Vector3 Force
        {
            get { return OpenMetaverse.Vector3.Zero; }
            set { return; }
        }

        public override int VehicleType
        {
            get { return 0; }
            set { return; }
        }

        public override void VehicleFloatParam(int param, float value)
        {

        }

        public override void VehicleVectorParam(int param, OpenMetaverse.Vector3 value)
        {

        }
        
        public override void VehicleRotationParam(int param, OpenMetaverse.Quaternion rotation)
        {

        }

        public override void SetVolumeDetect(int param)
        {

        }

        public override OpenMetaverse.Vector3 CenterOfMass
        {
            get { return OpenMetaverse.Vector3.Zero; }
        }

        public override OpenMetaverse.Vector3 GeometricCenter
        {
            get { return OpenMetaverse.Vector3.Zero; }
        }

        public override PrimitiveBaseShape Shape
        {
            set { return; }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }

        public override OpenMetaverse.Vector3 Acceleration
        {
            get { return _acceleration; }
        }

        public override OpenMetaverse.Quaternion Orientation
        {
            get { return _orientation; }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _orientation = value;
                    ReOrient();
                }
            }
        }
        public override void link(PhysicsActor obj)
        {

        }

        public override void delink()
        {

        }

        public override void LockAngularMotion(OpenMetaverse.Vector3 axis)
        {

        }

        public override float Mass
        {
            get { return ActorMass; }
        }

        public virtual float ActorMass
        {
            get { return 0; }
        }

        public override int PhysicsActorType
        {
            get { return (int) m_PhysicsActorType; }
            set { m_PhysicsActorType = value; }
        }

        public RigidBody RigidBody
        {
            get { return rigidBody; }
        }

        public Vector3 RigidBodyPosition
        {
            get { return rigidBody.CenterOfMassPosition; }
        }

        public override bool IsPhysical
        {
            get { return _physical; }
            set { _physical = value; }
        }

        public override bool Flying
        {
            get { return flying; }
            set { flying = value; }
        }

        public override bool ThrottleUpdates
        {
            get { return false; }
            set { return; }
        }

        public override bool IsColliding
        {
            get { return iscolliding; }
            set { iscolliding = value; }
        }

        public override bool CollidingGround
        {
            get { return false; }
            set { return; }
        }

        public override bool CollidingObj
        {
            get { return false; }
            set { return; }
        }

        public override uint LocalID
        {
            set { return; }
        }

        public override bool Grabbed
        {
            set { return; }
        }

        public override bool Selected
        {
            set { return; }
        }

        public override float Buoyancy
        {
            get { return 0f; }
            set { return; }
        }

        public override bool FloatOnWater
        {
            set { return; }
        }

        public virtual void SetAcceleration(OpenMetaverse.Vector3 accel)
        {
            lock (BulletXScene.BulletXLock)
            {
                _acceleration = accel;
            }
        }

        public override bool Kinematic
        {
            get { return false; }
            set { }
        }

        public override void AddForce(OpenMetaverse.Vector3 force, bool pushforce)
        {
        }
        public override OpenMetaverse.Vector3 Torque
        {
            get { return OpenMetaverse.Vector3.Zero; }
            set { return; }
        }
        public override void AddAngularForce(OpenMetaverse.Vector3 force, bool pushforce)
        {
        }

        public override void SetMomentum(OpenMetaverse.Vector3 momentum)
        {
        }

        internal virtual void ValidateHeight(float heighmapPositionValue)
        {
        }

        internal virtual void UpdateKinetics()
        {
        }

        #region Methods for updating values of RigidBody

        protected internal void Translate()
        {
            Translate(_position);
        }

        protected internal void Translate(OpenMetaverse.Vector3 _newPos)
        {
            Vector3 _translation;
            _translation = BulletXMaths.PhysicsVectorToXnaVector3(_newPos) - rigidBody.CenterOfMassPosition;
            rigidBody.Translate(_translation);
        }

        protected internal void Speed()
        {
            Speed(_velocity);
        }

        protected internal void Speed(OpenMetaverse.Vector3 _newSpeed)
        {
            Vector3 _speed;
            _speed = BulletXMaths.PhysicsVectorToXnaVector3(_newSpeed);
            rigidBody.LinearVelocity = _speed;
        }

        protected internal void ReOrient()
        {
            ReOrient(_orientation);
        }

        protected internal void ReOrient(OpenMetaverse.Quaternion _newOrient)
        {
            Quaternion _newOrientation;
            _newOrientation = BulletXMaths.QuaternionToXnaQuaternion(_newOrient);
            Matrix _comTransform = rigidBody.CenterOfMassTransform;
            BulletXMaths.SetRotation(ref _comTransform, _newOrientation);
            rigidBody.CenterOfMassTransform = _comTransform;
        }

        protected internal void ReSize()
        {
            ReSize(_size);
        }

        protected internal virtual void ReSize(OpenMetaverse.Vector3 _newSize)
        {
        }

        public virtual void ScheduleTerseUpdate()
        {
            base.RequestPhysicsterseUpdate();
        }

        #endregion

        public override void CrossingFailure()
        {

        }
        public override OpenMetaverse.Vector3 PIDTarget { set { return; } }
        public override bool PIDActive { set { return; } }
        public override float PIDTau { set { return; } }

        public override float PIDHoverHeight { set { return; } }
        public override bool PIDHoverActive { set { return; } }
        public override PIDHoverType PIDHoverType { set { return; } }
        public override float PIDHoverTau { set { return; } }


        public override void SubscribeEvents(int ms)
        {

        }
        public override void UnSubscribeEvents()
        {

        }
        public override bool SubscribedEvents()
        {
            return false;
        }
    }

    /// <summary>
    /// PhysicsActor Character Class for BulletX
    /// </summary>
    public class BulletXCharacter : BulletXActor
    {
        public BulletXCharacter(BulletXScene parent_scene, OpenMetaverse.Vector3 pos)
            : this(String.Empty, parent_scene, pos)
        {
        }

        public BulletXCharacter(String avName, BulletXScene parent_scene, OpenMetaverse.Vector3 pos)
            : this(avName, parent_scene, pos, OpenMetaverse.Vector3.Zero, OpenMetaverse.Vector3.Zero, OpenMetaverse.Vector3.Zero,
                   OpenMetaverse.Quaternion.Identity)
        {
        }

        public BulletXCharacter(String avName, BulletXScene parent_scene, OpenMetaverse.Vector3 pos, OpenMetaverse.Vector3 velocity,
                                OpenMetaverse.Vector3 size, OpenMetaverse.Vector3 acceleration, OpenMetaverse.Quaternion orientation)
            : base(avName)
        {
            //This fields will be removed. They're temporal
            float _sizeX = 0.5f;
            float _sizeY = 0.5f;
            float _sizeZ = 1.6f;
            //.
            _position = pos;
            _velocity = velocity;
            _size = size;
            //---
            _size.X = _sizeX;
            _size.Y = _sizeY;
            _size.Z = _sizeZ;
            //.
            _acceleration = acceleration;
            _orientation = orientation;
            _physical = true;

            float _mass = 50.0f; //This depends of avatar's dimensions
            //For RigidBody Constructor. The next values might change
            float _linearDamping = 0.0f;
            float _angularDamping = 0.0f;
            float _friction = 0.5f;
            float _restitution = 0.0f;
            Matrix _startTransform = Matrix.Identity;
            Matrix _centerOfMassOffset = Matrix.Identity;
            lock (BulletXScene.BulletXLock)
            {
                _startTransform.Translation = BulletXMaths.PhysicsVectorToXnaVector3(pos);
                //CollisionShape _collisionShape = new BoxShape(new MonoXnaCompactMaths.Vector3(1.0f, 1.0f, 1.60f));
                //For now, like ODE, collisionShape = sphere of radious = 1.0
                CollisionShape _collisionShape = new SphereShape(1.0f);
                DefaultMotionState _motionState = new DefaultMotionState(_startTransform, _centerOfMassOffset);
                Vector3 _localInertia = new Vector3();
                _collisionShape.CalculateLocalInertia(_mass, out _localInertia); //Always when mass > 0
                rigidBody =
                    new RigidBody(_mass, _motionState, _collisionShape, _localInertia, _linearDamping, _angularDamping,
                                  _friction, _restitution);
                //rigidBody.ActivationState = ActivationState.DisableDeactivation;
                //It's seems that there are a bug with rigidBody constructor and its CenterOfMassPosition
                Vector3 _vDebugTranslation;
                _vDebugTranslation = _startTransform.Translation - rigidBody.CenterOfMassPosition;
                rigidBody.Translate(_vDebugTranslation);
                parent_scene.ddWorld.AddRigidBody(rigidBody);
            }
        }

        public override int PhysicsActorType
        {
            get { return (int) ActorTypes.Agent; }
            set { return; }
        }

        public override OpenMetaverse.Vector3 Position
        {
            get { return base.Position; }
            set { base.Position = value; }
        }

        public override OpenMetaverse.Vector3 Velocity
        {
            get { return base.Velocity; }
            set { base.Velocity = value; }
        }

        public override OpenMetaverse.Vector3 Size
        {
            get { return base.Size; }
            set { base.Size = value; }
        }

        public override OpenMetaverse.Vector3 Acceleration
        {
            get { return base.Acceleration; }
        }

        public override OpenMetaverse.Quaternion Orientation
        {
            get { return base.Orientation; }
            set { base.Orientation = value; }
        }

        public override bool Flying
        {
            get { return base.Flying; }
            set { base.Flying = value; }
        }

        public override bool IsColliding
        {
            get { return base.IsColliding; }
            set { base.IsColliding = value; }
        }

        public override bool Kinematic
        {
            get { return base.Kinematic; }
            set { base.Kinematic = value; }
        }

        public override void SetAcceleration(OpenMetaverse.Vector3 accel)
        {
            base.SetAcceleration(accel);
        }

        public override void AddForce(OpenMetaverse.Vector3 force, bool pushforce)
        {
            base.AddForce(force, pushforce);
        }

        public override void SetMomentum(OpenMetaverse.Vector3 momentum)
        {
            base.SetMomentum(momentum);
        }

        internal void Move(float timeStep)
        {
            Vector3 vec = new Vector3();
            //At this point it's supossed that:
            //_velocity == rigidBody.LinearVelocity
            vec.X = _velocity.X;
            vec.Y = _velocity.Y;
            vec.Z = _velocity.Z;
            if ((vec.X != 0.0f) || (vec.Y != 0.0f) || (vec.Z != 0.0f)) rigidBody.Activate();
            if (flying)
            {
                //Antigravity with movement
                if (_position.Z <= BulletXScene.HeightLevel0)
                {
                    vec.Z += BulletXScene.Gravity*timeStep;
                }
                    //Lowgravity with movement
                else if ((_position.Z > BulletXScene.HeightLevel0)
                         && (_position.Z <= BulletXScene.HeightLevel1))
                {
                    vec.Z += BulletXScene.Gravity*timeStep*(1.0f - BulletXScene.LowGravityFactor);
                }
                    //Lowgravity with...
                else if (_position.Z > BulletXScene.HeightLevel1)
                {
                    if (vec.Z > 0) //no movement
                        vec.Z = BulletXScene.Gravity*timeStep*(1.0f - BulletXScene.LowGravityFactor);
                    else
                        vec.Z += BulletXScene.Gravity*timeStep*(1.0f - BulletXScene.LowGravityFactor);
                }
            }
            rigidBody.LinearVelocity = vec;
        }

        //This validation is very basic
        internal override void ValidateHeight(float heighmapPositionValue)
        {
            if (rigidBody.CenterOfMassPosition.Z < heighmapPositionValue + _size.Z/2.0f)
            {
                Matrix m = rigidBody.WorldTransform;
                Vector3 v3 = m.Translation;
                v3.Z = heighmapPositionValue + _size.Z/2.0f;
                m.Translation = v3;
                rigidBody.WorldTransform = m;
                //When an Avie touch the ground it's vertical velocity it's reduced to ZERO
                Speed(new OpenMetaverse.Vector3(rigidBody.LinearVelocity.X, rigidBody.LinearVelocity.Y, 0.0f));
            }
        }

        internal override void UpdateKinetics()
        {
            _position = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.CenterOfMassPosition);
            _velocity = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.LinearVelocity);
            //Orientation it seems that it will be the default.
            ReOrient();
        }
    }

    /// <summary>
    /// PhysicsActor Prim Class for BulletX
    /// </summary>
    public class BulletXPrim : BulletXActor
    {
        //Density it will depends of material.
        //For now all prims have the same density, all prims are made of water. Be water my friend! :D
        private const float _density = 1000.0f;
        private BulletXScene _parent_scene;
        private OpenMetaverse.Vector3 m_prev_position;
        private bool m_lastUpdateSent = false;
        //added by jed zhu
        private IMesh _mesh;
        public IMesh GetMesh() { return _mesh; }



        public BulletXPrim(String primName, BulletXScene parent_scene, OpenMetaverse.Vector3 pos, OpenMetaverse.Vector3 size,
                           OpenMetaverse.Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs, bool isPhysical)
            : this(
                primName, parent_scene, pos, OpenMetaverse.Vector3.Zero, size, OpenMetaverse.Vector3.Zero, rotation, mesh, pbs,
                isPhysical)
        {
        }

        public BulletXPrim(String primName, BulletXScene parent_scene, OpenMetaverse.Vector3 pos, OpenMetaverse.Vector3 velocity,
                           OpenMetaverse.Vector3 size,
                           OpenMetaverse.Vector3 acceleration, OpenMetaverse.Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs,
                           bool isPhysical)
            : base(primName)
        {
            if ((size.X == 0) || (size.Y == 0) || (size.Z == 0))
                throw new Exception("Size 0");
            if (OpenMetaverse.Quaternion.Normalize(rotation).Length() == 0f)
                rotation = OpenMetaverse.Quaternion.Identity;

            _position = pos;
            _physical = isPhysical;
            _velocity = _physical ? velocity : OpenMetaverse.Vector3.Zero;
            _size = size;
            _acceleration = acceleration;
            _orientation = rotation;

            _parent_scene = parent_scene;

            CreateRigidBody(parent_scene, mesh, pos, size);
        }

        public override int PhysicsActorType
        {
            get { return (int) ActorTypes.Prim; }
            set { return; }
        }

        public override OpenMetaverse.Vector3 Position
        {
            get { return base.Position; }
            set { base.Position = value; }
        }

        public override OpenMetaverse.Vector3 Velocity
        {
            get { return base.Velocity; }
            set { base.Velocity = value; }
        }

        public override OpenMetaverse.Vector3 Size
        {
            get { return _size; }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _size = value;
                    ReSize();
                }
            }
        }

        public override OpenMetaverse.Vector3 Acceleration
        {
            get { return base.Acceleration; }
        }

        public override OpenMetaverse.Quaternion Orientation
        {
            get { return base.Orientation; }
            set { base.Orientation = value; }
        }

        public override float ActorMass
        {
            get
            {
                //For now all prims are boxes
                return (_physical ? 1 : 0)*_density*_size.X*_size.Y*_size.Z;
            }
        }

        public override bool IsPhysical
        {
            get { return base.IsPhysical; }
            set
            {
                base.IsPhysical = value;
                if (value)
                {
                    //---
                    PhysicsPluginManager.PhysicsPluginMessage("Physical - Recreate", true);
                    //---
                    ReCreateRigidBody(_size);
                }
                else
                {
                    //---
                    PhysicsPluginManager.PhysicsPluginMessage("Physical - SetMassProps", true);
                    //---
                    rigidBody.SetMassProps(Mass, new Vector3());
                }
            }
        }

        public override bool Flying
        {
            get { return base.Flying; }
            set { base.Flying = value; }
        }

        public override bool IsColliding
        {
            get { return base.IsColliding; }
            set { base.IsColliding = value; }
        }

        public override bool Kinematic
        {
            get { return base.Kinematic; }
            set { base.Kinematic = value; }
        }

        public override void SetAcceleration(OpenMetaverse.Vector3 accel)
        {
            lock (BulletXScene.BulletXLock)
            {
                _acceleration = accel;
            }
        }

        public override void AddForce(OpenMetaverse.Vector3 force, bool pushforce)
        {
            base.AddForce(force,pushforce);
        }

        public override void SetMomentum(OpenMetaverse.Vector3 momentum)
        {
            base.SetMomentum(momentum);
        }

        internal override void ValidateHeight(float heighmapPositionValue)
        {
            if (rigidBody.CenterOfMassPosition.Z < heighmapPositionValue + _size.Z/2.0f)
            {
                Matrix m = rigidBody.WorldTransform;
                Vector3 v3 = m.Translation;
                v3.Z = heighmapPositionValue + _size.Z/2.0f;
                m.Translation = v3;
                rigidBody.WorldTransform = m;
                //When a Prim touch the ground it's vertical velocity it's reduced to ZERO
                //Static objects don't have linear velocity
                if (_physical)
                    Speed(new OpenMetaverse.Vector3(rigidBody.LinearVelocity.X, rigidBody.LinearVelocity.Y, 0.0f));
            }
        }

        internal override void UpdateKinetics()
        {
            if (_physical) //Updates properties. Prim updates its properties physically
            {
                _position = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.CenterOfMassPosition);

                _velocity = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.LinearVelocity);
                _orientation = BulletXMaths.XnaQuaternionToQuaternion(rigidBody.Orientation);

                if ((Math.Abs(m_prev_position.X - _position.X) < 0.03)
                    && (Math.Abs(m_prev_position.Y - _position.Y) < 0.03)
                    && (Math.Abs(m_prev_position.Z - _position.Z) < 0.03))
                {
                    if (!m_lastUpdateSent)
                    {
                        _velocity = OpenMetaverse.Vector3.Zero;
                        base.ScheduleTerseUpdate();
                        m_lastUpdateSent = true;
                    }
                }
                else
                {
                    m_lastUpdateSent = false;
                    base.ScheduleTerseUpdate();
                }
                m_prev_position = _position;
            }
            else //Doesn't updates properties. That's a cancel
            {
                Translate();
                //Speed(); //<- Static objects don't have linear velocity
                ReOrient();
            }
        }

        #region Methods for updating values of RigidBody

        protected internal void CreateRigidBody(BulletXScene parent_scene, IMesh mesh, OpenMetaverse.Vector3 pos,
                                                OpenMetaverse.Vector3 size)
        {
            //For RigidBody Constructor. The next values might change
            float _linearDamping = 0.0f;
            float _angularDamping = 0.0f;
            float _friction = 1.0f;
            float _restitution = 0.0f;
            Matrix _startTransform = Matrix.Identity;
            Matrix _centerOfMassOffset = Matrix.Identity;
            //added by jed zhu
            _mesh = mesh;

            lock (BulletXScene.BulletXLock)
            {
                _startTransform.Translation = BulletXMaths.PhysicsVectorToXnaVector3(pos);
                //For now all prims are boxes
                CollisionShape _collisionShape;
                if (mesh == null)
                {
                    _collisionShape = new BoxShape(BulletXMaths.PhysicsVectorToXnaVector3(size)/2.0f);
                }
                else
                {
                    int iVertexCount = mesh.getVertexList().Count;
                    int[] indices = mesh.getIndexListAsInt();
                    Vector3[] v3Vertices = new Vector3[iVertexCount];
                    for (int i = 0; i < iVertexCount; i++)
                    {
                        OpenMetaverse.Vector3 v = mesh.getVertexList()[i];
                        if (v != null) // Note, null has special meaning. See meshing code for details
                            v3Vertices[i] = BulletXMaths.PhysicsVectorToXnaVector3(v);
                        else
                            v3Vertices[i] = Vector3.Zero;
                    }
                    TriangleIndexVertexArray triMesh = new TriangleIndexVertexArray(indices, v3Vertices);

                    _collisionShape = new TriangleMeshShape(triMesh);
                }
                DefaultMotionState _motionState = new DefaultMotionState(_startTransform, _centerOfMassOffset);
                Vector3 _localInertia = new Vector3();
                if (_physical) _collisionShape.CalculateLocalInertia(Mass, out _localInertia); //Always when mass > 0
                rigidBody =
                    new RigidBody(Mass, _motionState, _collisionShape, _localInertia, _linearDamping, _angularDamping,
                                  _friction, _restitution);
                //rigidBody.ActivationState = ActivationState.DisableDeactivation;
                //It's seems that there are a bug with rigidBody constructor and its CenterOfMassPosition
                Vector3 _vDebugTranslation;
                _vDebugTranslation = _startTransform.Translation - rigidBody.CenterOfMassPosition;
                rigidBody.Translate(_vDebugTranslation);
                //---
                parent_scene.ddWorld.AddRigidBody(rigidBody);
            }
        }

        protected internal void ReCreateRigidBody(OpenMetaverse.Vector3 size)
        {
            //There is a bug when trying to remove a rigidBody that is colliding with something..
            try
            {
                _parent_scene.ddWorld.RemoveRigidBody(rigidBody);
            }
            catch (Exception ex)
            {
                BulletXScene.BulletXMessage(_parent_scene.is_ex_message + ex.Message, true);
                rigidBody.ActivationState = ActivationState.DisableSimulation;
                _parent_scene.AddForgottenRigidBody(rigidBody);
            }
            CreateRigidBody(_parent_scene, null, _position, size);
                // Note, null for the meshing definitely is wrong. It's here for the moment to apease the compiler
            if (_physical) Speed(); //Static objects don't have linear velocity
            ReOrient();
            GC.Collect();
        }

        protected internal override void ReSize(OpenMetaverse.Vector3 _newSize)
        {
            //I wonder to know how to resize with a simple instruction in BulletX. It seems that for now there isn't
            //so i have to do it manually. That's recreating rigidbody
            ReCreateRigidBody(_newSize);
        }

        #endregion
    }

    /// <summary>
    /// This Class manage a HeighField as a RigidBody. This is for to be added in the BulletXScene
    /// </summary>
    internal class BulletXPlanet
    {
        private OpenMetaverse.Vector3 _staticPosition;
//         private Vector3 _staticVelocity;
//         private OpenMetaverse.Quaternion _staticOrientation;
        private float _mass;
        // private BulletXScene _parentscene;
        internal float[] _heightField;
        private RigidBody _flatPlanet;

        internal RigidBody RigidBody
        {
            get { return _flatPlanet; }
        }

        internal BulletXPlanet(BulletXScene parent_scene, float[] heightField)
        {
            _staticPosition = new OpenMetaverse.Vector3(BulletXScene.MaxXY / 2, BulletXScene.MaxXY / 2, 0);
//             _staticVelocity = new PhysicsVector();
//             _staticOrientation = OpenMetaverse.Quaternion.Identity;
            _mass = 0; //No active
            // _parentscene = parent_scene;
            _heightField = heightField;

            float _linearDamping = 0.0f;
            float _angularDamping = 0.0f;
            float _friction = 0.5f;
            float _restitution = 0.0f;
            Matrix _startTransform = Matrix.Identity;
            Matrix _centerOfMassOffset = Matrix.Identity;

            lock (BulletXScene.BulletXLock)
            {
                try
                {
                    _startTransform.Translation = BulletXMaths.PhysicsVectorToXnaVector3(_staticPosition);
                    CollisionShape _collisionShape =
                        new HeightfieldTerrainShape(BulletXScene.MaxXY, BulletXScene.MaxXY, _heightField,
                                                    (float) BulletXScene.MaxZ, 2, true, false);
                    DefaultMotionState _motionState = new DefaultMotionState(_startTransform, _centerOfMassOffset);
                    Vector3 _localInertia = new Vector3();
                    //_collisionShape.CalculateLocalInertia(_mass, out _localInertia); //Always when mass > 0
                    _flatPlanet =
                        new RigidBody(_mass, _motionState, _collisionShape, _localInertia, _linearDamping,
                                      _angularDamping, _friction, _restitution);
                    //It's seems that there are a bug with rigidBody constructor and its CenterOfMassPosition
                    Vector3 _vDebugTranslation;
                    _vDebugTranslation = _startTransform.Translation - _flatPlanet.CenterOfMassPosition;
                    _flatPlanet.Translate(_vDebugTranslation);
                    parent_scene.ddWorld.AddRigidBody(_flatPlanet);
                }
                catch (Exception ex)
                {
                    BulletXScene.BulletXMessage(ex.Message, true);
                }
            }
            BulletXScene.BulletXMessage("BulletXPlanet created.", false);
        }

        internal float HeightValue(Vector3 position)
        {
            int li_x, li_y;
            float height;
            li_x = (int) Math.Round(position.X);
            if (li_x < 0) li_x = 0;
            if (li_x >= BulletXScene.MaxXY) li_x = BulletXScene.MaxXY - 1;
            li_y = (int) Math.Round(position.Y);
            if (li_y < 0) li_y = 0;
            if (li_y >= BulletXScene.MaxXY) li_y = BulletXScene.MaxXY - 1;

            height = ((HeightfieldTerrainShape) _flatPlanet.CollisionShape).getHeightFieldValue(li_x, li_y);
            if (height < 0) height = 0;
            else if (height > BulletXScene.MaxZ) height = BulletXScene.MaxZ;

            return height;
        }
    }
}
