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
 *     * Neither the name of the OpenSim Project nor the
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
using OpenMetaverse;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class WindModule : IRegionModule
    {

        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private int m_frame = 0;
        private int m_frame_mod = 150;
        private Random rndnums = new Random(System.Environment.TickCount);
        private Scene m_scene = null;
        private bool ready = false;
        private Vector2[] windSpeeds = new Vector2[16 * 16];

        private Dictionary<UUID, ulong> m_rootAgents = new Dictionary<UUID, ulong>();

        // Current time in elpased seconds since Jan 1st 1970
  
      
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log.Debug("[WIND] Initializing");

            m_scene = scene;

            m_frame = 0;



            // Align ticks with Second Life



            // Just in case they don't have the stanzas
            try
            {
                
            }
            catch (Exception e)
            {
                m_log.Debug("[WIND] Configuration access failed, using defaults. Reason: " + e.Message);
                
            }

            
            scene.EventManager.OnFrame += WindUpdate;
            
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnClientClosed += ClientLoggedOut;

            GenWindPos();

            ready = true;


            
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            ready = false;
            //  Remove our hooks
            m_scene.EventManager.OnFrame -= WindUpdate;
            // m_scene.EventManager.OnNewClient -= SunToClient;
            m_scene.EventManager.OnMakeChildAgent -= MakeChildAgent;
            m_scene.EventManager.OnAvatarEnteringNewParcel -= AvatarEnteringParcel;
            m_scene.EventManager.OnClientClosed -= ClientLoggedOut;
        }

        public string Name
        {
            get { return "WindModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void WindToClient(IClientAPI client)
        {
                if (ready)
                {
                    //if (!sunFixed)
                        //GenWindPos();    // Generate shared values once
                    client.SendWindData(windSpeeds);
                    m_log.Debug("[WIND] Initial update for new client");
                }
        }

        public void WindUpdate()
        {
            if (((m_frame++ % m_frame_mod) != 0) || !ready)
            {
                return;
            }
            //m_log.Debug("[WIND]:Regenerating...");
            GenWindPos();        // Generate shared values once

            //int spotxp = 0;
            //int spotyp = 0;
            //int spotxm = 0;
            //int spotym = 0;
            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                if (!avatar.IsChildAgent)
                {
                    avatar.ControllingClient.SendWindData(windSpeeds);
                }
            }

            // set estate settings for region access to sun position
            //m_scene.RegionInfo.RegionSettings.SunVector = Position;
            //m_scene.RegionInfo.EstateSettings.sunHour = GetLindenEstateHourFromCurrentTime();
        }
        public void ForceWindUpdateToAllClients()
        {
            GenWindPos();        // Generate shared values once

            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                if (!avatar.IsChildAgent)
                    avatar.ControllingClient.SendWindData(windSpeeds);
            }

            // set estate settings for region access to sun position
            //m_scene.RegionInfo.RegionSettings.SunVector = Position;
            //m_scene.RegionInfo.RegionSettings.SunPosition = GetLindenEstateHourFromCurrentTime();
        }
        /// <summary>
        /// Calculate the sun's orbital position and its velocity.
        /// </summary>

        private void GenWindPos()
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    windSpeeds[y * 16 + x].X = (float)(rndnums.NextDouble() * 2d - 1d);
                    windSpeeds[y * 16 + x].Y = (float)(rndnums.NextDouble() * 2d - 1d);
                }
            }
        }

        private void ClientLoggedOut(UUID AgentId)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(AgentId))
                {
                    m_rootAgents.Remove(AgentId);
                    m_log.Info("[WIND]: Removing " + AgentId + ". Agent logged out.");
                }
            }
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    m_rootAgents[avatar.UUID] = avatar.RegionHandle;
                }
                else
                {
                    m_rootAgents.Add(avatar.UUID, avatar.RegionHandle);
                    WindToClient(avatar.ControllingClient);
                }
            }
            //m_log.Info("[FRIEND]: " + avatar.Name + " status:" + (!avatar.IsChildAgent).ToString());
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    if (m_rootAgents[avatar.UUID] == avatar.RegionHandle)
                    {
                        m_rootAgents.Remove(avatar.UUID);
                    }
                }
            }
        }
    }
}
