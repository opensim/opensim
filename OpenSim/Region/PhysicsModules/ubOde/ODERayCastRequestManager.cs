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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
using OdeAPI;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.ubOde
{
    /// <summary>
    /// Processes raycast requests as ODE is in a state to be able to do them.
    /// This ensures that it's thread safe and there will be no conflicts.
    /// Requests get returned by a different thread then they were requested by.
    /// </summary>
    public class ODERayCastRequestManager
    {
        /// <summary>
        /// Pending ray requests
        /// </summary>
        protected OpenSim.Framework.LocklessQueue<ODERayRequest> m_PendingRequests = new OpenSim.Framework.LocklessQueue<ODERayRequest>();

        /// <summary>
        /// Scene that created this object.
        /// </summary>
        private ODEScene m_scene;

        IntPtr ray; // the ray. we only need one for our lifetime
        IntPtr Sphere;
        IntPtr Box;
        IntPtr Plane;

        private int CollisionContactGeomsPerTest = 25;
        private const int DefaultMaxCount = 25;
        private const int MaxTimePerCallMS = 30;

        /// <summary>
        /// ODE near callback delegate
        /// </summary>
        private d.NearCallback nearCallback;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<ContactResult> m_contactResults = new List<ContactResult>();
        private RayFilterFlags CurrentRayFilter;
        private int CurrentMaxCount;

        public ODERayCastRequestManager(ODEScene pScene)
        {
            m_scene = pScene;
            nearCallback = near;
            ray = d.CreateRay(IntPtr.Zero, 1.0f);
            d.GeomSetCategoryBits(ray, 0);
            Box = d.CreateBox(IntPtr.Zero, 1.0f, 1.0f, 1.0f);
            d.GeomSetCategoryBits(Box, 0);
            Sphere = d.CreateSphere(IntPtr.Zero,1.0f);
            d.GeomSetCategoryBits(Sphere, 0);
            Plane = d.CreatePlane(IntPtr.Zero, 0f,0f,1f,1f);
            d.GeomSetCategoryBits(Sphere, 0);
        }

        public void QueueRequest(ODERayRequest req)
        {
            if (req.Count == 0)
                req.Count = DefaultMaxCount;

            m_PendingRequests.Enqueue(req);
        }

        /// <summary>
        /// Process all queued raycast requests
        /// </summary>
        /// <returns>Time in MS the raycasts took to process.</returns>
        public int ProcessQueuedRequests()
        {

            if (m_PendingRequests.Count <= 0)
                return 0;

            if (m_scene.ContactgeomsArray == IntPtr.Zero || ray == IntPtr.Zero)
                // oops something got wrong or scene isn't ready still
            {
                m_PendingRequests.Clear();
                return 0;
            }

            int time = Util.EnvironmentTickCount();

            ODERayRequest req;
            int closestHit;
            int backfacecull;
            CollisionCategories catflags;

            while (m_PendingRequests.Dequeue(out req))
            {
                if (req.callbackMethod != null)
                {
                    IntPtr geom = IntPtr.Zero;
                    if (req.actor != null)
                    {
                        if (m_scene.haveActor(req.actor))
                        {
                            if (req.actor is OdePrim)
                                geom = ((OdePrim)req.actor).prim_geom;
                            else if (req.actor is OdeCharacter)
                                geom = ((OdePrim)req.actor).prim_geom;
                        }
                        if (geom == IntPtr.Zero)
                        {
                            NoContacts(req);
                            continue;
                        }
                    }
                   
                    CurrentRayFilter = req.filter;
                    CurrentMaxCount = req.Count;

                    CollisionContactGeomsPerTest = req.Count & 0xffff;

                    closestHit = ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0 ? 0 : 1);
                    backfacecull = ((CurrentRayFilter & RayFilterFlags.BackFaceCull) == 0 ? 0 : 1);

                    if (req.callbackMethod is ProbeBoxCallback)
                    {
                        if (CollisionContactGeomsPerTest > 80)
                            CollisionContactGeomsPerTest = 80;
                        d.GeomBoxSetLengths(Box, req.Normal.X, req.Normal.Y, req.Normal.Z);
                        d.GeomSetPosition(Box, req.Origin.X, req.Origin.Y, req.Origin.Z);
                        d.Quaternion qtmp;
                        qtmp.X = req.orientation.X;
                        qtmp.Y = req.orientation.Y;
                        qtmp.Z = req.orientation.Z;
                        qtmp.W = req.orientation.W;
                        d.GeomSetQuaternion(Box, ref qtmp);
                    }
                    else if (req.callbackMethod is ProbeSphereCallback)
                    {
                        if (CollisionContactGeomsPerTest > 80)
                            CollisionContactGeomsPerTest = 80;

                        d.GeomSphereSetRadius(Sphere, req.length);
                        d.GeomSetPosition(Sphere, req.Origin.X, req.Origin.Y, req.Origin.Z);
                    }
                    else if (req.callbackMethod is ProbePlaneCallback)
                    {
                        if (CollisionContactGeomsPerTest > 80)
                            CollisionContactGeomsPerTest = 80;

                        d.GeomPlaneSetParams(Plane, req.Normal.X, req.Normal.Y, req.Normal.Z, req.length);
                    }

                    else
                    {
                        if (CollisionContactGeomsPerTest > 25)
                            CollisionContactGeomsPerTest = 25;

                        d.GeomRaySetLength(ray, req.length);
                        d.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);
                        d.GeomRaySetParams(ray, 0, backfacecull);

                        if (req.callbackMethod is RaycastCallback)
                        {
                            // if we only want one get only one per Collision pair saving memory
                            CurrentRayFilter |= RayFilterFlags.ClosestHit;
                            d.GeomRaySetClosestHit(ray, 1);
                        }
                        else
                            d.GeomRaySetClosestHit(ray, closestHit);
                    }

                    if ((CurrentRayFilter & RayFilterFlags.ContactsUnImportant) != 0)
                        unchecked
                        {
                            CollisionContactGeomsPerTest |= (int)d.CONTACTS_UNIMPORTANT;
                        }

                    if (geom == IntPtr.Zero)
                    {
                        // translate ray filter to Collision flags
                        catflags = 0;
                        if ((CurrentRayFilter & RayFilterFlags.volumedtc) != 0)
                            catflags |= CollisionCategories.VolumeDtc;
                        if ((CurrentRayFilter & RayFilterFlags.phantom) != 0)
                            catflags |= CollisionCategories.Phantom;
                        if ((CurrentRayFilter & RayFilterFlags.agent) != 0)
                            catflags |= CollisionCategories.Character;
                        if ((CurrentRayFilter & RayFilterFlags.PrimsNonPhantom) != 0)
                            catflags |= CollisionCategories.Geom;
                        if ((CurrentRayFilter & RayFilterFlags.land) != 0)
                            catflags |= CollisionCategories.Land;
                        if ((CurrentRayFilter & RayFilterFlags.water) != 0)
                            catflags |= CollisionCategories.Water;

                        if (catflags != 0)
                        {
                            if (req.callbackMethod is ProbeBoxCallback)
                            {
                                catflags |= CollisionCategories.Space;
                                d.GeomSetCollideBits(Box, (uint)catflags);
                                d.GeomSetCategoryBits(Box, (uint)catflags);
                                doProbe(req, Box);
                            }
                            else if (req.callbackMethod is ProbeSphereCallback)
                            {
                                catflags |= CollisionCategories.Space;
                                d.GeomSetCollideBits(Sphere, (uint)catflags);
                                d.GeomSetCategoryBits(Sphere, (uint)catflags);
                                doProbe(req, Sphere);
                            }
                            else if (req.callbackMethod is ProbePlaneCallback)
                            {
                                catflags |= CollisionCategories.Space;
                                d.GeomSetCollideBits(Plane, (uint)catflags);
                                d.GeomSetCategoryBits(Plane, (uint)catflags);
                                doPlane(req,IntPtr.Zero);
                            }
                            else
                            {
                                d.GeomSetCollideBits(ray, (uint)catflags);
                                doSpaceRay(req);
                            }
                        }
                    }
                    else
                    {
                        // if we select a geom don't use filters

                        if (req.callbackMethod is ProbePlaneCallback)
                        {
                            d.GeomSetCollideBits(Plane, (uint)CollisionCategories.All);
                            doPlane(req,geom);
                        }
                        else
                        {
                            d.GeomSetCollideBits(ray, (uint)CollisionCategories.All);
                            doGeomRay(req,geom);
                        }
                    }
                }

                if (Util.EnvironmentTickCountSubtract(time) > MaxTimePerCallMS)
                    break;
            }

            lock (m_contactResults)
                m_contactResults.Clear();

            return Util.EnvironmentTickCountSubtract(time);
        }
        /// <summary>
        /// Method that actually initiates the raycast with spaces
        /// </summary>
        /// <param name="req"></param>
        /// 

        private void NoContacts(ODERayRequest req)
        {
            if (req.callbackMethod is RaycastCallback)
            {
                ((RaycastCallback)req.callbackMethod)(false, Vector3.Zero, 0, 0, Vector3.Zero);
                return;
            }
            List<ContactResult> cresult = new List<ContactResult>();

            if (req.callbackMethod is RayCallback)
                ((RayCallback)req.callbackMethod)(cresult);
            else if (req.callbackMethod is ProbeBoxCallback)
                ((ProbeBoxCallback)req.callbackMethod)(cresult);
            else if (req.callbackMethod is ProbeSphereCallback)
                ((ProbeSphereCallback)req.callbackMethod)(cresult);
        }

        private const RayFilterFlags FilterActiveSpace = RayFilterFlags.agent | RayFilterFlags.physical | RayFilterFlags.LSLPhantom;
//        private const RayFilterFlags FilterStaticSpace = RayFilterFlags.water | RayFilterFlags.land | RayFilterFlags.nonphysical | RayFilterFlags.LSLPhanton;
        private const RayFilterFlags FilterStaticSpace = RayFilterFlags.water | RayFilterFlags.nonphysical | RayFilterFlags.LSLPhantom;

        private void doSpaceRay(ODERayRequest req)
        {
            // Collide tests
            if ((CurrentRayFilter & FilterActiveSpace) != 0)
            {
                d.SpaceCollide2(ray, m_scene.ActiveSpace, IntPtr.Zero, nearCallback);
                d.SpaceCollide2(ray, m_scene.CharsSpace, IntPtr.Zero, nearCallback);
            }
            if ((CurrentRayFilter & FilterStaticSpace) != 0 && (m_contactResults.Count < CurrentMaxCount))
                d.SpaceCollide2(ray, m_scene.StaticSpace, IntPtr.Zero, nearCallback);
            if ((CurrentRayFilter & RayFilterFlags.land) != 0 && (m_contactResults.Count < CurrentMaxCount))
            {
                // current ode land to ray collisions is very bad
                // so for now limit its range badly
                if (req.length > 60.0f)
                    d.GeomRaySetLength(ray, 60.0f);

                d.SpaceCollide2(ray, m_scene.GroundSpace, IntPtr.Zero, nearCallback);
            }

            if (req.callbackMethod is RaycastCallback)
            {
                // Define default results
                bool hitYN = false;
                uint hitConsumerID = 0;
                float distance = float.MaxValue;
                Vector3 closestcontact = Vector3.Zero;
                Vector3 snormal = Vector3.Zero;

                // Find closest contact and object.
                lock (m_contactResults)
                {
                    foreach (ContactResult cResult in m_contactResults)
                    {
                        if(cResult.Depth < distance)
                        {
                            closestcontact = cResult.Pos;
                            hitConsumerID = cResult.ConsumerID;
                            distance = cResult.Depth;
                            snormal = cResult.Normal;
                        }
                    }
                    m_contactResults.Clear();
                }

                if (distance > 0 && distance < float.MaxValue)
                    hitYN = true;
                ((RaycastCallback)req.callbackMethod)(hitYN, closestcontact, hitConsumerID, distance, snormal);
            }
            else
            {
                List<ContactResult> cresult = new List<ContactResult>(m_contactResults.Count);
                lock (m_PendingRequests)
                {
                    cresult.AddRange(m_contactResults);
                    m_contactResults.Clear();
                }
                ((RayCallback)req.callbackMethod)(cresult);
            }
        }

        private void doProbe(ODERayRequest req, IntPtr probe)
        {
            // Collide tests
            if ((CurrentRayFilter & FilterActiveSpace) != 0)
            {
                d.SpaceCollide2(probe, m_scene.ActiveSpace, IntPtr.Zero, nearCallback);
                d.SpaceCollide2(probe, m_scene.CharsSpace, IntPtr.Zero, nearCallback);
            }
            if ((CurrentRayFilter & FilterStaticSpace) != 0 && (m_contactResults.Count < CurrentMaxCount))
                d.SpaceCollide2(probe, m_scene.StaticSpace, IntPtr.Zero, nearCallback);
            if ((CurrentRayFilter & RayFilterFlags.land) != 0 && (m_contactResults.Count < CurrentMaxCount))
                d.SpaceCollide2(probe, m_scene.GroundSpace, IntPtr.Zero, nearCallback);

            List<ContactResult> cresult = new List<ContactResult>(m_contactResults.Count);
            lock (m_PendingRequests)
            {
                cresult.AddRange(m_contactResults);
                m_contactResults.Clear();
            }
            if (req.callbackMethod is ProbeBoxCallback)
                ((ProbeBoxCallback)req.callbackMethod)(cresult);
            else if (req.callbackMethod is ProbeSphereCallback)
                ((ProbeSphereCallback)req.callbackMethod)(cresult);
        }

        private void doPlane(ODERayRequest req,IntPtr geom)
        {
            // Collide tests
            if (geom == IntPtr.Zero)
            {
                if ((CurrentRayFilter & FilterActiveSpace) != 0)
                {
                    d.SpaceCollide2(Plane, m_scene.ActiveSpace, IntPtr.Zero, nearCallback);
                    d.SpaceCollide2(Plane, m_scene.CharsSpace, IntPtr.Zero, nearCallback);
                }
                if ((CurrentRayFilter & FilterStaticSpace) != 0 && (m_contactResults.Count < CurrentMaxCount))
                    d.SpaceCollide2(Plane, m_scene.StaticSpace, IntPtr.Zero, nearCallback);
                if ((CurrentRayFilter & RayFilterFlags.land) != 0 && (m_contactResults.Count < CurrentMaxCount))
                    d.SpaceCollide2(Plane, m_scene.GroundSpace, IntPtr.Zero, nearCallback);
            }
            else
            {
                d.SpaceCollide2(Plane, geom, IntPtr.Zero, nearCallback);
            }

            List<ContactResult> cresult = new List<ContactResult>(m_contactResults.Count);
            lock (m_PendingRequests)
            {
                cresult.AddRange(m_contactResults);
                m_contactResults.Clear();
            }

            ((ProbePlaneCallback)req.callbackMethod)(cresult);
        }

        /// <summary>
        /// Method that actually initiates the raycast with a geom
        /// </summary>
        /// <param name="req"></param>
        private void doGeomRay(ODERayRequest req, IntPtr geom)
        {
            // Collide test
            d.SpaceCollide2(ray, geom, IntPtr.Zero, nearCallback); // still do this to have full AABB pre test

            if (req.callbackMethod is RaycastCallback)
            {
                // Define default results
                bool hitYN = false;
                uint hitConsumerID = 0;
                float distance = float.MaxValue;
                Vector3 closestcontact = Vector3.Zero;
                Vector3 snormal = Vector3.Zero;

                // Find closest contact and object.
                lock (m_contactResults)
                {
                    foreach (ContactResult cResult in m_contactResults)
                    {
                        if(cResult.Depth < distance )
                        {
                            closestcontact = cResult.Pos;
                            hitConsumerID = cResult.ConsumerID;
                            distance = cResult.Depth;
                            snormal = cResult.Normal;
                        }
                    }
                    m_contactResults.Clear();
                }

                if (distance > 0 && distance < float.MaxValue)
                    hitYN = true;

                ((RaycastCallback)req.callbackMethod)(hitYN, closestcontact, hitConsumerID, distance, snormal);
            }
            else
            {
                List<ContactResult> cresult = new List<ContactResult>(m_contactResults.Count);
                lock (m_PendingRequests)
                {
                    cresult.AddRange(m_contactResults);
                    m_contactResults.Clear();
                }
                ((RayCallback)req.callbackMethod)(cresult);
            }
        }

        private bool GetCurContactGeom(int index, ref d.ContactGeom newcontactgeom)
        {
            IntPtr ContactgeomsArray = m_scene.ContactgeomsArray;
            if (ContactgeomsArray == IntPtr.Zero || index >= CollisionContactGeomsPerTest)
                return false;

            IntPtr contactptr = new IntPtr(ContactgeomsArray.ToInt64() + (Int64)(index * d.ContactGeom.unmanagedSizeOf));
            newcontactgeom = (d.ContactGeom)Marshal.PtrToStructure(contactptr, typeof(d.ContactGeom));
            return true;
        }
        
        // This is the standard Near. g1 is the ray
        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
             if (g2 == IntPtr.Zero || g1 == g2)
                return;

             if (m_contactResults.Count >= CurrentMaxCount)
                return;

            if (d.GeomIsSpace(g2))
            {
                try
                {
                    d.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[PHYSICS Ray]: Unable to Space collide test an object: {0}", e.Message);
                }
                return;
            }

            int count = 0;
            try
            {
                count = d.CollidePtr(g1, g2, CollisionContactGeomsPerTest, m_scene.ContactgeomsArray, d.ContactGeom.unmanagedSizeOf);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS Ray]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            if (count == 0)
                return;
/*
            uint cat1 = d.GeomGetCategoryBits(g1);
            uint cat2 = d.GeomGetCategoryBits(g2);
            uint col1 = d.GeomGetCollideBits(g1);
            uint col2 = d.GeomGetCollideBits(g2);
*/
            
            uint ID = 0;
            PhysicsActor p2 = null;

            m_scene.actor_name_map.TryGetValue(g2, out p2);

            if (p2 == null)
                return;

            switch (p2.PhysicsActorType)
            {
                case (int)ActorTypes.Prim:

                    RayFilterFlags thisFlags;

                    if (p2.IsPhysical)
                        thisFlags = RayFilterFlags.physical;
                    else
                        thisFlags = RayFilterFlags.nonphysical;

                    if (p2.Phantom)
                        thisFlags |= RayFilterFlags.phantom;

                    if (p2.IsVolumeDtc)
                        thisFlags |= RayFilterFlags.volumedtc;

                    if ((thisFlags & CurrentRayFilter) == 0)
                        return;

                    ID = ((OdePrim)p2).LocalID;
                    break;

                case (int)ActorTypes.Agent:

                    if ((CurrentRayFilter & RayFilterFlags.agent) == 0)
                        return;
                    else
                        ID = ((OdeCharacter)p2).LocalID;
                    break;

                case (int)ActorTypes.Ground:

                    if ((CurrentRayFilter & RayFilterFlags.land) == 0)
                        return;
                    break;

                case (int)ActorTypes.Water:

                    if ((CurrentRayFilter & RayFilterFlags.water) == 0)
                        return;
                    break;

                default:
                    break;
            }

            d.ContactGeom curcontact = new d.ContactGeom();

            // closestHit for now only works for meshs, so must do it for others
            if ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0)
            {
                // Loop all contacts, build results.
                for (int i = 0; i < count; i++)
                {
                    if (!GetCurContactGeom(i, ref curcontact))
                        break;

                    ContactResult collisionresult = new ContactResult();
                    collisionresult.ConsumerID = ID;
                    collisionresult.Pos.X = curcontact.pos.X;
                    collisionresult.Pos.Y = curcontact.pos.Y;
                    collisionresult.Pos.Z = curcontact.pos.Z;
                    collisionresult.Depth = curcontact.depth;
                    collisionresult.Normal.X = curcontact.normal.X;
                    collisionresult.Normal.Y = curcontact.normal.Y;
                    collisionresult.Normal.Z = curcontact.normal.Z;
                    lock (m_contactResults)
                    {
                        m_contactResults.Add(collisionresult);
                        if (m_contactResults.Count >= CurrentMaxCount)
                            return;
                    }
                }
            }
            else
            {
                // keep only closest contact
                ContactResult collisionresult = new ContactResult();
                collisionresult.ConsumerID = ID;
                collisionresult.Depth = float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    if (!GetCurContactGeom(i, ref curcontact))
                        break;

                    if (curcontact.depth < collisionresult.Depth)
                    {
                        collisionresult.Pos.X = curcontact.pos.X;
                        collisionresult.Pos.Y = curcontact.pos.Y;
                        collisionresult.Pos.Z = curcontact.pos.Z;
                        collisionresult.Depth = curcontact.depth;
                        collisionresult.Normal.X = curcontact.normal.X;
                        collisionresult.Normal.Y = curcontact.normal.Y;
                        collisionresult.Normal.Z = curcontact.normal.Z;
                    }
                }

                if (collisionresult.Depth != float.MaxValue)
                {
                    lock (m_contactResults)
                        m_contactResults.Add(collisionresult);
                }
            }
        }

        /// <summary>
        /// Dereference the creator scene so that it can be garbage collected if needed.
        /// </summary>
        internal void Dispose()
        {
            m_scene = null;
            if (ray != IntPtr.Zero)
            {
                d.GeomDestroy(ray);
                ray = IntPtr.Zero;
            }
            if (Box != IntPtr.Zero)
            {
                d.GeomDestroy(Box);
                Box = IntPtr.Zero;
            }
            if (Sphere != IntPtr.Zero)
            {
                d.GeomDestroy(Sphere);
                Sphere = IntPtr.Zero;
            }
            if (Plane != IntPtr.Zero)
            {
                d.GeomDestroy(Plane);
                Plane = IntPtr.Zero;
            }           
        }
    }

    public struct ODERayRequest
    {
        public PhysicsActor actor;
        public Vector3 Origin;
        public Vector3 Normal;
        public int Count;
        public float length;
        public object callbackMethod;
        public RayFilterFlags filter;
        public Quaternion orientation;
    }
}
