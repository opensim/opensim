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

using System.Collections.Generic;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.Avatar.NPC;
using OpenSim.Framework;

namespace OpenSim.Region.OptionalModules.World.NPC
{
    public class NPCModule : IRegionModule, INPCModule
    {
        // private const bool m_enabled = false;

        private Dictionary<UUID,NPCAvatar> m_avatars = new Dictionary<UUID, NPCAvatar>();

        private Dictionary<UUID,AvatarAppearance> m_appearanceCache = new Dictionary<UUID, AvatarAppearance>();

        private AvatarAppearance GetAppearance(UUID target, Scene scene)
        {
            if (m_appearanceCache.ContainsKey(target))
                return m_appearanceCache[target];

            return scene.CommsManager.AvatarService.GetUserAppearance(target);
        }

        public UUID CreateNPC(string firstname, string lastname,Vector3 position, Scene scene, UUID cloneAppearanceFrom)
        {
            NPCAvatar npcAvatar = new NPCAvatar(firstname, lastname, position, scene);
            npcAvatar.CircuitCode = (uint) Util.RandomClass.Next(0, int.MaxValue);

            scene.ClientManager.Add(npcAvatar.CircuitCode, npcAvatar);
            scene.AddNewClient(npcAvatar);

            ScenePresence sp;
            if(scene.TryGetAvatar(npcAvatar.AgentId, out sp))
            {
                AvatarAppearance x = GetAppearance(cloneAppearanceFrom, scene);

                List<byte> wearbyte = new List<byte>();
                for (int i = 0; i < x.VisualParams.Length; i++)
                {
                    wearbyte.Add(x.VisualParams[i]);
                }

                sp.SetAppearance(x.Texture.GetBytes(), wearbyte);
            }

            m_avatars.Add(npcAvatar.AgentId, npcAvatar);

            return npcAvatar.AgentId;
        }

        public void Autopilot(UUID agentID, Scene scene, Vector3 pos)
        {
            lock (m_avatars)
                if (m_avatars.ContainsKey(agentID))
                {
                    ScenePresence sp;
                    scene.TryGetAvatar(agentID, out sp);
                    sp.DoAutoPilot(0, pos, m_avatars[agentID]);
                }
        }

        public void Say(UUID agentID, Scene scene, string text)
        {
            lock (m_avatars)
                if (m_avatars.ContainsKey(agentID))
                {
                    m_avatars[agentID].Say(text);
                }
        }

        public void DeleteNPC(UUID agentID, Scene scene)
        {
            lock(m_avatars)
                if (m_avatars.ContainsKey(agentID))
                {
                    scene.RemoveClient(agentID);
                    m_avatars.Remove(agentID);
                }
        }


        public void Initialise(Scene scene, IConfigSource source)
        {
            scene.RegisterModuleInterface<INPCModule>(this);
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
