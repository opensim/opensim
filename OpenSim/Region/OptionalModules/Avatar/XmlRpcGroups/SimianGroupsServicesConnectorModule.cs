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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Threading;

using Nwc.XmlRpc;

using log4net;
using Mono.Addins;
using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;

/***************************************************************************
 * Simian Data Map
 * ===============
 *
 * OwnerID -> Type -> Key
 * -----------------------
 *
 * UserID -> Group -> ActiveGroup
 * + GroupID
 *
 * UserID -> GroupSessionDropped -> GroupID
 * UserID -> GroupSessionInvited -> GroupID
 *
 * UserID -> GroupMember -> GroupID
 * + SelectedRoleID [UUID]
 * + AcceptNotices  [bool]
 * + ListInProfile  [bool]
 * + Contribution   [int]
 *
 * UserID -> GroupRole[GroupID] -> RoleID
 *
 *
 * GroupID -> Group -> GroupName
 * + Charter
 * + ShowInList
 * + InsigniaID
 * + MembershipFee
 * + OpenEnrollment
 * + AllowPublish
 * + MaturePublish
 * + FounderID
 * + EveryonePowers
 * + OwnerRoleID
 * + OwnersPowers
 *
 * GroupID -> GroupRole -> RoleID
 * + Name
 * + Description
 * + Title
 * + Powers
 *
 * GroupID -> GroupMemberInvite -> InviteID
 * + AgentID
 * + RoleID
 *
 * GroupID -> GroupNotice -> NoticeID
 * + TimeStamp      [uint]
 * + FromName       [string]
 * + Subject        [string]
 * + Message        [string]
 * + BinaryBucket   [byte[]]
 *
 * */

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianGroupsServicesConnectorModule")]
    public class SimianGroupsServicesConnectorModule : ISharedRegionModule, IGroupsServicesConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const GroupPowers m_DefaultEveryonePowers = GroupPowers.AllowSetHome |
            GroupPowers.Accountable |
            GroupPowers.JoinChat |
            GroupPowers.AllowVoiceChat |
            GroupPowers.ReceiveNotices |
            GroupPowers.StartProposal |
            GroupPowers.VoteOnProposal;

        // Would this be cleaner as (GroupPowers)ulong.MaxValue;
        public const GroupPowers m_DefaultOwnerPowers = GroupPowers.Accountable
                                | GroupPowers.AllowEditLand
                                | GroupPowers.AllowFly
                                | GroupPowers.AllowLandmark
                                | GroupPowers.AllowRez
                                | GroupPowers.AllowSetHome
                                | GroupPowers.AllowVoiceChat
                                | GroupPowers.AssignMember
                                | GroupPowers.AssignMemberLimited
                                | GroupPowers.ChangeActions
                                | GroupPowers.ChangeIdentity
                                | GroupPowers.ChangeMedia
                                | GroupPowers.ChangeOptions
                                | GroupPowers.CreateRole
                                | GroupPowers.DeedObject
                                | GroupPowers.DeleteRole
                                | GroupPowers.Eject
                                | GroupPowers.FindPlaces
                                | GroupPowers.Invite
                                | GroupPowers.JoinChat
                                | GroupPowers.LandChangeIdentity
                                | GroupPowers.LandDeed
                                | GroupPowers.LandDivideJoin
                                | GroupPowers.LandEdit
                                | GroupPowers.LandEjectAndFreeze
                                | GroupPowers.LandGardening
                                | GroupPowers.LandManageAllowed
                                | GroupPowers.LandManageBanned
                                | GroupPowers.LandManagePasses
                                | GroupPowers.LandOptions
                                | GroupPowers.LandRelease
                                | GroupPowers.LandSetSale
                                | GroupPowers.ModerateChat
                                | GroupPowers.ObjectManipulate
                                | GroupPowers.ObjectSetForSale
                                | GroupPowers.ReceiveNotices
                                | GroupPowers.RemoveMember
                                | GroupPowers.ReturnGroupOwned
                                | GroupPowers.ReturnGroupSet
                                | GroupPowers.ReturnNonGroup
                                | GroupPowers.RoleProperties
                                | GroupPowers.SendNotices
                                | GroupPowers.SetLandingPoint
                                | GroupPowers.StartProposal
                                | GroupPowers.VoteOnProposal;

        private bool m_connectorEnabled = false;

        private string m_groupsServerURI = string.Empty;

        private bool m_debugEnabled = false;

        private Dictionary<string, bool> m_pendingRequests = new Dictionary<string,bool>();

        private ExpiringCache<string, OSDMap> m_memoryCache;
        private int m_cacheTimeout = 30;

        // private IUserAccountService m_accountService = null;


        #region Region Module interfaceBase Members

        public string Name
        {
            get { return "SimianGroupsServicesConnector"; }
        }

        // this module is not intended to be replaced, but there should only be 1 of them.
        public Type ReplaceableInterface
        {
            get { return null; }
        }

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
                    || (groupsConfig.GetString("ServicesConnectorModule", "XmlRpcGroupsServicesConnector") != Name))
                {
                    m_connectorEnabled = false;
                    return;
                }

                m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]: Initializing {0}", this.Name);

                m_groupsServerURI = groupsConfig.GetString("GroupsServerURI", string.Empty);
                if (string.IsNullOrEmpty(m_groupsServerURI))
                {
                    m_log.ErrorFormat("Please specify a valid Simian Server for GroupsServerURI in OpenSim.ini, [Groups]");
                    m_connectorEnabled = false;
                    return;
                }


                m_cacheTimeout = groupsConfig.GetInt("GroupsCacheTimeout", 30);
                if (m_cacheTimeout == 0)
                {
                    m_log.WarnFormat("[SIMIAN-GROUPS-CONNECTOR] Groups Cache Disabled.");
                }
                else
                {
                    m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR] Groups Cache Timeout set to {0}.", m_cacheTimeout);
                }



                m_memoryCache = new ExpiringCache<string,OSDMap>();


                // If we got all the config options we need, lets start'er'up
                m_connectorEnabled = true;

                m_debugEnabled = groupsConfig.GetBoolean("DebugEnabled", true);

            }
        }

        public void Close()
        {
        }

        public void AddRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            if (m_connectorEnabled)
            {
                scene.RegisterModuleInterface<IGroupsServicesConnector>(this);
            }
        }

        public void RemoveRegion(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            if (scene.RequestModuleInterface<IGroupsServicesConnector>() == this)
            {
                scene.UnregisterModuleInterface<IGroupsServicesConnector>(this);
            }
        }

        public void RegionLoaded(OpenSim.Region.Framework.Scenes.Scene scene)
        {
            // TODO: May want to consider listenning for Agent Connections so we can pre-cache group info
            // scene.EventManager.OnNewClient += OnNewClient;
        }

        #endregion

        #region ISharedRegionModule Members

        public void PostInitialise()
        {
            // NoOp
        }

        #endregion




        #region IGroupsServicesConnector Members

        /// <summary>
        /// Create a Group, including Everyone and Owners Role, place FounderID in both groups, select Owner as selected role, and newly created group as agent's active role.
        /// </summary>
        public UUID CreateGroup(UUID requestingAgentID, string name, string charter, bool showInList, UUID insigniaID,
                                int membershipFee, bool openEnrollment, bool allowPublish,
                                bool maturePublish, UUID founderID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            UUID GroupID     = UUID.Random();
            UUID OwnerRoleID = UUID.Random();

            OSDMap GroupInfoMap = new OSDMap();
            GroupInfoMap["Charter"] = OSD.FromString(charter);
            GroupInfoMap["ShowInList"] = OSD.FromBoolean(showInList);
            GroupInfoMap["InsigniaID"] = OSD.FromUUID(insigniaID);
            GroupInfoMap["MembershipFee"] = OSD.FromInteger(0);
            GroupInfoMap["OpenEnrollment"] = OSD.FromBoolean(openEnrollment);
            GroupInfoMap["AllowPublish"] = OSD.FromBoolean(allowPublish);
            GroupInfoMap["MaturePublish"] = OSD.FromBoolean(maturePublish);
            GroupInfoMap["FounderID"] = OSD.FromUUID(founderID);
            GroupInfoMap["EveryonePowers"] = OSD.FromULong((ulong)m_DefaultEveryonePowers);
            GroupInfoMap["OwnerRoleID"] = OSD.FromUUID(OwnerRoleID);
            GroupInfoMap["OwnersPowers"] = OSD.FromULong((ulong)m_DefaultOwnerPowers);

            if (SimianAddGeneric(GroupID, "Group", name, GroupInfoMap))
            {
                AddGroupRole(requestingAgentID, GroupID, UUID.Zero, "Everyone", "Members of " + name, "Member of " + name, (ulong)m_DefaultEveryonePowers);
                AddGroupRole(requestingAgentID, GroupID, OwnerRoleID, "Owners", "Owners of " + name, "Owner of " + name, (ulong)m_DefaultOwnerPowers);

                AddAgentToGroup(requestingAgentID, requestingAgentID, GroupID, OwnerRoleID);

                return GroupID;
            }
            else
            {
                return UUID.Zero;
            }
        }


        public void UpdateGroup(UUID requestingAgentID, UUID groupID, string charter, bool showInList,
                                UUID insigniaID, int membershipFee, bool openEnrollment,
                                bool allowPublish, bool maturePublish)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);
            // TODO: Check to make sure requestingAgentID has permission to update group

            string GroupName;
            OSDMap GroupInfoMap;
            if (SimianGetFirstGenericEntry(groupID, "GroupInfo", out GroupName, out GroupInfoMap))
            {
                GroupInfoMap["Charter"] = OSD.FromString(charter);
                GroupInfoMap["ShowInList"] = OSD.FromBoolean(showInList);
                GroupInfoMap["InsigniaID"] = OSD.FromUUID(insigniaID);
                GroupInfoMap["MembershipFee"] = OSD.FromInteger(0);
                GroupInfoMap["OpenEnrollment"] = OSD.FromBoolean(openEnrollment);
                GroupInfoMap["AllowPublish"] = OSD.FromBoolean(allowPublish);
                GroupInfoMap["MaturePublish"] = OSD.FromBoolean(maturePublish);

                SimianAddGeneric(groupID, "Group", GroupName, GroupInfoMap);
            }

        }


        public void AddGroupRole(UUID requestingAgentID, UUID groupID, UUID roleID, string name, string description,
                                 string title, ulong powers)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap GroupRoleInfo = new OSDMap();
            GroupRoleInfo["Name"] = OSD.FromString(name);
            GroupRoleInfo["Description"] = OSD.FromString(description);
            GroupRoleInfo["Title"] = OSD.FromString(title);
            GroupRoleInfo["Powers"] = OSD.FromULong((ulong)powers);

            // TODO: Add security, make sure that requestingAgentID has permision to add roles
            SimianAddGeneric(groupID, "GroupRole", roleID.ToString(), GroupRoleInfo);
        }

        public void RemoveGroupRole(UUID requestingAgentID, UUID groupID, UUID roleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: Add security

            // Can't delete the Everyone Role
            if (roleID != UUID.Zero)
            {
                // Remove all GroupRole Members from Role
                Dictionary<UUID, OSDMap> GroupRoleMembers;
                string GroupRoleMemberType = "GroupRole" + groupID.ToString();
                if (SimianGetGenericEntries(GroupRoleMemberType, roleID.ToString(), out GroupRoleMembers))
                {
                    foreach (UUID UserID in GroupRoleMembers.Keys)
                    {
                        EnsureRoleNotSelectedByMember(groupID, roleID, UserID);

                        SimianRemoveGenericEntry(UserID, GroupRoleMemberType, roleID.ToString());
                    }
                }

                // Remove role
                SimianRemoveGenericEntry(groupID, "GroupRole", roleID.ToString());
            }
        }


        public void UpdateGroupRole(UUID requestingAgentID, UUID groupID, UUID roleID, string name, string description,
                                    string title, ulong powers)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // TODO: Security, check that requestingAgentID is allowed to update group roles

            OSDMap GroupRoleInfo;
            if (SimianGetGenericEntry(groupID, "GroupRole", roleID.ToString(), out GroupRoleInfo))
            {
                if (name != null)
                {
                    GroupRoleInfo["Name"] = OSD.FromString(name);
                }
                if (description != null)
                {
                    GroupRoleInfo["Description"] = OSD.FromString(description);
                }
                if (title != null)
                {
                    GroupRoleInfo["Title"] = OSD.FromString(title);
                }
                GroupRoleInfo["Powers"] = OSD.FromULong((ulong)powers);

            }


            SimianAddGeneric(groupID, "GroupRole", roleID.ToString(), GroupRoleInfo);
        }

        public GroupRecord GetGroupRecord(UUID requestingAgentID, UUID groupID, string groupName)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap GroupInfoMap = null;
            if (groupID != UUID.Zero)
            {
                if (!SimianGetFirstGenericEntry(groupID, "Group", out groupName, out GroupInfoMap))
                {
                    return null;
                }
            }
            else if (!string.IsNullOrEmpty(groupName))
            {
                if (!SimianGetFirstGenericEntry("Group", groupName, out groupID, out GroupInfoMap))
                {
                    return null;
                }
            }

            GroupRecord GroupInfo = new GroupRecord();

            GroupInfo.GroupID = groupID;
            GroupInfo.GroupName = groupName;
            GroupInfo.Charter = GroupInfoMap["Charter"].AsString();
            GroupInfo.ShowInList = GroupInfoMap["ShowInList"].AsBoolean();
            GroupInfo.GroupPicture = GroupInfoMap["InsigniaID"].AsUUID();
            GroupInfo.MembershipFee = GroupInfoMap["MembershipFee"].AsInteger();
            GroupInfo.OpenEnrollment = GroupInfoMap["OpenEnrollment"].AsBoolean();
            GroupInfo.AllowPublish = GroupInfoMap["AllowPublish"].AsBoolean();
            GroupInfo.MaturePublish = GroupInfoMap["MaturePublish"].AsBoolean();
            GroupInfo.FounderID = GroupInfoMap["FounderID"].AsUUID();
            GroupInfo.OwnerRoleID = GroupInfoMap["OwnerRoleID"].AsUUID();

            return GroupInfo;

        }

        public GroupProfileData GetMemberGroupProfile(UUID requestingAgentID, UUID groupID, UUID memberID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap groupProfile;
            string groupName;
            if (!SimianGetFirstGenericEntry(groupID, "Group", out groupName, out groupProfile))
            {
                // GroupProfileData is not nullable
                return new GroupProfileData();
            }

            GroupProfileData MemberGroupProfile = new GroupProfileData();
            MemberGroupProfile.GroupID = groupID;
            MemberGroupProfile.Name = groupName;

            if (groupProfile["Charter"] != null)
            {
                MemberGroupProfile.Charter = groupProfile["Charter"].AsString();
            }

            MemberGroupProfile.ShowInList = groupProfile["ShowInList"].AsString() == "1";
            MemberGroupProfile.InsigniaID = groupProfile["InsigniaID"].AsUUID();
            MemberGroupProfile.MembershipFee = groupProfile["MembershipFee"].AsInteger();
            MemberGroupProfile.OpenEnrollment = groupProfile["OpenEnrollment"].AsBoolean();
            MemberGroupProfile.AllowPublish = groupProfile["AllowPublish"].AsBoolean();
            MemberGroupProfile.MaturePublish = groupProfile["MaturePublish"].AsBoolean();
            MemberGroupProfile.FounderID = groupProfile["FounderID"].AsUUID();;
            MemberGroupProfile.OwnerRole = groupProfile["OwnerRoleID"].AsUUID();

            Dictionary<UUID, OSDMap> Members;
            if (SimianGetGenericEntries("GroupMember",groupID.ToString(), out Members))
            {
                MemberGroupProfile.GroupMembershipCount = Members.Count;
            }

            Dictionary<string, OSDMap> Roles;
            if (SimianGetGenericEntries(groupID, "GroupRole", out Roles))
            {
                MemberGroupProfile.GroupRolesCount = Roles.Count;
            }

            // TODO: Get Group Money balance from somewhere
            // group.Money = 0;

            GroupMembershipData MemberInfo = GetAgentGroupMembership(requestingAgentID, memberID, groupID);

            MemberGroupProfile.MemberTitle = MemberInfo.GroupTitle;
            MemberGroupProfile.PowersMask = MemberInfo.GroupPowers;

            return MemberGroupProfile;
        }

        public void SetAgentActiveGroup(UUID requestingAgentID, UUID agentID, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap ActiveGroup = new OSDMap();
            ActiveGroup.Add("GroupID", OSD.FromUUID(groupID));
            SimianAddGeneric(agentID, "Group", "ActiveGroup", ActiveGroup);
        }

        public void SetAgentActiveGroupRole(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap GroupMemberInfo;
            if (!SimianGetGenericEntry(agentID, "GroupMember", groupID.ToString(), out GroupMemberInfo))
            {
                GroupMemberInfo = new OSDMap();
            }

            GroupMemberInfo["SelectedRoleID"] = OSD.FromUUID(roleID);
            SimianAddGeneric(agentID, "GroupMember", groupID.ToString(), GroupMemberInfo);
        }

        public void SetAgentGroupInfo(UUID requestingAgentID, UUID agentID, UUID groupID, bool acceptNotices, bool listInProfile)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap GroupMemberInfo;
            if (!SimianGetGenericEntry(agentID, "GroupMember", groupID.ToString(), out GroupMemberInfo))
            {
                GroupMemberInfo = new OSDMap();
            }

            GroupMemberInfo["AcceptNotices"] = OSD.FromBoolean(acceptNotices);
            GroupMemberInfo["ListInProfile"] = OSD.FromBoolean(listInProfile);
            GroupMemberInfo["Contribution"] = OSD.FromInteger(0);
            GroupMemberInfo["SelectedRole"] = OSD.FromUUID(UUID.Zero);
            SimianAddGeneric(agentID, "GroupMember", groupID.ToString(), GroupMemberInfo);
        }

        public void AddAgentToGroupInvite(UUID requestingAgentID, UUID inviteID, UUID groupID, UUID roleID, UUID agentID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap Invite = new OSDMap();
            Invite["AgentID"] = OSD.FromUUID(agentID);
            Invite["RoleID"] = OSD.FromUUID(roleID);

            SimianAddGeneric(groupID, "GroupMemberInvite", inviteID.ToString(), Invite);
        }

        public GroupInviteInfo GetAgentToGroupInvite(UUID requestingAgentID, UUID inviteID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap GroupMemberInvite;
            UUID GroupID;
            if (!SimianGetFirstGenericEntry("GroupMemberInvite", inviteID.ToString(), out GroupID, out GroupMemberInvite))
            {
                return null;
            }

            GroupInviteInfo inviteInfo = new GroupInviteInfo();
            inviteInfo.InviteID = inviteID;
            inviteInfo.GroupID = GroupID;
            inviteInfo.AgentID = GroupMemberInvite["AgentID"].AsUUID();
            inviteInfo.RoleID = GroupMemberInvite["RoleID"].AsUUID();

            return inviteInfo;
        }

        public void RemoveAgentToGroupInvite(UUID requestingAgentID, UUID inviteID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupInviteInfo invite = GetAgentToGroupInvite(requestingAgentID, inviteID);
            SimianRemoveGenericEntry(invite.GroupID, "GroupMemberInvite", inviteID.ToString());
        }

        public void AddAgentToGroup(UUID requestingAgentID, UUID AgentID, UUID GroupID, UUID RoleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Setup Agent/Group information
            SetAgentGroupInfo(requestingAgentID, AgentID, GroupID, true, true);

            // Add agent to Everyone Group
            AddAgentToGroupRole(requestingAgentID, AgentID, GroupID, UUID.Zero);

            // Add agent to Specified Role
            AddAgentToGroupRole(requestingAgentID, AgentID, GroupID, RoleID);

            // Set selected role in this group to specified role
            SetAgentActiveGroupRole(requestingAgentID, AgentID, GroupID, RoleID);
        }

        public void RemoveAgentFromGroup(UUID requestingAgentID, UUID agentID, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // If current active group is the group the agent is being removed from, change their group to UUID.Zero
            GroupMembershipData memberActiveMembership = GetAgentActiveMembership(requestingAgentID, agentID);
            if (memberActiveMembership.GroupID == groupID)
            {
                SetAgentActiveGroup(agentID, agentID, UUID.Zero);
            }

            // Remove Group Member information for this group
            SimianRemoveGenericEntry(agentID, "GroupMember", groupID.ToString());

            // By using a Simian Generics Type consisting of a prefix and a groupID,
            // combined with RoleID as key allows us to get a list of roles a particular member
            // of a group is assigned to.
            string GroupRoleMemberType = "GroupRole" + groupID.ToString();

            // Take Agent out of all other group roles
            Dictionary<string, OSDMap> GroupRoles;
            if (SimianGetGenericEntries(agentID, GroupRoleMemberType, out GroupRoles))
            {
                foreach (string roleID in GroupRoles.Keys)
                {
                    SimianRemoveGenericEntry(agentID, GroupRoleMemberType, roleID);
                }
            }
        }

        public void AddAgentToGroupRole(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            SimianAddGeneric(agentID, "GroupRole" + groupID.ToString(), roleID.ToString(), new OSDMap());
        }

        public void RemoveAgentFromGroupRole(UUID requestingAgentID, UUID agentID, UUID groupID, UUID roleID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // Cannot remove members from the Everyone Role
            if (roleID != UUID.Zero)
            {
                EnsureRoleNotSelectedByMember(groupID, roleID, agentID);

                string GroupRoleMemberType = "GroupRole" + groupID.ToString();
                SimianRemoveGenericEntry(agentID, GroupRoleMemberType, roleID.ToString());
            }
        }

        public List<DirGroupsReplyData> FindGroups(UUID requestingAgentID, string search)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<DirGroupsReplyData> findings = new List<DirGroupsReplyData>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "Type", "Group" },
                { "Key", search },
                { "Fuzzy", "1" }
            };


            OSDMap response = CachedPostRequest(requestArgs);
            if (response["Success"].AsBoolean() && response["Entries"] is OSDArray)
            {
                OSDArray entryArray = (OSDArray)response["Entries"];
                foreach (OSDMap entryMap in entryArray)
                {
                    DirGroupsReplyData data = new DirGroupsReplyData();
                    data.groupID = entryMap["OwnerID"].AsUUID();
                    data.groupName = entryMap["Key"].AsString();

                    // TODO: is there a better way to do this?
                    Dictionary<UUID, OSDMap> Members;
                    if (SimianGetGenericEntries("GroupMember", data.groupID.ToString(), out Members))
                    {
                        data.members = Members.Count;
                    }
                    else
                    {
                        data.members = 0;
                    }

                    // TODO: sort results?
                    // data.searchOrder = order;

                    findings.Add(data);
                }
            }

            return findings;
        }

        public GroupMembershipData GetAgentGroupMembership(UUID requestingAgentID, UUID agentID, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GroupMembershipData data = null;
//            bool foundData = false;

            OSDMap UserGroupMemberInfo;
            if (SimianGetGenericEntry(agentID, "GroupMember", groupID.ToString(), out UserGroupMemberInfo))
            {
                data = new GroupMembershipData();
                data.AcceptNotices = UserGroupMemberInfo["AcceptNotices"].AsBoolean();
                data.Contribution = UserGroupMemberInfo["Contribution"].AsInteger();
                data.ListInProfile = UserGroupMemberInfo["ListInProfile"].AsBoolean();
                data.ActiveRole = UserGroupMemberInfo["SelectedRoleID"].AsUUID();

                ///////////////////////////////
                // Agent Specific Information:
                //
                OSDMap UserActiveGroup;
                if (SimianGetGenericEntry(agentID, "Group", "ActiveGroup", out UserActiveGroup))
                {
                    data.Active = UserActiveGroup["GroupID"].AsUUID().Equals(groupID);
                }

                ///////////////////////////////
                // Role Specific Information:
                //
                OSDMap GroupRoleInfo;
                if (SimianGetGenericEntry(groupID, "GroupRole", data.ActiveRole.ToString(), out GroupRoleInfo))
                {
                    data.GroupTitle = GroupRoleInfo["Title"].AsString();
                    data.GroupPowers = GroupRoleInfo["Powers"].AsULong();
                }

                ///////////////////////////////
                // Group Specific Information:
                //
                OSDMap GroupInfo;
                string GroupName;
                if (SimianGetFirstGenericEntry(groupID, "Group", out GroupName, out GroupInfo))
                {
                    data.GroupID = groupID;
                    data.AllowPublish = GroupInfo["AllowPublish"].AsBoolean();
                    data.Charter = GroupInfo["Charter"].AsString();
                    data.FounderID = GroupInfo["FounderID"].AsUUID();
                    data.GroupName = GroupName;
                    data.GroupPicture = GroupInfo["InsigniaID"].AsUUID();
                    data.MaturePublish = GroupInfo["MaturePublish"].AsBoolean();
                    data.MembershipFee = GroupInfo["MembershipFee"].AsInteger();
                    data.OpenEnrollment = GroupInfo["OpenEnrollment"].AsBoolean();
                    data.ShowInList = GroupInfo["ShowInList"].AsBoolean();
                }
            }

            return data;
        }

        public GroupMembershipData GetAgentActiveMembership(UUID requestingAgentID, UUID agentID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            UUID GroupID = UUID.Zero;
            OSDMap UserActiveGroup;
            if (SimianGetGenericEntry(agentID, "Group", "ActiveGroup", out UserActiveGroup))
            {
                GroupID = UserActiveGroup["GroupID"].AsUUID();
            }

            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  Active GroupID : {0}", GroupID.ToString());
            return GetAgentGroupMembership(requestingAgentID, agentID, GroupID);
        }

        public List<GroupMembershipData> GetAgentGroupMemberships(UUID requestingAgentID, UUID agentID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupMembershipData> memberships = new List<GroupMembershipData>();

            Dictionary<string,OSDMap> GroupMemberShips;
            if (SimianGetGenericEntries(agentID, "GroupMember", out GroupMemberShips))
            {
                foreach (string key in GroupMemberShips.Keys)
                {
                    memberships.Add(GetAgentGroupMembership(requestingAgentID, agentID, UUID.Parse(key)));
                }
            }

            return memberships;
        }

        public List<GroupRolesData> GetAgentGroupRoles(UUID requestingAgentID, UUID agentID, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> Roles = new List<GroupRolesData>();

            Dictionary<string, OSDMap> GroupRoles;
            if (SimianGetGenericEntries(groupID, "GroupRole", out GroupRoles))
            {
                Dictionary<string, OSDMap> MemberRoles;
                if (SimianGetGenericEntries(agentID, "GroupRole" + groupID.ToString(), out MemberRoles))
                {
                    foreach (KeyValuePair<string, OSDMap> kvp in MemberRoles)
                    {
                        GroupRolesData data = new GroupRolesData();
                        data.RoleID = UUID.Parse(kvp.Key);
                        data.Name = GroupRoles[kvp.Key]["Name"].AsString();
                        data.Description = GroupRoles[kvp.Key]["Description"].AsString();
                        data.Title = GroupRoles[kvp.Key]["Title"].AsString();
                        data.Powers = GroupRoles[kvp.Key]["Powers"].AsULong();

                        Roles.Add(data);
                    }
                }
            }
            return Roles;
        }

        public List<GroupRolesData> GetGroupRoles(UUID requestingAgentID, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRolesData> Roles = new List<GroupRolesData>();

            Dictionary<string, OSDMap> GroupRoles;
            if (SimianGetGenericEntries(groupID, "GroupRole", out GroupRoles))
            {
                foreach (KeyValuePair<string, OSDMap> role in GroupRoles)
                {
                    GroupRolesData data = new GroupRolesData();

                    data.RoleID = UUID.Parse(role.Key);

                    data.Name = role.Value["Name"].AsString();
                    data.Description = role.Value["Description"].AsString();
                    data.Title = role.Value["Title"].AsString();
                    data.Powers = role.Value["Powers"].AsULong();

                    Dictionary<UUID, OSDMap> GroupRoleMembers;
                    if (SimianGetGenericEntries("GroupRole" + groupID.ToString(), role.Key, out GroupRoleMembers))
                    {
                        data.Members = GroupRoleMembers.Count;
                    }
                    else
                    {
                        data.Members = 0;
                    }

                    Roles.Add(data);
                }
            }

            return Roles;

        }



        public List<GroupMembersData> GetGroupMembers(UUID requestingAgentID, UUID GroupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupMembersData> members = new List<GroupMembersData>();

            OSDMap GroupInfo;
            string GroupName;
            UUID GroupOwnerRoleID = UUID.Zero;
            if (!SimianGetFirstGenericEntry(GroupID, "Group", out GroupName, out GroupInfo))
            {
                return members;
            }
            GroupOwnerRoleID = GroupInfo["OwnerRoleID"].AsUUID();

            // Locally cache group roles, since we'll be needing this data for each member
            Dictionary<string,OSDMap> GroupRoles;
            SimianGetGenericEntries(GroupID, "GroupRole", out GroupRoles);

            // Locally cache list of group owners
            Dictionary<UUID, OSDMap> GroupOwners;
            SimianGetGenericEntries("GroupRole" + GroupID.ToString(), GroupOwnerRoleID.ToString(), out GroupOwners);


            Dictionary<UUID, OSDMap> GroupMembers;
            if (SimianGetGenericEntries("GroupMember", GroupID.ToString(), out GroupMembers))
            {
                foreach (KeyValuePair<UUID, OSDMap> member in GroupMembers)
                {
                    GroupMembersData data = new GroupMembersData();

                    data.AgentID = member.Key;

                    UUID SelectedRoleID = member.Value["SelectedRoleID"].AsUUID();

                    data.AcceptNotices = member.Value["AcceptNotices"].AsBoolean();
                    data.ListInProfile = member.Value["ListInProfile"].AsBoolean();
                    data.Contribution = member.Value["Contribution"].AsInteger();

                    data.IsOwner = GroupOwners.ContainsKey(member.Key);

                    OSDMap GroupRoleInfo = GroupRoles[SelectedRoleID.ToString()];
                    data.Title = GroupRoleInfo["Title"].AsString();
                    data.AgentPowers = GroupRoleInfo["Powers"].AsULong();

                    members.Add(data);
                }
            }

            return members;

        }

        public List<GroupRoleMembersData> GetGroupRoleMembers(UUID requestingAgentID, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupRoleMembersData> members = new List<GroupRoleMembersData>();

            Dictionary<string, OSDMap> GroupRoles;
            if (SimianGetGenericEntries(groupID, "GroupRole", out GroupRoles))
            {
                foreach (KeyValuePair<string, OSDMap> Role in GroupRoles)
                {
                    Dictionary<UUID, OSDMap> GroupRoleMembers;
                    if (SimianGetGenericEntries("GroupRole"+groupID.ToString(), Role.Key, out GroupRoleMembers))
                    {
                        foreach (KeyValuePair<UUID, OSDMap> GroupRoleMember in GroupRoleMembers)
                        {
                            GroupRoleMembersData data = new GroupRoleMembersData();

                            data.MemberID = GroupRoleMember.Key;
                            data.RoleID = UUID.Parse(Role.Key);

                            members.Add(data);
                        }
                    }
                }
            }

            return members;
        }

        public List<GroupNoticeData> GetGroupNotices(UUID requestingAgentID, UUID GroupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            List<GroupNoticeData> values = new List<GroupNoticeData>();

            Dictionary<string, OSDMap> Notices;
            if (SimianGetGenericEntries(GroupID, "GroupNotice", out Notices))
            {
                foreach (KeyValuePair<string, OSDMap> Notice in Notices)
                {
                    GroupNoticeData data = new GroupNoticeData();
                    data.NoticeID = UUID.Parse(Notice.Key);
                    data.Timestamp = Notice.Value["TimeStamp"].AsUInteger();
                    data.FromName = Notice.Value["FromName"].AsString();
                    data.Subject = Notice.Value["Subject"].AsString();
                    data.HasAttachment = Notice.Value["BinaryBucket"].AsBinary().Length > 0;

                    //TODO: Figure out how to get this
                    data.AssetType = 0;

                    values.Add(data);
                }
            }

            return values;

        }
        public GroupNoticeInfo GetGroupNotice(UUID requestingAgentID, UUID noticeID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap GroupNotice;
            UUID GroupID;
            if (SimianGetFirstGenericEntry("GroupNotice", noticeID.ToString(), out GroupID, out GroupNotice))
            {
                GroupNoticeInfo data = new GroupNoticeInfo();
                data.GroupID = GroupID;
                data.Message = GroupNotice["Message"].AsString();
                data.BinaryBucket = GroupNotice["BinaryBucket"].AsBinary();
                data.noticeData.NoticeID = noticeID;
                data.noticeData.Timestamp = GroupNotice["TimeStamp"].AsUInteger();
                data.noticeData.FromName = GroupNotice["FromName"].AsString();
                data.noticeData.Subject = GroupNotice["Subject"].AsString();
                data.noticeData.HasAttachment = data.BinaryBucket.Length > 0;
                data.noticeData.AssetType = 0;

                if (data.Message == null)
                {
                    data.Message = string.Empty;
                }

                return data;
            }
            return null;
        }
        public void AddGroupNotice(UUID requestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message, byte[] binaryBucket)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap Notice = new OSDMap();
            Notice["TimeStamp"] = OSD.FromUInteger((uint)Util.UnixTimeSinceEpoch());
            Notice["FromName"] = OSD.FromString(fromName);
            Notice["Subject"] = OSD.FromString(subject);
            Notice["Message"] = OSD.FromString(message);
            Notice["BinaryBucket"] = OSD.FromBinary(binaryBucket);

            SimianAddGeneric(groupID, "GroupNotice", noticeID.ToString(), Notice);

        }
        #endregion

        #region GroupSessionTracking

        public void ResetAgentGroupChatSessions(UUID agentID)
        {
            Dictionary<string, OSDMap> agentSessions;

            if (SimianGetGenericEntries(agentID, "GroupSessionDropped", out agentSessions))
            {
                foreach (string GroupID in agentSessions.Keys)
                {
                    SimianRemoveGenericEntry(agentID, "GroupSessionDropped", GroupID);
                }
            }

            if (SimianGetGenericEntries(agentID, "GroupSessionInvited", out agentSessions))
            {
                foreach (string GroupID in agentSessions.Keys)
                {
                    SimianRemoveGenericEntry(agentID, "GroupSessionInvited", GroupID);
                }
            }
        }

        public bool hasAgentDroppedGroupChatSession(UUID agentID, UUID groupID)
        {
            OSDMap session;
            return SimianGetGenericEntry(agentID, "GroupSessionDropped", groupID.ToString(), out session);
        }

        public void AgentDroppedFromGroupChatSession(UUID agentID, UUID groupID)
        {
            SimianAddGeneric(agentID, "GroupSessionDropped", groupID.ToString(), new OSDMap());
        }

        public void AgentInvitedToGroupChatSession(UUID agentID, UUID groupID)
        {
            SimianAddGeneric(agentID, "GroupSessionInvited", groupID.ToString(), new OSDMap());
        }

        public bool hasAgentBeenInvitedToGroupChatSession(UUID agentID, UUID groupID)
        {
            OSDMap session;
            return SimianGetGenericEntry(agentID, "GroupSessionDropped", groupID.ToString(), out session);
        }

        #endregion

        private void EnsureRoleNotSelectedByMember(UUID groupID, UUID roleID, UUID userID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            // If member's SelectedRole is roleID, change their selected role to Everyone
            // before removing them from the role
            OSDMap UserGroupInfo;
            if (SimianGetGenericEntry(userID, "GroupMember", groupID.ToString(), out UserGroupInfo))
            {
                if (UserGroupInfo["SelectedRoleID"].AsUUID() == roleID)
                {
                    UserGroupInfo["SelectedRoleID"] = OSD.FromUUID(UUID.Zero);
                }
                SimianAddGeneric(userID, "GroupMember", groupID.ToString(), UserGroupInfo);
            }
        }


        #region Simian Util Methods
        private bool SimianAddGeneric(UUID ownerID, string type, string key, OSDMap map)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called ({1},{2},{3})", System.Reflection.MethodBase.GetCurrentMethod().Name, ownerID, type, key);

            string value = OSDParser.SerializeJsonString(map);

            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  value: {0}", value);

            NameValueCollection RequestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddGeneric" },
                { "OwnerID", ownerID.ToString() },
                { "Type", type },
                { "Key", key },
                { "Value", value}
            };


            OSDMap Response = CachedPostRequest(RequestArgs);
            if (Response["Success"].AsBoolean())
            {
                return true;
            }
            else
            {
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: Error {0}, {1}, {2}, {3}", ownerID, type, key, Response["Message"]);
                return false;
            }
        }

        /// <summary>
        /// Returns the first of possibly many entries for Owner/Type pair
        /// </summary>
        private bool SimianGetFirstGenericEntry(UUID ownerID, string type, out string key, out OSDMap map)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called ({1},{2})", System.Reflection.MethodBase.GetCurrentMethod().Name, ownerID, type);

            NameValueCollection RequestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "OwnerID", ownerID.ToString() },
                { "Type", type }
            };


            OSDMap Response = CachedPostRequest(RequestArgs);
            if (Response["Success"].AsBoolean() && Response["Entries"] is OSDArray)
            {
                OSDArray entryArray = (OSDArray)Response["Entries"];
                if (entryArray.Count >= 1)
                {
                    OSDMap entryMap = entryArray[0] as OSDMap;
                    key = entryMap["Key"].AsString();
                    map = (OSDMap)OSDParser.DeserializeJson(entryMap["Value"].AsString());

                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  Generics Result {0}", entryMap["Value"].AsString());

                    return true;
                }
                else
                {
                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  No Generics Results");
                }
            }
            else
            {
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: Error retrieving group info ({0})", Response["Message"]);
            }
            key = null;
            map = null;
            return false;
        }
        private bool SimianGetFirstGenericEntry(string type, string key, out UUID ownerID, out OSDMap map)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called ({1},{2})", System.Reflection.MethodBase.GetCurrentMethod().Name, type, key);


            NameValueCollection RequestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "Type", type },
                { "Key", key}
            };


            OSDMap Response = CachedPostRequest(RequestArgs);
            if (Response["Success"].AsBoolean() && Response["Entries"] is OSDArray)
            {
                OSDArray entryArray = (OSDArray)Response["Entries"];
                if (entryArray.Count >= 1)
                {
                    OSDMap entryMap = entryArray[0] as OSDMap;
                    ownerID = entryMap["OwnerID"].AsUUID();
                    map = (OSDMap)OSDParser.DeserializeJson(entryMap["Value"].AsString());

                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  Generics Result {0}", entryMap["Value"].AsString());

                    return true;
                }
                else
                {
                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  No Generics Results");
                }
            }
            else
            {
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: Error retrieving group info ({0})", Response["Message"]);
            }
            ownerID = UUID.Zero;
            map = null;
            return false;
        }

        private bool SimianGetGenericEntry(UUID ownerID, string type, string key, out OSDMap map)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called ({1},{2},{3})", System.Reflection.MethodBase.GetCurrentMethod().Name, ownerID, type, key);

            NameValueCollection RequestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "OwnerID", ownerID.ToString() },
                { "Type", type },
                { "Key", key}
            };


            OSDMap Response = CachedPostRequest(RequestArgs);
            if (Response["Success"].AsBoolean() && Response["Entries"] is OSDArray)
            {
                OSDArray entryArray = (OSDArray)Response["Entries"];
                if (entryArray.Count == 1)
                {
                    OSDMap entryMap = entryArray[0] as OSDMap;
                    key = entryMap["Key"].AsString();
                    map = (OSDMap)OSDParser.DeserializeJson(entryMap["Value"].AsString());

                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  Generics Result {0}", entryMap["Value"].AsString());

                    return true;
                }
                else
                {
                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  No Generics Results");
                }
            }
            else
            {
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: Error retrieving group info ({0})", Response["Message"]);
            }
            map = null;
            return false;
        }

        private bool SimianGetGenericEntries(UUID ownerID, string type, out Dictionary<string, OSDMap> maps)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called ({1},{2})", System.Reflection.MethodBase.GetCurrentMethod().Name,ownerID, type);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "OwnerID", ownerID.ToString() },
                { "Type", type }
            };



            OSDMap response = CachedPostRequest(requestArgs);
            if (response["Success"].AsBoolean() && response["Entries"] is OSDArray)
            {
                maps = new Dictionary<string, OSDMap>();

                OSDArray entryArray = (OSDArray)response["Entries"];
                foreach (OSDMap entryMap in entryArray)
                {
                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  Generics Result {0}", entryMap["Value"].AsString());
                    maps.Add(entryMap["Key"].AsString(), (OSDMap)OSDParser.DeserializeJson(entryMap["Value"].AsString()));
                }
                if (maps.Count == 0)
                {
                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  No Generics Results");
                }

                return true;
            }
            else
            {
                maps = null;
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: Error retrieving group info ({0})", response["Message"]);
            }
            return false;
        }
        private bool SimianGetGenericEntries(string type, string key, out Dictionary<UUID, OSDMap> maps)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called ({1},{2})", System.Reflection.MethodBase.GetCurrentMethod().Name, type, key);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "Type", type },
                { "Key", key }
            };



            OSDMap response = CachedPostRequest(requestArgs);
            if (response["Success"].AsBoolean() && response["Entries"] is OSDArray)
            {
                maps = new Dictionary<UUID, OSDMap>();

                OSDArray entryArray = (OSDArray)response["Entries"];
                foreach (OSDMap entryMap in entryArray)
                {
                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  Generics Result {0}", entryMap["Value"].AsString());
                    maps.Add(entryMap["OwnerID"].AsUUID(), (OSDMap)OSDParser.DeserializeJson(entryMap["Value"].AsString()));
                }
                if (maps.Count == 0)
                {
                    if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  No Generics Results");
                }
                return true;
            }
            else
            {
                maps = null;
                m_log.WarnFormat("[SIMIAN-GROUPS-CONNECTOR]: Error retrieving group info ({0})", response["Message"]);
            }
            return false;
        }

        private bool SimianRemoveGenericEntry(UUID ownerID, string type, string key)
        {
            if (m_debugEnabled) m_log.InfoFormat("[SIMIAN-GROUPS-CONNECTOR]  {0} called ({1},{2},{3})", System.Reflection.MethodBase.GetCurrentMethod().Name, ownerID, type, key);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "RemoveGeneric" },
                { "OwnerID", ownerID.ToString() },
                { "Type", type },
                { "Key", key }
            };


            OSDMap response = CachedPostRequest(requestArgs);
            if (response["Success"].AsBoolean())
            {
                return true;
            }
            else
            {
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: Error {0}, {1}, {2}, {3}", ownerID, type, key, response["Message"]);
                return false;
            }
        }
        #endregion

        #region CheesyCache
        OSDMap CachedPostRequest(NameValueCollection requestArgs)
        {
            // Immediately forward the request if the cache is disabled.
            if (m_cacheTimeout == 0)
            {
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: cache is disabled");
                return WebUtil.PostToService(m_groupsServerURI, requestArgs);
            }

            // Check if this is an update or a request
            if (requestArgs["RequestMethod"] == "RemoveGeneric"
                || requestArgs["RequestMethod"] == "AddGeneric")
            {
                m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: clearing generics cache");

                // Any and all updates cause the cache to clear
                m_memoryCache.Clear();

                // Send update to server, return the response without caching it
                return WebUtil.PostToService(m_groupsServerURI, requestArgs);
            }

            // If we're not doing an update, we must be requesting data

            // Create the cache key for the request and see if we have it cached
            string CacheKey = WebUtil.BuildQueryString(requestArgs);

            // This code uses a leader/follower pattern. On a cache miss, the request is added
            // to a queue; the first thread to add it to the queue completes the request while
            // follow on threads busy wait for the results, this situation seems to happen
            // often when checking permissions
            while (true)
            {
                OSDMap response = null;
                bool firstRequest = false;

                lock (m_memoryCache)
                {
                    if (m_memoryCache.TryGetValue(CacheKey, out response))
                        return response;

                    if (! m_pendingRequests.ContainsKey(CacheKey))
                    {
                        m_pendingRequests.Add(CacheKey,true);
                        firstRequest = true;
                    }
                }

                if (firstRequest)
                {
                    // if it wasn't in the cache, pass the request to the Simian Grid Services
                    try
                    {
                        response = WebUtil.PostToService(m_groupsServerURI, requestArgs);
                    }
                    catch (Exception)
                    {
                        m_log.ErrorFormat("[SIMIAN GROUPS CONNECTOR]: request failed {0}", CacheKey);
                    }

                    // and cache the response
                    lock (m_memoryCache)
                    {
                        m_memoryCache.AddOrUpdate(CacheKey, response, TimeSpan.FromSeconds(m_cacheTimeout));
                        m_pendingRequests.Remove(CacheKey);
                    }

                    return response;
                }

                Thread.Sleep(50); // waiting for a web request to complete, 50msecs is reasonable
            }

            // if (!m_memoryCache.TryGetValue(CacheKey, out response))
            // {
            //     m_log.WarnFormat("[SIMIAN GROUPS CONNECTOR]: query not in the cache");
            //     Util.PrintCallStack();

            //     // if it wasn't in the cache, pass the request to the Simian Grid Services
            //     response = WebUtil.PostToService(m_groupsServerURI, requestArgs);

            //     // and cache the response
            //     m_memoryCache.AddOrUpdate(CacheKey, response, TimeSpan.FromSeconds(m_cacheTimeout));
            // }

            // // return cached response
            // return response;
        }
        #endregion

    }

}

