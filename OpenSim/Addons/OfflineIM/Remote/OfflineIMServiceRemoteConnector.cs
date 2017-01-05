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
using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.OfflineIM
{
    public class OfflineIMServiceRemoteConnector : IOfflineIMService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = string.Empty;
        private IServiceAuth m_Auth;
        private object m_Lock = new object();

        public OfflineIMServiceRemoteConnector(string url)
        {
            m_ServerURI = url;
            m_log.DebugFormat("[OfflineIM.V2.RemoteConnector]: Offline IM server at {0}", m_ServerURI);
        }

        public OfflineIMServiceRemoteConnector(IConfigSource config)
        {
            IConfig cnf = config.Configs["Messaging"];
            if (cnf == null)
            {
                m_log.WarnFormat("[OfflineIM.V2.RemoteConnector]: Missing Messaging configuration");
                return;
            }

            m_ServerURI = cnf.GetString("OfflineMessageURL", string.Empty);

            /// This is from BaseServiceConnector
            string authType = Util.GetConfigVarFromSections<string>(config, "AuthType", new string[] { "Network", "Messaging" }, "None");

            switch (authType)
            {
                case "BasicHttpAuthentication":
                    m_Auth = new BasicHttpAuthentication(config, "Messaging");
                    break;
            }
            ///
            m_log.DebugFormat("[OfflineIM.V2.RemoteConnector]: Offline IM server at {0} with auth {1}",
                m_ServerURI, (m_Auth == null ? "None" : m_Auth.GetType().ToString()));
        }

        #region IOfflineIMService
        public List<GridInstantMessage> GetMessages(UUID principalID)
        {
            List<GridInstantMessage> ims = new List<GridInstantMessage>();

            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["PrincipalID"] = principalID;
            Dictionary<string, object> ret = MakeRequest("GET", sendData);

            if (ret == null)
                return ims;

            if (!ret.ContainsKey("RESULT"))
                return ims;

            string result = ret["RESULT"].ToString();
            if (result == "NULL" || result.ToLower() == "false")
            {
                string reason = ret.ContainsKey("REASON") ? ret["REASON"].ToString() : "Unknown error";
                m_log.DebugFormat("[OfflineIM.V2.RemoteConnector]: GetMessages for {0} failed: {1}", principalID, reason);
                return ims;
            }

            foreach (object v in ((Dictionary<string, object>)ret["RESULT"]).Values)
            {
                GridInstantMessage m = OfflineIMDataUtils.GridInstantMessage((Dictionary<string, object>)v);
                ims.Add(m);
            }

            return ims;
        }

        public bool StoreMessage(GridInstantMessage im, out string reason)
        {
            reason = string.Empty;
            Dictionary<string, object> sendData = OfflineIMDataUtils.GridInstantMessage(im);

            Dictionary<string, object> ret = MakeRequest("STORE", sendData);

            if (ret == null)
            {
                reason = "Bad response from server";
                return false;
            }

            string result = ret["RESULT"].ToString();
            if (result == "NULL" || result.ToLower() == "false")
            {
                reason = ret.ContainsKey("REASON") ? ret["REASON"].ToString() : "Unknown error";
                return false;
            }

            return true;
        }

        public void DeleteMessages(UUID userID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["UserID"] = userID;

            MakeRequest("DELETE", sendData);
        }

        #endregion


        #region Make Request

        private Dictionary<string, object> MakeRequest(string method, Dictionary<string, object> sendData)
        {
            sendData["METHOD"] = method;

            string reply = string.Empty;
            lock (m_Lock)
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                         m_ServerURI + "/offlineim",
                         ServerUtils.BuildQueryString(sendData),
                         m_Auth);

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(
                    reply);

            return replyData;
        }
        #endregion

    }
}
