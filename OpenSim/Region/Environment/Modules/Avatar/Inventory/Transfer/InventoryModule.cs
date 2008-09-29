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

using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Inventory.Transfer
{
    public class InventoryModule : IInventoryModule, IRegionModule
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// We need to keep track of the pending item offers between clients since the itemId offered only
        /// occurs in the initial offer message, not the accept message.  So this dictionary links
        /// IM Session Ids to ItemIds
        /// </summary>
        private IDictionary<UUID, UUID> m_pendingOffers = new Dictionary<UUID, UUID>();

        private List<Scene> m_Scenelist = new List<Scene>();
        private Dictionary<UUID, Scene> m_AgentRegions = new Dictionary<UUID, Scene>();

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!m_Scenelist.Contains(scene))
            {
                m_Scenelist.Add(scene);

                scene.RegisterModuleInterface<IInventoryModule>(this);

                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnClientClosed += ClientLoggedOut;
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
            get { return "InventoryModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        private void OnNewClient(IClientAPI client)
        {
            // Inventory giving is conducted via instant message
            client.OnInstantMessage += OnInstantMessage;
        }

        private void OnInstantMessage(IClientAPI client, UUID fromAgentID,
                                      UUID fromAgentSession, UUID toAgentID,
                                      UUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, Vector3 Position, UUID RegionID,
                                      byte[] binaryBucket)
        {
            if (dialog == (byte) InstantMessageDialog.InventoryOffered)
            {
                m_log.DebugFormat(
                    "[AGENT INVENTORY]: Routing inventory offering message from {0}, {1} to {2}",
                    client.AgentId, client.Name, toAgentID);

                if (((Scene)(client.Scene)).Entities.ContainsKey(toAgentID) && ((Scene)(client.Scene)).Entities[toAgentID] is ScenePresence)
                {
                    ScenePresence user = (ScenePresence) ((Scene)(client.Scene)).Entities[toAgentID];

                    if (!user.IsChildAgent)
                    {
                        //byte[] rawId = new byte[16];

                        // First byte of the array is probably the item type
                        // Next 16 bytes are the UUID
                        //Array.Copy(binaryBucket, 1, rawId, 0, 16);

                        //UUID itemId = new UUID(new Guid(rawId));
                        UUID itemId = new UUID(binaryBucket, 1);

                        m_log.DebugFormat(
                            "[AGENT INVENTORY]: ItemId for giving is {0}", itemId);

                        m_pendingOffers[imSessionID] = itemId;

                        user.ControllingClient.SendInstantMessage(
                            fromAgentID, fromAgentSession, message, toAgentID, imSessionID, fromAgentName,
                            dialog, timestamp, binaryBucket);

                        return;
                    }
                    else
                    {
                        m_log.WarnFormat(
                            "[AGENT INVENTORY]: Agent {0} targeted for inventory give by {1}, {2} of {3} was a child agent!",
                            toAgentID, client.AgentId, client.Name, message);
                    }
                }
                else
                {
                    m_log.WarnFormat(
                        "[AGENT INVENTORY]: Could not find agent {0} for user {1}, {2} to give {3}",
                        toAgentID, client.AgentId, client.Name, message);
                }
            }
            else if (dialog == (byte) InstantMessageDialog.InventoryAccepted)
            {
                m_log.DebugFormat(
                    "[AGENT INVENTORY]: Routing inventory accepted message from {0}, {1} to {2}",
                    client.AgentId, client.Name, toAgentID);

                if (((Scene)(client.Scene)).Entities.ContainsKey(toAgentID) && ((Scene)(client.Scene)).Entities[toAgentID] is ScenePresence)
                {
                    ScenePresence user = (ScenePresence) ((Scene)(client.Scene)).Entities[toAgentID];

                    if (!user.IsChildAgent)
                    {
                        user.ControllingClient.SendInstantMessage(
                            fromAgentID, fromAgentSession, message, toAgentID, imSessionID, fromAgentName,
                            dialog, timestamp, binaryBucket);

                        if (m_pendingOffers.ContainsKey(imSessionID))
                        {
                            m_log.DebugFormat(
                                "[AGENT INVENTORY]: Accepted item id {0}", m_pendingOffers[imSessionID]);

                            // Since the message originates from the accepting client, the toAgentID is
                            // the agent giving the item.
                            ((Scene)(client.Scene)).GiveInventoryItem(client, toAgentID, m_pendingOffers[imSessionID]);

                            m_pendingOffers.Remove(imSessionID);
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[AGENT INVENTORY]: Could not find an item associated with session id {0} to accept",
                                imSessionID);
                        }

                        return;
                    }
                    else
                    {
                        m_log.WarnFormat(
                            "[AGENT INVENTORY]: Agent {0} targeted for inventory give by {1}, {2} of {3} was a child agent!",
                            toAgentID, client.AgentId, client.Name, message);
                    }
                }
                else
                {
                    m_log.WarnFormat(
                        "[AGENT INVENTORY]: Could not find agent {0} for user {1}, {2} to give {3}",
                        toAgentID, client.AgentId, client.Name, message);
                }
            }
            else if (dialog == (byte) InstantMessageDialog.InventoryDeclined)
            {
                if (((Scene)(client.Scene)).Entities.ContainsKey(toAgentID) && ((Scene)(client.Scene)).Entities[toAgentID] is ScenePresence)
                {
                    ScenePresence user = (ScenePresence) ((Scene)(client.Scene)).Entities[toAgentID];

                    if (!user.IsChildAgent)
                    {
                        user.ControllingClient.SendInstantMessage(
                            fromAgentID, fromAgentSession, message, toAgentID, imSessionID, fromAgentName,
                            dialog, timestamp, binaryBucket);

                        if (m_pendingOffers.ContainsKey(imSessionID))
                        {
                            m_log.DebugFormat(
                                "[AGENT INVENTORY]: Declined item id {0}", m_pendingOffers[imSessionID]);

                            m_pendingOffers.Remove(imSessionID);
                        }
                        else
                        {
                            m_log.ErrorFormat(
                                "[AGENT INVENTORY]: Could not find an item associated with session id {0} to decline",
                                imSessionID);
                        }
                    }
                }
            }
        }

        public void SetRootAgentScene(UUID agentID, Scene scene)
        {
            m_AgentRegions[agentID] = scene;
        }

        public bool NeedSceneCacheClear(UUID agentID, Scene scene)
        {
            if (!m_AgentRegions.ContainsKey(agentID))
            {
                // Since we can get here two ways, we need to scan
                // the scenes here. This is somewhat more expensive
                // but helps avoid a nasty bug
                //

                foreach (Scene s in m_Scenelist)
                {
                    ScenePresence presence;

                    if (s.TryGetAvatar(agentID, out presence))
                    {
                        // If the agent is in this scene, then we
                        // are being called twice in a single
                        // teleport. This is wasteful of cycles
                        // but harmless due to this 2nd level check
                        //
                        // If the agent is found in another scene
                        // then the list wasn't current
                        //
                        // If the agent is totally unknown, then what
                        // are we even doing here??
                        //
                        if (s == scene)
                            return true;
                        else
                            return false;
                    }
                }
                return true;
            }

            // The agent is left in current Scene, so we must be
            // going to another instance
            //
            if (m_AgentRegions[agentID] == scene)
            {
                m_AgentRegions.Remove(agentID);
                return true;
            }

            // Another region has claimed the agent
            //
            return false;
        }

        public void ClientLoggedOut(UUID agentID)
        {
            if (m_AgentRegions.ContainsKey(agentID))
                m_AgentRegions.Remove(agentID);
        }
    }
}
