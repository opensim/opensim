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
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class HGFriendsServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IFriendsService m_FriendsService;
        private IUserAgentService m_UserAgentService;

        public HGFriendsServerPostHandler(IFriendsService service, IUserAgentService uservice) :
                base("POST", "/hgfriends")
        {
            m_FriendsService = service;
            m_UserAgentService = uservice;
            m_log.DebugFormat("[HGFRIENDS HANDLER]: HGFriendsServerPostHandler is On");
        }

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
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
                }
                m_log.DebugFormat("[HGFRIENDS HANDLER]: unknown method {0} request {1}", method.Length, method);
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

            string perms = "0";
            FriendInfo[] friendsInfo = m_FriendsService.GetFriends(principalID);
            foreach (FriendInfo finfo in friendsInfo)
            {
                if (finfo.Friend.StartsWith(friendID.ToString()))
                    return SuccessResult(finfo.TheirFlags.ToString());
            }

            return FailureResult("Friend not found");
        }

        byte[] NewFriendship(Dictionary<string, object> request)
        {
            if (!VerifyServiceKey(request))
                return FailureResult();

            // OK, can proceed
            FriendInfo friend = new FriendInfo(request);
            UUID friendID;
            string tmp = string.Empty;
            if (!Util.ParseUniversalUserIdentifier(friend.Friend, out friendID, out tmp, out tmp, out tmp, out tmp))
                return FailureResult();

            m_log.DebugFormat("[HGFRIENDS HANDLER]: New friendship {0} {1}", friend.PrincipalID, friend.Friend);

            // If the friendship already exists, return fail
            FriendInfo[] finfos = m_FriendsService.GetFriends(friend.PrincipalID);
            foreach (FriendInfo finfo in finfos)
                if (finfo.Friend.StartsWith(friendID.ToString()))
                    return FailureResult();

            // the user needs to confirm when he gets home
            bool success = m_FriendsService.StoreFriend(friend.PrincipalID.ToString(), friend.Friend, 0);

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
                return FailureResult();

            FriendInfo[] finfos = m_FriendsService.GetFriends(friend.PrincipalID);
            foreach (FriendInfo finfo in finfos)
            {
                // We check the secret here
                if (finfo.Friend.StartsWith(friend.Friend) && finfo.Friend.EndsWith(secret))
                {
                    m_log.DebugFormat("[HGFRIENDS HANDLER]: Delete friendship {0} {1}", friend.PrincipalID, friend.Friend);
                    m_FriendsService.Delete(friend.PrincipalID, finfo.Friend);
                    m_FriendsService.Delete(finfo.Friend, friend.PrincipalID.ToString());

                    return SuccessResult();
                }
            }

            return FailureResult();
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

            string serviceKey = request["KEY"].ToString();
            string sessionStr = request["SESSIONID"].ToString();
            UUID sessionID;
            UUID.TryParse(sessionStr, out sessionID);

            if (!m_UserAgentService.VerifyAgent(sessionID, serviceKey))
            {
                m_log.WarnFormat("[HGFRIENDS HANDLER]: Key {0} for session {1} did not match existing key. Ignoring request", serviceKey, sessionID);
                return false;
            }

            m_log.DebugFormat("[XXX] Verification ok");
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

            return DocToBytes(doc);
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

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Value", "");
            message.AppendChild(doc.CreateTextNode(value));

            rootElement.AppendChild(message);

            return DocToBytes(doc);
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

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Message", "");
            message.AppendChild(doc.CreateTextNode(msg));

            rootElement.AppendChild(message);

            return DocToBytes(doc);
        }

        private byte[] DocToBytes(XmlDocument doc)
        {
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, null);
            xw.Formatting = Formatting.Indented;
            doc.WriteTo(xw);
            xw.Flush();

            return ms.ToArray();
        }

        #endregion
    }
}
