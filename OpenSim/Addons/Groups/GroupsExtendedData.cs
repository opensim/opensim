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
using System.Runtime.CompilerServices;

using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Groups
{
    public class ExtendedGroupRecord : GroupRecord
    {
        public int MemberCount;
        public int RoleCount;
        public string ServiceLocation;
        public string FounderUUI;
    }

    public class ExtendedGroupMembershipData : GroupMembershipData
    {
        public string AccessToken;
    }

    public class ExtendedGroupMembersData
    {
        // This is the only difference: this is a string
        public string AgentID;
        public int Contribution;
        public string OnlineStatus;
        public ulong AgentPowers;
        public string Title;
        public bool IsOwner;
        public bool ListInProfile;
        public bool AcceptNotices;
        public string AccessToken;
    }

    public class ExtendedGroupRoleMembersData
    {
        public UUID RoleID;
        // This is the only difference: this is a string
        public string MemberID;

    }

    public struct ExtendedGroupNoticeData
    {
        public UUID NoticeID;
        public uint Timestamp;
        public string FromName;
        public string Subject;
        public bool HasAttachment;
        public byte AttachmentType;
        public string AttachmentName;
        public UUID AttachmentItemID;
        public string AttachmentOwnerID;

        public GroupNoticeData ToGroupNoticeData()
        {
            GroupNoticeData n = new GroupNoticeData();
            n.FromName = this.FromName;
            n.AssetType = this.AttachmentType;
            n.HasAttachment = this.HasAttachment;
            n.NoticeID = this.NoticeID;
            n.Subject = this.Subject;
            n.Timestamp = this.Timestamp;

            return n;
        }
    }

    public class GroupsDataUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Sanitize(string s)
        {
            return s ?? string.Empty;
        }

        public static Dictionary<string, object> GroupRecord(ExtendedGroupRecord grec)
        {
            Dictionary<string, object> dict = [];
            if (grec == null)
                return dict;

            dict["AllowPublish"] = grec.AllowPublish.ToString();
            dict["Charter"] = Sanitize(grec.Charter);
            dict["FounderID"] = grec.FounderID.ToString();
            dict["FounderUUI"] = Sanitize(grec.FounderUUI);
            dict["GroupID"] = grec.GroupID.ToString();
            dict["GroupName"] = Sanitize(grec.GroupName);
            dict["InsigniaID"] = grec.GroupPicture.ToString();
            dict["MaturePublish"] = grec.MaturePublish.ToString();
            dict["MembershipFee"] = grec.MembershipFee.ToString();
            dict["OpenEnrollment"] = grec.OpenEnrollment.ToString();
            dict["OwnerRoleID"] = grec.OwnerRoleID.ToString();
            dict["ServiceLocation"] = Sanitize(grec.ServiceLocation);
            dict["ShownInList"] = grec.ShowInList.ToString();
            dict["MemberCount"] =  grec.MemberCount.ToString();
            dict["RoleCount"] = grec.RoleCount.ToString();

            return dict;
        }

        public static ExtendedGroupRecord GroupRecord(Dictionary<string, object> dict)
        {
            if (dict == null)
                return null;

            ExtendedGroupRecord grec = new ExtendedGroupRecord();
            object otmp;
            if (dict.TryGetValue("AllowPublish", out otmp) && otmp != null)
                grec.AllowPublish = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("Charter", out otmp) && otmp != null)
                grec.Charter = otmp.ToString();
            else
                grec.Charter = string.Empty;

            if (dict.TryGetValue("FounderID", out otmp) && otmp != null)
                grec.FounderID = UUID.Parse(dict["FounderID"].ToString());

            if (dict.TryGetValue("FounderUUI", out otmp) && otmp != null)
                grec.FounderUUI = otmp.ToString();
            else
                grec.FounderUUI = string.Empty;

            if (dict.TryGetValue("GroupID", out otmp) && otmp != null)
                grec.GroupID = UUID.Parse(otmp.ToString());

            if (dict.TryGetValue("GroupName", out otmp) && otmp != null)
                grec.GroupName = otmp.ToString();
            else
                grec.GroupName = string.Empty;

            if (dict.TryGetValue("InsigniaID", out otmp) && otmp != null)
                grec.GroupPicture = UUID.Parse(otmp.ToString());

            if (dict.TryGetValue("MaturePublish", out otmp) && otmp != null)
                grec.MaturePublish = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("MembershipFee", out otmp) && otmp != null)
                grec.MembershipFee = int.Parse(otmp.ToString());

            if (dict.TryGetValue("OpenEnrollment", out otmp) && otmp != null)
                grec.OpenEnrollment = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("OwnerRoleID", out otmp) && otmp != null)
                grec.OwnerRoleID = UUID.Parse(otmp.ToString());

            if (dict.TryGetValue("ServiceLocation", out otmp) && otmp != null)
                grec.ServiceLocation = otmp.ToString();
            else
                grec.ServiceLocation = string.Empty;

            if (dict.TryGetValue("ShownInList", out otmp) && otmp != null)
                grec.ShowInList = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("MemberCount", out otmp) && otmp != null)
                grec.MemberCount = int.Parse(otmp.ToString());

            if (dict.TryGetValue("RoleCount", out otmp) && otmp != null)
                grec.RoleCount = int.Parse(otmp.ToString());

            return grec;
        }

        public static Dictionary<string, object> GroupMembershipData(ExtendedGroupMembershipData membership)
        {
            Dictionary<string, object> dict = [];
            if (membership == null)
                return dict;

            dict["AcceptNotices"] = membership.AcceptNotices.ToString();
            dict["AccessToken"] = Sanitize(membership.AccessToken);
            dict["Active"] = membership.Active.ToString();
            dict["ActiveRole"] = membership.ActiveRole.ToString();
            dict["AllowPublish"] = membership.AllowPublish.ToString();
            dict["Charter"] = Sanitize(membership.Charter);
            dict["Contribution"] = membership.Contribution.ToString();
            dict["FounderID"] = membership.FounderID.ToString();
            dict["GroupID"] = membership.GroupID.ToString();
            dict["GroupName"] = Sanitize(membership.GroupName);
            dict["GroupPicture"] = membership.GroupPicture.ToString();
            dict["GroupPowers"] = membership.GroupPowers.ToString();
            dict["GroupTitle"] = Sanitize(membership.GroupTitle);
            dict["ListInProfile"] = membership.ListInProfile.ToString();
            dict["MaturePublish"] = membership.MaturePublish.ToString();
            dict["MembershipFee"] = membership.MembershipFee.ToString();
            dict["OpenEnrollment"] = membership.OpenEnrollment.ToString();
            dict["ShowInList"] = membership.ShowInList.ToString();

            return dict;
        }

        public static ExtendedGroupMembershipData GroupMembershipData(Dictionary<string, object> dict)
        {
            if (dict == null)
                return null;

            ExtendedGroupMembershipData membership = new ExtendedGroupMembershipData();
            object otmp;
            if (dict.TryGetValue("AcceptNotices", out otmp) && otmp != null)
                membership.AcceptNotices = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("AccessToken", out otmp) && otmp != null)
                membership.AccessToken = otmp.ToString();
            else
                membership.AccessToken = string.Empty;

            if (dict.TryGetValue("Active", out otmp) && otmp != null)
                membership.Active = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("ActiveRole", out otmp) && otmp != null)
                membership.ActiveRole = UUID.Parse(otmp.ToString());

            if (dict.TryGetValue("AllowPublish", out otmp) && otmp != null)
                membership.AllowPublish = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("Charter", out otmp) && otmp != null)
                membership.Charter = otmp.ToString();
            else
                membership.Charter = string.Empty;

            if (dict.TryGetValue("Contribution", out otmp) && otmp != null)
                membership.Contribution = int.Parse(otmp.ToString());

            if (dict.TryGetValue("FounderID", out otmp) && otmp != null)
                membership.FounderID = UUID.Parse(otmp.ToString());

            if (dict.TryGetValue("GroupID", out otmp) && otmp != null)
                membership.GroupID = UUID.Parse(otmp.ToString());

            if (dict.TryGetValue("GroupName", out otmp) && otmp != null)
                membership.GroupName = otmp.ToString();
            else
                membership.GroupName = string.Empty;

            if (dict.TryGetValue("GroupPicture", out otmp) && otmp != null)
                membership.GroupPicture = UUID.Parse(otmp.ToString());

            if (dict.TryGetValue("GroupPowers", out otmp) && otmp != null)
                membership.GroupPowers = ulong.Parse(otmp.ToString());

            if (dict.TryGetValue("GroupTitle", out otmp) && otmp != null)
                membership.GroupTitle = otmp.ToString();
            else
                membership.GroupTitle = string.Empty;

            if (dict.TryGetValue("ListInProfile", out otmp) && otmp != null)
                membership.ListInProfile = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("MaturePublish", out otmp) && otmp != null)
                membership.MaturePublish = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("MembershipFee", out otmp) && otmp != null)
                membership.MembershipFee = int.Parse(otmp.ToString());

            if (dict.TryGetValue("OpenEnrollment", out otmp) && otmp != null)
                membership.OpenEnrollment = bool.Parse(otmp.ToString());

            if (dict.TryGetValue("ShowInList", out otmp) && otmp != null)
                membership.ShowInList = bool.Parse(otmp.ToString());

            return membership;
        }

        public static Dictionary<string, object> GroupMembersData(ExtendedGroupMembersData member)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            dict["AcceptNotices"] = member.AcceptNotices.ToString();
            dict["AccessToken"] = Sanitize(member.AccessToken);
            dict["AgentID"] = Sanitize(member.AgentID);
            dict["AgentPowers"] = member.AgentPowers.ToString();
            dict["Contribution"] = member.Contribution.ToString();
            dict["IsOwner"] = member.IsOwner.ToString();
            dict["ListInProfile"] = member.ListInProfile.ToString();
            dict["OnlineStatus"] = Sanitize(member.OnlineStatus);
            dict["Title"] = Sanitize(member.Title);

            return dict;
        }

        public static ExtendedGroupMembersData GroupMembersData(Dictionary<string, object> dict)
        {
            ExtendedGroupMembersData member = new ExtendedGroupMembersData();

            if (dict == null)
                return member;

            object value;
            if (dict.TryGetValue("AcceptNotices", out value) && value != null)
                member.AcceptNotices = bool.Parse(value.ToString());

            if (dict.TryGetValue("AccessToken", out value) && value != null)
                member.AccessToken = value.ToString();
            else
                member.AccessToken = string.Empty;

            if (dict.TryGetValue("AgentID", out value) && value != null)
                member.AgentID = value.ToString();
            else
                member.AgentID = UUID.ZeroString;

            if (dict.TryGetValue("AgentPowers", out value) && value != null)
                member.AgentPowers = ulong.Parse(value.ToString());

            if (dict.TryGetValue("Contribution", out value) && value != null)
                member.Contribution = int.Parse(value.ToString());

            if (dict.TryGetValue("IsOwner", out value) && value != null)
                member.IsOwner = bool.Parse(value.ToString());

            if (dict.TryGetValue("ListInProfile", out value) && value != null)
                member.ListInProfile = bool.Parse(value.ToString());

            if (dict.TryGetValue("OnlineStatus", out value) && value != null)
                member.OnlineStatus = value.ToString();
            else
                member.OnlineStatus = string.Empty;

            if (dict.TryGetValue("Title", out value) && value != null)
                member.Title = value.ToString();
            else
                member.Title = string.Empty;

            return member;
        }

        public static Dictionary<string, object> GroupRolesData(GroupRolesData role)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            dict["Description"] = Sanitize(role.Description);
            dict["Members"] = role.Members.ToString();
            dict["Name"] = Sanitize(role.Name);
            dict["Powers"] = role.Powers.ToString();
            dict["RoleID"] = role.RoleID.ToString();
            dict["Title"] = Sanitize(role.Title);

            return dict;
        }

        public static GroupRolesData GroupRolesData(Dictionary<string, object> dict)
        {
            GroupRolesData role = new GroupRolesData();

            if (dict == null)
                return role;

            object value;
            if (dict.TryGetValue("Description", out value) && value != null)
                role.Description = value.ToString();
            else
                role.Description = string.Empty;

            if (dict.TryGetValue("Members", out value) && value != null)
                role.Members = int.Parse(value.ToString());

            if (dict.TryGetValue("Name", out value) && value != null)
                role.Name = value.ToString();
            else
                role.Name = string.Empty;

            if (dict.TryGetValue("Powers", out value) && value != null)
                role.Powers = ulong.Parse(value.ToString());

            if (dict.TryGetValue("Title", out value) && value != null)
                role.Title = value.ToString();
            else
                role.Title = string.Empty;

            if (dict.TryGetValue("RoleID", out value) && value != null)
                role.RoleID = UUID.Parse(value.ToString());

            return role;
        }

        public static Dictionary<string, object> GroupRoleMembersData(ExtendedGroupRoleMembersData rmember)
        {
            return new Dictionary<string, object>()
                {
                    ["RoleID"] = rmember.RoleID.ToString(),
                    ["MemberID"] = rmember.MemberID
                };
        }

        public static ExtendedGroupRoleMembersData GroupRoleMembersData(Dictionary<string, object> dict)
        {
            ExtendedGroupRoleMembersData rmember = new ExtendedGroupRoleMembersData();

            object value;
            if (dict.TryGetValue("RoleID", out value) && value != null)
                rmember.RoleID = new UUID(value.ToString());

            if (dict.TryGetValue("MemberID", out value) && value != null)
                rmember.MemberID = value.ToString();

            return rmember;
        }

        public static Dictionary<string, object> GroupInviteInfo(GroupInviteInfo invite)
        {
            return new Dictionary<string, object>()
            {
                ["InviteID"] = invite.InviteID.ToString(),
                ["GroupID"] = invite.GroupID.ToString(),
                ["RoleID"] = invite.RoleID.ToString(),
                ["AgentID"] = invite.AgentID
            };
        }

        public static GroupInviteInfo GroupInviteInfo(Dictionary<string, object> dict)
        {
            return dict == null ? null :
                new GroupInviteInfo
                {
                    InviteID = new UUID(dict["InviteID"].ToString()),
                    GroupID = new UUID(dict["GroupID"].ToString()),
                    RoleID = new UUID(dict["RoleID"].ToString()),
                    AgentID = Sanitize(dict["AgentID"].ToString())
                };
        }

        public static Dictionary<string, object> GroupNoticeData(ExtendedGroupNoticeData notice)
        {
            return new Dictionary<string, object>
            {
                ["NoticeID"] = notice.NoticeID.ToString(),
                ["Timestamp"] = notice.Timestamp.ToString(),
                ["FromName"] = Sanitize(notice.FromName),
                ["Subject"] = Sanitize(notice.Subject),
                ["HasAttachment"] = notice.HasAttachment.ToString(),
                ["AttachmentItemID"] = notice.AttachmentItemID.ToString(),
                ["AttachmentName"] = Sanitize(notice.AttachmentName),
                ["AttachmentType"] = notice.AttachmentType.ToString(),
                ["AttachmentOwnerID"] = Sanitize(notice.AttachmentOwnerID)
            };
        }

        public static ExtendedGroupNoticeData GroupNoticeData(Dictionary<string, object> dict)
        {
            return dict == null ? new ExtendedGroupNoticeData() :
                    new ExtendedGroupNoticeData()
                    { 
                        NoticeID = new UUID(dict["NoticeID"].ToString()),
                        Timestamp = uint.Parse(dict["Timestamp"].ToString()),
                        FromName = Sanitize(dict["FromName"].ToString()),
                        Subject = Sanitize(dict["Subject"].ToString()),
                        HasAttachment = bool.Parse(dict["HasAttachment"].ToString()),
                        AttachmentItemID = new UUID(dict["AttachmentItemID"].ToString()),
                        AttachmentName = dict["AttachmentName"].ToString(),
                        AttachmentType = byte.Parse(dict["AttachmentType"].ToString()),
                        AttachmentOwnerID = dict["AttachmentOwnerID"].ToString()
                    };
            }

        public static Dictionary<string, object> GroupNoticeInfo(GroupNoticeInfo notice)
        {
            Dictionary<string, object> dict = GroupNoticeData(notice.noticeData);
            dict["GroupID"] = notice.GroupID.ToString();
            dict["Message"] = Sanitize(notice.Message);

            return dict;
        }

        public static GroupNoticeInfo GroupNoticeInfo(Dictionary<string, object> dict)
        {
            return new GroupNoticeInfo
            {
                noticeData = GroupNoticeData(dict),
                GroupID = new UUID(dict["GroupID"].ToString()),
                Message = Sanitize(dict["Message"].ToString())
            };
        }

        public static Dictionary<string, object> DirGroupsReplyData(DirGroupsReplyData g)
        {
            return new Dictionary<string, object>
            {
                ["GroupID"] = g.groupID,
                ["Name"] = g.groupName,
                ["NMembers"] = g.members,
                ["SearchOrder"] = g.searchOrder
            };
        }

        public static DirGroupsReplyData DirGroupsReplyData(Dictionary<string, object> dict)
        {
            DirGroupsReplyData g;

            g.groupID = new UUID(dict["GroupID"].ToString());
            g.groupName = dict["Name"].ToString();
            int.TryParse(dict["NMembers"].ToString(), out g.members);
            float.TryParse(dict["SearchOrder"].ToString(), out g.searchOrder);

            return g;
        }
    }

}
