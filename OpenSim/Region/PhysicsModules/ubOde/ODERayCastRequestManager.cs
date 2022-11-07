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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
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
        private readonly ODEScene m_scene;
        private readonly SafeNativeMethods.ContactGeom[] m_contacts;

        IntPtr ray; // the ray. we only need one for our lifetime

        private int CollisionContactGeomsPerTest = 25;
        private const int DefaultMaxCount = 25;
        private const int MaxTimePerCallMS = 30;

        /// <summary>
        /// ODE near callback delegate
        /// </summary>
        private readonly SafeNativeMethods.NearCallback nearCallback;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<ContactResult> m_contactResults = new List<ContactResult>();
        private RayFilterFlags CurrentRayFilter;
        private int CurrentMaxCount;
        ContactResult SharedCollisionResult = new ContactResult();

        public ODERayCastRequestManager(ODEScene pScene)
        {
            m_scene = pScene;
            m_contacts = pScene.m_contacts;
            nearCallback = near;
            ray = SafeNativeMethods.CreateRay(IntPtr.Zero, 1.0f);
            SafeNativeMethods.GeomSetCategoryBits(ray, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QueueRequest(ODERayRequest req)
        {
            if (req.Count == 0)
                req.Count = DefaultMaxCount;

            m_PendingRequests.Enqueue(req);
        }

        /// <summary>
        /// Process all queued raycast requests
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessQueuedRequests()
        {
            if (m_PendingRequests.Count == 0)
                return ;

            int time = Util.EnvironmentTickCount();
            while (m_PendingRequests.Dequeue(out ODERayRequest req))
            {
                if(req.length <= 0)
                {
                    NoContacts(req);
                    continue;
                }

                IntPtr geom = IntPtr.Zero;
                if (req.actor != null)
                {
                    if (m_scene.haveActor(req.actor))
                    {
                        if (req.actor is OdePrim)
                            geom = ((OdePrim)req.actor).m_prim_geom;
                        else if (req.actor is OdeCharacter)
                            geom = ((OdeCharacter)req.actor).collider;
                    }
                    if (geom == IntPtr.Zero)
                    {
                        NoContacts(req);
                        continue;
                    }
                }

                CurrentRayFilter = req.filter;
                CurrentMaxCount = req.Count;
                if (CurrentMaxCount > 25)
                    CurrentMaxCount = 25;

                unchecked
                {
                    CollisionContactGeomsPerTest = ((CurrentRayFilter & RayFilterFlags.ContactsUnImportant) != 0) ?
                        CurrentMaxCount | (int)SafeNativeMethods.CONTACTS_UNIMPORTANT : CurrentMaxCount;
                }

                int backfacecull = ((CurrentRayFilter & RayFilterFlags.BackFaceCull) == 0 ? 0 : 1);
                SafeNativeMethods.GeomRaySetParams(ray, 0, backfacecull);

                if (req.callbackMethod is RaycastCallback)
                {
                    // if we only want one get only one per Collision pair saving memory
                    CurrentRayFilter |= RayFilterFlags.ClosestHit;
                    SafeNativeMethods.GeomRaySetClosestHit(ray, 1);
                }
                else
                {
                    int closestHit = ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0 ? 0 : 1);
                    SafeNativeMethods.GeomRaySetClosestHit(ray, closestHit);
                }

                if (geom == IntPtr.Zero)
                {
                    // translate ray filter to Collision flags
                    CollisionCategories catflags = 0;
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

                    if (catflags != 0)
                    {
                        SafeNativeMethods.GeomSetCollideBits(ray, (uint)catflags);
                        doSpaceRay(req);
                    }
                }
                else
                {
                    // if we select a geom don't use filters
                    SafeNativeMethods.GeomSetCollideBits(ray, (uint)CollisionCategories.All);
                    doGeomRay(req,geom);
                }

                if (Util.EnvironmentTickCountSubtract(time) > MaxTimePerCallMS)
                    break;
            }

            lock (m_contactResults)
                m_contactResults.Clear();
        }
        /// <summary>
        /// Method that actually initiates the raycast with spaces
        /// </summary>
        /// <param name="req"></param>
        ///

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void NoContacts(ODERayRequest req)
        {
            if (req.callbackMethod is RaycastCallback)
            {
                ((RaycastCallback)req.callbackMethod)(false, Vector3.Zero, 0, 0, Vector3.Zero);
                return;
            }
            if (req.callbackMethod is RayCallback)
                ((RayCallback)req.callbackMethod)(new List<ContactResult>());
        }

        private const RayFilterFlags FilterActiveSpace = RayFilterFlags.physical | RayFilterFlags.LSLPhantom;
        private const RayFilterFlags FilterStaticSpace = RayFilterFlags.nonphysical | RayFilterFlags.LSLPhantom;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void doSpaceRay(ODERayRequest req)
        {
            /*
            float endx;
            float endy;

            if (req.Normal.X <= 0)
            {
                if (req.Origin.X <= 0)
                {
                    NoContacts(req);
                    return;
                }

                endx = req.Origin.X - req.length;
                if (endx < 0)
                {
                    req.length += endx;
                    endx = 0;
                }
            }
            else
            {
                if (req.Origin.X < 0)
                {
                    req.length += req.Origin.X;
                    if (req.length <= 0)
                    {
                        NoContacts(req);
                        return;
                    }
                    req.Origin.X = 0;
                }

                endx = req.Origin.X + req.length;
                if (endx < 0)
                {
                    req.length += endx;
                    endx = 0;
                }
            }
            */

            SafeNativeMethods.GeomRaySetLength(ray, req.length);
            SafeNativeMethods.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide tests
            if ((CurrentRayFilter & FilterActiveSpace) != 0)
                SafeNativeMethods.SpaceCollide2(ray, m_scene.ActiveSpace, IntPtr.Zero, nearCallback);

            if ((CurrentRayFilter & RayFilterFlags.agent) != 0)
            {
                foreach(OdeCharacter chr in m_scene._charactersList)
                {
                    if (m_contactResults.Count >= CurrentMaxCount)
                            break;
                    collideRayAvatar(chr);
                }
            }

            if ((CurrentRayFilter & FilterStaticSpace) != 0 && (m_contactResults.Count < CurrentMaxCount))
                SafeNativeMethods.SpaceCollide2(ray, m_scene.StaticSpace, IntPtr.Zero, nearCallback);

            if ((CurrentRayFilter & RayFilterFlags.land) != 0 && (m_contactResults.Count < CurrentMaxCount))
            {
                // current ode land to ray collisions is very bad
                // so for now limit its range badly
                if (req.length > 60.0f)
                {
                    Vector3 t = req.Normal * req.length;
                    float tmp = t.X * t.X + t.Y * t.Y;
                    if(tmp > 2500)
                    {
                        float tmp2 = req.length * req.length - tmp + 2500;
                        tmp2 = (float)Math.Sqrt(tmp2);
                        SafeNativeMethods.GeomRaySetLength(ray, tmp2);
                    }

                }
                collideRayTerrain(m_scene.TerrainGeom);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void doGeomRay(ODERayRequest req, IntPtr geom)
        {
            SafeNativeMethods.GeomRaySetLength(ray, req.length);
            SafeNativeMethods.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide test
            SafeNativeMethods.SpaceCollide2(ray, geom, IntPtr.Zero, nearCallback); // still do this to have full AABB pre test

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

        // This is the standard Near. g1 is the ray
        private void near(IntPtr dummy, IntPtr g1, IntPtr g2)
        {
            if (g2 == IntPtr.Zero)
                return;

            if (m_contactResults.Count >= CurrentMaxCount)
                return;

            if (SafeNativeMethods.GeomIsSpace(g2))
            {
                try
                {
                    SafeNativeMethods.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
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
                count = SafeNativeMethods.CollidePtr(g1, g2, CollisionContactGeomsPerTest, m_scene.ContactgeomsArray, SafeNativeMethods.ContactGeom.unmanagedSizeOf);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS Ray]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            if (count == 0)
                return;

            m_scene.actor_name_map.TryGetValue(g2, out PhysicsActor p2);

            if (p2 == null)
                return;

            uint ID = 0;
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

                default:
                    return;
//                    break;
            }

            // closestHit for now only works for meshs, so must do it for others
            if ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0)
            {
                // Loop all contacts, build results.
                for (int i = 0; i < count; i++)
                {
                    lock (m_contactResults)
                    {
                        m_contactResults.Add(new ContactResult
                        {
                            ConsumerID = ID,
                            Pos = new Vector3(m_contacts[i].pos.X, m_contacts[i].pos.Y, m_contacts[i].pos.Z),
                            Normal = new Vector3(m_contacts[i].normal.X, m_contacts[i].normal.Y, m_contacts[i].normal.Z),
                            Depth = m_contacts[i].depth
                        });
                        if (m_contactResults.Count >= CurrentMaxCount)
                            return;
                    }
                }
            }
            else
            {
                // keep only closest contact
                SharedCollisionResult.ConsumerID = ID;
                SharedCollisionResult.Depth = float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    if (m_contacts[i].depth < SharedCollisionResult.Depth)
                    {
                        SharedCollisionResult.Pos.X = m_contacts[i].pos.X;
                        SharedCollisionResult.Pos.Y = m_contacts[i].pos.Y;
                        SharedCollisionResult.Pos.Z = m_contacts[i].pos.Z;
                        SharedCollisionResult.Normal.X = m_contacts[i].normal.X;
                        SharedCollisionResult.Normal.Y = m_contacts[i].normal.Y;
                        SharedCollisionResult.Normal.Z = m_contacts[i].normal.Z;
                        SharedCollisionResult.Depth = m_contacts[i].depth;
                    }
                }

                if (SharedCollisionResult.Depth != float.MaxValue)
                {
                    lock (m_contactResults)
                        m_contactResults.Add(SharedCollisionResult);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void collideRayAvatar(OdeCharacter chr)
        {
            if (chr.collider == IntPtr.Zero)
                return;

            int count = 0;
            try
            {
                count = SafeNativeMethods.CollidePtr(ray, chr.collider, CollisionContactGeomsPerTest, m_scene.ContactgeomsArray, SafeNativeMethods.ContactGeom.unmanagedSizeOf);
                if (count == 0)
                    return;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS Ray]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            // closestHit for now only works for meshs, so must do it for others
            if ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0)
            {
                uint id = chr.LocalID;
                // Loop all contacts, build results.
                for (int i = 0; i < count; i++)
                {
                    lock (m_contactResults)
                    {
                        m_contactResults.Add(new ContactResult
                        {
                            ConsumerID = id,
                            Pos = new Vector3(m_contacts[i].pos.X, m_contacts[i].pos.Y, m_contacts[i].pos.Z),
                            Normal = new Vector3(m_contacts[i].normal.X, m_contacts[i].normal.Y, m_contacts[i].normal.Z),
                            Depth = m_contacts[i].depth
                        });
                        if (m_contactResults.Count >= CurrentMaxCount)
                            return;
                    }
                }
            }
            else
            {
                // keep only closest contact
                SharedCollisionResult.ConsumerID = chr.LocalID;
                SharedCollisionResult.Depth = float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    if (m_contacts[i].depth < SharedCollisionResult.Depth)
                    {
                        SharedCollisionResult.Pos.X = m_contacts[i].pos.X;
                        SharedCollisionResult.Pos.Y = m_contacts[i].pos.Y;
                        SharedCollisionResult.Pos.Z = m_contacts[i].pos.Z;
                        SharedCollisionResult.Normal.X = m_contacts[i].normal.X;
                        SharedCollisionResult.Normal.Y = m_contacts[i].normal.Y;
                        SharedCollisionResult.Normal.Z = m_contacts[i].normal.Z;
                        SharedCollisionResult.Depth = m_contacts[i].depth;
                    }
                }

                if (SharedCollisionResult.Depth != float.MaxValue)
                {
                    lock (m_contactResults)
                        m_contactResults.Add(SharedCollisionResult);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void collideRayTerrain(IntPtr terrain)
        {
            if (terrain == IntPtr.Zero)
                return;

            int count = 0;
            try
            {
                count = SafeNativeMethods.CollidePtr(ray, terrain, CollisionContactGeomsPerTest, m_scene.ContactgeomsArray, SafeNativeMethods.ContactGeom.unmanagedSizeOf);
                if (count == 0)
                    return;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS Ray]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            // closestHit for now only works for meshs, so must do it for others
            if ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0)
            {
                // Loop all contacts, build results.
                for (int i = 0; i < count; i++)
                {
                    lock (m_contactResults)
                    {
                        m_contactResults.Add(new ContactResult
                        {
                            ConsumerID = 0,
                            Pos = new Vector3(m_contacts[i].pos.X, m_contacts[i].pos.Y, m_contacts[i].pos.Z),
                            Normal = new Vector3(m_contacts[i].normal.X, m_contacts[i].normal.Y, m_contacts[i].normal.Z),
                            Depth = m_contacts[i].depth
                        });
                        if (m_contactResults.Count >= CurrentMaxCount)
                            return;
                    }
                }
            }
            else
            {
                // keep only closest contact
                SharedCollisionResult.ConsumerID = 0;
                SharedCollisionResult.Depth = float.MaxValue;

                for (int i = 0; i < count; i++)
                {
                    if (m_contacts[i].depth < SharedCollisionResult.Depth)
                    {
                        SharedCollisionResult.Pos.X = m_contacts[i].pos.X;
                        SharedCollisionResult.Pos.Y = m_contacts[i].pos.Y;
                        SharedCollisionResult.Pos.Z = m_contacts[i].pos.Z;
                        SharedCollisionResult.Normal.X = m_contacts[i].normal.X;
                        SharedCollisionResult.Normal.Y = m_contacts[i].normal.Y;
                        SharedCollisionResult.Normal.Z = m_contacts[i].normal.Z;
                        SharedCollisionResult.Depth = m_contacts[i].depth;
                    }
                }

                if (SharedCollisionResult.Depth != float.MaxValue)
                {
                    lock (m_contactResults)
                        m_contactResults.Add(SharedCollisionResult);
                }
            }
        }

        /// <summary>
        /// Dereference the creator scene so that it can be garbage collected if needed.
        /// </summary>
        internal void Dispose()
        {
            if (ray != IntPtr.Zero)
            {
                SafeNativeMethods.GeomDestroy(ray);
                ray = IntPtr.Zero;
            }
        }
    }


    public class ODERayRequest
    {
        public Vector3 Origin;
        public Vector3 Normal;
        public object callbackMethod;
        public PhysicsActor actor;
        public int Count;
        public float length;
        public RayFilterFlags filter;
    }
}
