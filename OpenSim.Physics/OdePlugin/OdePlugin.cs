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
using Ode.NET;

namespace OpenSim.Physics.OdePlugin
{
	/// <summary>
	/// ODE plugin 
	/// </summary>
	public class OdePlugin : IPhysicsPlugin
	{
		private OdeScene _mScene;
			
		public OdePlugin()
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
                                _mScene = new OdeScene();
                        }
                        return(_mScene);
		}
		
		public string GetName()
		{
			return("OpenDynamicsEngine");
		}
		
		public void Dispose()
		{
			
		}
	}
	
	public class OdeScene :PhysicsScene
	{
		private	IntPtr world;
		private IntPtr space;
		private IntPtr contactgroup;
		private double[] _heightmap;

		public OdeScene()
		{
			world = d.WorldCreate();
                        space = d.HashSpaceCreate(IntPtr.Zero);
                        contactgroup = d.JointGroupCreate(0);
			d.WorldSetGravity(world, 0.0f, 0.0f, -0.5f);
                        d.WorldSetCFM(world, 1e-5f);
                        d.WorldSetAutoDisableFlag(world, true);
                        d.WorldSetContactMaxCorrectingVel(world, 0.1f);
                        d.WorldSetContactSurfaceLayer(world, 0.001f);
		}
		
		public override PhysicsActor AddAvatar(PhysicsVector position)
		{
			PhysicsVector pos = new PhysicsVector();
			pos.X = position.X;
			pos.Y = position.Y;
			pos.Z = position.Z;
			return new OdeCharacter();
		}
		
		public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size)
		{
			PhysicsVector pos = new PhysicsVector();
			pos.X = position.X;
			pos.Y = position.Y;
			pos.Z = position.Z;
			PhysicsVector siz = new PhysicsVector();
			siz.X = size.X;
			siz.Y = size.Y;
			siz.Z = size.Z;
			return new OdePrim();
		}
		
		public override void Simulate(float timeStep)
		{
				
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
			for(int i=0; i<65536; i++) {
				this._heightmap[i]=(double)heightMap[i];
			}
			IntPtr HeightmapData = d.GeomHeightfieldDataCreate();
			d.GeomHeightfieldDataBuildDouble(HeightmapData,_heightmap,1,256,256,256,256,1.0f,0.0f,2.0f,0);
			d.CreateHeightfield(space, HeightmapData, 0);
		}
	}
	
	public  class OdeCharacter : PhysicsActor
	{
		private PhysicsVector _position;
		private PhysicsVector _velocity;
		private PhysicsVector _acceleration;
		private bool flying;
		private float gravityAccel;
		
		public OdeCharacter()
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
				flying = value;
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
		
		public void Move(float timeStep)
		{
			PhysicsVector vec = new PhysicsVector();
			vec.X = this._velocity.X * timeStep;
			vec.Y = this._velocity.Y * timeStep;
			if(flying)
			{
				vec.Z = ( this._velocity.Z) * timeStep;
			}
			else
			{
				gravityAccel+= -9.8f;
				vec.Z = (gravityAccel + this._velocity.Z) * timeStep;
			}
			//int res = this._character.Move(vec);
			//if(res == 1)
			//{
			//	gravityAccel = 0;
			//}
		}
		
		public void UpdatePosition()
		{
		}
	}
	
	public  class OdePrim : PhysicsActor
	{
		private PhysicsVector _position;
		private PhysicsVector _velocity;
		private PhysicsVector _acceleration;
		
		public OdePrim()
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
		public override PhysicsVector Position
		{
			get
			{
				PhysicsVector pos = new PhysicsVector();
				//PhysicsVector vec = this._prim.Position;
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
		
		public override Axiom.MathLib.Quaternion Orientation
		{
			get
			{
				Axiom.MathLib.Quaternion res = new Axiom.MathLib.Quaternion();
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
