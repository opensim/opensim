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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules
{
    public class CloudModule : ICloudModule
    {
        private static readonly log4net.ILog m_log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private Random m_rndnums = new Random(Environment.TickCount);
        private Scene m_scene = null;
        private bool m_ready = false;
        private bool m_enabled = false;
        private float m_cloudDensity = 1.0F;
        private float[] cloudCover = new float[16 * 16];

        private Dictionary<UUID, ulong> m_rootAgents = new Dictionary<UUID, ulong>();
     
        public void Initialise(Scene scene, IConfigSource config)
        {
            IConfig cloudConfig = config.Configs["Cloud"];

            if (cloudConfig != null)
            {
                m_enabled = cloudConfig.GetBoolean("enabled", false);
                m_cloudDensity = cloudConfig.GetFloat("density", 0.5F);
            }

            if (m_enabled)
            {

                m_scene = scene;

                scene.EventManager.OnMakeChildAgent += MakeChildAgent;
                scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
                scene.EventManager.OnClientClosed += ClientLoggedOut;
                scene.RegisterModuleInterface<ICloudModule>(this);

                GenerateCloudCover();

                m_ready = true;

            }

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            if (m_enabled)
            {
                m_ready = false;
                //  Remove our hooks
                m_scene.EventManager.OnMakeChildAgent -= MakeChildAgent;
                m_scene.EventManager.OnAvatarEnteringNewParcel -= AvatarEnteringParcel;
                m_scene.EventManager.OnClientClosed -= ClientLoggedOut;
            }
        }

        public string Name
        {
            get { return "CloudModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }


        public float CloudCover(int x, int y, int z)
        {
            float cover = 0f;
            x /= 16;
            y /= 16;
            if (x < 0) x = 0;
            if (x > 15) x = 15;
            if (y < 0) y = 0;
            if (y > 15) y = 15;

            if (cloudCover != null)
            {
                cover = cloudCover[y * 16 + x];
            }

            return cover;
        }

        public void CloudsToClient(IClientAPI client)
        {
            if (m_ready)
            {
                client.SendCloudData(cloudCover);
            }
        }

       
        /// <summary>
        /// Calculate the cloud cover over the region.
        /// </summary>
        private void GenerateCloudCover()
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    cloudCover[y * 16 + x] = (float)(m_rndnums.NextDouble()); // 0 to 1
                    cloudCover[y * 16 + x] *= m_cloudDensity;
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
                    CloudsToClient(avatar.ControllingClient);
                }
            }
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
