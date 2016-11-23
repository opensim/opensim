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
using System.Reflection;

using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenSim.Framework;

using OpenMetaverse;
using log4net;

namespace OpenSim.Services.Connectors.Friends
{
    public class FriendsSimConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected virtual string ServicePath()
        {
            return "friends";
        }

        public bool FriendshipOffered(GridRegion region, UUID userID, UUID friendID, string message)
        {
            return FriendshipOffered(region, userID, friendID, message, String.Empty);
        }

        public virtual bool FriendshipOffered(GridRegion region, UUID userID, UUID friendID, string message, string userName)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            //sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "friendship_offered";

            sendData["FromID"] = userID.ToString();
            sendData["ToID"] = friendID.ToString();
            sendData["Message"] = message;
            if (userName != String.Empty)
                sendData["FromName"] = userName;

            return Call(region, sendData);
        }

        public bool FriendshipApproved(GridRegion region, UUID userID, string userName, UUID friendID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            //sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "friendship_approved";

            sendData["FromID"] = userID.ToString();
            sendData["FromName"] = userName;
            sendData["ToID"] = friendID.ToString();

            return Call(region, sendData);
        }

        public bool FriendshipDenied(GridRegion region, UUID userID, string userName, UUID friendID)
        {
            if (region == null)
                return false;

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            //sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "friendship_denied";

            sendData["FromID"] = userID.ToString();
            sendData["FromName"] = userName;
            sendData["ToID"] = friendID.ToString();

            return Call(region, sendData);
        }

        public bool FriendshipTerminated(GridRegion region, UUID userID, UUID friendID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            //sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "friendship_terminated";

            sendData["FromID"] = userID.ToString();
            sendData["ToID"] = friendID.ToString();

            return Call(region, sendData);
        }

        public bool GrantRights(GridRegion region, UUID userID, UUID friendID, int userFlags, int rights)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            //sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "grant_rights";

            sendData["FromID"] = userID.ToString();
            sendData["ToID"] = friendID.ToString();
            sendData["UserFlags"] = userFlags.ToString();
            sendData["Rights"] = rights.ToString();

            return Call(region, sendData);
        }

        public bool StatusNotify(GridRegion region, UUID userID, string friendID, bool online)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            //sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "status";

            sendData["FromID"] = userID.ToString();
            sendData["ToID"] = friendID;
            sendData["Online"] = online.ToString();

            return Call(region, sendData);
        }

        private bool Call(GridRegion region, Dictionary<string, object> sendData)
        {
            Util.FireAndForget(x => {
                string reqString = ServerUtils.BuildQueryString(sendData);
                //m_log.DebugFormat("[FRIENDS SIM CONNECTOR]: queryString = {0}", reqString);
                if (region == null)
                    return;

                string path = ServicePath();
                if (!region.ServerURI.EndsWith("/"))
                    path = "/" + path;
                string uri = region.ServerURI + path;
                // m_log.DebugFormat("[FRIENDS SIM CONNECTOR]: calling {0}", uri);

                try
                {
                    string reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, reqString, 15, null, false);
                    if (reply != string.Empty)
                    {
                        Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                        if (replyData.ContainsKey("RESULT"))
                        {
//                            if (replyData["RESULT"].ToString().ToLower() == "true")
//                                return;
//                            else
                            return;
                        }
                        else
                            m_log.DebugFormat("[FRIENDS SIM CONNECTOR]: reply data does not contain result field");

                    }
                    else
                        m_log.DebugFormat("[FRIENDS SIM CONNECTOR]: received empty reply");
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[FRIENDS SIM CONNECTOR]: Exception when contacting remote sim at {0}: {1}", uri, e.Message);
                }

                return;
            });

            return true;
        }
    }
}
