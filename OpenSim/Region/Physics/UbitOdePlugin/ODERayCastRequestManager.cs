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
using OpenSim.Region.Physics.Manager;
using OdeAPI;
using log4net;

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

        IntPtr ray;

        private const int ColisionContactGeomsPerTest = 5;

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
            ray = d.CreateRay(IntPtr.Zero, 1.0f);
        }

        /// <summary>
        /// Queues a raycast
        /// </summary>
        /// <param name="position">Origin of Ray</param>
        /// <param name="direction">Ray normal</param>
        /// <param name="length">Ray length</param>
        /// <param name="retMethod">Return method to send the results</param>
        public void QueueRequest(Vector3 position, Vector3 direction, float length, RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.Count = 0;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(IntPtr geom, Vector3 position, Vector3 direction, float length, RayCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = geom;
            req.callbackMethod = retMethod;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;
            req.Count = 0;

            m_PendingRequests.Enqueue(req);
        }

        public void QueueRequest(Vector3 position, Vector3 direction, float length, RaycastCallback retMethod)
        {
            ODERayRequest req = new ODERayRequest();
            req.geom = IntPtr.Zero;
            req.callbackMethod = retMethod;
            req.Count = 0;
            req.length = length;
            req.Normal = direction;
            req.Origin = position;

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
            req.Count = 0;

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

            m_PendingRequests.Enqueue(req);
        }

        /// <summary>
        /// Process all queued raycast requests
        /// </summary>
        /// <returns>Time in MS the raycasts took to process.</returns>
        public int ProcessQueuedRequests()
        {
            int time = System.Environment.TickCount;

            if (m_PendingRequests.Count <= 0)
                return 0;

            if (m_scene.ContactgeomsArray == IntPtr.Zero) // oops something got wrong or scene isn't ready still
            {
                m_PendingRequests.Clear();
                return 0;
            }

            ODERayRequest req;

            int i = 50; // arbitary limit of processed tests per frame

            while(m_PendingRequests.Dequeue(out req))
            {
                if (req.geom == IntPtr.Zero)
                    doSpaceRay(req);
                else
                    doGeomRay(req);
            if(--i < 0)
                break;
            }

            lock (m_contactResults)
                m_contactResults.Clear();

            return System.Environment.TickCount - time;
        }
        /// <summary>
        /// Method that actually initiates the raycast with full top space
        /// </summary>
        /// <param name="req"></param>
        private void doSpaceRay(ODERayRequest req)
        {
            // Create the ray
//            IntPtr ray = d.CreateRay(m_scene.TopSpace, req.length);
            d.GeomRaySetLength(ray, req.length);
            d.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide test
            d.SpaceCollide2(m_scene.TopSpace, ray, IntPtr.Zero, nearCallback);

            // Remove Ray
//            d.GeomDestroy(ray);

            if (req.callbackMethod == null)
                return;

            if (req.callbackMethod is RaycastCallback)
            {
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
               
                ((RaycastCallback)req.callbackMethod)(hitYN, closestcontact, hitConsumerID, distance, snormal);
            }
            else
            {
                ((RayCallback)req.callbackMethod)(m_contactResults);
                lock (m_PendingRequests)
                    m_contactResults.Clear();
            }
        }

        /// <summary>
        /// Method that actually initiates the raycast with a geom
        /// </summary>
        /// <param name="req"></param>
        private void doGeomRay(ODERayRequest req)
        {
            // Create the ray
//            IntPtr ray = d.CreateRay(m_scene.TopSpace, req.length);
            d.GeomRaySetLength(ray, req.length);
            d.GeomRaySet(ray, req.Origin.X, req.Origin.Y, req.Origin.Z, req.Normal.X, req.Normal.Y, req.Normal.Z);

            // Collide test
            d.SpaceCollide2(req.geom, ray, IntPtr.Zero, nearCallback); // still do this to have full AABB pre test

            // Remove Ray
//            d.GeomDestroy(ray);

            if (req.callbackMethod == null)
                return;

            if (req.callbackMethod is RaycastCallback)
            {
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

                ((RaycastCallback)req.callbackMethod)(hitYN, closestcontact, hitConsumerID, distance, snormal);
            }
            else
            {
                ((RayCallback)req.callbackMethod)(m_contactResults);
                lock (m_PendingRequests)
                    m_contactResults.Clear();
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
        
        // This is the standard Near. g2 is the ray
        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {
            //Don't test against heightfield Geom, or you'll be sorry!
            // Exclude heightfield geom

            if (g1 == IntPtr.Zero || g1 == g2)
                return;

            if (d.GeomGetClass(g1) == d.GeomClassID.HeightfieldClass)
                return;

            // Raytest against AABBs of spaces first, then dig into the spaces it hits for actual geoms.
            if (d.GeomIsSpace(g1))
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
            catch (SEHException)
            {
                m_log.Error("[PHYSICS Ray]: The Operating system shut down ODE because of corrupt memory.  This could be a result of really irregular terrain.  If this repeats continuously, restart using Basic Physics and terrain fill your terrain.  Restarting the sim.");
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[PHYSICS Ray]: Unable to collide test an object: {0}", e.Message);
                return;
            }

            if (count == 0)
                return;

            PhysicsActor p1 = null;

            if (g1 != IntPtr.Zero)
                m_scene.actor_name_map.TryGetValue(g1, out p1);

            d.ContactGeom curcontact = new d.ContactGeom();
            // Loop over contacts, build results.
            for (int i = 0; i < count; i++)
            {
                if (!GetCurContactGeom(i, ref curcontact))
                    break;
                if (p1 != null) { 
                    if (p1 is OdePrim)
                    {
                        ContactResult collisionresult = new ContactResult();
                    
                        collisionresult.ConsumerID = ((OdePrim)p1).m_localID;
                        collisionresult.Pos = new Vector3(curcontact.pos.X, curcontact.pos.Y, curcontact.pos.Z);
                        collisionresult.Depth = curcontact.depth;
                        collisionresult.Normal = new Vector3(curcontact.normal.X, curcontact.normal.Y,
                                                             curcontact.normal.Z);
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

    public struct ODERayRequest
    {
        public IntPtr geom;
        public Vector3 Origin;
        public Vector3 Normal;
        public int Count;
        public float length;
        public object callbackMethod;
    }
}