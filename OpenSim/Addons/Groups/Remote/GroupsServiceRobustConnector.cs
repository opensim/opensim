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
using System.Reflection;
using System.Text;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Server.Handlers.Base;
using log4net;
using OpenMetaverse;

namespace OpenSim.Groups
{
    public class GroupsServiceRobustConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private GroupsService m_GroupsService;
        private string m_ConfigName = "Groups";

        public GroupsServiceRobustConnector(IConfigSource config, IHttpServer server, string configName) :
            base(config, server, configName)
        {
            string key = string.Empty;
            if (configName != String.Empty)
                m_ConfigName = configName;

            m_log.DebugFormat("[Groups.RobustConnector]: Starting with config name {0}", m_ConfigName);

            IConfig groupsConfig = config.Configs[m_ConfigName];
            if (groupsConfig != null)
            {
                key = groupsConfig.GetString("SecretKey", string.Empty);
                m_log.DebugFormat("[Groups.RobustConnector]: Starting with secret key {0}", key);
            }
//            else
//                m_log.DebugFormat("[Groups.RobustConnector]: Unable to find {0} section in configuration", m_ConfigName);

            m_GroupsService = new GroupsService(config);

            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);

            server.AddStreamHandler(new GroupsServicePostHandler(m_GroupsService, auth));
        }
    }

    public class GroupsServicePostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private GroupsService m_GroupsService;

        public GroupsServicePostHandler(GroupsService service, IServiceAuth auth) :
            base("POST", "/groups", auth)
        {
            m_GroupsService = service;
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                string method = request["METHOD"].ToString();
                request.Remove("METHOD");

//                m_log.DebugFormat("[Groups.Handler]: {0}", method);
                switch (method)
                {
                    case "PUTGROUP":
                        return HandleAddOrUpdateGroup(request);
                    case "GETGROUP":
                        return HandleGetGroup(request);
                    case "ADDAGENTTOGROUP":
                        return HandleAddAgentToGroup(request);
                    case "REMOVEAGENTFROMGROUP":
                        return HandleRemoveAgentFromGroup(request);
                    case "GETMEMBERSHIP":
                        return HandleGetMembership(request);
                    case "GETGROUPMEMBERS":
                        return HandleGetGroupMembers(request);
                    case "PUTROLE":
                        return HandlePutRole(request);
                    case "REMOVEROLE":
                        return HandleRemoveRole(request);
                    case "GETGROUPROLES":
                        return HandleGetGroupRoles(request);
                    case "GETROLEMEMBERS":
                        return HandleGetRoleMembers(request);
                    case "AGENTROLE":
                        return HandleAgentRole(request);
                    case "GETAGENTROLES":
                        return HandleGetAgentRoles(request);
                    case "SETACTIVE":
                        return HandleSetActive(request);
                    case "UPDATEMEMBERSHIP":
                        return HandleUpdateMembership(request);
                    case "INVITE":
                        return HandleInvite(request);
                    case "ADDNOTICE":
                        return HandleAddNotice(request);
                    case "GETNOTICES":
                        return HandleGetNotices(request);
                    case "FINDGROUPS":
                        return HandleFindGroups(request);
                }
                m_log.DebugFormat("[GROUPS HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.Error(string.Format("[GROUPS HANDLER]: Exception {0} ", e.Message), e);
            }

            return FailureResult();
        }

        byte[] HandleAddOrUpdateGroup(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            ExtendedGroupRecord grec = GroupsDataUtils.GroupRecord(request);
            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("OP"))
                NullResult(result, "Bad network data");

            else
            {
                string RequestingAgentID = request["RequestingAgentID"].ToString();
                string reason = string.Empty;
                string op = request["OP"].ToString();
                if (op == "ADD")
                {
                    grec.GroupID = m_GroupsService.CreateGroup(RequestingAgentID, grec.GroupName, grec.Charter, grec.ShowInList, grec.GroupPicture, grec.MembershipFee,
                        grec.OpenEnrollment, grec.AllowPublish, grec.MaturePublish, grec.FounderID, out reason);

                }
                else if (op == "UPDATE")
                {
                    m_GroupsService.UpdateGroup(RequestingAgentID, grec.GroupID, grec.Charter, grec.ShowInList, grec.GroupPicture, grec.MembershipFee,
                        grec.OpenEnrollment, grec.AllowPublish, grec.MaturePublish);

                }

                if (grec.GroupID != UUID.Zero)
                {
                    grec = m_GroupsService.GetGroupRecord(RequestingAgentID, grec.GroupID);
                    if (grec == null)
                        NullResult(result, "Internal Error");
                    else
                        result["RESULT"] = GroupsDataUtils.GroupRecord(grec);
                }
                else
                    NullResult(result, reason);
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetGroup(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID"))
                NullResult(result, "Bad network data");
            else
            {
                string RequestingAgentID = request["RequestingAgentID"].ToString();
                ExtendedGroupRecord grec = null;
                if (request.ContainsKey("GroupID"))
                {
                    UUID groupID = new UUID(request["GroupID"].ToString());
                    grec = m_GroupsService.GetGroupRecord(RequestingAgentID, groupID);
                }
                else if (request.ContainsKey("Name"))
                {
                    string name = request["Name"].ToString();
                    grec = m_GroupsService.GetGroupRecord(RequestingAgentID, name);
                }

                if (grec == null)
                    NullResult(result, "Group not found");
                else
                    result["RESULT"] = GroupsDataUtils.GroupRecord(grec);
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleAddAgentToGroup(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("AgentID") ||
                !request.ContainsKey("GroupID") || !request.ContainsKey("RoleID"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                UUID roleID = new UUID(request["RoleID"].ToString());
                string agentID = request["AgentID"].ToString();
                string requestingAgentID = request["RequestingAgentID"].ToString();
                string token = string.Empty;
                string reason = string.Empty;

                if (request.ContainsKey("AccessToken"))
                    token = request["AccessToken"].ToString();

                if (!m_GroupsService.AddAgentToGroup(requestingAgentID, agentID, groupID, roleID, token, out reason))
                    NullResult(result, reason);
                else
                {
                    GroupMembershipData membership = m_GroupsService.GetAgentGroupMembership(requestingAgentID, agentID, groupID);
                    if (membership == null)
                        NullResult(result, "Internal error");
                    else
                        result["RESULT"] = GroupsDataUtils.GroupMembershipData((ExtendedGroupMembershipData)membership);
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleRemoveAgentFromGroup(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("AgentID") || !request.ContainsKey("GroupID"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string agentID = request["AgentID"].ToString();
                string requestingAgentID = request["RequestingAgentID"].ToString();

                if (!m_GroupsService.RemoveAgentFromGroup(requestingAgentID, agentID, groupID))
                    NullResult(result, string.Format("Insufficient permissions. {0}", agentID));
                else
                    result["RESULT"] = "true";
            }

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
        }

        byte[] HandleGetMembership(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("AgentID"))
                NullResult(result, "Bad network data");
            else
            {
                string agentID = request["AgentID"].ToString();
                UUID groupID = UUID.Zero;
                if (request.ContainsKey("GroupID"))
                    groupID = new UUID(request["GroupID"].ToString());
                string requestingAgentID = request["RequestingAgentID"].ToString();
                bool all = request.ContainsKey("ALL");

                if (!all)
                {
                    ExtendedGroupMembershipData membership = null;
                    if (groupID == UUID.Zero)
                    {
                        membership = m_GroupsService.GetAgentActiveMembership(requestingAgentID, agentID);
                    }
                    else
                    {
                        membership = m_GroupsService.GetAgentGroupMembership(requestingAgentID, agentID, groupID);
                    }

                    if (membership == null)
                        NullResult(result, "No such membership");
                    else
                        result["RESULT"] = GroupsDataUtils.GroupMembershipData(membership);
                }
                else
                {
                    List<GroupMembershipData> memberships = m_GroupsService.GetAgentGroupMemberships(requestingAgentID, agentID);
                    if (memberships == null || (memberships != null && memberships.Count == 0))
                    {
                        NullResult(result, "No memberships");
                    }
                    else
                    {
                        Dictionary<string, object> dict = new Dictionary<string, object>();
                        int i = 0;
                        foreach (GroupMembershipData m in memberships)
                            dict["m-" + i++] = GroupsDataUtils.GroupMembershipData((ExtendedGroupMembershipData)m);

                        result["RESULT"] = dict;
                    }
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetGroupMembers(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string requestingAgentID = request["RequestingAgentID"].ToString();

                List<ExtendedGroupMembersData> members = m_GroupsService.GetGroupMembers(requestingAgentID, groupID);
                if (members == null || (members != null && members.Count == 0))
                {
                    NullResult(result, "No members");
                }
                else
                {
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    int i = 0;
                    foreach (ExtendedGroupMembersData m in members)
                    {
                        dict["m-" + i++] = GroupsDataUtils.GroupMembersData(m);
                    }

                    result["RESULT"] = dict;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandlePutRole(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("RoleID") ||
                !request.ContainsKey("Name") || !request.ContainsKey("Description") || !request.ContainsKey("Title") ||
                !request.ContainsKey("Powers") || !request.ContainsKey("OP"))
                NullResult(result, "Bad network data");

            else
            {
                string op = request["OP"].ToString();
                string reason = string.Empty;

                bool success = false;
                if (op == "ADD")
                    success = m_GroupsService.AddGroupRole(request["RequestingAgentID"].ToString(), new UUID(request["GroupID"].ToString()),
                        new UUID(request["RoleID"].ToString()), request["Name"].ToString(), request["Description"].ToString(),
                        request["Title"].ToString(), UInt64.Parse(request["Powers"].ToString()), out reason);

                else if (op == "UPDATE")
                    success = m_GroupsService.UpdateGroupRole(request["RequestingAgentID"].ToString(), new UUID(request["GroupID"].ToString()),
                        new UUID(request["RoleID"].ToString()), request["Name"].ToString(), request["Description"].ToString(),
                        request["Title"].ToString(), UInt64.Parse(request["Powers"].ToString()));

                result["RESULT"] = success.ToString();
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleRemoveRole(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("RoleID"))
                NullResult(result, "Bad network data");

            else
            {
                m_GroupsService.RemoveGroupRole(request["RequestingAgentID"].ToString(), new UUID(request["GroupID"].ToString()),
                    new UUID(request["RoleID"].ToString()));
                result["RESULT"] = "true";
            }

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
        }

        byte[] HandleGetGroupRoles(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string requestingAgentID = request["RequestingAgentID"].ToString();

                List<GroupRolesData> roles = m_GroupsService.GetGroupRoles(requestingAgentID, groupID);
                if (roles == null || (roles != null && roles.Count == 0))
                {
                    NullResult(result, "No members");
                }
                else
                {
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    int i = 0;
                    foreach (GroupRolesData r in roles)
                        dict["r-" + i++] = GroupsDataUtils.GroupRolesData(r);

                    result["RESULT"] = dict;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetRoleMembers(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string requestingAgentID = request["RequestingAgentID"].ToString();

                List<ExtendedGroupRoleMembersData> rmembers = m_GroupsService.GetGroupRoleMembers(requestingAgentID, groupID);
                if (rmembers == null || (rmembers != null && rmembers.Count == 0))
                {
                    NullResult(result, "No members");
                }
                else
                {
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    int i = 0;
                    foreach (ExtendedGroupRoleMembersData rm in rmembers)
                        dict["rm-" + i++] = GroupsDataUtils.GroupRoleMembersData(rm);

                    result["RESULT"] = dict;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleAgentRole(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("RoleID") ||
                !request.ContainsKey("AgentID") || !request.ContainsKey("OP"))
                NullResult(result, "Bad network data");

            else
            {
                string op = request["OP"].ToString();

                bool success = false;
                if (op == "ADD")
                    success = m_GroupsService.AddAgentToGroupRole(request["RequestingAgentID"].ToString(), request["AgentID"].ToString(),
                        new UUID(request["GroupID"].ToString()), new UUID(request["RoleID"].ToString()));

                else if (op == "DELETE")
                    success = m_GroupsService.RemoveAgentFromGroupRole(request["RequestingAgentID"].ToString(), request["AgentID"].ToString(),
                        new UUID(request["GroupID"].ToString()), new UUID(request["RoleID"].ToString()));

                result["RESULT"] = success.ToString();
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetAgentRoles(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("AgentID"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string agentID = request["AgentID"].ToString();
                string requestingAgentID = request["RequestingAgentID"].ToString();

                List<GroupRolesData> roles = m_GroupsService.GetAgentGroupRoles(requestingAgentID, agentID, groupID);
                if (roles == null || (roles != null && roles.Count == 0))
                {
                    NullResult(result, "No members");
                }
                else
                {
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    int i = 0;
                    foreach (GroupRolesData r in roles)
                        dict["r-" + i++] = GroupsDataUtils.GroupRolesData(r);

                    result["RESULT"] = dict;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleSetActive(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") ||
                !request.ContainsKey("AgentID") || !request.ContainsKey("OP"))
            {
                NullResult(result, "Bad network data");
                string xmlString = ServerUtils.BuildXmlResponse(result);
                return Util.UTF8NoBomEncoding.GetBytes(xmlString);
            }
            else
            {
                string op = request["OP"].ToString();

                if (op == "GROUP")
                {
                    ExtendedGroupMembershipData group = m_GroupsService.SetAgentActiveGroup(request["RequestingAgentID"].ToString(),
                        request["AgentID"].ToString(), new UUID(request["GroupID"].ToString()));

                    if (group == null)
                        NullResult(result, "Internal error");
                    else
                        result["RESULT"] = GroupsDataUtils.GroupMembershipData(group);

                    string xmlString = ServerUtils.BuildXmlResponse(result);

                    //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
                    return Util.UTF8NoBomEncoding.GetBytes(xmlString);

                }
                else if (op == "ROLE" && request.ContainsKey("RoleID"))
                {
                    m_GroupsService.SetAgentActiveGroupRole(request["RequestingAgentID"].ToString(), request["AgentID"].ToString(),
                        new UUID(request["GroupID"].ToString()), new UUID(request["RoleID"].ToString()));
                    result["RESULT"] = "true";
                }

                return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
            }

        }

        byte[] HandleUpdateMembership(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("AgentID") || !request.ContainsKey("GroupID") ||
                !request.ContainsKey("AcceptNotices") || !request.ContainsKey("ListInProfile"))
                NullResult(result, "Bad network data");

            else
            {
                m_GroupsService.UpdateMembership(request["RequestingAgentID"].ToString(), request["AgentID"].ToString(), new UUID(request["GroupID"].ToString()),
                    bool.Parse(request["AcceptNotices"].ToString()), bool.Parse(request["ListInProfile"].ToString()));

                result["RESULT"] = "true";
            }

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
        }

        byte[] HandleInvite(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("InviteID"))
            {
                NullResult(result, "Bad network data");
                string xmlString = ServerUtils.BuildXmlResponse(result);
                return Util.UTF8NoBomEncoding.GetBytes(xmlString);
            }
            else
            {
                string op = request["OP"].ToString();

                if (op == "ADD" && request.ContainsKey("GroupID") && request.ContainsKey("RoleID") && request.ContainsKey("AgentID"))
                {
                    bool success = m_GroupsService.AddAgentToGroupInvite(request["RequestingAgentID"].ToString(),
                        new UUID(request["InviteID"].ToString()), new UUID(request["GroupID"].ToString()),
                        new UUID(request["RoleID"].ToString()), request["AgentID"].ToString());

                    result["RESULT"] = success.ToString();
                    return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));

                }
                else if (op == "DELETE")
                {
                    m_GroupsService.RemoveAgentToGroupInvite(request["RequestingAgentID"].ToString(), new UUID(request["InviteID"].ToString()));
                    result["RESULT"] = "true";
                    return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
                }
                else if (op == "GET")
                {
                    GroupInviteInfo invite = m_GroupsService.GetAgentToGroupInvite(request["RequestingAgentID"].ToString(),
                        new UUID(request["InviteID"].ToString()));

                    if (invite != null)
                        result["RESULT"] = GroupsDataUtils.GroupInviteInfo(invite);
                    else
                        result["RESULT"] = "NULL";

                    return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
                }

                NullResult(result, "Bad OP in request");
                return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
            }

        }

        byte[] HandleAddNotice(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("NoticeID") ||
                !request.ContainsKey("FromName") || !request.ContainsKey("Subject") || !request.ContainsKey("Message") ||
                !request.ContainsKey("HasAttachment"))
                NullResult(result, "Bad network data");

            else
            {

                bool hasAtt = bool.Parse(request["HasAttachment"].ToString());
                byte attType = 0;
                string attName = string.Empty;
                string attOwner = string.Empty;
                UUID attItem = UUID.Zero;
                if (request.ContainsKey("AttachmentType"))
                    attType = byte.Parse(request["AttachmentType"].ToString());
                if (request.ContainsKey("AttachmentName"))
                    attName = request["AttachmentName"].ToString();
                if (request.ContainsKey("AttachmentItemID"))
                    attItem = new UUID(request["AttachmentItemID"].ToString());
                if (request.ContainsKey("AttachmentOwnerID"))
                    attOwner = request["AttachmentOwnerID"].ToString();

                bool success = m_GroupsService.AddGroupNotice(request["RequestingAgentID"].ToString(), new UUID(request["GroupID"].ToString()),
                        new UUID(request["NoticeID"].ToString()), request["FromName"].ToString(), request["Subject"].ToString(),
                        request["Message"].ToString(), hasAtt, attType, attName, attItem, attOwner);

                result["RESULT"] = success.ToString();
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetNotices(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID"))
                NullResult(result, "Bad network data");

            else if (request.ContainsKey("NoticeID")) // just one
            {
                GroupNoticeInfo notice =  m_GroupsService.GetGroupNotice(request["RequestingAgentID"].ToString(), new UUID(request["NoticeID"].ToString()));

                if (notice == null)
                    NullResult(result, "NO such notice");
                else
                    result["RESULT"] = GroupsDataUtils.GroupNoticeInfo(notice);

            }
            else if (request.ContainsKey("GroupID")) // all notices for group
            {
                List<ExtendedGroupNoticeData> notices = m_GroupsService.GetGroupNotices(request["RequestingAgentID"].ToString(), new UUID(request["GroupID"].ToString()));

                if (notices == null || (notices != null && notices.Count == 0))
                    NullResult(result, "No notices");
                else
                {
                    Dictionary<string, object> dict = new Dictionary<string, object>();
                    int i = 0;
                    foreach (ExtendedGroupNoticeData n in notices)
                        dict["n-" + i++] = GroupsDataUtils.GroupNoticeData(n);

                    result["RESULT"] = dict;
                }

            }
            else
                NullResult(result, "Bad OP in request");

            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleFindGroups(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("Query"))
                NullResult(result, "Bad network data");

            List<DirGroupsReplyData> hits = m_GroupsService.FindGroups(request["RequestingAgentID"].ToString(), request["Query"].ToString());

            if (hits == null || (hits != null && hits.Count == 0))
                NullResult(result, "No hits");
            else
            {
                Dictionary<string, object> dict = new Dictionary<string, object>();
                int i = 0;
                foreach (DirGroupsReplyData n in hits)
                    dict["n-" + i++] = GroupsDataUtils.DirGroupsReplyData(n);

                result["RESULT"] = dict;
            }


            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }


        #region Helpers

        private void NullResult(Dictionary<string, object> result, string reason)
        {
            result["RESULT"] = "NULL";
            result["REASON"] = reason;
        }

        private byte[] FailureResult()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            NullResult(result, "Unknown method");
            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        private byte[] FailureResult(string reason)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            NullResult(result, reason);
            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }
        #endregion
    }
}
