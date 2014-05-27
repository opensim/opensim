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
using OpenSim.Framework.Communications;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class UserAccountServicesConnector : BaseServiceConnector, IUserAccountService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public UserAccountServicesConnector()
        {
        }

        public UserAccountServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public UserAccountServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig assetConfig = source.Configs["UserAccountService"];
            if (assetConfig == null)
            {
                m_log.Error("[ACCOUNT CONNECTOR]: UserAccountService missing from OpenSim.ini");
                throw new Exception("User account connector init error");
            }

            string serviceURI = assetConfig.GetString("UserAccountServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[ACCOUNT CONNECTOR]: No Server URI named in section UserAccountService");
                throw new Exception("User account connector init error");
            }
            m_ServerURI = serviceURI;

            base.Initialise(source, "UserAccountService");
        }

        public virtual UserAccount GetUserAccount(UUID scopeID, string firstName, string lastName)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getaccount";

            sendData["ScopeID"] = scopeID;
            sendData["FirstName"] = firstName.ToString();
            sendData["LastName"] = lastName.ToString();

            return SendAndGetReply(sendData);
        }

        public virtual UserAccount GetUserAccount(UUID scopeID, string email)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getaccount";

            sendData["ScopeID"] = scopeID;
            sendData["Email"] = email;

            return SendAndGetReply(sendData);
        }

        public virtual UserAccount GetUserAccount(UUID scopeID, UUID userID)
        {
            //m_log.DebugFormat("[ACCOUNTS CONNECTOR]: GetUserAccount {0}", userID);
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getaccount";

            sendData["ScopeID"] = scopeID;
            sendData["UserID"] = userID.ToString();

            return SendAndGetReply(sendData);
        }

        public List<UserAccount> GetUserAccounts(UUID scopeID, string query)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getaccounts";

            sendData["ScopeID"] = scopeID.ToString();
            sendData["query"] = query;

            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/accounts";
            // m_log.DebugFormat("[ACCOUNTS CONNECTOR]: queryString = {0}", reqString);
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply == null || (reply != null && reply == string.Empty))
                {
                    m_log.DebugFormat("[ACCOUNT CONNECTOR]: GetUserAccounts received null or empty reply");
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ACCOUNT CONNECTOR]: Exception when contacting user accounts server at {0}: {1}", uri, e.Message);
            }

            List<UserAccount> accounts = new List<UserAccount>();

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if (replyData != null)
            {
                if (replyData.ContainsKey("result") && replyData["result"].ToString() == "null")
                {
                    return accounts;
                }

                Dictionary<string, object>.ValueCollection accountList = replyData.Values;
                //m_log.DebugFormat("[ACCOUNTS CONNECTOR]: GetAgents returned {0} elements", pinfosList.Count);
                foreach (object acc in accountList)
                {
                    if (acc is Dictionary<string, object>)
                    {
                        UserAccount pinfo = new UserAccount((Dictionary<string, object>)acc);
                        accounts.Add(pinfo);
                    }
                    else
                        m_log.DebugFormat("[ACCOUNT CONNECTOR]: GetUserAccounts received invalid response type {0}",
                            acc.GetType());
                }
            }
            else
                m_log.DebugFormat("[ACCOUNTS CONNECTOR]: GetUserAccounts received null response");

            return accounts;
        }

        public void InvalidateCache(UUID userID)
        {
        }

        public virtual bool StoreUserAccount(UserAccount data)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "setaccount";

            Dictionary<string, object> structData = data.ToKeyValuePairs();

            foreach (KeyValuePair<string, object> kvp in structData)
            {
                if (kvp.Value == null)
                {
                    m_log.DebugFormat("[ACCOUNTS CONNECTOR]: Null value for {0}", kvp.Key);
                    continue;
                }
                sendData[kvp.Key] = kvp.Value.ToString();
            }

            return SendAndGetBoolReply(sendData);
        }

        private UserAccount SendAndGetReply(Dictionary<string, object> sendData)
        {
            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/accounts";
            // m_log.DebugFormat("[ACCOUNTS CONNECTOR]: queryString = {0}", reqString);
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply == null || (reply != null && reply == string.Empty))
                {
                    m_log.DebugFormat("[ACCOUNT CONNECTOR]: GetUserAccount received null or empty reply");
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ACCOUNT CONNECTOR]: Exception when contacting user accounts server at {0}: {1}", uri, e.Message);
            }

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
            UserAccount account = null;

            if ((replyData != null) && replyData.ContainsKey("result") && (replyData["result"] != null))
            {
                if (replyData["result"] is Dictionary<string, object>)
                {
                    account = new UserAccount((Dictionary<string, object>)replyData["result"]);
                }
            }

            return account;

        }

        private bool SendAndGetBoolReply(Dictionary<string, object> sendData)
        {
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/accounts";
            // m_log.DebugFormat("[ACCOUNTS CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString().ToLower() == "success")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[ACCOUNTS CONNECTOR]: Set or Create UserAccount reply data does not contain result field");

                }
                else
                    m_log.DebugFormat("[ACCOUNTS CONNECTOR]: Set or Create UserAccount received empty reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[ACCOUNT CONNECTOR]: Exception when contacting user accounts server at {0}: {1}", uri, e.Message);
            }

            return false;
        }

    }
}
