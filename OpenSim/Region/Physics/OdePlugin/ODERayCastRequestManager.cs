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
using Ode.NET;
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
        /// Pending Raycast Requests
        /// </summary>
        protected List<ODERayCastRequest> m_PendingRequests = new List<ODERayCastRequest>();

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
                    foreach (ODERayCastRequest req in m_PendingRequests)
                    {
                        if (req.callbackMethod != null) // quick optimization here, don't raycast 
                            RayCast(req);               // if there isn't anyone to send results to
                            
                    }

                    m_PendingRequests.Clear();
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
            // Create the ray
            IntPtr ray = d.CreateRay(m_scene.space, req.length);
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
                    }
                }

                m_contactResults.Clear();
            }

            // Return results
            if (req.callbackMethod != null)
                req.callbackMethod(hitYN, closestcontact, hitConsumerID, distance);
        }

        // This is the standard Near.   Uses space AABBs to speed up detection.
        private void near(IntPtr space, IntPtr g1, IntPtr g2)
        {

            //Don't test against heightfield Geom, or you'll be sorry!
            
            /*
             terminate called after throwing an instance of 'std::bad_alloc'
                  what():  std::bad_alloc
                Stacktrace:
                 
                  at (wrapper managed-to-native) Ode.NET.d.Collide (intptr,intptr,int,Ode.NET.d/ContactGeom[],int) <0x00004>
                  at (wrapper managed-to-native) Ode.NET.d.Collide (intptr,intptr,int,Ode.NET.d/ContactGeom[],int) <0xffffffff>
                  at OpenSim.Region.Physics.OdePlugin.ODERayCastRequestManager.near (intptr,intptr,intptr) <0x00280>
                  at (wrapper native-to-managed) OpenSim.Region.Physics.OdePlugin.ODERayCastRequestManager.near (intptr,intptr,intptr) <0xfff
                fffff>
                  at (wrapper managed-to-native) Ode.NET.d.SpaceCollide2 (intptr,intptr,intptr,Ode.NET.d/NearCallback) <0x00004>
                  at (wrapper managed-to-native) Ode.NET.d.SpaceCollide2 (intptr,intptr,intptr,Ode.NET.d/NearCallback) <0xffffffff>
                  at OpenSim.Region.Physics.OdePlugin.ODERayCastRequestManager.RayCast (OpenSim.Region.Physics.OdePlugin.ODERayCastRequest) <
                0x00114>
                  at OpenSim.Region.Physics.OdePlugin.ODERayCastRequestManager.ProcessQueuedRequests () <0x000eb>
                  at OpenSim.Region.Physics.OdePlugin.OdeScene.Simulate (single) <0x017e6>
                  at OpenSim.Region.Framework.Scenes.SceneGraph.UpdatePhysics (double) <0x00042>
                  at OpenSim.Region.Framework.Scenes.Scene.Update () <0x0039e>
                  at OpenSim.Region.Framework.Scenes.Scene.Heartbeat (object) <0x00019>
                  at (wrapper runtime-invoke) object.runtime_invoke_void__this___object (object,intptr,intptr,intptr) <0xffffffff>
                 
                Native stacktrace:
                 
                        mono [0x80d2a42]
                        [0xb7f5840c]
                        /lib/i686/cmov/libc.so.6(abort+0x188) [0xb7d1a018]
                        /usr/lib/libstdc++.so.6(_ZN9__gnu_cxx27__verbose_terminate_handlerEv+0x158) [0xb45fc988]
                        /usr/lib/libstdc++.so.6 [0xb45fa865]
                        /usr/lib/libstdc++.so.6 [0xb45fa8a2]
                        /usr/lib/libstdc++.so.6 [0xb45fa9da]
                        /usr/lib/libstdc++.so.6(_Znwj+0x83) [0xb45fb033]
                        /usr/lib/libstdc++.so.6(_Znaj+0x1d) [0xb45fb11d]
                        libode.so(_ZN13dxHeightfield23dCollideHeightfieldZoneEiiiiP6dxGeomiiP12dContactGeomi+0xd04) [0xb46678e4]
                        libode.so(_Z19dCollideHeightfieldP6dxGeomS0_iP12dContactGeomi+0x54b) [0xb466832b]
                        libode.so(dCollide+0x102) [0xb46571b2]
                        [0x95cfdec9]
                        [0x8ea07fe1]
                        [0xab260146]
                        libode.so [0xb465a5c4]
                        libode.so(_ZN11dxHashSpace8collide2EPvP6dxGeomPFvS0_S2_S2_E+0x75) [0xb465bcf5]
                        libode.so(dSpaceCollide2+0x177) [0xb465ac67]
                        [0x95cf978e]
                        [0x8ea07945]
                        [0x95cf2bbc]
                        [0xab2787e7]
                        [0xab419fb3]
                        [0xab416657]
                        [0xab415bda]
                        [0xb609b08e]
                        mono(mono_runtime_delegate_invoke+0x34) [0x8192534]
                        mono [0x81a2f0f]
                        mono [0x81d28b6]
                        mono [0x81ea2c6]
                        /lib/i686/cmov/libpthread.so.0 [0xb7e744c0]
                        /lib/i686/cmov/libc.so.6(clone+0x5e) [0xb7dcd6de]
             */

            // Exclude heightfield geom

            if (g1 == IntPtr.Zero || g2 == IntPtr.Zero)
                return;
            if (d.GeomGetClass(g1) == d.GeomClassID.HeightfieldClass || d.GeomGetClass(g2) == d.GeomClassID.HeightfieldClass)
                return;

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
                    count = d.Collide(g1, g2, contacts.GetLength(0), contacts, d.ContactGeom.SizeOf);
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
                if (p1 != null) { 
                    if (p1 is OdePrim)
                    {
                        ContactResult collisionresult = new ContactResult();
                    
                        collisionresult.ConsumerID = ((OdePrim)p1).m_localID;
                        collisionresult.Pos = new Vector3(contacts[i].pos.X, contacts[i].pos.Y, contacts[i].pos.Z);
                        collisionresult.Depth = contacts[i].depth;
                        
                        lock (m_contactResults)
                            m_contactResults.Add(collisionresult);
                    }
                }

                if (p2 != null)
                {
                    if (p2 is OdePrim)
                    {
                        ContactResult collisionresult = new ContactResult();

                        collisionresult.ConsumerID = ((OdePrim)p2).m_localID;
                        collisionresult.Pos = new Vector3(contacts[i].pos.X, contacts[i].pos.Y, contacts[i].pos.Z);
                        collisionresult.Depth = contacts[i].depth;

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

    public struct ContactResult
    {
        public Vector3 Pos;
        public float Depth;
        public uint ConsumerID;
    }
}
