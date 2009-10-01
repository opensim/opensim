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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Gods
{
    public class GodsModule : IRegionModule, IGodsModule
    {
        protected Scene m_scene;
        protected IDialogModule m_dialogModule;
        
        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_dialogModule = m_scene.RequestModuleInterface<IDialogModule>();
            m_scene.RegisterModuleInterface<IGodsModule>(this);
        }
        
        public void PostInitialise() {}
        public void Close() {}
        public string Name { get { return "Gods Module"; } }
        public bool IsSharedModule { get { return false; } }
        
        public void RequestGodlikePowers(
            UUID agentID, UUID sessionID, UUID token, bool godLike, IClientAPI controllingClient)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentID);

            if (sp != null)
            {
                if (godLike == false)
                {
                    sp.GrantGodlikePowers(agentID, sessionID, token, godLike);
                    return;
                }

                // First check that this is the sim owner
                if (m_scene.Permissions.IsGod(agentID))
                {
                    // Next we check for spoofing.....
                    UUID testSessionID = sp.ControllingClient.SessionId;
                    if (sessionID == testSessionID)
                    {
                        if (sessionID == controllingClient.SessionId)
                        {
                            //m_log.Info("godlike: " + godLike.ToString());
                            sp.GrantGodlikePowers(agentID, testSessionID, token, godLike);
                        }
                    }
                }
                else
                {
                    if (m_dialogModule != null)
                        m_dialogModule.SendAlertToUser(agentID, "Request for god powers denied");
                }
            }
        }
        
        /// <summary>
        /// Kicks User specified from the simulator. This logs them off of the grid
        /// If the client gets the UUID: 44e87126e7944ded05b37c42da3d5cdb it assumes
        /// that you're kicking it even if the avatar's UUID isn't the UUID that the
        /// agent is assigned
        /// </summary>
        /// <param name="godID">The person doing the kicking</param>
        /// <param name="sessionID">The session of the person doing the kicking</param>
        /// <param name="agentID">the person that is being kicked</param>
        /// <param name="kickflags">This isn't used apparently</param>
        /// <param name="reason">The message to send to the user after it's been turned into a field</param>
        public void KickUser(UUID godID, UUID sessionID, UUID agentID, uint kickflags, byte[] reason)
        {
            // For some reason the client sends this seemingly hard coded UUID for kicking everyone.   Dun-know.
            UUID kickUserID = new UUID("44e87126e7944ded05b37c42da3d5cdb");
            
            ScenePresence sp = m_scene.GetScenePresence(agentID);

            if (sp != null || agentID == kickUserID)
            {
                if (m_scene.Permissions.IsGod(godID))
                {
                    if (agentID == kickUserID)
                    {
                        m_scene.ClientManager.ForEachClient(
                            delegate(IClientAPI controller)
                            {
                                if (controller.AgentId != godID)
                                    controller.Kick(Utils.BytesToString(reason));
                            }
                        );

                        // This is a bit crude.   It seems the client will be null before it actually stops the thread
                        // The thread will kill itself eventually :/
                        // Is there another way to make sure *all* clients get this 'inter region' message?
                        m_scene.ForEachScenePresence(
                            delegate(ScenePresence p)
                            {
                                if (p.UUID != godID && !p.IsChildAgent)
                                {
                                    // Possibly this should really be p.Close() though that method doesn't send a close
                                    // to the client
                                    p.ControllingClient.Close(true);
                                }
                            }
                        );
                    }
                    else
                    {
                        m_scene.SceneGraph.removeUserCount(!sp.IsChildAgent);

                        sp.ControllingClient.Kick(Utils.BytesToString(reason));
                        sp.ControllingClient.Close(true);
                    }
                }
                else
                {
                    m_dialogModule.SendAlertToUser(godID, "Kick request denied");
                }
            }
        }
    }
}