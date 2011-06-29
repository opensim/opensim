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
using OpenSim.Region.CoreModules.Avatar.NPC;
using OpenSim.Framework;
using Timer=System.Timers.Timer;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.OptionalModules.World.NPC
{
    public class NPCModule : IRegionModule, INPCModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private const bool m_enabled = false;

        private Mutex m_createMutex;
        private Timer m_timer;

        private Dictionary<UUID,NPCAvatar> m_avatars = new Dictionary<UUID, NPCAvatar>();
        private Dictionary<UUID,AvatarAppearance> m_appearanceCache = new Dictionary<UUID, AvatarAppearance>();

        // Timer vars.
        private bool p_inUse = false;
        private readonly object p_lock = new object();
        // Private Temporary Variables.
        private string p_firstname;
        private string p_lastname;
        private Vector3 p_position;
        private Scene p_scene;
        private UUID p_cloneAppearanceFrom;
        private UUID p_returnUuid;

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_createMutex = new Mutex(false);

            m_timer = new Timer(500);
            m_timer.Elapsed += m_timer_Elapsed;
            m_timer.Start();

            scene.RegisterModuleInterface<INPCModule>(this);
        }

        private AvatarAppearance GetAppearance(UUID target, Scene scene)
        {
            if (m_appearanceCache.ContainsKey(target))
                return m_appearanceCache[target];

            AvatarAppearance appearance = scene.AvatarService.GetAppearance(target);
            if (appearance != null)
            {
                m_appearanceCache.Add(target, appearance);
                return appearance;
            }

            return new AvatarAppearance();
        }

        public UUID CreateNPC(string firstname, string lastname,Vector3 position, Scene scene, UUID cloneAppearanceFrom)
        {
            m_log.DebugFormat(
                "[NPC MODULE]: Queueing request to create NPC {0} {1} at {2} in {3} cloning appearance of {4}",
                firstname, lastname, position, scene.RegionInfo.RegionName, cloneAppearanceFrom);

            // Block.
            m_createMutex.WaitOne();

            // Copy Temp Variables for Timer to pick up.
            lock (p_lock)
            {
                p_firstname = firstname;
                p_lastname = lastname;
                p_position = position;
                p_scene = scene;
                p_cloneAppearanceFrom = cloneAppearanceFrom;
                p_inUse = true;
                p_returnUuid = UUID.Zero;
            }

            while (p_returnUuid == UUID.Zero)
            {
                Thread.Sleep(250);
            }

            m_createMutex.ReleaseMutex();

            return p_returnUuid;
        }

        public void Autopilot(UUID agentID, Scene scene, Vector3 pos)
        {
            lock (m_avatars)
            {
                if (m_avatars.ContainsKey(agentID))
                {
                    ScenePresence sp;
                    scene.TryGetScenePresence(agentID, out sp);
                    sp.DoAutoPilot(0, pos, m_avatars[agentID]);
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

        void m_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                lock (p_lock)
                {
                    if (p_inUse)
                    {
                        p_inUse = false;
    
                        NPCAvatar npcAvatar = new NPCAvatar(p_firstname, p_lastname, p_position, p_scene);
                        npcAvatar.CircuitCode = (uint) Util.RandomClass.Next(0, int.MaxValue);
    
                        m_log.DebugFormat(
                            "[NPC MODULE]: Creating NPC {0} {1} {2} at {3} in {4}",
                            p_firstname, p_lastname, npcAvatar.AgentId, p_position, p_scene.RegionInfo.RegionName);
    
                        AgentCircuitData acd = new AgentCircuitData();
                        acd.AgentID = npcAvatar.AgentId;
                        acd.firstname = p_firstname;
                        acd.lastname = p_lastname;
                        acd.ServiceURLs = new Dictionary<string, object>();
    
                        AvatarAppearance originalAppearance = GetAppearance(p_cloneAppearanceFrom, p_scene);
                        AvatarAppearance npcAppearance = new AvatarAppearance(originalAppearance, true);
                        acd.Appearance = npcAppearance;
    
                        p_scene.AuthenticateHandler.AddNewCircuit(npcAvatar.CircuitCode, acd);
                        p_scene.AddNewClient(npcAvatar);
    
                        ScenePresence sp;
                        if (p_scene.TryGetScenePresence(npcAvatar.AgentId, out sp))
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
    
                        m_avatars.Add(npcAvatar.AgentId, npcAvatar);
    
                        p_returnUuid = npcAvatar.AgentId;

                        m_log.DebugFormat("[NPC MODULE]: Created NPC with id {0}", p_returnUuid);
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[NPC MODULE]: NPC creation failed with exception {0} {1}", ex.Message, ex.StackTrace);
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
