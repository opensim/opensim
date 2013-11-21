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
using OpenSim.Server.Base;

using OpenMetaverse;
using log4net;

namespace OpenSim.Groups
{
    public class GroupsServiceHGConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI;
        private object m_Lock = new object();

        public GroupsServiceHGConnector(string url)
        {
            m_ServerURI = url;
            if (!m_ServerURI.EndsWith("/"))
                m_ServerURI += "/";

            m_log.DebugFormat("[Groups.HGConnector]: Groups server at {0}", m_ServerURI);
        }

        public bool CreateProxy(string RequestingAgentID, string AgentID, string accessToken, UUID groupID, string url, string name, out string reason)
        {
            reason = string.Empty;

            Dictionary<string, object> sendData = new Dictionary<string,object>();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["AgentID"] = AgentID.ToString();
            sendData["AccessToken"] = accessToken;
            sendData["GroupID"] = groupID.ToString();
            sendData["Location"] = url;
            sendData["Name"] = name;
            Dictionary<string, object> ret = MakeRequest("POSTGROUP", sendData);

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

        public void RemoveAgentFromGroup(string AgentID, UUID GroupID, string token)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["AgentID"] = AgentID;
            sendData["GroupID"] = GroupID.ToString();
            sendData["AccessToken"] = GroupsDataUtils.Sanitize(token);
            MakeRequest("REMOVEAGENTFROMGROUP", sendData);
        }

        public ExtendedGroupRecord GetGroupRecord(string RequestingAgentID, UUID GroupID, string GroupName, string token)
        {
            if (GroupID == UUID.Zero && (GroupName == null || (GroupName != null && GroupName == string.Empty)))
                return null;

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            if (GroupID != UUID.Zero)
                sendData["GroupID"] = GroupID.ToString();
            if (!string.IsNullOrEmpty(GroupName))
                sendData["Name"] = GroupsDataUtils.Sanitize(GroupName);

            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["AccessToken"] = GroupsDataUtils.Sanitize(token);

            Dictionary<string, object> ret = MakeRequest("GETGROUP", sendData);

            if (ret == null)
                return null;

            if (!ret.ContainsKey("RESULT"))
                return null;

            if (ret["RESULT"].ToString() == "NULL")
                return null;

            return GroupsDataUtils.GroupRecord((Dictionary<string, object>)ret["RESULT"]);
        }

        public List<ExtendedGroupMembersData> GetGroupMembers(string RequestingAgentID, UUID GroupID, string token)
        {
            List<ExtendedGroupMembersData> members = new List<ExtendedGroupMembersData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["AccessToken"] = GroupsDataUtils.Sanitize(token);
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

        public List<GroupRolesData> GetGroupRoles(string RequestingAgentID, UUID GroupID, string token)
        {
            List<GroupRolesData> roles = new List<GroupRolesData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["AccessToken"] = GroupsDataUtils.Sanitize(token);
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

        public List<ExtendedGroupRoleMembersData> GetGroupRoleMembers(string RequestingAgentID, UUID GroupID, string token)
        {
            List<ExtendedGroupRoleMembersData> rmembers = new List<ExtendedGroupRoleMembersData>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["GroupID"] = GroupID.ToString();
            sendData["RequestingAgentID"] = RequestingAgentID;
            sendData["AccessToken"] = GroupsDataUtils.Sanitize(token);
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

        public bool AddNotice(string RequestingAgentID, UUID groupID, UUID noticeID, string fromName, string subject, string message,
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

        public bool VerifyNotice(UUID noticeID, UUID groupID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["NoticeID"] = noticeID.ToString();
            sendData["GroupID"] = groupID.ToString();
            Dictionary<string, object> ret = MakeRequest("VERIFYNOTICE", sendData);

            if (ret == null)
                return false;

            if (!ret.ContainsKey("RESULT"))
                return false;

            if (ret["RESULT"].ToString().ToLower() != "true")
                return false;

            return true;
        }

        //
        //
        //
        //
        //

        #region Make Request

        private Dictionary<string, object> MakeRequest(string method, Dictionary<string, object> sendData)
        {
            sendData["METHOD"] = method;

            string reply = string.Empty;
            lock (m_Lock)
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                         m_ServerURI + "hg-groups",
                         ServerUtils.BuildQueryString(sendData));

            //m_log.DebugFormat("[XXX]: reply was {0}", reply);

            if (string.IsNullOrEmpty(reply))
                return null;

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(
                    reply);

            return replyData;
        }
        #endregion

    }
}
