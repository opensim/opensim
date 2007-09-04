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
#region References
using System;
using System.Collections.Generic;
using OpenSim.Region.Physics.Manager;
using Axiom.Math;
//Specific References for BulletXPlugin
using MonoXnaCompactMaths;
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
        public static MonoXnaCompactMaths.Vector3 PhysicsVectorToXnaVector3(PhysicsVector physicsVector)
        {
            return new MonoXnaCompactMaths.Vector3(physicsVector.X, physicsVector.Y, physicsVector.Z);
        }
        public static void PhysicsVectorToXnaVector3(PhysicsVector physicsVector, out MonoXnaCompactMaths.Vector3 XnaVector3)
        {
            XnaVector3.X = physicsVector.X;
            XnaVector3.Y = physicsVector.Y;
            XnaVector3.Z = physicsVector.Z;
        }
        public static PhysicsVector XnaVector3ToPhysicsVector(MonoXnaCompactMaths.Vector3 xnaVector3)
        {
            return new PhysicsVector(xnaVector3.X, xnaVector3.Y, xnaVector3.Z);
        }
        /*public static void XnaVector3ToPhysicsVector(MonoXnaCompactMaths.Vector3 xnaVector3, out PhysicsVector physicsVector)
        {
            xnaVector3.X = physicsVector.X;
            xnaVector3.Y = physicsVector.Y;
            xnaVector3.Z = physicsVector.Z;
        }*/
        #region Axiom and Xna
        ///// <summary>
        ///// BTW maybe some conversions will be a simply converion that it doesn't require this class, but i don't know
        ///// </summary>
        ///// <param name="AxiomVector3"></param>
        ///// <returns></returns>
        //public static MonoXnaCompactMaths.Vector3 Vector3AxiomToXna(Axiom.Math.Vector3 AxiomVector3)
        //{
        //    return new MonoXnaCompactMaths.Vector3(AxiomVector3.x, AxiomVector3.y, AxiomVector3.z);
        //}
        #endregion
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
            this._characters.Add(newAv);
            return newAv;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {

        }

        public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size, Axiom.Math.Quaternion rotation)
        {
            PhysicsVector pos = new PhysicsVector();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z;
            PhysicsVector siz = new PhysicsVector();
            siz.X = size.X;
            siz.Y = size.Y;
            siz.Z = size.Z;
            return new BulletXPrim();
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            throw new Exception("The method or operation is not implemented.");
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
        private PhysicsVector _acceleration;
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
        {
            _velocity = new PhysicsVector();
            _position = pos;
            _acceleration = new PhysicsVector();
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

        public override PhysicsVector Size
        {
            get
            {
                throw new Exception("The method or operation is not implemented.");
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
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

        public override Axiom.Math.Quaternion Orientation
        {
            get
            {
                return Axiom.Math.Quaternion.Identity;
            }
            set
            {

            }
        }

        public override PhysicsVector Acceleration
        {
            get
            {
                return _acceleration;
            }

        }
        public void SetAcceleration(PhysicsVector accel)
        {
            this._acceleration = accel;
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
        private PhysicsVector _acceleration;

        public BulletXPrim()
        {
            _velocity = new PhysicsVector();
            _position = new PhysicsVector();
            _acceleration = new PhysicsVector();
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
        public override PhysicsVector Size
        {
            get
            {
                throw new Exception("The method or operation is not implemented.");
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }
        public override PhysicsVector Position
        {
            get
            {
                PhysicsVector pos = new PhysicsVector();
                //	PhysicsVector vec = this._prim.Position;
                //pos.X = vec.X;
                //pos.Y = vec.Y;
                //pos.Z = vec.Z;
                return pos;

            }
            set
            {
                /*PhysicsVector vec = value;
                PhysicsVector pos = new PhysicsVector();
                pos.X = vec.X;
                pos.Y = vec.Y;
                pos.Z = vec.Z;
                this._prim.Position = pos;*/
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
        public override Axiom.Math.Quaternion Orientation
        {
            get
            {
                Axiom.Math.Quaternion res = new Axiom.Math.Quaternion();
                return res;
            }
            set
            {

            }
        }
        public override PhysicsVector Acceleration
        {
            get
            {
                return _acceleration;
            }

        }
        public void SetAcceleration(PhysicsVector accel)
        {
            this._acceleration = accel;
        }
        public override void AddForce(PhysicsVector force)
        {

        }
        public override void SetMomentum(PhysicsVector momentum)
        {

        }
    }
}

