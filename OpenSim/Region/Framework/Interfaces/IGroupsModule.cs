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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate void NewGroupNotice(UUID groupID, UUID noticeID);

    public interface IGroupsModule
    {
        event NewGroupNotice OnNewGroupNotice;

        /// <summary>
        /// Create a group
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="name"></param>
        /// <param name="charter"></param>
        /// <param name="showInList"></param>
        /// <param name="insigniaID"></param>
        /// <param name="membershipFee"></param>
        /// <param name="openEnrollment"></param>
        /// <param name="allowPublish"></param>
        /// <param name="maturePublish"></param>
        /// <returns>The UUID of the created group</returns>
        UUID CreateGroup(
            IClientAPI remoteClient, string name, string charter, bool showInList, UUID insigniaID, int membershipFee,
            bool openEnrollment, bool allowPublish, bool maturePublish);

        /// <summary>
        /// Get a group
        /// </summary>
        /// <param name="name">Name of the group</param>
        /// <returns>The group's data.  Null if there is no such group.</returns>
        GroupRecord GetGroupRecord(string name);

        /// <summary>
        /// Get a group
        /// </summary>
        /// <param name="GroupID">ID of the group</param>
        /// <returns>The group's data.  Null if there is no such group.</returns>
        GroupRecord GetGroupRecord(UUID GroupID);

        void ActivateGroup(IClientAPI remoteClient, UUID groupID);
        List<GroupTitlesData> GroupTitlesRequest(IClientAPI remoteClient, UUID groupID);
        List<GroupMembersData> GroupMembersRequest(IClientAPI remoteClient, UUID groupID);
        List<GroupRolesData> GroupRoleDataRequest(IClientAPI remoteClient, UUID groupID);
        List<GroupRoleMembersData> GroupRoleMembersRequest(IClientAPI remoteClient, UUID groupID);
        GroupProfileData GroupProfileRequest(IClientAPI remoteClient, UUID groupID);
        GroupMembershipData[] GetMembershipData(UUID UserID);
        GroupMembershipData GetMembershipData(UUID GroupID, UUID UserID);
        GroupMembershipData GetActiveMembershipData(UUID UserID);

        void UpdateGroupInfo(IClientAPI remoteClient, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish);

        void SetGroupAcceptNotices(IClientAPI remoteClient, UUID groupID, bool acceptNotices, bool listInProfile);

        void GroupTitleUpdate(IClientAPI remoteClient, UUID GroupID, UUID TitleRoleID);

        GroupNoticeData[] GroupNoticesListRequest(IClientAPI remoteClient, UUID GroupID);
        string GetGroupTitle(UUID avatarID);
        void GroupRoleUpdate(IClientAPI remoteClient, UUID GroupID, UUID RoleID, string name, string description, string title, ulong powers, byte updateType);
        void GroupRoleChanges(IClientAPI remoteClient, UUID GroupID, UUID RoleID, UUID MemberID, uint changes);
        void GroupNoticeRequest(IClientAPI remoteClient, UUID groupNoticeID);
        GridInstantMessage CreateGroupNoticeIM(UUID agentID, UUID groupNoticeID, byte dialog);
        void SendAgentGroupDataUpdate(IClientAPI remoteClient);
        void JoinGroupRequest(IClientAPI remoteClient, UUID GroupID);
        void LeaveGroupRequest(IClientAPI remoteClient, UUID GroupID);
        void EjectGroupMemberRequest(IClientAPI remoteClient, UUID GroupID, UUID EjecteeID);
        void EjectGroupMember(IClientAPI remoteClient, UUID agentID, UUID GroupID, UUID EjecteeID);
        void InviteGroupRequest(IClientAPI remoteClient, UUID GroupID, UUID InviteeID, UUID RoleID);
        void InviteGroup(IClientAPI remoteClient, UUID agentID, UUID GroupID, UUID InviteeID, UUID RoleID);
        void NotifyChange(UUID GroupID);

        List<DirGroupsReplyData> FindGroups(IClientAPI remoteClient, string query);
    }
}