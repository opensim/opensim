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

using Nini.Config;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HGFriendsServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IUserAgentService m_UserAgentService;
        private IFriendsSimConnector m_FriendsLocalSimConnector;
        private IHGFriendsService m_TheService;

        public HGFriendsServerPostHandler(IHGFriendsService service, IUserAgentService uas, IFriendsSimConnector friendsConn) :
                base("POST", "/hgfriends")
        {
            m_TheService = service;
            m_UserAgentService = uas;
            m_FriendsLocalSimConnector = friendsConn;

            m_log.DebugFormat("[HGFRIENDS HANDLER]: HGFriendsServerPostHandler is On ({0})",
                (m_FriendsLocalSimConnector == null ? "robust" : "standalone"));

            if (m_TheService == null)
                m_log.ErrorFormat("[HGFRIENDS HANDLER]: TheService is null!");
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

                switch (method)
                {
                    case "getfriendperms":
                        return GetFriendPerms(request);

                    case "newfriendship":
                        return NewFriendship(request);

                    case "deletefriendship":
                        return DeleteFriendship(request);

                        /* Same as inter-sim */
                    case "friendship_offered":
                        return FriendshipOffered(request);

                    case "validate_friendship_offered":
                        return ValidateFriendshipOffered(request);

                    case "statusnotification":
                        return StatusNotification(request);
                    /*
                    case "friendship_approved":
                        return FriendshipApproved(request);

                    case "friendship_denied":
                        return FriendshipDenied(request);

                    case "friendship_terminated":
                        return FriendshipTerminated(request);

                    case "grant_rights":
                        return GrantRights(request);
                        */
                }

                m_log.DebugFormat("[HGFRIENDS HANDLER]: unknown method {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HGFRIENDS HANDLER]: Exception {0}", e);
            }

            return FailureResult();
        }

        #region Method-specific handlers

        byte[] GetFriendPerms(Dictionary<string, object> request)
        {
            if (!VerifyServiceKey(request))
                return FailureResult();

            UUID principalID = UUID.Zero;
            if (request.ContainsKey("PRINCIPALID"))
                UUID.TryParse(request["PRINCIPALID"].ToString(), out principalID);
            else
            {
                m_log.WarnFormat("[HGFRIENDS HANDLER]: no principalID in request to get friend perms");
                return FailureResult();
            }

            UUID friendID = UUID.Zero;
            if (request.ContainsKey("FRIENDID"))
                UUID.TryParse(request["FRIENDID"].ToString(), out friendID);
            else
            {
                m_log.WarnFormat("[HGFRIENDS HANDLER]: no friendID in request to get friend perms");
                return FailureResult();
            }

            int perms = m_TheService.GetFriendPerms(principalID, friendID);
            if (perms < 0)
                return FailureResult("Friend not found");

            return SuccessResult(perms.ToString());
        }

        byte[] NewFriendship(Dictionary<string, object> request)
        {
            bool verified = VerifyServiceKey(request);

            FriendInfo friend = new FriendInfo(request);

            bool success = m_TheService.NewFriendship(friend, verified);

            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] DeleteFriendship(Dictionary<string, object> request)
        {
            FriendInfo friend = new FriendInfo(request);
            string secret = string.Empty;
            if (request.ContainsKey("SECRET"))
                secret = request["SECRET"].ToString();

            if (secret == string.Empty)
                return BoolResult(false);

            bool success = m_TheService.DeleteFriendship(friend, secret);

            return BoolResult(success);
        }

        byte[] FriendshipOffered(Dictionary<string, object> request)
        {
            UUID fromID = UUID.Zero;
            UUID toID = UUID.Zero;
            string message = string.Empty;
            string name = string.Empty;

            if (!request.ContainsKey("FromID") || !request.ContainsKey("ToID"))
                return BoolResult(false);

            if (!UUID.TryParse(request["ToID"].ToString(), out toID))
                return BoolResult(false);

            message = request["Message"].ToString();

            if (!UUID.TryParse(request["FromID"].ToString(), out fromID))
                return BoolResult(false);

            if (request.ContainsKey("FromName"))
                name = request["FromName"].ToString();

            bool success = m_TheService.FriendshipOffered(fromID, name, toID, message);

            return BoolResult(success);
        }

        byte[] ValidateFriendshipOffered(Dictionary<string, object> request)
        {
            FriendInfo friend = new FriendInfo(request);
            UUID friendID = UUID.Zero;
            if (!UUID.TryParse(friend.Friend, out friendID))
                return BoolResult(false);

            bool success = m_TheService.ValidateFriendshipOffered(friend.PrincipalID, friendID);

            return BoolResult(success);
        }

        byte[] StatusNotification(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.ContainsKey("userID"))
                UUID.TryParse(request["userID"].ToString(), out principalID);
            else
            {
                m_log.WarnFormat("[HGFRIENDS HANDLER]: no userID in request to notify");
                return FailureResult();
            }

            bool online = true;
            if (request.ContainsKey("online"))
                Boolean.TryParse(request["online"].ToString(), out online);
            else
            {
                m_log.WarnFormat("[HGFRIENDS HANDLER]: no online in request to notify");
                return FailureResult();
            }

            List<string> friends = new List<string>();
            int i = 0;
            foreach (KeyValuePair<string, object> kvp in request)
            {
                if (kvp.Key.Equals("friend_" + i.ToString()))
                {
                    friends.Add(kvp.Value.ToString());
                    i++;
                }
            }

            List<UUID> onlineFriends = m_TheService.StatusNotification(friends, principalID, online);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((onlineFriends == null) || ((onlineFriends != null) && (onlineFriends.Count == 0)))
                result["RESULT"] = "NULL";
            else
            {
                i = 0;
                foreach (UUID f in onlineFriends)
                {
                    result["friend_" + i] = f.ToString();
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        #endregion

        #region Misc

        private bool VerifyServiceKey(Dictionary<string, object> request)
        {
            if (!request.ContainsKey("KEY") || !request.ContainsKey("SESSIONID"))
            {
                m_log.WarnFormat("[HGFRIENDS HANDLER]: ignoring request without Key or SessionID");
                return false;
            }

            if (request["KEY"] == null || request["SESSIONID"] == null)
                return false;

            string serviceKey = request["KEY"].ToString();
            string sessionStr = request["SESSIONID"].ToString();

            UUID sessionID;
            if (!UUID.TryParse(sessionStr, out sessionID) || serviceKey == string.Empty)
                return false;

            if (!m_UserAgentService.VerifyAgent(sessionID, serviceKey))
            {
                m_log.WarnFormat("[HGFRIENDS HANDLER]: Key {0} for session {1} did not match existing key. Ignoring request", serviceKey, sessionID);
                return false;
            }

            m_log.DebugFormat("[HGFRIENDS HANDLER]: Verification ok");
            return true;
        }

        private byte[] SuccessResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        private byte[] SuccessResult(string value)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "RESULT", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Value", "");
            message.AppendChild(doc.CreateTextNode(value));

            rootElement.AppendChild(message);

            return Util.DocToBytes(doc);
        }


        private byte[] FailureResult()
        {
            return FailureResult(String.Empty);
        }

        private byte[] FailureResult(string msg)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "RESULT", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Message", "");
            message.AppendChild(doc.CreateTextNode(msg));

            rootElement.AppendChild(message);

            return Util.DocToBytes(doc);
        }

        private byte[] BoolResult(bool value)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "RESULT", "");
            result.AppendChild(doc.CreateTextNode(value.ToString()));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        #endregion
    }
}
