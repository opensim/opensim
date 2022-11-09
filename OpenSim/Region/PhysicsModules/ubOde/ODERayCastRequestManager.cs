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
using System.Collections.Concurrent;
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
        protected ConcurrentQueue<ODERayRequest> m_PendingRequests = new();

        /// <summary>
        /// Scene that created this object.
        /// </summary>
        private readonly ODEScene m_scene;
        private readonly UBOdeNative.ContactGeom[] m_contacts;

        IntPtr ray; // the ray. we only need one for our lifetime

        private int CollisionContactGeomsPerTest = 25;
        private const int ResultsMaxCount = 25;
        private const int DefaultResultsMaxCount = 25;
        private const int MaxTimePerCallMS = 30;

        /// <summary>
        /// ODE near callback delegate
        /// </summary>
        private readonly UBOdeNative.NearCallback nearCallback;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly List<ContactResult> m_contactResults = new(ResultsMaxCount);
        private readonly object m_contactResultsLock = new();
        private RayFilterFlags CurrentRayFilter;
        private int CurrentMaxCount;
        ContactResult SharedCollisionResult = new();

        public ODERayCastRequestManager(ODEScene pScene)
        {
            m_scene = pScene;
            m_contacts = pScene.m_contacts;
            nearCallback = near;
            ray = UBOdeNative.CreateRay(IntPtr.Zero, 1.0f);
            UBOdeNative.GeomSetCategoryBits(ray, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QueueRequest(ODERayRequest req)
        {
            if (req.Count == 0)
                req.Count = DefaultResultsMaxCount;

            m_PendingRequests.Enqueue(req);
        }

        /// <summary>
        /// Process all queued raycast requests
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessQueuedRequests()
        {
            if (m_PendingRequests.IsEmpty)
                return ;

            int time = Util.EnvironmentTickCount();
            while (m_PendingRequests.TryDequeue(out ODERayRequest req))
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
                        if (req.actor is OdePrim prim)
                            geom = prim.m_prim_geom;
                        else if (req.actor is OdeCharacter ch)
                            geom = ch.collider;
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
                        CurrentMaxCount | (int)UBOdeNative.CONTACTS_UNIMPORTANT : CurrentMaxCount;
                }

                int backfacecull = ((CurrentRayFilter & RayFilterFlags.BackFaceCull) == 0 ? 0 : 1);
                UBOdeNative.GeomRaySetParams(ray, 0, backfacecull);

                if (req.callbackMethod is RaycastCallback)
                {
                    // if we only want one get only one per Collision pair saving memory
                    CurrentRayFilter |= RayFilterFlags.ClosestHit;
                    UBOdeNative.GeomRaySetClosestHit(ray, 1);
                }
                else
                {
                    int closestHit = ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0 ? 0 : 1);
                    UBOdeNative.GeomRaySetClosestHit(ray, closestHit);
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
                        UBOdeNative.GeomSetCollideBits(ray, (uint)catflags);
                        doSpaceRay(req);
                    }
                }
                else
                {
                    // if we select a geom don't use filters
                    UBOdeNative.GeomSetCollideBits(ray, (uint)CollisionCategories.All);
                    doGeomRay(req,geom);
                }

                if (Util.EnvironmentTickCountSubtract(time) > MaxTimePerCallMS)
                    break;
            }

            lock (m_contactResultsLock)
                m_contactResults.Clear();
        }
        /// <summary>
        /// Method that actually initiates the raycast with spaces
        /// </summary>
        /// <param name="req"></param>
        ///

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void NoContacts(ODERayRequest req)
        {
            if (req.callbackMethod is RaycastCallback callback)
                callback(false, Vector3.Zero, 0, 0, Vector3.Zero);
 
            else if (req.callbackMethod is RayCallback raycallback)
                raycallback(new List<ContactResult>());
        }

        private const RayFilterFlags FilterActiveSpace = RayFilterFlags.physical | RayFilterFlags.LSLPhantom;
        private const RayFilterFlags FilterStaticSpace = RayFilterFlags.nonphysical | RayFilterFlags.LSLPhantom;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void doSpaceRay(ODERayRequest req)
        {
            UBOdeNative.GeomRaySetLength(ray, req.length);
            UBOdeNative.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide tests
            if ((CurrentRayFilter & FilterActiveSpace) != 0)
                UBOdeNative.SpaceCollide2(ray, m_scene.ActiveSpace, IntPtr.Zero, nearCallback);

            if ((CurrentRayFilter & RayFilterFlags.agent) != 0)
            {
                foreach(OdeCharacter chr in CollectionsMarshal.AsSpan(m_scene._charactersList))
                {
                    if (m_contactResults.Count >= CurrentMaxCount)
                            break;
 
                    if(m_scene.CollideRaySimpleCapsule(chr, req.Origin, req.Normal, req.length, ref SharedCollisionResult))
                    {
                        SharedCollisionResult.ConsumerID = chr.m_baseLocalID;
                        m_contactResults.Add(SharedCollisionResult);
                    }
                }
            }

            if ((CurrentRayFilter & FilterStaticSpace) != 0 && (m_contactResults.Count < CurrentMaxCount))
                UBOdeNative.SpaceCollide2(ray, m_scene.StaticSpace, IntPtr.Zero, nearCallback);

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
                        UBOdeNative.GeomRaySetLength(ray, tmp2);
                    }
                }
                CollideRayTerrain(m_scene.TerrainGeom);
            }

            if (req.callbackMethod is RaycastCallback callback)
            {
                // Define default results
                bool hitYN = false;
                uint hitConsumerID = 0;
                float distance = float.MaxValue;
                Vector3 closestcontact = Vector3.Zero;
                Vector3 snormal = Vector3.Zero;

                // Find closest contact and object.
                lock (m_contactResultsLock)
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
                callback(hitYN, closestcontact, hitConsumerID, distance, snormal);
            }
            else
            {
                List<ContactResult> cresult = new(m_contactResults.Count);
                lock (m_contactResultsLock)
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
            UBOdeNative.GeomRaySetLength(ray, req.length);
            UBOdeNative.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide test
            UBOdeNative.SpaceCollide2(ray, geom, IntPtr.Zero, nearCallback); // still do this to have full AABB pre test

            if (req.callbackMethod is RaycastCallback callback)
            {
                // Define default results
                bool hitYN = false;
                uint hitConsumerID = 0;
                float distance = float.MaxValue;
                Vector3 closestcontact = Vector3.Zero;
                Vector3 snormal = Vector3.Zero;

                // Find closest contact and object.
                lock (m_contactResultsLock)
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

                callback(hitYN, closestcontact, hitConsumerID, distance, snormal);
            }
            else
            {
                List<ContactResult> cresult = new(m_contactResults.Count);
                lock (m_contactResultsLock)
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

            if (UBOdeNative.GeomIsSpace(g2))
            {
                try
                {
                    UBOdeNative.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[PHYSICS Ray]: Unable to Space collide test an object: {0}", e.Message);
                }
                return;
            }

            int count;
            try
            {
                count = UBOdeNative.CollidePtr(g1, g2, CollisionContactGeomsPerTest, m_scene.ContactgeomsArray, UBOdeNative.SizeOfContactGeom);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS Ray]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            if (count == 0)
                return;

            m_scene.actor_name_map.TryGetValue(g2, out PhysicsActor p2);

            if (p2 is null || p2.PhysicsActorType != (int)ActorTypes.Prim)
                return;

            RayFilterFlags thisFlags = p2.IsPhysical ? RayFilterFlags.physical : RayFilterFlags.nonphysical;

            if (p2.Phantom)
                thisFlags |= RayFilterFlags.phantom;

            if (p2.IsVolumeDtc)
                thisFlags |= RayFilterFlags.volumedtc;

            if ((thisFlags & CurrentRayFilter) == 0)
                return;

            uint ID = ((OdePrim)p2).LocalID;

            // closestHit for now only works for meshs, so must do it for others
            if ((CurrentRayFilter & RayFilterFlags.ClosestHit) == 0)
            {
                // Loop all contacts, build results.
                for (int i = 0; i < count; ++i)
                {
                    ref UBOdeNative.ContactGeom curCtg = ref m_contacts[i];
                    lock (m_contactResultsLock)
                    {
                        m_contactResults.Add(new ContactResult
                        {
                            ConsumerID = ID,
                            Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos),
                            Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal),
                            Depth = curCtg.depth
                        });
                        if (m_contactResults.Count >= CurrentMaxCount)
                            return;
                    }
                }
            }
            else
            {
                // keep only closest contact
                ref UBOdeNative.ContactGeom curCtg = ref m_contacts[0];
                SharedCollisionResult.Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos);
                SharedCollisionResult.Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal);
                float depth = curCtg.depth;
                SharedCollisionResult.Depth = depth;

                for (int i = 1; i < count; ++i)
                {
                    curCtg = ref m_contacts[i];
                    if (curCtg.depth < depth)
                    {
                        SharedCollisionResult.Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos);
                        SharedCollisionResult.Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal);
                        depth = curCtg.depth;
                        SharedCollisionResult.Depth = depth;
                    }
                }

                SharedCollisionResult.ConsumerID = ID;
                lock (m_contactResultsLock)
                    m_contactResults.Add(SharedCollisionResult);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CollideRayAvatar(OdeCharacter chr)
        {
            int count;
            try
            {
                count = UBOdeNative.CollidePtr(ray, chr.collider, CollisionContactGeomsPerTest, m_scene.ContactgeomsArray, UBOdeNative.SizeOfContactGeom);
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
                    ref UBOdeNative.ContactGeom curCtg = ref m_contacts[i];
                    lock (m_contactResultsLock)
                    {
                        m_contactResults.Add(new ContactResult
                        {
                            ConsumerID = id,
                            Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos),
                            Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal),
                            Depth = curCtg.depth
                        });
                        if (m_contactResults.Count >= CurrentMaxCount)
                            return;
                    }
                }
            }
            else
            {
                // keep only closest contact
                ref UBOdeNative.ContactGeom curCtg = ref m_contacts[0];
                SharedCollisionResult.Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos);
                SharedCollisionResult.Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal);
                float depth = curCtg.depth;
                SharedCollisionResult.Depth = depth;

                for (int i = 1; i < count; ++i)
                {
                    curCtg = ref m_contacts[i];
                    if (curCtg.depth < depth)
                    {
                        SharedCollisionResult.Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos);
                        SharedCollisionResult.Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal);
                        depth = curCtg.depth;
                        SharedCollisionResult.Depth = depth;
                    }
                }

                SharedCollisionResult.ConsumerID = chr.LocalID;
                lock (m_contactResultsLock)
                    m_contactResults.Add(SharedCollisionResult);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CollideRayTerrain(IntPtr terrain)
        {
            if (terrain == IntPtr.Zero)
                return;

            int count;
            try
            {
                count = UBOdeNative.CollidePtr(ray, terrain, CollisionContactGeomsPerTest, m_scene.ContactgeomsArray, UBOdeNative.SizeOfContactGeom);
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
                    ref UBOdeNative.ContactGeom curCtg = ref m_contacts[i];
                    lock (m_contactResultsLock)
                    {
                        m_contactResults.Add(new ContactResult
                        {
                            ConsumerID = 0,
                            Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos),
                            Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal),
                            Depth = curCtg.depth
                        });
                        if (m_contactResults.Count >= CurrentMaxCount)
                            return;
                    }
                }
            }
            else
            {
                // keep only closest contact
                ref UBOdeNative.ContactGeom curCtg = ref m_contacts[0];
                SharedCollisionResult.Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos);
                SharedCollisionResult.Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal);
                float depth = curCtg.depth;
                SharedCollisionResult.Depth = depth;

                for (int i = 1; i < count; ++i)
                {
                    curCtg = ref m_contacts[i];
                    if (curCtg.depth < depth)
                    {
                        SharedCollisionResult.Pos = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.pos);
                        SharedCollisionResult.Normal = Unsafe.As<UBOdeNative.Vector3, Vector3>(ref curCtg.normal);
                        depth = curCtg.depth;
                        SharedCollisionResult.Depth = depth;
                    }
                }

                SharedCollisionResult.ConsumerID = 0;
                lock (m_contactResultsLock)
                    m_contactResults.Add(SharedCollisionResult);
            }
        }

        /// <summary>
        /// Dereference the creator scene so that it can be garbage collected if needed.
        /// </summary>
        internal void Dispose()
        {
            if (ray != IntPtr.Zero)
            {
                UBOdeNative.GeomDestroy(ray);
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
