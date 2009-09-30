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
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Clients;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Region.Communications.Local;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.Communications.Hypergrid
{
    /// <summary>
    /// For the time being, this class is just an identity wrapper around OGS1UserServices, 
    /// so it always fails for foreign users.
    /// Later it needs to talk with the foreign users' user servers.
    /// </summary>
    public class HGUserServices : OGS1UserServices
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private OGS1UserServices m_remoteUserServices;
        private LocalUserServices m_localUserServices;

        // Constructor called when running in grid mode
        public HGUserServices(CommunicationsManager commsManager)
            : base(commsManager)
        {
        }

        // Constructor called when running in standalone
        public HGUserServices(CommunicationsManager commsManager, LocalUserServices local)
            : base(commsManager)
        {
            m_localUserServices = local;
        }

        public override void SetInventoryService(IInventoryService invService)
        {
            base.SetInventoryService(invService);
            if (m_localUserServices != null)
                m_localUserServices.SetInventoryService(invService);
        }

        public override UUID AddUser(
            string firstName, string lastName, string password, string email, uint regX, uint regY, UUID uuid)
        {
            // Only valid to create users locally
            if (m_localUserServices != null)
                return m_localUserServices.AddUser(firstName, lastName, password, email, regX, regY, uuid);

            return UUID.Zero;
        }
        
        public override bool AddUserAgent(UserAgentData agentdata)
        {
            if (m_localUserServices != null)
                return m_localUserServices.AddUserAgent(agentdata);

            return base.AddUserAgent(agentdata);
        }

        public override UserAgentData GetAgentByUUID(UUID userId)
        {
            string url = string.Empty;
            if ((m_localUserServices != null) && !IsForeignUser(userId, out url))
                return m_localUserServices.GetAgentByUUID(userId);

            return base.GetAgentByUUID(userId);
        }

        public override void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            string url = string.Empty;
            if ((m_localUserServices != null) && !IsForeignUser(userid, out url))
                m_localUserServices.LogOffUser(userid, regionid, regionhandle, position, lookat);
            else
                base.LogOffUser(userid, regionid, regionhandle, position, lookat);
        }

        public override UserProfileData GetUserProfile(string firstName, string lastName)
        {
            if (m_localUserServices != null)
                return m_localUserServices.GetUserProfile(firstName, lastName);

            return base.GetUserProfile(firstName, lastName);
        }

        public override List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            if (m_localUserServices != null)
                return m_localUserServices.GenerateAgentPickerRequestResponse(queryID, query);

            return base.GenerateAgentPickerRequestResponse(queryID, query);
        }

        /// <summary>
        /// Get a user profile from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public override UserProfileData GetUserProfile(UUID avatarID)
        {
            //string url = string.Empty;
            // Unfortunately we can't query for foreigners here,
            // because we'll end up in an infinite loop...
            //if ((m_localUserServices != null) && (!IsForeignUser(avatarID, out url)))
            if (m_localUserServices != null)
                return m_localUserServices.GetUserProfile(avatarID);

            return base.GetUserProfile(avatarID);
        }

        public override void ClearUserAgent(UUID avatarID)
        {
            if (m_localUserServices != null)
                m_localUserServices.ClearUserAgent(avatarID);
            else
                base.ClearUserAgent(avatarID);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public override UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            if (m_localUserServices != null)
                return m_localUserServices.SetupMasterUser(firstName, lastName);

            return base.SetupMasterUser(firstName, lastName);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public override UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            if (m_localUserServices != null)
                return m_localUserServices.SetupMasterUser(firstName, lastName, password);

            return base.SetupMasterUser(firstName, lastName, password);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public override UserProfileData SetupMasterUser(UUID uuid)
        {
            if (m_localUserServices != null)
                return m_localUserServices.SetupMasterUser(uuid);

            return base.SetupMasterUser(uuid);
        }

        public override bool ResetUserPassword(string firstName, string lastName, string newPassword)
        {
            if (m_localUserServices != null)
                return m_localUserServices.ResetUserPassword(firstName, lastName, newPassword);
            else
                return base.ResetUserPassword(firstName, lastName, newPassword);
        }

        public override bool UpdateUserProfile(UserProfileData userProfile)
        {
            string url = string.Empty;
            if ((m_localUserServices != null) && (!IsForeignUser(userProfile.ID, out url)))
                return m_localUserServices.UpdateUserProfile(userProfile);

            return base.UpdateUserProfile(userProfile);
        }

        #region IUserServices Friend Methods

        // NOTE: We're still not dealing with foreign user friends

        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public override void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            if (m_localUserServices != null)
                m_localUserServices.AddNewUserFriend(friendlistowner, friend, perms);
            else
                base.AddNewUserFriend(friendlistowner, friend, perms);
        }

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        public override void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            if (m_localUserServices != null)
                m_localUserServices.RemoveUserFriend(friendlistowner, friend);
            else
                base.RemoveUserFriend(friend, friend);
        }

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public override void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            if (m_localUserServices != null)
                m_localUserServices.UpdateUserFriendPerms(friendlistowner, friend, perms);
            else
                base.UpdateUserFriendPerms(friendlistowner, friend, perms);
        }
        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for UUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        public override List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            if (m_localUserServices != null)
                return m_localUserServices.GetUserFriendList(friendlistowner);

            return base.GetUserFriendList(friendlistowner);
        }

        #endregion

        /// Appearance
        public override AvatarAppearance GetUserAppearance(UUID user)
        {
            string url = string.Empty;
            if ((m_localUserServices != null) && (!IsForeignUser(user, out url)))
                return m_localUserServices.GetUserAppearance(user);
            else
                return base.GetUserAppearance(user);
        }

        public override void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            string url = string.Empty;
            if ((m_localUserServices != null) && (!IsForeignUser(user, out url)))
                m_localUserServices.UpdateUserAppearance(user, appearance);
            else
                base.UpdateUserAppearance(user, appearance);
        }

        #region IMessagingService

        public override Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids)
        {
            if (m_localUserServices != null)
                return m_localUserServices.GetFriendRegionInfos(uuids);

            return base.GetFriendRegionInfos(uuids);
        }
        #endregion

        public override bool VerifySession(UUID userID, UUID sessionID)
        {
            string url = string.Empty;
            if ((m_localUserServices != null) && (!IsForeignUser(userID, out url)))
                return m_localUserServices.VerifySession(userID, sessionID);
            else
                return base.VerifySession(userID, sessionID);
        }


        protected override string GetUserServerURL(UUID userID)
        {
            string serverURL = string.Empty;
            if (IsForeignUser(userID, out serverURL))
                return serverURL;

            return m_commsManager.NetworkServersInfo.UserURL;
        }

        public bool IsForeignUser(UUID userID, out string userServerURL)
        {
            userServerURL = m_commsManager.NetworkServersInfo.UserURL;
            CachedUserInfo uinfo = m_commsManager.UserProfileCacheService.GetUserDetails(userID);
            if (uinfo != null)
            {
                if (!HGNetworkServersInfo.Singleton.IsLocalUser(uinfo.UserProfile))
                {
                    userServerURL = ((ForeignUserProfileData)(uinfo.UserProfile)).UserServerURI;
                    return true;
                }
            }
            return false;
        }
    }
}
