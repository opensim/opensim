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
//using OpenSim.Region.Physics.OdePlugin.Meshing;

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
        private List<OdePrim> _activeprims = new List<OdePrim>();
        public Dictionary<IntPtr, String> geom_name_map = new Dictionary<IntPtr, String>();
        public Dictionary<IntPtr, PhysicsActor> actor_name_map = new Dictionary<IntPtr, PhysicsActor>();
        private d.ContactGeom[] contacts = new d.ContactGeom[30];
        private d.Contact contact;
        private d.Contact TerrainContact;
        private d.Contact AvatarMovementprimContact;
        private d.Contact AvatarMovementTerrainContact;
        
        private int m_physicsiterations = 10;
        private float m_SkipFramesAtms = 0.40f; // Drop frames gracefully at a 400 ms lag
        private PhysicsActor PANull = new NullPhysicsActor();
        private float step_time = 0.0f;
        public IntPtr world;
        
        public IntPtr space;
        // split static geometry collision handling into spaces of 64 meters
        public IntPtr[] staticPrimspace = new IntPtr[74]; 
        
        public static Object OdeLock = new Object();

        public IMesher mesher;

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

            TerrainContact.surface.mode |= d.ContactFlags.SoftERP;
            TerrainContact.surface.mu = 250.0f;
            TerrainContact.surface.bounce = 0.1f;
            TerrainContact.surface.soft_erp = 0.1025f;

            AvatarMovementprimContact.surface.mu = 150.0f;
            AvatarMovementprimContact.surface.bounce = 0.2f;

            AvatarMovementTerrainContact.surface.mode |= d.ContactFlags.SoftERP;
            AvatarMovementTerrainContact.surface.mu = 150.0f;
            AvatarMovementTerrainContact.surface.bounce = 0.1f;
            AvatarMovementTerrainContact.surface.soft_erp = 0.1025f;

            lock (OdeLock)
            {
                world = d.WorldCreate();
                space = d.HashSpaceCreate(IntPtr.Zero);
                d.HashSpaceSetLevels(space, -4, 128);
                contactgroup = d.JointGroupCreate(0);
                //contactgroup

                            
                d.WorldSetGravity(world, 0.0f, 0.0f, -10.0f);
                d.WorldSetAutoDisableFlag(world, false);
                d.WorldSetContactSurfaceLayer(world, 0.001f);
                d.WorldSetQuickStepNumIterations(world, m_physicsiterations);
                d.WorldSetContactMaxCorrectingVel(world, 1000.0f);
            }

            _heightmap = new double[258*258];

            for (int i = 0; i < staticPrimspace.Length; i++)
            {
                staticPrimspace[i] = IntPtr.Zero;
            }
            
        }

        

        public override void Initialise(IMesher meshmerizer)
        {
            mesher = meshmerizer;

        }



        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked
            if (d.GeomIsSpace(g1) || d.GeomIsSpace(g2) )
            {
                // Separating static prim geometry spaces.   
                // We'll be calling near recursivly if one 
                // of them is a space to find all of the 
                // contact points in the space

                d.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
                //Colliding a space or a geom with a space or a geom.

                //Collide all geoms in each space..   
                //if (d.GeomIsSpace(g1)) d.SpaceCollide(g1, IntPtr.Zero, nearCallback);
                //if (d.GeomIsSpace(g2)) d.SpaceCollide(g2, IntPtr.Zero, nearCallback);

            } 
            else 
            {
                // Colliding Geom To Geom
                // This portion of the function 'was' blatantly ripped off from BoxStack.cs
                
                IntPtr b1 = d.GeomGetBody(g1);
                IntPtr b2 = d.GeomGetBody(g2);


                if (g1 == g2)
                    return; // Can't collide with yourself
      


                if (b1 != IntPtr.Zero && b2 != IntPtr.Zero && d.AreConnectedExcluding(b1, b2, d.JointType.Contact))
                    return;


                d.GeomClassID id = d.GeomGetClass(g1);
                
                String name1 = null;
                String name2 = null;

                if (!geom_name_map.TryGetValue(g1, out name1))
                {
                    name1 = "null";
                }
                if (!geom_name_map.TryGetValue(g2, out name2))
                {
                    name2 = "null";
                }

                if (id == d.GeomClassID.TriMeshClass)
                {
                    

    //               MainLog.Instance.Verbose("near: A collision was detected between {1} and {2}", 0, name1, name2);
                    //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
                }
                
                int count;
                
                    count = d.Collide(g1, g2, contacts.GetLength(0), contacts, d.ContactGeom.SizeOf);
             
                for (int i = 0; i < count; i++)
                {
                    IntPtr joint;
                    // If we're colliding with terrain, use 'TerrainContact' instead of contact.
                    // allows us to have different settings
                    PhysicsActor p1;
                    PhysicsActor p2;

                    if (!actor_name_map.TryGetValue(g2, out p1))
                    {
                        p1 = PANull;
                    }
                    if (!actor_name_map.TryGetValue(g2, out p2))
                    {
                        p2 = PANull;
                    }

                    // We only need to test p2 for 'jump crouch purposes'
                    p2.IsColliding = true;

                    switch(p1.PhysicsActorType) {
                        case (int)ActorTypes.Agent:
                            p2.CollidingObj = true;
                            break;
                        case (int)ActorTypes.Prim:
                            p2.CollidingObj = true;
                            break;
                        case (int)ActorTypes.Unknown:
                            p2.CollidingGround = true;
                            break;
                    }

                    if (name1 == "Terrain" || name2 == "Terrain")
                    {
                        if ((p2.PhysicsActorType == (int)ActorTypes.Agent) && (Math.Abs(p2.Velocity.X) > 0.01f || Math.Abs(p2.Velocity.Y) > 0.01f))
                        {
                            AvatarMovementTerrainContact.geom = contacts[i];
                            joint = d.JointCreateContact(world, contactgroup, ref AvatarMovementTerrainContact);
                        }
                        else
                        {
                            TerrainContact.geom = contacts[i];
                            joint = d.JointCreateContact(world, contactgroup, ref TerrainContact);
                        }
                            
                        
                    }
                    else
                    {
                        if ((p2.PhysicsActorType == (int)ActorTypes.Agent) && (Math.Abs(p2.Velocity.X) > 0.01f || Math.Abs(p2.Velocity.Y) > 0.01f))
                        {
                            AvatarMovementprimContact.geom = contacts[i];
                            joint = d.JointCreateContact(world, contactgroup, ref AvatarMovementprimContact);
                        }
                        else
                        {
                            contact.geom = contacts[i];
                            joint = d.JointCreateContact(world, contactgroup, ref contact);
                            
                        }
                        
                    }
                    
                    
                    d.JointAttach(joint, b1, b2);


                    
                    if (count > 3)
                    {
                        p2.ThrottleUpdates = true;
                    }
                    //System.Console.WriteLine(count.ToString());
                    //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
                }
            }
        }

        private void collision_optimized(float timeStep)
        {

            foreach (OdeCharacter chr in _characters)
            {

                
                chr.IsColliding = false;
                chr.CollidingGround = false;
                chr.CollidingObj = false;
                d.SpaceCollide2(space, chr.Shell, IntPtr.Zero, nearCallback);
                foreach (OdeCharacter ch2 in _characters)
                /// should be a separate space -- lots of avatars will be N**2 slow
                {

                    
                    //d.SpaceCollide2(chr.Shell, ch2.Shell, IntPtr.Zero, nearCallback);
                }
               
            }
            // If the sim is running slow this frame, 
            // don't process collision for prim!
            if (timeStep < (m_SkipFramesAtms / 2))
            {
                foreach (OdePrim chr in _activeprims)
                {
                    // This if may not need to be there..    it might be skipped anyway.
                    if (d.BodyIsEnabled(chr.Body))
                    {
                        d.SpaceCollide2(space, chr.prim_geom, IntPtr.Zero, nearCallback);
                        foreach (OdePrim ch2 in _prims)
                        /// should be a separate space -- lots of avatars will be N**2 slow
                        {
                            if (ch2.IsPhysical && d.BodyIsEnabled(ch2.Body))
                            {
                                // Only test prim that are 0.03 meters away in one direction.
                                // This should be Optimized!

                                if ((Math.Abs(ch2.Position.X - chr.Position.X) < 0.03) || (Math.Abs(ch2.Position.Y - chr.Position.Y) < 0.03) || (Math.Abs(ch2.Position.X - chr.Position.X) < 0.03))
                                {
                                    d.SpaceCollide2(chr.prim_geom, ch2.prim_geom, IntPtr.Zero, nearCallback);
                                }
                            }
                        }
                    }
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
                    if (prim.IsPhysical)
                    {
                        OdePrim p;
                        p = (OdePrim) prim;
                        p.disableBody();
                    }
                    // we don't want to remove the main space
                    if (((OdePrim)prim).m_targetSpace != space && ((OdePrim)prim).IsPhysical == false)
                    {
                        // If the geometry is in the targetspace, remove it from the target space
                        if (d.SpaceQuery(((OdePrim)prim).m_targetSpace, ((OdePrim)prim).prim_geom))
                        {
                            d.SpaceRemove(((OdePrim)prim).m_targetSpace, ((OdePrim)prim).prim_geom);
                        }

                      

                        //If there are no more geometries in the sub-space, we don't need it in the main space anymore
                        if (d.SpaceGetNumGeoms(((OdePrim)prim).m_targetSpace) == 0)
                        {
                            d.SpaceRemove(space, ((OdePrim)prim).m_targetSpace);
                            // free up memory used by the space.
                            d.SpaceDestroy(((OdePrim)prim).m_targetSpace);
                            resetSpaceArrayItemToZero(calculateSpaceArrayItemFromPos(((OdePrim)prim).Position));
                        }
                    }
                  
                        d.GeomDestroy(((OdePrim)prim).prim_geom);
                    
                    _prims.Remove((OdePrim)prim);
                    
                }
            }
        }
        public void resetSpaceArrayItemToZero(IntPtr space)
        {
            for (int i = 0; i < staticPrimspace.Length; i++)
            {
                if (staticPrimspace[i] == space)
                    staticPrimspace[i] = IntPtr.Zero;
            }
        }
        public void resetSpaceArrayItemToZero(int arrayitem)
        {
            staticPrimspace[arrayitem] = IntPtr.Zero;
        }

        public IntPtr recalculateSpaceForGeom(IntPtr geom, PhysicsVector pos, IntPtr currentspace)
        {
            //Todo recalculate space the prim is in.
            // Called from setting the Position and Size of an ODEPrim so 
            // it's already in locked space.

            // we don't want to remove the main space
            // we don't need to test physical here because this function should 
            // never be called if the prim is physical(active)
            if (currentspace != space)
            {
                if (d.SpaceQuery(currentspace, geom))
                {
                    d.SpaceRemove(currentspace, geom);
                }

                //If there are no more geometries in the sub-space, we don't need it in the main space anymore
                if (d.SpaceGetNumGeoms(currentspace) == 0)
                {
                    d.SpaceRemove(space, currentspace);
                    // free up memory used by the space.
                    d.SpaceDestroy(currentspace);
                    resetSpaceArrayItemToZero(currentspace);
                }
            }

                
            // The routines in the Position and Size sections do the 'inserting' into the space, 
            // so all we have to do is make sure that the space that we're putting the prim into 
            // is in the 'main' space.
            int iprimspaceArrItem = calculateSpaceArrayItemFromPos(pos);
            IntPtr newspace = calculateSpaceForGeom(pos);

            if (newspace == IntPtr.Zero)
            {
                newspace = createprimspace(iprimspaceArrItem);
                d.HashSpaceSetLevels(newspace, -4, 66);
            }
                    
            return newspace;
        }

        public IntPtr createprimspace(int iprimspaceArrItem) {
            // creating a new space for prim and inserting it into main space.
            staticPrimspace[iprimspaceArrItem] = d.HashSpaceCreate(IntPtr.Zero);
            d.SpaceAdd(space, staticPrimspace[iprimspaceArrItem]);
            return staticPrimspace[iprimspaceArrItem];
        }

        public IntPtr calculateSpaceForGeom(PhysicsVector pos)
        {
            IntPtr locationbasedspace = staticPrimspace[calculateSpaceArrayItemFromPos(pos)];
            //locationbasedspace = space;
            return locationbasedspace;
        }
        public int calculateSpaceArrayItemFromPos(PhysicsVector pos)
        {
            int returnint = ((int)((pos.X + pos.Y)/8.6f));
            return returnint;
        }

        private PhysicsActor AddPrim(String name, PhysicsVector position, PhysicsVector size, Quaternion rotation,
                                     IMesh mesh, PrimitiveBaseShape pbs, bool isphysical)
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

            
            int iprimspaceArrItem = calculateSpaceArrayItemFromPos(pos);
            IntPtr targetspace = calculateSpaceForGeom(pos);

            if (targetspace == IntPtr.Zero)
                targetspace = createprimspace(iprimspaceArrItem);

            OdePrim newPrim;
            lock (OdeLock)
            {
                newPrim = new OdePrim(name, this, targetspace, pos, siz, rot, mesh, pbs, isphysical);
            }
            _prims.Add(newPrim);
            
            return newPrim;
        }

        public void addActivePrim(OdePrim activatePrim)
         {
            // adds active prim..   (ones that should be iterated over in collisions_optimized

                 _activeprims.Add(activatePrim);

        }
        public void remActivePrim(OdePrim deactivatePrim)
        {

                  _activeprims.Remove(deactivatePrim);
               

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

        
        public bool needsMeshing(PrimitiveBaseShape pbs)
        {
            if (pbs.ProfileHollow != 0)
                return true;

            if ((pbs.ProfileBegin != 0) || pbs.ProfileEnd != 0)
                return true;

            return false;
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
            IMesh mesh = null;

            switch (pbs.ProfileShape)
            {
                case ProfileShape.Square:
                    /// support simple box & hollow box now; later, more shapes
                    if (needsMeshing(pbs))
                    {
                         mesh = mesher.CreateMesh(primName, pbs, size);
                    }
                   
                    break;
            }
           
            result = AddPrim(primName, position, size, rotation, mesh, pbs, isPhysical);


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
                
                

                // If We're loaded down by something else, 
                // or debugging with the Visual Studio project on pause
                // skip a few frames to catch up gracefully.
                // without shooting the physicsactors all over the place
                


                if (step_time >= m_SkipFramesAtms)
                {
                    // Instead of trying to catch up, it'll do one physics frame only
                    step_time = ODE_STEPSIZE;
                    this.m_physicsiterations = 5;
                }
                else
                {
                    m_physicsiterations = 10;
                }
                
                // Process 10 frames if the sim is running normal..  
                // process 5 frames if the sim is running slow
                d.WorldSetQuickStepNumIterations(world, m_physicsiterations);


                int i = 0;
                while (step_time > 0.0f)
                {
                    foreach (OdeCharacter actor in _characters)
                    {
                            actor.Move(timeStep);
                            actor.collidelock = true;
                    }

                    
                    collision_optimized(timeStep);
                    d.WorldQuickStep(world, ODE_STEPSIZE);
                    d.JointGroupEmpty(contactgroup);
                    foreach (OdeCharacter actor in _characters)
                    {
                        actor.collidelock = false;
                    }
                    
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
                        //Console.WriteLine("RENDER: cylinder; " + // shape
                                          //OdeCharacter.CAPSULE_RADIUS + ", " + OdeCharacter.CAPSULE_LENGTH + //size
                                          //"; 0, 1, 0;  " + // color
                                          //(actor.Position.X - 128.0f) + ", " + (actor.Position.Y - 128.0f) + ", " +
                                          //(actor.Position.Z + Zoff) + ";  " + // position
                                          //temp.M00 + "," + temp.M10 + "," + temp.M20 + ", " + // rotation
                                          //temp.M01 + "," + temp.M11 + "," + temp.M21 + ", " +
                                          //temp.M02 + "," + temp.M12 + "," + temp.M22);
                        d.Vector3 caphead;
                        //d.BodyGetRelPointPos(actor.Body, 0, 0, OdeCharacter.CAPSULE_LENGTH*.5f, out caphead);
                        d.Vector3 capfoot;
                        //d.BodyGetRelPointPos(actor.Body, 0, 0, -OdeCharacter.CAPSULE_LENGTH*.5f, out capfoot);
                        //Console.WriteLine("RENDER: sphere;  " + OdeCharacter.CAPSULE_RADIUS + // shape, size
                                          //"; 1, 0, 1;  " + //color
                                          //(caphead.X - 128.0f) + ", " + (caphead.Y - 128.0f) + ", " + (caphead.Z + Zoff) +
                                          //";  " + // position
                                          ///"1,0,0, 0,1,0, 0,0,1"); // rotation
                       // Console.WriteLine("RENDER: sphere;  " + OdeCharacter.CAPSULE_RADIUS + // shape, size
                                          //"; 1, 0, 0;  " + //color
                                          //(capfoot.X - 128.0f) + ", " + (capfoot.Y - 128.0f) + ", " + (capfoot.Z + Zoff) +
                                          //";  " + // position
                                          //"1,0,0, 0,1,0, 0,0,1"); // rotation
                    }
                }
                if (timeStep < 0.2f)
                {
                    OdePrim outofBoundsPrim = null;
                    foreach (OdePrim actor in _activeprims)
                    {
                        if (actor.IsPhysical && (d.BodyIsEnabled(actor.Body) || !actor._zeroFlag))
                        {
                            actor.UpdatePositionAndVelocity();
                            
                        }
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
    # region ODE Actors

    public class OdeCharacter : PhysicsActor
    {
        private PhysicsVector _position;
        private d.Vector3 _zeroPosition;
        private bool _zeroFlag = false;
        private bool m_lastUpdateSent = false;
        private PhysicsVector _velocity;
        private PhysicsVector _target_velocity;
        private PhysicsVector _acceleration;
        private PhysicsVector m_rotationalVelocity;
        private static float PID_D = 3020.0f;
        private static float PID_P = 7000.0f;
        private static float POSTURE_SERVO = 10000.0f;
        public static float CAPSULE_RADIUS = 0.5f;
        public float CAPSULE_LENGTH = 0.79f;
        private bool flying = false;
        private bool m_iscolliding = false;
        private bool m_iscollidingGround = false;
        private bool m_wascolliding = false;
        private bool m_wascollidingGround = false;
        private bool m_alwaysRun = false;
        private bool m_hackSentFall = false;
        private bool m_hackSentFly = false;
        private string m_name = "";
        
        private bool[] m_colliderarr = new bool[11];
        private bool[] m_colliderGroundarr = new bool[11];


        private bool jumping = false;
        //private float gravityAccel;
        public IntPtr Body;
        private OdeScene _parent_scene;
        public IntPtr Shell;
        public d.Mass ShellMass;
        public bool collidelock = false;

        public OdeCharacter(String avName, OdeScene parent_scene, PhysicsVector pos)
        {
            _velocity = new PhysicsVector();
            _target_velocity = new PhysicsVector();
            _position = pos;
            _acceleration = new PhysicsVector();
            _parent_scene = parent_scene;
            
            for (int i = 0; i < 11; i++)
            {
                m_colliderarr[i] = false;
            }

            lock (OdeScene.OdeLock)
            {

                Shell = d.CreateCapsule(parent_scene.space, CAPSULE_RADIUS, CAPSULE_LENGTH);
                d.MassSetCapsule(out ShellMass, 50.0f, 3, 0.4f, 1.0f);
                Body = d.BodyCreate(parent_scene.world);
                d.BodySetMass(Body, ref ShellMass);
                d.BodySetPosition(Body, pos.X, pos.Y, pos.Z);
                d.GeomSetBody(Shell, Body);
            }
            m_name = avName;
            parent_scene.geom_name_map[Shell] = avName;
            parent_scene.actor_name_map[Shell] = (PhysicsActor)this;
        }
        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Agent; }
            set { return; }
        }
        public override bool SetAlwaysRun
        {
            get { return m_alwaysRun; }
            set { m_alwaysRun = value;}
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
            
            get { return m_iscolliding; }
            set
            {
                int i;
                int truecount=0;
                int falsecount=0;

                if (m_colliderarr.Length >= 10)
                {
                    for (i = 0; i < 10; i++)
                    {
                        m_colliderarr[i] = m_colliderarr[i + 1];
                    }
                }
                m_colliderarr[10] = value;

                for (i = 0; i < 11; i++)
                {
                    if (m_colliderarr[i])
                    {
                        truecount++;
                    }
                    else
                    {
                        falsecount++;
                    }
                }
                
                // Equal truecounts and false counts means we're colliding with something.

                if (falsecount > 1.2 * truecount)
                {
                    m_iscolliding = false;
                }
                else
                {
                    m_iscolliding = true;
                }
                if (m_wascolliding != m_iscolliding)
                {
                    base.SendCollisionUpdate(new CollisionEventUpdate());
                }
                m_wascolliding = m_iscolliding;
            }
        }
        public override bool CollidingGround
        {
            get { return m_iscollidingGround; }
            set
            {
                int i;
                int truecount = 0;
                int falsecount = 0;

                if (m_colliderGroundarr.Length >= 10)
                {
                    for (i = 0; i < 10; i++)
                    {
                        m_colliderGroundarr[i] = m_colliderGroundarr[i + 1];
                    }
                }
                m_colliderGroundarr[10] = value;

                for (i = 0; i < 11; i++)
                {
                    if (m_colliderGroundarr[i])
                    {
                        truecount++;
                    }
                    else
                    {
                        falsecount++;
                    }
                }

                // Equal truecounts and false counts means we're colliding with something.

                if (falsecount > 1.2 * truecount)
                {
                    m_iscollidingGround = false;
                }
                else
                {
                    m_iscollidingGround = true;
                }
                if (m_wascollidingGround != m_iscollidingGround)
                {
                    //base.SendCollisionUpdate(new CollisionEventUpdate());
                }
                m_wascollidingGround = m_iscollidingGround;
            }
        }
        public override bool CollidingObj
        {
            get { return false; }
            set { return; }
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
        public override PhysicsVector RotationalVelocity
        {
            get { return m_rotationalVelocity; }
            set { m_rotationalVelocity = value; }
        }
        public override PhysicsVector Size
        {
            get { return new PhysicsVector(CAPSULE_RADIUS*2, CAPSULE_RADIUS*2, CAPSULE_LENGTH); }
            set {
                lock (OdeScene.OdeLock)
                {
                    PhysicsVector SetSize = value;
                    float prevCapsule = CAPSULE_LENGTH;
                    float capsuleradius = CAPSULE_RADIUS;
                    capsuleradius = 0.2f;

                    CAPSULE_LENGTH = (SetSize.Z - ((SetSize.Z * 0.43f))); // subtract 43% of the size
                    d.BodyDestroy(Body);
                    d.GeomDestroy(Shell);
                    //OpenSim.Framework.Console.MainLog.Instance.Verbose("PHYSICS", "Set Avatar Height To: " + (CAPSULE_RADIUS + CAPSULE_LENGTH));
                    Shell = d.CreateCapsule(_parent_scene.space, capsuleradius, CAPSULE_LENGTH);
                    d.MassSetCapsule(out ShellMass, 50.0f, 3, CAPSULE_RADIUS, CAPSULE_LENGTH);
                    Body = d.BodyCreate(_parent_scene.world);
                    d.BodySetMass(Body, ref ShellMass);
                    d.BodySetPosition(Body, _position.X, _position.Y, _position.Z + Math.Abs(CAPSULE_LENGTH-prevCapsule));
                    d.GeomSetBody(Shell, Body);
                }
                _parent_scene.geom_name_map[Shell] = m_name;
                _parent_scene.actor_name_map[Shell] = (PhysicsActor)this;
            }
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

            //m_lastUpdateSent = false;
        }
        public void doForce(PhysicsVector force)
        {
            if (!collidelock)
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
                //m_lastUpdateSent = false;
               
            }

        }
        public override void SetMomentum(PhysicsVector momentum)
        {

        }
        
        public void Move(float timeStep)
        {
            //  no lock; for now it's only called from within Simulate()
            PhysicsVector vec = new PhysicsVector();
            d.Vector3 vel = d.BodyGetLinearVel(Body);
            float movementdivisor = 1f;

            if (!m_alwaysRun)
            {
                movementdivisor = 1.3f;
            }
            else
            {
                movementdivisor = 0.8f;
                
            }

            //  if velocity is zero, use position control; otherwise, velocity control
            if (_target_velocity.X == 0.0f && _target_velocity.Y == 0.0f && _target_velocity.Z == 0.0f && m_iscolliding)
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
                if (m_iscolliding || flying)
                {

                    vec.X = ((_target_velocity.X/movementdivisor) - vel.X) * PID_D;
                    vec.Y = ((_target_velocity.Y/movementdivisor) - vel.Y) * PID_D;
                }
                if (m_iscolliding && !flying && _target_velocity.Z > 0.0f)
                {
                    d.Vector3 pos = d.BodyGetPosition(Body);
                    vec.Z = (_target_velocity.Z - vel.Z) * PID_D + (_zeroPosition.Z - pos.Z) * PID_P;
                    if (_target_velocity.X > 0)
                    {
                        vec.X = ((_target_velocity.X - vel.X) / 1.2f) * PID_D;
                    }
                    if (_target_velocity.Y > 0)
                    {
                        vec.Y = ((_target_velocity.Y - vel.Y) / 1.2f) * PID_D;
                    }
                }
                else if (!m_iscolliding && !flying)
                {
                    d.Vector3 pos = d.BodyGetPosition(Body);
                    if (_target_velocity.X > 0)
                    {
                        vec.X = ((_target_velocity.X - vel.X)/1.2f) * PID_D;
                    }
                    if (_target_velocity.Y > 0)
                    {
                        vec.Y = ((_target_velocity.Y - vel.Y)/1.2f) * PID_D;
                    }

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
                if (!m_lastUpdateSent)
                {
                    m_lastUpdateSent = true;
                    base.RequestPhysicsterseUpdate();
                    
                }
            }
            else
            {
                m_lastUpdateSent = false;
                vec = d.BodyGetLinearVel(Body);
                _velocity.X = (vec.X);
                _velocity.Y = (vec.Y);
                
                _velocity.Z = (vec.Z);
                if (_velocity.Z < -6 && !m_hackSentFall)
                {
                    m_hackSentFall = true;
                    base.SendCollisionUpdate(new CollisionEventUpdate());
                }
                else if (flying && !m_hackSentFly)
                {
                    //m_hackSentFly = true;
                    //base.SendCollisionUpdate(new CollisionEventUpdate());
                }
                else
                {
                    m_hackSentFly = false;
                    m_hackSentFall = false;
                }
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
        private PhysicsVector m_lastVelocity = new PhysicsVector(0.0f,0.0f,0.0f);
        private PhysicsVector m_lastposition = new PhysicsVector(0.0f, 0.0f, 0.0f);
        private PhysicsVector m_rotationalVelocity;
        private PhysicsVector _size;
        private PhysicsVector _acceleration;
        private Quaternion _orientation;

        private IMesh _mesh;
        private PrimitiveBaseShape _pbs;
        private OdeScene _parent_scene;
        public IntPtr m_targetSpace = (IntPtr)0;
        public IntPtr prim_geom;
        public IntPtr _triMeshData;
        private bool iscolliding = false;
        private bool m_isphysical = false;
        private bool m_throttleUpdates = false;
        private int throttleCounter = 0;
        public bool outofBounds = false;
        
        public bool _zeroFlag = false;
        private bool m_lastUpdateSent = false;

        public IntPtr Body = (IntPtr) 0;
        private String m_primName;
        private PhysicsVector _target_velocity;
        public d.Mass pMass;
        private const float MassMultiplier = 150f; //  Ref: Water: 1000kg..  this iset to 500
        private int debugcounter = 0;


        public OdePrim(String primName, OdeScene parent_scene, IntPtr targetSpace, PhysicsVector pos, PhysicsVector size,
                       Quaternion rotation, IMesh mesh, PrimitiveBaseShape pbs, bool pisPhysical)
        {
            

            _velocity = new PhysicsVector();
            _position = pos;
            _size = size;
            _acceleration = new PhysicsVector();
            m_rotationalVelocity = PhysicsVector.Zero;
            _orientation = rotation;
            _mesh = mesh;
            _pbs = pbs;
            _parent_scene = parent_scene;
            m_targetSpace = targetSpace;

            if (pos.Z < 0)
                m_isphysical = false;
            else
            {
                m_isphysical = pisPhysical;
                // If we're physical, we need to be in the master space for now.
                // linksets *should* be in a space together..  but are not currently
                if (m_isphysical)
                    m_targetSpace = _parent_scene.space;

            }
            m_primName = primName;
            
            

            lock (OdeScene.OdeLock)
            {
                if (mesh != null)
                {
                    setMesh(parent_scene, mesh);
                }
                else
                {
                    prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                }

                d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                d.Quaternion myrot = new d.Quaternion();
                myrot.W = rotation.w;
                myrot.X = rotation.x;
                myrot.Y = rotation.y;
                myrot.Z = rotation.z;
                d.GeomSetQuaternion(prim_geom, ref myrot);
                

                if (m_isphysical && Body == (IntPtr)0) {
                    enableBody();
                }
                parent_scene.geom_name_map[prim_geom] = primName;
                parent_scene.actor_name_map[prim_geom] = (PhysicsActor)this;
                    //  don't do .add() here; old geoms get recycled with the same hash
            }
        }
        public override int PhysicsActorType
        {
            get { return (int)ActorTypes.Prim; }
            set { return; }
        }
        public override bool SetAlwaysRun
        {
            get { return false; }
            set { return; }
        }
        public void enableBody()
        {
            // Sets the geom to a body
            Body = d.BodyCreate(_parent_scene.world);

            setMass();
            d.BodySetPosition(Body, _position.X, _position.Y, _position.Z);
            d.Quaternion myrot = new d.Quaternion();
            myrot.W = _orientation.w;
            myrot.X = _orientation.x;
            myrot.Y = _orientation.y;
            myrot.Z = _orientation.z;
            d.BodySetQuaternion(Body, ref myrot);
            d.GeomSetBody(prim_geom, Body);
            d.BodySetAutoDisableFlag(Body, true);
            d.BodySetAutoDisableSteps(Body,20);
            
            _parent_scene.addActivePrim(this);
        }
        public void setMass()
        {
            //Sets Mass based on member MassMultiplier.   
            if (Body != (IntPtr)0)
            {
                d.MassSetBox(out pMass, (_size.X * _size.Y * _size.Z * MassMultiplier), _size.X, _size.Y, _size.Z);
                d.BodySetMass(Body, ref pMass);
            }
        }
        public void disableBody()
        {
            //this kills the body so things like 'mesh' can re-create it.
            if (Body != (IntPtr)0)
            {
                _parent_scene.remActivePrim(this);
                d.BodyDestroy(Body);
                Body = (IntPtr)0;
            }
        }
        public void setMesh(OdeScene parent_scene, IMesh mesh)
        {
            //Kill Body so that mesh can re-make the geom
            if (IsPhysical && Body != (IntPtr)0)
            {
                disableBody();
            }
            float[] vertexList = mesh.getVertexListAsFloatLocked(); // Note, that vertextList is pinned in memory
            int[] indexList = mesh.getIndexListAsIntLocked(); // Also pinned, needs release after usage
            int VertexCount = vertexList.GetLength(0)/3;
            int IndexCount = indexList.GetLength(0);

            _triMeshData = d.GeomTriMeshDataCreate();

            d.GeomTriMeshDataBuildSimple(_triMeshData, vertexList, 3*sizeof (float), VertexCount, indexList, IndexCount,
                                         3*sizeof (int));
            d.GeomTriMeshDataPreprocess(_triMeshData);

            prim_geom = d.CreateTriMesh(m_targetSpace, _triMeshData, parent_scene.triCallback, null, null);
            
            if (IsPhysical && Body == (IntPtr)0)
            {
                // Recreate the body
                enableBody();
            }
        }

        public override bool IsPhysical
        {
            get { return m_isphysical; }
            set {
                
                lock (OdeScene.OdeLock)
                {
                    if (m_isphysical == value)
                    {
                        // If the object is already what the user checked
                        
                        return;
                    }
                    if (value == true)
                    {
                        if (Body == (IntPtr)0)
                        {
                            enableBody();
                        }

                    }
                    else if (value == false)
                    {
                        if (Body != (IntPtr)0)
                        {
                            disableBody();
                        }
                    }
                    m_isphysical = value;
                }

            }
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
        public override bool ThrottleUpdates
        {
            get { return m_throttleUpdates; }
            set { m_throttleUpdates=value; }
        }

        public override PhysicsVector Position
        {
            get {return _position; }
                
           
            set
            {
                _position = value;
                lock (OdeScene.OdeLock)
                {
                    if (m_isphysical)
                    {
                        // This is a fallback..   May no longer be necessary.
                        if (Body == (IntPtr)0)
                            enableBody();
                         //Prim auto disable after 20 frames, 
                         ///if you move it, re-enable the prim manually.
                        d.BodyEnable(Body);
                        d.BodySetPosition(Body, _position.X, _position.Y, _position.Z); 
                    }
                    else
                    {
                        d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                        m_targetSpace = _parent_scene.recalculateSpaceForGeom(prim_geom, _position, m_targetSpace);
                        d.SpaceAdd(m_targetSpace, prim_geom);
                    }
                   
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
                    string oldname = _parent_scene.geom_name_map[prim_geom];
                    
                    // Cleanup of old prim geometry
                    if (_mesh != null)
                    {
                        // Cleanup meshing here
                    }
                    //kill body to rebuild 
                    if (IsPhysical && Body != (IntPtr)0)
                    {
                        disableBody();
                    }
                    if (d.SpaceQuery(m_targetSpace,prim_geom)) {
                        d.SpaceRemove(m_targetSpace,prim_geom);
                    }
                    d.GeomDestroy(prim_geom);
                      
                      // we don't need to do space calculation because the client sends a position update also.

                      // Construction of new prim
                    if (this._parent_scene.needsMeshing(_pbs))
                    {

                        
                        // Don't need to re-enable body..   it's done in SetMesh
                        IMesh mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size);
                        // createmesh returns null when it's a shape that isn't a cube.
                        if (mesh != null)
                        {
                            setMesh(_parent_scene, mesh);
                        }
                        else
                        {
                            prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                            d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                            d.Quaternion myrot = new d.Quaternion();
                            myrot.W = _orientation.w;
                            myrot.X = _orientation.x;
                            myrot.Y = _orientation.y;
                            myrot.Z = _orientation.z;
                            d.GeomSetQuaternion(prim_geom, ref myrot);


                        }
                    } else {
                        prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                        d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                        d.Quaternion myrot = new d.Quaternion();
                        myrot.W = _orientation.w;
                        myrot.X = _orientation.x;
                        myrot.Y = _orientation.y;
                        myrot.Z = _orientation.z;
                        d.GeomSetQuaternion(prim_geom, ref myrot);
                        
                        
                        //d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
                        if (IsPhysical && Body == (IntPtr)0)
                        {
                            // Re creates body on size.
                            // EnableBody also does setMass()
                            enableBody();
                            d.BodyEnable(Body);
                        } 
                        
                    }

                    
                    _parent_scene.geom_name_map[prim_geom] = oldname;

                }
            }
        }

        public override PrimitiveBaseShape Shape
        {
            set
            {
                _pbs = value;
                lock (OdeScene.OdeLock)
                {
                    string oldname = _parent_scene.geom_name_map[prim_geom];

                    // Cleanup of old prim geometry and Bodies
                    if (IsPhysical && Body != (IntPtr)0)
                    {
                        disableBody();
                    }
                    d.GeomDestroy(prim_geom);
                    if (_mesh != null)
                    {
                        
                        d.GeomBoxSetLengths(prim_geom, _size.X, _size.Y, _size.Z);
                    
                    }

                    // Construction of new prim
                    if (this._parent_scene.needsMeshing(_pbs))
                    {
                        IMesh mesh = _parent_scene.mesher.CreateMesh(oldname, _pbs, _size);
                        if (mesh != null)
                        {
                            setMesh(_parent_scene, mesh);
                        }
                        else
                        {
                            prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                        }
                    } else {
                        prim_geom = d.CreateBox(m_targetSpace, _size.X, _size.Y, _size.Z);
                    }
                    if (IsPhysical && Body == (IntPtr)0)
                    {
                        //re-create new body
                        enableBody();
                    }
                    else
                    {
                        d.GeomSetPosition(prim_geom, _position.X, _position.Y, _position.Z);
                        d.Quaternion myrot = new d.Quaternion();
                        myrot.W = _orientation.w;
                        myrot.X = _orientation.x;
                        myrot.Y = _orientation.y;
                        myrot.Z = _orientation.z;
                        d.GeomSetQuaternion(prim_geom, ref myrot);
                    }
                    _parent_scene.geom_name_map[prim_geom] = oldname;

                    

                }
            }
        }

        public override PhysicsVector Velocity
        {
            get { 
                // Averate previous velocity with the new one so 
                // client object interpolation works a 'little' better
                PhysicsVector returnVelocity = new PhysicsVector();
                returnVelocity.X = (m_lastVelocity.X + _velocity.X) / 2;
                returnVelocity.Y = (m_lastVelocity.Y + _velocity.Y) / 2;
                returnVelocity.Z = (m_lastVelocity.Z + _velocity.Z) / 2;
                return returnVelocity;
            }
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
                    if (m_isphysical && Body != (IntPtr)0)
                    {
                        d.BodySetQuaternion(Body, ref myrot);
                    }
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
        public void Move(float timestep)
        {
            
        }
        public override PhysicsVector RotationalVelocity
        {
            get{ return m_rotationalVelocity;}
            set { m_rotationalVelocity = value; }
        }

        public void UpdatePositionAndVelocity() {
         //  no lock; called from Simulate() -- if you call this from elsewhere, gotta lock or do Monitor.Enter/Exit!
            
            if (Body != (IntPtr)0)
            {
                d.Vector3 vec = d.BodyGetPosition(Body);
                d.Quaternion ori = d.BodyGetQuaternion(Body);
                d.Vector3 vel = d.BodyGetLinearVel(Body);
                d.Vector3 rotvel = d.BodyGetAngularVel(Body);
               
                PhysicsVector l_position = new PhysicsVector();
                //  kluge to keep things in bounds.  ODE lets dead avatars drift away (they should be removed!)
                if (vec.X < 0.0f) vec.X = 0.0f;
                if (vec.Y < 0.0f) vec.Y = 0.0f;
                if (vec.X > 255.95f) vec.X = 255.95f;
                if (vec.Y > 255.95f) vec.Y = 255.95f;
                m_lastposition = _position;

                l_position.X = vec.X;
                l_position.Y = vec.Y;
                l_position.Z = vec.Z;
                if (l_position.Z < 0)
                {
                    // This is so prim that get lost underground don't fall forever and suck up 
                    // 
                    // Sim resources and memory.
                    // Disables the prim's movement physics....  
                    // It's a hack and will generate a console message if it fails.
                    
                       
                    
                    
                    IsPhysical = false;
                    _velocity.X = 0;
                    _velocity.Y = 0;
                    _velocity.Z = 0;
                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;
                    //base.RequestPhysicsterseUpdate();
                    m_throttleUpdates = false;
                    throttleCounter = 0;
                    _zeroFlag = true;
                    outofBounds = true;
                }

                if ((Math.Abs(m_lastposition.X - l_position.X) < 0.02) 
                    && (Math.Abs(m_lastposition.Y - l_position.Y) < 0.02) 
                    && (Math.Abs(m_lastposition.Z - l_position.Z) < 0.02 ))
                {
                    
                    _zeroFlag = true;
                }
                else
                {
                    //System.Console.WriteLine(Math.Abs(m_lastposition.X - l_position.X).ToString());
                    _zeroFlag = false;
                }
                


                if (_zeroFlag)
                {
                    // Supposedly this is supposed to tell SceneObjectGroup that 
                    // no more updates need to be sent..  
                    // but it seems broken.
                    _velocity.X = 0.0f;
                    _velocity.Y = 0.0f;
                    _velocity.Z = 0.0f;
                    //_orientation.w = 0f;
                    //_orientation.x = 0f;
                    //_orientation.y = 0f;
                    //_orientation.z = 0f;
                    m_rotationalVelocity.X = 0;
                    m_rotationalVelocity.Y = 0;
                    m_rotationalVelocity.Z = 0;
                    if (!m_lastUpdateSent)
                    {
                        m_throttleUpdates = false;
                        throttleCounter = 0;
                        base.RequestPhysicsterseUpdate();
                        m_lastUpdateSent = true;
                    }

                }
                else
                {
                    m_lastVelocity = _velocity;

                    _position = l_position;

                    _velocity.X = vel.X;
                    _velocity.Y = vel.Y;
                    _velocity.Z = vel.Z;

                    m_rotationalVelocity.X = rotvel.X;
                    m_rotationalVelocity.Y = rotvel.Y;
                    m_rotationalVelocity.Z = rotvel.Z;
                    //System.Console.WriteLine("ODE: " + m_rotationalVelocity.ToString());
                    _orientation.w = ori.W;
                    _orientation.x = ori.X;
                    _orientation.y = ori.Y;
                    _orientation.z = ori.Z;
                    m_lastUpdateSent = false;
                    if (!m_throttleUpdates || throttleCounter > 15)
                    {
                        base.RequestPhysicsterseUpdate();
                    }
                    else
                    {
                        throttleCounter++;
                    }
                }
                m_lastposition = l_position;
            }
            else
            {
                // Not a body..   so Make sure the client isn't interpolating
                _velocity.X = 0;
                _velocity.Y = 0;
                _velocity.Z = 0;
                m_rotationalVelocity.X = 0;
                m_rotationalVelocity.Y = 0;
                m_rotationalVelocity.Z = 0;
                _zeroFlag = true;
            }

        }
        public override void SetMomentum(PhysicsVector momentum)
        {
        }
    }
}
    #endregion