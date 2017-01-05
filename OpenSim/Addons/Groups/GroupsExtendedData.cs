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
        public static string Sanitize(string s)
        {
            return s == null ? string.Empty : s;
        }

        public static Dictionary<string, object> GroupRecord(ExtendedGroupRecord grec)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
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
            if (dict.ContainsKey("AllowPublish") && dict["AllowPublish"] != null)
                grec.AllowPublish = bool.Parse(dict["AllowPublish"].ToString());

            if (dict.ContainsKey("Charter") && dict["Charter"] != null)
                grec.Charter = dict["Charter"].ToString();
            else
                grec.Charter = string.Empty;

            if (dict.ContainsKey("FounderID") && dict["FounderID"] != null)
                grec.FounderID = UUID.Parse(dict["FounderID"].ToString());

            if (dict.ContainsKey("FounderUUI") && dict["FounderUUI"] != null)
                grec.FounderUUI = dict["FounderUUI"].ToString();
            else
                grec.FounderUUI = string.Empty;

            if (dict.ContainsKey("GroupID") && dict["GroupID"] != null)
                grec.GroupID = UUID.Parse(dict["GroupID"].ToString());

            if (dict.ContainsKey("GroupName") && dict["GroupName"] != null)
                grec.GroupName = dict["GroupName"].ToString();
            else
                grec.GroupName = string.Empty;

            if (dict.ContainsKey("InsigniaID") && dict["InsigniaID"] != null)
                grec.GroupPicture = UUID.Parse(dict["InsigniaID"].ToString());

            if (dict.ContainsKey("MaturePublish") && dict["MaturePublish"] != null)
                grec.MaturePublish = bool.Parse(dict["MaturePublish"].ToString());

            if (dict.ContainsKey("MembershipFee") && dict["MembershipFee"] != null)
                grec.MembershipFee = Int32.Parse(dict["MembershipFee"].ToString());

            if (dict.ContainsKey("OpenEnrollment") && dict["OpenEnrollment"] != null)
                grec.OpenEnrollment = bool.Parse(dict["OpenEnrollment"].ToString());

            if (dict.ContainsKey("OwnerRoleID") && dict["OwnerRoleID"] != null)
                grec.OwnerRoleID = UUID.Parse(dict["OwnerRoleID"].ToString());

            if (dict.ContainsKey("ServiceLocation") && dict["ServiceLocation"] != null)
                grec.ServiceLocation = dict["ServiceLocation"].ToString();
            else
                grec.ServiceLocation = string.Empty;

            if (dict.ContainsKey("ShownInList") && dict["ShownInList"] != null)
                grec.ShowInList = bool.Parse(dict["ShownInList"].ToString());

            if (dict.ContainsKey("MemberCount") && dict["MemberCount"] != null)
                grec.MemberCount = Int32.Parse(dict["MemberCount"].ToString());

            if (dict.ContainsKey("RoleCount") && dict["RoleCount"] != null)
                grec.RoleCount = Int32.Parse(dict["RoleCount"].ToString());

            return grec;
        }

        public static Dictionary<string, object> GroupMembershipData(ExtendedGroupMembershipData membership)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
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

            if (dict.ContainsKey("AcceptNotices") && dict["AcceptNotices"] != null)
                membership.AcceptNotices = bool.Parse(dict["AcceptNotices"].ToString());

            if (dict.ContainsKey("AccessToken") && dict["AccessToken"] != null)
                membership.AccessToken = dict["AccessToken"].ToString();
            else
                membership.AccessToken = string.Empty;

            if (dict.ContainsKey("Active") && dict["Active"] != null)
                membership.Active = bool.Parse(dict["Active"].ToString());

            if (dict.ContainsKey("ActiveRole") && dict["ActiveRole"] != null)
                membership.ActiveRole = UUID.Parse(dict["ActiveRole"].ToString());

            if (dict.ContainsKey("AllowPublish") && dict["AllowPublish"] != null)
                membership.AllowPublish = bool.Parse(dict["AllowPublish"].ToString());

            if (dict.ContainsKey("Charter") && dict["Charter"] != null)
                membership.Charter = dict["Charter"].ToString();
            else
                membership.Charter = string.Empty;

            if (dict.ContainsKey("Contribution") && dict["Contribution"] != null)
                membership.Contribution = Int32.Parse(dict["Contribution"].ToString());

            if (dict.ContainsKey("FounderID") && dict["FounderID"] != null)
                membership.FounderID = UUID.Parse(dict["FounderID"].ToString());

            if (dict.ContainsKey("GroupID") && dict["GroupID"] != null)
                membership.GroupID = UUID.Parse(dict["GroupID"].ToString());

            if (dict.ContainsKey("GroupName") && dict["GroupName"] != null)
                membership.GroupName = dict["GroupName"].ToString();
            else
                membership.GroupName = string.Empty;

            if (dict.ContainsKey("GroupPicture") && dict["GroupPicture"] != null)
                membership.GroupPicture = UUID.Parse(dict["GroupPicture"].ToString());

            if (dict.ContainsKey("GroupPowers") && dict["GroupPowers"] != null)
                membership.GroupPowers = UInt64.Parse(dict["GroupPowers"].ToString());

            if (dict.ContainsKey("GroupTitle") && dict["GroupTitle"] != null)
                membership.GroupTitle = dict["GroupTitle"].ToString();
            else
                membership.GroupTitle = string.Empty;

            if (dict.ContainsKey("ListInProfile") && dict["ListInProfile"] != null)
                membership.ListInProfile = bool.Parse(dict["ListInProfile"].ToString());

            if (dict.ContainsKey("MaturePublish") && dict["MaturePublish"] != null)
                membership.MaturePublish = bool.Parse(dict["MaturePublish"].ToString());

            if (dict.ContainsKey("MembershipFee") && dict["MembershipFee"] != null)
                membership.MembershipFee = Int32.Parse(dict["MembershipFee"].ToString());

            if (dict.ContainsKey("OpenEnrollment") && dict["OpenEnrollment"] != null)
                membership.OpenEnrollment = bool.Parse(dict["OpenEnrollment"].ToString());

            if (dict.ContainsKey("ShowInList") && dict["ShowInList"] != null)
                membership.ShowInList = bool.Parse(dict["ShowInList"].ToString());

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

            if (dict.ContainsKey("AcceptNotices") && dict["AcceptNotices"] != null)
                member.AcceptNotices = bool.Parse(dict["AcceptNotices"].ToString());

            if (dict.ContainsKey("AccessToken") && dict["AccessToken"] != null)
                member.AccessToken = Sanitize(dict["AccessToken"].ToString());
            else
                member.AccessToken = string.Empty;

            if (dict.ContainsKey("AgentID") && dict["AgentID"] != null)
                member.AgentID = Sanitize(dict["AgentID"].ToString());
            else
                member.AgentID = UUID.Zero.ToString();

            if (dict.ContainsKey("AgentPowers") && dict["AgentPowers"] != null)
                member.AgentPowers = UInt64.Parse(dict["AgentPowers"].ToString());

            if (dict.ContainsKey("Contribution") && dict["Contribution"] != null)
                member.Contribution = Int32.Parse(dict["Contribution"].ToString());

            if (dict.ContainsKey("IsOwner") && dict["IsOwner"] != null)
                member.IsOwner = bool.Parse(dict["IsOwner"].ToString());

            if (dict.ContainsKey("ListInProfile") && dict["ListInProfile"] != null)
                member.ListInProfile = bool.Parse(dict["ListInProfile"].ToString());

            if (dict.ContainsKey("OnlineStatus") && dict["OnlineStatus"] != null)
                member.OnlineStatus = Sanitize(dict["OnlineStatus"].ToString());
            else
                member.OnlineStatus = string.Empty;

            if (dict.ContainsKey("Title") && dict["Title"] != null)
                member.Title = Sanitize(dict["Title"].ToString());
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

            if (dict.ContainsKey("Description") && dict["Description"] != null)
                role.Description = Sanitize(dict["Description"].ToString());
            else
                role.Description = string.Empty;

            if (dict.ContainsKey("Members") && dict["Members"] != null)
                role.Members = Int32.Parse(dict["Members"].ToString());

            if (dict.ContainsKey("Name") && dict["Name"] != null)
                role.Name = Sanitize(dict["Name"].ToString());
            else
                role.Name = string.Empty;

            if (dict.ContainsKey("Powers") && dict["Powers"] != null)
                role.Powers = UInt64.Parse(dict["Powers"].ToString());

            if (dict.ContainsKey("Title") && dict["Title"] != null)
                role.Title = Sanitize(dict["Title"].ToString());
            else
                role.Title = string.Empty;

            if (dict.ContainsKey("RoleID") && dict["RoleID"] != null)
                role.RoleID = UUID.Parse(dict["RoleID"].ToString());

            return role;
        }

        public static Dictionary<string, object> GroupRoleMembersData(ExtendedGroupRoleMembersData rmember)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            dict["RoleID"] = rmember.RoleID.ToString();
            dict["MemberID"] = rmember.MemberID;
            return dict;
        }

        public static ExtendedGroupRoleMembersData GroupRoleMembersData(Dictionary<string, object> dict)
        {
            ExtendedGroupRoleMembersData rmember = new ExtendedGroupRoleMembersData();

            if (dict.ContainsKey("RoleID") && dict["RoleID"] != null)
                rmember.RoleID = new UUID(dict["RoleID"].ToString());

            if (dict.ContainsKey("MemberID") && dict["MemberID"] != null)
                rmember.MemberID = dict["MemberID"].ToString();

            return rmember;
        }

        public static Dictionary<string, object> GroupInviteInfo(GroupInviteInfo invite)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            dict["InviteID"] = invite.InviteID.ToString();
            dict["GroupID"] = invite.GroupID.ToString();
            dict["RoleID"] = invite.RoleID.ToString();
            dict["AgentID"] = invite.AgentID;

            return dict;
        }

        public static GroupInviteInfo GroupInviteInfo(Dictionary<string, object> dict)
        {
            if (dict == null)
                return null;

            GroupInviteInfo invite = new GroupInviteInfo();

            invite.InviteID = new UUID(dict["InviteID"].ToString());
            invite.GroupID = new UUID(dict["GroupID"].ToString());
            invite.RoleID = new UUID(dict["RoleID"].ToString());
            invite.AgentID = Sanitize(dict["AgentID"].ToString());

            return invite;
        }

        public static Dictionary<string, object> GroupNoticeData(ExtendedGroupNoticeData notice)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            dict["NoticeID"] = notice.NoticeID.ToString();
            dict["Timestamp"] = notice.Timestamp.ToString();
            dict["FromName"] = Sanitize(notice.FromName);
            dict["Subject"] = Sanitize(notice.Subject);
            dict["HasAttachment"] = notice.HasAttachment.ToString();
            dict["AttachmentItemID"] = notice.AttachmentItemID.ToString();
            dict["AttachmentName"] = Sanitize(notice.AttachmentName);
            dict["AttachmentType"] = notice.AttachmentType.ToString();
            dict["AttachmentOwnerID"] = Sanitize(notice.AttachmentOwnerID);

            return dict;
        }

        public static ExtendedGroupNoticeData GroupNoticeData(Dictionary<string, object> dict)
        {
            ExtendedGroupNoticeData notice = new ExtendedGroupNoticeData();

            if (dict == null)
                return notice;

            notice.NoticeID = new UUID(dict["NoticeID"].ToString());
            notice.Timestamp = UInt32.Parse(dict["Timestamp"].ToString());
            notice.FromName = Sanitize(dict["FromName"].ToString());
            notice.Subject = Sanitize(dict["Subject"].ToString());
            notice.HasAttachment = bool.Parse(dict["HasAttachment"].ToString());
            notice.AttachmentItemID = new UUID(dict["AttachmentItemID"].ToString());
            notice.AttachmentName = dict["AttachmentName"].ToString();
            notice.AttachmentType = byte.Parse(dict["AttachmentType"].ToString());
            notice.AttachmentOwnerID = dict["AttachmentOwnerID"].ToString();

            return notice;
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
            GroupNoticeInfo notice = new GroupNoticeInfo();

            notice.noticeData = GroupNoticeData(dict);
            notice.GroupID = new UUID(dict["GroupID"].ToString());
            notice.Message = Sanitize(dict["Message"].ToString());

            return notice;
        }

        public static Dictionary<string, object> DirGroupsReplyData(DirGroupsReplyData g)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();

            dict["GroupID"] = g.groupID;
            dict["Name"] = g.groupName;
            dict["NMembers"] = g.members;
            dict["SearchOrder"] = g.searchOrder;

            return dict;
        }

        public static DirGroupsReplyData DirGroupsReplyData(Dictionary<string, object> dict)
        {
            DirGroupsReplyData g;

            g.groupID = new UUID(dict["GroupID"].ToString());
            g.groupName = dict["Name"].ToString();
            Int32.TryParse(dict["NMembers"].ToString(), out g.members);
            float.TryParse(dict["SearchOrder"].ToString(), out g.searchOrder);

            return g;
        }
    }

}
