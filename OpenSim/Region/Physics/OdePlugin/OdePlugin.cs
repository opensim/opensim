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

using System;
using System.Collections.Generic;
using Axiom.Math;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

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
        private static float ODE_STEPSIZE = 0.004f;
        private static bool RENDER_FLAG = false;
        private IntPtr contactgroup;
        private IntPtr LandGeom = (IntPtr) 0;
        private double[] _heightmap;
        private d.NearCallback nearCallback;
        public d.TriCallback triCallback;
        public d.TriArrayCallback triArrayCallback;
        private List<OdeCharacter> _characters = new List<OdeCharacter>();
        private List<OdePrim> _prims = new List<OdePrim>();
        public Dictionary<IntPtr, String> geom_name_map = new Dictionary<IntPtr, String>();
        public Dictionary<IntPtr, PhysicsActor> actor_name_map = new Dictionary<IntPtr, PhysicsActor>();
        private d.ContactGeom[] contacts = new d.ContactGeom[30];
        private d.Contact contact;
        private PhysicsActor PANull = new NullPhysicsActor();
        private float step_time = 0.0f;
        public IntPtr world;
        public IntPtr space;
        public static Object OdeLock = new Object();

        public OdeScene()
        {
            nearCallback = near;
            triCallback = TriCallback;
            triArrayCallback = TriArrayCallback;
            /*
            contact.surface.mode |= d.ContactFlags.Approx1 | d.ContactFlags.SoftCFM | d.ContactFlags.SoftERP;
            contact.surface.mu = 10.0f;
            contact.surface.bounce = 0.9f;
            contact.surface.soft_erp = 0.005f;
            contact.surface.soft_cfm = 0.00003f;
            */
            contact.surface.mu = 250.0f;
            contact.surface.bounce = 0.2f;
            lock (OdeLock)
            {
                world = d.WorldCreate();
                space = d.HashSpaceCreate(IntPtr.Zero);
                contactgroup = d.JointGroupCreate(0);
                d.WorldSetGravity(world, 0.0f, 0.0f, -10.0f);
                d.WorldSetAutoDisableFlag(world, false);
                d.WorldSetContactSurfaceLayer(world, 0.001f);
                d.WorldSetQuickStepNumIterations(world, 10);
                d.WorldSetContactMaxCorrectingVel(world, 1000.0f);
            }

            _heightmap = new double[258*258];
            
        }

        // This function blatantly ripped off from BoxStack.cs
        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked
            IntPtr b1 = d.GeomGetBody(g1);
            IntPtr b2 = d.GeomGetBody(g2);


            if (g1 == g2)
                return; // Can't collide with yourself
  


            if (b1 != IntPtr.Zero && b2 != IntPtr.Zero && d.AreConnectedExcluding(b1, b2, d.JointType.Contact))
                return;


            d.GeomClassID id = d.GeomGetClass(g1);
            if (id == d.GeomClassID.TriMeshClass)
            {
                

//               MainLog.Instance.Verbose("near: A collision was detected between {1} and {2}", 0, name1, name2);
                //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
            }

            int count = d.Collide(g1, g2, contacts.GetLength(0), contacts, d.ContactGeom.SizeOf);
            for (int i = 0; i < count; i++)
            {
                contact.geom = contacts[i];
                IntPtr joint = d.JointCreateContact(world, contactgroup, ref contact);
                d.JointAttach(joint, b1, b2);
                PhysicsActor p1;
                PhysicsActor p2;


                if (!actor_name_map.TryGetValue(g1, out p1))
                {
                    p1 = PANull;
                }
                if (!actor_name_map.TryGetValue(g2, out p2))
                {
                    p2 = PANull;
                }

                p1.IsColliding = true;
                p2.IsColliding = true;
                //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
            }
        }

        private void collision_optimized()
        {
            foreach (OdeCharacter chr in _characters)
            {
                chr.IsColliding = false;
            }
            foreach (OdeCharacter chr in _characters)
            {
                
                    
               
                d.SpaceCollide2(space, chr.Shell, IntPtr.Zero, nearCallback);
                foreach (OdeCharacter ch2 in _characters)
                    /// should be a separate space -- lots of avatars will be N**2 slow
                {   

                    
                    d.SpaceCollide2(chr.Shell, ch2.Shell, IntPtr.Zero, nearCallback);
                }
            }
        }

        public override PhysicsActor AddAvatar(string avName, PhysicsVector position)
        {
            PhysicsVector pos = new PhysicsVector();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z;
            OdeCharacter newAv = new OdeCharacter(avName, this, pos);
            _characters.Add(newAv);
            return newAv;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            lock (OdeLock)
            {
                ((OdeCharacter) actor).Destroy();
                _characters.Remove((OdeCharacter) actor);
            }
        }

        public override void RemovePrim(PhysicsActor prim)
        {
            if (prim is OdePrim)
            {
                lock (OdeLock)
                {
                    d.GeomDestroy(((OdePrim) prim).prim_geom);
                    _prims.Remove((OdePrim) prim);
                }
            }
        }

        private PhysicsActor AddPrim(String name, PhysicsVector position, PhysicsVector size, Quaternion rotation,
                                     Mesh mesh, PrimitiveBaseShape pbs)
        {
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
            OdePrim newPrim;
            lock (OdeLock)
            {
                newPrim = new OdePrim(name, this, pos, siz, rot, mesh, pbs);
            }
            _prims.Add(newPrim);
            return newPrim;
        }


        public int TriArrayCallback(IntPtr trimesh, IntPtr refObject, int[] triangleIndex, int triCount)
        {
/*            String name1 = null;
            String name2 = null;

            if (!geom_name_map.TryGetValue(trimesh, out name1))
            {
                name1 = "null";
            }
            if (!geom_name_map.TryGetValue(refObject, out name2))
            {
                name2 = "null";
            }

            MainLog.Instance.Verbose("TriArrayCallback: A collision was detected between {1} and {2}", 0, name1, name2);
*/
            return 1;
        }

        public int TriCallback(IntPtr trimesh, IntPtr refObject, int triangleIndex)
        {
            String name1 = null;
            String name2 = null;

            if (!geom_name_map.TryGetValue(trimesh, out name1))
            {
                name1 = "null";
            }
            if (!geom_name_map.TryGetValue(refObject, out name2))
            {
                name2 = "null";
            }

//            MainLog.Instance.Verbose("TriCallback: A collision was detected between {1} and {2}. Index was {3}", 0, name1, name2, triangleIndex);

            d.Vector3 v0 = new d.Vector3();
            d.Vector3 v1 = new d.Vector3();
            d.Vector3 v2 = new d.Vector3();

            d.GeomTriMeshGetTriangle(trimesh, 0, ref v0, ref v1, ref v2);
//            MainLog.Instance.Debug("Triangle {0} is <{1},{2},{3}>, <{4},{5},{6}>, <{7},{8},{9}>", triangleIndex, v0.X, v0.Y, v0.Z, v1.X, v1.Y, v1.Z, v2.X, v2.Y, v2.Z);

            return 1;
        }


        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                  PhysicsVector size, Quaternion rotation) //To be removed
        {
            return this.AddPrimShape(primName, pbs, position, size, rotation, false);
        }
        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                  PhysicsVector size, Quaternion rotation, bool isPhysical)
        {
            PhysicsActor result;

            switch (pbs.ProfileShape)
            {
                case ProfileShape.Square:
                    /// support simple box & hollow box now; later, more shapes
                    if (pbs.ProfileHollow == 0)
                    {
                        result = AddPrim(primName, position, size, rotation, null, null);
                    }
                    else
                    {
                        Mesh mesh = Meshmerizer.CreateMesh(pbs, size);
                        result = AddPrim(primName, position, size, rotation, mesh, pbs);
                    }
                    break;

                default:
                    result = AddPrim(primName, position, size, rotation, null, null);
                    break;
            }

            return result;
        }


        public override void Simulate(float timeStep)
        {
            step_time += timeStep;
            lock (OdeLock)
            {
                if (_characters.Count > 0 && RENDER_FLAG)
                {
                    Console.WriteLine("RENDER: frame");
                }
                foreach (OdePrim p in _prims)
                {
                    if (_characters.Count > 0 && RENDER_FLAG)
                    {
                        Vector3 rx, ry, rz;
                        p.Orientation.ToAxes(out rx, out ry, out rz);
                        Console.WriteLine("RENDER: block; " + p.Size.X + ", " + p.Size.Y + ", " + p.Size.Z + "; " +
                                          "  0, 0, 1;  " + //shape, size, color
                                          (p.Position.X - 128.0f) + ", " + (p.Position.Y - 128.0f) + ", " +
                                          (p.Position.Z - 33.0f) + ";  " + // position
                                          rx.x + "," + ry.x + "," + rz.x + ", " + // rotation
                                          rx.y + "," + ry.y + "," + rz.y + ", " +
                                          rx.z + "," + ry.z + "," + rz.z);
                    }
                }
                int i = 0;
                while (step_time > 0.0f)
                {
                    foreach (OdeCharacter actor in _characters)
                    {
                            actor.Move(timeStep);
                    }

                    collision_optimized();
                    d.WorldQuickStep(world, ODE_STEPSIZE);
                    d.JointGroupEmpty(contactgroup);
                    step_time -= ODE_STEPSIZE;
                    i++;
                }

                foreach (OdeCharacter actor in _characters)
                {
                    actor.UpdatePositionAndVelocity();
                    if (RENDER_FLAG)
                    {
                        /// debugging code
                        float Zoff = -33.0f;
                        d.Matrix3 temp = d.BodyGetRotation(actor.Body);
                        Console.WriteLine("RENDER: cylinder; " + // shape
                                          OdeCharacter.CAPSULE_RADIUS + ", " + OdeCharacter.CAPSULE_LENGTH + //size
                                          "; 0, 1, 0;  " + // color
                                          (actor.Position.X - 128.0f) + ", " + (actor.Position.Y - 128.0f) + ", " +
                                          (actor.Position.Z + Zoff) + ";  " + // position
                                          temp.M00 + "," + temp.M10 + "," + temp.M20 + ", " + // rotation
                                          temp.M01 + "," + temp.M11 + "," + temp.M21 + ", " +
                                          temp.M02 + "," + temp.M12 + "," + temp.M22);
                        d.Vector3 caphead;
                        d.BodyGetRelPointPos(actor.Body, 0, 0, OdeCharacter.CAPSULE_LENGTH*.5f, out caphead);
                        d.Vector3 capfoot;
                        d.BodyGetRelPointPos(actor.Body, 0, 0, -OdeCharacter.CAPSULE_LENGTH*.5f, out capfoot);
                        Console.WriteLine("RENDER: sphere;  " + OdeCharacter.CAPSULE_RADIUS + // shape, size
                                          "; 1, 0, 1;  " + //color
                                          (caphead.X - 128.0f) + ", " + (caphead.Y - 128.0f) + ", " + (caphead.Z + Zoff) +
                                          ";  " + // position
                                          "1,0,0, 0,1,0, 0,0,1"); // rotation
                        Console.WriteLine("RENDER: sphere;  " + OdeCharacter.CAPSULE_RADIUS + // shape, size
                                          "; 1, 0, 0;  " + //color
                                          (capfoot.X - 128.0f) + ", " + (capfoot.Y - 128.0f) + ", " + (capfoot.Z + Zoff) +
                                          ";  " + // position
                                          "1,0,0, 0,1,0, 0,0,1"); // rotation
                    }
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
            // this._heightmap[i] = (double)heightMap[i];
            // dbm (danx0r) -- heightmap x,y must be swapped for Ode (should fix ODE, but for now...)
            // also, creating a buffer zone of one extra sample all around
            for (int x = 0; x < 258; x++)
            {
                for (int y = 0; y < 258; y++)
                {
                    int xx = x - 1;
                    if (xx < 0) xx = 0;
                    if (xx > 255) xx = 255;
                    int yy = y - 1;
                    if (yy < 0) yy = 0;
                    if (yy > 255) yy = 255;

                    double val = (double) heightMap[yy*256 + xx];
                    _heightmap[x*258 + y] = val;
                }
            }

            lock (OdeLock)
            {
                if (!(LandGeom == (IntPtr) 0))
                {
                    d.SpaceRemove(space, LandGeom);
                }
                IntPtr HeightmapData = d.GeomHeightfieldDataCreate();
                d.GeomHeightfieldDataBuildDouble(HeightmapData, _heightmap, 0, 258, 258, 258, 258, 1.0f, 0.0f, 2.0f, 0);
                d.GeomHeightfieldDataSetBounds(HeightmapData, 256, 256);
                LandGeom = d.CreateHeightfield(space, HeightmapData, 1);
                geom_name_map[LandGeom] = "Terrain";

                d.Matrix3 R = new d.Matrix3();

                Quaternion q1 = Quaternion.FromAngleAxis(1.5707f, new Vector3(1, 0, 0));
                Quaternion q2 = Quaternion.FromAngleAxis(1.5707f, new Vector3(0, 1, 0));
                //Axiom.Math.Quaternion q3 = Axiom.Math.Quaternion.FromAngleAxis(3.14f, new Axiom.Math.Vector3(0, 0, 1));

                q1 = q1*q2;
                //q1 = q1 * q3;
                Vector3 v3 = new Vector3();
                float angle = 0;
                q1.ToAngleAxis(ref angle, ref v3);

                d.RFromAxisAndAngle(out R, v3.x, v3.y, v3.z, angle);
                d.GeomSetRotation(LandGeom, ref R);
                d.GeomSetPosition(LandGeom, 128, 128, 0);
            }
        }

        public override void DeleteTerrain()
        {
        }
    }

    public class OdeCharacter : PhysicsActor
    {
        private PhysicsVector _position;
        private d.Vector3 _zeroPosition;
        private bool _zeroFlag = false;
        private PhysicsVector _velocity;
        private PhysicsVector _target_velocity;
        private PhysicsVector _acceleration;
        private static float PID_D = 4000.0f;
        private static float PID_P = 7000.0f;
        private static float POSTURE_SERVO = 10000.0f;
        public static float CAPSULE_RADIUS = 0.5f;
        public static float CAPSULE_LENGTH = 0.9f;
        private bool flying = false;
        private bool iscolliding = false;
        private bool jumping = false;
        //private float gravityAccel;
        public IntPtr Body;
        private OdeScene _parent_scene;
        public IntPtr Shell;
        public d.Mass ShellMass;

        public OdeCharacter(String avName, OdeScene parent_scene, PhysicsVector pos)
        {
            _velocity = new PhysicsVector();
            _target_velocity = new PhysicsVector();
            _position = pos;
            _acceleration = new PhysicsVector();
            _parent_scene = parent_scene;
            lock (OdeScene.OdeLock)
            {
                Shell = d.CreateCapsule(parent_scene.space, CAPSULE_RADIUS, CAPSULE_LENGTH);
                d.MassSetCapsule(out ShellMass, 50.0f, 3, 0.4f, 1.0f);
                Body = d.BodyCreate(parent_scene.world);
                d.BodySetMass(Body, ref ShellMass);
                d.BodySetPosition(Body, pos.X, pos.Y, pos.Z);
                d.GeomSetBody(Shell, Body);
            }
            parent_scene.geom_name_map[Shell] = avName;
            parent_scene.actor_name_map[Shell] = (PhysicsActor)this;
        }

        public override bool IsPhysical
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
            set
            {iscolliding = value;}
        }
        public override PhysicsVector Position
        {
            get { return _position; }
            set
            {
                lock (OdeScene.OdeLock)
                {
                    d.BodySetPosition(Body, value.X, value.Y, value.Z);
                    _position = value;
                }
            }
        }

        public override PhysicsVector Size
        {
            get { return new PhysicsVector(CAPSULE_RADIUS*2, CAPSULE_RADIUS*2, CAPSULE_LENGTH); }
            set { }
        }


        public override PhysicsVector Velocity
        {
            get { return _velocity; }
            set { _target_velocity = value; }
        }

        public override bool Kinematic
        {
            get { return false; }
            set { }
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

        public void SetAcceleration(PhysicsVector accel)
        {
            _acceleration = accel;
        }

        public override void AddForce(PhysicsVector force)
        {

            _target_velocity.X += force.X;
            _target_velocity.Y += force.Y;
            _target_velocity.Z += force.Z;


        }
        public void doForce(PhysicsVector force)
        {
            d.BodyAddForce(Body, force.X, force.Y, force.Z);

            //  ok -- let's stand up straight!
            d.Vector3 feet;
            d.Vector3 head;
            d.BodyGetRelPointPos(Body, 0.0f, 0.0f, -1.0f, out feet);
            d.BodyGetRelPointPos(Body, 0.0f, 0.0f, 1.0f, out head);
            float posture = head.Z - feet.Z;

            // restoring force proportional to lack of posture:
            float servo = (2.5f - posture) * POSTURE_SERVO;
            d.BodyAddForceAtRelPos(Body, 0.0f, 0.0f, servo, 0.0f, 0.0f, 1.0f);
            d.BodyAddForceAtRelPos(Body, 0.0f, 0.0f, -servo, 0.0f, 0.0f, -1.0f);

        }
        public override void SetMomentum(PhysicsVector momentum)
        {

        }
        
        public void Move(float timeStep)
        {
            //  no lock; for now it's only called from within Simulate()
            PhysicsVector vec = new PhysicsVector();
            d.Vector3 vel = d.BodyGetLinearVel(Body);

            //  if velocity is zero, use position control; otherwise, velocity control
            if (_target_velocity.X == 0.0f && _target_velocity.Y == 0.0f && _target_velocity.Z == 0.0f && iscolliding)
            {
                //  keep track of where we stopped.  No more slippin' & slidin'
                if (!_zeroFlag)
                {
                    _zeroFlag = true;
                    _zeroPosition = d.BodyGetPosition(Body);
                }
                d.Vector3 pos = d.BodyGetPosition(Body);
                vec.X = (_target_velocity.X - vel.X)*PID_D + (_zeroPosition.X - pos.X)*PID_P;
                vec.Y = (_target_velocity.Y - vel.Y)*PID_D + (_zeroPosition.Y - pos.Y)*PID_P;
                if (flying)
                {
                    vec.Z = (_target_velocity.Z - vel.Z)*PID_D + (_zeroPosition.Z - pos.Z)*PID_P;
                }
            }
            else
            {
                
                _zeroFlag = false;
                if (iscolliding || flying)
                {
                    vec.X = (_target_velocity.X - vel.X) * PID_D;
                    vec.Y = (_target_velocity.Y - vel.Y) * PID_D;
                }
                if (iscolliding && !flying && _target_velocity.Z > 0.0f)
                {
                    d.Vector3 pos = d.BodyGetPosition(Body);
                    vec.Z = (_target_velocity.Z - vel.Z) * PID_D + (_zeroPosition.Z - pos.Z) * PID_P;
                }

                if (flying)
                {
                    vec.Z = (_target_velocity.Z - vel.Z)*PID_D;
                }
            }
            if (flying)
            {
                vec.Z += 10.0f;
            }
            doForce(vec);
        }

        public void UpdatePositionAndVelocity()
        {
            //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            d.Vector3 vec = d.BodyGetPosition(Body);

            //  kluge to keep things in bounds.  ODE lets dead avatars drift away (they should be removed!)
            if (vec.X < 0.0f) vec.X = 0.0f;
            if (vec.Y < 0.0f) vec.Y = 0.0f;
            if (vec.X > 255.95f) vec.X = 255.95f;
            if (vec.Y > 255.95f) vec.Y = 255.95f;

            _position.X = vec.X;
            _position.Y = vec.Y;
            _position.Z = vec.Z;

            if (_zeroFlag)
            {
                _velocity.X = 0.0f;
                _velocity.Y = 0.0f;
                _velocity.Z = 0.0f;
            }
            else
            {
                vec = d.BodyGetLinearVel(Body);
                _velocity.X = vec.X;
                _velocity.Y = vec.Y;
                _velocity.Z = vec.Z;
            }
        }

        public void Destroy()
        {
            lock (OdeScene.OdeLock)
            {
                d.GeomDestroy(Shell);
                _parent_scene.geom_name_map.Remove(Shell);
                d.BodyDestroy(Body);
            }
        }
    }

    public class OdePrim : PhysicsActor
    {
        public PhysicsVector _position;
        private PhysicsVector _velocity;
        private PhysicsVector _size;
        private PhysicsVector _acceleration;
        public Quaternion _orientation;
        private Mesh _mesh;
        private PrimitiveBaseShape _pbs;
        private OdeScene _parent_scene;
        public IntPtr prim_geom;
        public IntPtr _triMeshData;
        private bool iscolliding = false;

        public OdePrim(String primName, OdeScene parent_scene, PhysicsVector pos, PhysicsVector size,
                       Quaternion rotation, Mesh mesh, PrimitiveBaseShape pbs)
        {
            _velocity = new PhysicsVector();
            _position = pos;
            _size = size;
            _acceleration = new PhysicsVector();
            _orientation = rotation;
            _mesh = mesh;
            _pbs = pbs;
            _parent_scene = parent_scene;
            

            lock (OdeScene.OdeLock)
            {
                if (mesh != null)
                {
                    setMesh(parent_scene, mesh);
                }
                else
                {
                    prim_geom = d.CreateBox(parent_scene.space, _size.X, _size.Y, _size.Z);
                }

                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.W = rotation.w;
                myrot.X = rotation.x;
                myrot.Y = rotation.y;
                myrot.Z = rotation.z;
                d.GeomSetQuaternion(prim_geom, ref myrot);
                parent_scene.geom_name_map[prim_geom] = primName;
                parent_scene.actor_name_map[prim_geom] = (PhysicsActor) this;
                    //  don't do .add() here; old geoms get recycled with the same hash
            }
        }

        public void setMesh(OdeScene parent_scene, Mesh mesh)
        {
            float[] vertexList = mesh.getVertexListAsFloat(); // Note, that vertextList is pinned in memory
            int[] indexList = mesh.getIndexListAsInt(); // Also pinned, needs release after usage
            int VertexCount = vertexList.GetLength(0)/3;
            int IndexCount = indexList.GetLength(0);

            _triMeshData = d.GeomTriMeshDataCreate();

            d.GeomTriMeshDataBuildSimple(_triMeshData, vertexList, 3*sizeof (float), VertexCount, indexList, IndexCount,
                                         3*sizeof (int));
            d.GeomTriMeshDataPreprocess(_triMeshData);

            prim_geom = d.CreateTriMesh(parent_scene.space, _triMeshData, parent_scene.triCallback, null, null);
        }

        public override bool IsPhysical
        {
            get { return false; }
            set { return; }
        }

        public override bool Flying
        {
            get { return false; //no flying prims for you
            }
            set { }
        }

        public override bool IsColliding
        {
            get { return iscolliding; }
            set { iscolliding = value; }
        }


        public override PhysicsVector Position
        {
            get { return _position; }
            set
            {
                _position = value;
                lock (OdeScene.OdeLock)
                {
                    d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                }
            }
        }

        public override PhysicsVector Size
        {
            get { return _size; }
            set
            {
                _size = value;
                lock (OdeScene.OdeLock)
                {
                    if (_mesh != null) // We deal with a mesh here
                    {
                        string oldname = _parent_scene.geom_name_map[prim_geom];
                        d.GeomDestroy(prim_geom);
                        Mesh mesh = Meshmerizer.CreateMesh(_pbs, _size);
                        setMesh(_parent_scene, mesh);
                        _parent_scene.geom_name_map[prim_geom] = oldname;
                    }
                    else
                    {
                        d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
                    }
                }
            }
        }

        public override PhysicsVector Velocity
        {
            get { return _velocity; }
            set { _velocity = value; }
        }

        public override bool Kinematic
        {
            get { return false; }
            set { }
        }

        public override Quaternion Orientation
        {
            get { return _orientation; }
            set
            {
                _orientation = value;
                lock (OdeScene.OdeLock)
                {
                    d.Quaternion myrot = new d.Quaternion();
                    myrot.W = _orientation.w;
                    myrot.X = _orientation.x;
                    myrot.Y = _orientation.y;
                    myrot.Z = _orientation.z;
                    d.GeomSetQuaternion(prim_geom, ref myrot);
                }
            }
        }

        public override PhysicsVector Acceleration
        {
            get { return _acceleration; }
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
