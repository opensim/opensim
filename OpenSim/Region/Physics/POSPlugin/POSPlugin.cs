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
using System.Collections.Generic;
using Axiom.Math;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.POSPlugin
{
    /// <summary>
    /// for now will be a very POS physics engine
    /// </summary>
    public class POSPlugin : IPhysicsPlugin
    {
        public POSPlugin()
        {
        }

        public bool Init()
        {
            return true;
        }

        public PhysicsScene GetScene()
        {
            return new POSScene();
        }

        public string GetName()
        {
            return ("POS");
        }

        public void Dispose()
        {
        }
    }

    public class POSScene : PhysicsScene
    {
        private List<POSActor> _actors = new List<POSActor>();
        private float[] _heightMap;
        private const float gravity = -1.0f;

        public POSScene()
        {
        }

        public override void Initialise(IMesher meshmerizer)
        {
            // Does nothing right now
        }

        public override PhysicsActor AddAvatar(string avName, PhysicsVector position)
        {
            POSActor act = new POSActor();
            act.Position = position;
            _actors.Add(act);
            return act;
        }

        public override void RemovePrim(PhysicsActor prim)
        {
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            POSActor act = (POSActor) actor;
            if (_actors.Contains(act))
            {
                _actors.Remove(act);
            }
        }

/*
        public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size, Quaternion rotation)
        {
            return null;
        }
*/

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                  PhysicsVector size, Quaternion rotation)
        {
            return this.AddPrimShape(primName, pbs, position, size, rotation, false);
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position, 
                                                  PhysicsVector size, Quaternion rotation, bool isPhysical)
        {
            return null;
        }

        public override void Simulate(float timeStep)
        {
            foreach (POSActor actor in _actors)
            {
                actor.Position.X += actor.Velocity.X * timeStep;
                actor.Position.Y += actor.Velocity.Y * timeStep;
                actor.Position.Z += actor.Velocity.Z * timeStep;

                if (!actor.Flying)
                {
                    actor.Velocity.Z += gravity;
                }

                if (actor.Position.X < 0)
                {
                    actor.Position.X = 0.1F;
                }
                else if (actor.Position.X > 256)
                {
                    actor.Position.X = 255.9F;
                }

                if (actor.Position.Y < 0)
                {
                    actor.Position.Y = 0.1F;
                }
                else if (actor.Position.Y >= 256)
                {
                    actor.Position.Y = 255.9F;
                }

                float terrainheight = _heightMap[(int) actor.Position.Y * 256 + (int) actor.Position.X];
                if (actor.Position.Z + (actor.Velocity.Z * timeStep) < terrainheight + 2)
                {
                    actor.Position.Z = terrainheight + 1.0f;
                    actor.Velocity.Z = 0;
                }
            }
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            get { return (false); // for now we won't be multithreaded
            }
        }

        public override void SetTerrain(float[] heightMap)
        {
            _heightMap = heightMap;
        }

        public override void DeleteTerrain()
        {
        }
    }

    public class POSActor : PhysicsActor
    {
        private PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _acceleration;
        private PhysicsVector m_rotationalVelocity = PhysicsVector.Zero;
        private bool flying;
        private bool iscolliding;

        public POSActor()
        {
            _velocity = new PhysicsVector();
            _position = new PhysicsVector();
            _acceleration = new PhysicsVector();
        }
        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Agent; }
            set { return; }
        }
        public override PhysicsVector RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }
        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }
        public override bool IsPhysical
        {
            get { return false; }
            set { return; }
        }
        public override bool ThrottleUpdates
        {
            get { return false; }
            set { return; }
        }
        public override bool Flying
        {
            get { return flying; }
            set { flying = value; }
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
        public override PhysicsVector Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public override PhysicsVector Size
        {
            get { return new PhysicsVector(0, 0, 0); }
            set { }
        }

        public override PrimitiveBaseShape Shape
        {
            set
            {
                return;
            }
        }

        public override PhysicsVector Velocity
        {
            get { return _velocity; }
            set { _velocity = value; }
        }

        public override Quaternion Orientation
        {
            get { return Quaternion.Identity; }
            set { }
        }

        public override PhysicsVector Acceleration
        {
            get { return _acceleration; }
        }

        public override bool Kinematic
        {
            get { return true; }
            set { }
        }

        public void SetAcceleration(PhysicsVector accel)
        {
            _acceleration = accel;
        }

        public override void AddForce(PhysicsVector force)
        {
        }

        public override void SetMomentum(PhysicsVector momentum)
        {
        }
    }
}
