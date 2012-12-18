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
using OpenSim.Region.Physics.Manager;
using OdeAPI;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.Physics.OdePlugin
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
        private OdeScene m_scene;

        IntPtr ray; // the ray. we only need one for our lifetime

        private const int ColisionContactGeomsPerTest = 5;
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

        public ODERayCastRequestManager(OdeScene pScene)
        {
            m_scene = pScene;
            nearCallback = near;
            ray = d.CreateRay(IntPtr.Zero, 1.0f);
            d.GeomSetCategoryBits(ray,0);
        }

        /// <summary>
        /// Queues request for a raycast to all world 
        /// </summary>
        /// <param name="position">Origin of Ray</param>
        /// <param name="direction">Ray direction</param>
        /// <param name="length">Ray length</param>
        /// <param name="retMethod">Return method to send the results</param>
        public void QueueRequest(Vector3 position, Vector3 direction, float length, RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.Count = DefaultMaxCount;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.filter = RayFilterFlags.AllPrims;

            m_PendingRequests.Enqueue(req);
        }

        /// <summary>
        /// Queues request for a raycast to particular part
        /// </summary>
        /// <param name="position">Origin of Ray</param>
        /// <param name="direction">Ray direction</param>
        /// <param name="length">Ray length</param>
        /// <param name="retMethod">Return method to send the results</param>
        public void QueueRequest(IntPtr geom, Vector3 position, Vector3 direction, float length, RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = DefaultMaxCount;
            req.filter = RayFilterFlags.AllPrims;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.Count = DefaultMaxCount;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.filter = RayFilterFlags.AllPrims | RayFilterFlags.land;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(IntPtr geom, Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = DefaultMaxCount;
            req.filter = RayFilterFlags.AllPrims;

            m_PendingRequests.Enqueue(req);
        }

        /// <summary>
        /// Queues a raycast
        /// </summary>
        /// <param name="position">Origin of Ray</param>
        /// <param name="direction">Ray normal</param>
        /// <param name="length">Ray length</param>
        /// <param name="count"></param>
        /// <param name="retMethod">Return method to send the results</param>
        public void QueueRequest(Vector3 position, Vector3 direction, float length, int count, RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = count;
            req.filter = RayFilterFlags.AllPrims;

            m_PendingRequests.Enqueue(req);
        }


        public void QueueRequest(Vector3 position, Vector3 direction, float length, int count,RayFilterFlags filter , RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = count;
            req.filter = filter;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(IntPtr geom, Vector3 position, Vector3 direction, float length, int count, RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = count;
            req.filter = RayFilterFlags.AllPrims;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(IntPtr geom, Vector3 position, Vector3 direction, float length, int count,RayFilterFlags flags, RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = count;
            req.filter = flags;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(Vector3 position, Vector3 direction, float length, int count, RaycastCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = count;
            req.filter = RayFilterFlags.AllPrims;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(IntPtr geom, Vector3 position, Vector3 direction, float length, int count, RaycastCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = count;
            req.filter = RayFilterFlags.AllPrims;

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
                    CurrentRayFilter = req.filter;
                    CurrentMaxCount = req.Count;

                    closestHit = ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0 ? 0 : 1);
                    backfacecull = ((CurrentRayFilter & RayFilterFlags.BackFaceCull) == 0 ? 0 : 1);

                    d.GeomRaySetLength(ray, req.length);
                    d.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);
                    d.GeomRaySetParams(ray, 0, backfacecull);
                    d.GeomRaySetClosestHit(ray, closestHit);

                    if (req.callbackMethod is RaycastCallback)
                        // if we only want one get only one per colision pair saving memory
                        CurrentRayFilter |= RayFilterFlags.ClosestHit;

                    if (req.geom == IntPtr.Zero)
                    {
                        // translate ray filter to colision flags
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
                            d.GeomSetCollideBits(ray, (uint)catflags);
                            doSpaceRay(req);
                        }
                    }
                    else
                    {
                        // if we select a geom don't use filters
                        d.GeomSetCollideBits(ray, (uint)CollisionCategories.All);
                        doGeomRay(req);
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

        private const RayFilterFlags FilterActiveSpace = RayFilterFlags.agent | RayFilterFlags.physical | RayFilterFlags.LSLPhanton;
//        private const RayFilterFlags FilterStaticSpace = RayFilterFlags.water | RayFilterFlags.land | RayFilterFlags.nonphysical | RayFilterFlags.LSLPhanton;
        private const RayFilterFlags FilterStaticSpace = RayFilterFlags.water | RayFilterFlags.nonphysical | RayFilterFlags.LSLPhanton;

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

                if (req.length > 30.0f)
                    d.GeomRaySetLength(ray, 30.0f);

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

        /// <summary>
        /// Method that actually initiates the raycast with a geom
        /// </summary>
        /// <param name="req"></param>
        private void doGeomRay(ODERayRequest req)
        {
            // Collide test
            d.SpaceCollide2(ray, req.geom, IntPtr.Zero, nearCallback); // still do this to have full AABB pre test

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
            if (ContactgeomsArray == IntPtr.Zero || index >= ColisionContactGeomsPerTest)
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
                count = d.CollidePtr(g1, g2, ColisionContactGeomsPerTest, m_scene.ContactgeomsArray, d.ContactGeom.unmanagedSizeOf);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS Ray]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            if (count == 0)
                return;

            uint ID = 0;
            PhysicsActor p2 = null;

            m_scene.actor_name_map.TryGetValue(g2, out p2);

            if (p2 == null)
            {
                /*
                                string name;

                                if (!m_scene.geom_name_map.TryGetValue(g2, out name))
                                    return;

                                if (name == "Terrain")
                                {
                                    // land colision
                                    if ((CurrentRayFilter & RayFilterFlags.land) == 0)
                                        return;
                                }
                                else if (name == "Water")
                                {
                                    if ((CurrentRayFilter & RayFilterFlags.water) == 0)
                                        return;
                                }
                                else
                                    return;
                 */
                return;
            }
            else
            {
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
                    collisionresult.Pos = new Vector3(curcontact.pos.X, curcontact.pos.Y, curcontact.pos.Z);
                    collisionresult.Depth = curcontact.depth;
                    collisionresult.Normal = new Vector3(curcontact.normal.X, curcontact.normal.Y,
                                                         curcontact.normal.Z);
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
                        collisionresult.Pos = new Vector3(curcontact.pos.X, curcontact.pos.Y, curcontact.pos.Z);
                        collisionresult.Depth = curcontact.depth;
                        collisionresult.Normal = new Vector3(curcontact.normal.X, curcontact.normal.Y,
                                                             curcontact.normal.Z);
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
        }
    }

    public struct ODERayRequest
    {
        public IntPtr geom;
        public Vector3 Origin;
        public Vector3 Normal;
        public int Count;
        public float length;
        public object callbackMethod;
        public RayFilterFlags filter;
    }
}