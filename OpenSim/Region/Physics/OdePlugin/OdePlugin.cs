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
using System;
using System.Threading;
using System.Collections.Generic;
using Axiom.Math;
using Ode.NET;
using OpenSim.Physics.Manager;

namespace OpenSim.Region.Physics.OdePlugin
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
            if (_mScene == null)
            {
                _mScene = new OdeScene();
            }
            return (_mScene);
        }

        public string GetName()
        {
            return ("OpenDynamicsEngine");
        }

        public void Dispose()
        {

        }
    }

    public class OdeScene : PhysicsScene
    {
        static public IntPtr world;
        static public IntPtr space;
        static private IntPtr contactgroup;
        static private IntPtr LandGeom;
        //static private IntPtr Land;
        private double[] _heightmap;
        static private d.NearCallback nearCallback = near;
        private List<OdeCharacter> _characters = new List<OdeCharacter>();
        private List<OdePrim> _prims = new List<OdePrim>();
        private static d.ContactGeom[] contacts = new d.ContactGeom[30];
        private static d.Contact contact;
        public OdeScene()
        {
            contact.surface.mode |= d.ContactFlags.Approx1 | d.ContactFlags.SoftCFM | d.ContactFlags.SoftERP;
            contact.surface.mu = 10.0f;
            contact.surface.bounce = 0.9f;
            contact.surface.soft_erp = 0.005f;
            contact.surface.soft_cfm = 0.00003f;

            Monitor.Enter(typeof(OdeScene));
            world = d.WorldCreate();
            space = d.HashSpaceCreate(IntPtr.Zero);
            contactgroup = d.JointGroupCreate(0);
            d.WorldSetGravity(world, 0.0f, 0.0f, -10.0f);   
            d.WorldSetAutoDisableFlag(world, false);
            d.WorldSetContactSurfaceLayer(world, 0.001f);
            Monitor.Exit(typeof(OdeScene));

            this._heightmap = new double[65536];
        }

        // This function blatantly ripped off from BoxStack.cs
        static private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked
            IntPtr b1 = d.GeomGetBody(g1);
            IntPtr b2 = d.GeomGetBody(g2);
            if (b1 != IntPtr.Zero && b2 != IntPtr.Zero && d.AreConnectedExcluding(b1, b2, d.JointType.Contact))
                return;

            int count = d.Collide(g1, g2, 500, contacts, d.ContactGeom.SizeOf);
            if (count>0)
            {
                if (b2 != IntPtr.Zero)
                {
                    Console.WriteLine("+++++ collision twixt: " + b1 + " & " + b2);
                }
            }
            for (int i = 0; i < count; ++i)
            {
                contact.geom = contacts[i];
                IntPtr joint = d.JointCreateContact(world, contactgroup, ref contact);
                d.JointAttach(joint, b1, b2);
            }

        }

        public override PhysicsActor AddAvatar(PhysicsVector position)
        {
            PhysicsVector pos = new PhysicsVector();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z + 20;
            OdeCharacter newAv = new OdeCharacter(this, pos);
            this._characters.Add(newAv);
            return newAv;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {

        }

        public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size, Quaternion rotation)
        {
            Console.WriteLine("+++++++++++++++++++++++++++++++++AddPrim pos: " + position + " size: " + size + " quat: " + rotation); 
            PhysicsVector pos = new PhysicsVector();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z;
            PhysicsVector siz = new PhysicsVector();
            siz.X = size.X;
            siz.Y = size.Y;
            siz.Z = size.Z;
            Quaternion rot = new Quaternion();
            rot.w = rotation.w;
            rot.x = rotation.x;
            rot.y = rotation.y;
            rot.z = rotation.z;
            OdePrim newPrim = new OdePrim(this, pos, siz, rot);
            this._prims.Add(newPrim);
            return newPrim;
        }

        public override void Simulate(float timeStep)
        {
            Monitor.Enter(typeof(OdeScene));
            foreach (OdePrim p in _prims)
            {
//                Console.WriteLine("+++ prim: " + p.Position);
            }
            foreach (OdeCharacter actor in _characters)
            {
                actor.Move(timeStep);
            }
            d.SpaceCollide(space, IntPtr.Zero, nearCallback);
            for (int i = 0; i < 50; i++)
            {
                d.WorldQuickStep(world, timeStep * 0.02f);
            }

            d.JointGroupEmpty(contactgroup);
            foreach (OdeCharacter actor in _characters)
            {
                actor.UpdatePosition();
            }
            Monitor.Exit(typeof(OdeScene));
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
            for (int i = 0; i < 65536; i++)
            {
                // this._heightmap[i] = (double)heightMap[i];
                // dbm (danx0r) -- heightmap x,y must be swapped for Ode (should fix ODE, but for now...)
                int x = i & 0xff;
                int y = i >> 8;
                this._heightmap[i] = (double)heightMap[x * 256 + y];
            }
            IntPtr HeightmapData = d.GeomHeightfieldDataCreate();

            Monitor.Enter(typeof(OdeScene));
            d.GeomHeightfieldDataBuildDouble(HeightmapData, _heightmap, 0, 256, 256, 256, 256, 1.0f, 0.0f, 2.0f, 0);
            d.GeomHeightfieldDataSetBounds(HeightmapData, 256, 256);
            LandGeom = d.CreateHeightfield(space, HeightmapData, 1);
            d.Matrix3 R = new d.Matrix3();
            
            Quaternion q1 =Quaternion.FromAngleAxis(1.5707f, new Vector3(1,0,0));
            Quaternion q2 =Quaternion.FromAngleAxis(1.5707f, new Vector3(0,1,0));
           //Axiom.Math.Quaternion q3 = Axiom.Math.Quaternion.FromAngleAxis(3.14f, new Axiom.Math.Vector3(0, 0, 1));
            
            q1 = q1 * q2;
            //q1 = q1 * q3;
            Vector3 v3 = new Vector3();
            float angle = 0;
            q1.ToAngleAxis(ref angle, ref v3);

            d.RFromAxisAndAngle(out R, v3.x, v3.y, v3.z, angle);
            d.GeomSetRotation(LandGeom, ref R); 
            d.GeomSetPosition(LandGeom, 128, 128, 0);
            Monitor.Exit(typeof(OdeScene));
        }

        public override void DeleteTerrain()
        {

        }
    }

    public class OdeCharacter : PhysicsActor
    {
        private PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _acceleration;
        private bool flying = false;
        //private float gravityAccel;
        private IntPtr BoundingCapsule;
        IntPtr capsule_geom;
        d.Mass capsule_mass;

        public OdeCharacter(OdeScene parent_scene, PhysicsVector pos)
        {
            _velocity = new PhysicsVector();
            _position = pos;
            _acceleration = new PhysicsVector();
            Monitor.Enter(typeof(OdeScene));
            d.MassSetCapsule(out capsule_mass, 50.0f, 3, 0.5f, 2f);
            capsule_geom = d.CreateSphere(OdeScene.space, 1.0f);        /// not a typo!  Spheres roll, capsules tumble
            this.BoundingCapsule = d.BodyCreate(OdeScene.world);
            d.BodySetMass(BoundingCapsule, ref capsule_mass);
            d.BodySetPosition(BoundingCapsule, pos.X, pos.Y, pos.Z);
            d.GeomSetBody(capsule_geom, BoundingCapsule);
            Monitor.Exit(typeof(OdeScene));
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

        public override PhysicsVector Size
        {
            get
            {
                return new PhysicsVector(0,0,0);
            }
            set
            {
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
            //  no lock; for now it's only called from within Simulate()
            PhysicsVector vec = new PhysicsVector();
            d.Vector3 vel = d.BodyGetLinearVel(BoundingCapsule);
            vec.X = (vel.X - this._velocity.X) * -75000.0f;
            vec.Y = (vel.Y - this._velocity.Y) * -75000.0f;
            if (flying)
            {
                vec.Z = (vel.Z - this._velocity.Z) * -75000.0f;
            }
            d.BodyAddForce(this.BoundingCapsule, vec.X, vec.Y, vec.Z);
        }

        public void UpdatePosition()
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            d.Vector3 vec = d.BodyGetPosition(BoundingCapsule);
            this._position.X = vec.X;
            this._position.Y = vec.Y;
            this._position.Z = vec.Z;
        }
    }

    public class OdePrim : PhysicsActor
    {
        private PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _size;
        private PhysicsVector _acceleration;
        private Quaternion _orientation;
        IntPtr prim_geom;

        public OdePrim(OdeScene parent_scene, PhysicsVector pos, PhysicsVector size, Quaternion rotation)
        {
            _velocity = new PhysicsVector();
            _position = pos;
            _size = size;
            _acceleration = new PhysicsVector();
            _orientation = rotation;
            prim_geom = d.CreateBox(OdeScene.space, _size.X, _size.Y, _size.Z);
            Monitor.Enter(typeof(OdeScene));
            d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
            d.Quaternion myrot = new d.Quaternion();
            myrot.W = rotation.w;
            myrot.X = rotation.x;
            myrot.Y = rotation.y;
            myrot.Z = rotation.z;
            d.GeomSetQuaternion(prim_geom, ref myrot);
            Monitor.Exit(typeof(OdeScene));
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
                return _position;
            }
            set
            {
                Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++ setting pos: " + value);
                _position = value;
                Monitor.Enter(typeof(OdeScene));
                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                Monitor.Exit(typeof(OdeScene));

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
                Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++ setting size: " + value);
                _size = value;
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

        public override Quaternion Orientation
        {
            get
            {
                return _orientation;
            }
            set
            {
                _orientation = value;
                Monitor.Enter(typeof(OdeScene));
                d.Quaternion myrot = new d.Quaternion();
                myrot.W = _orientation.w;
                myrot.X = _orientation.x;
                myrot.Y = _orientation.y;
                myrot.Z = _orientation.z;
                d.GeomSetQuaternion(prim_geom, ref myrot);
                Monitor.Exit(typeof(OdeScene));
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
