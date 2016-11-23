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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Friends;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class HGFriendsServicesConnector : FriendsSimConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;
        private string m_ServiceKey = String.Empty;
        private UUID m_SessionID;

        public HGFriendsServicesConnector()
        {
        }

        public HGFriendsServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public HGFriendsServicesConnector(string serverURI, UUID sessionID, string serviceKey)
        {
            m_ServerURI = serverURI.TrimEnd('/');
            m_ServiceKey = serviceKey;
            m_SessionID = sessionID;
        }

        protected override string ServicePath()
        {
            return "hgfriends";
        }

        #region IFriendsService

        public uint GetFriendPerms(UUID PrincipalID, UUID friendID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["PRINCIPALID"] = PrincipalID.ToString();
            sendData["FRIENDID"] = friendID.ToString();
            sendData["METHOD"] = "getfriendperms";
            sendData["KEY"] = m_ServiceKey;
            sendData["SESSIONID"] = m_SessionID.ToString();

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/hgfriends";

            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if ((replyData != null) && replyData.ContainsKey("Value") && (replyData["Value"] != null))
                    {
                        uint perms = 0;
                        uint.TryParse(replyData["Value"].ToString(), out perms);
                        return perms;
                    }
                    else
                        m_log.DebugFormat("[HGFRIENDS CONNECTOR]: GetFriendPerms {0} received null response",
                            PrincipalID);

                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: Exception when contacting friends server at {0}: {1}", uri, e.Message);
            }

            return 0;

        }

        public bool NewFriendship(UUID PrincipalID, string Friend)
        {
            FriendInfo finfo = new FriendInfo();
            finfo.PrincipalID = PrincipalID;
            finfo.Friend = Friend;

            Dictionary<string, object> sendData = finfo.ToKeyValuePairs();

            sendData["METHOD"] = "newfriendship";
            sendData["KEY"] = m_ServiceKey;
            sendData["SESSIONID"] = m_SessionID.ToString();

            string reply = string.Empty;
            string uri = m_ServerURI + "/hgfriends";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData));
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: Exception when contacting friends server at {0}: {1}", uri, e.Message);
                return false;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if ((replyData != null) && replyData.ContainsKey("Result") && (replyData["Result"] != null))
                {
                    bool success = false;
                    Boolean.TryParse(replyData["Result"].ToString(), out success);
                    return success;
                }
                else
                    m_log.DebugFormat("[HGFRIENDS CONNECTOR]: StoreFriend {0} {1} received null response",
                        PrincipalID, Friend);
            }
            else
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: StoreFriend received null reply");

            return false;

        }

        public bool DeleteFriendship(UUID PrincipalID, UUID Friend, string secret)
        {
            FriendInfo finfo = new FriendInfo();
            finfo.PrincipalID = PrincipalID;
            finfo.Friend = Friend.ToString();

            Dictionary<string, object> sendData = finfo.ToKeyValuePairs();

            sendData["METHOD"] = "deletefriendship";
            sendData["SECRET"] = secret;

            string reply = string.Empty;
            string uri = m_ServerURI + "/hgfriends";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData));
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: Exception when contacting friends server at {0}: {1}", uri, e.Message);
                return false;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData.ContainsKey("RESULT"))
                {
                    if (replyData["RESULT"].ToString().ToLower() == "true")
                        return true;
                    else
                        return false;
                }
                else
                    m_log.DebugFormat("[HGFRIENDS CONNECTOR]: reply data does not contain result field");

            }
            else
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: received empty reply");

            return false;

        }

        public bool ValidateFriendshipOffered(UUID fromID, UUID toID)
        {
            FriendInfo finfo = new FriendInfo();
            finfo.PrincipalID = fromID;
            finfo.Friend = toID.ToString();

            Dictionary<string, object> sendData = finfo.ToKeyValuePairs();

            sendData["METHOD"] = "validate_friendship_offered";

            string reply = string.Empty;
            string uri = m_ServerURI + "/hgfriends";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData));
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: Exception when contacting friends server at {0}: {1}", uri, e.Message);
                return false;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData.ContainsKey("RESULT"))
                {
                    if (replyData["RESULT"].ToString().ToLower() == "true")
                        return true;
                    else
                        return false;
                }
                else
                    m_log.DebugFormat("[HGFRIENDS CONNECTOR]: reply data does not contain result field");

            }
            else
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: received empty reply");

            return false;

        }

        public List<UUID> StatusNotification(List<string> friends, UUID userID, bool online)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            List<UUID> friendsOnline = new List<UUID>();

            sendData["METHOD"] = "statusnotification";
            sendData["userID"] = userID.ToString();
            sendData["online"] = online.ToString();
            int i = 0;
            foreach (string s in friends)
            {
                sendData["friend_" + i.ToString()] = s;
                i++;
            }

            string reply = string.Empty;
            string uri = m_ServerURI + "/hgfriends";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData),
                        15,
                        null,
                        false);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: Exception when contacting friends server at {0}: {1}", uri, e.Message);
                return friendsOnline;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                // Here is the actual response
                foreach (string key in replyData.Keys)
                {
                    if (key.StartsWith("friend_") && replyData[key] != null)
                    {
                        UUID uuid;
                        if (UUID.TryParse(replyData[key].ToString(), out uuid))
                            friendsOnline.Add(uuid);
                    }
                }
            }
            else
                m_log.DebugFormat("[HGFRIENDS CONNECTOR]: Received empty reply from remote StatusNotify");

            return friendsOnline;

        }

        #endregion
    }
}