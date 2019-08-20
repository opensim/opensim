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
using System.Text;
using DirFindFlags = OpenMetaverse.DirectoryManager.DirFindFlags;

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GroupsModule")]
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

        private IMessageTransferModule m_msgTransferModule;

        private IGroupsMessagingModule m_groupsMessagingModule;

        private IGroupsServicesConnector m_groupData;

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

                m_log.InfoFormat("[GROUPS]: Initializing {0}", this.Name);

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
                    "Debug",
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

            MainConsole.Instance.Output("{0} verbose logging set to {1}", null, Name, m_debugEnabled);
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
                    m_log.Warn("[GROUPS]: Could not get IMessageTransferModule");
                }
            }

            if (m_groupsMessagingModule == null)
            {
                m_groupsMessagingModule = scene.RequestModuleInterface<IGroupsMessagingModule>();

                // No message transfer module, no notices, group invites, rejects, ejects, etc
                if (m_groupsMessagingModule == null)
                    m_log.Warn("[GROUPS]: Could not get IGroupsMessagingModule");
            }

            lock (m_sceneList)
            {
                m_sceneList.Add(scene);
            }

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnMakeRootAgent += OnMakeRoot;
            scene.EventManager.OnMakeChildAgent += OnMakeChild;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnClientClosed += OnClientClosed;

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
            scene.EventManager.OnClientClosed -= OnClientClosed;
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

        private void OnMakeRoot(ScenePresence sp)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            sp.ControllingClient.OnUUIDGroupNameRequest += HandleUUIDGroupNameRequest;
            // Used for Notices and Group Invites/Accept/Reject
            sp.ControllingClient.OnInstantMessage += OnInstantMessage;

            // comented out because some viewers no longer suport it
            //  sp.ControllingClient.AddGenericPacketHandler("avatargroupsrequest", AvatarGroupsRequest);

            // Send out group data update for compatibility.
            // There might be some problem with the thread we're generating this on but not
            //   doing the update at this time causes problems (Mantis #7920 and #7915)
            // TODO: move sending this update to a later time in the rootification of the client.
            if(!sp.m_haveGroupInformation)
                SendAgentGroupDataUpdate(sp.ControllingClient, false);
        }

        private void OnMakeChild(ScenePresence sp)
        {
            if (m_debugEnabled) m_log.DebugFormat("[Groups]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            sp.ControllingClient.OnUUIDGroupNameRequest -= HandleUUIDGroupNameRequest;
            sp.ControllingClient.OnInstantMessage -= OnInstantMessage;
        }

        private void OnNewClient(IClientAPI client)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            client.OnAgentDataUpdateRequest += OnAgentDataUpdateRequest;
            client.OnRequestAvatarProperties += OnRequestAvatarProperties;


        }

/*  this should be the right message to ask for other avatars groups

        private void AvatarGroupsRequest(Object sender, string method, List<String> args)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            if (!(sender is IClientAPI))
                return;

            IClientAPI remoteClient = (IClientAPI)sender;

            UUID avatarID;
            UUID.TryParse(args[0], out avatarID);

            if (avatarID != UUID.Zero)
            {
                GroupMembershipData[] avatarGroups = GetProfileListedGroupMemberships(remoteClient, avatarID);
                remoteClient.SendAvatarGroupsReply(avatarID, avatarGroups);
            }
        }
*/

        // this should not be used to send groups memberships, but some viewers do expect it
        // it does send unnecessary memberships, when viewers just want other properties information
        private void OnRequestAvatarProperties(IClientAPI remoteClient, UUID avatarID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupMembershipData[] avatarGroups = GetProfileListedGroupMemberships(remoteClient, avatarID);
            remoteClient.SendAvatarGroupsReply(avatarID, avatarGroups);
        }


        private void OnClientClosed(UUID AgentId, Scene scene)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            if (scene == null)
                return;

            ScenePresence sp = scene.GetScenePresence(AgentId);
            IClientAPI client = sp.ControllingClient;
            if (client != null)
            {
                client.OnAgentDataUpdateRequest -= OnAgentDataUpdateRequest;
                client.OnRequestAvatarProperties -= OnRequestAvatarProperties;
                // make child possible not called?
                client.OnUUIDGroupNameRequest -= HandleUUIDGroupNameRequest;
                client.OnInstantMessage -= OnInstantMessage;
            }

/*
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
*/
        }

        private void OnAgentDataUpdateRequest(IClientAPI remoteClient, UUID dataForAgentID, UUID sessionID)
        {
            // this a private message for own agent only
            if (dataForAgentID != GetRequestingAgentID(remoteClient))
                return;

            SendAgentGroupDataUpdate(remoteClient, false);
            // its a info request not a change, so nothing is sent to others
            // they do get the group title with the avatar object update on arrivel to a region
        }

        private void HandleUUIDGroupNameRequest(UUID GroupID, IClientAPI remoteClient)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string GroupName;

            GroupRecord group = m_groupData.GetGroupRecord(GetRequestingAgentID(remoteClient), GroupID, null);
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
            if (m_debugEnabled)
                m_log.DebugFormat(
                    "[GROUPS]: {0} called for {1}, message type {2}",
                    System.Reflection.MethodBase.GetCurrentMethod().Name, remoteClient.Name, (InstantMessageDialog)im.dialog);

            // Group invitations
            if ((im.dialog == (byte)InstantMessageDialog.GroupInvitationAccept) || (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline))
            {
                UUID inviteID = new UUID(im.imSessionID);
                GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite(GetRequestingAgentID(remoteClient), inviteID);

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
                        m_groupData.AddAgentToGroup(GetRequestingAgentID(remoteClient), inviteInfo.AgentID, inviteInfo.GroupID, inviteInfo.RoleID);

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

                        IClientAPI inviteeClient = GetActiveRootClient(inviteInfo.AgentID);
                        if(inviteeClient !=null)
                        {
                            SendAgentGroupDataUpdate(inviteeClient,true);
                        }
                        m_groupData.RemoveAgentToGroupInvite(GetRequestingAgentID(remoteClient), inviteID);
                    }

                    // Reject
                    if (im.dialog == (byte)InstantMessageDialog.GroupInvitationDecline)
                    {
                        if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Received a reject invite notice.");
                        m_groupData.RemoveAgentToGroupInvite(GetRequestingAgentID(remoteClient), inviteID);
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
                if (m_groupData.GetGroupRecord(GetRequestingAgentID(remoteClient), GroupID, null) != null)
                {
                    UUID NoticeID = UUID.Random();
                    string Subject = im.message.Substring(0, im.message.IndexOf('|'));
                    string Message = im.message.Substring(Subject.Length + 1);

                    InventoryItemBase item = null;
                    bool hasAttachment = false;
                    UUID itemID = UUID.Zero;    //Assignment to quiet compiler
                    UUID ownerID = UUID.Zero;   //Assignment to quiet compiler
                    byte[] bucket;

                    if (im.binaryBucket.Length >= 1 && im.binaryBucket[0] > 0)
                    {
                        string binBucket = OpenMetaverse.Utils.BytesToString(im.binaryBucket);
                        binBucket = binBucket.Remove(0, 14).Trim();

                        OSDMap binBucketOSD = (OSDMap)OSDParser.DeserializeLLSDXml(binBucket);
                        if (binBucketOSD is OSD)
                        {
                            OSDMap binBucketMap = (OSDMap)binBucketOSD;

                            itemID = binBucketMap["item_id"].AsUUID();
                            ownerID = binBucketMap["owner_id"].AsUUID();

                            //Attempt to get the details of the attached item.
                            //If sender doesn't own the attachment, the item
                            //variable will be set to null and attachment will
                            //not be included with the group notice.
                            Scene scene = (Scene)remoteClient.Scene;
                            item = scene.InventoryService.GetItem(ownerID, itemID);

                            if (item != null)
                            {
                                //Got item details so include the attachment.
                                hasAttachment = true;
                            }
                        }
                        else
                        {
                            m_log.DebugFormat("[Groups]: Received OSD with unexpected type: {0}", binBucketOSD.GetType());
                        }
                    }

                    if (hasAttachment)
                    {
                        //Bucket contains information about attachment.
                        //
                        //Byte offset and description of bucket data:
                        //0:  1 byte indicating if attachment is present
                        //1:  1 byte indicating the type of attachment
                        //2:  16 bytes - Group UUID
                        //18: 16 bytes - UUID of the attachment owner
                        //34: 16 bytes - UUID of the attachment
                        //50: variable - Name of the attachment
                        //??: NUL byte to terminate the attachment name
                        byte[] name = Encoding.UTF8.GetBytes(item.Name);
                        bucket = new byte[51 + name.Length];//3 bytes, 3 UUIDs, and name
                        bucket[0] = 1;                      //Has attachment flag
                        bucket[1] = (byte)item.InvType;     //Type of Attachment
                        GroupID.ToBytes(bucket, 2);
                        ownerID.ToBytes(bucket, 18);
                        itemID.ToBytes(bucket, 34);
                        name.CopyTo(bucket, 50);
                    }
                    else
                    {
                        bucket = new byte[19];
                        bucket[0] = 0;  //Has attachment flag
                        bucket[1] = 0;  //Type of attachment
                        GroupID.ToBytes(bucket, 2);
                        bucket[18] = 0; //NUL terminate name of attachment
                    }

                    m_groupData.AddGroupNotice(GetRequestingAgentID(remoteClient), GroupID, NoticeID, im.fromAgentName, Subject, Message, bucket);
                    if (OnNewGroupNotice != null)
                    {
                        OnNewGroupNotice(GroupID, NoticeID);
                    }

                    if (m_debugEnabled)
                    {
                        foreach (GroupMembersData member in m_groupData.GetGroupMembers(GetRequestingAgentID(remoteClient), GroupID))
                        {
                            if (m_debugEnabled)
                            {
                                UserAccount targetUser
                                    = m_sceneList[0].UserAccountService.GetUserAccount(
                                        remoteClient.Scene.RegionInfo.ScopeID, member.AgentID);

                                if (targetUser != null)
                                {
                                    m_log.DebugFormat(
                                        "[GROUPS]: Prepping group notice {0} for agent: {1} who Accepts Notices ({2})",
                                        NoticeID, targetUser.FirstName + " " + targetUser.LastName, member.AcceptNotices);
                                }
                                else
                                {
                                    m_log.DebugFormat(
                                        "[GROUPS]: Prepping group notice {0} for agent: {1} who Accepts Notices ({2})",
                                        NoticeID, member.AgentID, member.AcceptNotices);
                                }
                            }
                        }
                    }

                    GridInstantMessage msg
                        = CreateGroupNoticeIM(UUID.Zero, NoticeID, (byte)OpenMetaverse.InstantMessageDialog.GroupNotice);

                    if (m_groupsMessagingModule != null)
                        m_groupsMessagingModule.SendMessageToGroup(
                            msg, GroupID, remoteClient.AgentId, gmd => gmd.AcceptNotices);
                }
            }

            if (im.dialog == (byte)InstantMessageDialog.GroupNoticeInventoryAccepted)
            {
                //Is bucket large enough to hold UUID of the attachment?
                if (im.binaryBucket.Length < 16)
                    return;

                UUID noticeID = new UUID(im.imSessionID);

                if (m_debugEnabled)
                    m_log.DebugFormat("[GROUPS]: Requesting notice {0} for {1}", noticeID, remoteClient.AgentId);

                GroupNoticeInfo notice = m_groupData.GetGroupNotice(GetRequestingAgentID(remoteClient), noticeID);
                if (notice != null)
                {
                    UUID giver = new UUID(notice.BinaryBucket, 18);
                    UUID attachmentUUID = new UUID(notice.BinaryBucket, 34);

                    if (m_debugEnabled)
                        m_log.DebugFormat("[Groups]: Giving inventory from {0} to {1}", giver, remoteClient.AgentId);

                    string message;
                    InventoryItemBase itemCopy = ((Scene)(remoteClient.Scene)).GiveInventoryItem(remoteClient.AgentId,
                        giver, attachmentUUID, out message);

                    if (itemCopy == null)
                    {
                        remoteClient.SendAgentAlertMessage(message, false);
                        return;
                    }

                    remoteClient.SendInventoryItemCreateUpdate(itemCopy, 0);
                }
                else
                {
                    if (m_debugEnabled)
                        m_log.DebugFormat(
                            "[GROUPS]: Could not find notice {0} for {1} on GroupNoticeInventoryAccepted.",
                            noticeID, remoteClient.AgentId);
                }
            }

            // Interop, received special 210 code for ejecting a group member
            // this only works within the comms servers domain, and won't work hypergrid
            // TODO:FIXME: Use a presence server of some kind to find out where the
            // client actually is, and try contacting that region directly to notify them,
            // or provide the notification via xmlrpc update queue
            if ((im.dialog == 210))
            {
                // This is sent from the region that the ejectee was ejected from
                // if it's being delivered here, then the ejectee is here
                // so we need to send local updates to the agent.

                UUID ejecteeID = new UUID(im.toAgentID);
                im.imSessionID = UUID.Zero.Guid;
                im.dialog = (byte)InstantMessageDialog.MessageFromAgent;
                OutgoingInstantMessage(im, ejecteeID);

                IClientAPI ejectee = GetActiveRootClient(ejecteeID);
                if (ejectee != null)
                {
                    UUID groupID = new UUID(im.imSessionID);
                    ejectee.SendAgentDropGroup(groupID);
                    SendAgentGroupDataUpdate(ejectee,true);
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
                        IClientAPI localClient = GetActiveRootClient(toAgentID);
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
            return m_groupData.GetGroupRecord(UUID.Zero, GroupID, null);
        }

        public GroupRecord GetGroupRecord(string name)
        {
            return m_groupData.GetGroupRecord(UUID.Zero, UUID.Zero, name);
        }

        public void ActivateGroup(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            UUID agentID = GetRequestingAgentID(remoteClient);
            m_groupData.SetAgentActiveGroup(agentID, agentID, groupID);

            // llClientView does this
            SendAgentGroupDataUpdate(remoteClient, true);
        }

        /// <summary>
        /// Get the Role Titles for an Agent, for a specific group
        /// </summary>
        public List<GroupTitlesData> GroupTitlesRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> agentRoles = m_groupData.GetAgentGroupRoles(GetRequestingAgentID(remoteClient), GetRequestingAgentID(remoteClient), groupID);
            GroupMembershipData agentMembership = m_groupData.GetAgentGroupMembership(GetRequestingAgentID(remoteClient), GetRequestingAgentID(remoteClient), groupID);

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
                    "[GROUPS]: GroupMembersRequest called for {0} from client {1}", groupID, remoteClient.Name);

            List<GroupMembersData> data = m_groupData.GetGroupMembers(GetRequestingAgentID(remoteClient), groupID);

            if (m_debugEnabled)
            {
                foreach (GroupMembersData member in data)
                {
                    m_log.DebugFormat("[GROUPS]: Member({0}) - IsOwner({1})", member.AgentID, member.IsOwner);
                }
            }

            return data;
        }

        public List<GroupRolesData> GroupRoleDataRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> data = m_groupData.GetGroupRoles(GetRequestingAgentID(remoteClient), groupID);

            return data;
        }

        public List<GroupRoleMembersData> GroupRoleMembersRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRoleMembersData> data = m_groupData.GetGroupRoleMembers(GetRequestingAgentID(remoteClient), groupID);

            if (m_debugEnabled)
            {
                foreach (GroupRoleMembersData member in data)
                {
                    m_log.DebugFormat("[GROUPS]: Member({0}) - Role({1})", member.MemberID, member.RoleID);
                }
            }
            return data;
        }

        public GroupProfileData GroupProfileRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupProfileData profile = new GroupProfileData();

            GroupRecord groupInfo = m_groupData.GetGroupRecord(GetRequestingAgentID(remoteClient), groupID, null);
            if (groupInfo != null)
            {
                profile.AllowPublish = groupInfo.AllowPublish;
                profile.Charter = groupInfo.Charter;
                profile.FounderID = groupInfo.FounderID;
                profile.GroupID = groupID;
                profile.GroupMembershipCount = m_groupData.GetGroupMembers(GetRequestingAgentID(remoteClient), groupID).Count;
                profile.GroupRolesCount = m_groupData.GetGroupRoles(GetRequestingAgentID(remoteClient), groupID).Count;
                profile.InsigniaID = groupInfo.GroupPicture;
                profile.MaturePublish = groupInfo.MaturePublish;
                profile.MembershipFee = groupInfo.MembershipFee;
                profile.Money = 0; // TODO: Get this from the currency server?
                profile.Name = groupInfo.GroupName;
                profile.OpenEnrollment = groupInfo.OpenEnrollment;
                profile.OwnerRole = groupInfo.OwnerRoleID;
                profile.ShowInList = groupInfo.ShowInList;
            }

            GroupMembershipData memberInfo = m_groupData.GetAgentGroupMembership(GetRequestingAgentID(remoteClient), GetRequestingAgentID(remoteClient), groupID);
            if (memberInfo != null)
            {
                profile.MemberTitle = memberInfo.GroupTitle;
                profile.PowersMask = memberInfo.GroupPowers;
            }
/*
            this should save xmlrpc calls, but seems to return wrong GroupMembershipCount and GroupRolesCount
            UUID agent = GetRequestingAgentID(remoteClient);
            GroupProfileData profile = m_groupData.GetMemberGroupProfile(agent, groupID, agent);
*/
            return profile;
        }

        public GroupMembershipData[] GetMembershipData(UUID agentID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            return m_groupData.GetAgentGroupMemberships(UUID.Zero, agentID).ToArray();
        }

        public GroupMembershipData GetMembershipData(UUID groupID, UUID agentID)
        {
            if (m_debugEnabled)
                m_log.DebugFormat(
                    "[GROUPS]: {0} called with groupID={1}, agentID={2}",
                    System.Reflection.MethodBase.GetCurrentMethod().Name, groupID, agentID);

            return m_groupData.GetAgentGroupMembership(UUID.Zero, agentID, groupID);
        }

        public GroupMembershipData GetActiveMembershipData(UUID agentID)
        {
            return m_groupData.GetAgentActiveMembership(agentID, agentID);
        }

        public void UpdateGroupInfo(IClientAPI remoteClient, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Note: Permissions checking for modification rights is handled by the Groups Server/Service
            m_groupData.UpdateGroup(GetRequestingAgentID(remoteClient), groupID, charter, showInList, insigniaID, membershipFee, openEnrollment, allowPublish, maturePublish);
        }

        public void SetGroupAcceptNotices(IClientAPI remoteClient, UUID groupID, bool acceptNotices, bool listInProfile)
        {
            // Note: Permissions checking for modification rights is handled by the Groups Server/Service
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.SetAgentGroupInfo(GetRequestingAgentID(remoteClient), GetRequestingAgentID(remoteClient), groupID, acceptNotices, listInProfile);
        }

        public UUID CreateGroup(IClientAPI remoteClient, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupRecord groupRecord = m_groupData.GetGroupRecord(GetRequestingAgentID(remoteClient), UUID.Zero, name);

            if (groupRecord != null)
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
                if (avatar.GodController.UserLevel < m_levelGroupCreate)
                {
                    remoteClient.SendCreateGroupReply(UUID.Zero, false, "You have insufficient permissions to create a group.");
                    return UUID.Zero;
                }
            }

            // check funds
            // is there a money module present ?
            IMoneyModule money = scene.RequestModuleInterface<IMoneyModule>();
            if (money != null && money.GroupCreationCharge > 0)
            {
                // do the transaction, that is if the agent has sufficient funds
                if (!money.AmountCovered(remoteClient.AgentId, money.GroupCreationCharge)) {
                    remoteClient.SendCreateGroupReply(UUID.Zero, false, "You have insufficient funds to create a group.");
                    return UUID.Zero;
                }
                money.ApplyCharge(GetRequestingAgentID(remoteClient), money.GroupCreationCharge, MoneyTransactionType.GroupCreate, name);
            }
            UUID groupID = m_groupData.CreateGroup(GetRequestingAgentID(remoteClient), name, charter, showInList, insigniaID, membershipFee, openEnrollment, allowPublish, maturePublish, GetRequestingAgentID(remoteClient));

            remoteClient.SendCreateGroupReply(groupID, true, "Group created successfully");

            // Update the founder with new group information.
            SendAgentGroupDataUpdate(remoteClient, true);

            return groupID;
        }

        public GroupNoticeData[] GroupNoticesListRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // ToDo: check if agent is a member of group and is allowed to see notices?

            return m_groupData.GetGroupNotices(GetRequestingAgentID(remoteClient), groupID).ToArray();
        }

        /// <summary>
        /// Get the title of the agent's current role.
        /// </summary>
        public string GetGroupTitle(UUID avatarID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(UUID.Zero, avatarID);
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

            m_groupData.SetAgentActiveGroupRole(GetRequestingAgentID(remoteClient), GetRequestingAgentID(remoteClient), groupID, titleRoleID);

            // TODO: Not sure what all is needed here, but if the active group role change is for the group
            // the client currently has set active, then we need to do a scene presence update too
            // if (m_groupData.GetAgentActiveMembership(GetRequestingAgentID(remoteClient)).GroupID == GroupID)

            SendDataUpdate(remoteClient, true);
        }

        public void GroupRoleUpdate(IClientAPI remoteClient, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, byte updateType)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Security Checks are handled in the Groups Service.

            switch ((OpenMetaverse.GroupRoleUpdate)updateType)
            {
                case OpenMetaverse.GroupRoleUpdate.Create:
                    m_groupData.AddGroupRole(GetRequestingAgentID(remoteClient), groupID, UUID.Random(), name, description, title, powers);
                    break;

                case OpenMetaverse.GroupRoleUpdate.Delete:
                    m_groupData.RemoveGroupRole(GetRequestingAgentID(remoteClient), groupID, roleID);
                    break;

                case OpenMetaverse.GroupRoleUpdate.UpdateAll:
                case OpenMetaverse.GroupRoleUpdate.UpdateData:
                case OpenMetaverse.GroupRoleUpdate.UpdatePowers:
                    if (m_debugEnabled)
                    {
                        GroupPowers gp = (GroupPowers)powers;
                        m_log.DebugFormat("[GROUPS]: Role ({0}) updated with Powers ({1}) ({2})", name, powers.ToString(), gp.ToString());
                    }
                    m_groupData.UpdateGroupRole(GetRequestingAgentID(remoteClient), groupID, roleID, name, description, title, powers);
                    break;

                case OpenMetaverse.GroupRoleUpdate.NoUpdate:
                default:
                    // No Op
                    break;

            }

            // TODO: This update really should send out updates for everyone in the role that just got changed.
            SendAgentGroupDataUpdate(remoteClient, false);
        }

        public void GroupRoleChanges(IClientAPI remoteClient, UUID groupID, UUID roleID, UUID memberID, uint changes)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            // Todo: Security check

            switch (changes)
            {
                case 0:
                    // Add
                    m_groupData.AddAgentToGroupRole(GetRequestingAgentID(remoteClient), memberID, groupID, roleID);

                    break;
                case 1:
                    // Remove
                    m_groupData.RemoveAgentFromGroupRole(GetRequestingAgentID(remoteClient), memberID, groupID, roleID);

                    break;
                default:
                    m_log.ErrorFormat("[GROUPS]: {0} does not understand changes == {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, changes);
                    break;
            }

            // TODO: This update really should send out updates for everyone in the role that just got changed.
            SendAgentGroupDataUpdate(remoteClient, false);
        }

        public void GroupNoticeRequest(IClientAPI remoteClient, UUID groupNoticeID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupNoticeInfo data = m_groupData.GetGroupNotice(GetRequestingAgentID(remoteClient), groupNoticeID);

            if (data != null)
            {
                GridInstantMessage msg = CreateGroupNoticeIM(remoteClient.AgentId, groupNoticeID, (byte)InstantMessageDialog.GroupNoticeRequested);

                OutgoingInstantMessage(msg, GetRequestingAgentID(remoteClient));
            }
        }

        public GridInstantMessage CreateGroupNoticeIM(UUID agentID, UUID groupNoticeID, byte dialog)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GridInstantMessage msg = new GridInstantMessage();
            byte[] bucket;

            msg.imSessionID = groupNoticeID.Guid;
            msg.toAgentID = agentID.Guid;
            msg.dialog = dialog;
            msg.fromGroup = true;
            msg.offline = (byte)1; // Allow this message to be stored for offline use
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = UUID.Zero.Guid;

            GroupNoticeInfo info = m_groupData.GetGroupNotice(agentID, groupNoticeID);
            if (info != null)
            {
                msg.fromAgentID = info.GroupID.Guid;
                msg.timestamp = info.noticeData.Timestamp;
                msg.fromAgentName = info.noticeData.FromName;
                msg.message = info.noticeData.Subject + "|" + info.Message;

                if (info.BinaryBucket[0] > 0)
                {
                    //32 is due to not needing space for two of the UUIDs.
                    //(Don't need UUID of attachment or its owner in IM)
                    //50 offset gets us to start of attachment name.
                    //We are skipping the attachment flag, type, and
                    //the three UUID fields at the start of the bucket.
                    bucket = new byte[info.BinaryBucket.Length-32];
                    bucket[0] = 1;      //Has attachment
                    bucket[1] = info.BinaryBucket[1];
                    Array.Copy(info.BinaryBucket, 50,
                               bucket, 18, info.BinaryBucket.Length-50);
                }
                else
                {
                    bucket = new byte[19];
                    bucket[0] = 0;      //No attachment
                    bucket[1] = 0;      //Attachment type
                    bucket[18] = 0;     //NUL terminate name
                }

                info.GroupID.ToBytes(bucket, 2);
                msg.binaryBucket = bucket;
            }
            else
            {
                if (m_debugEnabled)
                    m_log.DebugFormat("[GROUPS]: Group Notice {0} not found, composing empty message.", groupNoticeID);

                msg.fromAgentID = UUID.Zero.Guid;
                msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
                msg.fromAgentName = string.Empty;
                msg.message = string.Empty;
                msg.binaryBucket = new byte[0];
            }

            return msg;
        }

        public void JoinGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Should check to see if OpenEnrollment, or if there's an outstanding invitation
            m_groupData.AddAgentToGroup(GetRequestingAgentID(remoteClient), GetRequestingAgentID(remoteClient), groupID, UUID.Zero);

            // check funds
            // is there a money module present ?
            GroupRecord groupRecord = m_groupData.GetGroupRecord(GetRequestingAgentID(remoteClient), groupID, null);
            IMoneyModule money = remoteClient.Scene.RequestModuleInterface<IMoneyModule>();
            if (money != null && groupRecord.MembershipFee > 0)
            {
                // do the transaction, that is if the agent has sufficient funds
                if (!money.AmountCovered(GetRequestingAgentID(remoteClient), groupRecord.MembershipFee)) {
                    remoteClient.SendCreateGroupReply(UUID.Zero, false, "You have insufficient funds to join the group.");
                    return;
                }
                money.ApplyCharge(GetRequestingAgentID(remoteClient), groupRecord.MembershipFee, MoneyTransactionType.GroupJoin, groupRecord.GroupName);
            }

            remoteClient.SendJoinGroupReply(groupID, true);

            SendAgentGroupDataUpdate(remoteClient, true);
        }

        public void LeaveGroupRequest(IClientAPI remoteClient, UUID groupID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_groupData.RemoveAgentFromGroup(GetRequestingAgentID(remoteClient), GetRequestingAgentID(remoteClient), groupID);

            remoteClient.SendLeaveGroupReply(groupID, true);

            remoteClient.SendAgentDropGroup(groupID);

            // SL sends out notifcations to the group messaging session that the person has left
            // Should this also update everyone who is in the group?
            SendAgentGroupDataUpdate(remoteClient, true);
        }

        public void EjectGroupMemberRequest(IClientAPI remoteClient, UUID groupID, UUID ejecteeID)
        {
            EjectGroupMember(remoteClient, GetRequestingAgentID(remoteClient), groupID, ejecteeID);
        }

        public void EjectGroupMember(IClientAPI remoteClient, UUID agentID, UUID groupID, UUID ejecteeID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Todo: Security check?
            m_groupData.RemoveAgentFromGroup(agentID, ejecteeID, groupID);

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

            GroupRecord groupInfo = m_groupData.GetGroupRecord(agentID, groupID, null);
            if (groupInfo == null)
                return;


            IClientAPI ejecteeClient = GetActiveRootClient(ejecteeID);

            // Send Message to Ejectee
            GridInstantMessage msg = new GridInstantMessage();

            string ejecteeName = "Unknown member";
            // if local send a normal message
            if(ejecteeClient != null)
            {
                msg.imSessionID = UUID.Zero.Guid;
                msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.MessageFromAgent;
                // also execute and send update
                ejecteeClient.SendAgentDropGroup(groupID);
                SendAgentGroupDataUpdate(ejecteeClient,true);
                ejecteeName = ejecteeClient.Name;
            }
            else // send
            {
                // Interop, received special 210 code for ejecting a group member
                // this only works within the comms servers domain, and won't work hypergrid
                // TODO:FIXME: Use a presence server of some kind to find out where the
                // client actually is, and try contacting that region directly to notify them,
                // or provide the notification via xmlrpc update queue

                msg.imSessionID = groupInfo.GroupID.Guid;
                msg.dialog = (byte)210; //interop
                UserAccount account = m_sceneList[0].UserAccountService.GetUserAccount(regionInfo.ScopeID, ejecteeID);
                if (account != null)
                    ejecteeName = account.FirstName + " " + account.LastName;
            }

            msg.fromAgentID = agentID.Guid;
            // msg.fromAgentID = info.GroupID;
            msg.toAgentID = ejecteeID.Guid;
            //msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
            msg.timestamp = 0;
            msg.fromAgentName = agentName;
            msg.message = string.Format("You have been ejected from '{1}' by {0}.", agentName, groupInfo.GroupName);
//
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = regionInfo.RegionID.Guid;
            msg.binaryBucket = new byte[0];
            OutgoingInstantMessage(msg, ejecteeID);

            // Message to ejector

            msg = new GridInstantMessage();
            msg.imSessionID = UUID.Zero.Guid;
            msg.fromAgentID = agentID.Guid;
            msg.toAgentID = agentID.Guid;
            msg.timestamp = 0;
            msg.fromAgentName = agentName;

            msg.message = string.Format("{2} has been ejected from '{1}' by {0}.", agentName, groupInfo.GroupName, ejecteeName);

//            msg.dialog = (byte)210; //interop
            msg.dialog = (byte)OpenMetaverse.InstantMessageDialog.MessageFromAgent;
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = regionInfo.RegionID.Guid;
            msg.binaryBucket = new byte[0];
            OutgoingInstantMessage(msg, agentID);
        }

        public void InviteGroupRequest(IClientAPI remoteClient, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            InviteGroup(remoteClient, GetRequestingAgentID(remoteClient), groupID, invitedAgentID, roleID);
        }

        public void InviteGroup(IClientAPI remoteClient, UUID agentID, UUID groupID, UUID invitedAgentID, UUID roleID)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            string agentName;
            RegionInfo regionInfo;

            // remoteClient provided or just agentID?
            if (remoteClient != null)
            {
                agentName = remoteClient.Name;
                regionInfo = remoteClient.Scene.RegionInfo;
            }
            else
            {
                IClientAPI client = GetActiveClient(agentID);

                if (client != null)
                {
                    agentName = client.Name;
                    regionInfo = client.Scene.RegionInfo;
                }
                else
                {
                    regionInfo = m_sceneList[0].RegionInfo;
                    UserAccount account = m_sceneList[0].UserAccountService.GetUserAccount(regionInfo.ScopeID, agentID);

                    if (account != null)
                    {
                        agentName = account.FirstName + " " + account.LastName;
                    }
                    else
                    {
                        agentName = "Unknown member";
                    }
                }
            }

            // Todo: Security check, probably also want to send some kind of notification
            UUID InviteID = UUID.Random();

            m_groupData.AddAgentToGroupInvite(agentID, InviteID, groupID, roleID, invitedAgentID);

            // Check to see if the invite went through, if it did not then it's possible
            // the remoteClient did not validate or did not have permission to invite.
            GroupInviteInfo inviteInfo = m_groupData.GetAgentToGroupInvite(agentID, InviteID);

            if (inviteInfo != null)
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
                    msg.message = string.Format("{0} has invited you to join a group. There is no cost to join this group.", agentName);
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
            return m_groupData.FindGroups(GetRequestingAgentID(remoteClient), query);
        }


        #endregion

        #region Client/Update Tools

        private IClientAPI GetActiveRootClient(UUID agentID)
        {
            foreach (Scene scene in m_sceneList)
            {
                ScenePresence sp = scene.GetScenePresence(agentID);
                if (sp != null && !sp.IsChildAgent && !sp.IsDeleted)
                {
                        return sp.ControllingClient;
                }
            }
            return null;
         }

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
                if (sp != null && !sp.IsDeleted)
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

        private void SendScenePresenceUpdate(UUID AgentID, string Title)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: Updating scene title for {0} with title: {1}", AgentID, Title);

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

        public void SendAgentGroupDataUpdate(IClientAPI remoteClient)
        {
            SendAgentGroupDataUpdate(remoteClient, true);
        }

        /// <summary>
        /// Tell remoteClient about its agent groups, and optionally send title to others
        /// </summary>
        private void SendAgentGroupDataUpdate(IClientAPI remoteClient, bool tellOthers)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: {0} called for {1}", System.Reflection.MethodBase.GetCurrentMethod().Name, remoteClient.Name);

            // NPCs currently don't have a CAPs structure or event queues.  There is a strong argument for conveying this information
            // to them anyway since it makes writing server-side bots a lot easier, but for now we don't do anything.
            if (remoteClient.SceneAgent.PresenceType == PresenceType.Npc)
                return;

            // TODO: All the client update functions need to be reexamined because most do too much and send too much stuff

            UUID agentID = GetRequestingAgentID(remoteClient);

            SendDataUpdate(remoteClient,  tellOthers);

            GroupMembershipData[] membershipArray = GetProfileListedGroupMemberships(remoteClient, agentID);
            remoteClient.UpdateGroupMembership(membershipArray);

            remoteClient.SendAgentGroupDataUpdate(agentID, membershipArray);
         }

        /// <summary>
        /// Get a list of groups memberships for the agent that are marked "ListInProfile"
        /// (unless that agent has a godLike aspect, in which case get all groups)
        /// </summary>
        /// <param name="dataForAgentID"></param>
        /// <returns></returns>
        private GroupMembershipData[] GetProfileListedGroupMemberships(IClientAPI requestingClient, UUID dataForAgentID)
        {
            List<GroupMembershipData> membershipData = m_groupData.GetAgentGroupMemberships(requestingClient.AgentId, dataForAgentID);
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
                m_log.InfoFormat("[GROUPS]: Get group membership information for {0} requested by {1}", dataForAgentID, requestingClient.AgentId);
                foreach (GroupMembershipData membership in membershipArray)
                {
                    m_log.InfoFormat("[GROUPS]: {0} :: {1} - {2} - {3}", dataForAgentID, membership.GroupName, membership.GroupTitle, membership.GroupPowers);
                }
            }

            return membershipArray;
        }

        //tell remoteClient about its agent group info, and optionally send title to others
        private void SendDataUpdate(IClientAPI remoteClient, bool tellOthers)
        {
            if (m_debugEnabled) m_log.DebugFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            UUID activeGroupID = UUID.Zero;
            string activeGroupTitle = string.Empty;
            string activeGroupName = string.Empty;
            ulong activeGroupPowers = (ulong)GroupPowers.None;

            UUID agentID = GetRequestingAgentID(remoteClient);
            GroupMembershipData membership = m_groupData.GetAgentActiveMembership(agentID, agentID);
            if (membership != null)
            {
                activeGroupID = membership.GroupID;
                activeGroupTitle = membership.GroupTitle;
                activeGroupPowers = membership.GroupPowers;
                activeGroupName = membership.GroupName;
            }

            UserAccount account = m_sceneList[0].UserAccountService.GetUserAccount(remoteClient.Scene.RegionInfo.ScopeID, agentID);
            string firstname, lastname;
            if (account != null)
            {
                firstname = account.FirstName;
                lastname = account.LastName;
            }
            else
            {
                firstname = "Unknown";
                lastname = "Unknown";
            }

            remoteClient.SendAgentDataUpdate(agentID, activeGroupID, firstname,
                    lastname, activeGroupPowers, activeGroupName,
                    activeGroupTitle);


            if (tellOthers)
                SendScenePresenceUpdate(agentID, activeGroupTitle);

            ScenePresence sp = (ScenePresence)remoteClient.SceneAgent;
            if (sp != null)
                sp.Grouptitle = activeGroupTitle;
        }

        #endregion

        #region IM Backed Processes

        private void OutgoingInstantMessage(GridInstantMessage msg, UUID msgTo)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            IClientAPI localClient = GetActiveRootClient(msgTo);
            if (localClient != null)
            {
                if (m_debugEnabled) m_log.InfoFormat("[GROUPS]: MsgTo ({0}) is local, delivering directly", localClient.Name);
                localClient.SendInstantMessage(msg);
            }
            else if (m_msgTransferModule != null)
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

    public class GroupNoticeInfo
    {
        public GroupNoticeData noticeData = new GroupNoticeData();
        public UUID GroupID = UUID.Zero;
        public string Message = string.Empty;
        public byte[] BinaryBucket = new byte[0];
    }
}
