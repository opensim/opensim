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
using OpenMetaverse;

using OpenSim.Framework;

namespace OpenSim.Services.Interfaces
{
    public class UserAccount
    {
        public UserAccount()
        {
        }

        public UserAccount(UUID principalID)
        {
            PrincipalID = principalID;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenSim.Services.Interfaces.UserAccount"/> class.
        /// This method is used by externasl/3rd party management applications that need us to create a
        /// random UUID for the new user.
        /// </summary>
        /// <param name='scopeID'>
        /// Scope I.
        /// </param>
        /// <param name='firstName'>
        /// First name.
        /// </param>
        /// <param name='lastName'>
        /// Last name.
        /// </param>
        /// <param name='email'>
        /// Email.
        /// </param>
        public UserAccount(UUID scopeID, string firstName, string lastName, string email)
        {
            PrincipalID = UUID.Random();
            ScopeID = scopeID;
            FirstName = firstName;
            LastName = lastName;
            Email = email;
            ServiceURLs = new Dictionary<string, object>();
            Created = Util.UnixTimeSinceEpoch();
        }

        public UserAccount(UUID scopeID, UUID principalID, string firstName, string lastName, string email)
        {
            PrincipalID = principalID;
            ScopeID = scopeID;
            FirstName = firstName;
            LastName = lastName;
            Email = email;
            ServiceURLs = new Dictionary<string, object>();
            Created = Util.UnixTimeSinceEpoch();
        }

        public string FirstName;
        public string LastName;
        public string Email;
        public UUID PrincipalID;
        public UUID ScopeID;
        public int UserLevel;
        public int UserFlags;
        public string UserTitle;
        public string UserCountry;
        public Boolean LocalToGrid = true;

        public Dictionary<string, object> ServiceURLs;

        public int Created;

        public string Name
        {
            get { return FirstName + " " + LastName; }
        }

        public UserAccount(Dictionary<string, object> kvp)
        {
            if (kvp.ContainsKey("FirstName"))
                FirstName = kvp["FirstName"].ToString();
            if (kvp.ContainsKey("LastName"))
                LastName = kvp["LastName"].ToString();
            if (kvp.ContainsKey("Email"))
                Email = kvp["Email"].ToString();
            if (kvp.ContainsKey("PrincipalID"))
                UUID.TryParse(kvp["PrincipalID"].ToString(), out PrincipalID);
            if (kvp.ContainsKey("ScopeID"))
                UUID.TryParse(kvp["ScopeID"].ToString(), out ScopeID);
            if (kvp.ContainsKey("UserLevel"))
                UserLevel = Convert.ToInt32(kvp["UserLevel"].ToString());
            if (kvp.ContainsKey("UserFlags"))
                UserFlags = Convert.ToInt32(kvp["UserFlags"].ToString());
            if (kvp.ContainsKey("UserTitle"))
                UserTitle = kvp["UserTitle"].ToString();
            if (kvp.ContainsKey("UserCountry"))
                UserCountry = kvp["UserCountry"].ToString();
            if (kvp.ContainsKey("LocalToGrid"))
                Boolean.TryParse(kvp["LocalToGrid"].ToString(), out LocalToGrid);

            if (kvp.ContainsKey("Created"))
                Created = Convert.ToInt32(kvp["Created"].ToString());
            if (kvp.ContainsKey("ServiceURLs") && kvp["ServiceURLs"] != null)
            {
                ServiceURLs = new Dictionary<string, object>();
                string str = kvp["ServiceURLs"].ToString();
                if (str != string.Empty)
                {
                    string[] parts = str.Split(new char[] { ';' });
//                    Dictionary<string, object> dic = new Dictionary<string, object>();
                    foreach (string s in parts)
                    {
                        string[] parts2 = s.Split(new char[] { '*' });
                        if (parts2.Length == 2)
                            ServiceURLs[parts2[0]] = parts2[1];
                    }
                }
            }
        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["FirstName"] = FirstName;
            result["LastName"] = LastName;
            result["Email"] = Email;
            result["PrincipalID"] = PrincipalID.ToString();
            result["ScopeID"] = ScopeID.ToString();
            result["Created"] = Created.ToString();
            result["UserLevel"] = UserLevel.ToString();
            result["UserFlags"] = UserFlags.ToString();
            result["UserTitle"] = UserTitle;
            result["UserCountry"] = UserCountry;
            result["LocalToGrid"] = LocalToGrid.ToString();

            string str = string.Empty;
            foreach (KeyValuePair<string, object> kvp in ServiceURLs)
            {
                str += kvp.Key + "*" + (kvp.Value == null ? "" : kvp.Value) + ";";
            }
            result["ServiceURLs"] = str;

            return result;
        }

    };

    public interface IUserAccountService
    {
        UserAccount GetUserAccount(UUID scopeID, UUID userID);
        UserAccount GetUserAccount(UUID scopeID, string FirstName, string LastName);
        UserAccount GetUserAccount(UUID scopeID, string Email);

        /// <summary>
        /// Returns the list of avatars that matches both the search criterion and the scope ID passed
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        List<UserAccount> GetUserAccounts(UUID scopeID, string query);
        List<UserAccount> GetUserAccountsWhere(UUID scopeID, string where);

        /// <summary>
        /// Store the data given, wich replaces the stored data, therefore must be complete.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        bool StoreUserAccount(UserAccount data);

        void InvalidateCache(UUID userID);
    }
}
