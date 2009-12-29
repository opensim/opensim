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

namespace OpenSim.Services.Interfaces
{
    public class UserAccount
    {
        public UserAccount()
        {
        }

        public UserAccount(UUID userID)
        {
            UserID = userID;
        }

        public string FirstName;
        public string LastName;
        public string Email;
        public UUID UserID;
        public UUID ScopeID;

        // These are here because they
        // concern the account rather than
        // the profile. They just happen to
        // be used in the Linden profile as well
        //
        public int GodLevel;
        public int UserFlags;
        public string AccountType;

        public DateTime Created;

        public UserAccount(Dictionary<string, object> kvp)
        {
            if (kvp.ContainsKey("FirstName"))
                FirstName = kvp["FirstName"].ToString();
            if (kvp.ContainsKey("LastName"))
                LastName = kvp["LastName"].ToString();
            if (kvp.ContainsKey("Email"))
                Email = kvp["Email"].ToString();
            if (kvp.ContainsKey("UserID"))
                UUID.TryParse(kvp["UserID"].ToString(), out UserID);
            if (kvp.ContainsKey("ScopeID"))
                UUID.TryParse(kvp["ScopeID"].ToString(), out ScopeID);
            if (kvp.ContainsKey("GodLevel"))
                Int32.TryParse(kvp["GodLevel"].ToString(), out GodLevel);
            if (kvp.ContainsKey("UserFlags"))
                Int32.TryParse(kvp["UserFlags"].ToString(), out UserFlags);
            if (kvp.ContainsKey("AccountType"))
                AccountType = kvp["AccountType"].ToString();
            if (kvp.ContainsKey("Created"))
                DateTime.TryParse(kvp["Created"].ToString(), out Created);

        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["FirstName"] = FirstName;
            result["LastName"] = LastName;
            result["Email"] = Email;
            result["UserID"] = UserID.ToString();
            result["ScopeID"] = ScopeID.ToString();
            result["GodLevel"] = GodLevel.ToString();
            result["UserFlags"] = UserFlags.ToString();
            result["AccountType"] = AccountType.ToString();
            result["Created"] = Created.ToString();

            return result;
        }
    };

    public interface IUserAccountService
    {
        UserAccount GetUserAccount(UUID scopeID, UUID userID);
        UserAccount GetUserAccount(UUID scopeID, string FirstName, string LastName);
        // Returns the list of avatars that matches both the search
        // criterion and the scope ID passed
        //
        List<UserAccount> GetUserAccount(UUID scopeID, string query);

        // Update all updatable fields
        //
        bool SetUserAccount(UserAccount data, UUID PrincipalID, string token);
        
        // Creates a user data record
        bool CreateUserAccount(UserAccount data, UUID PrincipalID, string token);
    }
}
