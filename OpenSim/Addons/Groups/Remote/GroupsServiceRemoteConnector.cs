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
using System.Linq;
using System.Reflection;
using System.Text;

using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Server.Base;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Groups
{
    public class GroupsServiceRemoteConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI;
        private IServiceAuth m_Auth;
        private object m_Lock = new object();

        public GroupsServiceRemoteConnector(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];
            string url = groupsConfig.GetString("GroupsServerURI", string.Empty);
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                throw new Exception(string.Format("[Groups.RemoteConnector]: Malformed groups server URL {0}. Fix it or disable the Groups feature.", url));

            m_ServerURI = url;
            if (!m_ServerURI.EndsWith("/"))
                m_ServerURI += "/";

            /// This is from BaseServiceConnector
            string authType = Util.GetConfigVarFromSections<string>(config, "AuthType", new string[] { "Network", "Groups" }, "None");

            switch (authType)
            {
                case "BasicHttpAuthentication":
                    m_Auth = new BasicHttpAuthentication(config, "Groups");
                    break;
            }
            ///

            m_log.DebugFormat("[Groups.RemoteConnector]: Groups server at {0}, authentication {1}",
                m_ServerURI, (m_Auth == null ? "None" : m_Auth.GetType().ToString()));
        }

        public ExtendedGroupRecord CreateGroup(string RequestingAgentID, string name, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment,
                                bool allowPublish, bool maturePublish, UUID founderID, out string reason)
        {
            reason = string.Empty;

            ExtendedGroupRecord rec = new ExtendedGroupRecord();
            rec.AllowPublish = allowPublish;
            rec.Charter = charter;
            rec.FounderID = founderID;
            rec.GroupName = name;
            rec.GroupPicture = insigniaID;
            rec.MaturePublish = maturePublish;
            rec.MembershipFee = membershipFee;
            rec.OpenEnrollment = openEnrollment;
            rec.ShowInList = showInList;

            Dictionary<string, object> sendData = GroupsDataUtils.GroupRecord(rec);
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "ADD";
            Dictionary<string, object> ret = MakeRequest("PUTGROUP", sendData);

            if (ret == null)
                return null;

            if (ret["RESULT"].ToString() == "NULL")
            {
                reason = ret["REASON"].ToString();
                return null;
            }

            return GroupsDataUtils.GroupRecord((Dictionary<string, object>)ret["RESULT"]);

        }

        public ExtendedGroupRecord UpdateGroup(string RequestingAgentID, UUID groupID, string charter, bool showInList, UUID insigniaID, int membershipFee, bool openEnrollment, bool allowPublish, bool maturePublish)
        {
            ExtendedGroupRecord rec = new ExtendedGroupRecord();
            rec.AllowPublish = allowPublish;
            rec.Charter = charter;
            rec.GroupPicture = insigniaID;
            rec.MaturePublish = maturePublish;
            rec.GroupID = groupID;
            rec.MembershipFee = membershipFee;
            rec.OpenEnrollment = openEnrollment;
            rec.ShowInList = showInList;

            Dictionary<string, object> sendData = GroupsDataUtils.GroupRecord(rec);
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "UPDATE";
            Dictionary<string, object> ret = MakeRequest("PUTGROUP", sendData);

            if (ret == null || (ret != null && (!ret.ContainsKey("RESULT") || ret["RESULT"].ToString() == "NULL")))
                return null;

            return GroupsDataUtils.GroupRecord((Dictionary<string, object>)ret["RESULT"]);
        }

        public ExtendedGroupRecord GetGroupRecord(string RequestingAgentID, UUID GroupID, string GroupName)
        {
            if (GroupID == UUID.Zero && (GroupName == null || (GroupName != null && GroupName == string.Empty)))
                return null;

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            if (GroupID != UUID.Zero)
                sendData["GroupID"] = GroupID.ToString();
            if (!string.IsNullOrEmpty(GroupName))
                sendData["Name"] = GroupsDataUtils.Sanitize(GroupName);

            sendData["RequestingAgentID"] = RequestingAgentID;

            Dictionary<string, object> ret = MakeRequest("GETGROUP", sendData);

            if (ret == null || (ret != null && (!ret.ContainsKey("RESULT") || ret["RESULT"].ToString() == "NULL")))
                return null;

            return GroupsDataUtils.GroupRecord((Dictionary<string, object>)ret["RESULT"]);
        }

        public List<DirGroupsReplyData> FindGroups(string RequestingAgentID, string query)
        {
            List<DirGroupsReplyData> hits = new List<DirGroupsReplyData>();
            if (string.IsNullOrEmpty(query))
                return hits;

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["Query"] = query;
            sendData["RequestingAgentID"] = RequestingAgentID;

            Dictionary<string, object> ret = MakeRequest("FINDGROUPS", sendData);

            if (ret == null)
                return hits;

            if (!ret.ContainsKey("RESULT"))
                return hits;

            if (ret["RESULT"].ToString() == "NULL")
                return hits;

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                DirGroupsReplyData m = GroupsDataUtils.DirGroupsReplyData((Dictionary<string, object>)v);
                hits.Add(m);
            }

            return hits;
        }

        public GroupMembershipData AddAgentToGroup(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID, string token, out string reason)
        {
            reason = string.Empty;

            Dictionary<string, object> sendData = new Dictionary<string,object>();
            sendData["AgentID"] = AgentID;
            sendData["GroupID"] = GroupID.ToString();
            sendData["RoleID"] = RoleID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["AccessToken"] = token;
            Dictionary<string, object> ret = MakeRequest("ADDAGENTTOGROUP", sendData);

            if (ret == null)
                return null;

            if (!ret.ContainsKey("RESULT"))
                return null;

            if (ret["RESULT"].ToString() == "NULL")
            {
                reason = ret["REASON"].ToString();
                return null;
            }

            return GroupsDataUtils.GroupMembershipData((Dictionary<string, object>)ret["RESULT"]);

        }

        public void RemoveAgentFromGroup(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID;
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            MakeRequest("REMOVEAGENTFROMGROUP", sendData);
        }

        public ExtendedGroupMembershipData GetMembership(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID;
            if (GroupID != UUID.Zero)
                sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            Dictionary<string, object> ret = MakeRequest("GETMEMBERSHIP", sendData);

            if (ret == null)
                return null;

            if (!ret.ContainsKey("RESULT"))
                return null;

            if (ret["RESULT"].ToString() == "NULL")
                return null;

            return GroupsDataUtils.GroupMembershipData((Dictionary<string, object>)ret["RESULT"]);
        }

        public List<GroupMembershipData> GetMemberships(string RequestingAgentID, string AgentID)
        {
            List<GroupMembershipData> memberships = new List<GroupMembershipData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID;
            sendData["ALL"] = "true";
            sendData["RequestingAgentID"] = RequestingAgentID;
            Dictionary<string, object> ret = MakeRequest("GETMEMBERSHIP", sendData);

            if (ret == null)
                return memberships;

            if (!ret.ContainsKey("RESULT"))
                return memberships;

            if (ret["RESULT"].ToString() == "NULL")
                return memberships;

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                GroupMembershipData m = GroupsDataUtils.GroupMembershipData((Dictionary<string, object>)v);
                memberships.Add(m);
            }

            return memberships;
        }

        public List<ExtendedGroupMembersData> GetGroupMembers(string RequestingAgentID, UUID GroupID)
        {
            List<ExtendedGroupMembersData> members = new List<ExtendedGroupMembersData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;

            Dictionary<string, object> ret = MakeRequest("GETGROUPMEMBERS", sendData);

            if (ret == null)
                return members;

            if (!ret.ContainsKey("RESULT"))
                return members;

            if (ret["RESULT"].ToString() == "NULL")
                return members;

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                ExtendedGroupMembersData m = GroupsDataUtils.GroupMembersData((Dictionary<string, object>)v);
                members.Add(m);
            }

            return members;
        }

        public bool AddGroupRole(string RequestingAgentID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers, out string reason)
        {
            reason = string.Empty;

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = groupID.ToString();
            sendData["RoleID"] = roleID.ToString();
            sendData["Name"] = GroupsDataUtils.Sanitize(name);
            sendData["Description"] = GroupsDataUtils.Sanitize(description);
            sendData["Title"] = GroupsDataUtils.Sanitize(title);
            sendData["Powers"] = powers.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "ADD";
            Dictionary<string, object> ret = MakeRequest("PUTROLE", sendData);

            if (ret == null)
                return false;

            if (!ret.ContainsKey("RESULT"))
                return false;

            if (ret["RESULT"].ToString().ToLower() != "true")
            {
                reason = ret["REASON"].ToString();
                return false;
            }

            return true;
        }

        public bool UpdateGroupRole(string RequestingAgentID, UUID groupID, UUID roleID, string name, string description, string title, ulong powers)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = groupID.ToString();
            sendData["RoleID"] = roleID.ToString();
            sendData["Name"] = GroupsDataUtils.Sanitize(name);
            sendData["Description"] = GroupsDataUtils.Sanitize(description);
            sendData["Title"] = GroupsDataUtils.Sanitize(title);
            sendData["Powers"] = powers.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "UPDATE";
            Dictionary<string, object> ret = MakeRequest("PUTROLE", sendData);

            if (ret == null)
                return false;

            if (!ret.ContainsKey("RESULT"))
                return false;

            if (ret["RESULT"].ToString().ToLower() != "true")
                return false;

            return true;
        }

        public void RemoveGroupRole(string RequestingAgentID, UUID groupID, UUID roleID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = groupID.ToString();
            sendData["RoleID"] = roleID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            MakeRequest("REMOVEROLE", sendData);
        }

        public List<GroupRolesData> GetGroupRoles(string RequestingAgentID, UUID GroupID)
        {
            List<GroupRolesData> roles = new List<GroupRolesData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            Dictionary<string, object> ret = MakeRequest("GETGROUPROLES", sendData);

            if (ret == null)
                return roles;

            if (!ret.ContainsKey("RESULT"))
                return roles;

            if (ret["RESULT"].ToString() == "NULL")
                return roles;

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                GroupRolesData m = GroupsDataUtils.GroupRolesData((Dictionary<string, object>)v);
                roles.Add(m);
            }

            return roles;
        }

        public List<ExtendedGroupRoleMembersData> GetGroupRoleMembers(string RequestingAgentID, UUID GroupID)
        {
            List<ExtendedGroupRoleMembersData> rmembers = new List<ExtendedGroupRoleMembersData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            Dictionary<string, object> ret = MakeRequest("GETROLEMEMBERS", sendData);

            if (ret == null)
                return rmembers;

            if (!ret.ContainsKey("RESULT"))
                return rmembers;

            if (ret["RESULT"].ToString() == "NULL")
                return rmembers;

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                ExtendedGroupRoleMembersData m = GroupsDataUtils.GroupRoleMembersData((Dictionary<string, object>)v);
                rmembers.Add(m);
            }

            return rmembers;
        }

        public bool AddAgentToGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID.ToString();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RoleID"] = RoleID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "ADD";

            Dictionary<string, object> ret = MakeRequest("AGENTROLE", sendData);

            if (ret == null)
                return false;

            if (!ret.ContainsKey("RESULT"))
                return false;

            if (ret["RESULT"].ToString().ToLower() != "true")
                return false;

            return true;
        }

        public bool RemoveAgentFromGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID.ToString();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RoleID"] = RoleID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "DELETE";

            Dictionary<string, object> ret = MakeRequest("AGENTROLE", sendData);

            if (ret == null)
                return false;

            if (!ret.ContainsKey("RESULT"))
                return false;

            if (ret["RESULT"].ToString().ToLower() != "true")
                return false;

            return true;
        }

        public List<GroupRolesData> GetAgentGroupRoles(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            List<GroupRolesData> roles = new List<GroupRolesData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID.ToString();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            Dictionary<string, object> ret = MakeRequest("GETAGENTROLES", sendData);

            if (ret == null)
                return roles;

            if (!ret.ContainsKey("RESULT"))
                return roles;

            if (ret["RESULT"].ToString() == "NULL")
                return roles;

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                GroupRolesData m = GroupsDataUtils.GroupRolesData((Dictionary<string, object>)v);
                roles.Add(m);
            }

            return roles;
        }

        public GroupMembershipData SetAgentActiveGroup(string RequestingAgentID, string AgentID, UUID GroupID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID.ToString();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "GROUP";

            Dictionary<string, object> ret = MakeRequest("SETACTIVE", sendData);

            if (ret == null)
                return null;

            if (!ret.ContainsKey("RESULT"))
                return null;

            if (ret["RESULT"].ToString() == "NULL")
                return null;

            return GroupsDataUtils.GroupMembershipData((Dictionary<string, object>)ret["RESULT"]);
        }

        public void SetAgentActiveGroupRole(string RequestingAgentID, string AgentID, UUID GroupID, UUID RoleID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID.ToString();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RoleID"] = RoleID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "ROLE";

            MakeRequest("SETACTIVE", sendData);
        }

        public void UpdateMembership(string RequestingAgentID, string AgentID, UUID GroupID, bool AcceptNotices, bool ListInProfile)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID.ToString();
            sendData["GroupID"] = GroupID.ToString();
            sendData["AcceptNotices"] = AcceptNotices.ToString();
            sendData["ListInProfile"] = ListInProfile.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            MakeRequest("UPDATEMEMBERSHIP", sendData);
        }

        public bool AddAgentToGroupInvite(string RequestingAgentID, UUID inviteID, UUID groupID, UUID roleID, string agentID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["InviteID"] = inviteID.ToString();
            sendData["GroupID"] = groupID.ToString();
            sendData["RoleID"] = roleID.ToString();
            sendData["AgentID"] = agentID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "ADD";

            Dictionary<string, object> ret = MakeRequest("INVITE", sendData);

            if (ret == null)
                return false;

            if (!ret.ContainsKey("RESULT"))
                return false;

            if (ret["RESULT"].ToString().ToLower() != "true") // it may return "NULL"
                return false;

            return true;
        }

        public GroupInviteInfo GetAgentToGroupInvite(string RequestingAgentID, UUID inviteID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["InviteID"] = inviteID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "GET";

            Dictionary<string, object> ret = MakeRequest("INVITE", sendData);

            if (ret == null)
                return null;

            if (!ret.ContainsKey("RESULT"))
                return null;

            if (ret["RESULT"].ToString() == "NULL")
                return null;

            return GroupsDataUtils.GroupInviteInfo((Dictionary<string, object>)ret["RESULT"]);
        }

        public void RemoveAgentToGroupInvite(string RequestingAgentID, UUID inviteID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["InviteID"] = inviteID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["OP"] = "DELETE";

            MakeRequest("INVITE", sendData);
        }

        public bool AddGroupNotice(string RequestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message,
            bool hasAttachment, byte attType, string attName, UUID attItemID, string attOwnerID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = groupID.ToString();
            sendData["NoticeID"] = noticeID.ToString();
            sendData["FromName"] = GroupsDataUtils.Sanitize(fromName);
            sendData["Subject"] = GroupsDataUtils.Sanitize(subject);
            sendData["Message"] = GroupsDataUtils.Sanitize(message);
            sendData["HasAttachment"] = hasAttachment.ToString();
            if (hasAttachment)
            {
                sendData["AttachmentType"] = attType.ToString();
                sendData["AttachmentName"] = attName.ToString();
                sendData["AttachmentItemID"] = attItemID.ToString();
                sendData["AttachmentOwnerID"] = attOwnerID;
            }
            sendData["RequestingAgentID"] = RequestingAgentID;

            Dictionary<string, object> ret = MakeRequest("ADDNOTICE", sendData);

            if (ret == null)
                return false;

            if (!ret.ContainsKey("RESULT"))
                return false;

            if (ret["RESULT"].ToString().ToLower() != "true")
                return false;

            return true;
        }

        public GroupNoticeInfo GetGroupNotice(string RequestingAgentID, UUID noticeID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["NoticeID"] = noticeID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;

            Dictionary<string, object> ret = MakeRequest("GETNOTICES", sendData);

            if (ret == null)
                return null;

            if (!ret.ContainsKey("RESULT"))
                return null;

            if (ret["RESULT"].ToString() == "NULL")
                return null;

            return GroupsDataUtils.GroupNoticeInfo((Dictionary<string, object>)ret["RESULT"]);
        }

        public List<ExtendedGroupNoticeData> GetGroupNotices(string RequestingAgentID, UUID GroupID)
        {
            List<ExtendedGroupNoticeData> notices = new List<ExtendedGroupNoticeData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            Dictionary<string, object> ret = MakeRequest("GETNOTICES", sendData);

            if (ret == null)
                return notices;

            if (!ret.ContainsKey("RESULT"))
                return notices;

            if (ret["RESULT"].ToString() == "NULL")
                return notices;

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                ExtendedGroupNoticeData m = GroupsDataUtils.GroupNoticeData((Dictionary<string, object>)v);
                notices.Add(m);
            }

            return notices;
        }

        #region Make Request

        private Dictionary<string, object> MakeRequest(string method, Dictionary<string, object> sendData)
        {
            sendData["METHOD"] = method;

            string reply = string.Empty;
            lock (m_Lock)
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                         m_ServerURI + "groups",
                         ServerUtils.BuildQueryString(sendData),
                         m_Auth);

            if (reply == string.Empty)
                return null;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(
                    reply);

            return replyData;
        }

        #endregion
    }
}