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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using DirFindFlags = OpenMetaverse.DirectoryManager.DirFindFlags;

namespace OpenSim.Groups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GroupsModule")]
    public class GroupsModule : ISharedRegionModule, IGroupsModule
    {
        /// <summary>
        /// </summary>

        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_sceneList = new List<Scene>();

        private IMessageTransferModule m_msgTransferModule = null;
        
        private IGroupsServicesConnector m_groupData = null;
        private IUserManagement m_UserManagement;

        // Configuration settings
        private bool m_groupsEnabled = false;
        private bool m_groupNoticesEnabled = true;
        private bool m_debugEnabled = false;
        private int  m_levelGroupCreate = 0;

        #region Region Module interfaceBase Members

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

                m_log.InfoFormat("[Groups]: Initializing {0}", this.Name);

                m_groupNoticesEnabled   = groupsConfig.GetBoolean("NoticesEnabled", true);
                m_debugEnabled          = groupsConfig.GetBoolean("DebugEnabled", false);
                m_levelGroupCreate      = groupsConfig.GetInt("LevelGroupCreate", 0);
            }
        }

        public void AddRegion(Scene scene)
        {
            if (m_groupsEnabled)
            {
                scene.RegisterModuleInterface<IGroupsModule>(this);
                scene.AddCommand(
                    "debug",
                    this,
                    "debug groups verbose",
                    "debug groups verbose <true|false>",
                    "This setting turns on very verbose groups debugging",
                    HandleDebugGroupsVerbose);
            }
        }

        private void HandleDebugGroupsVerbose(object modules, string[] args)
        {
            if (args.Length < 4)
            {
                MainConsole.Instance.Output("Usage: debug groups verbose <true|false>");
                return;
            }

            bool verbose = false;
            if (!bool.TryParse(args[3], out verbose))
            {
                MainConsole.Instance.Output("Usage: debug groups verbose <true|false>");
                return;
            }

            m_debugEnabled = verbose;

            MainConsole.Instance.OutputFormat("{0} verbose logging set to {1}", Name, m_debugEnabled);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnMakeRootAgent += OnMakeRoot;
            scene.EventManager.OnMakeChildAgent += OnMakeChild;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            // The InstantMessageModule itself doesn't do this, 
            // so lets see if things explode if we don't do it
            // scene.EventManager.OnClientClosed += OnClientClosed;

            if (m_groupData == null)
            {
                m_groupData = scene.RequestModuleInterface<IGroupsServicesConnector>();

                // No Groups Service Connector, then nothing works...
                if (m_groupData == null)
                {
                    m_groupsEnabled = false;
                    m_log.Error("[Groups]: Could not get IGroupsServicesConnector");
                    RemoveRegion(scene);
                    return;
                }
            }

            if (m_msgTransferModule == null)
            {
                m_msgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

                // No message transfer module, no notices, group invites, rejects, ejects, etc
                if (m_msgTransferModule == null)
                {
                    m_log.Warn("[Groups]: Could not get MessageTransferModule");
                }
            }

            if (m_UserManagement == null)
            {
                m_UserManagement = scene.RequestModuleInterface<IUserManagement>();
                if (m_UserManagement == null)
                    m_log.Warn("[Groups]: Could not get UserManagementModule");
            }

            lock (m_sceneList)
            {
                m_sceneList.Add(scene);
            }


        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnMakeRootAgent -= OnMakeRoot;
            scene.EventManager.OnMakeChildAgent -= OnMakeChild;
            scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;

            lock (m_sceneList)
            {
                m_sceneList.Remove(scene);
            }
        }

        public void Close()
        {
            if (!m_groupsEnabled)
                return;

            if (m_debugEnabled) m_log.Debug("[Groups]: Shutting down Groups module.");
        }

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "Groups Module V2"; }
        }

        public void PostInitialise()
        {
            // NoOp
        }

        #endregion

        #region EventHandlers
        private void OnNewClient(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
            client.OnRequestAvatarProperties += OnRequestAvatarProperties;
        }

        private void OnMakeRoot(ScenePresence sp)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            sp.ControllingClient.OnUUIDGroupNameRequest += HandleUUIDGroupNameRequest;
            // Used for Notices and Group Invites/Accept/Reject
            sp.ControllingClient.OnInstantMessage += OnInstantMessage;

            // Send client their groups information.
            SendAgentGroupDataUpdate(sp.ControllingClient, sp.UUID);
        }

        private void OnMakeChild(ScenePresence sp)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            sp.ControllingClient.OnUUIDGroupNameRequest -= HandleUUIDGroupNameRequest;
            // Used for Notices and Group Invites/Accept/Reject
            sp.ControllingClient.OnInstantMessage -= OnInstantMessage;
        }

        private void OnRequestAvatarProperties(IClientAPI remoteClient, UUID avatarID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            //GroupMembershipData[] avatarGroups = m_groupData.GetAgentGroupMemberships(GetRequestingAgentID(remoteClient), avatarID).ToArray();
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
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

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
                    if (m_debugEnabled) m_log.WarnFormat("[Groups]: Client closed that wasn't registered here.");
                }

                
            }
        }
        */

        private void OnAgentDataUpdateRequest(IClientAPI remoteClient, UUID dataForAgentID, UUID sessionID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            UUID activeGroupID = UUID.Zero;
            string activeGroupTitle = string.Empty;
            string activeGroupName = string.Empty;
            ulong activeGroupPowers  = (ulong)GroupPowers.None;

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(GetRequestingAgentIDStr(remoteClient), dataForAgentID.ToString());
            if (membership != null)
            {
                activeGroupID = membership.GroupID;
                activeGroupTitle = membership.GroupTitle;
                activeGroupPowers = membership.GroupPowers;
            }

            SendAgentDataUpdate(remoteClient, dataForAgentID, activeGroupID, activeGroupName, activeGroupPowers, activeGroupTitle);

            SendScenePresenceUpdate(dataForAgentID, activeGroupTitle);
        }

        private void HandleUUIDGroupNameRequest(UUID GroupID, IClientAPI remoteClient)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string GroupName;

            GroupRecord group = m_groupData.GetGroupRecord(GetRequestingAgentIDStr(remoteClient), GroupID, null);
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
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            //m_log.DebugFormat("[Groups]: IM From {0} to {1} msg {2} type {3}", im.fromAgentID, im.toAgentID, im.message, (InstantMessageDialog)im.dialog);
            // Group invitations
            if ((im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept) || (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline))
            {
                UUID inviteID = new UUID(im.imSessionID);
                GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite(GetRequestingAgentIDStr(remoteClient), inviteID);

                if (inviteInfo == null)
                {
                    if (m_debugEnabled) m_log.WarnFormat("[Groups]: Received an Invite IM for an invite that does not exist {0}.", inviteID);
                    return;
                }

                //m_log.DebugFormat("[XXX]: Invite is for Agent {0} to Group {1}.", inviteInfo.AgentID, inviteInfo.GroupID);

                UUID fromAgentID = new UUID(im.fromAgentID);
                UUID invitee = UUID.Zero;
                string tmp = string.Empty;
                Util.ParseUniversalUserIdentifier(inviteInfo.AgentID, out invitee, out tmp, out tmp, out tmp, out tmp);
                if ((inviteInfo != null) && (fromAgentID == invitee))
                {
                    // Accept
                    if (im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept)
                    {
                        //m_log.DebugFormat("[XXX]: Received an accept invite notice.");

                        // and the sessionid is the role
                        string reason = string.Empty;
                        if (!m_groupData.AddAgentToGroup(GetRequestingAgentIDStr(remoteClient), invitee.ToString(), inviteInfo.GroupID, inviteInfo.RoleID, string.Empty, out reason))
                            remoteClient.SendAgentAlertMessage("Unable to add you to the group: " + reason, false);
                        else
                        {
                            GridInstantMessage msg = new GridInstantMessage();
                            msg.imSessionID = UUID.Zero.Guid;
                            msg.fromAgentID = UUID.Zero.Guid;
                            msg.toAgentID = invitee.Guid;
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

                            OutgoingInstantMessage(msg, invitee);

                            UpdateAllClientsWithGroupInfo(invitee);
                        }

                        m_groupData.RemoveAgentToGroupInvite(GetRequestingAgentIDStr(remoteClient), inviteID);

                    }

                    // Reject
                    if (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline)
                    {
                        if (m_debugEnabled) m_log.DebugFormat("[Groups]: Received a reject invite notice.");
                        m_groupData.RemoveAgentToGroupInvite(GetRequestingAgentIDStr(remoteClient), inviteID);

                        m_groupData.RemoveAgentFromGroup(GetRequestingAgentIDStr(remoteClient), inviteInfo.AgentID, inviteInfo.GroupID);
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
                if (m_groupData.GetGroupRecord(GetRequestingAgentIDStr(remoteClient), GroupID, null) != null)
                {
                    UUID NoticeID = UUID.Random();
                    string Subject = im.message.Substring(0, im.message.IndexOf('|'));
                    string Message = im.message.Substring(Subject.Length + 1);

                    InventoryItemBase item = null;
                    bool hasAttachment = false;

                    if (im.binaryBucket.Length >= 1 && im.binaryBucket[0] > 0)
                    {
                        hasAttachment = true;
                        string binBucket = OpenMetaverse.Utils.BytesToString(im.binaryBucket);
                        binBucket = binBucket.Remove(0, 14).Trim();

                        OSD binBucketOSD = OSDParser.DeserializeLLSDXml(binBucket);
                        if (binBucketOSD is OSDMap)
                        {
                            OSDMap binBucketMap = (OSDMap)binBucketOSD;

                            UUID itemID = binBucketMap["item_id"].AsUUID();
                            UUID ownerID = binBucketMap["owner_id"].AsUUID();
                            item = new InventoryItemBase(itemID, ownerID);
                            item = m_sceneList[0].InventoryService.GetItem(item);
                        }
                        else
                            m_log.DebugFormat("[Groups]: Received OSD with unexpected type: {0}", binBucketOSD.GetType());
                    }

                    if (m_groupData.AddGroupNotice(GetRequestingAgentIDStr(remoteClient), GroupID, NoticeID, im.fromAgentName, Subject, Message,
                        hasAttachment, 
                        (byte)(item == null ? 0 : item.AssetType),
                        item == null ? null : item.Name, 
                        item == null ? UUID.Zero : item.ID,
                        item == null ? UUID.Zero.ToString() : item.Owner.ToString()))
                    {
                        if (OnNewGroupNotice != null)
                        {
                            OnNewGroupNotice(GroupID, NoticeID);
                        }

                        // Send notice out to everyone that wants notices
                        // Build notice IIM
                        GridInstantMessage msg = CreateGroupNoticeIM(UUID.Zero, NoticeID, (byte)OpenMetaverse.InstantMessageDialog.GroupNotice);
                        foreach (GroupMembersData member in m_groupData.GetGroupMembers(GetRequestingAgentIDStr(remoteClient), GroupID))
                        {
                            if (member.AcceptNotices)
                            {
                                msg.toAgentID = member.AgentID.Guid;
                                OutgoingInstantMessage(msg, member.AgentID);
                            }
                        }
                    }
                }
            }

            if (im.dialog == (byte)InstantMessageDialog.GroupNoticeInventoryAccepted)
            {
                if (im.binaryBucket.Length < 16) // Invalid
                    return;

                //// 16 bytes are the UUID. Maybe.
//                UUID folderID = new UUID(im.binaryBucket, 0);
                UUID noticeID = new UUID(im.imSessionID);

                GroupNoticeInfo notice = m_groupData.GetGroupNotice(remoteClient.AgentId.ToString(), noticeID);
                if (notice != null)
                {
                    UUID giver = new UUID(im.toAgentID);
                    string tmp = string.Empty;
                    Util.ParseUniversalUserIdentifier(notice.noticeData.AttachmentOwnerID, out giver, out tmp, out tmp, out tmp, out tmp);

                    m_log.DebugFormat("[Groups]: Giving inventory from {0} to {1}", giver, remoteClient.AgentId);
                    InventoryItemBase itemCopy = ((Scene)(remoteClient.Scene)).GiveInventoryItem(remoteClient.AgentId, 
                        giver, notice.noticeData.AttachmentItemID);

                    if (itemCopy == null)
                    {
                        remoteClient.SendAgentAlertMessage("Can't find item to give. Nothing given.", false);
                        return;
                    }

                    remoteClient.SendInventoryItemCreateUpdate(itemCopy, 0);
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
                    UUID groupID = new UUID(im.imSessionID);
                    ejectee.SendAgentDropGroup(groupID);
                }
            }
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            if (m_debugEnabled) m_log.InfoFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

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
            return m_groupData.GetGroupRecord(UUID.Zero.ToString(), GroupID, null);
        }

        public GroupRecord GetGroupRecord(string name)
        {
            return m_groupData.GetGroupRecord(UUID.Zero.ToString(), UUID.Zero, name);
        }
        
        public void ActivateGroup(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentActiveGroup(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID);

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
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> agentRoles = m_groupData.GetAgentGroupRoles(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID);
            GroupMembershipData agentMembership = m_groupData.GetAgentGroupMembership(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID);

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
            if (m_debugEnabled) 
                m_log.DebugFormat(
                    "[Groups]: GroupMembersRequest called for {0} from client {1}", groupID, remoteClient.Name);
            
            List<GroupMembersData> data = m_groupData.GetGroupMembers(GetRequestingAgentIDStr(remoteClient), groupID);

            if (m_debugEnabled)
            {
                foreach (GroupMembersData member in data)
                {
                    m_log.DebugFormat("[Groups]: Member({0}) - IsOwner({1})", member.AgentID, member.IsOwner);
                }
            }

            return data;

        }

        public List<GroupRolesData> GroupRoleDataRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> data = m_groupData.GetGroupRoles(GetRequestingAgentIDStr(remoteClient), groupID);

            return data;
        }

        public List<GroupRoleMembersData> GroupRoleMembersRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRoleMembersData> data = m_groupData.GetGroupRoleMembers(GetRequestingAgentIDStr(remoteClient), groupID);

            if (m_debugEnabled)
            {
                foreach (GroupRoleMembersData member in data)
                {
                    m_log.DebugFormat("[Groups]: Member({0}) - Role({1})", member.MemberID, member.RoleID);
                }
            }
            return data;
        }

        public GroupProfileData GroupProfileRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupProfileData profile = new GroupProfileData();

            // just to get the OwnerRole...
            ExtendedGroupRecord groupInfo = m_groupData.GetGroupRecord(GetRequestingAgentIDStr(remoteClient), groupID, string.Empty);
            GroupMembershipData memberInfo = m_groupData.GetAgentGroupMembership(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID);
            if (groupInfo != null)
            {
                profile.AllowPublish = groupInfo.AllowPublish;
                profile.Charter = groupInfo.Charter;
                profile.FounderID = groupInfo.FounderID;
                profile.GroupID = groupID;
                profile.GroupMembershipCount = groupInfo.MemberCount;
                profile.GroupRolesCount = groupInfo.RoleCount;
                profile.InsigniaID = groupInfo.GroupPicture;
                profile.MaturePublish = groupInfo.MaturePublish;
                profile.MembershipFee = groupInfo.MembershipFee;
                profile.Money = 0;
                profile.Name = groupInfo.GroupName;
                profile.OpenEnrollment = groupInfo.OpenEnrollment;
                profile.OwnerRole = groupInfo.OwnerRoleID;
                profile.ShowInList = groupInfo.ShowInList;
            }
            if (memberInfo != null)
            {
                profile.MemberTitle = memberInfo.GroupTitle;
                profile.PowersMask = memberInfo.GroupPowers;
            }

            return profile;
        }

        public GroupMembershipData[] GetMembershipData(UUID agentID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMemberships(UUID.Zero.ToString(), agentID.ToString()).ToArray();
        }

        public GroupMembershipData GetMembershipData(UUID groupID, UUID agentID)
        {
            if (m_debugEnabled) 
                m_log.DebugFormat(
                    "[Groups]: {0} called with groupID={1}, agentID={2}",
                    System.Reflection.MethodBase.GetCurrentMethod().Name, groupID, agentID);

            return m_groupData.GetAgentGroupMembership(UUID.Zero.ToString(), agentID.ToString(), groupID);
        }

        public void UpdateGroupInfo(IClientAPI remoteClient, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Note: Permissions checking for modification rights is handled by the Groups Server/Service
            string reason = string.Empty;
            if (!m_groupData.UpdateGroup(GetRequestingAgentIDStr(remoteClient), groupID, charter, showInList, insigniaID, membershipFee,
                openEnrollment, allowPublish, maturePublish, out reason))
                remoteClient.SendAgentAlertMessage(reason, false);
        }

        public void SetGroupAcceptNotices(IClientAPI remoteClient, UUID groupID, bool acceptNotices, bool listInProfile)
        {
            // Note: Permissions checking for modification rights is handled by the Groups Server/Service
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.UpdateMembership(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID, acceptNotices, listInProfile);
        }

        public UUID CreateGroup(IClientAPI remoteClient, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called in {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, remoteClient.Scene.RegionInfo.RegionName);

            if (m_groupData.GetGroupRecord(GetRequestingAgentIDStr(remoteClient), UUID.Zero, name) != null)
            {
                remoteClient.SendCreateGroupReply(UUID.Zero, false, "A group with the same name already exists.");
                return UUID.Zero;
            }

            // check user level
            ScenePresence avatar = null;
            Scene scene = (Scene)remoteClient.Scene;
            scene.TryGetScenePresence(remoteClient.AgentId, out avatar);

            if (avatar != null)
            {
                if (avatar.UserLevel < m_levelGroupCreate)
                {
                    remoteClient.SendCreateGroupReply(UUID.Zero, false, String.Format("Insufficient permissions to create a group. Requires level {0}", m_levelGroupCreate));
                    return UUID.Zero;
                }
            }

            // check funds
            // is there is a money module present ?
            IMoneyModule money = scene.RequestModuleInterface<IMoneyModule>();
            if (money != null)
            {
                // do the transaction, that is if the agent has got sufficient funds
                if (!money.AmountCovered(remoteClient.AgentId, money.GroupCreationCharge)) {
                    remoteClient.SendCreateGroupReply(UUID.Zero, false, "Insufficient funds to create a group.");
                    return UUID.Zero;
                }
            }

            string reason = string.Empty;
            UUID groupID = m_groupData.CreateGroup(remoteClient.AgentId, name, charter, showInList, insigniaID, membershipFee, openEnrollment, 
                allowPublish, maturePublish, remoteClient.AgentId, out reason);

            if (groupID != UUID.Zero)
            {
                if (money != null)
                    money.ApplyCharge(remoteClient.AgentId, money.GroupCreationCharge, MoneyTransactionType.GroupCreate);

                remoteClient.SendCreateGroupReply(groupID, true, "Group created successfullly");

                // Update the founder with new group information.
                SendAgentGroupDataUpdate(remoteClient, GetRequestingAgentID(remoteClient));
            }
            else
                remoteClient.SendCreateGroupReply(groupID, false, reason);

            return groupID;
        }

        public GroupNoticeData[] GroupNoticesListRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // ToDo: check if agent is a member of group and is allowed to see notices?

            List<ExtendedGroupNoticeData> notices = m_groupData.GetGroupNotices(GetRequestingAgentIDStr(remoteClient), groupID);
            List<GroupNoticeData> os_notices = new List<GroupNoticeData>();
            foreach (ExtendedGroupNoticeData n in notices)
            {
                GroupNoticeData osn = n.ToGroupNoticeData();
                os_notices.Add(osn);
            }

            return os_notices.ToArray();
        }

        /// <summary>
        /// Get the title of the agent's current role.
        /// </summary>
        public string GetGroupTitle(UUID avatarID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(UUID.Zero.ToString(), avatarID.ToString());
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
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentActiveGroupRole(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID, titleRoleID);

            // TODO: Not sure what all is needed here, but if the active group role change is for the group
            // the client currently has set active, then we need to do a scene presence update too
            // if (m_groupData.GetAgentActiveMembership(GetRequestingAgentID(remoteClient)).GroupID == GroupID)
                
            UpdateAllClientsWithGroupInfo(GetRequestingAgentID(remoteClient));
        }


        public void GroupRoleUpdate(IClientAPI remoteClient, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, byte updateType)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Security Checks are handled in the Groups Service.

            switch ((OpenMetaverse.GroupRoleUpdate)updateType)
            {
                case OpenMetaverse.GroupRoleUpdate.Create:
                    string reason = string.Empty;
                    if (!m_groupData.AddGroupRole(GetRequestingAgentIDStr(remoteClient), groupID, UUID.Random(), name, description, title, powers, out reason))
                        remoteClient.SendAgentAlertMessage("Unable to create role: " + reason, false);
                    break;

                case OpenMetaverse.GroupRoleUpdate.Delete:
                    m_groupData.RemoveGroupRole(GetRequestingAgentIDStr(remoteClient), groupID, roleID);
                    break;

                case OpenMetaverse.GroupRoleUpdate.UpdateAll:
                case OpenMetaverse.GroupRoleUpdate.UpdateData:
                case OpenMetaverse.GroupRoleUpdate.UpdatePowers:
                    if (m_debugEnabled)
                    {
                        GroupPowers gp = (GroupPowers)powers;
                        m_log.DebugFormat("[Groups]: Role ({0}) updated with Powers ({1}) ({2})", name, powers.ToString(), gp.ToString());
                    }
                    m_groupData.UpdateGroupRole(GetRequestingAgentIDStr(remoteClient), groupID, roleID, name, description, title, powers);
                    break;

                case OpenMetaverse.GroupRoleUpdate.NoUpdate:
                default:
                    // No Op
                    break;

            }

            // TODO: This update really should send out updates for everyone in the role that just got changed.
            SendAgentGroupDataUpdate(remoteClient, GetRequestingAgentID(remoteClient));
        }

        public void GroupRoleChanges(IClientAPI remoteClient, UUID groupID, UUID roleID, UUID memberID, uint changes)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            // Todo: Security check

            switch (changes)
            {
                case 0:
                    // Add
                    m_groupData.AddAgentToGroupRole(GetRequestingAgentIDStr(remoteClient), memberID.ToString(), groupID, roleID);

                    break;
                case 1:
                    // Remove
                    m_groupData.RemoveAgentFromGroupRole(GetRequestingAgentIDStr(remoteClient), memberID.ToString(), groupID, roleID);
                    
                    break;
                default:
                    m_log.ErrorFormat("[Groups]: {0} does not understand changes == {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, changes);
                    break;
            }

            // TODO: This update really should send out updates for everyone in the role that just got changed.
            SendAgentGroupDataUpdate(remoteClient, GetRequestingAgentID(remoteClient));
        }

        public void GroupNoticeRequest(IClientAPI remoteClient, UUID groupNoticeID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called for notice {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, groupNoticeID);

            GridInstantMessage msg = CreateGroupNoticeIM(remoteClient.AgentId, groupNoticeID, (byte)InstantMessageDialog.GroupNoticeRequested);

            OutgoingInstantMessage(msg, GetRequestingAgentID(remoteClient));
        }

        public GridInstantMessage CreateGroupNoticeIM(UUID agentID, UUID groupNoticeID, byte dialog)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GridInstantMessage msg = new GridInstantMessage();
            byte[] bucket;

            msg.imSessionID = groupNoticeID.Guid;
            msg.toAgentID = agentID.Guid;
            msg.dialog = dialog;
            // msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupNotice;
            msg.fromGroup = true;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = UUID.Zero.Guid;

            GroupNoticeInfo info = m_groupData.GetGroupNotice(agentID.ToString(), groupNoticeID);
            if (info != null)
            {
                msg.fromAgentID = info.GroupID.Guid;
                msg.timestamp = info.noticeData.Timestamp;
                msg.fromAgentName = info.noticeData.FromName;
                msg.message = info.noticeData.Subject + "|" + info.Message;
                if (info.noticeData.HasAttachment)
                {
                    byte[] name = System.Text.Encoding.UTF8.GetBytes(info.noticeData.AttachmentName);
                    bucket = new byte[19 + name.Length];
                    bucket[0] = 1; // has attachment?
                    bucket[1] = info.noticeData.AttachmentType; // attachment type
                    name.CopyTo(bucket, 18);
                }
                else
                {
                    bucket = new byte[19];
                    bucket[0] = 0; // Has att?
                    bucket[1] = 0; // type
                    bucket[18] = 0; // null terminated
                }

                info.GroupID.ToBytes(bucket, 2);
                msg.binaryBucket = bucket;
            }
            else
            {
                m_log.DebugFormat("[Groups]: Group Notice {0} not found, composing empty message.", groupNoticeID);
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
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Send agent information about his groups
            SendAgentGroupDataUpdate(remoteClient, GetRequestingAgentID(remoteClient));
        }

        public void JoinGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string reason = string.Empty;
            // Should check to see if OpenEnrollment, or if there's an outstanding invitation
            if (m_groupData.AddAgentToGroup(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID, UUID.Zero, string.Empty, out reason))
            {

                remoteClient.SendJoinGroupReply(groupID, true);

                // Should this send updates to everyone in the group?
                SendAgentGroupDataUpdate(remoteClient, GetRequestingAgentID(remoteClient));

                if (reason != string.Empty)
                    // A warning
                    remoteClient.SendAlertMessage("Warning: " + reason);
            }
            else
                remoteClient.SendJoinGroupReply(groupID, false);
        }

        public void LeaveGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.RemoveAgentFromGroup(GetRequestingAgentIDStr(remoteClient), GetRequestingAgentIDStr(remoteClient), groupID);

            remoteClient.SendLeaveGroupReply(groupID, true);

            remoteClient.SendAgentDropGroup(groupID);

            // SL sends out notifcations to the group messaging session that the person has left
            // Should this also update everyone who is in the group?
            SendAgentGroupDataUpdate(remoteClient, GetRequestingAgentID(remoteClient));
        }

        public void EjectGroupMemberRequest(IClientAPI remoteClient, UUID groupID, UUID ejecteeID)
        {
            EjectGroupMember(remoteClient, GetRequestingAgentID(remoteClient), groupID, ejecteeID);
        }

        public void EjectGroupMember(IClientAPI remoteClient, UUID agentID, UUID groupID, UUID ejecteeID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Todo: Security check?
            m_groupData.RemoveAgentFromGroup(agentID.ToString(), ejecteeID.ToString(), groupID);

            string agentName;
            RegionInfo regionInfo;

            // remoteClient provided or just agentID?
            if (remoteClient != null)
            {
                agentName = remoteClient.Name;
                regionInfo = remoteClient.Scene.RegionInfo;
                remoteClient.SendEjectGroupMemberReply(agentID, groupID, true);
            }
            else
            {
                IClientAPI client = GetActiveClient(agentID);

                if (client != null)
                {
                    agentName = client.Name;
                    regionInfo = client.Scene.RegionInfo;
                    client.SendEjectGroupMemberReply(agentID, groupID, true);
                }
                else
                {
                    regionInfo = m_sceneList[0].RegionInfo;
                    UserAccount acc = m_sceneList[0].UserAccountService.GetUserAccount(regionInfo.ScopeID, agentID);

                    if (acc != null)
                    {
                        agentName = acc.FirstName + " " + acc.LastName;
                    }
                    else
                    {
                        agentName = "Unknown member";
                    }
                }
            }

            GroupRecord groupInfo = m_groupData.GetGroupRecord(agentID.ToString(), groupID, null);

            UserAccount account = m_sceneList[0].UserAccountService.GetUserAccount(regionInfo.ScopeID, ejecteeID);
            if ((groupInfo == null) || (account == null))
            {
                return;
            }

            // Send Message to Ejectee
            GridInstantMessage msg = new GridInstantMessage();
            
            msg.imSessionID = UUID.Zero.Guid;
            msg.fromAgentID = agentID.Guid;
            // msg.fromAgentID = info.GroupID;
            msg.toAgentID = ejecteeID.Guid;
            //msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
            msg.timestamp = 0;
            msg.fromAgentName = agentName;
            msg.message = string.Format("You have been ejected from '{1}' by {0}.", agentName, groupInfo.GroupName);
            msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.MessageFromAgent;
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = regionInfo.RegionID.Guid;
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
            msg.fromAgentID = agentID.Guid;
            msg.toAgentID = agentID.Guid;
            msg.timestamp = 0;
            msg.fromAgentName = agentName;
            if (account != null)
            {
                msg.message = string.Format("{2} has been ejected from '{1}' by {0}.", agentName, groupInfo.GroupName, account.FirstName + " " + account.LastName);
            }
            else
            {
                msg.message = string.Format("{2} has been ejected from '{1}' by {0}.", agentName, groupInfo.GroupName, "Unknown member");
            }
            msg.dialog = (byte)210; //interop
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = regionInfo.RegionID.Guid;
            msg.binaryBucket = new byte[0];
            OutgoingInstantMessage(msg, agentID);


            // SL sends out messages to everyone in the group
            // Who all should receive updates and what should they be updated with?
            UpdateAllClientsWithGroupInfo(ejecteeID);
        }

        public void InviteGroupRequest(IClientAPI remoteClient, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            InviteGroup(remoteClient, GetRequestingAgentID(remoteClient), groupID, invitedAgentID, roleID);
        }

        public void InviteGroup(IClientAPI remoteClient, UUID agentID, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string agentName = m_UserManagement.GetUserName(agentID);
            RegionInfo regionInfo = m_sceneList[0].RegionInfo;

            GroupRecord group = m_groupData.GetGroupRecord(agentID.ToString(), groupID, null);
            if (group == null)
            {
                m_log.DebugFormat("[Groups]: No such group {0}", groupID);
                return;
            }

            // Todo: Security check, probably also want to send some kind of notification
            UUID InviteID = UUID.Random();

            if (m_groupData.AddAgentToGroupInvite(agentID.ToString(), InviteID, groupID, roleID, invitedAgentID.ToString()))
            {
                if (m_msgTransferModule != null)
                {
                    Guid inviteUUID = InviteID.Guid;

                    GridInstantMessage msg = new GridInstantMessage();

                    msg.imSessionID = inviteUUID;

                    // msg.fromAgentID = agentID.Guid;
                    msg.fromAgentID = groupID.Guid;
                    msg.toAgentID = invitedAgentID.Guid;
                    //msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                    msg.timestamp = 0;
                    msg.fromAgentName = agentName;
                    msg.message = string.Format("{0} has invited you to join a group called {1}. There is no cost to join this group.", agentName, group.GroupName);
                    msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.GroupInvitation;
                    msg.fromGroup = true;
                    msg.offline = (byte)0;
                    msg.ParentEstateID = 0;
                    msg.Position = Vector3.Zero;
                    msg.RegionID = regionInfo.RegionID.Guid;
                    msg.binaryBucket = new byte[20];

                    OutgoingInstantMessage(msg, invitedAgentID);
                }
            }
        }

        public List<DirGroupsReplyData> FindGroups(IClientAPI remoteClient, string query)
        {
            return m_groupData.FindGroups(GetRequestingAgentIDStr(remoteClient), query);
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
                ScenePresence sp = scene.GetScenePresence(agentID);
                if (sp != null)
                {
                    if (!sp.IsChildAgent)
                    {
                        return sp.ControllingClient;
                    }
                    else
                    {
                        child = sp.ControllingClient;
                    }
                }
            }

            // If we didn't find a root, then just return whichever child we found, or null if none
            return child;
        }

        /// <summary>
        /// Send 'remoteClient' the group membership 'data' for agent 'dataForAgentID'.
        /// </summary>
        private void SendGroupMembershipInfoViaCaps(IClientAPI remoteClient, UUID dataForAgentID, GroupMembershipData[] data)
        {
            if (m_debugEnabled) m_log.InfoFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDArray AgentData = new OSDArray(1);
            OSDMap AgentDataMap = new OSDMap(1);
            AgentDataMap.Add("AgentID", OSD.FromUUID(dataForAgentID));
            AgentData.Add(AgentDataMap);


            OSDArray GroupData = new OSDArray(data.Length);
            OSDArray NewGroupData = new OSDArray(data.Length);

            foreach (GroupMembershipData membership in data)
            {
                if (GetRequestingAgentID(remoteClient) != dataForAgentID)
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
                GroupDataMap.Add("GroupPowers", OSD.FromULong(membership.GroupPowers));
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

            if (m_debugEnabled)
            {
                m_log.InfoFormat("[Groups]: {0}", OSDParser.SerializeJsonString(llDataStruct));
            }

            IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();

            if (queue != null)
            {
                queue.Enqueue(queue.BuildEvent("AgentGroupDataUpdate", llDataStruct), GetRequestingAgentID(remoteClient));
            }
            
        }

        private void SendScenePresenceUpdate(UUID AgentID, string Title)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: Updating scene title for {0} with title: {1}", AgentID, Title);

            ScenePresence presence = null;

            foreach (Scene scene in m_sceneList)
            {
                presence = scene.GetScenePresence(AgentID);
                if (presence != null)
                {
                    if (presence.Grouptitle != Title)
                    {
                        presence.Grouptitle = Title;

                        if (! presence.IsChildAgent)
                            presence.SendAvatarDataToAllAgents();
                    }
                }
            }
        }

        /// <summary>
        /// Send updates to all clients who might be interested in groups data for dataForClientID
        /// </summary>
        private void UpdateAllClientsWithGroupInfo(UUID dataForClientID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

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
            if (m_debugEnabled) m_log.InfoFormat("[Groups]: {0} called for {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, remoteClient.Name);

            // TODO: All the client update functions need to be reexamined because most do too much and send too much stuff

            OnAgentDataUpdateRequest(remoteClient, dataForAgentID, UUID.Zero);

            // Need to send a group membership update to the client
            // UDP version doesn't seem to behave nicely.  But we're going to send it out here
            // with an empty group membership to hopefully remove groups being displayed due
            // to the core Groups Stub
            //remoteClient.SendGroupMembership(new GroupMembershipData[0]);

            GroupMembershipData[] membershipArray = GetProfileListedGroupMemberships(remoteClient, dataForAgentID);
            SendGroupMembershipInfoViaCaps(remoteClient, dataForAgentID, membershipArray);
            //remoteClient.SendAvatarGroupsReply(dataForAgentID, membershipArray);
            if (remoteClient.AgentId == dataForAgentID)
                remoteClient.RefreshGroupMembership();
        }

        /// <summary>
        /// Get a list of groups memberships for the agent that are marked "ListInProfile"
        /// (unless that agent has a godLike aspect, in which case get all groups)
        /// </summary>
        /// <param name="dataForAgentID"></param>
        /// <returns></returns>
        private GroupMembershipData[] GetProfileListedGroupMemberships(IClientAPI requestingClient, UUID dataForAgentID)
        {
            List<GroupMembershipData> membershipData = m_groupData.GetAgentGroupMemberships(requestingClient.AgentId.ToString(), dataForAgentID.ToString());
            GroupMembershipData[] membershipArray;

            //  cScene and property accessor 'isGod' are in support of the opertions to bypass 'hidden' group attributes for
            // those with a GodLike aspect.
            Scene cScene = (Scene)requestingClient.Scene;
            bool isGod = cScene.Permissions.IsGod(requestingClient.AgentId);

            if (isGod)
            {
                membershipArray = membershipData.ToArray();
            }
            else
            {
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
            }
            
            if (m_debugEnabled)
            {
                m_log.InfoFormat("[Groups]: Get group membership information for {0} requested by {1}", dataForAgentID, requestingClient.AgentId);
                foreach (GroupMembershipData membership in membershipArray)
                {
                    m_log.InfoFormat("[Groups]: {0} :: {1} - {2} - {3}", dataForAgentID, membership.GroupName, membership.GroupTitle, membership.GroupPowers);
                }
            }

            return membershipArray;
        }

 
        private void SendAgentDataUpdate(IClientAPI remoteClient, UUID dataForAgentID, UUID activeGroupID, string activeGroupName, ulong activeGroupPowers, string activeGroupTitle)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: All the client update functions need to be reexamined because most do too much and send too much stuff
            string firstname = "Unknown", lastname = "Unknown";
            string name = m_UserManagement.GetUserName(dataForAgentID);
            if (!string.IsNullOrEmpty(name))
            {
                string[] parts = name.Split(new char[] { ' ' });
                if (parts.Length >= 2)
                {
                    firstname = parts[0];
                    lastname = parts[1];
                }
            }
            
            remoteClient.SendAgentDataUpdate(dataForAgentID, activeGroupID, firstname,
                    lastname, activeGroupPowers, activeGroupName,
                    activeGroupTitle);
        }

        #endregion

        #region IM Backed Processes

        private void OutgoingInstantMessage(GridInstantMessage msg, UUID msgTo)
        {
            if (m_debugEnabled) m_log.InfoFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            IClientAPI localClient = GetActiveClient(msgTo);
            if (localClient != null)
            {
                if (m_debugEnabled) m_log.InfoFormat("[Groups]: MsgTo ({0}) is local, delivering directly", localClient.Name);
                localClient.SendInstantMessage(msg);
            }
            else if (m_msgTransferModule != null)
            {
                if (m_debugEnabled) m_log.InfoFormat("[Groups]: MsgTo ({0}) is not local, delivering via TransferModule", msgTo);
                m_msgTransferModule.SendInstantMessage(msg, delegate(bool success) { if (m_debugEnabled) m_log.DebugFormat("[Groups]: Message Sent: {0}", success?"Succeeded":"Failed"); });
            }
        }

        public void NotifyChange(UUID groupID)
        {
            // Notify all group members of a chnge in group roles and/or
            // permissions
            //
        }

        #endregion

        private string GetRequestingAgentIDStr(IClientAPI client)
        {
            return GetRequestingAgentID(client).ToString();
        }

        private UUID GetRequestingAgentID(IClientAPI client)
        {
            UUID requestingAgentID = UUID.Zero;
            if (client != null)
            {
                requestingAgentID = client.AgentId;
            }
            return requestingAgentID;
        }

    }

}
