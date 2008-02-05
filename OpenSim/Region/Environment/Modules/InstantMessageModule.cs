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
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class InstantMessageModule : IRegionModule
    {
        private List<Scene> m_scenes = new List<Scene>();

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnGridInstantMessageToIMModule += OnGridInstantMessage;
            }
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnInstantMessage += OnInstantMessage;
        }

        private void OnInstantMessage(IClientAPI client,LLUUID fromAgentID,
                                      LLUUID fromAgentSession, LLUUID toAgentID,
                                      LLUUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline, 
                                      uint ParentEstateID, LLVector3 Position, LLUUID RegionID, 
                                      byte[] binaryBucket)
        {
            bool FriendDialog = ((dialog == (byte)38) || (dialog == (byte)39) || (dialog == (byte)40));

            // IM dialogs need to be pre-processed and have their sessionID filled by the server
            // so the sim can match the transaction on the return packet.
            
            // Don't send a Friend Dialog IM with a LLUUID.Zero session.
            if (!(FriendDialog && imSessionID == LLUUID.Zero))
            {
                foreach (Scene scene in m_scenes)
                {
                    if (scene.Entities.ContainsKey(toAgentID) && scene.Entities[toAgentID] is ScenePresence)
                    {
                        // Local message
                        ScenePresence user = (ScenePresence)scene.Entities[toAgentID];
                        if (!user.IsChildAgent)
                        {
                            user.ControllingClient.SendInstantMessage(fromAgentID, fromAgentSession, message,
                                                                      toAgentID, imSessionID, fromAgentName, dialog,
                                                                      timestamp);
                            // Message sent
                            return;
                        }
                    }
                }
            }

            // Still here, try send via Grid
            // TODO
        }
        
        // Trusty OSG1 called method.  This method also gets called from the FriendsModule
        // Turns out the sim has to send an instant message to the user to get it to show an accepted friend.

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // Trigger the above event handler
            OnInstantMessage(null,new LLUUID(msg.fromAgentID), new LLUUID(msg.fromAgentSession), 
                new LLUUID(msg.toAgentID), new LLUUID(msg.imSessionID), msg.timestamp, msg.fromAgentName, 
                msg.message, msg.dialog, msg.fromGroup, msg.offline, msg.ParentEstateID, 
                new LLVector3(msg.Position.x,msg.Position.y,msg.Position.z), new LLUUID(msg.RegionID), 
                msg.binaryBucket);

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
