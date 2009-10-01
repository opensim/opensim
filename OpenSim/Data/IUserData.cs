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

namespace OpenSim.Data
{
    /// <summary>
    /// An interface for connecting to user storage servers.
    /// </summary>
    public interface IUserDataPlugin : IPlugin
    {
        /// <summary>
        /// Returns a user profile from a database via their UUID
        /// </summary>
        /// <param name="user">The user's UUID</param>
        /// <returns>The user data profile.  Returns null if no user is found</returns>
        UserProfileData GetUserByUUID(UUID user);

        /// <summary>
        /// Returns a users profile by searching their username parts
        /// </summary>
        /// <param name="fname">Account firstname</param>
        /// <param name="lname">Account lastname</param>
        /// <returns>The user data profile.  Null if no user is found</returns>
        UserProfileData GetUserByName(string fname, string lname);
        
        /// <summary>
        /// Get a user from a given uri.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns>The user data profile.  Null if no user is found.</returns>
        UserProfileData GetUserByUri(Uri uri);

        /// <summary>
        /// Returns a list of UUIDs firstnames and lastnames that match string query entered into the avatar picker.
        /// </summary>
        /// <param name="queryID">ID associated with the user's query. This must match what the client sent</param>
        /// <param name="query">The filtered contents of the search box when the user hit search.</param>
        /// <returns>A list of user details.  If there are no results than either an empty list or null</returns>
        List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query);

        /// <summary>
        /// Returns the current agent for a user searching by it's UUID
        /// </summary>
        /// <param name="user">The users UUID</param>
        /// <returns>The current agent session.  Null if no session was found</returns>
        UserAgentData GetAgentByUUID(UUID user);

        /// <summary>
        /// Returns the current session agent for a user searching by username
        /// </summary>
        /// <param name="name">The users account name</param>
        /// <returns>The current agent session</returns>
        UserAgentData GetAgentByName(string name);

        /// <summary>
        /// Returns the current session agent for a user searching by username parts
        /// </summary>
        /// <param name="fname">The users first account name</param>
        /// <param name="lname">The users account surname</param>
        /// <returns>The current agent session</returns>
        UserAgentData GetAgentByName(string fname, string lname);

        /// <summary>
        /// Stores new web-login key for user during web page login
        /// </summary>
        /// <param name="webLoginKey"></param>
        void StoreWebLoginKey(UUID agentID, UUID webLoginKey);

        /// <summary>
        /// Adds a new User profile to the database
        /// </summary>
        /// <param name="user">UserProfile to add</param>
        void AddNewUserProfile(UserProfileData user);

        /// <summary>
        /// Adds a temporary user profile.  A temporary userprofile is one that should exist only for the lifetime of
        /// the process.
        /// </summary>
        /// <param name="userProfile"></param>
        void AddTemporaryUserProfile(UserProfileData userProfile);

        /// <summary>
        /// Updates an existing user profile
        /// </summary>
        /// <param name="user">UserProfile to update</param>
        bool UpdateUserProfile(UserProfileData user);

        /// <summary>
        /// Adds a new agent to the database
        /// </summary>
        /// <param name="agent">The agent to add</param>
        void AddNewUserAgent(UserAgentData agent);

        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms);

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        void RemoveUserFriend(UUID friendlistowner, UUID friend);

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms);

        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for UUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        /// <returns>The user's friends.  If there are no results than either an empty list or null</returns>
        List<FriendListItem> GetUserFriendList(UUID friendlistowner);

        /// <summary>
        /// Returns a list of <see cref="FriendRegionInfo/>s for the specified UUIDs.
        /// </summary>
        /// <param name="uuids">
        /// A <see cref="List"/> of <see cref="UUID/>s to fetch info for
        /// </param>
        /// <returns>
        /// A <see cref="Dictionary"/>, mapping the <see cref="UUID"/>s to <see cref="FriendRegionInfo"/>s.
        /// </returns>
        Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids);

        /// <summary>
        /// Attempts to move currency units between accounts (NOT RELIABLE / TRUSTWORTHY. DONT TRY RUN YOUR OWN CURRENCY EXCHANGE WITH REAL VALUES)
        /// </summary>
        /// <param name="from">The account to transfer from</param>
        /// <param name="to">The account to transfer to</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>Successful?</returns>
        bool MoneyTransferRequest(UUID from, UUID to, uint amount);

        /// <summary>
        /// Attempts to move inventory between accounts, if inventory is copyable it will be copied into the target account.
        /// </summary>
        /// <param name="from">User to transfer from</param>
        /// <param name="to">User to transfer to</param>
        /// <param name="inventory">Specified inventory item</param>
        /// <returns>Successful?</returns>
        bool InventoryTransferRequest(UUID from, UUID to, UUID inventory);

        /// <summary>
        /// Initialises the plugin (artificial constructor)
        /// </summary>
        void Initialise(string connect);

        /// <summary>
        /// Gets the user appearance
        /// </summer>
        AvatarAppearance GetUserAppearance(UUID user);

        void UpdateUserAppearance(UUID user, AvatarAppearance appearance);

        void ResetAttachments(UUID userID);

        void LogoutUsers(UUID regionID);
    }

    public class UserDataInitialiser : PluginInitialiserBase
    {
        private string connect;
        public UserDataInitialiser (string s) { connect = s; }
        public override void Initialise (IPlugin plugin)
        {
            IUserDataPlugin p = plugin as IUserDataPlugin;
            p.Initialise (connect);
        }
    }
}
