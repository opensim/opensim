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
using System.Linq;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class GroupsMessagingModule : ISharedRegionModule, IGroupsMessagingModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_sceneList = new List<Scene>();
        private IPresenceService m_presenceService;

        private IMessageTransferModule m_msgTransferModule = null;

        private IGroupsServicesConnector m_groupData = null;

        // Config Options
        private bool m_groupMessagingEnabled = false;
        private bool m_debugEnabled = true;

        /// <summary>
        /// If enabled, module only tries to send group IMs to online users by querying cached presence information.
        /// </summary>
        private bool m_messageOnlineAgentsOnly;

        /// <summary>
        /// Cache for online users.
        /// </summary>
        /// <remarks>
        /// Group ID is key, presence information for online members is value.
        /// Will only be non-null if m_messageOnlineAgentsOnly = true
        /// We cache here so that group messages don't constantly have to re-request the online user list to avoid
        /// attempted expensive sending of messages to offline users.
        /// The tradeoff is that a user that comes online will not receive messages consistently from all other users
        /// until caches have updated.
        /// Therefore, we set the cache expiry to just 20 seconds.
        /// </remarks>
        private ExpiringCache<UUID, PresenceInfo[]> m_usersOnlineCache;

        private int m_usersOnlineCacheExpirySeconds = 20;

        #region IRegionModuleBase Members

        public void Initialise(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];

            if (groupsConfig == null)
            {
                // Do not run this module by default.
                return;
            }
            else
            {
                // if groups aren't enabled, we're not needed.
                // if we're not specified as the connector to use, then we're not wanted
                if ((groupsConfig.GetBoolean("Enabled", false) == false)
                     || (groupsConfig.GetString("MessagingModule", "") != Name))
                {
                    m_groupMessagingEnabled = false;
                    return;
                }

                m_groupMessagingEnabled = groupsConfig.GetBoolean("MessagingEnabled", true);

                if (!m_groupMessagingEnabled)
                {
                    return;
                }

                m_messageOnlineAgentsOnly = groupsConfig.GetBoolean("MessageOnlineUsersOnly", false);

                if (m_messageOnlineAgentsOnly)
                     m_usersOnlineCache = new ExpiringCache<UUID, PresenceInfo[]>();

                m_debugEnabled = groupsConfig.GetBoolean("DebugEnabled", true);
            }

            m_log.InfoFormat(
                "[GROUPS-MESSAGING]: GroupsMessagingModule enabled with MessageOnlineOnly = {0}, DebugEnabled = {1}",
                m_messageOnlineAgentsOnly, m_debugEnabled);
        }

        public void AddRegion(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;
            
            scene.RegisterModuleInterface<IGroupsMessagingModule>(this);
        }
        
        public void RegionLoaded(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData = scene.RequestModuleInterface<IGroupsServicesConnector>();

            // No groups module, no groups messaging
            if (m_groupData == null)
            {
                m_log.Error("[GROUPS-MESSAGING]: Could not get IGroupsServicesConnector, GroupsMessagingModule is now disabled.");
                Close();
                m_groupMessagingEnabled = false;
                return;
            }

            m_msgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

            // No message transfer module, no groups messaging
            if (m_msgTransferModule == null)
            {
                m_log.Error("[GROUPS-MESSAGING]: Could not get MessageTransferModule");
                Close();
                m_groupMessagingEnabled = false;
                return;
            }

            if (m_presenceService == null)
                m_presenceService = scene.PresenceService;

            m_sceneList.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnClientLogin += OnClientLogin;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_sceneList.Remove(scene);
        }

        public void Close()
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.Debug("[GROUPS-MESSAGING]: Shutting down GroupsMessagingModule module.");

            foreach (Scene scene in m_sceneList)
            {
                scene.EventManager.OnNewClient -= OnNewClient;
                scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            }

            m_sceneList.Clear();

            m_groupData = null;
            m_msgTransferModule = null;
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "GroupsMessagingModule"; }
        }

        #endregion

        #region ISharedRegionModule Members

        public void PostInitialise()
        {
            // NoOp
        }

        #endregion

        /// <summary>
        /// Not really needed, but does confirm that the group exists.
        /// </summary>
        public bool StartGroupChatSession(UUID agentID, UUID groupID)
        {
            if (m_debugEnabled)
                m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
                
            GroupRecord groupInfo = m_groupData.GetGroupRecord(agentID, groupID, null);

            if (groupInfo != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        
        public void SendMessageToGroup(GridInstantMessage im, UUID groupID)
        {
            List<GroupMembersData> groupMembers = m_groupData.GetGroupMembers(new UUID(im.fromAgentID), groupID);
            int groupMembersCount = groupMembers.Count;

            if (m_messageOnlineAgentsOnly)
            {
                string[] t1 = groupMembers.ConvertAll<string>(gmd => gmd.AgentID.ToString()).ToArray();

                // We cache in order not to overwhlem the presence service on large grids with many groups.  This does
                // mean that members coming online will not see all group members until after m_usersOnlineCacheExpirySeconds has elapsed.
                // (assuming this is the same across all grid simulators).
                PresenceInfo[] onlineAgents;
                if (!m_usersOnlineCache.TryGetValue(groupID, out onlineAgents))
                {
                    onlineAgents = m_presenceService.GetAgents(t1);
                    m_usersOnlineCache.Add(groupID, onlineAgents, m_usersOnlineCacheExpirySeconds);
                }

                HashSet<string> onlineAgentsUuidSet = new HashSet<string>();
                Array.ForEach<PresenceInfo>(onlineAgents, pi => onlineAgentsUuidSet.Add(pi.UserID));

                groupMembers = groupMembers.Where(gmd => onlineAgentsUuidSet.Contains(gmd.AgentID.ToString())).ToList();

    //            if (m_debugEnabled)
//                    m_log.DebugFormat(
//                        "[GROUPS-MESSAGING]: SendMessageToGroup called for group {0} with {1} visible members, {2} online",
//                        groupID, groupMembersCount, groupMembers.Count());
            }
            else
            {
                if (m_debugEnabled)
                    m_log.DebugFormat(
                        "[GROUPS-MESSAGING]: SendMessageToGroup called for group {0} with {1} visible members",
                        groupID, groupMembers.Count);
            }

            int requestStartTick = Environment.TickCount;

            foreach (GroupMembersData member in groupMembers)
            {
                if (m_groupData.hasAgentDroppedGroupChatSession(member.AgentID, groupID))
                {
                    // Don't deliver messages to people who have dropped this session
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} has dropped session, not delivering to them", member.AgentID);
                    continue;
                }

                // Copy Message
                GridInstantMessage msg = new GridInstantMessage();
                msg.imSessionID = groupID.Guid;
                msg.fromAgentName = im.fromAgentName;
                msg.message = im.message;
                msg.dialog = im.dialog;
                msg.offline = im.offline;
                msg.ParentEstateID = im.ParentEstateID;
                msg.Position = im.Position;
                msg.RegionID = im.RegionID;
                msg.binaryBucket = im.binaryBucket;
                msg.timestamp = (uint)Util.UnixTimeSinceEpoch();

                msg.fromAgentID = im.fromAgentID;
                msg.fromGroup = true;

                msg.toAgentID = member.AgentID.Guid;

                IClientAPI client = GetActiveClient(member.AgentID);
                if (client == null)
                {
                    // If they're not local, forward across the grid
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Delivering to {0} via Grid", member.AgentID);
                    m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
                }
                else
                {
                    // Deliver locally, directly
                    if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Passing to ProcessMessageFromGroupSession to deliver to {0} locally", client.Name);
                    ProcessMessageFromGroupSession(msg);
                }
            }

            // Temporary for assessing how long it still takes to send messages to large online groups.
            if (m_messageOnlineAgentsOnly)
                m_log.DebugFormat(
                    "[GROUPS-MESSAGING]: SendMessageToGroup for group {0} with {1} visible members, {2} online took {3}ms",
                    groupID, groupMembersCount, groupMembers.Count(), Environment.TickCount - requestStartTick);
        }
        
        #region SimGridEventHandlers

        void OnClientLogin(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: OnInstantMessage registered for {0}", client.Name);
        }

        private void OnNewClient(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: OnInstantMessage registered for {0}", client.Name);

            client.OnInstantMessage += OnInstantMessage;
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // The instant message module will only deliver messages of dialog types:
            // MessageFromAgent, StartTyping, StopTyping, MessageFromObject
            //
            // Any other message type will not be delivered to a client by the 
            // Instant Message Module


            if (m_debugEnabled)
            {
                m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

                DebugGridInstantMessage(msg);
            }

            // Incoming message from a group
            if ((msg.fromGroup == true) && 
                ((msg.dialog == (byte)InstantMessageDialog.SessionSend)
                 || (msg.dialog == (byte)InstantMessageDialog.SessionAdd)
                 || (msg.dialog == (byte)InstantMessageDialog.SessionDrop)))
            {
                ProcessMessageFromGroupSession(msg);
            }
        }

        private void ProcessMessageFromGroupSession(GridInstantMessage msg)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Session message from {0} going to agent {1}", msg.fromAgentName, msg.toAgentID);

            UUID AgentID = new UUID(msg.fromAgentID);
            UUID GroupID = new UUID(msg.imSessionID);

            switch (msg.dialog)
            {
                case (byte)InstantMessageDialog.SessionAdd:
                    m_groupData.AgentInvitedToGroupChatSession(AgentID, GroupID);
                    break;

                case (byte)InstantMessageDialog.SessionDrop:
                    m_groupData.AgentDroppedFromGroupChatSession(AgentID, GroupID);
                    break;

                case (byte)InstantMessageDialog.SessionSend:
                    if (!m_groupData.hasAgentDroppedGroupChatSession(AgentID, GroupID)
                        && !m_groupData.hasAgentBeenInvitedToGroupChatSession(AgentID, GroupID)
                        )
                    {
                        // Agent not in session and hasn't dropped from session
                        // Add them to the session for now, and Invite them
                        m_groupData.AgentInvitedToGroupChatSession(AgentID, GroupID);

                        UUID toAgentID = new UUID(msg.toAgentID);
                        IClientAPI activeClient = GetActiveClient(toAgentID);
                        if (activeClient != null)
                        {
                            GroupRecord groupInfo = m_groupData.GetGroupRecord(UUID.Zero, GroupID, null);
                            if (groupInfo != null)
                            {
                                if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Sending chatterbox invite instant message");

                                // Force? open the group session dialog???
                                // and simultanously deliver the message, so we don't need to do a seperate client.SendInstantMessage(msg);
                                IEventQueue eq = activeClient.Scene.RequestModuleInterface<IEventQueue>();
                                eq.ChatterboxInvitation(
                                    GroupID
                                    , groupInfo.GroupName
                                    , new UUID(msg.fromAgentID)
                                    , msg.message
                                    , new UUID(msg.toAgentID)
                                    , msg.fromAgentName
                                    , msg.dialog
                                    , msg.timestamp
                                    , msg.offline == 1
                                    , (int)msg.ParentEstateID
                                    , msg.Position
                                    , 1
                                    , new UUID(msg.imSessionID)
                                    , msg.fromGroup
                                    , Utils.StringToBytes(groupInfo.GroupName)
                                    );

                                eq.ChatterBoxSessionAgentListUpdates(
                                    new UUID(GroupID)
                                    , new UUID(msg.fromAgentID)
                                    , new UUID(msg.toAgentID)
                                    , false //canVoiceChat
                                    , false //isModerator
                                    , false //text mute
                                    );
                            }
                        }
                    }
                    else if (!m_groupData.hasAgentDroppedGroupChatSession(AgentID, GroupID))
                    {
                        // User hasn't dropped, so they're in the session, 
                        // maybe we should deliver it.
                        IClientAPI client = GetActiveClient(new UUID(msg.toAgentID));
                        if (client != null)
                        {
                            // Deliver locally, directly
                            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: Delivering to {0} locally", client.Name);
                            client.SendInstantMessage(msg);
                        }
                        else
                        {
                            m_log.WarnFormat("[GROUPS-MESSAGING]: Received a message over the grid for a client that isn't here: {0}", msg.toAgentID);
                        }
                    }
                    break;

                default:
                    m_log.WarnFormat("[GROUPS-MESSAGING]: I don't know how to proccess a {0} message.", ((InstantMessageDialog)msg.dialog).ToString());
                    break;
            }
        }

        #endregion


        #region ClientEvents
        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled)
            {
                m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

                DebugGridInstantMessage(im);
            }

            // Start group IM session
            if ((im.dialog == (byte)InstantMessageDialog.SessionGroupStart))
            {
                if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING]: imSessionID({0}) toAgentID({1})", im.imSessionID, im.toAgentID);

                UUID GroupID = new UUID(im.imSessionID);
                UUID AgentID = new UUID(im.fromAgentID);

                GroupRecord groupInfo = m_groupData.GetGroupRecord(UUID.Zero, GroupID, null);
    
                if (groupInfo != null)
                {
                    m_groupData.AgentInvitedToGroupChatSession(AgentID, GroupID);

                    ChatterBoxSessionStartReplyViaCaps(remoteClient, groupInfo.GroupName, GroupID);

                    IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();
                    queue.ChatterBoxSessionAgentListUpdates(
                        GroupID
                        , AgentID
                        , new UUID(im.toAgentID)
                        , false //canVoiceChat
                        , false //isModerator
                        , false //text mute
                        );
                }
            }

            // Send a message from locally connected client to a group
            if ((im.dialog == (byte)InstantMessageDialog.SessionSend))
            {
                UUID GroupID = new UUID(im.imSessionID);
                UUID AgentID = new UUID(im.fromAgentID);

                if (m_debugEnabled) 
                    m_log.DebugFormat("[GROUPS-MESSAGING]: Send message to session for group {0} with session ID {1}", GroupID, im.imSessionID.ToString());

                //If this agent is sending a message, then they want to be in the session
                m_groupData.AgentInvitedToGroupChatSession(AgentID, GroupID);

                SendMessageToGroup(im, GroupID);
            }
        }

        #endregion

        void ChatterBoxSessionStartReplyViaCaps(IClientAPI remoteClient, string groupName, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS-MESSAGING]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap moderatedMap = new OSDMap(4);
            moderatedMap.Add("voice", OSD.FromBoolean(false));

            OSDMap sessionMap = new OSDMap(4);
            sessionMap.Add("moderated_mode", moderatedMap);
            sessionMap.Add("session_name", OSD.FromString(groupName));
            sessionMap.Add("type", OSD.FromInteger(0));
            sessionMap.Add("voice_enabled", OSD.FromBoolean(false));

            OSDMap bodyMap = new OSDMap(4);
            bodyMap.Add("session_id", OSD.FromUUID(groupID));
            bodyMap.Add("temp_session_id", OSD.FromUUID(groupID));
            bodyMap.Add("success", OSD.FromBoolean(true));
            bodyMap.Add("session_info", sessionMap);

            IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();

            if (queue != null)
            {
                queue.Enqueue(queue.BuildEvent("ChatterBoxSessionStartReply", bodyMap), remoteClient.AgentId);
            }
        }

        private void DebugGridInstantMessage(GridInstantMessage im)
        {
            // Don't log any normal IMs (privacy!)
            if (m_debugEnabled && im.dialog != (byte)InstantMessageDialog.MessageFromAgent)
            {
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: fromGroup({0})", im.fromGroup ? "True" : "False");
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: Dialog({0})", ((InstantMessageDialog)im.dialog).ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: fromAgentID({0})", im.fromAgentID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: fromAgentName({0})", im.fromAgentName.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: imSessionID({0})", im.imSessionID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: message({0})", im.message.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: offline({0})", im.offline.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: toAgentID({0})", im.toAgentID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING]: IM: binaryBucket({0})", OpenMetaverse.Utils.BytesToHexString(im.binaryBucket, "BinaryBucket"));
            }
        }

        #region Client Tools

        /// <summary>
        /// Try to find an active IClientAPI reference for agentID giving preference to root connections
        /// </summary>
        private IClientAPI GetActiveClient(UUID agentID)
        {
            if (m_debugEnabled) m_log.WarnFormat("[GROUPS-MESSAGING]: Looking for local client {0}", agentID);

            IClientAPI child = null;

            // Try root avatar first
            foreach (Scene scene in m_sceneList)
            {
                ScenePresence sp = scene.GetScenePresence(agentID);
                if (sp != null)
                {
                    if (!sp.IsChildAgent)
                    {
                        if (m_debugEnabled) m_log.WarnFormat("[GROUPS-MESSAGING]: Found root agent for client : {0}", sp.ControllingClient.Name);
                        return sp.ControllingClient;
                    }
                    else
                    {
                        if (m_debugEnabled) m_log.WarnFormat("[GROUPS-MESSAGING]: Found child agent for client : {0}", sp.ControllingClient.Name);
                        child = sp.ControllingClient;
                    }
                }
            }

            // If we didn't find a root, then just return whichever child we found, or null if none
            if (child == null)
            {
                if (m_debugEnabled) m_log.WarnFormat("[GROUPS-MESSAGING]: Could not find local client for agent : {0}", agentID);
            }
            else
            {
                if (m_debugEnabled) m_log.WarnFormat("[GROUPS-MESSAGING]: Returning child agent for client : {0}", child.Name);
            }
            return child;
        }

        #endregion
    }
}
