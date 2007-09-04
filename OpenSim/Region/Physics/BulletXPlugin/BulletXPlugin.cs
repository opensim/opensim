#region Copyright
/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are 
met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written 
permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF 
THIS
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
    /// This Class converts objects and types for BulletX
    /// </summary>
    public class BulletXConversions
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
            return ("BulletXEngine");
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
        public DiscreteDynamicsWorld ddWorld;
		private CollisionDispatcher cDispatcher;
		private OverlappingPairCache opCache;
		private SequentialImpulseConstraintSolver sicSolver;

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

        public BulletXScene()
        {
            cDispatcher = new CollisionDispatcher();
            MonoXnaCompactMaths.Vector3 worldMinDim = new MonoXnaCompactMaths.Vector3((float)minXY, (float)minXY, (float)minZ);
            MonoXnaCompactMaths.Vector3 worldMaxDim = new MonoXnaCompactMaths.Vector3((float)maxXY, (float)maxXY, (float)maxZ);
            opCache = new AxisSweep3(worldMinDim, worldMaxDim, maxHandles);
            sicSolver = new SequentialImpulseConstraintSolver();

            ddWorld = new DiscreteDynamicsWorld(cDispatcher, opCache, sicSolver);
            ddWorld.Gravity = new MonoXnaCompactMaths.Vector3(0, 0, -gravity);

            this._heightmap = new float[65536];
        }
        public override PhysicsActor AddAvatar(PhysicsVector position)
        {
            PhysicsVector pos = new PhysicsVector();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z + 20;
            BulletXCharacter newAv = new BulletXCharacter(this, pos);
            _characters.Add(newAv);
            return newAv;
        }
        public override void RemoveAvatar(PhysicsActor actor)
        {
            if (actor is BulletXCharacter)
            {
                _characters.Remove((BulletXCharacter)actor);
            }
        }
        public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size, AxiomQuaternion rotation)
        {
            return new BulletXPrim(this, position, size, rotation);
        }
        public override void RemovePrim(PhysicsActor prim)
        {
            if (prim is BulletXPrim)
            {
                _prims.Remove((BulletXPrim)prim);
            }
        }
        public override void Simulate(float timeStep)
        {
            foreach (BulletXCharacter actor in _characters)
            {
                actor.Move(timeStep);

            }
            ddWorld.StepSimulation(timeStep, 0, timeStep);
            foreach (BulletXCharacter actor in _characters)
            {
                actor.ValidateHeight(this._heightmap[
                    (int)Math.Round(actor.RigidBodyHorizontalPosition.x) * 256
                    + (int)Math.Round(actor.RigidBodyHorizontalPosition.y)]);
            }
            foreach (BulletXCharacter actor in _characters)
            {
                actor.UpdatePosition();
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
            _position = pos;
            _velocity = velocity;
            _size = size;
            _acceleration = acceleration;
            _orientation = orientation;
            float _mass = 50.0f; //This depends of avatar's dimensions
            Matrix _startTransform = Matrix.Identity;
            _startTransform.Translation = BulletXConversions.PhysicsVectorToXnaVector3(pos);
            Matrix _centerOfMassOffset = Matrix.Identity;
            CollisionShape _collisionShape = new BoxShape(new MonoXnaCompactMaths.Vector3(0.5f, 0.5f, 1.60f));
            DefaultMotionState _motionState = new DefaultMotionState(_startTransform, _centerOfMassOffset);
            MonoXnaCompactMaths.Vector3 _localInertia = new MonoXnaCompactMaths.Vector3();
            _collisionShape.CalculateLocalInertia(_mass, out _localInertia); 
//Always when mass > 0

            //The next values might change
            float _linearDamping = 0.0f;
            float _angularDamping = 0.0f;
            float _friction = 0.5f;
            float _restitution = 0.0f;

            rigidBody = new RigidBody(_mass, _motionState, _collisionShape, _localInertia, _linearDamping, _angularDamping, _friction, _restitution);
            rigidBody.ActivationState = ActivationState.DisableDeactivation;

            parent_scene.ddWorld.AddRigidBody(rigidBody);
        }
        public override PhysicsVector Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
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
                _velocity = value;
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
                _size = value;
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
                _orientation = value;
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
            _acceleration = accel;
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
        public void Move(float timeStep)
        {
            MonoXnaCompactMaths.Vector3 vec = new MonoXnaCompactMaths.Vector3();
            //if (this._velocity.X == 0.0f)
            //    vec.X = this.rigidBody.LinearVelocity.X; //current velocity
            //else
                vec.X = this._velocity.X; //overrides current velocity

            //if (this._velocity.Y == 0.0f)
            //    vec.Y = this.rigidBody.LinearVelocity.Y; //current velocity
            //else
                vec.Y = this._velocity.Y; //overrides current velocity

            float nextZVelocity;
            //if (this._velocity.Z == 0.0f)
            //    nextZVelocity = this.rigidBody.LinearVelocity.Z; //current velocity
            //else
                nextZVelocity = this._velocity.Z; //overrides current velocity

            if (flying)
            {
                //Antigravity with movement
                if (this._position.Z <= BulletXScene.HeightLevel0)
                {
                    vec.Z = nextZVelocity + BulletXScene.Gravity * timeStep;
                }
                //Lowgravity with movement
                else if((this._position.Z > BulletXScene.HeightLevel0)
                    && (this._position.Z <= BulletXScene.HeightLevel1))
                {
                    vec.Z = nextZVelocity + BulletXScene.Gravity * timeStep * (1.0f - BulletXScene.LowGravityFactor);
                }
                //Lowgravity with...
                else if (this._position.Z > BulletXScene.HeightLevel1)
                {
                    if(nextZVelocity > 0) //no movement
                        vec.Z = BulletXScene.Gravity * timeStep * (1.0f - BulletXScene.LowGravityFactor);
                    else
                        vec.Z = nextZVelocity + BulletXScene.Gravity * timeStep * (1.0f - BulletXScene.LowGravityFactor);

                }
            }
            else
            {
                vec.Z = nextZVelocity;
            }
            rigidBody.LinearVelocity = vec;
        }
        public void UpdatePosition()
        {
            this._position = BulletXConversions.XnaVector3ToPhysicsVector(rigidBody.CenterOfMassPosition);
        }
        //This validation is very basic
        internal void ValidateHeight(float heighmapPositionValue)
        {
            if (rigidBody.CenterOfMassPosition.Z < heighmapPositionValue)
            {
                Matrix m = rigidBody.WorldTransform;
                MonoXnaCompactMaths.Vector3 v3 = m.Translation;
                v3.Z = heighmapPositionValue;
                m.Translation = v3;
                rigidBody.WorldTransform = m;
            }
        }
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
            _acceleration = aceleration;
            _orientation = rotation;
        }
        public override PhysicsVector Position
        {
            get
            {
                return _position;

            }
            set
            {
                _position = value;
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
                _velocity = value;
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
                _size = value;
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
                _orientation = value;
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
        public void SetAcceleration(PhysicsVector accel)
        {
            _acceleration = accel;
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
    }
}

