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
using System.Collections.Generic;
using Axiom.Math;
using OpenSim.Physics.Manager;

namespace OpenSim.Region.Physics.BasicPhysicsPlugin
{
	/// <summary>
	/// Will be the PhysX plugin but for now will be a very basic physics engine
	/// </summary>
	public class BasicPhysicsPlugin : IPhysicsPlugin
	{
		public BasicPhysicsPlugin()
		{
			
		}
		
		public bool Init()
		{
			return true;
		}
		
		public PhysicsScene GetScene()
		{
		    return new BasicScene();
		}
		
		public string GetName()
		{
			return("basicphysics");
		}
		
		public void Dispose()
		{
			
		}
	}
	
	public class BasicScene :PhysicsScene
	{
		private List<BasicActor> _actors = new List<BasicActor>();
		private float[] _heightMap;
		
		public BasicScene()
		{
			
		}
		
		public override PhysicsActor AddAvatar(PhysicsVector position)
		{
			BasicActor act = new BasicActor();
			act.Position = position;
			_actors.Add(act);
			return act;
		}

        public override void RemoveAvatar(PhysicsActor actor)
        {
            BasicActor act = (BasicActor)actor;
            if(_actors.Contains(act))
            {
                _actors.Remove(act);
            }

        }

		public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size)
		{
			return null;
		}
		
		public override void Simulate(float timeStep)
		{
			foreach (BasicActor actor in _actors)
			{
                float height = _heightMap[(int)actor.Position.Y * 256 + (int)actor.Position.X] + 1.2f;
                actor.Position.X = actor.Position.X + (actor.Velocity.X * timeStep);
                actor.Position.Y = actor.Position.Y + (actor.Velocity.Y * timeStep);
                if (actor.Flying)
                    {
                    if (actor.Position.Z + (actor.Velocity.Z * timeStep) < _heightMap[(int)actor.Position.Y * 256 + (int)actor.Position.X] + 2)
                    {
                        actor.Position.Z = height;
                        actor.Velocity.Z = 0;
                    }
                    else
                    {
                        actor.Position.Z = actor.Position.Z + (actor.Velocity.Z * timeStep);
                    }
                }
                else
                {
                    actor.Position.Z = height;
                    actor.Velocity.Z = 0;
                }

                if (actor.Position.Y < 0)
                {
                    actor.Position.Y = 0;
                }
                else if (actor.Position.Y > 256)
                {
                    actor.Position.Y = 256;
                }

                if (actor.Position.X < 0)
                {
                    actor.Position.X = 0;
                }
                if (actor.Position.X > 256)
                {
                    actor.Position.X = 256;
                }
            }
		}
		
		public override void GetResults()
		{
		
		}
		
		public override bool IsThreaded
		{
			get
			{
				return(false); // for now we won't be multithreaded
			}
		}
		
		public override void SetTerrain(float[] heightMap)
		{
			this._heightMap = heightMap;
		}

        public override void DeleteTerrain()
        {

        }
	}
	
	public  class BasicActor : PhysicsActor
	{
		private PhysicsVector _position;
		private PhysicsVector _velocity;
		private PhysicsVector _acceleration;
		private bool flying;
		public BasicActor()
		{
			_velocity = new PhysicsVector();
			_position = new PhysicsVector();
			_acceleration = new PhysicsVector();
		}
		
		public override bool Flying
		{
			get
			{
                return flying;
			}
			set
			{
				flying= value;
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
		
		public override Quaternion Orientation
		{
			get
			{
				return Quaternion.Identity;
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
		
		public override bool Kinematic
		{
			get
			{
				return true;
			}
			set
			{
				
			}
		}
		public void SetAcceleration (PhysicsVector accel)
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
