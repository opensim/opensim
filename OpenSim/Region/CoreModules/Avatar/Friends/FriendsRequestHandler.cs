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
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml;

using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Framework.Servers.HttpServer;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    
//    public class FriendsRequestHandler : BaseStreamHandlerBasicDOSProtector
    public class FriendsSimpleRequestHandler : SimpleStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private FriendsModule m_FriendsModule;
        /*
        public FriendsRequestHandler(FriendsModule fmodule)
                : base("POST", "/friends", new BasicDosProtectorOptions()
                                            {
                                                AllowXForwardedFor = true,
                                                ForgetTimeSpan = TimeSpan.FromMinutes(2),
                                                MaxRequestsInTimeframe = 20,
                                                ReportingName = "FRIENDSDOSPROTECTOR",
                                                RequestTimeSpan = TimeSpan.FromSeconds(5),
                                                ThrottledAction = BasicDOSProtector.ThrottleAction.DoThrottledMethod
                                            })
        */
        public FriendsSimpleRequestHandler(FriendsModule fmodule) : base("/friends")
        {
            m_FriendsModule = fmodule;
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            if (m_FriendsModule is null || m_FriendsModule.Scene is null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
                return;
            }

            if (httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            httpResponse.KeepAlive = false;
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            httpResponse.ContentType = "text/xml";
            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                string body;
                using (StreamReader sr = new StreamReader(httpRequest.InputStream))
                    body = sr.ReadToEnd();

                body = body.Trim();
                Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                {
                    httpResponse.RawBuffer = FailureResult();
                    return;
                }

                string method = request["METHOD"].ToString();
                request.Remove("METHOD");

                switch (method)
                {
                    case "friendship_offered":
                        httpResponse.RawBuffer = FriendshipOffered(request);
                        return;
                    case "friendship_approved":
                        httpResponse.RawBuffer = FriendshipApproved(request);
                        return;
                    case "friendship_denied":
                        httpResponse.RawBuffer = FriendshipDenied(request);
                        return;
                    case "friendship_terminated":
                        httpResponse.RawBuffer = FriendshipTerminated(request);
                        return;
                    case "grant_rights":
                        httpResponse.RawBuffer = GrantRights(request);
                        return;
                    case "status":
                        httpResponse.RawBuffer = StatusNotification(request);
                        return;
                }
            }
            catch (Exception e)
            {
                m_log.Debug("[FRIENDS]: Exception {0}" + e.ToString());
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;

            }

            httpResponse.RawBuffer = FailureResult();
        }

        byte[] FriendshipOffered(Dictionary<string, object> request)
        {
            object tmpo;

            if (!request.TryGetValue("FromID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID fromID))
                return FailureResult();

            if (!request.TryGetValue("ToID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID toID))
                return FailureResult();

            string message = string.Empty;
            if (request.TryGetValue("Message", out tmpo))
                message = tmpo.ToString();

            UserAccount account = m_FriendsModule.UserAccountService.GetUserAccount(UUID.Zero, fromID);
            string name = (account == null) ? "Unknown" : account.FirstName + " " + account.LastName;

            GridInstantMessage im = new GridInstantMessage(m_FriendsModule.Scene, fromID, name, toID,
                (byte)InstantMessageDialog.FriendshipOffered, message, false, Vector3.Zero);

            // !! HACK
            im.imSessionID = im.fromAgentID;

            if (m_FriendsModule.LocalFriendshipOffered(toID, im))
                return SuccessResult();

            return FailureResult();
        }

        byte[] FriendshipApproved(Dictionary<string, object> request)
        {
            object tmpo;

            if (!request.TryGetValue("FromID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID fromID))
                return FailureResult();

            if (!request.TryGetValue("ToID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID toID))
                return FailureResult();

            string fromName = string.Empty;
            if (request.TryGetValue("FromName", out tmpo))
                fromName = tmpo.ToString();

            if (m_FriendsModule.LocalFriendshipApproved(fromID, fromName, toID))
                return SuccessResult();

            return FailureResult();
        }

        byte[] FriendshipDenied(Dictionary<string, object> request)
        {
            object tmpo;
            string fromName = string.Empty;

            if (!request.TryGetValue("FromID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID fromID))
                return FailureResult();

            if (!request.TryGetValue("ToID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID toID))
                return FailureResult();

            if (request.TryGetValue("FromName", out tmpo))
                fromName = tmpo.ToString();

            if (m_FriendsModule.LocalFriendshipDenied(fromID, fromName, toID))
                return SuccessResult();

            return FailureResult();
        }

        byte[] FriendshipTerminated(Dictionary<string, object> request)
        {
            object tmpo;
            if (!request.TryGetValue("FromID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID fromID))
                return FailureResult();

            if (!request.TryGetValue("ToID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID toID))
                return FailureResult();

            if (m_FriendsModule.LocalFriendshipTerminated(fromID, toID))
                return SuccessResult();

            return FailureResult();
        }

        byte[] GrantRights(Dictionary<string, object> request)
        {
            object tmpo;
            if (!request.TryGetValue("FromID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID fromID))
                return FailureResult();

            if (!request.TryGetValue("ToID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID toID))
                return FailureResult();

            if (!request.TryGetValue("UserFlags", out tmpo) || !Int32.TryParse(tmpo.ToString(), out int oldRights))
                return FailureResult();

            if (!request.TryGetValue("Rights", out tmpo) || !Int32.TryParse(tmpo.ToString(), out int newRights))
                return FailureResult();

            if (m_FriendsModule.LocalGrantRights(fromID, toID, oldRights, newRights))
                return SuccessResult();

            return FailureResult();
        }

        byte[] StatusNotification(Dictionary<string, object> request)
        {
            object tmpo;
            if (!request.TryGetValue("FromID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID fromID))
                return FailureResult();

            if (!request.TryGetValue("ToID", out tmpo) || !UUID.TryParse(tmpo.ToString(), out UUID toID))
                return FailureResult();

            if (!request.TryGetValue("Online", out tmpo) || !Boolean.TryParse(tmpo.ToString(), out bool online))
                return FailureResult();

            if (m_FriendsModule.LocalStatusNotification(fromID, toID, online))
                return SuccessResult();

            return FailureResult();
        }

        #region Misc

        private byte[] FailureResult()
        {
            return BoolResult(false);
        }

        private byte[] SuccessResult()
        {
            return BoolResult(true);
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
