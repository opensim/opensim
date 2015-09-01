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
using OpenSim.Server.Handlers.Base;
using log4net;
using OpenMetaverse;

namespace OpenSim.Groups
{
    public class HGGroupsServiceRobustConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private HGGroupsService m_GroupsService;
        private string m_ConfigName = "Groups";

        // Called by Robust shell
        public HGGroupsServiceRobustConnector(IConfigSource config, IHttpServer server, string configName) :
            this(config, server, configName, null, null)
        {
        }

        // Called by the sim-bound module
        public HGGroupsServiceRobustConnector(IConfigSource config, IHttpServer server, string configName, IOfflineIMService im, IUserAccountService users) :
            base(config, server, configName)
        {
            if (configName != String.Empty)
                m_ConfigName = configName;

            m_log.DebugFormat("[Groups.RobustHGConnector]: Starting with config name {0}", m_ConfigName);

            string homeURI = Util.GetConfigVarFromSections<string>(config, "HomeURI", 
                new string[] { "Startup", "Hypergrid", m_ConfigName}, string.Empty); 
            if (homeURI == string.Empty)
                throw new Exception(String.Format("[Groups.RobustHGConnector]: please provide the HomeURI [Startup] or in section {0}", m_ConfigName));

            IConfig cnf = config.Configs[m_ConfigName];
            if (cnf == null)
                throw new Exception(String.Format("[Groups.RobustHGConnector]: {0} section does not exist", m_ConfigName));

            if (im == null)
            {
                string imDll = cnf.GetString("OfflineIMService", string.Empty);
                if (imDll == string.Empty)
                    throw new Exception(String.Format("[Groups.RobustHGConnector]: please provide OfflineIMService in section {0}", m_ConfigName));

                Object[] args = new Object[] { config };
                im = ServerUtils.LoadPlugin<IOfflineIMService>(imDll, args);
            }

            if (users == null)
            {
                string usersDll = cnf.GetString("UserAccountService", string.Empty);
                if (usersDll == string.Empty)
                    throw new Exception(String.Format("[Groups.RobustHGConnector]: please provide UserAccountService in section {0}", m_ConfigName));

                Object[] args = new Object[] { config };
                users = ServerUtils.LoadPlugin<IUserAccountService>(usersDll, args);
            }

            m_GroupsService = new HGGroupsService(config, im, users, homeURI);

            server.AddStreamHandler(new HGGroupsServicePostHandler(m_GroupsService));
        }

    }

    public class HGGroupsServicePostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private HGGroupsService m_GroupsService;

        public HGGroupsServicePostHandler(HGGroupsService service) :
            base("POST", "/hg-groups")
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

                m_log.DebugFormat("[Groups.RobustHGConnector]: {0}", method);
                switch (method)
                {
                    case "POSTGROUP":
                        return HandleAddGroupProxy(request);
                    case "REMOVEAGENTFROMGROUP":
                        return HandleRemoveAgentFromGroup(request);
                    case "GETGROUP":
                        return HandleGetGroup(request);
                    case "ADDNOTICE":
                        return HandleAddNotice(request);
                    case "VERIFYNOTICE":
                        return HandleVerifyNotice(request);
                    case "GETGROUPMEMBERS":
                        return HandleGetGroupMembers(request);
                    case "GETGROUPROLES":
                        return HandleGetGroupRoles(request);
                    case "GETROLEMEMBERS":
                        return HandleGetRoleMembers(request);

                }
                m_log.DebugFormat("[Groups.RobustHGConnector]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.Error(string.Format("[Groups.RobustHGConnector]: Exception {0} ", e.Message), e);
            }

            return FailureResult();
        }

        byte[] HandleAddGroupProxy(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID")
                || !request.ContainsKey("AgentID")
                || !request.ContainsKey("AccessToken") || !request.ContainsKey("Location"))
                NullResult(result, "Bad network data");

            else
            {
                string RequestingAgentID = request["RequestingAgentID"].ToString();
                string agentID = request["AgentID"].ToString();
                UUID groupID = new UUID(request["GroupID"].ToString());
                string accessToken = request["AccessToken"].ToString();
                string location = request["Location"].ToString();
                string name = string.Empty;
                if (request.ContainsKey("Name"))
                    name = request["Name"].ToString();

                string reason = string.Empty;
                bool success = m_GroupsService.CreateGroupProxy(RequestingAgentID, agentID, accessToken, groupID, location, name, out reason);
                result["REASON"] = reason;
                result["RESULT"] = success.ToString();
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleRemoveAgentFromGroup(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("AccessToken") || !request.ContainsKey("AgentID") ||
                !request.ContainsKey("GroupID"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string agentID = request["AgentID"].ToString();
                string token = request["AccessToken"].ToString();

                if (!m_GroupsService.RemoveAgentFromGroup(agentID, agentID, groupID, token))
                    NullResult(result, "Internal error");
                else
                    result["RESULT"] = "true";
            }

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(ServerUtils.BuildXmlResponse(result));
        }

        byte[] HandleGetGroup(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("AccessToken"))
                NullResult(result, "Bad network data");
            else
            {
                string RequestingAgentID = request["RequestingAgentID"].ToString();
                string token = request["AccessToken"].ToString();

                UUID groupID = UUID.Zero;
                string groupName = string.Empty;

                if (request.ContainsKey("GroupID"))
                    groupID = new UUID(request["GroupID"].ToString());
                if (request.ContainsKey("Name"))
                    groupName = request["Name"].ToString();

                ExtendedGroupRecord grec = m_GroupsService.GetGroupRecord(RequestingAgentID, groupID, groupName, token);
                if (grec == null)
                    NullResult(result, "Group not found");
                else
                    result["RESULT"] = GroupsDataUtils.GroupRecord(grec);
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleGetGroupMembers(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("AccessToken"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string requestingAgentID = request["RequestingAgentID"].ToString();
                string token = request["AccessToken"].ToString();

                List<ExtendedGroupMembersData> members = m_GroupsService.GetGroupMembers(requestingAgentID, groupID, token);
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

        byte[] HandleGetGroupRoles(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("AccessToken"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string requestingAgentID = request["RequestingAgentID"].ToString();
                string token = request["AccessToken"].ToString();

                List<GroupRolesData> roles = m_GroupsService.GetGroupRoles(requestingAgentID, groupID, token);
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

            if (!request.ContainsKey("RequestingAgentID") || !request.ContainsKey("GroupID") || !request.ContainsKey("AccessToken"))
                NullResult(result, "Bad network data");
            else
            {
                UUID groupID = new UUID(request["GroupID"].ToString());
                string requestingAgentID = request["RequestingAgentID"].ToString();
                string token = request["AccessToken"].ToString();

                List<ExtendedGroupRoleMembersData> rmembers = m_GroupsService.GetGroupRoleMembers(requestingAgentID, groupID, token);
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
                    attName = request["AttachmentType"].ToString();
                if (request.ContainsKey("AttachmentItemID"))
                    attItem = new UUID(request["AttachmentItemID"].ToString());
                if (request.ContainsKey("AttachmentOwnerID"))
                    attOwner = request["AttachmentOwnerID"].ToString();

                bool success = m_GroupsService.AddNotice(request["RequestingAgentID"].ToString(), new UUID(request["GroupID"].ToString()),
                        new UUID(request["NoticeID"].ToString()), request["FromName"].ToString(), request["Subject"].ToString(),
                        request["Message"].ToString(), hasAtt, attType, attName, attItem, attOwner);

                result["RESULT"] = success.ToString();
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] HandleVerifyNotice(Dictionary<string, object> request)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (!request.ContainsKey("NoticeID") || !request.ContainsKey("GroupID"))
                NullResult(result, "Bad network data");

            else
            {
                UUID noticeID = new UUID(request["NoticeID"].ToString());
                UUID groupID = new UUID(request["GroupID"].ToString());

                bool success = m_GroupsService.VerifyNotice(noticeID, groupID);
                //m_log.DebugFormat("[XXX]: VerifyNotice returned {0}", success);
                result["RESULT"] = success.ToString();
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[XXX]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        //
        //
        //
        //
        //

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

        #endregion
    }
}
