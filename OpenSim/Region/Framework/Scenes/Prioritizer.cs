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
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.Framework.Scenes
{
    public enum UpdatePrioritizationSchemes
    {
        SimpleAngularDistance = 0,
        BestAvatarResponsiveness = 1,
    }

    public class Prioritizer
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        public Prioritizer(Scene scene)
        {
            m_scene = scene;
        }

        /// <summary>
        /// Returns the priority queue into which the update should be placed.
        /// </summary>
        public uint GetUpdatePriority(IClientAPI client, ISceneEntity entity)
        {
            // If entity is null we have a serious problem
            if (entity == null)
            {
                m_log.WarnFormat("[PRIORITIZER] attempt to prioritize null entity");
                throw new InvalidOperationException("Prioritization entity not defined");
            }

            // If this is an update for our own avatar give it the highest priority
            if (client.AgentId == entity.UUID)
                return 0;

            uint priority;

            switch (m_scene.UpdatePrioritizationScheme)
            {
                case UpdatePrioritizationSchemes.SimpleAngularDistance:
                    priority = GetPriorityByAngularDistance(client, entity);
                    break;
                case UpdatePrioritizationSchemes.BestAvatarResponsiveness:
                default:
                    priority = GetPriorityByBestAvatarResponsiveness(client, entity);
                    break;
            }

            return priority;
        }

        private uint GetPriorityByBestAvatarResponsiveness(IClientAPI client, ISceneEntity entity)
        {
            uint pqueue = 2; // keep compiler happy

            ScenePresence presence = m_scene.GetScenePresence(client.AgentId);
            if (presence != null)
            {
                // All avatars other than our own go into pqueue 1
                if (entity is ScenePresence)
                    return 1;

                if (entity is SceneObjectPart)
                {
                    SceneObjectGroup sog = ((SceneObjectPart)entity).ParentGroup;
                    // Attachments are high priority,
                    if (sog.IsAttachment)
                        return 2;

                    if(presence.ParentPart != null)
                    {
                        if(presence.ParentPart.ParentGroup == sog)
                            return 2;
                    }
                    
                    pqueue = ComputeDistancePriority(client, entity, false);

                    // Non physical prims are lower priority than physical prims
                    PhysicsActor physActor = sog.RootPart.PhysActor;
                    if (physActor == null || !physActor.IsPhysical)
                        pqueue++;
                }
            }
            else
                pqueue = ComputeDistancePriority(client, entity, false);

            return pqueue;
        }

        private uint ComputeDistancePriority(IClientAPI client, ISceneEntity entity, bool useFrontBack)
        {
            // Get this agent's position
            ScenePresence presence = m_scene.GetScenePresence(client.AgentId);
            if (presence == null)
            {
                // this shouldn't happen, it basically means that we are prioritizing
                // updates to send to a client that doesn't have a presence in the scene
                // seems like there's race condition here...

                // m_log.WarnFormat("[PRIORITIZER] attempt to use agent {0} not in the scene",client.AgentId);
                // throw new InvalidOperationException("Prioritization agent not defined");
                return PriorityQueue.NumberOfQueues - 1;
            }

            // Use group position for child prims, since we are putting child prims in
            // the same queue with the root of the group, the root prim (which goes into
            // the queue first) should always be sent first, no need to adjust child prim
            // priorities
            Vector3 entityPos = entity.AbsolutePosition;
            if (entity is SceneObjectPart)
            {
                SceneObjectGroup group = (entity as SceneObjectPart).ParentGroup;
                entityPos = group.AbsolutePosition;
            }

            // Use the camera position for local agents and avatar position for remote agents
            // Why would I want that? They could be camming but I still see them at the
            // avatar position, so why should I update them as if they were at their
            // camera positions? Makes no sense!
            // TODO: Fix this mess
            //Vector3 presencePos = (presence.IsChildAgent) ?
            //    presence.AbsolutePosition :
            //    presence.CameraPosition;

            Vector3 presencePos = presence.AbsolutePosition;

            // Compute the distance...
            double distance = Vector3.Distance(presencePos, entityPos);

            // And convert the distance to a priority queue, this computation gives queues
            // at 10, 20, 40, 80, 160, 320, 640, and 1280m
            uint pqueue = PriorityQueue.NumberOfImmediateQueues + 1; // reserve attachments queue
            if (distance > 10f)
            {
                float tmp = (float)Math.Log((double)distance) * 1.442695f - 3.321928f;
                // for a map identical to original:
                // now
                // 1st constant is 1/(log(2)) (natural log) so we get log2(distance)
                // 2st constant makes it be log2(distance/10)
                pqueue += (uint)tmp;
            }

            // If this is a root agent, then determine front & back
            // Bump up the priority queue (drop the priority) for any objects behind the avatar
            if (useFrontBack && ! presence.IsChildAgent)
            {
                // Root agent, decrease priority for objects behind us
                Vector3 camPosition = presence.CameraPosition;
                Vector3 camAtAxis = presence.CameraAtAxis;

                // Plane equation
                float d = -Vector3.Dot(camPosition, camAtAxis);
                float p = Vector3.Dot(camAtAxis, entityPos) + d;
                if (p < 0.0f)
                    pqueue++;
            }

            return pqueue;
        }

        private uint GetPriorityByAngularDistance(IClientAPI client, ISceneEntity entity)
        {
            ScenePresence presence = m_scene.GetScenePresence(client.AgentId);
            if (presence == null)
                return PriorityQueue.NumberOfQueues - 1;

            uint pqueue = ComputeAngleDistancePriority(presence, entity);
            return pqueue;
        }

        private uint ComputeAngleDistancePriority(ScenePresence presence, ISceneEntity entity)
        {
            uint pqueue = PriorityQueue.NumberOfImmediateQueues;
            float distance;

            Vector3 presencePos = presence.AbsolutePosition;
            if(entity is ScenePresence)
            {
                ScenePresence sp = entity as ScenePresence;
                distance = Vector3.DistanceSquared(presencePos, sp.AbsolutePosition);
                if (distance > 400f)
                {
                    float tmp = (float)Math.Log(distance) * 0.7213475f - 4.321928f;
                    pqueue += (uint)tmp;
                }
                return pqueue;
            }

            SceneObjectPart sop = entity as SceneObjectPart;
            SceneObjectGroup group = sop.ParentGroup;
            if(presence.ParentPart != null)
            {
                if(presence.ParentPart.ParentGroup == group)
                    return pqueue;
            }

            if (group.IsAttachment)
            {
                if(group.RootPart.LocalId == presence.LocalId)
                    return pqueue;

                distance = Vector3.DistanceSquared(presencePos, group.AbsolutePosition);
                if (distance > 400f)
                {
                    float tmp = (float)Math.Log(distance) * 0.7213475f - 4.321928f;
                    pqueue += (uint)tmp;
                }
                return pqueue;
            }

            float bradius = group.GetBoundsRadius();
            Vector3 grppos = group.getCenterOffset();
            distance = Vector3.Distance(presencePos, grppos);
            distance -= bradius;
            if(distance < 0)
                return pqueue;

            distance *= group.getAreaFactor();
            if(group.IsAttachment)
                distance *= 0.5f;
            else if(group.UsesPhysics)
                distance *= 0.6f;
            else if(group.GetSittingAvatarsCount() > 0)
                distance *= 0.5f;

            if (distance > 10f)
            {
                float tmp = (float)Math.Log(distance) * 1.442695f - 3.321928f;
                // for a map identical to original:
                // now
                // 1st constant is 1/(log(2)) (natural log) so we get log2(distance)
                // 2st constant makes it be log2(distance/10)
                pqueue += (uint)tmp;
            }

            return pqueue;
        }
    }
}
