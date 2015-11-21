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
using OpenMetaverse;
using OpenSim.Region.PhysicsModules.SharedBase;
using log4net;

namespace OpenSim.Region.PhysicsModule.ODE
{
    /// <summary>
    /// Processes raycast requests as ODE is in a state to be able to do them.
    /// This ensures that it's thread safe and there will be no conflicts.
    /// Requests get returned by a different thread then they were requested by.
    /// </summary>
    public class ODERayCastRequestManager
    {
        /// <summary>
        /// Pending raycast requests
        /// </summary>
        protected List<ODERayCastRequest> m_PendingRequests = new List<ODERayCastRequest>();

        /// <summary>
        /// Pending ray requests
        /// </summary>
        protected List<ODERayRequest> m_PendingRayRequests = new List<ODERayRequest>();

        /// <summary>
        /// Scene that created this object.
        /// </summary>
        private OdeScene m_scene;

        /// <summary>
        /// ODE contact array to be filled by the collision testing
        /// </summary>
        d.ContactGeom[] contacts = new d.ContactGeom[5];

        /// <summary>
        /// ODE near callback delegate
        /// </summary>
        private d.NearCallback nearCallback;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<ContactResult> m_contactResults = new List<ContactResult>();


        public ODERayCastRequestManager(OdeScene pScene)
        {
            m_scene = pScene;
            nearCallback = near;
            
        }

        /// <summary>
        /// Queues a raycast
        /// </summary>
        /// <param name="position">Origin of Ray</param>
        /// <param name="direction">Ray normal</param>
        /// <param name="length">Ray length</param>
        /// <param name="retMethod">Return method to send the results</param>
        public void QueueRequest(Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            lock (m_PendingRequests)
            {
                ODERayCastRequest req = new ODERayCastRequest();
                req.callbackMethod = retMethod;
                req.length = length;
                req.Normal = direction;
                req.Origin = position;

                m_PendingRequests.Add(req);
            }
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
            lock (m_PendingRequests)
            {
                ODERayRequest req = new ODERayRequest();
                req.callbackMethod = retMethod;
                req.length = length;
                req.Normal = direction;
                req.Origin = position;
                req.Count = count;

                m_PendingRayRequests.Add(req);
            }
        }

        /// <summary>
        /// Process all queued raycast requests
        /// </summary>
        /// <returns>Time in MS the raycasts took to process.</returns>
        public int ProcessQueuedRequests()
        {
            int time = System.Environment.TickCount;
            lock (m_PendingRequests)
            {
                if (m_PendingRequests.Count > 0)
                {
                    ODERayCastRequest[] reqs = m_PendingRequests.ToArray();
                    for (int i = 0; i < reqs.Length; i++)
                    {
                        if (reqs[i].callbackMethod != null) // quick optimization here, don't raycast 
                            RayCast(reqs[i]);               // if there isn't anyone to send results
                    }

                    m_PendingRequests.Clear();
                }
            }

            lock (m_PendingRayRequests)
            {
                if (m_PendingRayRequests.Count > 0)
                {
                    ODERayRequest[] reqs = m_PendingRayRequests.ToArray();
                    for (int i = 0; i < reqs.Length; i++)
                    {
                        if (reqs[i].callbackMethod != null) // quick optimization here, don't raycast 
                            RayCast(reqs[i]);               // if there isn't anyone to send results
                    }

                    m_PendingRayRequests.Clear();
                }
            }

            lock (m_contactResults)
                m_contactResults.Clear();

            return System.Environment.TickCount - time;
        }

        /// <summary>
        /// Method that actually initiates the raycast
        /// </summary>
        /// <param name="req"></param>
        private void RayCast(ODERayCastRequest req)
        {
            // NOTE: limit ray length or collisions will take all avaiable stack space
            // this value may still be too large, depending on machine configuration
            // of maximum stack
            float len = req.length;
            if (len > 100f)
                len = 100f;

            // Create the ray
            IntPtr ray = d.CreateRay(m_scene.space, len);
            d.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide test
            d.SpaceCollide2(m_scene.space, ray, IntPtr.Zero, nearCallback);

            // Remove Ray
            d.GeomDestroy(ray);

            // Define default results
            bool hitYN = false;
            uint hitConsumerID = 0;
            float distance = 999999999999f;
            Vector3 closestcontact = new Vector3(99999f, 99999f, 99999f);
            Vector3 snormal = Vector3.Zero;

            // Find closest contact and object.
            lock (m_contactResults)
            {
                foreach (ContactResult cResult in m_contactResults)
                {
                    if (Vector3.Distance(req.Origin, cResult.Pos) < Vector3.Distance(req.Origin, closestcontact))
                    {
                        closestcontact = cResult.Pos;
                        hitConsumerID = cResult.ConsumerID;
                        distance = cResult.Depth;
                        hitYN = true;
                        snormal = cResult.Normal;
                    }
                }

                m_contactResults.Clear();
            }

            // Return results
            if (req.callbackMethod != null)
                req.callbackMethod(hitYN, closestcontact, hitConsumerID, distance, snormal);
        }

        /// <summary>
        /// Method that actually initiates the raycast
        /// </summary>
        /// <param name="req"></param>
        private void RayCast(ODERayRequest req)
        {
            // limit ray length or collisions will take all avaiable stack space
            float len = req.length;
            if (len > 100f)
                len = 100f;

            // Create the ray
            IntPtr ray = d.CreateRay(m_scene.space, len);
            d.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide test
            d.SpaceCollide2(m_scene.space, ray, IntPtr.Zero, nearCallback);

            // Remove Ray
            d.GeomDestroy(ray);

            // Find closest contact and object.
            lock (m_contactResults)
            {
                // Return results
                if (req.callbackMethod != null)
                    req.callbackMethod(m_contactResults);
            }
        }
        
        // This is the standard Near.   Uses space AABBs to speed up detection.
        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {

            if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                return;
//            if (d.GeomGetClass(g1) == d.GeomClassID.HeightfieldClass || d.GeomGetClass(g2) == d.GeomClassID.HeightfieldClass)
//                return;

            // Raytest against AABBs of spaces first, then dig into the spaces it hits for actual geoms.
            if (d.GeomIsSpace(g1) || d.GeomIsSpace(g2))
            {
                if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                    return;
                
                // Separating static prim geometry spaces.
                // We'll be calling near recursivly if one
                // of them is a space to find all of the
                // contact points in the space
                try
                {
                    d.SpaceCollide2(g1, g2, IntPtr.Zero, nearCallback);
                }
                catch (AccessViolationException)
                {
                    m_log.Warn("[PHYSICS]: Unable to collide test a space");
                    return;
                }
                //Colliding a space or a geom with a space or a geom. so drill down

                //Collide all geoms in each space..
                //if (d.GeomIsSpace(g1)) d.SpaceCollide(g1, IntPtr.Zero, nearCallback);
                //if (d.GeomIsSpace(g2)) d.SpaceCollide(g2, IntPtr.Zero, nearCallback);
                return;
            }

            if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                return;

            int count = 0;
            try
            {
                
                if (g1 == g2)
                    return; // Can't collide with yourself

                lock (contacts)
                {
                    count = d.Collide(g1, g2, contacts.GetLength(0), contacts, d.ContactGeom.unmanagedSizeOf);
                }
            }
            catch (SEHException)
            {
                m_log.Error("[PHYSICS]: The Operating system shut down ODE because of corrupt memory.  This could be a result of really irregular terrain.  If this repeats continuously, restart using Basic Physics and terrain fill your terrain.  Restarting the sim.");
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            PhysicsActor p1 = null;
            PhysicsActor p2 = null;

            if (g1 != IntPtr.Zero)
                m_scene.actor_name_map.TryGetValue(g1, out p1);

            if (g2 != IntPtr.Zero)
                m_scene.actor_name_map.TryGetValue(g1, out p2);

            // Loop over contacts, build results.
            for (int i = 0; i < count; i++)
            {
                if (p1 != null)
                {
                    if (p1 is OdePrim)
                    {
                        ContactResult collisionresult = new ContactResult();
                    
                        collisionresult.ConsumerID = p1.LocalID;
                        collisionresult.Pos = new Vector3(contacts[i].pos.X, contacts[i].pos.Y, contacts[i].pos.Z);
                        collisionresult.Depth = contacts[i].depth;
                        collisionresult.Normal = new Vector3(contacts[i].normal.X, contacts[i].normal.Y,
                                                             contacts[i].normal.Z);
                        lock (m_contactResults)
                            m_contactResults.Add(collisionresult);
                    }
                }

                if (p2 != null)
                {
                    if (p2 is OdePrim)
                    {
                        ContactResult collisionresult = new ContactResult();

                        collisionresult.ConsumerID = p2.LocalID;
                        collisionresult.Pos = new Vector3(contacts[i].pos.X, contacts[i].pos.Y, contacts[i].pos.Z);
                        collisionresult.Depth = contacts[i].depth;
                        collisionresult.Normal = new Vector3(contacts[i].normal.X, contacts[i].normal.Y,
                                      contacts[i].normal.Z);

                        lock (m_contactResults)
                            m_contactResults.Add(collisionresult);
                    }
                }
            }
        }

        /// <summary>
        /// Dereference the creator scene so that it can be garbage collected if needed.
        /// </summary>
        internal void Dispose()
        {
            m_scene = null;
        }
    }

    public struct ODERayCastRequest
    {
        public Vector3 Origin;
        public Vector3 Normal;
        public float length;
        public RaycastCallback callbackMethod;
    }

    public struct ODERayRequest
    {
        public Vector3 Origin;
        public Vector3 Normal;
        public int Count;
        public float length;
        public RayCallback callbackMethod;
    }
}
