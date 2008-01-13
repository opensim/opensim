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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
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
using System.Runtime.InteropServices;
using Axiom.Math;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Framework.Console;
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
        // TODO: this should be hard-coded in some common place
        private const uint m_regionWidth = 256;
        private const uint m_regionHeight = 256;

        private static float ODE_STEPSIZE = 0.004f;
        private static bool RENDER_FLAG = false;
        private static float metersInSpace = 29.9f;
        private IntPtr contactgroup;
        private IntPtr LandGeom = (IntPtr) 0;
        private double[] _heightmap;
        private float[] _origheightmap;
        private d.NearCallback nearCallback;
        public d.TriCallback triCallback;
        public d.TriArrayCallback triArrayCallback;
        private List<OdeCharacter> _characters = new List<OdeCharacter>();
        private List<OdePrim> _prims = new List<OdePrim>();
        private List<OdePrim> _activeprims = new List<OdePrim>();
        private List<OdePrim> _taintedPrim = new List<OdePrim>();
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
        // split static geometry collision handling into spaces of 30 meters
        public IntPtr[,] staticPrimspace = new IntPtr[(int) (300/metersInSpace),(int) (300/metersInSpace)];

        public static Object OdeLock = new Object();

        public IMesher mesher;


        /// <summary>
        /// Initiailizes the scene
        /// Sets many properties that ODE requires to be stable
        /// These settings need to be tweaked 'exactly' right or weird stuff happens.
        /// </summary>
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

            // Centeral contact friction and bounce
            contact.surface.mu = 250.0f;
            contact.surface.bounce = 0.2f;

            // Terrain contact friction and Bounce 
            // This is the *non* moving version.   Use this when an avatar 
            // isn't moving to keep it in place better
            TerrainContact.surface.mode |= d.ContactFlags.SoftERP;
            TerrainContact.surface.mu = 550.0f;
            TerrainContact.surface.bounce = 0.1f;
            TerrainContact.surface.soft_erp = 0.1025f;

            // Prim contact friction and bounce
            // THis is the *non* moving version of friction and bounce 
            // Use this when an avatar comes in contact with a prim
            // and is moving
            AvatarMovementprimContact.surface.mu = 150.0f;
            AvatarMovementprimContact.surface.bounce = 0.1f;

            // Terrain contact friction bounce and various error correcting calculations
            // Use this when an avatar is in contact with the terrain and moving.
            AvatarMovementTerrainContact.surface.mode |= d.ContactFlags.SoftERP;
            AvatarMovementTerrainContact.surface.mu = 150.0f;
            AvatarMovementTerrainContact.surface.bounce = 0.1f;
            AvatarMovementTerrainContact.surface.soft_erp = 0.1025f;

            lock (OdeLock)
            {
                // Creat the world and the first space
                world = d.WorldCreate();
                space = d.HashSpaceCreate(IntPtr.Zero);
                d.HashSpaceSetLevels(space, -4, 128);
                contactgroup = d.JointGroupCreate(0);
                //contactgroup


                // Set the gravity,, don't disable things automatically (we set it explicitly on some things)

                d.WorldSetGravity(world, 0.0f, 0.0f, -10.0f);
                d.WorldSetAutoDisableFlag(world, false);
                d.WorldSetContactSurfaceLayer(world, 0.001f);
                
                // Set how many steps we go without running collision testing
                // This is in addition to the step size.
                // Essentially Steps * m_physicsiterations
                d.WorldSetQuickStepNumIterations(world, m_physicsiterations);
                ///d.WorldSetContactMaxCorrectingVel(world, 1000.0f);
            }

            // zero out a heightmap array float array (single dimention [flattened]))
            _heightmap = new double[514*514];


            // Zero out the prim spaces array (we split our space into smaller spaces so 
            // we can hit test less.
            for (int i = 0; i < staticPrimspace.GetLength(0); i++)
            {
                for (int j = 0; j < staticPrimspace.GetLength(1); j++)
                {
                    staticPrimspace[i, j] = IntPtr.Zero;
                }
            }
        }

        // Initialize the mesh plugin
        public override void Initialise(IMesher meshmerizer)
        {
            mesher = meshmerizer;
        }

        /// <summary>
        /// Debug space message for printing the space that a prim/avatar is in.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>Returns which split up space the given position is in.</returns>
        public string whichspaceamIin(PhysicsVector pos)
        {
            return calculateSpaceForGeom(pos).ToString();
        }

        /// <summary>
        /// This is our near callback.  A geometry is near a body
        /// </summary>
        /// <param name="space">The space that contains the geoms.  Remember, spaces are also geoms</param>
        /// <param name="g1">a geometry or space</param>
        /// <param name="g2">another geometry or space</param>
        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //  no lock here!  It's invoked from within Simulate(), which is thread-locked
            
            // Test if we're collidng a geom with a space.  
            // If so we have to drill down into the space recursively

            if (d.GeomIsSpace(g1) || d.GeomIsSpace(g2))
            {
                // Separating static prim geometry spaces.   
                // We'll be calling near recursivly if one 
                // of them is a space to find all of the 
                // contact points in the space

                d.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
                //Colliding a space or a geom with a space or a geom. so drill down

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

                //if (id == d.GeomClassID.TriMeshClass)
                //{
                    //               MainLog.Instance.Verbose("near: A collision was detected between {1} and {2}", 0, name1, name2);
                    //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
                //}

                // Figure out how many contact points we have
                int count = 0;
                try
                {
                    count = d.Collide(g1, g2, contacts.GetLength(0), contacts, d.ContactGeom.SizeOf);
                }
                catch (SEHException)
                {
                    MainLog.Instance.Error("PHYSICS",
                                           "The Operating system shut down ODE because of corrupt memory.  This could be a result of really irregular terrain.  If this repeats continuously, restart using Basic Physics and terrain fill your terrain.  Restarting the sim.");
                    base.TriggerPhysicsBasedRestart();
                }

                PhysicsActor p1;
                PhysicsActor p2;

                for (int i = 0; i < count; i++)
                {
                    IntPtr joint;
                    // If we're colliding with terrain, use 'TerrainContact' instead of contact.
                    // allows us to have different settings
                    

                    if (!actor_name_map.TryGetValue(g1, out p1))
                    {
                        p1 = PANull;
                    }
                    if (!actor_name_map.TryGetValue(g2, out p2))
                    {
                        p2 = PANull;
                    }

                    // We only need to test p2 for 'jump crouch purposes'
                    p2.IsColliding = true;


                    switch (p1.PhysicsActorType)
                    {
                        case (int)ActorTypes.Agent:
                            p2.CollidingObj = true;
                            break;
                        case (int)ActorTypes.Prim:
                            if (p2.Velocity.X > 0 || p2.Velocity.Y > 0 || p2.Velocity.Z > 0)
                                p2.CollidingObj = true;
                            break;
                        case (int)ActorTypes.Unknown:
                            p2.CollidingGround = true;
                            break;
                        default:
                            p2.CollidingGround = true;
                            break;
                    }

                    // we don't want prim or avatar to explode

                    #region InterPenetration Handling - Unintended physics explosions

                    if (contacts[i].depth >= 0.08f)
                    {
                        /*  This is disabled at the moment only because it needs more tweaking
                            It will eventually be uncommented
                         
                        if (contacts[i].depth >= 1.00f)
                        {
                            //MainLog.Instance.Debug("PHYSICS",contacts[i].depth.ToString());
                        }
                         
                        //If you interpenetrate a prim with an agent
                        if ((p2.PhysicsActorType == (int) ActorTypes.Agent &&
                             p1.PhysicsActorType == (int) ActorTypes.Prim) ||
                            (p1.PhysicsActorType == (int) ActorTypes.Agent &&
                             p2.PhysicsActorType == (int) ActorTypes.Prim))
                        {
                            if (p2.PhysicsActorType == (int) ActorTypes.Agent)
                            {   
                                p2.CollidingObj = true;
                                contacts[i].depth = 0.003f;
                                p2.Velocity = p2.Velocity + new PhysicsVector(0, 0, 2.5f);
                                OdeCharacter character = (OdeCharacter) p2;
                                character.SetPidStatus(true);
                                contacts[i].pos = new d.Vector3(contacts[i].pos.X + (p1.Size.X / 2), contacts[i].pos.Y + (p1.Size.Y / 2), contacts[i].pos.Z + (p1.Size.Z / 2));
                                
                            }
                            else
                            {
                                
                                contacts[i].depth = 0.0000000f;
                            }
                            if (p1.PhysicsActorType == (int) ActorTypes.Agent)
                            {
                                
                                p1.CollidingObj = true;
                                contacts[i].depth = 0.003f;
                                p1.Velocity = p1.Velocity + new PhysicsVector(0, 0, 2.5f);
                                contacts[i].pos = new d.Vector3(contacts[i].pos.X + (p2.Size.X / 2), contacts[i].pos.Y + (p2.Size.Y / 2), contacts[i].pos.Z + (p2.Size.Z / 2));
                                OdeCharacter character = (OdeCharacter)p1;
                                character.SetPidStatus(true);
                            }
                            else
                            {
                                
                                contacts[i].depth = 0.0000000f;
                            }
                        }
                         */

                        // If you interpenetrate a prim with another prim
                        if (p1.PhysicsActorType == (int) ActorTypes.Prim && p2.PhysicsActorType == (int) ActorTypes.Prim)
                        {
                            // Don't collide, one or both prim will explode.
                            contacts[i].depth = 0f;
                        }
                        if (contacts[i].depth >= 1.00f)
                        {
                            OpenSim.Framework.Console.MainLog.Instance.Verbose("P", contacts[i].depth.ToString());
                            if ((p2.PhysicsActorType == (int) ActorTypes.Agent &&
                                 p1.PhysicsActorType == (int) ActorTypes.Unknown) ||
                                (p1.PhysicsActorType == (int) ActorTypes.Agent &&
                                 p2.PhysicsActorType == (int) ActorTypes.Unknown))
                            {
                                if (p2.PhysicsActorType == (int) ActorTypes.Agent)
                                {
                                    OdeCharacter character = (OdeCharacter) p2;

                                    //p2.CollidingObj = true;
                                    contacts[i].depth = 0.00000003f;
                                    p2.Velocity = p2.Velocity + new PhysicsVector(0, 0, 0.5f);
                                    contacts[i].pos =
                                        new d.Vector3(contacts[i].pos.X + (p1.Size.X/2),
                                                      contacts[i].pos.Y + (p1.Size.Y/2),
                                                      contacts[i].pos.Z + (p1.Size.Z/2));
                                    character.SetPidStatus(true);
                                }
                                else
                                {
                                }
                                if (p1.PhysicsActorType == (int) ActorTypes.Agent)
                                {
                                    OdeCharacter character = (OdeCharacter)p1;

                                    //p2.CollidingObj = true;
                                    contacts[i].depth = 0.00000003f;
                                    p1.Velocity = p1.Velocity + new PhysicsVector(0, 0, 0.5f);
                                    contacts[i].pos =
                                        new d.Vector3(contacts[i].pos.X + (p1.Size.X/2),
                                                      contacts[i].pos.Y + (p1.Size.Y/2),
                                                      contacts[i].pos.Z + (p1.Size.Z/2));
                                    character.SetPidStatus(true);
                                }
                                else
                                {
                                    //contacts[i].depth = 0.0000000f;
                                }
                            }
                        }
                    }

                    #endregion

                    if (contacts[i].depth >= 0f)
                    {
                        // If we're collidng against terrain
                        if (name1 == "Terrain" || name2 == "Terrain")
                        {
                            // If we're moving
                            if ((p2.PhysicsActorType == (int) ActorTypes.Agent) &&
                                (Math.Abs(p2.Velocity.X) > 0.01f || Math.Abs(p2.Velocity.Y) > 0.01f))
                            {
                                // Use the movement terrain contact
                                AvatarMovementTerrainContact.geom = contacts[i];
                                joint = d.JointCreateContact(world, contactgroup, ref AvatarMovementTerrainContact);
                            }
                            else
                            {
                                // Use the non moving terrain contact
                                TerrainContact.geom = contacts[i];
                                joint = d.JointCreateContact(world, contactgroup, ref TerrainContact);
                            }
                        }
                        else
                        {
                            // we're colliding with prim or avatar

                            // check if we're moving
                            if ((p2.PhysicsActorType == (int) ActorTypes.Agent) &&
                                (Math.Abs(p2.Velocity.X) > 0.01f || Math.Abs(p2.Velocity.Y) > 0.01f))
                            {
                                // Use the Movement prim contact
                                AvatarMovementprimContact.geom = contacts[i];
                                joint = d.JointCreateContact(world, contactgroup, ref AvatarMovementprimContact);
                            }
                            else
                            {
                                // Use the non movement contact
                                contact.geom = contacts[i];
                                joint = d.JointCreateContact(world, contactgroup, ref contact);
                            }
                        }
                        d.JointAttach(joint, b1, b2);
                    }

                    if (count > 3)
                    {
                        // If there are more then 3 contact points, it's likely
                        // that we've got a pile of objects
                        // 
                        // We don't want to send out hundreds of terse updates over and over again
                        // so lets throttle them and send them again after it's somewhat sorted out.
                        p2.ThrottleUpdates = true;
                    }
                    //System.Console.WriteLine(count.ToString());
                    //System.Console.WriteLine("near: A collision was detected between {1} and {2}", 0, name1, name2);
                }
            }
        }
        private float GetTerrainHeightAtXY(float x, float y) 
        {
            return (float)_origheightmap[(int) y*256 + (int) x];
            

        }

        /// <summary>
        /// This is our collision testing routine in ODE
        /// </summary>
        /// <param name="timeStep"></param>
        private void collision_optimized(float timeStep)
        {
            foreach (OdeCharacter chr in _characters)
            {
                // Reset the collision values to false
                // since we don't know if we're colliding yet

                chr.IsColliding = false;
                chr.CollidingGround = false;
                chr.CollidingObj = false;
                
                // test the avatar's geometry for collision with the space
                // This will return near and the space that they are the closest to
                // And we'll run this again against the avatar and the space segment
                // This will return with a bunch of possible objects in the space segment
                // and we'll run it again on all of them.

                d.SpaceCollide2(space, chr.Shell, IntPtr.Zero, nearCallback);
                //float terrainheight = GetTerrainHeightAtXY(chr.Position.X, chr.Position.Y);
                //if (chr.Position.Z + (chr.Velocity.Z * timeStep) < terrainheight + 10)
                //{
                    //chr.Position.Z = terrainheight + 10.0f;
                    //forcedZ = true;
                //}
            }

            // If the sim is running slow this frame, 
            // don't process collision for prim!
            if (timeStep < (m_SkipFramesAtms/3))
            {
                foreach (OdePrim chr in _activeprims)
                {
                    // This if may not need to be there..    it might be skipped anyway.
                    if (d.BodyIsEnabled(chr.Body))
                    {
                        d.SpaceCollide2(space, chr.prim_geom, IntPtr.Zero, nearCallback);
                        //foreach (OdePrim ch2 in _prims)
                        /// should be a separate space -- lots of avatars will be N**2 slow
                        //{
                        //if (ch2.IsPhysical && d.BodyIsEnabled(ch2.Body))
                        //{
                        // Only test prim that are 0.03 meters away in one direction.
                        // This should be Optimized!

                        //if ((Math.Abs(ch2.Position.X - chr.Position.X) < 0.03) || (Math.Abs(ch2.Position.Y - chr.Position.Y) < 0.03) || (Math.Abs(ch2.Position.X - chr.Position.X) < 0.03))
                        //{
                        //d.SpaceCollide2(chr.prim_geom, ch2.prim_geom, IntPtr.Zero, nearCallback);
                        //}
                        //}
                        //}
                    }
                }
            }
            else
            {
                // Everything is going slow, so we're skipping object to object collisions
                // At least collide test against the ground.
                foreach (OdePrim chr in _activeprims)
                {
                    // This if may not need to be there..    it might be skipped anyway.
                    if (d.BodyIsEnabled(chr.Body))
                    {
                        // Collide test the prims with the terrain..   since if you don't do this, 
                        // next frame, all of the physical prim in the scene will awaken and explode upwards
                        d.SpaceCollide2(LandGeom, chr.prim_geom, IntPtr.Zero, nearCallback);
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
                    OdePrim p = (OdePrim) prim;

                    p.setPrimForRemoval();
                    AddPhysicsActorTaint(prim);
                }
            }
        }

        /// <summary>
        /// This is called from within simulate but outside the locked portion
        /// We need to do our own locking here
        /// Essentially, we need to remove the prim from our space segment, whatever segment it's in.
        /// 
        /// If there are no more prim in the segment, we need to empty (spacedestroy)the segment and reclaim memory 
        /// that the space was using.
        /// </summary>
        /// <param name="prim"></param>
        public void RemovePrimThreadLocked(OdePrim prim)
        {
            lock (OdeLock)
            {
                if (prim.IsPhysical)
                {
                    prim.disableBody();
                }
                // we don't want to remove the main space
                if (prim.m_targetSpace != space && prim.IsPhysical == false)
                {
                    // If the geometry is in the targetspace, remove it from the target space
                    if (d.SpaceQuery(prim.m_targetSpace, prim.prim_geom))
                    {
                        if (!(prim.m_targetSpace.Equals(null)))
                        {
                            if (d.GeomIsSpace(prim.m_targetSpace))
                            {
                                d.SpaceRemove(prim.m_targetSpace, prim.prim_geom);
                            }
                            else
                            {
                                MainLog.Instance.Verbose("Physics",
                                                         "Invalid Scene passed to 'removeprim from scene':" +
                                                         ((OdePrim) prim).m_targetSpace.ToString());
                            }
                        }
                    }


                    //If there are no more geometries in the sub-space, we don't need it in the main space anymore
                    if (d.SpaceGetNumGeoms(prim.m_targetSpace) == 0)
                    {
                        if (!(prim.m_targetSpace.Equals(null)))
                        {
                            if (d.GeomIsSpace(prim.m_targetSpace))
                            {
                                d.SpaceRemove(space, prim.m_targetSpace);
                                // free up memory used by the space.
                                d.SpaceDestroy(prim.m_targetSpace);
                                int[] xyspace = calculateSpaceArrayItemFromPos(prim.Position);
                                resetSpaceArrayItemToZero(xyspace[0], xyspace[1]);
                            }
                            else
                            {
                                MainLog.Instance.Verbose("Physics",
                                                         "Invalid Scene passed to 'removeprim from scene':" +
                                                         ((OdePrim) prim).m_targetSpace.ToString());
                            }
                        }
                    }
                }

                d.GeomDestroy(prim.prim_geom);

                _prims.Remove(prim);
            }
        }
        /// <summary>
        /// Takes a space pointer and zeros out the array we're using to hold the spaces
        /// </summary>
        /// <param name="space"></param>
        public void resetSpaceArrayItemToZero(IntPtr space)
        {
            for (int x = 0; x < staticPrimspace.GetLength(0); x++)
            {
                for (int y = 0; y < staticPrimspace.GetLength(1); y++)
                {
                    if (staticPrimspace[x, y] == space)
                        staticPrimspace[x, y] = IntPtr.Zero;
                }
            }
        }

        public void resetSpaceArrayItemToZero(int arrayitemX, int arrayitemY)
        {
            staticPrimspace[arrayitemX, arrayitemY] = IntPtr.Zero;
        }

        /// <summary>
        /// Called when a static prim moves.  Allocates a space for the prim based on it's position
        /// </summary>
        /// <param name="geom">the pointer to the geom that moved</param>
        /// <param name="pos">the position that the geom moved to</param>
        /// <param name="currentspace">a pointer to the space it was in before it was moved.</param>
        /// <returns>a pointer to the new space it's in</returns>
        public IntPtr recalculateSpaceForGeom(IntPtr geom, PhysicsVector pos, IntPtr currentspace)
        {
            
            // Called from setting the Position and Size of an ODEPrim so 
            // it's already in locked space.

            // we don't want to remove the main space
            // we don't need to test physical here because this function should 
            // never be called if the prim is physical(active)

            // All physical prim end up in the root space

            if (currentspace != space)
            {
                if (d.SpaceQuery(currentspace, geom) && currentspace != (IntPtr) 0)
                {
                    if (d.GeomIsSpace(currentspace))
                    {
                        d.SpaceRemove(currentspace, geom);
                    }
                    else
                    {
                        MainLog.Instance.Verbose("Physics",
                                                 "Invalid Scene passed to 'recalculatespace':" + currentspace.ToString() +
                                                 " Geom:" + geom.ToString());
                    }
                }
                else
                {
                    IntPtr sGeomIsIn = d.GeomGetSpace(geom);
                    if (!(sGeomIsIn.Equals(null)))
                    {
                        if (sGeomIsIn != (IntPtr) 0)
                        {
                            if (d.GeomIsSpace(currentspace))
                            {
                                d.SpaceRemove(sGeomIsIn, geom);
                            }
                            else
                            {
                                MainLog.Instance.Verbose("Physics",
                                                         "Invalid Scene passed to 'recalculatespace':" +
                                                         sGeomIsIn.ToString() + " Geom:" + geom.ToString());
                            }
                        }
                    }
                }


                //If there are no more geometries in the sub-space, we don't need it in the main space anymore
                if (d.SpaceGetNumGeoms(currentspace) == 0)
                {
                    if (currentspace != (IntPtr) 0)
                    {
                        if (d.GeomIsSpace(currentspace))
                        {
                            d.SpaceRemove(space, currentspace);
                            // free up memory used by the space.
                            d.SpaceDestroy(currentspace);
                            resetSpaceArrayItemToZero(currentspace);
                        }
                        else
                        {
                            MainLog.Instance.Verbose("Physics",
                                                     "Invalid Scene passed to 'recalculatespace':" +
                                                     currentspace.ToString() + " Geom:" + geom.ToString());
                        }
                    }
                }
            }
            else
            {
                // this is a physical object that got disabled. ;.;
                if (d.SpaceQuery(currentspace, geom))
                {
                    if (currentspace != (IntPtr) 0)
                        if (d.GeomIsSpace(currentspace))
                        {
                            d.SpaceRemove(currentspace, geom);
                        }
                        else
                        {
                            MainLog.Instance.Verbose("Physics",
                                                     "Invalid Scene passed to 'recalculatespace':" +
                                                     currentspace.ToString() + " Geom:" + geom.ToString());
                        }
                }
                else
                {
                    IntPtr sGeomIsIn = d.GeomGetSpace(geom);
                    if (!(sGeomIsIn.Equals(null)))
                    {
                        if (sGeomIsIn != (IntPtr) 0)
                        {
                            if (d.GeomIsSpace(sGeomIsIn))
                            {
                                d.SpaceRemove(sGeomIsIn, geom);
                            }
                            else
                            {
                                MainLog.Instance.Verbose("Physics",
                                                         "Invalid Scene passed to 'recalculatespace':" +
                                                         sGeomIsIn.ToString() + " Geom:" + geom.ToString());
                            }
                        }
                    }
                }
            }


            // The routines in the Position and Size sections do the 'inserting' into the space, 
            // so all we have to do is make sure that the space that we're putting the prim into 
            // is in the 'main' space.
            int[] iprimspaceArrItem = calculateSpaceArrayItemFromPos(pos);
            IntPtr newspace = calculateSpaceForGeom(pos);

            if (newspace == IntPtr.Zero)
            {
                newspace = createprimspace(iprimspaceArrItem[0], iprimspaceArrItem[1]);
                d.HashSpaceSetLevels(newspace, -4, 66);
            }

            return newspace;
        }

        /// <summary>
        /// Creates a new space at X Y
        /// </summary>
        /// <param name="iprimspaceArrItemX"></param>
        /// <param name="iprimspaceArrItemY"></param>
        /// <returns>A pointer to the created space</returns>
        public IntPtr createprimspace(int iprimspaceArrItemX, int iprimspaceArrItemY)
        {
            // creating a new space for prim and inserting it into main space.
            staticPrimspace[iprimspaceArrItemX, iprimspaceArrItemY] = d.HashSpaceCreate(IntPtr.Zero);
            d.SpaceAdd(space, staticPrimspace[iprimspaceArrItemX, iprimspaceArrItemY]);
            return staticPrimspace[iprimspaceArrItemX, iprimspaceArrItemY];
        }

        /// <summary>
        /// Calculates the space the prim should be in by it's position
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>a pointer to the space. This could be a new space or reused space.</returns>
        public IntPtr calculateSpaceForGeom(PhysicsVector pos)
        {
            int[] xyspace = calculateSpaceArrayItemFromPos(pos);
            //MainLog.Instance.Verbose("Physics", "Attempting to use arrayItem: " + xyspace[0].ToString() + "," + xyspace[1].ToString());
            IntPtr locationbasedspace = staticPrimspace[xyspace[0], xyspace[1]];

            //locationbasedspace = space;
            return locationbasedspace;
        }

        /// <summary>
        /// Holds the space allocation logic
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>an array item based on the position</returns>
        public int[] calculateSpaceArrayItemFromPos(PhysicsVector pos)
        {
            int[] returnint = new int[2];

            returnint[0] = (int) (pos.X/metersInSpace);

            if (returnint[0] > ((int) (259f/metersInSpace)))
                returnint[0] = ((int) (259f/metersInSpace));
            if (returnint[0] < 0)
                returnint[0] = 0;

            returnint[1] = (int) (pos.Y/metersInSpace);
            if (returnint[0] > ((int) (259f/metersInSpace)))
                returnint[0] = ((int) (259f/metersInSpace));
            if (returnint[0] < 0)
                returnint[0] = 0;

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


            int[] iprimspaceArrItem = calculateSpaceArrayItemFromPos(pos);
            IntPtr targetspace = calculateSpaceForGeom(pos);

            if (targetspace == IntPtr.Zero)
                targetspace = createprimspace(iprimspaceArrItem[0], iprimspaceArrItem[1]);

            OdePrim newPrim;
            lock (OdeLock)
            {
                newPrim = new OdePrim(name, this, targetspace, pos, siz, rot, mesh, pbs, isphysical);

                _prims.Add(newPrim);
            }

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

        /// <summary>
        /// Routine to figure out if we need to mesh this prim with our mesher
        /// </summary>
        /// <param name="pbs"></param>
        /// <returns></returns>
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
            return AddPrimShape(primName, pbs, position, size, rotation, false);
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

        /// <summary>
        /// Called after our prim properties are set Scale, position etc.
        /// We use this event queue like method to keep changes to the physical scene occuring in the threadlocked mutex
        /// This assures us that we have no race conditions
        /// </summary>
        /// <param name="prim"></param>
        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
            if (prim is OdePrim)
            {
                OdePrim taintedprim = ((OdePrim) prim);
                if (!(_taintedPrim.Contains(taintedprim)))
                    _taintedPrim.Add(taintedprim);
            }
        }

        /// <summary>
        /// This is our main simulate loop
        /// It's thread locked by a Mutex in the scene. 
        /// It holds Collisions, it instructs ODE to step through the physical reactions
        /// It moves the objects around in memory
        /// It calls the methods that report back to the object owners.. (scenepresence, SceneObjectGroup)
        /// </summary>
        /// <param name="timeStep"></param>
        /// <returns></returns>
        public override float Simulate(float timeStep)
        {
            float fps = 0;

            step_time += timeStep;


            // If We're loaded down by something else, 
            // or debugging with the Visual Studio project on pause
            // skip a few frames to catch up gracefully.
            // without shooting the physicsactors all over the place


            if (step_time >= m_SkipFramesAtms)
            {
                // Instead of trying to catch up, it'll do 5 physics frames only
                step_time = ODE_STEPSIZE;
                m_physicsiterations = 5;
            }
            else
            {
                m_physicsiterations = 10;
            }
            lock (OdeLock)
            {
                // Process 10 frames if the sim is running normal..  
                // process 5 frames if the sim is running slow
                try
                {
                    d.WorldSetQuickStepNumIterations(world, m_physicsiterations);
                }
                catch (StackOverflowException)
                {
                    MainLog.Instance.Error("PHYSICS",
                                           "The operating system wasn't able to allocate enough memory for the simulation.  Restarting the sim.");
                    base.TriggerPhysicsBasedRestart();
                }

                int i = 0;


                // Figure out the Frames Per Second we're going at.
                //(step_time == 0.004f, there's 250 of those per second.   Times the step time/step size
                fps = ((step_time/ODE_STEPSIZE)*(m_physicsiterations*250));


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
                }

                bool processedtaints = false;
                foreach (OdePrim prim in _taintedPrim)
                {
                    prim.ProcessTaints(timeStep);
                    if (prim.m_taintremove)
                    {
                        RemovePrimThreadLocked(prim);
                    }
                    processedtaints = true;
                }

                if (processedtaints)
                    _taintedPrim = new List<OdePrim>();

                if (timeStep < 0.2f)
                {
                    foreach (OdePrim actor in _activeprims)
                    {
                        if (actor.IsPhysical && (d.BodyIsEnabled(actor.Body) || !actor._zeroFlag))
                        {
                            actor.UpdatePositionAndVelocity();
                        }
                    }
                }
            }
            return fps;
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            get { return (false); // for now we won't be multithreaded
            }
        }

        public float[] ResizeTerrain512(float[] heightMap)
        {
            float[] returnarr = new float[262144];
            float[,] resultarr = new float[m_regionWidth,m_regionHeight];

            // Filling out the array into it's multi-dimentional components
            for (int y = 0; y < m_regionHeight; y++)
            {
                for (int x = 0; x < m_regionWidth; x++)
                {
                    resultarr[y, x] = heightMap[y*m_regionWidth + x];
                }
            }

            // Resize using interpolation

            // This particular way is quick but it only works on a multiple of the original

            // The idea behind this method can be described with the following diagrams
            // second pass and third pass happen in the same loop really..  just separated 
            // them to show what this does.

            // First Pass
            // ResultArr:
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1
            // 1,1,1,1,1,1

            // Second Pass
            // ResultArr2:
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,
            // ,,,,,,,,,,
            // 1,,1,,1,,1,,1,,1,

            // Third pass fills in the blanks
            // ResultArr2:
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1
            // 1,1,1,1,1,1,1,1,1,1,1,1

            // X,Y = .
            // X+1,y = ^
            // X,Y+1 = *
            // X+1,Y+1 = #

            // Filling in like this;
            // .*
            // ^#
            // 1st .
            // 2nd *
            // 3rd ^
            // 4th #
            // on single loop.

            float[,] resultarr2 = new float[512,512];
            for (int y = 0; y < m_regionHeight; y++)
            {
                for (int x = 0; x < m_regionWidth; x++)
                {
                    resultarr2[y*2, x*2] = resultarr[y, x];

                    if (y < m_regionHeight)
                    {
                        if (y + 1 < m_regionHeight)
                        {
                            if (x + 1 < m_regionWidth)
                            {
                                resultarr2[(y*2) + 1, x*2] = ((resultarr[y, x] + resultarr[y + 1, x] +
                                                               resultarr[y, x + 1] + resultarr[y + 1, x + 1])/4);
                            }
                            else
                            {
                                resultarr2[(y*2) + 1, x*2] = ((resultarr[y, x] + resultarr[y + 1, x])/2);
                            }
                        }
                        else
                        {
                            resultarr2[(y*2) + 1, x*2] = resultarr[y, x];
                        }
                    }
                    if (x < m_regionWidth)
                    {
                        if (x + 1 < m_regionWidth)
                        {
                            if (y + 1 < m_regionHeight)
                            {
                                resultarr2[y*2, (x*2) + 1] = ((resultarr[y, x] + resultarr[y + 1, x] +
                                                               resultarr[y, x + 1] + resultarr[y + 1, x + 1])/4);
                            }
                            else
                            {
                                resultarr2[y*2, (x*2) + 1] = ((resultarr[y, x] + resultarr[y, x + 1])/2);
                            }
                        }
                        else
                        {
                            resultarr2[y*2, (x*2) + 1] = resultarr[y, x];
                        }
                    }
                    if (x < m_regionWidth && y < m_regionHeight)
                    {
                        if ((x + 1 < m_regionWidth) && (y + 1 < m_regionHeight))
                        {
                            resultarr2[(y*2) + 1, (x*2) + 1] = ((resultarr[y, x] + resultarr[y + 1, x] +
                                                                 resultarr[y, x + 1] + resultarr[y + 1, x + 1])/4);
                        }
                        else
                        {
                            resultarr2[(y*2) + 1, (x*2) + 1] = resultarr[y, x];
                        }
                    }
                }
            }
            //Flatten out the array
            int i = 0;
            for (int y = 0; y < 512; y++)
            {
                for (int x = 0; x < 512; x++)
                {
                    if (resultarr2[y, x] <= 0)
                        returnarr[i] = 0.0000001f;
                    else
                        returnarr[i] = resultarr2[y, x];

                    i++;
                }
            }

            return returnarr;
        }

        public override void SetTerrain(float[] heightMap)
        {
            // this._heightmap[i] = (double)heightMap[i];
            // dbm (danx0r) -- heightmap x,y must be swapped for Ode (should fix ODE, but for now...)
            // also, creating a buffer zone of one extra sample all around
            _origheightmap = heightMap;
            const uint heightmapWidth = m_regionWidth + 2;
            const uint heightmapHeight = m_regionHeight + 2;
            const uint heightmapWidthSamples = 2*m_regionWidth + 2;
            const uint heightmapHeightSamples = 2*m_regionHeight + 2;
            const float scale = 1.0f;
            const float offset = 0.0f;
            const float thickness = 2.0f;
            const int wrap = 0;

            //Double resolution
            heightMap = ResizeTerrain512(heightMap);
            for (int x = 0; x < heightmapWidthSamples; x++)
            {
                for (int y = 0; y < heightmapHeightSamples; y++)
                {
                    int xx = Util.Clip(x - 1, 0, 511);
                    int yy = Util.Clip(y - 1, 0, 511);

                    double val = (double) heightMap[yy*512 + xx];
                    _heightmap[x*heightmapHeightSamples + y] = val;
                }
            }

            lock (OdeLock)
            {
                if (!(LandGeom == (IntPtr) 0))
                {
                    d.SpaceRemove(space, LandGeom);
                }
                IntPtr HeightmapData = d.GeomHeightfieldDataCreate();
                d.GeomHeightfieldDataBuildDouble(HeightmapData, _heightmap, 0, heightmapWidth, heightmapHeight,
                                                 (int) heightmapWidthSamples, (int) heightmapHeightSamples, scale,
                                                 offset, thickness, wrap);
                d.GeomHeightfieldDataSetBounds(HeightmapData, m_regionWidth, m_regionHeight);
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
}
