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
using System;
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
        private List<POSCharacter> _characters = new List<POSCharacter>();
        private List<POSPrim> _prims = new List<POSPrim>();
        private float[] _heightMap;
        private const float gravity = -9.8f;

        public POSScene()
        {
        }

        public override void Initialise(IMesher meshmerizer)
        {
            // Does nothing right now
        }

        public override PhysicsActor AddAvatar(string avName, PhysicsVector position)
        {
            POSCharacter act = new POSCharacter();
            act.Position = position;
            _characters.Add(act);
            return act;
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            POSPrim p = (POSPrim) prim;
            if (_prims.Contains(p))
            {
                _prims.Remove(p);
            }
        }

        public override void RemoveAvatar(PhysicsActor character)
        {
            POSCharacter act = (POSCharacter) character;
            if (_characters.Contains(act))
            {
                _characters.Remove(act);
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
            POSPrim prim = new POSPrim();
            prim.Position = position;
            prim.Orientation = rotation;
            prim.Size = size;
            _prims.Add(prim);
            return prim;
        }

        private bool check_collision(POSCharacter c, POSPrim p)
        {
            /*
            Console.WriteLine("checking whether " + c + " collides with " + p +
                " absX: " + Math.Abs(p.Position.X - c.Position.X) +
                " sizeX: " + p.Size.X * 0.5 + 0.5);
             */

            Vector3 rotatedPos = p.Orientation.Inverse() * new Vector3(c.Position.X - p.Position.X, c.Position.Y - p.Position.Y, c.Position.Z - p.Position.Z);
            Vector3 avatarSize = p.Orientation.Inverse() * new Vector3(c.Size.X, c.Size.Y, c.Size.Z);

            if (Math.Abs(rotatedPos.x) >= (p.Size.X * 0.5 + Math.Abs(avatarSize.x)))
            {
                return false;
            }
            if (Math.Abs(rotatedPos.y) >= (p.Size.Y * 0.5 + Math.Abs(avatarSize.y)))
            {
                return false;
            }
            if (Math.Abs(rotatedPos.z) >= (p.Size.Z * 0.5 + Math.Abs(avatarSize.z)))
            {
                return false;
            }
            return true;
        }

        private bool check_all_prims(POSCharacter c)
        {
            foreach (POSPrim p in _prims)
            {
                if (check_collision(c, p))
                    return true;
            }
            return false;
        }
        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {

        }
        public override void Simulate(float timeStep)
        {
            foreach (POSCharacter character in _characters)
            {
                float oldposX = character.Position.X;
                float oldposY = character.Position.Y;
                float oldposZ = character.Position.Z;

                if (!character.Flying)
                {
                    character._target_velocity.Z += gravity * timeStep;
                }

                bool forcedZ = false;

                float terrainheight = _heightMap[(int)(character.Position.Y + (character._target_velocity.Y * timeStep)) * 256 + (int)(character.Position.X + (character._target_velocity.X * timeStep))];
                if (character.Position.Z + (character._target_velocity.Z * timeStep) < terrainheight + 2)
                {
                    character.Position.Z = terrainheight + 1.0f;
                    forcedZ = true;
                }
                else
                {
                    character.Position.Z = character.Position.Z + (character._target_velocity.Z * timeStep);
                }

                /// this is it -- the magic you've all been waiting for!  Ladies and gentlemen -- 
                /// Completely Bogus Collision Detection!!!
                /// better known as the CBCD algorithm

                if (check_all_prims(character))
                {
                    character.Position.Z = oldposZ;                 //  first try Z axis
                    if (check_all_prims(character))
                    {
                        character.Position.Z = oldposZ + 0.5f;                 // try harder
                    }
                    else
                    {
                        forcedZ = true;
                    }
                }            
                character.Position.X = character.Position.X + (character._target_velocity.X * timeStep);
                if (check_all_prims(character))
                {
                    character.Position.X = oldposX;
                }
                character.Position.Y = character.Position.Y + (character._target_velocity.Y * timeStep);
                if (check_all_prims(character))
                {
                    character.Position.Y = oldposY;
                }
                

                if (character.Position.Y < 0)
                {
                    character.Position.Y = 0.1F;
                }
                else if (character.Position.Y >= 256)
                {
                    character.Position.Y = 255.9F;
                }

                if (character.Position.X < 0)
                {
                    character.Position.X = 0.1F;
                }
                else if (character.Position.X > 256)
                {
                    character.Position.X = 255.9F;
                }

                character._velocity.X = (character.Position.X - oldposX) / timeStep;
                character._velocity.Y = (character.Position.Y - oldposY) / timeStep;

                if (forcedZ)
                {
                    character._velocity.Z = 0;
                    character._target_velocity.Z = 0;
                    character.RequestPhysicsterseUpdate();
                }
                else
                {
                    character._velocity.Z = (character.Position.Z - oldposZ) / timeStep;
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

    public class POSCharacter : PhysicsActor
    {
        private PhysicsVector _position;
        public PhysicsVector _velocity;
        public PhysicsVector _target_velocity = PhysicsVector.Zero;
        private PhysicsVector _acceleration;
        private PhysicsVector m_rotationalVelocity = PhysicsVector.Zero;
        private bool flying;
        private bool iscolliding;

        public POSCharacter()
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
            get { return new PhysicsVector(0.5f, 0.5f, 1.0f); }
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
            set { _target_velocity = value; }
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

    public class POSPrim : PhysicsActor
    {
        private PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _acceleration;
        private PhysicsVector _size;
        private PhysicsVector m_rotationalVelocity = PhysicsVector.Zero;
        private Quaternion _orientation;
        private bool iscolliding;

        public POSPrim()
        {
            _velocity = new PhysicsVector();
            _position = new PhysicsVector();
            _acceleration = new PhysicsVector();
        }
        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Prim; }
            set { return; }
        }
        public override PhysicsVector RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
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
            get { return _size; }
            set { _size = value; }
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
            get { return _orientation; }
            set { _orientation = value; }
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
        public override bool Flying
        {
            get { return false; }
            set { }
        }

        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }
    }
}
