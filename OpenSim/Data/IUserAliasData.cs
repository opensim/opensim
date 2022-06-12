/*
 * Copyright (c) 2020 - 2022, Michael Dickson and the OpenSim-NGC contributors.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim-NGC Project nor the
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

namespace OpenSim.Data
{
    // This MUST be a ref type!
    public class UserAliasData
    {
        public int Id = 0;
        public UUID AliasID;
        public UUID UserID = UUID.Zero;
        public string Description;
    }

    /// <summary>
    /// An interface for connecting to the UserAlias datastore
    /// </summary>
    public interface IUserAliasData
    {
        bool Store(UserAliasData data);
        
        UserAliasData Get(int Id);

        /// <summary>
        /// Lookup and return a local user based on an Alias entry if a local 
        /// user exists for this aliasID
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="aliasID"></param>
        /// <returns>UserAccount or NULL</returns>
        UserAliasData GetUserForAlias(UUID aliasID);

        /// <summary>
        /// Giver a userid/user on the local grid. lookup and return a
        /// list of all the known Aliases IDs for the user.
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        List<UserAliasData> GetUserAliases(UUID userID);

        bool Delete(string field, string val);
    }
}
