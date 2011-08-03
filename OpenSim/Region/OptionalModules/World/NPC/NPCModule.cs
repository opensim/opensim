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
using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework;
using Timer=System.Timers.Timer;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.OptionalModules.World.NPC
{
    public class NPCModule : IRegionModule, INPCModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, NPCAvatar> m_avatars = new Dictionary<UUID, NPCAvatar>();
        private Dictionary<UUID, AvatarAppearance> m_appearanceCache = new Dictionary<UUID, AvatarAppearance>();

        public void Initialise(Scene scene, IConfigSource source)
        {
            IConfig config = source.Configs["NPC"];

            if (config != null && config.GetBoolean("Enabled", false))
            {
                scene.RegisterModuleInterface<INPCModule>(this);
                scene.EventManager.OnSignificantClientMovement += HandleOnSignificantClientMovement;
            }
        }

        public void HandleOnSignificantClientMovement(ScenePresence presence)
        {
            lock (m_avatars)
            {
                if (m_avatars.ContainsKey(presence.UUID))
                {
                    double distanceToTarget = Util.GetDistanceTo(presence.AbsolutePosition, presence.MoveToPositionTarget);
//                            m_log.DebugFormat(
//                                "[NPC MODULE]: Abs pos of {0} is {1}, target {2}, distance {3}",
//                                presence.Name, presence.AbsolutePosition, presence.MoveToPositionTarget, distanceToTarget);

                    // Check the error term of the current position in relation to the target position
                    if (distanceToTarget <= 1)
                    {
//                        m_log.DebugFormat("[NPC MODULE]: Stopping movement of npc {0} {1}", presence.Name, presence.UUID);
                        // We are close enough to the target for now
                        presence.ResetMoveToTarget();
                        presence.Velocity = Vector3.Zero;

                        // FIXME: This doesn't work
                        if (presence.PhysicsActor.Flying)
                            presence.Animator.TrySetMovementAnimation("HOVER");
                        else
                            presence.Animator.TrySetMovementAnimation("STAND");
                    }
                    else
                    {
                        Vector3 agent_control_v3 = new Vector3();
                        presence.HandleMoveToPositionUpdate(ref agent_control_v3, presence.Rotation, false, true);
                        presence.AddNewMovement(agent_control_v3, presence.Rotation);
                    }
//
////                    presence.DoMoveToPositionUpdate((0, presence.MoveToPositionTarget, null);

//
//

                }
            }
        }

        private AvatarAppearance GetAppearance(UUID target, Scene scene)
        {
            if (m_appearanceCache.ContainsKey(target))
                return m_appearanceCache[target];

            ScenePresence originalPresence = scene.GetScenePresence(target);

            if (originalPresence != null)
            {
                AvatarAppearance originalAppearance = originalPresence.Appearance;
                m_appearanceCache.Add(target, originalAppearance);
                return originalAppearance;
            }
            else
            {
                m_log.DebugFormat(
                    "[NPC MODULE]: Avatar {0} is not in the scene for us to grab baked textures from them.  Using defaults.", target);

                return new AvatarAppearance();
            }
        }

        public UUID CreateNPC(string firstname, string lastname, Vector3 position, Scene scene, UUID cloneAppearanceFrom)
        {
            NPCAvatar npcAvatar = new NPCAvatar(firstname, lastname, position, scene);
            npcAvatar.CircuitCode = (uint)Util.RandomClass.Next(0, int.MaxValue);

            m_log.DebugFormat(
                "[NPC MODULE]: Creating NPC {0} {1} {2} at {3} in {4}",
                firstname, lastname, npcAvatar.AgentId, position, scene.RegionInfo.RegionName);

            AgentCircuitData acd = new AgentCircuitData();
            acd.AgentID = npcAvatar.AgentId;
            acd.firstname = firstname;
            acd.lastname = lastname;
            acd.ServiceURLs = new Dictionary<string, object>();

            AvatarAppearance originalAppearance = GetAppearance(cloneAppearanceFrom, scene);
            AvatarAppearance npcAppearance = new AvatarAppearance(originalAppearance, true);
            acd.Appearance = npcAppearance;

//            for (int i = 0; i < acd.Appearance.Texture.FaceTextures.Length; i++)
//            {
//                m_log.DebugFormat(
//                    "[NPC MODULE]: NPC avatar {0} has texture id {1} : {2}",
//                    acd.AgentID, i, acd.Appearance.Texture.FaceTextures[i]);
//            }

            scene.AuthenticateHandler.AddNewCircuit(npcAvatar.CircuitCode, acd);
            scene.AddNewClient(npcAvatar);

            ScenePresence sp;
            if (scene.TryGetScenePresence(npcAvatar.AgentId, out sp))
            {
                m_log.DebugFormat(
                    "[NPC MODULE]: Successfully retrieved scene presence for NPC {0} {1}", sp.Name, sp.UUID);

                // Shouldn't call this - temporary.
                sp.CompleteMovement(npcAvatar);

//                        sp.SendAppearanceToAllOtherAgents();
//
//                        // Send animations back to the avatar as well
//                        sp.Animator.SendAnimPack();
            }
            else
            {
                m_log.WarnFormat("[NPC MODULE]: Could not find scene presence for NPC {0} {1}", sp.Name, sp.UUID);
            }

            lock (m_avatars)
                m_avatars.Add(npcAvatar.AgentId, npcAvatar);

            m_log.DebugFormat("[NPC MODULE]: Created NPC with id {0}", npcAvatar.AgentId);

            return npcAvatar.AgentId;
        }

        public void Autopilot(UUID agentID, Scene scene, Vector3 pos)
        {
            lock (m_avatars)
            {
                if (m_avatars.ContainsKey(agentID))
                {
                    ScenePresence sp;
                    scene.TryGetScenePresence(agentID, out sp);

                    m_log.DebugFormat(
                        "[NPC MODULE]: Moving {0} to {1} in {2}", sp.Name, pos, scene.RegionInfo.RegionName);

                    sp.MoveToTarget(pos);
                }
            }
        }

        public void Say(UUID agentID, Scene scene, string text)
        {
            lock (m_avatars)
            {
                if (m_avatars.ContainsKey(agentID))
                {
                    m_avatars[agentID].Say(text);
                }
            }
        }

        public void DeleteNPC(UUID agentID, Scene scene)
        {
            lock (m_avatars)
            {
                if (m_avatars.ContainsKey(agentID))
                {
                    scene.RemoveClient(agentID);
                    m_avatars.Remove(agentID);
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "NPCModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}