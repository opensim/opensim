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
using OpenSim.Region.Physics.Manager;

/*
 * Steps to add a new prioritization policy:
 * 
 *  - Add a new value to the UpdatePrioritizationSchemes enum.
 *  - Specify this new value in the [InterestManagement] section of your
 *    OpenSim.ini. The name in the config file must match the enum value name
 *    (although it is not case sensitive).
 *  - Write a new GetPriorityBy*() method in this class.
 *  - Add a new entry to the switch statement in GetUpdatePriority() that calls
 *    your method.
 */

namespace OpenSim.Region.Framework.Scenes
{
    public enum UpdatePrioritizationSchemes
    {
        Time = 0,
        Distance = 1,
        SimpleAngularDistance = 2,
        FrontBack = 3,
        BestAvatarResponsiveness = 4,
    }

    public class Prioritizer
    {
//        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        /// <summary>
        /// This is added to the priority of all child prims, to make sure that the root prim update is sent to the
        /// viewer before child prim updates.
        /// The adjustment is added to child prims and subtracted from root prims, so the gap ends up
        /// being double.  We do it both ways so that there is a still a priority delta even if the priority is already
        /// double.MinValue or double.MaxValue.
        /// </summary>
        private double m_childPrimAdjustmentFactor = 0.05;

        private Scene m_scene;

        public Prioritizer(Scene scene)
        {
            m_scene = scene;
        }

        public double GetUpdatePriority(IClientAPI client, ISceneEntity entity)
        {
            double priority = 0;

            if (entity == null)
                return 100000;
            
            switch (m_scene.UpdatePrioritizationScheme)
            {
                case UpdatePrioritizationSchemes.Time:
                    priority = GetPriorityByTime();
                    break;
                case UpdatePrioritizationSchemes.Distance:
                    priority = GetPriorityByDistance(client, entity);
                    break;
                case UpdatePrioritizationSchemes.SimpleAngularDistance:
                    priority = GetPriorityByDistance(client, entity); // TODO: Reimplement SimpleAngularDistance
                    break;
                case UpdatePrioritizationSchemes.FrontBack:
                    priority = GetPriorityByFrontBack(client, entity);
                    break;
                case UpdatePrioritizationSchemes.BestAvatarResponsiveness:
                    priority = GetPriorityByBestAvatarResponsiveness(client, entity);
                    break;
                default:
                    throw new InvalidOperationException("UpdatePrioritizationScheme not defined.");
            }
            
            // Adjust priority so that root prims are sent to the viewer first.  This is especially important for 
            // attachments acting as huds, since current viewers fail to display hud child prims if their updates
            // arrive before the root one.
            if (entity is SceneObjectPart)
            {
                SceneObjectPart sop = ((SceneObjectPart)entity);
                
                if (sop.IsRoot)
                {
                    if (priority >= double.MinValue + m_childPrimAdjustmentFactor)
                        priority -= m_childPrimAdjustmentFactor;
                }
                else
                {
                    if (priority <= double.MaxValue - m_childPrimAdjustmentFactor)
                        priority += m_childPrimAdjustmentFactor;
                }
            }
            
            return priority;
        }

        private double GetPriorityByTime()
        {
            return DateTime.UtcNow.ToOADate();
        }

        private double GetPriorityByDistance(IClientAPI client, ISceneEntity entity)
        {
            ScenePresence presence = m_scene.GetScenePresence(client.AgentId);
            if (presence != null)
            {
                // If this is an update for our own avatar give it the highest priority
                if (presence == entity)
                    return 0.0;

                // Use the camera position for local agents and avatar position for remote agents
                Vector3 presencePos = (presence.IsChildAgent) ?
                    presence.AbsolutePosition :
                    presence.CameraPosition;

                // Use group position for child prims
                Vector3 entityPos;
                if (entity is SceneObjectPart)
                {
                    // Can't use Scene.GetGroupByPrim() here, since the entity may have been delete from the scene
                    // before its scheduled update was triggered
                    //entityPos = m_scene.GetGroupByPrim(entity.LocalId).AbsolutePosition;
                    entityPos = ((SceneObjectPart)entity).ParentGroup.AbsolutePosition;
                }
                else
                {
                    entityPos = entity.AbsolutePosition;
                }

                return Vector3.DistanceSquared(presencePos, entityPos);
            }

            return double.NaN;
        }

        private double GetPriorityByFrontBack(IClientAPI client, ISceneEntity entity)
        {
            ScenePresence presence = m_scene.GetScenePresence(client.AgentId);
            if (presence != null)
            {
                // If this is an update for our own avatar give it the highest priority
                if (presence == entity)
                    return 0.0;

                // Use group position for child prims
                Vector3 entityPos = entity.AbsolutePosition;
                if (entity is SceneObjectPart)
                {
                    // Can't use Scene.GetGroupByPrim() here, since the entity may have been delete from the scene
                    // before its scheduled update was triggered
                    //entityPos = m_scene.GetGroupByPrim(entity.LocalId).AbsolutePosition;
                    entityPos = ((SceneObjectPart)entity).ParentGroup.AbsolutePosition;
                }
                else
                {
                    entityPos = entity.AbsolutePosition;
                }

                if (!presence.IsChildAgent)
                {
                    // Root agent. Use distance from camera and a priority decrease for objects behind us
                    Vector3 camPosition = presence.CameraPosition;
                    Vector3 camAtAxis = presence.CameraAtAxis;

                    // Distance
                    double priority = Vector3.DistanceSquared(camPosition, entityPos);

                    // Plane equation
                    float d = -Vector3.Dot(camPosition, camAtAxis);
                    float p = Vector3.Dot(camAtAxis, entityPos) + d;
                    if (p < 0.0f) priority *= 2.0;

                    return priority;
                }
                else
                {
                    // Child agent. Use the normal distance method
                    Vector3 presencePos = presence.AbsolutePosition;

                    return Vector3.DistanceSquared(presencePos, entityPos);
                }
            }

            return double.NaN;
        }

        private double GetPriorityByBestAvatarResponsiveness(IClientAPI client, ISceneEntity entity)
        {
            // If this is an update for our own avatar give it the highest priority
            if (client.AgentId == entity.UUID)
                return 0.0;
            if (entity == null)
                return double.NaN;

            // Use group position for child prims
            Vector3 entityPos = entity.AbsolutePosition;
            if (entity is SceneObjectPart)
            {
                SceneObjectGroup group = (entity as SceneObjectPart).ParentGroup;
                if (group != null)
                    entityPos = group.AbsolutePosition;
                else
                    entityPos = entity.AbsolutePosition;
            }
            else
                entityPos = entity.AbsolutePosition;

            ScenePresence presence = m_scene.GetScenePresence(client.AgentId);
            if (presence != null)
            {
                if (!presence.IsChildAgent)
                {
                    if (entity is ScenePresence)
                        return 1.0;

                    // Root agent. Use distance from camera and a priority decrease for objects behind us
                    Vector3 camPosition = presence.CameraPosition;
                    Vector3 camAtAxis = presence.CameraAtAxis;

                    // Distance
                    double priority = Vector3.DistanceSquared(camPosition, entityPos);

                    // Plane equation
                    float d = -Vector3.Dot(camPosition, camAtAxis);
                    float p = Vector3.Dot(camAtAxis, entityPos) + d;
                    if (p < 0.0f) priority *= 2.0;

                    if (entity is SceneObjectPart)
                    {
                        PhysicsActor physActor = ((SceneObjectPart)entity).ParentGroup.RootPart.PhysActor;
                        if (physActor == null || !physActor.IsPhysical)
                            priority += 100;

                        if (((SceneObjectPart)entity).ParentGroup.RootPart.IsAttachment)
                            priority = 1.0;
                    }
                    return priority;
                }
                else
                {
                    // Child agent. Use the normal distance method
                    Vector3 presencePos = presence.AbsolutePosition;

                    return Vector3.DistanceSquared(presencePos, entityPos);
                }
            }

            return double.NaN;
        }
    }
}
