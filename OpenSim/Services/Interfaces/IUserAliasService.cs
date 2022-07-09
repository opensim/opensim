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
    public class UserAlias
    {
        public UUID AliasID;
        public UUID UserID = UUID.Zero;
        public string Description;
        public UserAlias()
        {
        }

        public UserAlias(UUID AliasID, UUID UserID, string Description)
        {
            this.AliasID = AliasID;
            this.UserID = UserID;
            this.Description = Description;
        }

        public UserAlias(Dictionary<string, object> kvp)
        {
            if (kvp.ContainsKey("AliasID"))
                UUID.TryParse(kvp["AliasID"].ToString(), out AliasID);
            if (kvp.ContainsKey("UserID"))
                UUID.TryParse(kvp["UserID"].ToString(), out UserID);
            if (kvp.ContainsKey("Description"))
                Description = kvp["Description"].ToString();
        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["AliasID"] = AliasID;
            result["UserID"] = UserID;
            result["Description"] = Description;

            return result;
        }
    }

    public interface IUserAliasService
    {
        /// <summary>
        /// Create a user alias for a local user.  UserID must map to a local user account
        /// </summary>
        /// <param name="AliasID"></param>
        /// <param name="UserID"></param>
        /// <param name="Description"></param>
        /// <returns>UserAlias or NULL</returns>
        UserAlias CreateAlias(UUID AliasID, UUID UserID, string Description);

        /// <summary>
        /// Lookup and return a local user based on an Alias entry if a local 
        /// user exists for this aliasID
        /// </summary>
        /// <param name="aliasID"></param>
        /// <returns>UserAccount or NULL</returns>
        UserAlias GetUserForAlias(UUID aliasID);

        /// <summary>
        /// Given a userid/user on the local grid. lookup and return a
        /// list of all the known Aliases IDs for the user.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        List<UserAlias> GetUserAliases(UUID userID);

        /// <summary>
        /// Delete an existing Alias
        /// </summary>
        /// <param name="aliasID"></param>
        /// <returns>TRUE on success, False on Error</returns>
        bool DeleteAlias(UUID aliasID);
    }
}
