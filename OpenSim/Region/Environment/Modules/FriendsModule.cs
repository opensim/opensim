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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using Nini.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Region.Environment.Modules
{
    public class FriendsModule : IRegionModule
    {
        private List<Scene> m_scenes = new List<Scene>();
        private LogBase m_log;

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log = MainLog.Instance;
            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                scene.EventManager.OnNewClient += OnNewClient;
            }
        }

        private void OnNewClient(IClientAPI client)
        {
            //FormFriendship(client,new Guid("c43a67ab-b196-4d62-936c-b40369547dee"));
            //FormFriendship(client, new Guid("0a2f777b-f44c-4662-8b22-c90ae038a3e6"));
        }

        public void PostInitialise()
        {
        }

        private void FormFriendship(IClientAPI client, Guid friend)
        {
            foreach (Scene scene in m_scenes)
            {
                if (scene.Entities.ContainsKey(client.AgentId) && scene.Entities[client.AgentId] is ScenePresence)
                {
                    OnlineNotificationPacket ONPack = new OnlineNotificationPacket();
                    OnlineNotificationPacket.AgentBlockBlock[] AgentBlock = new OnlineNotificationPacket.AgentBlockBlock[1];

                    AgentBlock[0] = new OnlineNotificationPacket.AgentBlockBlock();
                    AgentBlock[0].AgentID = new LLUUID(friend);
                    ONPack.AgentBlock = AgentBlock;
                    client.OutPacket(ONPack,ThrottleOutPacketType.Task);
                }
            }

        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "FriendsModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}