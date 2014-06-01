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
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Friends
{
    public class FriendsServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IFriendsService m_FriendsService;

        public FriendsServerPostHandler(IFriendsService service, IServiceAuth auth) :
                base("POST", "/friends", auth)
        {
            m_FriendsService = service;
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
                    case "getfriends":
                        return GetFriends(request);

                    case "getfriends_string":
                        return GetFriendsString(request);

                    case "storefriend":
                        return StoreFriend(request);

                    case "deletefriend":
                        return DeleteFriend(request);

                    case "deletefriend_string":
                        return DeleteFriendString(request);

                }

                m_log.DebugFormat("[FRIENDS HANDLER]: unknown method request {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[FRIENDS HANDLER]: Exception {0}", e);
            }

            return FailureResult();
        }

        #region Method-specific handlers

        byte[] GetFriends(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.ContainsKey("PRINCIPALID"))
                UUID.TryParse(request["PRINCIPALID"].ToString(), out principalID);
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to get friends");

            FriendInfo[] finfos = m_FriendsService.GetFriends(principalID);

            return PackageFriends(finfos);
        }

        byte[] GetFriendsString(Dictionary<string, object> request)
        {
            string principalID = string.Empty;
            if (request.ContainsKey("PRINCIPALID"))
                principalID = request["PRINCIPALID"].ToString();
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to get friends");

            FriendInfo[] finfos = m_FriendsService.GetFriends(principalID);

            return PackageFriends(finfos);
        }

        private byte[] PackageFriends(FriendInfo[] finfos)
        {

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((finfos == null) || ((finfos != null) && (finfos.Length == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (FriendInfo finfo in finfos)
                {
                    Dictionary<string, object> rinfoDict = finfo.ToKeyValuePairs();
                    result["friend" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[FRIENDS HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] StoreFriend(Dictionary<string, object> request)
        {
            string principalID = string.Empty, friend = string.Empty; int flags = 0;
            FromKeyValuePairs(request, out principalID, out friend, out flags);
            bool success = m_FriendsService.StoreFriend(principalID, friend, flags);

            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] DeleteFriend(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.ContainsKey("PRINCIPALID"))
                UUID.TryParse(request["PRINCIPALID"].ToString(), out principalID);
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to delete friend");
            string friend = string.Empty;
            if (request.ContainsKey("FRIEND"))
                friend = request["FRIEND"].ToString();

            bool success = m_FriendsService.Delete(principalID, friend);
            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] DeleteFriendString(Dictionary<string, object> request)
        {
            string principalID = string.Empty;
            if (request.ContainsKey("PRINCIPALID"))
                principalID = request["PRINCIPALID"].ToString();
            else
                m_log.WarnFormat("[FRIENDS HANDLER]: no principalID in request to delete friend");
            string friend = string.Empty;
            if (request.ContainsKey("FRIEND"))
                friend = request["FRIEND"].ToString();

            bool success = m_FriendsService.Delete(principalID, friend);
            if (success)
                return SuccessResult();
            else
                return FailureResult();
        }

        #endregion

        #region Misc

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

            return Util.DocToBytes(doc);
        }

        void FromKeyValuePairs(Dictionary<string, object> kvp, out string principalID, out string friend, out int flags)
        {
            principalID = string.Empty;
            if (kvp.ContainsKey("PrincipalID") && kvp["PrincipalID"] != null)
                principalID = kvp["PrincipalID"].ToString();
            friend = string.Empty;
            if (kvp.ContainsKey("Friend") && kvp["Friend"] != null)
                friend = kvp["Friend"].ToString();
            flags = 0;
            if (kvp.ContainsKey("MyFlags") && kvp["MyFlags"] != null)
                Int32.TryParse(kvp["MyFlags"].ToString(), out flags);
        }

        #endregion
    }
}
