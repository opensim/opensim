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
using OpenSim.Services.UserAccountService;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.ServiceAuth;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.UserAccounts
{
    public class UserAccountServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IUserAccountService m_UserAccountService;
        private bool m_AllowCreateUser = false;
        private bool m_AllowSetAccount = false;

        public UserAccountServerPostHandler(IUserAccountService service)
            : this(service, null, null) {}

        public UserAccountServerPostHandler(IUserAccountService service, IConfig config, IServiceAuth auth) :
                base("POST", "/accounts", auth)
        {
            m_UserAccountService = service;

            if (config != null)
            {
                m_AllowCreateUser = config.GetBoolean("AllowCreateUser", m_AllowCreateUser);
                m_AllowSetAccount = config.GetBoolean("AllowSetAccount", m_AllowSetAccount);
            }
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            // We need to check the authorization header
            //httpRequest.Headers["authorization"] ...

            //m_log.DebugFormat("[XXX]: query String: {0}", body);
            string method = string.Empty;
            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                method = request["METHOD"].ToString();

                switch (method)
                {
                    case "createuser":
                        if (m_AllowCreateUser)
                            return CreateUser(request);
                        else
                            break;
                    case "getaccount":
                        return GetAccount(request);
                    case "getaccounts":
                        return GetAccounts(request);
                    case "setaccount":
                        if (m_AllowSetAccount)
                            return StoreAccount(request);
                        else
                            break;
                }

                m_log.DebugFormat("[USER SERVICE HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[USER SERVICE HANDLER]: Exception in method {0}: {1}", method, e);
            }

            return FailureResult();
        }

        byte[] GetAccount(Dictionary<string, object> request)
        {
            UserAccount account = null;
            UUID scopeID = UUID.Zero;
            Dictionary<string, object> result = new Dictionary<string, object>();

            if (request.ContainsKey("ScopeID") && !UUID.TryParse(request["ScopeID"].ToString(), out scopeID))
            {
                result["result"] = "null";
                return ResultToBytes(result);
            }

            if (request.ContainsKey("UserID") && request["UserID"] != null)
            {
                UUID userID;
                if (UUID.TryParse(request["UserID"].ToString(), out userID))
                    account = m_UserAccountService.GetUserAccount(scopeID, userID);
            }
            else if (request.ContainsKey("PrincipalID") && request["PrincipalID"] != null)
            {
                UUID userID;
                if (UUID.TryParse(request["PrincipalID"].ToString(), out userID))
                    account = m_UserAccountService.GetUserAccount(scopeID, userID);
            }
            else if (request.ContainsKey("Email") && request["Email"] != null)
            {
                account = m_UserAccountService.GetUserAccount(scopeID, request["Email"].ToString());
            }
            else if (request.ContainsKey("FirstName") && request.ContainsKey("LastName") &&
                request["FirstName"] != null && request["LastName"] != null)
            {
                account = m_UserAccountService.GetUserAccount(scopeID, request["FirstName"].ToString(), request["LastName"].ToString());
            }

            if (account == null)
            {
                result["result"] = "null";
            }
            else
            {
                result["result"] = account.ToKeyValuePairs();
            }

            return ResultToBytes(result);
        }

        byte[] GetAccounts(Dictionary<string, object> request)
        {
            if (!request.ContainsKey("query"))
                return FailureResult();

            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("ScopeID") && !UUID.TryParse(request["ScopeID"].ToString(), out scopeID))
                return FailureResult();

            string query = request["query"].ToString();

            List<UserAccount> accounts = m_UserAccountService.GetUserAccounts(scopeID, query);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((accounts == null) || ((accounts != null) && (accounts.Count == 0)))
            {
                result["result"] = "null";
            }
            else
            {
                int i = 0;
                foreach (UserAccount acc in accounts)
                {
                    Dictionary<string, object> rinfoDict = acc.ToKeyValuePairs();
                    result["account" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] StoreAccount(Dictionary<string, object> request)
        {
            UUID principalID = UUID.Zero;
            if (request.ContainsKey("PrincipalID") && !UUID.TryParse(request["PrincipalID"].ToString(), out principalID))
                return FailureResult();

            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("ScopeID") && !UUID.TryParse(request["ScopeID"].ToString(), out scopeID))
                return FailureResult();

            UserAccount existingAccount = m_UserAccountService.GetUserAccount(scopeID, principalID);
            if (existingAccount == null)
                return FailureResult();

            Dictionary<string, object> result = new Dictionary<string, object>();

            if (request.ContainsKey("FirstName"))
                existingAccount.FirstName = request["FirstName"].ToString();

            if (request.ContainsKey("LastName"))
                existingAccount.LastName = request["LastName"].ToString();

            if (request.ContainsKey("Email"))
                existingAccount.Email = request["Email"].ToString();

            int created = 0;
            if (request.ContainsKey("Created") && int.TryParse(request["Created"].ToString(), out created))
                existingAccount.Created = created;

            int userLevel = 0;
            if (request.ContainsKey("UserLevel") && int.TryParse(request["UserLevel"].ToString(), out userLevel))
                existingAccount.UserLevel = userLevel;

            int userFlags = 0;
            if (request.ContainsKey("UserFlags") && int.TryParse(request["UserFlags"].ToString(), out userFlags))
                existingAccount.UserFlags = userFlags;

            if (request.ContainsKey("UserTitle"))
                existingAccount.UserTitle = request["UserTitle"].ToString();

            if (!m_UserAccountService.StoreUserAccount(existingAccount))
            {
                m_log.ErrorFormat(
                    "[USER ACCOUNT SERVER POST HANDLER]: Account store failed for account {0} {1} {2}",
                    existingAccount.FirstName, existingAccount.LastName, existingAccount.PrincipalID);

                return FailureResult();
            }

            result["result"] = existingAccount.ToKeyValuePairs();

            return ResultToBytes(result);
        }

        byte[] CreateUser(Dictionary<string, object> request)
        {
            if (!
                request.ContainsKey("FirstName")
                    && request.ContainsKey("LastName")
                    && request.ContainsKey("Password"))
                return FailureResult();

            Dictionary<string, object> result = new Dictionary<string, object>();

            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("ScopeID") && !UUID.TryParse(request["ScopeID"].ToString(), out scopeID))
                return FailureResult();

            UUID principalID = UUID.Random();
            if (request.ContainsKey("PrincipalID") && !UUID.TryParse(request["PrincipalID"].ToString(), out principalID))
                return FailureResult();

            string firstName = request["FirstName"].ToString();
            string lastName = request["LastName"].ToString();
            string password = request["Password"].ToString();

            string email = "";
            if (request.ContainsKey("Email"))
                email = request["Email"].ToString();

            UserAccount createdUserAccount = null;

            if (m_UserAccountService is UserAccountService)
                createdUserAccount
                    = ((UserAccountService)m_UserAccountService).CreateUser(
                        scopeID, principalID, firstName, lastName, password, email);

            if (createdUserAccount == null)
                return FailureResult();

            result["result"] = createdUserAccount.ToKeyValuePairs();

            return ResultToBytes(result);
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

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        private byte[] FailureResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        private byte[] ResultToBytes(Dictionary<string, object> result)
        {
            string xmlString = ServerUtils.BuildXmlResponse(result);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }
    }
}