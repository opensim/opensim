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
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Base;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.AuthenticationService
{
    /// <summary>
    /// Simple authentication service implementation dealing only with users.
    /// It uses the user DB directly to access user information.
    /// It takes two config vars:
    /// - Authenticate = {true|false} : to do or not to do authentication
    /// - Authority = string like "osgrid.org" : this identity authority
    ///               that will be called back for identity verification
    /// </summary>
    public class HGAuthenticationService : ServiceBase, IAuthenticationService
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IUserDataPlugin m_Database;
        protected string m_AuthorityURL;
        protected bool m_PerformAuthentication;
        protected Dictionary<UUID, List<string>> m_UserKeys = new Dictionary<UUID, List<string>>();


        public HGAuthenticationService(IConfigSource config) : base(config)
        {
            string dllName = String.Empty;
            string connString = String.Empty;

            //
            // Try reading the [DatabaseService] section first, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                dllName = dbConfig.GetString("StorageProvider", String.Empty);
                connString = dbConfig.GetString("ConnectionString", String.Empty);
            }

            //
            // Try reading the more specific [InventoryService] section, if it exists
            //
            IConfig authConfig = config.Configs["AuthenticationService"];
            if (authConfig != null)
            {
                dllName = authConfig.GetString("StorageProvider", dllName);
                connString = authConfig.GetString("ConnectionString", connString);

                m_PerformAuthentication = authConfig.GetBoolean("Authenticate", true);
                m_AuthorityURL = "http://" + authConfig.GetString("Authority", "localhost");
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName.Equals(String.Empty))
                throw new Exception("No InventoryService configuration");

            m_Database = LoadPlugin<IUserDataPlugin>(dllName);
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module");

            m_Database.Initialise(connString);
        } 

        /// <summary>
        /// This implementation only authenticates users.
        /// </summary>
        /// <param name="principalID"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool Authenticate(UUID principalID, string password)
        {
            if (!m_PerformAuthentication)
                return true;

            UserProfileData profile = m_Database.GetUserByUUID(principalID);
            bool passwordSuccess = false;
            m_log.InfoFormat("[AUTH]: Authenticating {0} {1} ({2})", profile.FirstName, profile.SurName, profile.ID);

            // we do this to get our hash in a form that the server password code can consume
            // when the web-login-form submits the password in the clear (supposed to be over SSL!)
            if (!password.StartsWith("$1$"))
                password = "$1$" + Util.Md5Hash(password);

            password = password.Remove(0, 3); //remove $1$

            string s = Util.Md5Hash(password + ":" + profile.PasswordSalt);
            // Testing...
            //m_log.Info("[LOGIN]: SubHash:" + s + " userprofile:" + profile.passwordHash);
            //m_log.Info("[LOGIN]: userprofile:" + profile.passwordHash + " SubCT:" + password);

            passwordSuccess = (profile.PasswordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase)
                               || profile.PasswordHash.Equals(password, StringComparison.InvariantCulture));

            return passwordSuccess;
        }

        /// <summary>
        /// This generates authorization keys in the form
        /// http://authority/uuid
        /// after verifying that the caller is, indeed, authorized to request a key
        /// </summary>
        /// <param name="userID">The principal ID requesting the new key</param>
        /// <param name="authToken">The original authorization token for that principal, obtained during login</param>
        /// <returns></returns>
        public string GetKey(UUID principalID, string authToken)
        {
            UserProfileData profile = m_Database.GetUserByUUID(principalID);
            string newKey = string.Empty;

            if (profile != null)
            {
                m_log.DebugFormat("[AUTH]: stored auth token is {0}. Given token is {1}", profile.WebLoginKey.ToString(), authToken);
                // I'm overloading webloginkey for this, so that no changes are needed in the DB
                // The uses of webloginkey are fairly mutually exclusive
                if (profile.WebLoginKey.ToString().Equals(authToken))
                {
                    newKey = UUID.Random().ToString();
                    List<string> keys;
                    lock (m_UserKeys)
                    {
                        if (m_UserKeys.ContainsKey(principalID))
                        {
                            keys = m_UserKeys[principalID];
                        }
                        else
                        {
                            keys = new List<string>();
                            m_UserKeys.Add(principalID, keys);
                        }
                        keys.Add(newKey);
                    }
                    m_log.InfoFormat("[AUTH]: Successfully generated new auth key for {0}", principalID);
                }
                else
                    m_log.Warn("[AUTH]: Unauthorized key generation request. Denying new key.");
            }
            else
                m_log.Warn("[AUTH]: Principal not found.");

            return m_AuthorityURL + newKey;
        }

        /// <summary>
        /// This verifies the uuid portion of the key given out by GenerateKey
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool VerifyKey(UUID userID, string key)
        {
            lock (m_UserKeys)
            {
                if (m_UserKeys.ContainsKey(userID))
                {
                    List<string> keys = m_UserKeys[userID];
                    if (keys.Contains(key))
                    {
                        // Keys are one-time only, so remove it
                        keys.Remove(key);
                        return true;
                    }
                    return false;
                }
                else
                    return false;
            }
        }

        public UUID AllocateUserSession(UUID userID)
        {
            // Not implemented yet
            return UUID.Zero;
        }

        public bool VerifyUserSession(UUID userID, UUID sessionID)
        {
            UserProfileData userProfile = m_Database.GetUserByUUID(userID);

            if (userProfile != null && userProfile.CurrentAgent != null)
            {
                m_log.DebugFormat("[AUTH]: Verifying session {0} for {1}; current  session {2}", sessionID, userID, userProfile.CurrentAgent.SessionID);
                if (userProfile.CurrentAgent.SessionID == sessionID)
                {
                    return true;
                }
            }

            return false;
        }

        public void DestroyUserSession(UUID userID)
        {
            // Not implemented yet
        }
    }
}
