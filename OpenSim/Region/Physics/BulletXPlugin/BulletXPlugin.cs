#region Copyright
/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
#endregion
#region References
using System;
using System.Collections.Generic;
using OpenSim.Region.Physics.Manager;
using Axiom.Math;
using AxiomQuaternion = Axiom.Math.Quaternion;
//Specific References for BulletXPlugin
using MonoXnaCompactMaths; //Called as MXCM
using XnaDevRu.BulletX;
using XnaDevRu.BulletX.Dynamics;
#endregion

namespace OpenSim.Region.Physics.BulletXPlugin
{
    /// <summary>
    /// BulletXConversions are called now BulletXMaths
    /// This Class converts objects and types for BulletX and give some operations
    /// </summary>
    public class BulletXMaths 
    {
        //Vector3
        public static MonoXnaCompactMaths.Vector3 PhysicsVectorToXnaVector3(PhysicsVector physicsVector)
        {
            return new MonoXnaCompactMaths.Vector3(physicsVector.X, physicsVector.Y, physicsVector.Z);
        }
        public static PhysicsVector XnaVector3ToPhysicsVector(MonoXnaCompactMaths.Vector3 xnaVector3)
        {
            return new PhysicsVector(xnaVector3.X, xnaVector3.Y, xnaVector3.Z);
        }
        //Quaternion
        public static MonoXnaCompactMaths.Quaternion AxiomQuaternionToXnaQuaternion(AxiomQuaternion axiomQuaternion)
        {
            return new MonoXnaCompactMaths.Quaternion(axiomQuaternion.x, axiomQuaternion.y, axiomQuaternion.z, axiomQuaternion.w);
        }
        public static AxiomQuaternion XnaQuaternionToAxiomQuaternion(MonoXnaCompactMaths.Quaternion xnaQuaternion)
        {
            return new AxiomQuaternion(xnaQuaternion.W, xnaQuaternion.X, xnaQuaternion.Y, xnaQuaternion.Z);
        }

        //Next methods are extracted from XnaDevRu.BulletX(See 3rd party license):
        //- SetRotation (class MatrixOperations)
        //- GetRotation (class MatrixOperations)
        //- GetElement (class MathHelper)
        //- SetElement (class MathHelper)
        internal static void SetRotation(ref Matrix m, MonoXnaCompactMaths.Quaternion q)
        {
            float d = q.LengthSquared();
            float s = 2f / d;
            float xs = q.X * s, ys = q.Y * s, zs = q.Z * s;
            float wx = q.W * xs, wy = q.W * ys, wz = q.W * zs;
            float xx = q.X * xs, xy = q.X * ys, xz = q.X * zs;
            float yy = q.Y * ys, yz = q.Y * zs, zz = q.Z * zs;
            m = new Matrix(1 - (yy + zz), xy - wz, xz + wy, 0,
                            xy + wz, 1 - (xx + zz), yz - wx, 0,
                            xz - wy, yz + wx, 1 - (xx + yy), 0,
                            m.M41, m.M42, m.M43, 1);
        }
        internal static MonoXnaCompactMaths.Quaternion GetRotation(Matrix m)
        {
            MonoXnaCompactMaths.Quaternion q = new MonoXnaCompactMaths.Quaternion();

            float trace = m.M11 + m.M22 + m.M33;

            if (trace > 0)
            {
                float s = (float)Math.Sqrt(trace + 1);
                q.W = s * 0.5f;
                s = 0.5f / s;

                q.X = (m.M32 - m.M23) * s;
                q.Y = (m.M13 - m.M31) * s;
                q.Z = (m.M21 - m.M12) * s;
            }
            else
            {
                int i = m.M11 < m.M22 ?
                    (m.M22 < m.M33 ? 2 : 1) :
                    (m.M11 < m.M33 ? 2 : 0);
                int j = (i + 1) % 3;
                int k = (i + 2) % 3;

                float s = (float)Math.Sqrt(GetElement(m, i, i) - GetElement(m, j, j) - GetElement(m, k, k) + 1);
                SetElement(ref q, i, s * 0.5f);
                s = 0.5f / s;

                q.W = (GetElement(m, k, j) - GetElement(m, j, k)) * s;
                SetElement(ref q, j, (GetElement(m, j, i) + GetElement(m, i, j)) * s);
                SetElement(ref q, k, (GetElement(m, k, i) + GetElement(m, i, k)) * s);
            }

            return q;
        }
        internal static float SetElement(ref MonoXnaCompactMaths.Quaternion q, int index, float value)
        {
            switch (index)
            {
                case 0:
                    q.X = value; break;
                case 1:
                    q.Y = value; break;
                case 2:
                    q.Z = value; break;
                case 3:
                    q.W = value; break;
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
                    } break;
                case 1:
                    switch (col)
                    {
                        case 0:
                            return mat.M21;
                        case 1:
                            return mat.M22;
                        case 2:
                            return mat.M23;
                    } break;
                case 2:
                    switch (col)
                    {
                        case 0:
                            return mat.M31;
                        case 1:
                            return mat.M32;
                        case 2:
                            return mat.M33;
                    } break;
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
        public PhysicsScene GetScene()
        {
            if (_mScene == null)
            {
                _mScene = new BulletXScene();
            }
            return (_mScene);
        }
        public string GetName()
        {
            return ("modified_BulletX");//Changed!! "BulletXEngine" To "modified_BulletX"
        }
        public void Dispose()
        {
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
        private const int maxXY = 256;
        private const int maxZ = 4096;
        private const int maxHandles = 32766; //Why? I don't know
        private static float gravity = 9.8f;
        private static float heightLevel0 = 77.0f;
        private static float heightLevel1 = 200.0f;
        private static float lowGravityFactor = 0.2f;

        private float[] _heightmap;
        private List<BulletXCharacter> _characters = new List<BulletXCharacter>();
        private List<BulletXPrim> _prims = new List<BulletXPrim>();

        public static float Gravity { get { return gravity; } }
        public static float HeightLevel0 { get { return heightLevel0; } }
        public static float HeightLevel1 { get { return heightLevel1; } }
        public static float LowGravityFactor { get { return lowGravityFactor; } }
        #endregion

        public BulletXScene()
        {
            cDispatcher = new CollisionDispatcher();
            MonoXnaCompactMaths.Vector3 worldMinDim = new MonoXnaCompactMaths.Vector3((float)minXY, (float)minXY, (float)minZ);
            MonoXnaCompactMaths.Vector3 worldMaxDim = new MonoXnaCompactMaths.Vector3((float)maxXY, (float)maxXY, (float)maxZ);
            opCache = new AxisSweep3(worldMinDim, worldMaxDim, maxHandles);
            sicSolver = new SequentialImpulseConstraintSolver();

            lock (BulletXLock)
            {
                ddWorld = new DiscreteDynamicsWorld(cDispatcher, opCache, sicSolver);
                ddWorld.Gravity = new MonoXnaCompactMaths.Vector3(0, 0, -gravity);
            }

            this._heightmap = new float[65536];
        }
        public override PhysicsActor AddAvatar(PhysicsVector position)
        {
            PhysicsVector pos = new PhysicsVector();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z + 20;
            BulletXCharacter newAv = null;
            lock (BulletXLock)
            {
                newAv = new BulletXCharacter(this, pos);
                _characters.Add(newAv);
            }
            return newAv;
        }
        public override void RemoveAvatar(PhysicsActor actor)
        {
            if (actor is BulletXCharacter)
            {
                lock (BulletXLock)
                {
                    _characters.Remove((BulletXCharacter)actor);
                }
            }
        }
        public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size, AxiomQuaternion rotation)
        {
            BulletXPrim newPrim = null;
            lock (BulletXLock)
            {
                newPrim = new BulletXPrim(this, position, size, rotation);
                _prims.Add(newPrim);
            }
            return newPrim;
        }
        public override void RemovePrim(PhysicsActor prim)
        {
            if (prim is BulletXPrim)
            {
                lock (BulletXLock)
                {
                    _prims.Remove((BulletXPrim)prim);
                }
            }
        }
        public override void Simulate(float timeStep)
        {
            lock (BulletXLock)
            {
                BXSMove(timeStep);
                ddWorld.StepSimulation(timeStep, 0, timeStep);
                //Heightmap Validation:
                BXSValidateHeight();
                //End heightmap validation.
                BXSUpdateKinetics();
            }
        }
        private void BXSMove(float timeStep)
        {
            foreach (BulletXCharacter actor in _characters)
            {
                actor.Move(timeStep);
            }
            foreach (BulletXPrim prim in _prims)
            {
            }
        }
        private void BXSValidateHeight()
        {
            float _height;
            foreach (BulletXCharacter actor in _characters)
            {
                if ((actor.RigidBodyHorizontalPosition.x < 0) || (actor.RigidBodyHorizontalPosition.y < 0))
                {
                    _height = 0;
                }
                else
                {
                    _height = this._heightmap[
                    (int)Math.Round(actor.RigidBodyHorizontalPosition.x) * 256
                    + (int)Math.Round(actor.RigidBodyHorizontalPosition.y)];
                }
                actor.ValidateHeight(_height);
            }
            foreach (BulletXPrim prim in _prims)
            {
                if ((prim.RigidBodyHorizontalPosition.x < 0) || (prim.RigidBodyHorizontalPosition.y < 0))
                {
                    _height = 0;
                }
                else
                {
                    _height = this._heightmap[
                    (int)Math.Round(prim.RigidBodyHorizontalPosition.x) * 256
                    + (int)Math.Round(prim.RigidBodyHorizontalPosition.y)];
                }
                prim.ValidateHeight(_height);
            }
        }
        private void BXSUpdateKinetics()
        {
            //UpdatePosition > UpdateKinetics.
            //Not only position will be updated, also velocity cause acceleration.
            foreach (BulletXCharacter actor in _characters)
            {
                actor.UpdateKinetics();
            }
            foreach (BulletXPrim prim in _prims)
            {
                prim.UpdateKinetics();
            }
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
            //As the same as ODE, heightmap (x,y) must be swapped for BulletX
            for (int i = 0; i < 65536; i++)
            {
                // this._heightmap[i] = (double)heightMap[i];
                // dbm (danx0r) -- heightmap x,y must be swapped for Ode (should fix ODE, but for now...)
                int x = i & 0xff;
                int y = i >> 8;
                this._heightmap[i] = heightMap[x * 256 + y];
            }
            lock (BulletXLock)
            {
                //Updating BulletX HeightMap???
            }
        }
        public override void DeleteTerrain()
        {

        }
    }
    /// <summary>
    /// PhysicsActor Character Class for BulletX
    /// </summary>
    public class BulletXCharacter : PhysicsActor
    {
        private PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _size;
        private PhysicsVector _acceleration;
        private AxiomQuaternion _orientation;
        private bool flying;
        private RigidBody rigidBody;

        public Axiom.Math.Vector2 RigidBodyHorizontalPosition
        {
            get
            {
                return new Axiom.Math.Vector2(this.rigidBody.CenterOfMassPosition.X, this.rigidBody.CenterOfMassPosition.Y);
            }
        }
        public BulletXCharacter(BulletXScene parent_scene, PhysicsVector pos)
            : this(parent_scene, pos, new PhysicsVector(), new PhysicsVector(), new PhysicsVector(),
            AxiomQuaternion.Identity)
        {
        }
        public BulletXCharacter(BulletXScene parent_scene, PhysicsVector pos, PhysicsVector velocity,
            PhysicsVector size, PhysicsVector acceleration, AxiomQuaternion orientation)
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
                MonoXnaCompactMaths.Vector3 _localInertia = new MonoXnaCompactMaths.Vector3();
                _collisionShape.CalculateLocalInertia(_mass, out _localInertia); //Always when mass > 0
                rigidBody = new RigidBody(_mass, _motionState, _collisionShape, _localInertia, _linearDamping, _angularDamping, _friction, _restitution);
                rigidBody.ActivationState = ActivationState.DisableDeactivation;
                //It's seems that there are a bug with rigidBody constructor and its CenterOfMassPosition
                MonoXnaCompactMaths.Vector3 _vDebugTranslation;
                _vDebugTranslation = _startTransform.Translation - rigidBody.CenterOfMassPosition;
                rigidBody.Translate(_vDebugTranslation);
                parent_scene.ddWorld.AddRigidBody(rigidBody);
            }
        }
        public override PhysicsVector Position
        {
            get
            {
                return _position;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _position = value;
                    Translate();
                }
            }
        }
        public override PhysicsVector Velocity
        {
            get
            {
                return _velocity;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _velocity = value;
                    Speed();
                }
            }
        }
        public override PhysicsVector Size
        {
            get
            {
                return _size;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _size = value;
                }
            }
        }
        public override PhysicsVector Acceleration
        {
            get
            {
                return _acceleration;
            }
        }
        public override AxiomQuaternion Orientation
        {
            get
            {
                return _orientation;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _orientation = value;
                }
            }
        }
        public override bool Flying
        {
            get
            {
                return flying;
            }
            set
            {
                flying = value;
            }
        }
        public void SetAcceleration(PhysicsVector accel)
        {
            lock (BulletXScene.BulletXLock)
            {
                _acceleration = accel;
            }
        }
        public override bool Kinematic
        {
            get
            {
                return false;
            }
            set
            {

            }
        }
        public override void AddForce(PhysicsVector force)
        {

        }
        public override void SetMomentum(PhysicsVector momentum)
        {

        }
        internal void Move(float timeStep)
        {
            MonoXnaCompactMaths.Vector3 vec = new MonoXnaCompactMaths.Vector3();
            //At this point it's supossed that:
            //_velocity == rigidBody.LinearVelocity
            vec.X = this._velocity.X; 
            vec.Y = this._velocity.Y; 
            vec.Z = this._velocity.Z; 

            if (flying)
            {
                //Antigravity with movement
                if (this._position.Z <= BulletXScene.HeightLevel0)
                {
                    vec.Z += BulletXScene.Gravity * timeStep;
                }
                //Lowgravity with movement
                else if ((this._position.Z > BulletXScene.HeightLevel0)
                    && (this._position.Z <= BulletXScene.HeightLevel1))
                {
                    vec.Z += BulletXScene.Gravity * timeStep * (1.0f - BulletXScene.LowGravityFactor);
                }
                //Lowgravity with...
                else if (this._position.Z > BulletXScene.HeightLevel1)
                {
                    if (vec.Z > 0) //no movement
                        vec.Z = BulletXScene.Gravity * timeStep * (1.0f - BulletXScene.LowGravityFactor);
                    else
                        vec.Z += BulletXScene.Gravity * timeStep * (1.0f - BulletXScene.LowGravityFactor);

                }
            }
            rigidBody.LinearVelocity = vec;
        }
        //This validation is very basic
        internal void ValidateHeight(float heighmapPositionValue)
        {
            if (rigidBody.CenterOfMassPosition.Z < heighmapPositionValue + _size.Z / 2.0f)
            {
                Matrix m = rigidBody.WorldTransform;
                MonoXnaCompactMaths.Vector3 v3 = m.Translation;
                v3.Z = heighmapPositionValue + _size.Z / 2.0f;
                m.Translation = v3;
                rigidBody.WorldTransform = m;
                //When an Avie touch the ground it's vertical velocity it's reduced to ZERO
                Speed(new PhysicsVector(this.rigidBody.LinearVelocity.X, this.rigidBody.LinearVelocity.Y, 0.0f));
            }
        }
        internal void UpdateKinetics()
        {
            this._position = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.CenterOfMassPosition);
            this._velocity = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.LinearVelocity);
            //Orientation it seems that it will be the default.
            ReOrient();
        }

        #region Methods for updating values of RigidBody
        private void Translate()
        {
            Translate(this._position);
        }
        private void Translate(PhysicsVector _newPos)
        {
            MonoXnaCompactMaths.Vector3 _translation;
            _translation = BulletXMaths.PhysicsVectorToXnaVector3(_newPos) - rigidBody.CenterOfMassPosition;
            rigidBody.Translate(_translation);
        }
        private void Speed()
        {
            Speed(this._velocity);
        }
        private void Speed(PhysicsVector _newSpeed)
        {
            MonoXnaCompactMaths.Vector3 _speed;
            _speed = BulletXMaths.PhysicsVectorToXnaVector3(_newSpeed);
            rigidBody.LinearVelocity = _speed;
        }
        private void ReOrient()
        {
            ReOrient(this._orientation);
        }
        private void ReOrient(AxiomQuaternion _newOrient)
        {
            MonoXnaCompactMaths.Quaternion _newOrientation;
            _newOrientation = BulletXMaths.AxiomQuaternionToXnaQuaternion(_newOrient);
            Matrix _comTransform = rigidBody.CenterOfMassTransform;
            BulletXMaths.SetRotation(ref _comTransform, _newOrientation);
            rigidBody.CenterOfMassTransform = _comTransform;
        }
        #endregion
    }
    /// <summary>
    /// PhysicsActor Prim Class for BulletX
    /// </summary>
    public class BulletXPrim : PhysicsActor
    {
        private PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _size;
        private PhysicsVector _acceleration;
        private AxiomQuaternion _orientation;
        //Density it will depends of material. 
        //For now all prims have the same density, all prims are made of water. Be water my friend! :D
        private const float _density = 1000.0f;
        private RigidBody rigidBody;
        //_physical value will be linked with the prim object value
        private Boolean _physical = false;

        public Axiom.Math.Vector2 RigidBodyHorizontalPosition
        {
            get
            {
                return new Axiom.Math.Vector2(this.rigidBody.CenterOfMassPosition.X, this.rigidBody.CenterOfMassPosition.Y);
            }
        }
        public BulletXPrim(BulletXScene parent_scene, PhysicsVector pos, PhysicsVector size, AxiomQuaternion rotation)
            : this(parent_scene, pos, new PhysicsVector(), size, new PhysicsVector(), rotation)
        {
        }
        public BulletXPrim(BulletXScene parent_scene, PhysicsVector pos, PhysicsVector velocity, PhysicsVector size,
            PhysicsVector aceleration, AxiomQuaternion rotation)
        {
            _position = pos;
            _velocity = velocity;
            _size = size;
            if ((size.X == 0) || (size.Y == 0) || (size.Z == 0)) throw new Exception("Size 0");

            _acceleration = aceleration;
            //Because a bug, orientation will be fixed to AxiomQuaternion.Identity
            _orientation = AxiomQuaternion.Identity;
            //_orientation = rotation;
            //---
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
                //For now all prims are boxes
                CollisionShape _collisionShape = new BoxShape(BulletXMaths.PhysicsVectorToXnaVector3(_size) / 2.0f);
                DefaultMotionState _motionState = new DefaultMotionState(_startTransform, _centerOfMassOffset);
                MonoXnaCompactMaths.Vector3 _localInertia = new MonoXnaCompactMaths.Vector3();
                _collisionShape.CalculateLocalInertia(Mass, out _localInertia); //Always when mass > 0
                rigidBody = new RigidBody(Mass, _motionState, _collisionShape, _localInertia, _linearDamping, _angularDamping, _friction, _restitution);
                rigidBody.ActivationState = ActivationState.DisableDeactivation;
                //It's seems that there are a bug with rigidBody constructor and its CenterOfMassPosition
                MonoXnaCompactMaths.Vector3 _vDebugTranslation;
                _vDebugTranslation = _startTransform.Translation - rigidBody.CenterOfMassPosition;
                rigidBody.Translate(_vDebugTranslation);
                //---
                parent_scene.ddWorld.AddRigidBody(rigidBody);
            }
        }
        public override PhysicsVector Position
        {
            get
            {
                return _position;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _position = value;
                    Translate();
                }
            }
        }
        public override PhysicsVector Velocity
        {
            get
            {
                return _velocity;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _velocity = value;
                    Speed();
                }
            }
        }
        public override PhysicsVector Size
        {
            get
            {
                return _size;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _size = value;
                    ReSize();
                }
            }
        }
        public override PhysicsVector Acceleration
        {
            get
            {
                return _acceleration;
            }
        }
        public override AxiomQuaternion Orientation
        {
            get
            {
                return _orientation;
            }
            set
            {
                lock (BulletXScene.BulletXLock)
                {
                    _orientation = value;
                    ReOrient();
                }
            }
        }
        public float Mass
        {
            get
            {
                //For now all prims are boxes
                return _density * _size.X * _size.Y * _size.Z;
            }
        }
        public override bool Flying
        {
            get
            {
                return false; //no flying prims for you
            }
            set
            {

            }
        }
        public Boolean Physical
        {
            get
            {
                return _physical;
            }
            set
            {
                _physical = value;
            }
        }
        public void SetAcceleration(PhysicsVector accel)
        {
            lock (BulletXScene.BulletXLock)
            {
                _acceleration = accel;
            }
        }
        public override bool Kinematic
        {
            get
            {
                return false;
                //return this._prim.Kinematic;
            }
            set
            {
                //this._prim.Kinematic = value;
            }
        }
        public override void AddForce(PhysicsVector force)
        {

        }
        public override void SetMomentum(PhysicsVector momentum)
        {

        }
        internal void ValidateHeight(float heighmapPositionValue)
        {
            if (rigidBody.CenterOfMassPosition.Z < heighmapPositionValue + _size.Z / 2.0f)
            {
                Matrix m = rigidBody.WorldTransform;
                MonoXnaCompactMaths.Vector3 v3 = m.Translation;
                v3.Z = heighmapPositionValue + _size.Z / 2.0f;
                m.Translation = v3;
                rigidBody.WorldTransform = m;
                //When a Prim touch the ground it's vertical velocity it's reduced to ZERO
                Speed(new PhysicsVector(this.rigidBody.LinearVelocity.X, this.rigidBody.LinearVelocity.Y, 0.0f));
            }
        }
        internal void UpdateKinetics()
        {
            if (_physical) //Updates properties. Prim updates its properties physically
            {
                this._position = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.CenterOfMassPosition);
                this._velocity = BulletXMaths.XnaVector3ToPhysicsVector(rigidBody.LinearVelocity);
                //Orientation is not implemented yet in MonoXnaCompactMaths
                //this._orientation = BulletXMaths.XnaQuaternionToAxiomQuaternion(rigidBody.Orientation); < Good
                //ReOrient();
                //---
                ReOrient();
            }
            else //Doesn't updates properties. That's a cancel
            {
                Translate();
                Speed();
                //Orientation is not implemented yet in MonoXnaCompactMaths
                //ReOrient();
                ReOrient();
            }
        }

        #region Methods for updating values of RigidBody
        private void Translate()
        {
            Translate(this._position);
        }
        private void Translate(PhysicsVector _newPos)
        {
            MonoXnaCompactMaths.Vector3 _translation;
            _translation = BulletXMaths.PhysicsVectorToXnaVector3(_newPos) - rigidBody.CenterOfMassPosition;
            rigidBody.Translate(_translation);
        }
        private void Speed()
        {
            Speed(this._velocity);
        }
        private void Speed(PhysicsVector _newSpeed)
        {
            MonoXnaCompactMaths.Vector3 _speed;
            _speed = BulletXMaths.PhysicsVectorToXnaVector3(_newSpeed);
            rigidBody.LinearVelocity = _speed;
        }
        private void ReSize()
        {
            ReSize(this._size);
        }
        private void ReSize(PhysicsVector _newSize)
        {
            MonoXnaCompactMaths.Vector3 _newsize;
            _newsize = BulletXMaths.PhysicsVectorToXnaVector3(_newSize);
            //For now all prims are Boxes
            rigidBody.CollisionShape = new BoxShape(BulletXMaths.PhysicsVectorToXnaVector3(_newSize) / 2.0f);
        }
        private void ReOrient()
        {
            ReOrient(this._orientation);
        }
        private void ReOrient(AxiomQuaternion _newOrient)
        {
            MonoXnaCompactMaths.Quaternion _newOrientation;
            _newOrientation = BulletXMaths.AxiomQuaternionToXnaQuaternion(_newOrient);
            Matrix _comTransform = rigidBody.CenterOfMassTransform;
            BulletXMaths.SetRotation(ref _comTransform, _newOrientation);
            rigidBody.CenterOfMassTransform = _comTransform;
        }
        #endregion

    }
}
