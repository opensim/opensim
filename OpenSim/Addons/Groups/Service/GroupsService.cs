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
using Nini.Config;

using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;

namespace OpenSim.Groups
{
    public class GroupsService : GroupsServiceBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const GroupPowers DefaultEveryonePowers =
            GroupPowers.AllowSetHome |
            GroupPowers.Accountable |
            GroupPowers.JoinChat |
            GroupPowers.AllowVoiceChat |
            GroupPowers.ReceiveNotices |
            GroupPowers.StartProposal |
            GroupPowers.VoteOnProposal;

        public const GroupPowers OfficersPowers = DefaultEveryonePowers |
            GroupPowers.AllowFly |
            GroupPowers.AllowLandmark |
            GroupPowers.AllowRez |
            GroupPowers.AssignMemberLimited |
            GroupPowers.ChangeIdentity |
            GroupPowers.ChangeMedia |
            GroupPowers.ChangeOptions |
            GroupPowers.DeedObject |
            GroupPowers.Eject |
            GroupPowers.FindPlaces |
            GroupPowers.Invite |
            GroupPowers.LandChangeIdentity |
            GroupPowers.LandDeed |
            GroupPowers.LandDivideJoin |
            GroupPowers.LandEdit |
            GroupPowers.LandEjectAndFreeze |
            GroupPowers.LandGardening |
            GroupPowers.LandManageAllowed |
            GroupPowers.LandManageBanned |
            GroupPowers.LandManagePasses |
            GroupPowers.LandOptions |
            GroupPowers.LandRelease |
            GroupPowers.LandSetSale |
            GroupPowers.MemberVisible |
            GroupPowers.ModerateChat |
            GroupPowers.ObjectManipulate |
            GroupPowers.ObjectSetForSale |
            GroupPowers.ReturnGroupOwned |
            GroupPowers.ReturnGroupSet |
            GroupPowers.ReturnNonGroup |
            GroupPowers.RoleProperties |
            GroupPowers.SendNotices |
            GroupPowers.SetLandingPoint;

        public const GroupPowers OwnerPowers = OfficersPowers | 
            GroupPowers.Accountable |
            GroupPowers.AllowEditLand |
            GroupPowers.AssignMember |
            GroupPowers.ChangeActions |
            GroupPowers.CreateRole |
            GroupPowers.DeleteRole |
            GroupPowers.ExperienceAdmin |
            GroupPowers.ExperienceCreator |
            GroupPowers.GroupBanAccess |
            GroupPowers.HostEvent |
            GroupPowers.RemoveMember;

        #region Daily Cleanup

        private Timer m_CleanupTimer;

        public GroupsService(IConfigSource config, string configName)
            : base(config, configName)
        {
        }

        public GroupsService(IConfigSource config)
            : this(config, string.Empty)
        {
            // Once a day
            m_CleanupTimer = new Timer(24 * 60 * 60 * 1000);
            m_CleanupTimer.AutoReset = true;
            m_CleanupTimer.Elapsed += new ElapsedEventHandler(m_CleanupTimer_Elapsed);
            m_CleanupTimer.Enabled = true;
            m_CleanupTimer.Start();
        }

        private void m_CleanupTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_Database.DeleteOldNotices();
            m_Database.DeleteOldInvites();
        }

        #endregion

        public UUID CreateGroup(string RequestingAgentID, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment,
            bool allowPublish, bool maturePublish, UUID founderID, out string reason)
        {
            reason = string.Empty;

            // Check if the group already exists
            if (m_Database.RetrieveGroup(name) != null)
            {
                reason = "A group with that name already exists";
                return UUID.Zero;
            }

            // Create the group
            GroupData data = new GroupData();
            data.GroupID = UUID.Random();
            data.Data = new Dictionary<string, string>();
            data.Data["Name"] = name;
            data.Data["Charter"] = charter;
            data.Data["InsigniaID"] = insigniaID.ToString();
            data.Data["FounderID"] = founderID.ToString();
            data.Data["MembershipFee"] = membershipFee.ToString();
            data.Data["OpenEnrollment"] = openEnrollment ? "1" : "0";
            data.Data["ShowInList"] = showInList ? "1" : "0";
            data.Data["AllowPublish"] = allowPublish ? "1" : "0";
            data.Data["MaturePublish"] = maturePublish ? "1" : "0";
            UUID ownerRoleID = UUID.Random();
            data.Data["OwnerRoleID"] = ownerRoleID.ToString();

            if (!m_Database.StoreGroup(data))
                return UUID.Zero;

            // Create Everyone role
            _AddOrUpdateGroupRole(RequestingAgentID, data.GroupID, UUID.Zero, "Everyone", "Everyone in the group is in the everyone role.", "Member of " + name, (ulong)DefaultEveryonePowers, true);

            // Create Officers role
            UUID officersRoleID = UUID.Random();
            _AddOrUpdateGroupRole(RequestingAgentID, data.GroupID, officersRoleID, "Officers", "The officers of the group, with more powers than regular members.", "Officer of " + name, (ulong)OfficersPowers, true);

            // Create Owner role
            _AddOrUpdateGroupRole(RequestingAgentID, data.GroupID, ownerRoleID, "Owners", "Owners of the group", "Owner of " + name, (ulong)OwnerPowers, true);

            // Add founder to group
            _AddAgentToGroup(RequestingAgentID, founderID.ToString(), data.GroupID, ownerRoleID);
            _AddAgentToGroup(RequestingAgentID, founderID.ToString(), data.GroupID, officersRoleID);

            return data.GroupID;
        }

        public void UpdateGroup(string RequestingAgentID, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            GroupData data = m_Database.RetrieveGroup(groupID);
            if (data == null)
                return;

            // Check perms
            if (!HasPower(RequestingAgentID, groupID, GroupPowers.ChangeActions))
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at updating group {1} denied because of lack of permission", RequestingAgentID, groupID);
                return;
            }

            data.GroupID = groupID;
            data.Data["Charter"] = charter;
            data.Data["ShowInList"] = showInList ? "1" : "0";
            data.Data["InsigniaID"] = insigniaID.ToString();
            data.Data["MembershipFee"] = membershipFee.ToString();
            data.Data["OpenEnrollment"] = openEnrollment ? "1" : "0";
            data.Data["AllowPublish"] = allowPublish ? "1" : "0";
            data.Data["MaturePublish"] = maturePublish ? "1" : "0";

            m_Database.StoreGroup(data);

        }

        public ExtendedGroupRecord GetGroupRecord(string RequestingAgentID, UUID GroupID)
        {
            GroupData data = m_Database.RetrieveGroup(GroupID);

            return _GroupDataToRecord(data);
        }

        public ExtendedGroupRecord GetGroupRecord(string RequestingAgentID, string GroupName)
        {
            GroupData data = m_Database.RetrieveGroup(GroupName);

            return _GroupDataToRecord(data);
        }

        public List<DirGroupsReplyData> FindGroups(string RequestingAgentID, string search)
        {
            List<DirGroupsReplyData> groups = new List<DirGroupsReplyData>();

            GroupData[] data = m_Database.RetrieveGroups(search);

            if (data != null && data.Length > 0)
            {
                foreach (GroupData d in data)
                {
                    // Don't list group proxies
                    if (d.Data.ContainsKey("Location") && d.Data["Location"] != string.Empty)
                        continue;

                    int nmembers = m_Database.MemberCount(d.GroupID);
                    if(nmembers == 0)
                        continue;

                    DirGroupsReplyData g = new DirGroupsReplyData();

                    if (d.Data.ContainsKey("Name"))
                        g.groupName = d.Data["Name"];
                    else
                    {
                        m_log.DebugFormat("[Groups]: Key Name not found");
                        continue;
                    }

                    g.groupID = d.GroupID;
                    g.members = nmembers;

                    groups.Add(g);
                }
            }

            return groups;
        }

        public List<ExtendedGroupMembersData> GetGroupMembers(string RequestingAgentID, UUID GroupID)
        {
            List<ExtendedGroupMembersData> members = new List<ExtendedGroupMembersData>();

            GroupData group = m_Database.RetrieveGroup(GroupID);
            if (group == null)
                return members;

            // Unfortunately this doesn't quite work on legacy group data because of a bug
            // that's also being fixed here on CreateGroup. The OwnerRoleID sent to the DB was wrong.
            // See how to find the ownerRoleID a few lines below.
            UUID ownerRoleID = new UUID(group.Data["OwnerRoleID"]);

            RoleData[] roles = m_Database.RetrieveRoles(GroupID);
            if (roles == null)
                // something wrong with this group
                return members;
            List<RoleData> rolesList = new List<RoleData>(roles);

            // Let's find the "real" ownerRoleID
            RoleData ownerRole = rolesList.Find(r => r.Data["Powers"] == ((long)OwnerPowers).ToString());
            if (ownerRole != null)
                ownerRoleID = ownerRole.RoleID;

            // Check visibility?
            // When we don't want to check visibility, we pass it "all" as the requestingAgentID
            bool checkVisibility = !RequestingAgentID.Equals(UUID.Zero.ToString());

            if (checkVisibility)
            {
                // Is the requester a member of the group?
                bool isInGroup = false;
                if (m_Database.RetrieveMember(GroupID, RequestingAgentID) != null)
                    isInGroup = true;

                if (!isInGroup) // reduce the roles to the visible ones
                    rolesList = rolesList.FindAll(r => (UInt64.Parse(r.Data["Powers"]) & (ulong)GroupPowers.MemberVisible) != 0);
            }

            MembershipData[] datas = m_Database.RetrieveMembers(GroupID);
            if (datas == null || (datas != null && datas.Length == 0))
                return members;

            // OK, we have everything we need

            foreach (MembershipData d in datas)
            {
                RoleMembershipData[] rolememberships = m_Database.RetrieveMemberRoles(GroupID, d.PrincipalID);
                List<RoleMembershipData> rolemembershipsList = new List<RoleMembershipData>(rolememberships);

                ExtendedGroupMembersData m = new ExtendedGroupMembersData();

                // What's this person's current role in the group?
                UUID selectedRole = new UUID(d.Data["SelectedRoleID"]);
                RoleData selected = rolesList.Find(r => r.RoleID == selectedRole);

                if (selected != null)
                {
                    m.Title = selected.Data["Title"];
                    m.AgentPowers = UInt64.Parse(selected.Data["Powers"]);
                }

                m.AgentID = d.PrincipalID;
                m.AcceptNotices = d.Data["AcceptNotices"] == "1" ? true : false;
                m.Contribution = Int32.Parse(d.Data["Contribution"]);
                m.ListInProfile = d.Data["ListInProfile"] == "1" ? true : false;

                GridUserData gud = m_GridUserService.Get(d.PrincipalID);
                if (gud != null)
                {
                    if (bool.Parse(gud.Data["Online"]))
                    {
                        m.OnlineStatus = @"Online";
                    }
                    else
                    {
                        int unixtime = int.Parse(gud.Data["Login"]);
                        // The viewer is very picky about how these strings are formed. Eg. it will crash on malformed dates!
                        m.OnlineStatus = (unixtime == 0) ? @"unknown" : Util.ToDateTime(unixtime).ToString("MM/dd/yyyy");
                    }
                }

                // Is this person an owner of the group?
                m.IsOwner = (rolemembershipsList.Find(r => r.RoleID == ownerRoleID) != null) ? true : false;

                members.Add(m);
            }

            return members;
        }

        public bool AddGroupRole(string RequestingAgentID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, out string reason)
        {
            reason = string.Empty;
            // check that the requesting agent has permissions to add role
            if (!HasPower(RequestingAgentID, groupID, GroupPowers.CreateRole))
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at creating role in group {1} denied because of lack of permission", RequestingAgentID, groupID);
                reason = "Insufficient permission to create role";
                return false;
            }

            return _AddOrUpdateGroupRole(RequestingAgentID, groupID, roleID, name, description, title, powers, true);

        }

        public bool UpdateGroupRole(string RequestingAgentID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers)
        {
            // check perms
            if (!HasPower(RequestingAgentID, groupID, GroupPowers.ChangeActions))
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at changing role in group {1} denied because of lack of permission", RequestingAgentID, groupID);
                return false;
            }

            return _AddOrUpdateGroupRole(RequestingAgentID, groupID, roleID, name, description, title, powers, false);
        }

        public void RemoveGroupRole(string RequestingAgentID, UUID groupID, UUID roleID)
        {
            // check perms
            if (!HasPower(RequestingAgentID, groupID, GroupPowers.DeleteRole))
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at deleting role from group {1} denied because of lack of permission", RequestingAgentID, groupID);
                return;
            }

            // Can't delete Everyone and Owners roles
            if (roleID == UUID.Zero)
            {
                m_log.DebugFormat("[Groups]: Attempt at deleting Everyone role from group {0} denied", groupID);
                return;
            }

            GroupData group = m_Database.RetrieveGroup(groupID);
            if (group == null)
            {
                m_log.DebugFormat("[Groups]: Attempt at deleting role from non-existing group {0}", groupID);
                return;
            }

            if (roleID == new UUID(group.Data["OwnerRoleID"]))
            {
                m_log.DebugFormat("[Groups]: Attempt at deleting Owners role from group {0} denied", groupID);
                return;
            }

            _RemoveGroupRole(groupID, roleID);
        }

        public List<GroupRolesData> GetGroupRoles(string RequestingAgentID, UUID GroupID)
        {
            // TODO: check perms
            return _GetGroupRoles(GroupID);
        }

        public List<ExtendedGroupRoleMembersData> GetGroupRoleMembers(string RequestingAgentID, UUID GroupID)
        {
            // TODO: check perms

            // Is the requester a member of the group?
            bool isInGroup = false;
            if (m_Database.RetrieveMember(GroupID, RequestingAgentID) != null)
                isInGroup = true;

            return _GetGroupRoleMembers(GroupID, isInGroup);
        }

        public bool AddAgentToGroup(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID, string token, out string reason)
        {
            reason = string.Empty;

            _AddAgentToGroup(RequestingAgentID, AgentID, GroupID, RoleID, token);

            return true;
        }

        public bool RemoveAgentFromGroup(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            // check perms
            if (RequestingAgentID != AgentID && !HasPower(RequestingAgentID, GroupID, GroupPowers.Eject))
                return false;

            _RemoveAgentFromGroup(RequestingAgentID, AgentID, GroupID);

            return true;
        }

        public bool AddAgentToGroupInvite(string RequestingAgentID, UUID inviteID, UUID groupID, UUID roleID, string agentID)
        {
            // Check whether the invitee is already a member of the group
            MembershipData m = m_Database.RetrieveMember(groupID, agentID);
            if (m != null)
                return false;

            // Check permission to invite
            if (!HasPower(RequestingAgentID, groupID, GroupPowers.Invite))
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at inviting to group {1} denied because of lack of permission", RequestingAgentID, groupID);
                return false;
            }

            // Check whether there are pending invitations and delete them
            InvitationData invite = m_Database.RetrieveInvitation(groupID, agentID);
            if (invite != null)
                m_Database.DeleteInvite(invite.InviteID);

            invite = new InvitationData();
            invite.InviteID = inviteID;
            invite.PrincipalID = agentID;
            invite.GroupID = groupID;
            invite.RoleID = roleID;
            invite.Data = new Dictionary<string, string>();

            return m_Database.StoreInvitation(invite);
        }

        public GroupInviteInfo GetAgentToGroupInvite(string RequestingAgentID, UUID inviteID)
        {
            InvitationData data = m_Database.RetrieveInvitation(inviteID);

            if (data == null)
                return null;

            GroupInviteInfo inviteInfo = new GroupInviteInfo();
            inviteInfo.AgentID = data.PrincipalID;
            inviteInfo.GroupID = data.GroupID;
            inviteInfo.InviteID = data.InviteID;
            inviteInfo.RoleID = data.RoleID;

            return inviteInfo;
        }

        public void RemoveAgentToGroupInvite(string RequestingAgentID, UUID inviteID)
        {
            m_Database.DeleteInvite(inviteID);
        }

        public bool AddAgentToGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            //if (!m_Database.CheckOwnerRole(RequestingAgentID, GroupID, RoleID))
            //    return;

            // check permissions
            bool limited = HasPower(RequestingAgentID, GroupID, GroupPowers.AssignMemberLimited);
            bool unlimited = HasPower(RequestingAgentID, GroupID, GroupPowers.AssignMember) || IsOwner(RequestingAgentID, GroupID);
            if (!limited && !unlimited)
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at assigning {1} to role {2} denied because of lack of permission", RequestingAgentID, AgentID, RoleID);
                return false;
            }

            // AssignMemberLimited means that the person can assign another person to the same roles that she has in the group
            if (!unlimited && limited)
            {
                // check whether person's has this role
                RoleMembershipData rolemembership = m_Database.RetrieveRoleMember(GroupID, RoleID, RequestingAgentID);
                if (rolemembership == null)
                {
                    m_log.DebugFormat("[Groups]: ({0}) Attempt at assigning {1} to role {2} denied because of limited permission", RequestingAgentID, AgentID, RoleID);
                    return false;
                }
            }

            _AddAgentToGroupRole(RequestingAgentID, AgentID, GroupID, RoleID);

            return true;
        }

        public bool RemoveAgentFromGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            // Don't remove from Everyone role!
            if (RoleID == UUID.Zero)
                return false;

            // check permissions
            bool limited = HasPower(RequestingAgentID, GroupID, GroupPowers.AssignMemberLimited);
            bool unlimited = HasPower(RequestingAgentID, GroupID, GroupPowers.AssignMember) || IsOwner(RequestingAgentID, GroupID);
            if (!limited && !unlimited)
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at removing {1} from role {2} denied because of lack of permission", RequestingAgentID, AgentID, RoleID);
                return false;
            }

            // AssignMemberLimited means that the person can assign another person to the same roles that she has in the group
            if (!unlimited && limited)
            {
                // check whether person's has this role
                RoleMembershipData rolemembership = m_Database.RetrieveRoleMember(GroupID, RoleID, RequestingAgentID);
                if (rolemembership == null)
                {
                    m_log.DebugFormat("[Groups]: ({0}) Attempt at removing {1} from role {2} denied because of limited permission", RequestingAgentID, AgentID, RoleID);
                    return false;
                }
            }

            RoleMembershipData rolemember = m_Database.RetrieveRoleMember(GroupID, RoleID, AgentID);

            if (rolemember == null)
                return false;

            m_Database.DeleteRoleMember(rolemember);

            // Find another role for this person
            UUID newRoleID = UUID.Zero; // Everyone
            RoleMembershipData[] rdata = m_Database.RetrieveMemberRoles(GroupID, AgentID);
            if (rdata != null)
                foreach (RoleMembershipData r in rdata)
                {
                    if (r.RoleID != UUID.Zero)
                    {
                        newRoleID = r.RoleID;
                        break;
                    }
                }

            MembershipData member = m_Database.RetrieveMember(GroupID, AgentID);
            if (member != null)
            {
                member.Data["SelectedRoleID"] = newRoleID.ToString();
                m_Database.StoreMember(member);
            }

            return true;
        }

        public List<GroupRolesData> GetAgentGroupRoles(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            List<GroupRolesData> roles = new List<GroupRolesData>();
            // TODO: check permissions

            RoleMembershipData[] data = m_Database.RetrieveMemberRoles(GroupID, AgentID);
            if (data == null || (data != null && data.Length ==0))
                return roles;

            foreach (RoleMembershipData d in data)
            {
                RoleData rdata = m_Database.RetrieveRole(GroupID, d.RoleID);
                if (rdata == null) // hippos
                    continue;

                GroupRolesData r = new GroupRolesData();
                r.Name = rdata.Data["Name"];
                r.Powers = UInt64.Parse(rdata.Data["Powers"]);
                r.RoleID = rdata.RoleID;
                r.Title = rdata.Data["Title"];

                roles.Add(r);
            }

            return roles;
        }

        public ExtendedGroupMembershipData SetAgentActiveGroup(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            // TODO: check perms
            PrincipalData principal = new PrincipalData();
            principal.PrincipalID = AgentID;
            principal.ActiveGroupID = GroupID;
            m_Database.StorePrincipal(principal);

            return GetAgentGroupMembership(RequestingAgentID, AgentID, GroupID);
        }

        public ExtendedGroupMembershipData GetAgentActiveMembership(string RequestingAgentID, string AgentID)
        {
            // 1. get the principal data for the active group
            PrincipalData principal = m_Database.RetrievePrincipal(AgentID);
            if (principal == null)
                return null;

            return GetAgentGroupMembership(RequestingAgentID, AgentID, principal.ActiveGroupID);
        }

        public ExtendedGroupMembershipData GetAgentGroupMembership(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            return GetAgentGroupMembership(RequestingAgentID, AgentID, GroupID, null);
        }

        private ExtendedGroupMembershipData GetAgentGroupMembership(string RequestingAgentID, string AgentID, UUID GroupID, MembershipData membership)
        {
            // 2. get the active group
            GroupData group = m_Database.RetrieveGroup(GroupID);
            if (group == null)
                return null;

            // 3. get the membership info if we don't have it already
            if (membership == null)
            {
                membership = m_Database.RetrieveMember(group.GroupID, AgentID);
                if (membership == null)
                    return null;
            }

            // 4. get the active role
            UUID activeRoleID = new UUID(membership.Data["SelectedRoleID"]);
            RoleData role = m_Database.RetrieveRole(group.GroupID, activeRoleID);

            ExtendedGroupMembershipData data = new ExtendedGroupMembershipData();
            data.AcceptNotices = membership.Data["AcceptNotices"] == "1" ? true : false;
            data.AccessToken = membership.Data["AccessToken"];
            data.Active = true;
            data.ActiveRole = activeRoleID;
            data.AllowPublish = group.Data["AllowPublish"] == "1" ? true : false;
            data.Charter = group.Data["Charter"];
            data.Contribution = Int32.Parse(membership.Data["Contribution"]);
            data.FounderID = new UUID(group.Data["FounderID"]);
            data.GroupID = new UUID(group.GroupID);
            data.GroupName = group.Data["Name"];
            data.GroupPicture = new UUID(group.Data["InsigniaID"]);
            if (role != null)
            {
                data.GroupPowers = UInt64.Parse(role.Data["Powers"]);
                data.GroupTitle = role.Data["Title"];
            }
            data.ListInProfile = membership.Data["ListInProfile"] == "1" ? true : false;
            data.MaturePublish = group.Data["MaturePublish"] == "1" ? true : false;
            data.MembershipFee = Int32.Parse(group.Data["MembershipFee"]);
            data.OpenEnrollment = group.Data["OpenEnrollment"] == "1" ? true : false;
            data.ShowInList = group.Data["ShowInList"] == "1" ? true : false;

            return data;
        }

        public List<GroupMembershipData> GetAgentGroupMemberships(string RequestingAgentID, string AgentID)
        {
            List<GroupMembershipData> memberships = new List<GroupMembershipData>();

            // 1. Get all the groups that this person is a member of
            MembershipData[] mdata = m_Database.RetrieveMemberships(AgentID);

            if (mdata == null || (mdata != null && mdata.Length == 0))
                return memberships;

            foreach (MembershipData d in mdata)
            {
                GroupMembershipData gmember = GetAgentGroupMembership(RequestingAgentID, AgentID, d.GroupID, d);
                if (gmember != null)
                {
                    memberships.Add(gmember);
                    //m_log.DebugFormat("[XXX]: Member of {0} as {1}", gmember.GroupName, gmember.GroupTitle);
                    //Util.PrintCallStack();
                }
            }

            return memberships;
        }

        public void SetAgentActiveGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            MembershipData data = m_Database.RetrieveMember(GroupID, AgentID);
            if (data == null)
                return;

            data.Data["SelectedRoleID"] = RoleID.ToString();
            m_Database.StoreMember(data);
        }

        public void UpdateMembership(string RequestingAgentID, string AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile)
        {
            // TODO: check perms

            MembershipData membership = m_Database.RetrieveMember(GroupID, AgentID);
            if (membership == null)
                return;

            membership.Data["AcceptNotices"] = AcceptNotices ? "1" : "0";
            membership.Data["ListInProfile"] = ListInProfile ? "1" : "0";

            m_Database.StoreMember(membership);
        }

        public bool AddGroupNotice(string RequestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message,
            bool hasAttachment, byte attType, string attName, UUID attItemID, string attOwnerID)
        {
            // Check perms
            if (!HasPower(RequestingAgentID, groupID, GroupPowers.SendNotices))
            {
                m_log.DebugFormat("[Groups]: ({0}) Attempt at sending notice to group {1} denied because of lack of permission", RequestingAgentID, groupID);
                return false;
            }

            return _AddNotice(groupID, noticeID, fromName, subject, message, hasAttachment, attType, attName, attItemID, attOwnerID);
        }

        public GroupNoticeInfo GetGroupNotice(string RequestingAgentID, UUID noticeID)
        {
            NoticeData data = m_Database.RetrieveNotice(noticeID);

            if (data == null)
                return null;

            return _NoticeDataToInfo(data);
        }

        public List<ExtendedGroupNoticeData> GetGroupNotices(string RequestingAgentID, UUID groupID)
        {
            NoticeData[] data = m_Database.RetrieveNotices(groupID);
            List<ExtendedGroupNoticeData> infos = new List<ExtendedGroupNoticeData>();

            if (data == null || (data != null && data.Length == 0))
                return infos;

            foreach (NoticeData d in data)
            {
                ExtendedGroupNoticeData info = _NoticeDataToData(d);
                infos.Add(info);
            }

            return infos;
        }

        public void ResetAgentGroupChatSessions(string agentID)
        {
        }

        public bool hasAgentBeenInvitedToGroupChatSession(string agentID, UUID groupID)
        {
            return false;
        }

        public bool hasAgentDroppedGroupChatSession(string agentID, UUID groupID)
        {
            return false;
        }

        public void AgentDroppedFromGroupChatSession(string agentID, UUID groupID)
        {
        }

        public void AgentInvitedToGroupChatSession(string agentID, UUID groupID)
        {
        }

        #region Actions without permission checks

        protected void _AddAgentToGroup(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            _AddAgentToGroup(RequestingAgentID, AgentID, GroupID, RoleID, string.Empty);
        }

        protected void _RemoveAgentFromGroup(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            // 1. Delete membership
            m_Database.DeleteMember(GroupID, AgentID);

            // 2. Remove from rolememberships
            m_Database.DeleteMemberAllRoles(GroupID, AgentID);

            // 3. if it was active group, inactivate it
            PrincipalData principal = m_Database.RetrievePrincipal(AgentID);
            if (principal != null && principal.ActiveGroupID == GroupID)
            {
                principal.ActiveGroupID = UUID.Zero;
                m_Database.StorePrincipal(principal);
            }
        }

        protected void _AddAgentToGroup(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID, string accessToken)
        {
            // Check if it's already there
            MembershipData data = m_Database.RetrieveMember(GroupID, AgentID);
            if (data != null)
                return;

            // Add the membership
            data = new MembershipData();
            data.PrincipalID = AgentID;
            data.GroupID = GroupID;
            data.Data = new Dictionary<string, string>();
            data.Data["SelectedRoleID"] = RoleID.ToString();
            data.Data["Contribution"] = "0";
            data.Data["ListInProfile"] = "1";
            data.Data["AcceptNotices"] = "1";
            data.Data["AccessToken"] = accessToken;

            m_Database.StoreMember(data);

            // Add principal to everyone role
            _AddAgentToGroupRole(RequestingAgentID, AgentID, GroupID, UUID.Zero);

            // Add principal to role, if different from everyone role
            if (RoleID != UUID.Zero)
                _AddAgentToGroupRole(RequestingAgentID, AgentID, GroupID, RoleID);

            // Make this the active group
            PrincipalData pdata = new PrincipalData();
            pdata.PrincipalID = AgentID;
            pdata.ActiveGroupID = GroupID;
            m_Database.StorePrincipal(pdata);

        }

        protected bool _AddOrUpdateGroupRole(string RequestingAgentID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, bool add)
        {
            RoleData data = m_Database.RetrieveRole(groupID, roleID);

            if (add && data != null) // it already exists, can't create
            {
                m_log.DebugFormat("[Groups]: Group {0} already exists. Can't create it again", groupID);
                return false;
            }

            if (!add && data == null) // it doesn't exist, can't update
            {
                m_log.DebugFormat("[Groups]: Group {0} doesn't exist. Can't update it", groupID);
                return false;
            }

            if (add)
                data = new RoleData();

            data.GroupID = groupID;
            data.RoleID = roleID;
            data.Data = new Dictionary<string, string>();
            data.Data["Name"] = name;
            data.Data["Description"] = description;
            data.Data["Title"] = title;
            data.Data["Powers"] = powers.ToString();

            return m_Database.StoreRole(data);
        }

        protected void _RemoveGroupRole(UUID groupID, UUID roleID)
        {
            m_Database.DeleteRole(groupID, roleID);
        }

        protected void _AddAgentToGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            RoleMembershipData data = m_Database.RetrieveRoleMember(GroupID, RoleID, AgentID);
            if (data != null)
                return;

            data = new RoleMembershipData();
            data.GroupID = GroupID;
            data.PrincipalID = AgentID;
            data.RoleID = RoleID;
            m_Database.StoreRoleMember(data);

            // Make it the SelectedRoleID
            MembershipData membership = m_Database.RetrieveMember(GroupID, AgentID);
            if (membership == null)
            {
                m_log.DebugFormat("[Groups]: ({0}) No such member {0} in group {1}", AgentID, GroupID);
                return;
            }

            membership.Data["SelectedRoleID"] = RoleID.ToString();
            m_Database.StoreMember(membership);

        }

        protected List<GroupRolesData> _GetGroupRoles(UUID groupID)
        {
            List<GroupRolesData> roles = new List<GroupRolesData>();

            RoleData[] data = m_Database.RetrieveRoles(groupID);

            if (data == null || (data != null && data.Length == 0))
                return roles;

            foreach (RoleData d in data)
            {
                GroupRolesData r = new GroupRolesData();
                r.Description = d.Data["Description"];
                r.Members = m_Database.RoleMemberCount(groupID, d.RoleID);
                r.Name = d.Data["Name"];
                r.Powers = UInt64.Parse(d.Data["Powers"]);
                r.RoleID = d.RoleID;
                r.Title = d.Data["Title"];

                roles.Add(r);
            }

            return roles;
        }

        protected List<ExtendedGroupRoleMembersData> _GetGroupRoleMembers(UUID GroupID, bool isInGroup)
        {
            List<ExtendedGroupRoleMembersData> rmembers = new List<ExtendedGroupRoleMembersData>();

            RoleData[] rdata = new RoleData[0];
            if (!isInGroup)
            {
                rdata = m_Database.RetrieveRoles(GroupID);
                if (rdata == null || (rdata != null && rdata.Length == 0))
                    return rmembers;
            }
            List<RoleData> rlist = new List<RoleData>(rdata);
            if (!isInGroup)
                rlist = rlist.FindAll(r => (UInt64.Parse(r.Data["Powers"]) & (ulong)GroupPowers.MemberVisible) != 0);

            RoleMembershipData[] data = m_Database.RetrieveRolesMembers(GroupID);

            if (data == null || (data != null && data.Length == 0))
                return rmembers;

            foreach (RoleMembershipData d in data)
            {
                if (!isInGroup)
                {
                    RoleData rd = rlist.Find(_r => _r.RoleID == d.RoleID); // visible role
                    if (rd == null)
                        continue;
                }

                ExtendedGroupRoleMembersData r = new ExtendedGroupRoleMembersData();
                r.MemberID = d.PrincipalID;
                r.RoleID = d.RoleID;

                rmembers.Add(r);
            }

            return rmembers;
        }

        protected bool _AddNotice(UUID groupID, UUID noticeID, string fromName, string subject, string message,
            bool hasAttachment, byte attType, string attName, UUID attItemID, string attOwnerID)
        {
            NoticeData data = new NoticeData();
            data.GroupID = groupID;
            data.NoticeID = noticeID;
            data.Data = new Dictionary<string, string>();
            data.Data["FromName"] = fromName;
            data.Data["Subject"] = subject;
            data.Data["Message"] = message;
            data.Data["HasAttachment"] = hasAttachment ? "1" : "0";
            if (hasAttachment)
            {
                data.Data["AttachmentType"] = attType.ToString();
                data.Data["AttachmentName"] = attName;
                data.Data["AttachmentItemID"] = attItemID.ToString();
                data.Data["AttachmentOwnerID"] = attOwnerID;
            }
            data.Data["TMStamp"] = ((uint)Util.UnixTimeSinceEpoch()).ToString();

            return m_Database.StoreNotice(data);
        }

        #endregion

        #region structure translations
        ExtendedGroupRecord _GroupDataToRecord(GroupData data)
        {
            if (data == null)
                return null;

            ExtendedGroupRecord rec = new ExtendedGroupRecord();
            rec.AllowPublish = data.Data["AllowPublish"] == "1" ? true : false;
            rec.Charter = data.Data["Charter"];
            rec.FounderID = new UUID(data.Data["FounderID"]);
            rec.GroupID = data.GroupID;
            rec.GroupName = data.Data["Name"];
            rec.GroupPicture = new UUID(data.Data["InsigniaID"]);
            rec.MaturePublish = data.Data["MaturePublish"] == "1" ? true : false;
            rec.MembershipFee = Int32.Parse(data.Data["MembershipFee"]);
            rec.OpenEnrollment = data.Data["OpenEnrollment"] == "1" ? true : false;
            rec.OwnerRoleID = new UUID(data.Data["OwnerRoleID"]);
            rec.ShowInList = data.Data["ShowInList"] == "1" ? true : false;
            rec.ServiceLocation = data.Data["Location"];
            rec.MemberCount = m_Database.MemberCount(data.GroupID);
            rec.RoleCount = m_Database.RoleCount(data.GroupID);

            return rec;
        }

        GroupNoticeInfo _NoticeDataToInfo(NoticeData data)
        {
            GroupNoticeInfo notice = new GroupNoticeInfo();
            notice.GroupID = data.GroupID;
            notice.Message = data.Data["Message"];
            notice.noticeData = _NoticeDataToData(data);

            return notice;
        }

        ExtendedGroupNoticeData _NoticeDataToData(NoticeData data)
        {
            ExtendedGroupNoticeData notice = new ExtendedGroupNoticeData();
            notice.FromName = data.Data["FromName"];
            notice.NoticeID = data.NoticeID;
            notice.Subject = data.Data["Subject"];
            notice.Timestamp = uint.Parse((string)data.Data["TMStamp"]);
            notice.HasAttachment = data.Data["HasAttachment"] == "1" ? true : false;
            if (notice.HasAttachment)
            {
                notice.AttachmentName = data.Data["AttachmentName"];
                notice.AttachmentItemID = new UUID(data.Data["AttachmentItemID"].ToString());
                notice.AttachmentType = byte.Parse(data.Data["AttachmentType"].ToString());
                notice.AttachmentOwnerID = data.Data["AttachmentOwnerID"].ToString();
            }


            return notice;
        }

        #endregion

        #region permissions
        private bool HasPower(string agentID, UUID groupID, GroupPowers power)
        {
            RoleMembershipData[] rmembership = m_Database.RetrieveMemberRoles(groupID, agentID);
            if (rmembership == null || (rmembership != null && rmembership.Length == 0))
                return false;

            foreach (RoleMembershipData rdata in rmembership)
            {
                RoleData role = m_Database.RetrieveRole(groupID, rdata.RoleID);
                if ( (UInt64.Parse(role.Data["Powers"]) & (ulong)power) != 0 )
                    return true;
            }
            return false;
        }

        private bool IsOwner(string agentID, UUID groupID)
        {
            GroupData group = m_Database.RetrieveGroup(groupID);
            if (group == null)
                return false;

            RoleMembershipData rmembership = m_Database.RetrieveRoleMember(groupID, new UUID(group.Data["OwnerRoleID"]), agentID);
            if (rmembership == null)
                return false;

            return true;
        }
        #endregion

    }
}
