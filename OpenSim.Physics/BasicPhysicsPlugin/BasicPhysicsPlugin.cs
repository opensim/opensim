/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using OpenSim.Physics.Manager;

namespace OpenSim.Physics.BasicPhysicsPlugin
{
	/// <summary>
	/// Will be the PhysX plugin but for now will be a very basic physics engine
	/// </summary>
	public class BasicPhysicsPlugin : IPhysicsPlugin
	{
		private BasicScene _mScene;
		
		public BasicPhysicsPlugin()
		{
			
		}
		
		public bool Init()
		{
			return true;
		}
		
		public PhysicsScene GetScene()
		{
			if(_mScene == null)
			{
				_mScene = new BasicScene();
			}
			return(_mScene);
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
		
		public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size)
		{
			return null;
		}
		
		public override void Simulate(float timeStep)
		{
			foreach (BasicActor actor in _actors)
			{
				actor.Position.X = actor.Position.X + (actor.Velocity.X * timeStep);
				actor.Position.Y = actor.Position.Y + (actor.Velocity.Y * timeStep);
				actor.Position.Z = actor.Position.Z + (actor.Velocity.Z * timeStep);
				/*if(actor.Flying)
				{
					actor.Position.Z = actor.Position.Z + (actor.Velocity.Z * timeStep);
				}
				else
				{
					actor.Position.Z = actor.Position.Z + ((-9.8f + actor.Velocity.Z) * timeStep);
				}
				if(actor.Position.Z < (_heightMap[(int)actor.Position.Y * 256 + (int)actor.Position.X]+1))
				{*/
					actor.Position.Z = _heightMap[(int)actor.Position.Y * 256 + (int)actor.Position.X]+1;
				//}
				if(actor.Position.X<0)
				{
					actor.Position.X = 0;
					actor.Velocity.X = 0;
				}
				if(actor.Position.Y < 0)
				{
					actor.Position.Y = 0;
					actor.Velocity.Y = 0;
				}
				if(actor.Position.X > 255)
				{
					actor.Position.X = 255;
					actor.Velocity.X = 0;
				}
				if(actor.Position.Y > 255) 
				{
					actor.Position.Y = 255;
					actor.Velocity.X = 0;
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
				return false;
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
		
		public override Axiom.MathLib.Quaternion Orientation
		{
			get
			{
				return Axiom.MathLib.Quaternion.Identity;
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
