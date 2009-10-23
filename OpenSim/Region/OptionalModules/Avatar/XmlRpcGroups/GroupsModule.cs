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
using System.Reflection;
using System.Timers;

using log4net;
using Mono.Addins;
using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.Framework.EventQueue;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Caps = OpenSim.Framework.Capabilities.Caps;
using DirFindFlags = OpenMetaverse.DirectoryManager.DirFindFlags;



namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class GroupsModule : ISharedRegionModule, IGroupsModule
    {
        /// <summary>
        /// ; To use this module, you must specify the following in your OpenSim.ini
        /// [GROUPS]
        /// Enabled = true
        /// 
        /// Module   = GroupsModule
        /// NoticesEnabled = true
        /// DebugEnabled   = true
        /// 
        /// GroupsServicesConnectorModule = XmlRpcGroupsServicesConnector
        /// XmlRpcServiceURL      = http://osflotsam.org/xmlrpc.php
        /// XmlRpcServiceReadKey  = 1234
        /// XmlRpcServiceWriteKey = 1234
        /// 
        /// MessagingModule  = GroupsMessagingModule
        /// MessagingEnabled = true
        /// 
        /// ; Disables HTTP Keep-Alive for Groups Module HTTP Requests, work around for
        /// ; a problem discovered on some Windows based region servers.  Only disable
        /// ; if you see a large number (dozens) of the following Exceptions:
        /// ; System.Net.WebException: The request was aborted: The request was canceled.
        ///
        /// XmlRpcDisableKeepAlive = false
        /// </summary>

        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_sceneList = new List<Scene>();

        private IMessageTransferModule m_msgTransferModule = null;
        
        private IGroupsServicesConnector m_groupData = null;

        class GroupRequestIDInfo
        {
            public GroupRequestID RequestID = new GroupRequestID();
            public DateTime LastUsedTMStamp = DateTime.MinValue;
        }
        private Dictionary<UUID, GroupRequestIDInfo> m_clientRequestIDInfo = new Dictionary<UUID, GroupRequestIDInfo>();
        private const int m_clientRequestIDFlushTimeOut = 300000; // Every 5 minutes
        private Timer m_clientRequestIDFlushTimer;


        // Configuration settings
        private bool m_groupsEnabled = false;
        private bool m_groupNoticesEnabled = true;
        private bool m_debugEnabled = true;

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
                m_groupsEnabled = groupsConfig.GetBoolean("Enabled", false);
                if (!m_groupsEnabled)
                {
                    return;
                }

                if (groupsConfig.GetString("Module", "Default") != Name)
                {
                    m_groupsEnabled = false;

                    return;
                }

                m_log.InfoFormat("[GROUPS]: Initializing {0}", this.Name);

                m_groupNoticesEnabled   = groupsConfig.GetBoolean("NoticesEnabled", true);
                m_debugEnabled          = groupsConfig.GetBoolean("DebugEnabled", true);

                m_clientRequestIDFlushTimer = new Timer();
                m_clientRequestIDFlushTimer.Interval = m_clientRequestIDFlushTimeOut;
                m_clientRequestIDFlushTimer.Elapsed += FlushClientRequestIDInfoCache;
                m_clientRequestIDFlushTimer.AutoReset = true;
                m_clientRequestIDFlushTimer.Start();
            }
        }

        void FlushClientRequestIDInfoCache(object sender, ElapsedEventArgs e)
        {
            lock (m_clientRequestIDInfo)
            {
                TimeSpan cacheTimeout = new TimeSpan(0,0, m_clientRequestIDFlushTimeOut / 1000);
                UUID[] CurrentKeys = new UUID[m_clientRequestIDInfo.Count];
                foreach (UUID key in CurrentKeys)
                {
                    if (m_clientRequestIDInfo.ContainsKey(key))
                    {
                        if (DateTime.Now - m_clientRequestIDInfo[key].LastUsedTMStamp > cacheTimeout)
                        {
                            m_clientRequestIDInfo.Remove(key);
                        }
                    }
                }
            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_groupsEnabled)
                scene.RegisterModuleInterface<IGroupsModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            if (m_groupData == null)
            {
                m_groupData = scene.RequestModuleInterface<IGroupsServicesConnector>();

                // No Groups Service Connector, then nothing works...
                if (m_groupData == null)
                {
                    m_groupsEnabled = false;
                    m_log.Error("[GROUPS]: Could not get IGroupsServicesConnector");
                    Close();
                    return;
                }
            }

            if (m_msgTransferModule == null)
            {
                m_msgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

                // No message transfer module, no notices, group invites, rejects, ejects, etc
                if (m_msgTransferModule == null)
                {
                    m_groupsEnabled = false;
                    m_log.Error("[GROUPS]: Could not get MessageTransferModule");
                    Close();
                    return;
                }
            }

            lock (m_sceneList)
            {
                m_sceneList.Add(scene);
            }

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

            // The InstantMessageModule itself doesn't do this, 
            // so lets see if things explode if we don't do it
            // scene.EventManager.OnClientClosed += OnClientClosed;

        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            lock (m_sceneList)
            {
                m_sceneList.Remove(scene);
            }
        }

        public void Close()
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) m_log.Debug("[GROUPS]: Shutting down Groups module.");

            m_clientRequestIDFlushTimer.Stop();
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "GroupsModule"; }
        }

        #endregion

        #region ISharedRegionModule Members

        public void PostInitialise()
        {
            // NoOp
        }

        #endregion

        #region EventHandlers
        private void OnNewClient(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            client.OnUUIDGroupNameRequest += HandleUUIDGroupNameRequest;
            client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
            client.OnDirFindQuery += OnDirFindQuery;
            client.OnRequestAvatarProperties += OnRequestAvatarProperties;

            // Used for Notices and Group Invites/Accept/Reject
            client.OnInstantMessage += OnInstantMessage;

            lock (m_clientRequestIDInfo)
            {
                if (m_clientRequestIDInfo.ContainsKey(client.AgentId))
                {
                    // flush any old RequestID information
                    m_clientRequestIDInfo.Remove(client.AgentId);
                }
            }
            SendAgentGroupDataUpdate(client, client.AgentId);
        }

        private void OnRequestAvatarProperties(IClientAPI remoteClient, UUID avatarID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            //GroupMembershipData[] avatarGroups = m_groupData.GetAgentGroupMemberships(GetClientGroupRequestID(remoteClient), avatarID).ToArray();
            GroupMembershipData[] avatarGroups = GetProfileListedGroupMemberships(remoteClient, avatarID);
            remoteClient.SendAvatarGroupsReply(avatarID, avatarGroups);
        }

        /*
         * This becomes very problematic in a shared module.  In a shared module you may have more then one
         * reference to IClientAPI's, one for 0 or 1 root connections, and 0 or more child connections.
         * The OnClientClosed event does not provide anything to indicate which one of those should be closed
         * nor does it provide what scene it was from so that the specific reference can be looked up.
         * The InstantMessageModule.cs does not currently worry about unregistering the handles, 
         * and it should be an issue, since it's the client that references us not the other way around
         * , so as long as we don't keep a reference to the client laying around, the client can still be GC'ed
        private void OnClientClosed(UUID AgentId)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            lock (m_ActiveClients)
            {
                if (m_ActiveClients.ContainsKey(AgentId))
                {
                    IClientAPI client = m_ActiveClients[AgentId];
                    client.OnUUIDGroupNameRequest -= HandleUUIDGroupNameRequest;
                    client.OnAgentDataUpdateRequest -= OnAgentDataUpdateRequest;
                    client.OnDirFindQuery -= OnDirFindQuery;
                    client.OnInstantMessage -= OnInstantMessage;

                    m_ActiveClients.Remove(AgentId);
                }
                else
                {
                    if (m_debugEnabled) m_log.WarnFormat("[GROUPS]: Client closed that wasn't registered here.");
                }

                
            }
        }
        */


        void OnDirFindQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, int queryStart)
        {
            if (((DirFindFlags)queryFlags & DirFindFlags.Groups) == DirFindFlags.Groups)
            {
                if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called with queryText({1}) queryFlags({2}) queryStart({3})", System.Reflection.MethodBase.GetCurrentMethod().Name, queryText, (DirFindFlags)queryFlags, queryStart);

                // TODO: This currently ignores pretty much all the query flags including Mature and sort order
                remoteClient.SendDirGroupsReply(queryID, m_groupData.FindGroups(GetClientGroupRequestID(remoteClient), queryText).ToArray());
            }
            
        }

        private void OnAgentDataUpdateRequest(IClientAPI remoteClient, UUID dataForAgentID, UUID sessionID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            UUID activeGroupID = UUID.Zero;
            string activeGroupTitle = string.Empty;
            string activeGroupName = string.Empty;
            ulong activeGroupPowers  = (ulong)GroupPowers.None;

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(GetClientGroupRequestID(remoteClient), dataForAgentID);
            if (membership != null)
            {
                activeGroupID = membership.GroupID;
                activeGroupTitle = membership.GroupTitle;
                activeGroupPowers = membership.GroupPowers;
            }

            SendAgentDataUpdate(remoteClient, dataForAgentID, activeGroupID, activeGroupName, activeGroupPowers, activeGroupTitle);

            SendScenePresenceUpdate(dataForAgentID, activeGroupTitle);
        }

        private void HandleUUIDGroupNameRequest(UUID GroupID,IClientAPI remoteClient)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string GroupName;
            
            GroupRecord group = m_groupData.GetGroupRecord(GetClientGroupRequestID(remoteClient), GroupID, null);
            if (group != null)
            {
                GroupName = group.GroupName;
            }
            else
            {
                GroupName = "Unknown";
            }

            remoteClient.SendGroupNameReply(GroupID, GroupName);
        }

        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Group invitations
            if ((im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept) || (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline))
            {
                UUID inviteID = new UUID(im.imSessionID);
                GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite(GetClientGroupRequestID(remoteClient), inviteID);

                if (inviteInfo == null)
                {
                    if (m_debugEnabled) m_log.WarnFormat("[GROUPS]: Received an Invite IM for an invite that does not exist {0}.", inviteID);
                    return;
                }

                if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Invite is for Agent {0} to Group {1}.", inviteInfo.AgentID, inviteInfo.GroupID);

                UUID fromAgentID = new UUID(im.fromAgentID);
                if ((inviteInfo != null) && (fromAgentID == inviteInfo.AgentID))
                {
                    // Accept
                    if (im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept)
                    {
                        if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Received an accept invite notice.");

                        // and the sessionid is the role
                        m_groupData.AddAgentToGroup(GetClientGroupRequestID(remoteClient), inviteInfo.AgentID, inviteInfo.GroupID, inviteInfo.RoleID);

                        GridInstantMessage msg = new GridInstantMessage();
                        msg.imSessionID = UUID.Zero.Guid;
                        msg.fromAgentID = UUID.Zero.Guid;
                        msg.toAgentID = inviteInfo.AgentID.Guid;
                        msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                        msg.fromAgentName = "Groups";
                        msg.message = string.Format("You have been added to the group.");
                        msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.MessageBox;
                        msg.fromGroup = false;
                        msg.offline = (byte)0;
                        msg.ParentEstateID = 0;
                        msg.Position = Vector3.Zero;
                        msg.RegionID = UUID.Zero.Guid;
                        msg.binaryBucket = new byte[0];

                        OutgoingInstantMessage(msg, inviteInfo.AgentID);

                        UpdateAllClientsWithGroupInfo(inviteInfo.AgentID);

                        // TODO: If the inviter is still online, they need an agent dataupdate 
                        // and maybe group membership updates for the invitee

                        m_groupData.RemoveAgentToGroupInvite(GetClientGroupRequestID(remoteClient), inviteID);
                    }

                    // Reject
                    if (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline)
                    {
                        if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Received a reject invite notice.");
                        m_groupData.RemoveAgentToGroupInvite(GetClientGroupRequestID(remoteClient), inviteID);
                    }
                }
            }

            // Group notices
            if ((im.dialog == (byte)InstantMessageDialog.GroupNotice))
            {
                if (!m_groupNoticesEnabled)
                {
                    return;
                }

                UUID GroupID = new UUID(im.toAgentID);
                if (m_groupData.GetGroupRecord(GetClientGroupRequestID(remoteClient), GroupID, null) != null)
                {
                    UUID NoticeID = UUID.Random();
                    string Subject = im.message.Substring(0, im.message.IndexOf('|'));
                    string Message = im.message.Substring(Subject.Length + 1);

                    byte[] bucket;

                    if ((im.binaryBucket.Length == 1) && (im.binaryBucket[0] == 0))
                    {
                        bucket = new byte[19];
                        bucket[0] = 0; //dunno
                        bucket[1] = 0; //dunno
                        GroupID.ToBytes(bucket, 2);
                        bucket[18] = 0; //dunno
                    }
                    else
                    {
                        string binBucket = OpenMetaverse.Utils.BytesToString(im.binaryBucket);
                        binBucket = binBucket.Remove(0, 14).Trim();
                        if (m_debugEnabled)
                        {
                            m_log.WarnFormat("I don't understand a group notice binary bucket of: {0}", binBucket);

                            OSDMap binBucketOSD = (OSDMap)OSDParser.DeserializeLLSDXml(binBucket);
                            
                            foreach (string key in binBucketOSD.Keys)
                            {
                                if (binBucketOSD.ContainsKey(key))
                                {
                                    m_log.WarnFormat("{0}: {1}", key, binBucketOSD[key].ToString());
                                }
                            }
                        }
   
                        // treat as if no attachment
                        bucket = new byte[19];
                        bucket[0] = 0; //dunno
                        bucket[1] = 0; //dunno
                        GroupID.ToBytes(bucket, 2);
                        bucket[18] = 0; //dunno
                    }

                    
                    m_groupData.AddGroupNotice(GetClientGroupRequestID(remoteClient), GroupID, NoticeID, im.fromAgentName, Subject, Message, bucket);
                    if (OnNewGroupNotice != null)
                    {
                        OnNewGroupNotice(GroupID, NoticeID);
                    }

                    // Send notice out to everyone that wants notices
                    foreach (GroupMembersData member in m_groupData.GetGroupMembers(GetClientGroupRequestID(remoteClient), GroupID))
                    {
                         if (m_debugEnabled)
                        {
                            UserProfileData targetUserProfile = m_sceneList[0].CommsManager.UserService.GetUserProfile(member.AgentID);
                            if (targetUserProfile != null)
                            {
                                m_log.DebugFormat("[GROUPS]: Prepping group notice {0} for agent: {1} who Accepts Notices ({2})", NoticeID, targetUserProfile.Name, member.AcceptNotices);
                            }
                            else
                            {
                                m_log.DebugFormat("[GROUPS]: Prepping group notice {0} for agent: {1} who Accepts Notices ({2})", NoticeID, member.AgentID, member.AcceptNotices);
                            }
                        }

                       if (member.AcceptNotices)
                        {
                            // Build notice IIM
                            GridInstantMessage msg = CreateGroupNoticeIM(UUID.Zero, NoticeID, (byte)OpenMetaverse.InstantMessageDialog.GroupNotice);

                            msg.toAgentID = member.AgentID.Guid;
                            OutgoingInstantMessage(msg, member.AgentID);
                        }
                    }
                }
            }
            
            // Interop, received special 210 code for ejecting a group member
            // this only works within the comms servers domain, and won't work hypergrid
            // TODO:FIXME: Use a presense server of some kind to find out where the 
            // client actually is, and try contacting that region directly to notify them,
            // or provide the notification via xmlrpc update queue
            if ((im.dialog == 210))
            {
                // This is sent from the region that the ejectee was ejected from
                // if it's being delivered here, then the ejectee is here
                // so we need to send local updates to the agent.

                UUID ejecteeID = new UUID(im.toAgentID);

                im.dialog = (byte)InstantMessageDialog.MessageFromAgent;
                OutgoingInstantMessage(im, ejecteeID);

                IClientAPI ejectee = GetActiveClient(ejecteeID);
                if (ejectee != null)
                {
                    UUID groupID = new UUID(im.fromAgentID);
                    ejectee.SendAgentDropGroup(groupID);
                }
            }
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Trigger the above event handler
            OnInstantMessage(null, msg);

            // If a message from a group arrives here, it may need to be forwarded to a local client
            if (msg.fromGroup == true)
            {
                switch (msg.dialog)
                {
                    case (byte)InstantMessageDialog.GroupInvitation:
                    case (byte)InstantMessageDialog.GroupNotice:
                        UUID toAgentID = new UUID(msg.toAgentID);
                        IClientAPI localClient = GetActiveClient(toAgentID);
                        if (localClient != null)
                        {
                            localClient.SendInstantMessage(msg);
                        }
                        break;
                }
            }
        }

        #endregion

        #region IGroupsModule Members

        public event NewGroupNotice OnNewGroupNotice;

        public GroupRecord GetGroupRecord(UUID GroupID)
        {
            return m_groupData.GetGroupRecord(null, GroupID, null);
        }

        public void ActivateGroup(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentActiveGroup(GetClientGroupRequestID(remoteClient), remoteClient.AgentId, groupID);

            // Changing active group changes title, active powers, all kinds of things
            // anyone who is in any region that can see this client, should probably be 
            // updated with new group info.  At a minimum, they should get ScenePresence
            // updated with new title.
            UpdateAllClientsWithGroupInfo(remoteClient.AgentId);
        }

        /// <summary>
        /// Get the Role Titles for an Agent, for a specific group
        /// </summary>
        public List<GroupTitlesData> GroupTitlesRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupRequestID grID = GetClientGroupRequestID(remoteClient);

            List<GroupRolesData> agentRoles = m_groupData.GetAgentGroupRoles(grID, remoteClient.AgentId, groupID);
            GroupMembershipData agentMembership = m_groupData.GetAgentGroupMembership(grID, remoteClient.AgentId, groupID);

            List<GroupTitlesData> titles = new List<GroupTitlesData>();
            foreach (GroupRolesData role in agentRoles)
            {
                GroupTitlesData title = new GroupTitlesData();
                title.Name = role.Name;
                if (agentMembership != null)
                {
                    title.Selected = agentMembership.ActiveRole == role.RoleID;
                }
                title.UUID = role.RoleID;

                titles.Add(title);
            }

            return titles;
        }

        public List<GroupMembersData> GroupMembersRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupMembersData> data = m_groupData.GetGroupMembers(GetClientGroupRequestID(remoteClient), groupID);

            return data;

        }

        public List<GroupRolesData> GroupRoleDataRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> data = m_groupData.GetGroupRoles(GetClientGroupRequestID(remoteClient), groupID);

            return data;

        }

        public List<GroupRoleMembersData> GroupRoleMembersRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRoleMembersData> data = m_groupData.GetGroupRoleMembers(GetClientGroupRequestID(remoteClient), groupID);

            return data;


        }

        public GroupProfileData GroupProfileRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupProfileData profile = new GroupProfileData();

            GroupRequestID grID = GetClientGroupRequestID(remoteClient);

            GroupRecord groupInfo = m_groupData.GetGroupRecord(GetClientGroupRequestID(remoteClient), groupID, null);
            if (groupInfo != null)
            {
                profile.AllowPublish = groupInfo.AllowPublish;
                profile.Charter = groupInfo.Charter;
                profile.FounderID = groupInfo.FounderID;
                profile.GroupID = groupID;
                profile.GroupMembershipCount = m_groupData.GetGroupMembers(grID, groupID).Count;
                profile.GroupRolesCount = m_groupData.GetGroupRoles(grID, groupID).Count;
                profile.InsigniaID = groupInfo.GroupPicture;
                profile.MaturePublish = groupInfo.MaturePublish;
                profile.MembershipFee = groupInfo.MembershipFee;
                profile.Money = 0; // TODO: Get this from the currency server?
                profile.Name = groupInfo.GroupName;
                profile.OpenEnrollment = groupInfo.OpenEnrollment;
                profile.OwnerRole = groupInfo.OwnerRoleID;
                profile.ShowInList = groupInfo.ShowInList;
            }

            GroupMembershipData memberInfo = m_groupData.GetAgentGroupMembership(grID, remoteClient.AgentId, groupID);
            if (memberInfo != null)
            {
                profile.MemberTitle = memberInfo.GroupTitle;
                profile.PowersMask = memberInfo.GroupPowers;
            }

            return profile;
        }

        public GroupMembershipData[] GetMembershipData(UUID agentID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMemberships(null, agentID).ToArray();
        }

        public GroupMembershipData GetMembershipData(UUID groupID, UUID agentID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMembership(null, agentID, groupID);
        }

        public void UpdateGroupInfo(IClientAPI remoteClient, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: Security Check?

            m_groupData.UpdateGroup(GetClientGroupRequestID(remoteClient), groupID, charter, showInList, insigniaID, membershipFee, openEnrollment, allowPublish, maturePublish);
        }

        public void SetGroupAcceptNotices(IClientAPI remoteClient, UUID groupID, bool acceptNotices, bool listInProfile)
        {
            // TODO: Security Check?
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentGroupInfo(GetClientGroupRequestID(remoteClient), remoteClient.AgentId, groupID, acceptNotices, listInProfile);
        }

        public UUID CreateGroup(IClientAPI remoteClient, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupRequestID grID = GetClientGroupRequestID(remoteClient);

            if (m_groupData.GetGroupRecord(grID, UUID.Zero, name) != null)
            {
                remoteClient.SendCreateGroupReply(UUID.Zero, false, "A group with the same name already exists.");
                return UUID.Zero;
            }
            // is there is a money module present ?
            IMoneyModule money=remoteClient.Scene.RequestModuleInterface<IMoneyModule>();
            if (money != null)
            {
                // do the transaction, that is if the agent has got sufficient funds
                if (!money.GroupCreationCovered(remoteClient)) {
                    remoteClient.SendCreateGroupReply(UUID.Zero, false, "You have got issuficient funds to create a group.");
                    return UUID.Zero;
                }
                money.ApplyGroupCreationCharge(remoteClient.AgentId);
            }
            UUID groupID = m_groupData.CreateGroup(grID, name, charter, showInList, insigniaID, membershipFee, openEnrollment, allowPublish, maturePublish, remoteClient.AgentId);

            remoteClient.SendCreateGroupReply(groupID, true, "Group created successfullly");

            // Update the founder with new group information.
            SendAgentGroupDataUpdate(remoteClient, remoteClient.AgentId);

            return groupID;
        }

        public GroupNoticeData[] GroupNoticesListRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // ToDo: check if agent is a member of group and is allowed to see notices?

            return m_groupData.GetGroupNotices(GetClientGroupRequestID(remoteClient), groupID).ToArray();
        }

        /// <summary>
        /// Get the title of the agent's current role.
        /// </summary>
        public string GetGroupTitle(UUID avatarID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(null, avatarID);
            if (membership != null)
            {
                return membership.GroupTitle;
            } 
            return string.Empty;
        }

        /// <summary>
        /// Change the current Active Group Role for Agent
        /// </summary>
        public void GroupTitleUpdate(IClientAPI remoteClient, UUID groupID, UUID titleRoleID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentActiveGroupRole(GetClientGroupRequestID(remoteClient), remoteClient.AgentId, groupID, titleRoleID);

            // TODO: Not sure what all is needed here, but if the active group role change is for the group
            // the client currently has set active, then we need to do a scene presence update too
            // if (m_groupData.GetAgentActiveMembership(remoteClient.AgentId).GroupID == GroupID)
                
            UpdateAllClientsWithGroupInfo(remoteClient.AgentId);
        }


        public void GroupRoleUpdate(IClientAPI remoteClient, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, byte updateType)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Security Checks are handled in the Groups Service.

            GroupRequestID grID = GetClientGroupRequestID(remoteClient);

            switch ((OpenMetaverse.GroupRoleUpdate)updateType)
            {
                case OpenMetaverse.GroupRoleUpdate.Create:
                    m_groupData.AddGroupRole(grID, groupID, UUID.Random(), name, description, title, powers);
                    break;

                case OpenMetaverse.GroupRoleUpdate.Delete:
                    m_groupData.RemoveGroupRole(grID, groupID, roleID);
                    break;

                case OpenMetaverse.GroupRoleUpdate.UpdateAll:
                case OpenMetaverse.GroupRoleUpdate.UpdateData:
                case OpenMetaverse.GroupRoleUpdate.UpdatePowers:
                    if (m_debugEnabled)
                    {
                        GroupPowers gp = (GroupPowers)powers;
                        m_log.DebugFormat("[GROUPS]: Role ({0}) updated with Powers ({1}) ({2})", name, powers.ToString(), gp.ToString());
                    }
                    m_groupData.UpdateGroupRole(grID, groupID, roleID, name, description, title, powers);
                    break;

                case OpenMetaverse.GroupRoleUpdate.NoUpdate:
                default:
                    // No Op
                    break;

            }

            // TODO: This update really should send out updates for everyone in the role that just got changed.
            SendAgentGroupDataUpdate(remoteClient, remoteClient.AgentId);
        }

        public void GroupRoleChanges(IClientAPI remoteClient, UUID groupID, UUID roleID, UUID memberID, uint changes)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            // Todo: Security check

            GroupRequestID grID = GetClientGroupRequestID(remoteClient);

            switch (changes)
            {
                case 0:
                    // Add
                    m_groupData.AddAgentToGroupRole(grID, memberID, groupID, roleID);

                    break;
                case 1:
                    // Remove
                    m_groupData.RemoveAgentFromGroupRole(grID, memberID, groupID, roleID);
                    
                    break;
                default:
                    m_log.ErrorFormat("[GROUPS]: {0} does not understand changes == {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, changes);
                    break;
            }

            // TODO: This update really should send out updates for everyone in the role that just got changed.
            SendAgentGroupDataUpdate(remoteClient, remoteClient.AgentId);
        }

        public void GroupNoticeRequest(IClientAPI remoteClient, UUID groupNoticeID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupRequestID grID = GetClientGroupRequestID(remoteClient);

            GroupNoticeInfo data = m_groupData.GetGroupNotice(grID, groupNoticeID);

            if (data != null)
            {
                GroupRecord groupInfo = m_groupData.GetGroupRecord(grID, data.GroupID, null);

                GridInstantMessage msg = new GridInstantMessage();
                msg.imSessionID = UUID.Zero.Guid;
                msg.fromAgentID = data.GroupID.Guid;
                msg.toAgentID = remoteClient.AgentId.Guid;
                msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                msg.fromAgentName = "Group Notice : " + groupInfo == null ? "Unknown" : groupInfo.GroupName;
                msg.message = data.noticeData.Subject + "|" + data.Message;
                msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupNoticeRequested;
                msg.fromGroup = true;
                msg.offline = (byte)0;
                msg.ParentEstateID = 0;
                msg.Position = Vector3.Zero;
                msg.RegionID = UUID.Zero.Guid;
                msg.binaryBucket = data.BinaryBucket;

                OutgoingInstantMessage(msg, remoteClient.AgentId);
            }

        }

        public GridInstantMessage CreateGroupNoticeIM(UUID agentID, UUID groupNoticeID, byte dialog)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GridInstantMessage msg = new GridInstantMessage();
            msg.imSessionID = UUID.Zero.Guid;
            msg.toAgentID = agentID.Guid;
            msg.dialog = dialog;
            // msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupNotice;
            msg.fromGroup = true;
            msg.offline = (byte)1; // Allow this message to be stored for offline use
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = UUID.Zero.Guid;

            GroupNoticeInfo info = m_groupData.GetGroupNotice(null, groupNoticeID);
            if (info != null)
            {
                msg.fromAgentID = info.GroupID.Guid;
                msg.timestamp = info.noticeData.Timestamp;
                msg.fromAgentName = info.noticeData.FromName;
                msg.message = info.noticeData.Subject + "|" + info.Message;
                msg.binaryBucket = info.BinaryBucket;
            }
            else
            {
                if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Group Notice {0} not found, composing empty message.", groupNoticeID);
                msg.fromAgentID = UUID.Zero.Guid;
                msg.timestamp = (uint)Util.UnixTimeSinceEpoch(); ;
                msg.fromAgentName = string.Empty;
                msg.message = string.Empty;
                msg.binaryBucket = new byte[0];
            }

            return msg;
        }

        public void SendAgentGroupDataUpdate(IClientAPI remoteClient)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Send agent information about his groups
            SendAgentGroupDataUpdate(remoteClient, remoteClient.AgentId);
        }

        public void JoinGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Should check to see if OpenEnrollment, or if there's an outstanding invitation
            m_groupData.AddAgentToGroup(GetClientGroupRequestID(remoteClient), remoteClient.AgentId, groupID, UUID.Zero);

            remoteClient.SendJoinGroupReply(groupID, true);

            // Should this send updates to everyone in the group?
            SendAgentGroupDataUpdate(remoteClient, remoteClient.AgentId);
        }

        public void LeaveGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.RemoveAgentFromGroup(GetClientGroupRequestID(remoteClient), remoteClient.AgentId, groupID);

            remoteClient.SendLeaveGroupReply(groupID, true);

            remoteClient.SendAgentDropGroup(groupID);

            // SL sends out notifcations to the group messaging session that the person has left
            // Should this also update everyone who is in the group?
            SendAgentGroupDataUpdate(remoteClient, remoteClient.AgentId);
        }

        public void EjectGroupMemberRequest(IClientAPI remoteClient, UUID groupID, UUID ejecteeID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupRequestID grID = GetClientGroupRequestID(remoteClient);

            // Todo: Security check?
            m_groupData.RemoveAgentFromGroup(grID, ejecteeID, groupID);

            remoteClient.SendEjectGroupMemberReply(remoteClient.AgentId, groupID, true);

            GroupRecord groupInfo = m_groupData.GetGroupRecord(grID, groupID, null);
            UserProfileData userProfile = m_sceneList[0].CommsManager.UserService.GetUserProfile(ejecteeID);

            if ((groupInfo == null) || (userProfile == null))
            {
                return;
            }
            

            // Send Message to Ejectee
            GridInstantMessage msg = new GridInstantMessage();
            
            msg.imSessionID = UUID.Zero.Guid;
            msg.fromAgentID = remoteClient.AgentId.Guid;
            // msg.fromAgentID = info.GroupID;
            msg.toAgentID = ejecteeID.Guid;
            //msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
            msg.timestamp = 0;
            msg.fromAgentName = remoteClient.Name;
            msg.message = string.Format("You have been ejected from '{1}' by {0}.", remoteClient.Name, groupInfo.GroupName);
            msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.MessageFromAgent;
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = remoteClient.Scene.RegionInfo.RegionID.Guid;
            msg.binaryBucket = new byte[0];
            OutgoingInstantMessage(msg, ejecteeID);


            // Message to ejector
            // Interop, received special 210 code for ejecting a group member
            // this only works within the comms servers domain, and won't work hypergrid
            // TODO:FIXME: Use a presense server of some kind to find out where the 
            // client actually is, and try contacting that region directly to notify them,
            // or provide the notification via xmlrpc update queue

            msg = new GridInstantMessage();
            msg.imSessionID = UUID.Zero.Guid;
            msg.fromAgentID = remoteClient.AgentId.Guid;
            msg.toAgentID = remoteClient.AgentId.Guid;
            msg.timestamp = 0;
            msg.fromAgentName = remoteClient.Name;
            if (userProfile != null)
            {
                msg.message = string.Format("{2} has been ejected from '{1}' by {0}.", remoteClient.Name, groupInfo.GroupName, userProfile.Name);
            }
            else
            {
                msg.message = string.Format("{2} has been ejected from '{1}' by {0}.", remoteClient.Name, groupInfo.GroupName, "Unknown member");
            }
            msg.dialog = (byte)210; //interop
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = remoteClient.Scene.RegionInfo.RegionID.Guid;
            msg.binaryBucket = new byte[0];
            OutgoingInstantMessage(msg, remoteClient.AgentId);


            // SL sends out messages to everyone in the group
            // Who all should receive updates and what should they be updated with?
            UpdateAllClientsWithGroupInfo(ejecteeID);
        }

        public void InviteGroupRequest(IClientAPI remoteClient, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Todo: Security check, probably also want to send some kind of notification
            UUID InviteID = UUID.Random();
            GroupRequestID grid = GetClientGroupRequestID(remoteClient);

            m_groupData.AddAgentToGroupInvite(grid, InviteID, groupID, roleID, invitedAgentID);

            // Check to see if the invite went through, if it did not then it's possible
            // the remoteClient did not validate or did not have permission to invite.
            GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite(grid, InviteID);

            if (inviteInfo != null)
            {
                if (m_msgTransferModule != null)
                {
                    Guid inviteUUID = InviteID.Guid;

                    GridInstantMessage msg = new GridInstantMessage();

                    msg.imSessionID = inviteUUID;

                    // msg.fromAgentID = remoteClient.AgentId.Guid;
                    msg.fromAgentID = groupID.Guid;
                    msg.toAgentID = invitedAgentID.Guid;
                    //msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                    msg.timestamp = 0;
                    msg.fromAgentName = remoteClient.Name;
                    msg.message = string.Format("{0} has invited you to join a group. There is no cost to join this group.", remoteClient.Name);
                    msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupInvitation;
                    msg.fromGroup = true;
                    msg.offline = (byte)0;
                    msg.ParentEstateID = 0;
                    msg.Position = Vector3.Zero;
                    msg.RegionID = remoteClient.Scene.RegionInfo.RegionID.Guid;
                    msg.binaryBucket = new byte[20];

                    OutgoingInstantMessage(msg, invitedAgentID);
                }
            }
        }

        #endregion

        #region Client/Update Tools

        /// <summary>
        /// Try to find an active IClientAPI reference for agentID giving preference to root connections
        /// </summary>
        private IClientAPI GetActiveClient(UUID agentID)
        {
            IClientAPI child = null;

            // Try root avatar first
            foreach (Scene scene in m_sceneList)
            {
                if (scene.Entities.ContainsKey(agentID) &&
                        scene.Entities[agentID] is ScenePresence)
                {
                    ScenePresence user = (ScenePresence)scene.Entities[agentID];
                    if (!user.IsChildAgent)
                    {
                        return user.ControllingClient;
                    }
                    else
                    {
                        child = user.ControllingClient;
                    }
                }
            }

            // If we didn't find a root, then just return whichever child we found, or null if none
            return child;
        }

        private GroupRequestID GetClientGroupRequestID(IClientAPI client)
        {
            if (client == null)
            {
                return new GroupRequestID();
            }

            lock (m_clientRequestIDInfo)
            {
                if (!m_clientRequestIDInfo.ContainsKey(client.AgentId))
                {
                    GroupRequestIDInfo info = new GroupRequestIDInfo();
                    info.RequestID.AgentID = client.AgentId;
                    info.RequestID.SessionID = client.SessionId;

                    UserProfileData userProfile = m_sceneList[0].CommsManager.UserService.GetUserProfile(client.AgentId);
                    if (userProfile == null)
                    {
                        // This should be impossible.  If I've been passed a reference to a client
                        // that client should be registered with the UserService.  So something
                        // is horribly wrong somewhere.

                        m_log.WarnFormat("[GROUPS]: Could not find a user profile for {0} / {1}", client.Name, client.AgentId);

                        // Default to local user service and hope for the best?
                        info.RequestID.UserServiceURL = m_sceneList[0].CommsManager.NetworkServersInfo.UserURL;

                    }
                    else if (userProfile is ForeignUserProfileData)
                    {
                        // They aren't from around here
                        ForeignUserProfileData fupd = (ForeignUserProfileData)userProfile;
                        info.RequestID.UserServiceURL = fupd.UserServerURI;
                    }
                    else
                    {
                        // They're a local user, use this:
                        info.RequestID.UserServiceURL = m_sceneList[0].CommsManager.NetworkServersInfo.UserURL;
                    }

                    m_clientRequestIDInfo.Add(client.AgentId, info);
                }

                m_clientRequestIDInfo[client.AgentId].LastUsedTMStamp = DateTime.Now;

                return m_clientRequestIDInfo[client.AgentId].RequestID;
            }

            return new GroupRequestID();
        }

        /// <summary>
        /// Send 'remoteClient' the group membership 'data' for agent 'dataForAgentID'.
        /// </summary>
        private void SendGroupMembershipInfoViaCaps(IClientAPI remoteClient, UUID dataForAgentID, GroupMembershipData[] data)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDArray AgentData = new OSDArray(1);
            OSDMap AgentDataMap = new OSDMap(1);
            AgentDataMap.Add("AgentID", OSD.FromUUID(dataForAgentID));
            AgentData.Add(AgentDataMap);


            OSDArray GroupData = new OSDArray(data.Length);
            OSDArray NewGroupData = new OSDArray(data.Length);

            foreach (GroupMembershipData membership in data)
            {
                if (remoteClient.AgentId != dataForAgentID)
                {
                    if (!membership.ListInProfile)
                    {
                        // If we're sending group info to remoteclient about another agent, 
                        // filter out groups the other agent doesn't want to share.
                        continue;
                    }
                }

                OSDMap GroupDataMap = new OSDMap(6);
                OSDMap NewGroupDataMap = new OSDMap(1);

                GroupDataMap.Add("GroupID", OSD.FromUUID(membership.GroupID));
                GroupDataMap.Add("GroupPowers", OSD.FromBinary(membership.GroupPowers));
                GroupDataMap.Add("AcceptNotices", OSD.FromBoolean(membership.AcceptNotices));
                GroupDataMap.Add("GroupInsigniaID", OSD.FromUUID(membership.GroupPicture));
                GroupDataMap.Add("Contribution", OSD.FromInteger(membership.Contribution));
                GroupDataMap.Add("GroupName", OSD.FromString(membership.GroupName));
                NewGroupDataMap.Add("ListInProfile", OSD.FromBoolean(membership.ListInProfile));

                GroupData.Add(GroupDataMap);
                NewGroupData.Add(NewGroupDataMap);
            }

            OSDMap llDataStruct = new OSDMap(3);
            llDataStruct.Add("AgentData", AgentData);
            llDataStruct.Add("GroupData", GroupData);
            llDataStruct.Add("NewGroupData", NewGroupData);

            IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();

            if (queue != null)
            {
                queue.Enqueue(EventQueueHelper.buildEvent("AgentGroupDataUpdate", llDataStruct), remoteClient.AgentId);
            }
            
        }

        private void SendScenePresenceUpdate(UUID AgentID, string Title)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Updating scene title for {0} with title: {1}", AgentID, Title);

            ScenePresence presence = null;

            foreach (Scene scene in m_sceneList)
            {
                presence = scene.GetScenePresence(AgentID);
                if (presence != null)
                {
                    presence.Grouptitle = Title;

                    // FixMe: Ter suggests a "Schedule" method that I can't find.
                    presence.SendFullUpdateToAllClients();
                }
            }
        }

        /// <summary>
        /// Send updates to all clients who might be interested in groups data for dataForClientID
        /// </summary>
        private void UpdateAllClientsWithGroupInfo(UUID dataForClientID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: Probably isn't nessesary to update every client in every scene.
            // Need to examine client updates and do only what's nessesary.
            lock (m_sceneList)
            {
                foreach (Scene scene in m_sceneList)
                {
                    scene.ForEachClient(delegate(IClientAPI client) { SendAgentGroupDataUpdate(client, dataForClientID); });
                }
            }
        }

        /// <summary>
        /// Update remoteClient with group information about dataForAgentID
        /// </summary>
        private void SendAgentGroupDataUpdate(IClientAPI remoteClient, UUID dataForAgentID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: {0} called for {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, remoteClient.Name);

            // TODO: All the client update functions need to be reexamined because most do too much and send too much stuff

            OnAgentDataUpdateRequest(remoteClient, dataForAgentID, UUID.Zero);


            // Need to send a group membership update to the client
            // UDP version doesn't seem to behave nicely.  But we're going to send it out here
            // with an empty group membership to hopefully remove groups being displayed due
            // to the core Groups Stub
            remoteClient.SendGroupMembership(new GroupMembershipData[0]);

            GroupMembershipData[] membershipArray = GetProfileListedGroupMemberships(remoteClient, dataForAgentID);
            SendGroupMembershipInfoViaCaps(remoteClient, dataForAgentID, membershipArray);
            remoteClient.SendAvatarGroupsReply(dataForAgentID, membershipArray);

        }

        /// <summary>
        /// Get a list of groups memberships for the agent that are marked "ListInProfile"
        /// </summary>
        /// <param name="dataForAgentID"></param>
        /// <returns></returns>
        private GroupMembershipData[] GetProfileListedGroupMemberships(IClientAPI requestingClient, UUID dataForAgentID)
        {
            List<GroupMembershipData> membershipData = m_groupData.GetAgentGroupMemberships(GetClientGroupRequestID(requestingClient), dataForAgentID);
            GroupMembershipData[] membershipArray;

            if (requestingClient.AgentId != dataForAgentID)
            {
                Predicate<GroupMembershipData> showInProfile = delegate(GroupMembershipData membership)
                {
                    return membership.ListInProfile;
                };

                membershipArray = membershipData.FindAll(showInProfile).ToArray();
            }
            else
            {
                membershipArray = membershipData.ToArray();
            }

            if (m_debugEnabled)
            {
                m_log.InfoFormat("[GROUPS]: Get group membership information for {0} requested by {1}", dataForAgentID, requestingClient.AgentId);
                foreach (GroupMembershipData membership in membershipArray)
                {
                    m_log.InfoFormat("[GROUPS]: {0} :: {1} - {2}", dataForAgentID, membership.GroupName, membership.GroupTitle);
                }
            }

            return membershipArray;
        }

        private void SendAgentDataUpdate(IClientAPI remoteClient, UUID dataForAgentID, UUID activeGroupID, string activeGroupName, ulong activeGroupPowers, string activeGroupTitle)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: All the client update functions need to be reexamined because most do too much and send too much stuff
            UserProfileData userProfile = m_sceneList[0].CommsManager.UserService.GetUserProfile(dataForAgentID);
            string firstname, lastname;
            if (userProfile != null)
            {
                firstname = userProfile.FirstName;
                lastname = userProfile.SurName;
            }
            else
            {
                firstname = "Unknown";
                lastname = "Unknown";
            }

            remoteClient.SendAgentDataUpdate(dataForAgentID, activeGroupID, firstname,
                    lastname, activeGroupPowers, activeGroupName,
                    activeGroupTitle);
        }

        #endregion

        #region IM Backed Processes

        private void OutgoingInstantMessage(GridInstantMessage msg, UUID msgTo)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            IClientAPI localClient = GetActiveClient(msgTo);
            if (localClient != null)
            {
                if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: MsgTo ({0}) is local, delivering directly", localClient.Name);
                localClient.SendInstantMessage(msg);
            }
            else
            {
                if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: MsgTo ({0}) is not local, delivering via TransferModule", msgTo);
                m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) { if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Message Sent: {0}", success?"Succeeded":"Failed"); });
            }
        }

        public void NotifyChange(UUID groupID)
        {
            // Notify all group members of a chnge in group roles and/or
            // permissions
            //
        }

        #endregion
    }

}
