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
using System.Reflection;

using System.Collections;
//using Nwc.XmlRpc;

using log4net;
using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.EventQueue;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using DirFindFlags = OpenMetaverse.DirectoryManager.DirFindFlags;



namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    public class XmlRpcGroupsModule : INonSharedRegionModule, IGroupsModule
    {
        /// <summary>
        /// ; To use this module, you must specify the following in your OpenSim.ini
        /// [GROUPS]
        /// Enabled = true
        /// Module  = XmlRpcGroups
        /// XmlRpcServiceURL = http://osflotsam.org/xmlrpc.php
        /// XmlRpcMessagingEnabled = true
        /// XmlRpcNoticesEnabled = true
        /// XmlRpcDebugEnabled = true
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

        // This only works when running as non-Shared, in shared, there may be multiple IClientAPIs for a single client
        private Dictionary<UUID, IClientAPI> m_activeClients = new Dictionary<UUID, IClientAPI>();

        private IMessageTransferModule m_msgTransferModule = null;

        private IGroupDataProvider m_groupData = null;

        // Configuration settings
        private const string m_defaultXmlRpcServiceURL = "http://osflotsam.org/xmlrpc.php";
        private bool m_groupsEnabled = false;
        private bool m_groupNoticesEnabled = true;
        private bool m_debugEnabled = true;

        #region IRegionModule Members

        public void Initialise(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];

            m_log.Info("[GROUPS]: Initializing XmlRpcGroups");

            if (groupsConfig == null)
            {
                // Do not run this module by default.
                m_log.Info("[GROUPS]: No config found in OpenSim.ini -- not enabling XmlRpcGroups");
                return;
            }
            else
            {
                m_groupsEnabled = groupsConfig.GetBoolean("Enabled", false);
                if (!m_groupsEnabled)
                {
                    m_log.Info("[GROUPS]: Groups disabled in configuration");
                    return;
                }

                if (groupsConfig.GetString("Module", "Default") != "XmlRpcGroups")
                {
                    m_log.Info("[GROUPS]: Config Groups Module not set to XmlRpcGroups");
                    m_groupsEnabled = false;

                    return;
                }

                string ServiceURL = groupsConfig.GetString("XmlRpcServiceURL", m_defaultXmlRpcServiceURL);
                bool DisableKeepAlive = groupsConfig.GetBoolean("XmlRpcDisableKeepAlive", false);

                m_groupData = new XmlRpcGroupDataProvider(ServiceURL, DisableKeepAlive);
                m_log.InfoFormat("[GROUPS]: XmlRpc Service URL set to: {0}", ServiceURL);

                m_groupNoticesEnabled   = groupsConfig.GetBoolean("XmlRpcNoticesEnabled", true);
                m_debugEnabled          = groupsConfig.GetBoolean("XmlRpcDebugEnabled", true);

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

            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_msgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

            // No message transfer module, no notices, group invites, rejects, ejects, etc
            if (m_msgTransferModule == null)
            {
                m_groupsEnabled = false;
                m_log.Info("[GROUPS]: Could not get MessageTransferModule");
                Close();
                return;
            }


            m_sceneList.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_sceneList.Remove(scene);
        }

        public void Close()
        {
            if (!m_groupsEnabled)
                return;
            m_log.Debug("[GROUPS]: Shutting down XmlRpcGroups module.");
        }

        public string Name
        {
            get { return "XmlRpcGroupsModule"; }
        }
        #endregion

        private void UpdateAllClientsWithGroupInfo()
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            foreach (IClientAPI client in m_activeClients.Values)
            {
                UpdateClientWithGroupInfo(client);
            }
        }

        private void UpdateClientWithGroupInfo(IClientAPI client)
        {
            m_log.InfoFormat("[GROUPS] {0} called for {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, client.Name);
            OnAgentDataUpdateRequest(client, client.AgentId, UUID.Zero);


            // Need to send a group membership update to the client
            // UDP version doesn't seem to behave nicely
            // client.SendGroupMembership(GetMembershipData(client.AgentId));

            GroupMembershipData[] membershipData = m_groupData.GetAgentGroupMemberships(client.AgentId).ToArray();

            SendGroupMembershipInfoViaCaps(client, membershipData);
            client.SendAvatarGroupsReply(client.AgentId, membershipData);

        }

        #region EventHandlers
        private void OnNewClient(IClientAPI client)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            
            lock (m_activeClients)
            {
                if (!m_activeClients.ContainsKey(client.AgentId))
                {
                    client.OnUUIDGroupNameRequest += HandleUUIDGroupNameRequest;
                    client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
                    client.OnDirFindQuery += OnDirFindQuery;
                    client.OnInstantMessage += OnInstantMessage;

                    m_activeClients.Add(client.AgentId, client);
                }
            }

            UpdateClientWithGroupInfo(client);
        }

        private void OnClientClosed(UUID agentId)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            lock (m_activeClients)
            {
                if (m_activeClients.ContainsKey(agentId))
                {
                    IClientAPI client = m_activeClients[agentId];
                    client.OnUUIDGroupNameRequest -= HandleUUIDGroupNameRequest;
                    client.OnAgentDataUpdateRequest -= OnAgentDataUpdateRequest;
                    client.OnDirFindQuery -= OnDirFindQuery;
                    client.OnInstantMessage -= OnInstantMessage;

                    m_activeClients.Remove(agentId);
                }
                else
                {
                    m_log.InfoFormat("[GROUPS] Client closed that wasn't registered here.");
                }
            }
        }


        void OnDirFindQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, int queryStart)
        {
            if (((DirFindFlags)queryFlags & DirFindFlags.Groups) == DirFindFlags.Groups)
            {
                m_log.InfoFormat("[GROUPS] {0} called with queryText({1}) queryFlags({2}) queryStart({3})", 
                                 System.Reflection.MethodBase.GetCurrentMethod().Name, queryText, (DirFindFlags)queryFlags, queryStart);

                remoteClient.SendDirGroupsReply(queryID, m_groupData.FindGroups(queryText).ToArray());
            }
        }

        private void OnAgentDataUpdateRequest(IClientAPI remoteClient, UUID agentID, UUID sessionID)
        {
            m_log.InfoFormat("[GROUPS] {0} called with SessionID :: {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, sessionID);

            UUID activeGroupID = UUID.Zero;
            string activeGroupTitle = string.Empty;
            string activeGroupName = string.Empty;
            ulong activeGroupPowers  = (ulong)GroupPowers.None;

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(agentID);
            if (membership != null)
            {
                activeGroupID = membership.GroupID;
                activeGroupTitle = membership.GroupTitle;
                activeGroupPowers = membership.GroupPowers;
            }

            string firstname, lastname;
            IClientAPI agent;
            if (m_activeClients.TryGetValue(agentID, out agent))
            {
                firstname = agent.FirstName;
                lastname = agent.LastName;
            } else {
                firstname = "Unknown";
                lastname = "Unknown";
            }

            UpdateScenePresenceWithTitle(agentID, activeGroupTitle);

            remoteClient.SendAgentDataUpdate(agentID, activeGroupID, firstname,
                                             lastname, activeGroupPowers, activeGroupName,
                                             activeGroupTitle);
        }

        private void HandleUUIDGroupNameRequest(UUID groupID, IClientAPI remoteClient)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string groupName;

            GroupRecord group = m_groupData.GetGroupRecord(groupID, null);
            if (group != null)
            {
                groupName = group.GroupName;
            }
            else
            {
                groupName = "Unknown";
            }

            remoteClient.SendGroupNameReply(groupID, groupName);
        }


        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);


            // Group invitations
            if ((im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept) || 
                (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline))
            {
                m_log.WarnFormat("[GROUPS] Received an IIM for {0}.", ((InstantMessageDialog)im.dialog).ToString());

                UUID inviteID = new UUID(im.imSessionID);
                GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite(inviteID);

                m_log.WarnFormat("[GROUPS] Invite is for Agent {0} to Group {1}.", inviteInfo.AgentID, inviteInfo.GroupID);

                UUID fromAgentID = new UUID(im.fromAgentID);
                if ((inviteInfo != null) && (fromAgentID == inviteInfo.AgentID))
                {

                    // Accept
                    if (im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept)
                    {
                        m_log.WarnFormat("[GROUPS] Received an accept invite notice.");

                        // and the sessionid is the role
                        m_groupData.AddAgentToGroup(inviteInfo.AgentID, inviteInfo.GroupID, inviteInfo.RoleID);

                        if (m_msgTransferModule != null)
                        {
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

                            m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
                        }

                        UpdateAllClientsWithGroupInfo();

                        m_groupData.RemoveAgentToGroupInvite(inviteID);
                    }

                    // Reject
                    if (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline)
                    {
                        m_log.WarnFormat("[GROUPS] Received a reject invite notice.");
                        m_groupData.RemoveAgentToGroupInvite(inviteID);

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

                UUID groupID = new UUID(im.toAgentID);
                if (m_groupData.GetGroupRecord(groupID, null) != null)
                {
                    UUID noticeID = UUID.Random();
                    string subject = im.message.Substring(0, im.message.IndexOf('|'));
                    string message = im.message.Substring(subject.Length + 1);

                    byte[] bucket;

                    if ((im.binaryBucket.Length == 1) && (im.binaryBucket[0] == 0))
                    {
                        bucket = new byte[19];
                        bucket[0] = 0; //dunno
                        bucket[1] = 0; //dunno
                        groupID.ToBytes(bucket, 2);
                        bucket[18] = 0; //dunno
                    }
                    else
                    {
                        string binBucket = OpenMetaverse.Utils.BytesToString(im.binaryBucket);
                        binBucket = binBucket.Remove(0, 14).Trim();
                        m_log.WarnFormat("I don't understand a group notice binary bucket of: {0}", binBucket);

                        OSDMap binBucketOSD = (OSDMap)OSDParser.DeserializeLLSDXml(binBucket);
                        
                        foreach (string key in binBucketOSD.Keys)
                        {
                            m_log.WarnFormat("{0}: {1}", key, binBucketOSD[key].ToString());
                        }                     
   
                        // treat as if no attachment
                        bucket = new byte[19];
                        bucket[0] = 0; //dunno
                        bucket[1] = 0; //dunno
                        groupID.ToBytes(bucket, 2);
                        bucket[18] = 0; //dunno
                    }


                    m_groupData.AddGroupNotice(groupID, noticeID, im.fromAgentName, subject, message, bucket);
                    if (OnNewGroupNotice != null)
                    {
                        OnNewGroupNotice(groupID, noticeID);
                    }

                    // Build notice IIM
                    GridInstantMessage msg = CreateGroupNoticeIM(UUID.Zero, noticeID, 
                                                                 (byte)OpenMetaverse.InstantMessageDialog.GroupNotice);

                    // Send notice out to everyone that wants notices
                    foreach (GroupMembersData member in m_groupData.GetGroupMembers(groupID))
                    {
                        if (member.AcceptNotices)
                        {
                            msg.toAgentID = member.AgentID.Guid;
                            m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) {});
                        }
                    }
                }
            }
            
            // Interop, received special 210 code for ejecting a group member
            // this only works within the comms servers domain, and won't work hypergrid
            // TODO:FIXME: Use a presense server of some kind to find out where the 
            // client actually is, and try contacting that region directly to notify them,
            // or provide the notification via xmlrpc update queue
            if (im.dialog == 210)
            {
                // This is sent from the region that the ejectee was ejected from
                // if it's being delivered here, then the ejectee is here
                // so we need to send local updates to the agent.
                if (m_msgTransferModule != null)
                {
                    im.dialog = (byte)InstantMessageDialog.MessageFromAgent;
                    m_msgTransferModule.SendInstantMessage(im, delegate(bool success) {});
                }

                UUID ejecteeID = new UUID(im.toAgentID);
                UUID groupID = new UUID(im.toAgentID);
                if (m_activeClients.ContainsKey(ejecteeID))
                {
                    m_activeClients[ejecteeID].SendAgentDropGroup(groupID);
                }
            }
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Trigger the above event handler
            OnInstantMessage(null, msg);

            // If a message from a group arrives here, it may need to be forwarded to a local client
            if (msg.fromGroup == true)
            {
                switch( msg.dialog )
                {
                    case (byte)InstantMessageDialog.GroupInvitation:
                    case (byte)InstantMessageDialog.GroupNotice:
                        UUID toAgentID = new UUID(msg.toAgentID);
                        if (m_activeClients.ContainsKey(toAgentID))
                        {
                            m_activeClients[toAgentID].SendInstantMessage(msg);
                        }
                        break;
                }
            }

        }
        #endregion

        private void UpdateScenePresenceWithTitle(UUID agentID, string title)
        {
            m_log.DebugFormat("[GROUPS] Updating scene title for {0} with title: {1}", agentID, title);

            ScenePresence presence = null;
            lock (m_sceneList)
            {
                foreach (Scene scene in m_sceneList)
                {
                    presence = scene.GetScenePresence(agentID);
                    if (presence != null)
                    {
                        presence.Grouptitle = title;

                        // FixMe: Ter suggests a "Schedule" method that I can't find.
                        presence.SendFullUpdateToAllClients();
                    }
                }
            }
        }


        #region IGroupsModule Members

        public event NewGroupNotice OnNewGroupNotice;

        public GroupRecord GetGroupRecord(UUID groupID)
        {
            return m_groupData.GetGroupRecord(groupID, null);
        }

        public void ActivateGroup(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentActiveGroup(remoteClient.AgentId, groupID);

            // UpdateClientWithGroupInfo(remoteClient);
            UpdateAllClientsWithGroupInfo();
        }

        /// <summary>
        /// Get the Role Titles for an Agent, for a specific group
        /// </summary>
        public List<GroupTitlesData> GroupTitlesRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> agentRoles = m_groupData.GetAgentGroupRoles(remoteClient.AgentId, groupID);
            GroupMembershipData agentMembership = m_groupData.GetAgentGroupMembership(remoteClient.AgentId, groupID);

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
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupMembersData> data = m_groupData.GetGroupMembers(groupID);

            foreach (GroupMembersData member in data)
            {
                m_log.InfoFormat("[GROUPS] {0} {1}", member.AgentID, member.Title);
            }

            return data;
        }

        public List<GroupRolesData> GroupRoleDataRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> data = m_groupData.GetGroupRoles(groupID);

            foreach (GroupRolesData member in data)
            {
                m_log.InfoFormat("[GROUPS] {0} {1}", member.Title, member.Members);
            }

            return data;
        }

        public List<GroupRoleMembersData> GroupRoleMembersRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRoleMembersData> data = m_groupData.GetGroupRoleMembers(groupID);

            foreach (GroupRoleMembersData member in data)
            {
                m_log.InfoFormat("[GROUPS] Av: {0}  Role: {1}", member.MemberID, member.RoleID);
            }

            return data;
        }

        public GroupProfileData GroupProfileRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupProfileData profile = new GroupProfileData();

            GroupRecord groupInfo = m_groupData.GetGroupRecord(groupID, null);
            if (groupInfo != null)
            {
                profile.AllowPublish = groupInfo.AllowPublish;
                profile.Charter = groupInfo.Charter;
                profile.FounderID = groupInfo.FounderID;
                profile.GroupID = groupID;
                profile.GroupMembershipCount = m_groupData.GetGroupMembers(groupID).Count;
                profile.GroupRolesCount = m_groupData.GetGroupRoles(groupID).Count;
                profile.InsigniaID = groupInfo.GroupPicture;
                profile.MaturePublish = groupInfo.MaturePublish;
                profile.MembershipFee = groupInfo.MembershipFee;
                profile.Money = 0; // TODO: Get this from the currency server?
                profile.Name = groupInfo.GroupName;
                profile.OpenEnrollment = groupInfo.OpenEnrollment;
                profile.OwnerRole = groupInfo.OwnerRoleID;
                profile.ShowInList = groupInfo.ShowInList;
            }

            GroupMembershipData memberInfo = m_groupData.GetAgentGroupMembership(remoteClient.AgentId, groupID);
            if (memberInfo != null)
            {
                profile.MemberTitle = memberInfo.GroupTitle;
                profile.PowersMask = memberInfo.GroupPowers;
            }

            return profile;
        }

        public GroupMembershipData[] GetMembershipData(UUID userID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMemberships(userID).ToArray();
        }

        public GroupMembershipData GetMembershipData(UUID groupID, UUID userID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMembership(userID, groupID);
        }

        public void UpdateGroupInfo(IClientAPI remoteClient, UUID groupID, string charter, 
                                    bool showInList, UUID insigniaID, int membershipFee, 
                                    bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: Security Check?

            m_groupData.UpdateGroup(groupID, charter, showInList, insigniaID, membershipFee, 
                                    openEnrollment, allowPublish, maturePublish);
        }

        public void SetGroupAcceptNotices(IClientAPI remoteClient, UUID groupID, bool acceptNotices, bool listInProfile)
        {
            // TODO: Security Check?
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentGroupInfo(remoteClient.AgentId, groupID, acceptNotices, listInProfile);
        }

        public UUID CreateGroup(IClientAPI remoteClient, string name, string charter, 
                                bool showInList, UUID insigniaID, int membershipFee, 
                                bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            if (m_groupData.GetGroupRecord(UUID.Zero, name) != null)
            {
                remoteClient.SendCreateGroupReply(UUID.Zero, false, "A group with the same name already exists.");
                return UUID.Zero;
            }
            
            UUID groupID = m_groupData.CreateGroup(name, charter, showInList, insigniaID, membershipFee, 
                                                   openEnrollment, allowPublish, maturePublish, remoteClient.AgentId);

            remoteClient.SendCreateGroupReply(groupID, true, "Group created successfullly");

            UpdateClientWithGroupInfo(remoteClient);

            return groupID;
        }

        public GroupNoticeData[] GroupNoticesListRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // ToDo: check if agent is a member of group and is allowed to see notices?
            
            return m_groupData.GetGroupNotices(groupID).ToArray();
        }

        /// <summary>
        /// Get the title of the agent's current role.
        /// </summary>
        public string GetGroupTitle(UUID avatarID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(avatarID);
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
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentActiveGroupRole(remoteClient.AgentId, groupID, titleRoleID);

            UpdateAllClientsWithGroupInfo();
        }


        public void GroupRoleUpdate(IClientAPI remoteClient, UUID groupID, UUID roleID, 
                                    string name, string description, string title, ulong powers, byte updateType)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: Security Checks?

            switch ((OpenMetaverse.GroupRoleUpdate)updateType)
            {
                case OpenMetaverse.GroupRoleUpdate.Create:
                    m_groupData.AddGroupRole(groupID, UUID.Random(), name, description, title, powers);
                    break;

                case OpenMetaverse.GroupRoleUpdate.Delete:
                    m_groupData.RemoveGroupRole(groupID, roleID);
                    break;

                case OpenMetaverse.GroupRoleUpdate.UpdateAll:
                case OpenMetaverse.GroupRoleUpdate.UpdateData:
                case OpenMetaverse.GroupRoleUpdate.UpdatePowers:
                    m_groupData.UpdateGroupRole(groupID, roleID, name, description, title, powers);
                    break;

                case OpenMetaverse.GroupRoleUpdate.NoUpdate:
                default:
                    // No Op
                    break;

            }

            UpdateClientWithGroupInfo(remoteClient);
        }

        public void GroupRoleChanges(IClientAPI remoteClient, UUID groupID, UUID roleID, UUID memberID, uint changes)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            // Todo: Security check

            switch (changes)
            {
                case 0:
                    // Add
                    m_groupData.AddAgentToGroupRole(memberID, groupID, roleID);

                    break;
                case 1:
                    // Remove
                    m_groupData.RemoveAgentFromGroupRole(memberID, groupID, roleID);
                    
                    break;
                default:
                    m_log.ErrorFormat("[GROUPS] {0} does not understand changes == {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, changes);
                    break;
            }
            UpdateClientWithGroupInfo(remoteClient);
        }

        public void GroupNoticeRequest(IClientAPI remoteClient, UUID groupNoticeID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            
            GroupNoticeInfo data = m_groupData.GetGroupNotice(groupNoticeID);

            if (data != null)
            {
                if (m_msgTransferModule != null)
                {
                    GridInstantMessage msg = new GridInstantMessage();
                    msg.imSessionID = UUID.Zero.Guid;
                    msg.fromAgentID = data.GroupID.Guid;
                    msg.toAgentID = remoteClient.AgentId.Guid;
                    msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                    msg.fromAgentName = "Group Notice From";
                    msg.message = data.noticeData.Subject + "|" + data.Message;
                    msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupNoticeRequested;
                    msg.fromGroup = true;
                    msg.offline = (byte)0;
                    msg.ParentEstateID = 0;
                    msg.Position = Vector3.Zero;
                    msg.RegionID = UUID.Zero.Guid;
                    msg.binaryBucket = data.BinaryBucket;

                    m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
                }
            }

        }

        public GridInstantMessage CreateGroupNoticeIM(UUID agentID, UUID groupNoticeID, byte dialog)
        {
            m_log.WarnFormat("[GROUPS] {0} is probably not properly implemented", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GridInstantMessage msg = new GridInstantMessage();
            msg.imSessionID = UUID.Zero.Guid;
            msg.toAgentID = agentID.Guid;
            msg.dialog = dialog;
            // msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupNotice;
            msg.fromGroup = true;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = UUID.Zero.Guid;

            GroupNoticeInfo info = m_groupData.GetGroupNotice(groupNoticeID);
            if (info != null)
            {
                msg.fromAgentID = info.GroupID.Guid;
                msg.timestamp = info.noticeData.Timestamp;
                msg.fromAgentName = info.noticeData.FromName;
                msg.message = info.noticeData.Subject + "|" + info.Message;
                msg.binaryBucket = info.BinaryBucket;
            }

            return msg;
        }

        public void SendAgentGroupDataUpdate(IClientAPI remoteClient)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            UpdateClientWithGroupInfo(remoteClient);
        }

        public void JoinGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Should check to see if OpenEnrollment, or if there's an outstanding invitation
            m_groupData.AddAgentToGroup(remoteClient.AgentId, groupID, UUID.Zero);

            remoteClient.SendJoinGroupReply(groupID, true);

            UpdateClientWithGroupInfo(remoteClient);
        }

        public void LeaveGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.RemoveAgentFromGroup(remoteClient.AgentId, groupID);

            remoteClient.SendLeaveGroupReply(groupID, true);

            remoteClient.SendAgentDropGroup(groupID);

            UpdateClientWithGroupInfo(remoteClient);
        }

        public void EjectGroupMemberRequest(IClientAPI remoteClient, UUID groupID, UUID ejecteeID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Todo: Security check?
            m_groupData.RemoveAgentFromGroup(ejecteeID, groupID);

            remoteClient.SendEjectGroupMemberReply(remoteClient.AgentId, groupID, true);

            if (m_msgTransferModule != null)
            {
                GroupRecord groupInfo = m_groupData.GetGroupRecord(groupID, null);
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
                m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) 
                                                       { 
                                                           m_log.DebugFormat("[GROUPS] Message Sent Success: {0}", 
                                                                             success,ToString()); 
                                                       });


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
                    msg.message = string.Format("{2} has been ejected from '{1}' by {0}.", 
                                                remoteClient.Name, groupInfo.GroupName, userProfile.Name);
                }
                else
                {
                    msg.message = string.Format("{2} has been ejected from '{1}' by {0}.", 
                                                remoteClient.Name, groupInfo.GroupName, "Unknown member");
                }
                msg.dialog = (byte)210; //interop
                msg.fromGroup = false;
                msg.offline = (byte)0;
                msg.ParentEstateID = 0;
                msg.Position = Vector3.Zero;
                msg.RegionID = remoteClient.Scene.RegionInfo.RegionID.Guid;
                msg.binaryBucket = new byte[0];
                m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) 
                                                       {
                                                           m_log.DebugFormat("[GROUPS] Message Sent Success: {0}", 
                                                                             success, ToString()); 
                                                       });
            }

            UpdateAllClientsWithGroupInfo();
        }

        public void InviteGroupRequest(IClientAPI remoteClient, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            m_log.WarnFormat("[GROUPS] GID {0}, AID {1}, RID {2} ", groupID, invitedAgentID, roleID);

            // Todo: Security check, probably also want to send some kind of notification
            UUID inviteID = UUID.Random();
            m_log.WarnFormat("[GROUPS] Invite ID: {0}", inviteID);
            m_groupData.AddAgentToGroupInvite(inviteID, groupID, roleID, invitedAgentID);

            if (m_msgTransferModule != null)
            {
                Guid inviteUUID = inviteID.Guid;

                GridInstantMessage msg = new GridInstantMessage();
                
                msg.imSessionID = inviteUUID;
                
                // msg.fromAgentID = remoteClient.AgentId.Guid;
                msg.fromAgentID = groupID.Guid;
                msg.toAgentID = invitedAgentID.Guid;
                //msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                msg.timestamp = 0;
                msg.fromAgentName = remoteClient.Name;
                msg.message = string.Format("{0} has invited you to join a group. There is no cost to join this group.", 
                                            remoteClient.Name);
                msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupInvitation;
                msg.fromGroup = true;
                msg.offline = (byte)0;
                msg.ParentEstateID = 0;
                msg.Position = Vector3.Zero;
                msg.RegionID = remoteClient.Scene.RegionInfo.RegionID.Guid;
                msg.binaryBucket = new byte[20];
                
                m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) 
                                                       { 
                                                           m_log.DebugFormat("[GROUPS] Message Sent Success: {0}", success,ToString()); 
                                                       });
            }
        }

        #endregion

        void SendGroupMembershipInfoViaCaps(IClientAPI remoteClient, GroupMembershipData[] data)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDArray agentData = new OSDArray(1);
            OSDMap agentDataMap = new OSDMap(1);
            agentDataMap.Add("AgentID", OSD.FromUUID(remoteClient.AgentId));
            agentData.Add(agentDataMap);


            OSDArray groupData = new OSDArray(data.Length);
            OSDArray newGroupData = new OSDArray(data.Length);

            foreach (GroupMembershipData membership in data)
            {
                OSDMap groupDataMap = new OSDMap(6);
                OSDMap newGroupDataMap = new OSDMap(1);

                groupDataMap.Add("GroupID", OSD.FromUUID(membership.GroupID));
                groupDataMap.Add("GroupPowers", OSD.FromBinary(membership.GroupPowers));
                groupDataMap.Add("AcceptNotices", OSD.FromBoolean(membership.AcceptNotices));
                groupDataMap.Add("GroupInsigniaID", OSD.FromUUID(membership.GroupPicture));
                groupDataMap.Add("Contribution", OSD.FromInteger(membership.Contribution));
                groupDataMap.Add("GroupName", OSD.FromString(membership.GroupName));
                newGroupDataMap.Add("ListInProfile", OSD.FromBoolean(membership.ListInProfile));

                groupData.Add(groupDataMap);
                newGroupData.Add(newGroupDataMap);
            }

            OSDMap llDataStruct = new OSDMap(3);
            llDataStruct.Add("AgentData", agentData);
            llDataStruct.Add("GroupData", groupData);
            llDataStruct.Add("NewGroupData", newGroupData);

            IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();

            if (queue != null)
            {
                queue.Enqueue(EventQueueHelper.buildEvent("AgentGroupDataUpdate", llDataStruct), 
                              remoteClient.AgentId);
            }
            
        }
    }

}
