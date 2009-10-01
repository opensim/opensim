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
using OpenSim.Services.Interfaces;

namespace OpenSim.Framework.Communications
{
    public interface IUserService
    {
        /// <summary>
        /// Add a temporary user profile.
        /// </summary>
        /// A temporary user profile is one that should exist only for the lifetime of the process.
        /// <param name="userProfile"></param>
        void AddTemporaryUserProfile(UserProfileData userProfile);
        
        /// <summary>
        /// Loads a user profile by name
        /// </summary>
        /// <param name="firstName">First name</param>
        /// <param name="lastName">Last name</param>
        /// <returns>A user profile.  Returns null if no profile is found</returns>
        UserProfileData GetUserProfile(string firstName, string lastName);

        /// <summary>
        /// Loads a user profile from a database by UUID
        /// </summary>
        /// <param name="userId">The target UUID</param>
        /// <returns>A user profile.  Returns null if no user profile is found.</returns>
        UserProfileData GetUserProfile(UUID userId);
        
        UserProfileData GetUserProfile(Uri uri);

        Uri GetUserUri(UserProfileData userProfile);

        UserAgentData GetAgentByUUID(UUID userId);

        void ClearUserAgent(UUID avatarID);
        List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID QueryID, string Query);

        UserProfileData SetupMasterUser(string firstName, string lastName);
        UserProfileData SetupMasterUser(string firstName, string lastName, string password);
        UserProfileData SetupMasterUser(UUID userId);

        /// <summary>
        /// Update the user's profile.
        /// </summary>
        /// <param name="data">UserProfileData object with updated data. Should be obtained 
        ///                    via a call to GetUserProfile().</param>
        /// <returns>true if the update could be applied, false if it could not be applied.</returns>
        bool UpdateUserProfile(UserProfileData data);

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
        /// Logs off a user on the user server
        /// </summary>
        /// <param name="userid">UUID of the user</param>
        /// <param name="regionid">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="position">final position</param>
        /// <param name="lookat">final lookat</param>
        void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat);

        /// <summary>
        /// Logs off a user on the user server (deprecated as of 2008-08-27)
        /// </summary>
        /// <param name="userid">UUID of the user</param>
        /// <param name="regionid">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="posx">final position x</param>
        /// <param name="posy">final position y</param>
        /// <param name="posz">final position z</param>
        void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz);

        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship 
        /// for UUID friendslistowner
        /// </summary>
        /// 
        /// <param name="friendlistowner">The agent for whom we're retreiving the friends Data.</param>
        /// <returns>
        /// A List of FriendListItems that contains info about the user's friends.
        /// Always returns a list even if the user has no friends
        /// </returns>
        List<FriendListItem> GetUserFriendList(UUID friendlistowner);

        // This probably shouldn't be here, it belongs to IAuthentication
        // But since Scenes only have IUserService references, I'm placing it here for now.
        bool VerifySession(UUID userID, UUID sessionID);

        /// <summary>
        /// Authenticate a user by their password.
        /// </summary>
        /// 
        /// This is used by callers outside the login process that want to
        /// verify a user who has given their password.
        ///
        /// This should probably also be in IAuthentication but is here for the same reasons as VerifySession() is
        ///
        /// <param name="userID"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        bool AuthenticateUserByPassword(UUID userID, string password);

        // Temporary Hack until we move everything to the new service model
        void SetInventoryService(IInventoryService invService);
    }
}
