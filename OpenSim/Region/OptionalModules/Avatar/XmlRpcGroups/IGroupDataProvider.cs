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

using OpenMetaverse;

using OpenSim.Framework;

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    interface IGroupDataProvider
    {
        UUID CreateGroup(string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish, UUID founderID);
        void UpdateGroup(UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish);
        GroupRecord GetGroupRecord(UUID GroupID, string GroupName);
        List<DirGroupsReplyData> FindGroups(string search);
        List<GroupMembersData> GetGroupMembers(UUID GroupID);

        void AddGroupRole(UUID groupID, UUID roleID, string name, string description, string title, ulong powers);
        void UpdateGroupRole(UUID groupID, UUID roleID, string name, string description, string title, ulong powers);
        void RemoveGroupRole(UUID groupID, UUID roleID);
        List<GroupRolesData> GetGroupRoles(UUID GroupID);
        List<GroupRoleMembersData> GetGroupRoleMembers(UUID GroupID);

        void AddAgentToGroup(UUID AgentID, UUID GroupID, UUID RoleID);
        void RemoveAgentFromGroup(UUID AgentID, UUID GroupID);

        void AddAgentToGroupInvite(UUID inviteID, UUID groupID, UUID roleID, UUID agentID);
        GroupInviteInfo GetAgentToGroupInvite(UUID inviteID);
        void RemoveAgentToGroupInvite(UUID inviteID);


        void AddAgentToGroupRole(UUID AgentID, UUID GroupID, UUID RoleID);
        void RemoveAgentFromGroupRole(UUID AgentID, UUID GroupID, UUID RoleID);
        List<GroupRolesData> GetAgentGroupRoles(UUID AgentID, UUID GroupID);

        void SetAgentActiveGroup(UUID AgentID, UUID GroupID);
        GroupMembershipData GetAgentActiveMembership(UUID AgentID);

        void SetAgentActiveGroupRole(UUID AgentID, UUID GroupID, UUID RoleID);
        void SetAgentGroupInfo(UUID AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile);

        GroupMembershipData GetAgentGroupMembership(UUID AgentID, UUID GroupID);
        List<GroupMembershipData> GetAgentGroupMemberships(UUID AgentID);

        void AddGroupNotice(UUID groupID, UUID noticeID, string fromName, string subject, string message, byte[] binaryBucket);
        GroupNoticeInfo GetGroupNotice(UUID noticeID);
        List<GroupNoticeData> GetGroupNotices(UUID GroupID);
    }

    public class GroupInviteInfo
    {
        public UUID GroupID  = UUID.Zero;
        public UUID RoleID   = UUID.Zero;
        public UUID AgentID  = UUID.Zero;
        public UUID InviteID = UUID.Zero;
    }

}
