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

        private List<Scene> m_SceneList = new List<Scene>();

        // This only works when running as non-Shared, in shared, there may be multiple IClientAPIs for a single client
        private Dictionary<UUID, IClientAPI> m_ActiveClients = new Dictionary<UUID, IClientAPI>();

        private IMessageTransferModule m_MsgTransferModule = null;

        private IGroupDataProvider m_groupData = null;

        // Configuration settings
        private const string m_defaultXmlRpcServiceURL = "http://osflotsam.org/xmlrpc.php";
        private bool m_GroupsEnabled = false;
        private bool m_GroupNoticesEnabled = true;
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
                m_GroupsEnabled = groupsConfig.GetBoolean("Enabled", false);
                if (!m_GroupsEnabled)
                {
                    m_log.Info("[GROUPS]: Groups disabled in configuration");
                    return;
                }

                if (groupsConfig.GetString("Module", "Default") != "XmlRpcGroups")
                {
                    m_log.Info("[GROUPS]: Config Groups Module not set to XmlRpcGroups");
                    m_GroupsEnabled = false;

                    return;
                }

                string ServiceURL = groupsConfig.GetString("XmlRpcServiceURL", m_defaultXmlRpcServiceURL);
                bool DisableKeepAlive = groupsConfig.GetBoolean("XmlRpcDisableKeepAlive", false);

                m_groupData = new XmlRpcGroupDataProvider(ServiceURL, DisableKeepAlive);
                m_log.InfoFormat("[GROUPS]: XmlRpc Service URL set to: {0}", ServiceURL);

                m_GroupNoticesEnabled   = groupsConfig.GetBoolean("XmlRpcNoticesEnabled", true);
                m_debugEnabled          = groupsConfig.GetBoolean("XmlRpcDebugEnabled", true);

            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_GroupsEnabled)
                scene.RegisterModuleInterface<IGroupsModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_GroupsEnabled)
                return;

            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_MsgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

            // No message transfer module, no notices, group invites, rejects, ejects, etc
            if (m_MsgTransferModule == null)
            {
                m_GroupsEnabled = false;
                m_log.Info("[GROUPS]: Could not get MessageTransferModule");
                Close();
                return;
            }


            m_SceneList.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_GroupsEnabled)
                return;

            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_SceneList.Remove(scene);
        }

        public void Close()
        {
            if (!m_GroupsEnabled)
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
            foreach (IClientAPI client in m_ActiveClients.Values)
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

            
            lock (m_ActiveClients)
            {
                if (!m_ActiveClients.ContainsKey(client.AgentId))
                {
                    client.OnUUIDGroupNameRequest += HandleUUIDGroupNameRequest;
                    client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
                    client.OnDirFindQuery += OnDirFindQuery;
                    client.OnInstantMessage += OnInstantMessage;

                    m_ActiveClients.Add(client.AgentId, client);
                }
            }

            UpdateClientWithGroupInfo(client);
        }
        private void OnClientClosed(UUID AgentId)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

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
                    m_log.InfoFormat("[GROUPS] Client closed that wasn't registered here.");
                }

                
            }

        }


        void OnDirFindQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, int queryStart)
        {
            if (((DirFindFlags)queryFlags & DirFindFlags.Groups) == DirFindFlags.Groups)
            {
                m_log.InfoFormat("[GROUPS] {0} called with queryText({1}) queryFlags({2}) queryStart({3})", System.Reflection.MethodBase.GetCurrentMethod().Name, queryText, (DirFindFlags)queryFlags, queryStart);

                remoteClient.SendDirGroupsReply(queryID, m_groupData.FindGroups(queryText).ToArray());
            }
            
        }

        private void OnAgentDataUpdateRequest(IClientAPI remoteClient,
                UUID AgentID, UUID SessionID)
        {
            m_log.InfoFormat("[GROUPS] {0} called with SessionID :: {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, SessionID);


            UUID ActiveGroupID = UUID.Zero;
            string ActiveGroupTitle = string.Empty;
            string ActiveGroupName = string.Empty;
            ulong ActiveGroupPowers  = (ulong)GroupPowers.None;

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(AgentID);
            if (membership != null)
            {
                ActiveGroupID = membership.GroupID;
                ActiveGroupTitle = membership.GroupTitle;
                ActiveGroupPowers = membership.GroupPowers;
            }

            string firstname, lastname;
            IClientAPI agent;
            if( m_ActiveClients.TryGetValue(AgentID, out agent) )
            {
                firstname = agent.FirstName;
                lastname = agent.LastName;
            } else {
                firstname = "Unknown";
                lastname = "Unknown";
            }

            UpdateScenePresenceWithTitle(AgentID, ActiveGroupTitle);

            remoteClient.SendAgentDataUpdate(AgentID, ActiveGroupID, firstname,
                    lastname, ActiveGroupPowers, ActiveGroupName,
                    ActiveGroupTitle);
        }

        private void HandleUUIDGroupNameRequest(UUID GroupID,IClientAPI remote_client)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string GroupName;

            GroupRecord group = m_groupData.GetGroupRecord(GroupID, null);
            if (group != null)
            {
                GroupName = group.GroupName;
            }
            else
            {
                GroupName = "Unknown";
            }


            remote_client.SendGroupNameReply(GroupID, GroupName);
        }


        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);


            // Group invitations
            if ((im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept) || (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline))
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

                        if (m_MsgTransferModule != null)
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

                            m_MsgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
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
                if (!m_GroupNoticesEnabled)
                {
                    return;
                }

                UUID GroupID = new UUID(im.toAgentID);
                if( m_groupData.GetGroupRecord(GroupID, null) != null)
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
                        GroupID.ToBytes(bucket, 2);
                        bucket[18] = 0; //dunno
                    }


                    m_groupData.AddGroupNotice(GroupID, NoticeID, im.fromAgentName, Subject, Message, bucket);
                    if (OnNewGroupNotice != null)
                    {
                        OnNewGroupNotice(GroupID, NoticeID);
                    }

                    // Build notice IIM
                    GridInstantMessage msg = CreateGroupNoticeIM(UUID.Zero, NoticeID, (byte)OpenMetaverse.InstantMessageDialog.GroupNotice);

                    // Send notice out to everyone that wants notices
                    foreach( GroupMembersData member in m_groupData.GetGroupMembers(GroupID) )
                    {
                        if( member.AcceptNotices )
                        {
                            msg.toAgentID = member.AgentID.Guid;
                            m_MsgTransferModule.SendInstantMessage(msg, delegate(bool success) { });

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


                if (m_MsgTransferModule != null)
                {
                    im.dialog = (byte)InstantMessageDialog.MessageFromAgent;
                    m_MsgTransferModule.SendInstantMessage(im, delegate(bool success) { });
                }

                UUID ejecteeID = new UUID(im.toAgentID);
                UUID groupID = new UUID(im.toAgentID);
                if (m_ActiveClients.ContainsKey(ejecteeID))
                {
                    m_ActiveClients[ejecteeID].SendAgentDropGroup(groupID);
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
                        if (m_ActiveClients.ContainsKey(toAgentID))
                        {
                            m_ActiveClients[toAgentID].SendInstantMessage(msg);
                        }
                        break;
                }
            }

        }


        #endregion


        private void UpdateScenePresenceWithTitle(UUID AgentID, string Title)
        {
            m_log.DebugFormat("[GROUPS] Updating scene title for {0} with title: {1}", AgentID, Title);
            ScenePresence presence = null;
            lock (m_SceneList)
            {
                foreach (Scene scene in m_SceneList)
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
        }


        #region IGroupsModule Members

        public event NewGroupNotice OnNewGroupNotice;

        public GroupRecord GetGroupRecord(UUID GroupID)
        {
            return m_groupData.GetGroupRecord(GroupID, null);
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

        public GroupMembershipData[] GetMembershipData(UUID UserID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMemberships(UserID).ToArray();
        }

        public GroupMembershipData GetMembershipData(UUID GroupID, UUID UserID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMembership(UserID, GroupID);
        }

        public void UpdateGroupInfo(IClientAPI remoteClient, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: Security Check?

            m_groupData.UpdateGroup(groupID, charter, showInList, insigniaID, membershipFee, openEnrollment, allowPublish, maturePublish);
        }

        public void SetGroupAcceptNotices(IClientAPI remoteClient, UUID groupID, bool acceptNotices, bool listInProfile)
        {
            // TODO: Security Check?
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentGroupInfo(remoteClient.AgentId, groupID, acceptNotices, listInProfile);
        }

        public UUID CreateGroup(IClientAPI remoteClient, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            if( m_groupData.GetGroupRecord(UUID.Zero, name) != null )
            {
                remoteClient.SendCreateGroupReply(UUID.Zero, false, "A group with the same name already exists.");
                return UUID.Zero;
            }
            
            UUID GroupID = m_groupData.CreateGroup(name, charter, showInList, insigniaID, membershipFee, openEnrollment, allowPublish, maturePublish, remoteClient.AgentId);

            remoteClient.SendCreateGroupReply(GroupID, true, "Group created successfullly");

            UpdateClientWithGroupInfo(remoteClient);

            return GroupID;
        }

        public GroupNoticeData[] GroupNoticesListRequest(IClientAPI remoteClient, UUID GroupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // ToDo: check if agent is a member of group and is allowed to see notices?
            
            return m_groupData.GetGroupNotices(GroupID).ToArray();
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
        public void GroupTitleUpdate(IClientAPI remoteClient, UUID GroupID, UUID TitleRoleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentActiveGroupRole(remoteClient.AgentId, GroupID, TitleRoleID);

            UpdateAllClientsWithGroupInfo();
        }


        public void GroupRoleUpdate(IClientAPI remoteClient, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, byte updateType)
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
                if (m_MsgTransferModule != null)
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

                    m_MsgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
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

        public void LeaveGroupRequest(IClientAPI remoteClient, UUID GroupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.RemoveAgentFromGroup(remoteClient.AgentId, GroupID);

            remoteClient.SendLeaveGroupReply(GroupID, true);

            remoteClient.SendAgentDropGroup(GroupID);

            UpdateClientWithGroupInfo(remoteClient);
        }

        public void EjectGroupMemberRequest(IClientAPI remoteClient, UUID GroupID, UUID EjecteeID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Todo: Security check?
            m_groupData.RemoveAgentFromGroup(EjecteeID, GroupID);

            remoteClient.SendEjectGroupMemberReply(remoteClient.AgentId, GroupID, true);

            if (m_MsgTransferModule != null)
            {
                GroupRecord groupInfo = m_groupData.GetGroupRecord(GroupID, null);
                UserProfileData userProfile = m_SceneList[0].CommsManager.UserService.GetUserProfile(EjecteeID);

                if ((groupInfo == null) || (userProfile == null))
                {
                    return;
                }
                

                // Send Message to Ejectee
                GridInstantMessage msg = new GridInstantMessage();
                
                msg.imSessionID = UUID.Zero.Guid;
                msg.fromAgentID = remoteClient.AgentId.Guid;
                // msg.fromAgentID = info.GroupID;
                msg.toAgentID = EjecteeID.Guid;
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
                m_MsgTransferModule.SendInstantMessage(msg, delegate(bool success) { m_log.DebugFormat("[GROUPS] Message Sent Success: {0}", success,ToString()); });


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
                m_MsgTransferModule.SendInstantMessage(msg, delegate(bool success) { m_log.DebugFormat("[GROUPS] Message Sent Success: {0}", success, ToString()); });

                

            }


            UpdateAllClientsWithGroupInfo();
        }

        public void InviteGroupRequest(IClientAPI remoteClient, UUID GroupID, UUID InvitedAgentID, UUID RoleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            m_log.WarnFormat("[GROUPS] GID {0}, AID {1}, RID {2} ", GroupID, InvitedAgentID, RoleID);

            // Todo: Security check, probably also want to send some kind of notification
            UUID InviteID = UUID.Random();
            m_log.WarnFormat("[GROUPS] Invite ID: {0}", InviteID);
            m_groupData.AddAgentToGroupInvite(InviteID, GroupID, RoleID, InvitedAgentID);

            if (m_MsgTransferModule != null)
            {
                Guid inviteUUID = InviteID.Guid;

                GridInstantMessage msg = new GridInstantMessage();
                
                msg.imSessionID = inviteUUID;
                
                // msg.fromAgentID = remoteClient.AgentId.Guid;
                msg.fromAgentID = GroupID.Guid;
                msg.toAgentID = InvitedAgentID.Guid;
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
                
                m_MsgTransferModule.SendInstantMessage(msg, delegate(bool success) { m_log.DebugFormat("[GROUPS] Message Sent Success: {0}", success,ToString()); });
            }
        }

        #endregion

        void SendGroupMembershipInfoViaCaps(IClientAPI remoteClient, GroupMembershipData[] data)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDArray AgentData = new OSDArray(1);
            OSDMap AgentDataMap = new OSDMap(1);
            AgentDataMap.Add("AgentID", OSD.FromUUID(remoteClient.AgentId));
            AgentData.Add(AgentDataMap);


            OSDArray GroupData = new OSDArray(data.Length);
            OSDArray NewGroupData = new OSDArray(data.Length);

            foreach (GroupMembershipData membership in data)
            {
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
    }

}
