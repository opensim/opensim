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
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Groups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GroupsMessagingModule")]
    public class GroupsMessagingModule : ISharedRegionModule, IGroupsMessagingModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_sceneList = new List<Scene>();
        private IPresenceService m_presenceService;

        private IMessageTransferModule m_msgTransferModule = null;
        private IUserManagement m_UserManagement = null;
        private IGroupsServicesConnector m_groupData = null;

        // Config Options
        private bool m_groupMessagingEnabled;
        private bool m_debugEnabled;

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

        private Dictionary<UUID, List<string>> m_groupsAgentsDroppedFromChatSession = new Dictionary<UUID, List<string>>();
        private Dictionary<UUID, List<string>> m_groupsAgentsInvitedToChatSession = new Dictionary<UUID, List<string>>();

        #region Region Module interfaceBase Members

        public void Initialise(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];

            if (groupsConfig == null)
                // Do not run this module by default.
                return;

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
                return;

            m_messageOnlineAgentsOnly = groupsConfig.GetBoolean("MessageOnlineUsersOnly", false);

            if (m_messageOnlineAgentsOnly)
            {
                m_usersOnlineCache = new ExpiringCache<UUID, PresenceInfo[]>();
            }
            else
            {
                m_log.Error("[Groups.Messaging]: GroupsMessagingModule V2 requires MessageOnlineUsersOnly = true");
                m_groupMessagingEnabled = false;
                return;
            }

            m_debugEnabled = groupsConfig.GetBoolean("MessagingDebugEnabled", m_debugEnabled);

            m_log.InfoFormat(
                "[Groups.Messaging]: GroupsMessagingModule enabled with MessageOnlineOnly = {0}, DebugEnabled = {1}",
                m_messageOnlineAgentsOnly, m_debugEnabled);
        }

        public void AddRegion(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;

            scene.RegisterModuleInterface<IGroupsMessagingModule>(this);
            m_sceneList.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
            scene.EventManager.OnMakeChildAgent += OnMakeChildAgent;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnClientLogin += OnClientLogin;

            scene.AddCommand(
                "Debug",
                this,
                "debug groups messaging verbose",
                "debug groups messaging verbose <true|false>",
                "This setting turns on very verbose groups messaging debugging",
                HandleDebugGroupsMessagingVerbose);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData = scene.RequestModuleInterface<IGroupsServicesConnector>();

            // No groups module, no groups messaging
            if (m_groupData == null)
            {
                m_log.Error("[Groups.Messaging]: Could not get IGroupsServicesConnector, GroupsMessagingModule is now disabled.");
                RemoveRegion(scene);
                return;
            }

            m_msgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

            // No message transfer module, no groups messaging
            if (m_msgTransferModule == null)
            {
                m_log.Error("[Groups.Messaging]: Could not get MessageTransferModule");
                RemoveRegion(scene);
                return;
            }

            m_UserManagement = scene.RequestModuleInterface<IUserManagement>();

            // No groups module, no groups messaging
            if (m_UserManagement == null)
            {
                m_log.Error("[Groups.Messaging]: Could not get IUserManagement, GroupsMessagingModule is now disabled.");
                RemoveRegion(scene);
                return;
            }

            if (m_presenceService == null)
                m_presenceService = scene.PresenceService;
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_sceneList.Remove(scene);
            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            scene.EventManager.OnClientLogin -= OnClientLogin;
            scene.UnregisterModuleInterface<IGroupsMessagingModule>(this);
        }

        public void Close()
        {
            if (!m_groupMessagingEnabled)
                return;

            if (m_debugEnabled) m_log.Debug("[Groups.Messaging]: Shutting down GroupsMessagingModule module.");

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
            get { return "Groups Messaging Module V2"; }
        }

        public void PostInitialise()
        {
            // NoOp
        }

        #endregion

        private void HandleDebugGroupsMessagingVerbose(object modules, string[] args)
        {
            if (args.Length < 5)
            {
                MainConsole.Instance.Output("Usage: debug groups messaging verbose <true|false>");
                return;
            }

            bool verbose = false;
            if (!bool.TryParse(args[4], out verbose))
            {
                MainConsole.Instance.Output("Usage: debug groups messaging verbose <true|false>");
                return;
            }

            m_debugEnabled = verbose;

            MainConsole.Instance.Output("{0} verbose logging set to {1}", null, Name, m_debugEnabled);
        }

        /// <summary>
        /// Not really needed, but does confirm that the group exists.
        /// </summary>
        public bool StartGroupChatSession(UUID agentID, UUID groupID)
        {
            if (m_debugEnabled)
                m_log.DebugFormat("[Groups.Messaging]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupRecord groupInfo = m_groupData.GetGroupRecord(agentID.ToString(), groupID, null);

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
            SendMessageToGroup(im, groupID, UUID.Zero, null);
        }

        public void SendMessageToGroup(
            GridInstantMessage im, UUID groupID, UUID sendingAgentForGroupCalls, Func<GroupMembersData, bool> sendCondition)
        {
            int requestStartTick = Environment.TickCount;

            UUID fromAgentID = new UUID(im.fromAgentID);

            // Unlike current XmlRpcGroups, Groups V2 can accept UUID.Zero when a perms check for the requesting agent
            // is not necessary.
            List<GroupMembersData> groupMembers = m_groupData.GetGroupMembers(UUID.Zero.ToString(), groupID);

            int groupMembersCount = groupMembers.Count;
            PresenceInfo[] onlineAgents = null;

            // In V2 we always only send to online members.
            // Sending to offline members is not an option.
            string[] t1 = groupMembers.ConvertAll<string>(gmd => gmd.AgentID.ToString()).ToArray();

            // We cache in order not to overwhelm the presence service on large grids with many groups.  This does
            // mean that members coming online will not see all group members until after m_usersOnlineCacheExpirySeconds has elapsed.
            // (assuming this is the same across all grid simulators).
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
//                        "[Groups.Messaging]: SendMessageToGroup called for group {0} with {1} visible members, {2} online",
//                        groupID, groupMembersCount, groupMembers.Count());

            im.imSessionID = groupID.Guid;
            im.fromGroup = true;
            IClientAPI thisClient = GetActiveClient(fromAgentID);
            if (thisClient != null)
            {
                im.RegionID = thisClient.Scene.RegionInfo.RegionID.Guid;
            }

            if ((im.binaryBucket == null) || (im.binaryBucket.Length == 0) || ((im.binaryBucket.Length == 1 && im.binaryBucket[0] == 0)))
            {
                ExtendedGroupRecord groupInfo = m_groupData.GetGroupRecord(UUID.Zero.ToString(), groupID, null);
                if (groupInfo != null)
                    im.binaryBucket = Util.StringToBytes256(groupInfo.GroupName);
            }

            // Send to self first of all
            im.toAgentID = im.fromAgentID;
            im.fromGroup = true;
            ProcessMessageFromGroupSession(im);

            List<UUID> regions = new List<UUID>();
            List<UUID> clientsAlreadySent = new List<UUID>();

            // Then send to everybody else
            foreach (GroupMembersData member in groupMembers)
            {
                if (member.AgentID.Guid == im.fromAgentID)
                    continue;

                if (clientsAlreadySent.Contains(member.AgentID))
                    continue;

                clientsAlreadySent.Add(member.AgentID);

                if (sendCondition != null)
                {
                    if (!sendCondition(member))
                    {
                        if (m_debugEnabled)
                            m_log.DebugFormat(
                                "[Groups.Messaging]: Not sending to {0} as they do not fulfill send condition",
                                 member.AgentID);

                        continue;
                    }
                }
                else if (hasAgentDroppedGroupChatSession(member.AgentID.ToString(), groupID))
                {
                    // Don't deliver messages to people who have dropped this session
                    if (m_debugEnabled)
                        m_log.DebugFormat("[Groups.Messaging]: {0} has dropped session, not delivering to them", member.AgentID);

                    continue;
                }

                im.toAgentID = member.AgentID.Guid;

                IClientAPI client = GetActiveClient(member.AgentID);
                if (client == null)
                {
                    // If they're not local, forward across the grid
                    // BUT do it only once per region, please! Sim would be even better!
                    if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Delivering to {0} via Grid", member.AgentID);

                    bool reallySend = true;
                    if (onlineAgents != null)
                    {
                        PresenceInfo presence = onlineAgents.First(p => p.UserID == member.AgentID.ToString());
                        if (regions.Contains(presence.RegionID))
                            reallySend = false;
                        else
                            regions.Add(presence.RegionID);
                    }

                    if (reallySend)
                    {
                        // We have to create a new IM structure because the transfer module
                        // uses async send
                        GridInstantMessage msg = new GridInstantMessage(im, true);
                        m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
                    }
                }
                else
                {
                    // Deliver locally, directly
                    if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Passing to ProcessMessageFromGroupSession to deliver to {0} locally", client.Name);

                    ProcessMessageFromGroupSession(im);
                }

            }

            if (m_debugEnabled)
                m_log.DebugFormat(
                    "[Groups.Messaging]: SendMessageToGroup for group {0} with {1} visible members, {2} online took {3}ms",
                    groupID, groupMembersCount, groupMembers.Count(), Environment.TickCount - requestStartTick);
        }

        #region SimGridEventHandlers

        void OnClientLogin(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: OnInstantMessage registered for {0}", client.Name);
        }

        private void OnNewClient(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: OnInstantMessage registered for {0}", client.Name);

            ResetAgentGroupChatSessions(client.AgentId.ToString());
        }

        void OnMakeRootAgent(ScenePresence sp)
        {
            sp.ControllingClient.OnInstantMessage += OnInstantMessage;
        }

        void OnMakeChildAgent(ScenePresence sp)
        {
            sp.ControllingClient.OnInstantMessage -= OnInstantMessage;
        }


        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // The instant message module will only deliver messages of dialog types:
            // MessageFromAgent, StartTyping, StopTyping, MessageFromObject
            //
            // Any other message type will not be delivered to a client by the
            // Instant Message Module

            UUID regionID = new UUID(msg.RegionID);
            if (m_debugEnabled)
            {
                m_log.DebugFormat("[Groups.Messaging]: {0} called, IM from region {1}",
                    System.Reflection.MethodBase.GetCurrentMethod().Name, regionID);

                DebugGridInstantMessage(msg);
            }

            // Incoming message from a group
            if ((msg.fromGroup == true) && (msg.dialog == (byte)InstantMessageDialog.SessionSend))
            {
                // We have to redistribute the message across all members of the group who are here
                // on this sim

                UUID GroupID = new UUID(msg.imSessionID);

                Scene aScene = m_sceneList[0];
                GridRegion regionOfOrigin = aScene.GridService.GetRegionByUUID(aScene.RegionInfo.ScopeID, regionID);

                List<GroupMembersData> groupMembers = m_groupData.GetGroupMembers(UUID.Zero.ToString(), GroupID);

                //if (m_debugEnabled)
                //    foreach (GroupMembersData m in groupMembers)
                //        m_log.DebugFormat("[Groups.Messaging]: member {0}", m.AgentID);

                foreach (Scene s in m_sceneList)
                {
                    s.ForEachScenePresence(sp =>
                        {
                            // If we got this via grid messaging, it's because the caller thinks
                            // that the root agent is here. We should only send the IM to root agents.
                            if (sp.IsChildAgent)
                                return;

                            GroupMembersData m = groupMembers.Find(gmd =>
                                {
                                    return gmd.AgentID == sp.UUID;
                                });
                            if (m.AgentID == UUID.Zero)
                            {
                                if (m_debugEnabled)
                                    m_log.DebugFormat("[Groups.Messaging]: skipping agent {0} because he is not a member of the group", sp.UUID);
                                return;
                            }

                            // Check if the user has an agent in the region where
                            // the IM came from, and if so, skip it, because the IM
                            // was already sent via that agent
                            if (regionOfOrigin != null)
                            {
                                AgentCircuitData aCircuit = s.AuthenticateHandler.GetAgentCircuitData(sp.UUID);
                                if (aCircuit != null)
                                {
                                    if (aCircuit.ChildrenCapSeeds.Keys.Contains(regionOfOrigin.RegionHandle))
                                    {
                                        if (m_debugEnabled)
                                            m_log.DebugFormat("[Groups.Messaging]: skipping agent {0} because he has an agent in region of origin", sp.UUID);
                                        return;
                                    }
                                    else
                                    {
                                        if (m_debugEnabled)
                                            m_log.DebugFormat("[Groups.Messaging]: not skipping agent {0}", sp.UUID);
                                    }
                                }
                            }

                            UUID AgentID = sp.UUID;
                            msg.toAgentID = AgentID.Guid;

                            if (!hasAgentDroppedGroupChatSession(AgentID.ToString(), GroupID))
                            {
                                if (!hasAgentBeenInvitedToGroupChatSession(AgentID.ToString(), GroupID))
                                    AddAgentToSession(AgentID, GroupID, msg);
                                else
                                {
                                    if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Passing to ProcessMessageFromGroupSession to deliver to {0} locally", sp.Name);

                                    ProcessMessageFromGroupSession(msg);
                                }
                            }
                        });

                }
            }
        }

        private void ProcessMessageFromGroupSession(GridInstantMessage msg)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Session message from {0} going to agent {1}", msg.fromAgentName, msg.toAgentID);

            UUID AgentID = new UUID(msg.fromAgentID);
            UUID GroupID = new UUID(msg.imSessionID);
            UUID toAgentID = new UUID(msg.toAgentID);

            switch (msg.dialog)
            {
                case (byte)InstantMessageDialog.SessionAdd:
                    AgentInvitedToGroupChatSession(AgentID.ToString(), GroupID);
                    break;

                case (byte)InstantMessageDialog.SessionDrop:
                    AgentDroppedFromGroupChatSession(AgentID.ToString(), GroupID);
                    break;

                case (byte)InstantMessageDialog.SessionSend:
                    // User hasn't dropped, so they're in the session,
                    // maybe we should deliver it.
                    IClientAPI client = GetActiveClient(new UUID(msg.toAgentID));
                    if (client != null)
                    {
                        // Deliver locally, directly
                        if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Delivering to {0} locally", client.Name);

                        if (!hasAgentDroppedGroupChatSession(toAgentID.ToString(), GroupID))
                        {
                            if (!hasAgentBeenInvitedToGroupChatSession(toAgentID.ToString(), GroupID))
                                // This actually sends the message too, so no need to resend it
                                // with client.SendInstantMessage
                                AddAgentToSession(toAgentID, GroupID, msg);
                            else
                                client.SendInstantMessage(msg);
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("[Groups.Messaging]: Received a message over the grid for a client that isn't here: {0}", msg.toAgentID);
                    }
                    break;

                default:
                    m_log.WarnFormat("[Groups.Messaging]: I don't know how to proccess a {0} message.", ((InstantMessageDialog)msg.dialog).ToString());
                    break;
            }
        }

        private void AddAgentToSession(UUID AgentID, UUID GroupID, GridInstantMessage msg)
        {
            // Agent not in session and hasn't dropped from session
            // Add them to the session for now, and Invite them
            AgentInvitedToGroupChatSession(AgentID.ToString(), GroupID);

            IClientAPI activeClient = GetActiveClient(AgentID);
            if (activeClient != null)
            {
                GroupRecord groupInfo = m_groupData.GetGroupRecord(UUID.Zero.ToString(), GroupID, null);
                if (groupInfo != null)
                {
                    if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Sending chatterbox invite instant message");

                    // Force? open the group session dialog???
                    // and simultanously deliver the message, so we don't need to do a seperate client.SendInstantMessage(msg);
                    IEventQueue eq = activeClient.Scene.RequestModuleInterface<IEventQueue>();
                    eq.ChatterboxInvitation(
                        GroupID
                        , groupInfo.GroupName
                        , new UUID(msg.fromAgentID)
                        , msg.message
                        , AgentID
                        , msg.fromAgentName
                        , msg.dialog
                        , msg.timestamp
                        , msg.offline == 1
                        , (int)msg.ParentEstateID
                        , msg.Position
                        , 1
                        , new UUID(msg.imSessionID)
                        , msg.fromGroup
                        , OpenMetaverse.Utils.StringToBytes(groupInfo.GroupName)
                        );

                    eq.ChatterBoxSessionAgentListUpdates(
                        new UUID(GroupID)
                        , AgentID
                        , new UUID(msg.toAgentID)
                        , false //canVoiceChat
                        , false //isModerator
                        , false //text mute
                        , true // Enter
                        );
                }
            }
        }

        #endregion


        #region ClientEvents
        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled)
            {
                m_log.DebugFormat("[Groups.Messaging]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

                DebugGridInstantMessage(im);
            }

            // Start group IM session
            if ((im.dialog == (byte)InstantMessageDialog.SessionGroupStart))
            {
                if (m_debugEnabled) m_log.InfoFormat("[Groups.Messaging]: imSessionID({0}) toAgentID({1})", im.imSessionID, im.toAgentID);

                UUID GroupID = new UUID(im.imSessionID);
                UUID AgentID = new UUID(im.fromAgentID);

                GroupRecord groupInfo = m_groupData.GetGroupRecord(UUID.Zero.ToString(), GroupID, null);

                if (groupInfo != null)
                {
                    AgentInvitedToGroupChatSession(AgentID.ToString(), GroupID);

                    ChatterBoxSessionStartReplyViaCaps(remoteClient, groupInfo.GroupName, GroupID);

                    IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();
                    queue.ChatterBoxSessionAgentListUpdates(
                        GroupID
                        , AgentID
                        , new UUID(im.toAgentID)
                        , false //canVoiceChat
                        , false //isModerator
                        , false //text mute
                        , true
                        );
                }
            }

            // Send a message from locally connected client to a group
            if ((im.dialog == (byte)InstantMessageDialog.SessionSend))
            {
                UUID GroupID = new UUID(im.imSessionID);
                UUID AgentID = new UUID(im.fromAgentID);

                if (m_debugEnabled)
                    m_log.DebugFormat("[Groups.Messaging]: Send message to session for group {0} with session ID {1}", GroupID, im.imSessionID.ToString());

                //If this agent is sending a message, then they want to be in the session
                AgentInvitedToGroupChatSession(AgentID.ToString(), GroupID);

                SendMessageToGroup(im, GroupID);
            }
        }

        #endregion

        void ChatterBoxSessionStartReplyViaCaps(IClientAPI remoteClient, string groupName, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

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
                m_log.WarnFormat("[Groups.Messaging]: IM: fromGroup({0})", im.fromGroup ? "True" : "False");
                m_log.WarnFormat("[Groups.Messaging]: IM: Dialog({0})", ((InstantMessageDialog)im.dialog).ToString());
                m_log.WarnFormat("[Groups.Messaging]: IM: fromAgentID({0})", im.fromAgentID.ToString());
                m_log.WarnFormat("[Groups.Messaging]: IM: fromAgentName({0})", im.fromAgentName.ToString());
                m_log.WarnFormat("[Groups.Messaging]: IM: imSessionID({0})", im.imSessionID.ToString());
                m_log.WarnFormat("[Groups.Messaging]: IM: message({0})", im.message.ToString());
                m_log.WarnFormat("[Groups.Messaging]: IM: offline({0})", im.offline.ToString());
                m_log.WarnFormat("[Groups.Messaging]: IM: toAgentID({0})", im.toAgentID.ToString());
                m_log.WarnFormat("[Groups.Messaging]: IM: binaryBucket({0})", OpenMetaverse.Utils.BytesToHexString(im.binaryBucket, "BinaryBucket"));
            }
        }

        #region Client Tools

        /// <summary>
        /// Try to find an active IClientAPI reference for agentID giving preference to root connections
        /// </summary>
        private IClientAPI GetActiveClient(UUID agentID)
        {
            if (m_debugEnabled) m_log.WarnFormat("[Groups.Messaging]: Looking for local client {0}", agentID);

            IClientAPI child = null;

            // Try root avatar first
            foreach (Scene scene in m_sceneList)
            {
                ScenePresence sp = scene.GetScenePresence(agentID);
                if (sp != null)
                {
                    if (!sp.IsChildAgent)
                    {
                        if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Found root agent for client : {0}", sp.ControllingClient.Name);
                        return sp.ControllingClient;
                    }
                    else
                    {
                        if (m_debugEnabled) m_log.DebugFormat("[Groups.Messaging]: Found child agent for client : {0}", sp.ControllingClient.Name);
                        child = sp.ControllingClient;
                    }
                }
            }

            // If we didn't find a root, then just return whichever child we found, or null if none
            if (child == null)
            {
                if (m_debugEnabled) m_log.WarnFormat("[Groups.Messaging]: Could not find local client for agent : {0}", agentID);
            }
            else
            {
                if (m_debugEnabled) m_log.WarnFormat("[Groups.Messaging]: Returning child agent for client : {0}", child.Name);
            }
            return child;
        }

        #endregion

        #region GroupSessionTracking

        public void ResetAgentGroupChatSessions(string agentID)
        {
            foreach (List<string> agentList in m_groupsAgentsDroppedFromChatSession.Values)
                agentList.Remove(agentID);

            foreach (List<string> agentList in m_groupsAgentsInvitedToChatSession.Values)
                agentList.Remove(agentID);
        }

        public bool hasAgentBeenInvitedToGroupChatSession(string agentID, UUID groupID)
        {
            // If we're  tracking this group, and we can find them in the tracking, then they've been invited
            return m_groupsAgentsInvitedToChatSession.ContainsKey(groupID)
                && m_groupsAgentsInvitedToChatSession[groupID].Contains(agentID);
        }

        public bool hasAgentDroppedGroupChatSession(string agentID, UUID groupID)
        {
            // If we're tracking drops for this group,
            // and we find them, well... then they've dropped
            return m_groupsAgentsDroppedFromChatSession.ContainsKey(groupID)
                && m_groupsAgentsDroppedFromChatSession[groupID].Contains(agentID);
        }

        public void AgentDroppedFromGroupChatSession(string agentID, UUID groupID)
        {
            if (m_groupsAgentsDroppedFromChatSession.ContainsKey(groupID))
            {
                // If not in dropped list, add
                if (!m_groupsAgentsDroppedFromChatSession[groupID].Contains(agentID))
                {
                    m_groupsAgentsDroppedFromChatSession[groupID].Add(agentID);
                }
            }
        }

        public void AgentInvitedToGroupChatSession(string agentID, UUID groupID)
        {
            // Add Session Status if it doesn't exist for this session
            CreateGroupChatSessionTracking(groupID);

            // If nessesary, remove from dropped list
            if (m_groupsAgentsDroppedFromChatSession[groupID].Contains(agentID))
            {
                m_groupsAgentsDroppedFromChatSession[groupID].Remove(agentID);
            }

            // Add to invited
            if (!m_groupsAgentsInvitedToChatSession[groupID].Contains(agentID))
                m_groupsAgentsInvitedToChatSession[groupID].Add(agentID);
        }

        private void CreateGroupChatSessionTracking(UUID groupID)
        {
            if (!m_groupsAgentsDroppedFromChatSession.ContainsKey(groupID))
            {
                m_groupsAgentsDroppedFromChatSession.Add(groupID, new List<string>());
                m_groupsAgentsInvitedToChatSession.Add(groupID, new List<string>());
            }

        }
        #endregion

    }
}
