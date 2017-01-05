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
using OpenMetaverse;
using log4net;
using Nini.Config;
using System.Reflection;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Services.Base;

namespace OpenSim.Services.AuthenticationService
{
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need
    // verifiable identification.
    //
    public class AuthenticationServiceBase : ServiceBase
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected IAuthenticationData m_Database;
        protected IUserAccountService m_UserAccountService = null;

        public AuthenticationServiceBase(IConfigSource config, IUserAccountService acct) : this(config)
        {
            m_UserAccountService = acct;
        }

        public AuthenticationServiceBase(IConfigSource config) : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;
            string realm = "auth";

            //
            // Try reading the [AuthenticationService] section first, if it exists
            //
            IConfig authConfig = config.Configs["AuthenticationService"];
            if (authConfig != null)
            {
                dllName = authConfig.GetString("StorageProvider", dllName);
                connString = authConfig.GetString("ConnectionString", connString);
                realm = authConfig.GetString("Realm", realm);
            }

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (connString == String.Empty)
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName == String.Empty || realm == String.Empty)
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IAuthenticationData>(dllName,
                    new Object[] {connString, realm});
            if (m_Database == null)
                throw new Exception(string.Format("Could not find a storage interface in module {0}", dllName));
        }

        public bool Verify(UUID principalID, string token, int lifetime)
        {
            return m_Database.CheckToken(principalID, token, lifetime);
        }

        public virtual bool Release(UUID principalID, string token)
        {
            return m_Database.CheckToken(principalID, token, 0);
        }

        public virtual bool SetPassword(UUID principalID, string password)
        {
            string passwordSalt = Util.Md5Hash(UUID.Random().ToString());
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + passwordSalt);

            AuthenticationData auth = m_Database.Get(principalID);
            if (auth == null)
            {
                auth = new AuthenticationData();
                auth.PrincipalID = principalID;
                auth.Data = new System.Collections.Generic.Dictionary<string, object>();
                auth.Data["accountType"] = "UserAccount";
                auth.Data["webLoginKey"] = UUID.Zero.ToString();
            }
            auth.Data["passwordHash"] = md5PasswdHash;
            auth.Data["passwordSalt"] = passwordSalt;
            if (!m_Database.Store(auth))
            {
                m_log.DebugFormat("[AUTHENTICATION DB]: Failed to store authentication data");
                return false;
            }

            m_log.InfoFormat("[AUTHENTICATION DB]: Set password for principalID {0}", principalID);
            return true;
        }

        public virtual AuthInfo GetAuthInfo(UUID principalID)
        {
            AuthenticationData data = m_Database.Get(principalID);

            if (data == null)
            {
                return null;
            }
            else
            {
                AuthInfo info
                    = new AuthInfo()
                        {
                            PrincipalID = data.PrincipalID,
                            AccountType = data.Data["accountType"] as string,
                            PasswordHash = data.Data["passwordHash"] as string,
                            PasswordSalt = data.Data["passwordSalt"] as string,
                            WebLoginKey = data.Data["webLoginKey"] as string
                        };

                return info;
            }
        }

        public virtual bool SetAuthInfo(AuthInfo info)
        {
            AuthenticationData auth = new AuthenticationData();
            auth.PrincipalID = info.PrincipalID;
            auth.Data = new System.Collections.Generic.Dictionary<string, object>();
            auth.Data["accountType"] = info.AccountType;
            auth.Data["webLoginKey"] = info.WebLoginKey;
            auth.Data["passwordHash"] = info.PasswordHash;
            auth.Data["passwordSalt"] = info.PasswordSalt;

            if (!m_Database.Store(auth))
            {
                m_log.ErrorFormat("[AUTHENTICATION DB]: Failed to store authentication info.");
                return false;
            }

            m_log.DebugFormat("[AUTHENTICATION DB]: Set authentication info for principalID {0}", info.PrincipalID);
            return true;
        }

        protected string GetToken(UUID principalID, int lifetime)
        {
            UUID token = UUID.Random();

            if (m_Database.SetToken(principalID, token.ToString(), lifetime))
                return token.ToString();

            return String.Empty;
        }

    }
}
