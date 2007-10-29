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

using System.Collections.Generic;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Console;
using OpenSim.Framework;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules
{
    public class InstantMessageModule : IRegionModule
    {
        private List<Scene> m_scenes = new List<Scene>();
        private LogBase m_log;

        public InstantMessageModule()
        {
            m_log = OpenSim.Framework.Console.MainLog.Instance;
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                scene.EventManager.OnNewClient += OnNewClient;     
            }
        }

        void OnNewClient(OpenSim.Framework.IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
        }

        void OnInstantMessage(libsecondlife.LLUUID fromAgentID, 
            libsecondlife.LLUUID fromAgentSession, libsecondlife.LLUUID toAgentID, 
            libsecondlife.LLUUID imSessionID, uint timestamp, string fromAgentName, 
            string message, byte dialog)
        {
            // TODO: Remove after debugging. Privacy implications.
            m_log.Verbose("IM",fromAgentName + ": " + message);

            foreach (Scene m_scene in m_scenes)
            {
                if (m_scene.Entities.ContainsKey(toAgentID) && m_scene.Entities[toAgentID] is ScenePresence)
                {
                    // Local Message
                    ScenePresence user = (ScenePresence)m_scene.Entities[toAgentID];
                    if (!user.IsChildAgent)
                    {
                        user.ControllingClient.SendInstantMessage(fromAgentID, fromAgentSession, message,
                            toAgentID, imSessionID, fromAgentName, dialog, timestamp);
                    }
                    // Message sent
                    return;
                }
            }

            // Still here, try send via Grid
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "InstantMessageModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
