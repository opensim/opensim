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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenMetaverse;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Communications.OGS1;

namespace OpenSim.Region.Communications.Hypergrid
{
    /// <summary>
    /// For the time being, this class is just an identity wrapper around OGS1UserServices, 
    /// so it always fails for foreign users.
    /// Later it needs to talk with the foreign users' user servers.
    /// </summary>
    public class HGUserServices : IUserService, IAvatarService, IMessagingService
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private HGCommunicationsGridMode m_parent;
        private OGS1UserServices m_remoteUserServices;

        public HGUserServices(HGCommunicationsGridMode parent)
        {
            //m_parent = parent;
            m_remoteUserServices = new OGS1UserServices(parent);
        }

        public UserProfileData ConvertXMLRPCDataToUserProfile(Hashtable data)
        {
            return m_remoteUserServices.ConvertXMLRPCDataToUserProfile(data);
        }

        /// <summary>
        /// Get a user agent from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserAgentData GetAgentByUUID(UUID userId)
        {
            return m_remoteUserServices.GetAgentByUUID(userId);
        }

        public AvatarAppearance ConvertXMLRPCDataToAvatarAppearance(Hashtable data)
        {
            return m_remoteUserServices.ConvertXMLRPCDataToAvatarAppearance(data);
        }

        public List<AvatarPickerAvatar> ConvertXMLRPCDataToAvatarPickerList(UUID queryID, Hashtable data)
        {
            return m_remoteUserServices.ConvertXMLRPCDataToAvatarPickerList(queryID, data);
        }

        public List<FriendListItem> ConvertXMLRPCDataToFriendListItemList(Hashtable data)
        {
            return m_remoteUserServices.ConvertXMLRPCDataToFriendListItemList(data);
        }

        /// <summary>
        /// Logs off a user on the user server
        /// </summary>
        /// <param name="UserID">UUID of the user</param>
        /// <param name="regionID">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="position">final position</param>
        /// <param name="lookat">final lookat</param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            m_remoteUserServices.LogOffUser(userid, regionid, regionhandle, position, lookat);
        }

        /// <summary>
        /// Logs off a user on the user server (deprecated as of 2008-08-27)
        /// </summary>
        /// <param name="UserID">UUID of the user</param>
        /// <param name="regionID">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="posx">final position x</param>
        /// <param name="posy">final position y</param>
        /// <param name="posz">final position z</param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            m_remoteUserServices.LogOffUser(userid, regionid, regionhandle, posx, posy, posz);
        }

        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
        }

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            return m_remoteUserServices.GenerateAgentPickerRequestResponse(queryID, query);
        }

        /// <summary>
        /// Get a user profile from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserProfileData GetUserProfile(string name)
        {
            return m_remoteUserServices.GetUserProfile(name);
        }

        /// <summary>
        /// Get a user profile from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserProfileData GetUserProfile(UUID avatarID)
        {
            return m_remoteUserServices.GetUserProfile(avatarID);
        }

        public void ClearUserAgent(UUID avatarID)
        {
            m_remoteUserServices.ClearUserAgent(avatarID);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return m_remoteUserServices.SetupMasterUser(firstName, lastName);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            return m_remoteUserServices.SetupMasterUser(firstName, lastName, password);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(UUID uuid)
        {
            return m_remoteUserServices.SetupMasterUser(uuid);
        }

        public UUID AddUserProfile(string firstName, string lastName, string pass, uint regX, uint regY)
        {
            return m_remoteUserServices.AddUserProfile(firstName, lastName, pass, regX, regY);
        }
        
        public bool ResetUserPassword(string firstName, string lastName, string newPassword)
        {
            return m_remoteUserServices.ResetUserPassword(firstName, lastName, newPassword);
        }        

        public bool UpdateUserProfile(UserProfileData userProfile)
        {
            return m_remoteUserServices.UpdateUserProfile(userProfile);
        }

        #region IUserServices Friend Methods
        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            m_remoteUserServices.AddNewUserFriend(friendlistowner, friend, perms);
        }

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            m_remoteUserServices.RemoveUserFriend(friend, friend);
        }

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            m_remoteUserServices.UpdateUserFriendPerms(friendlistowner, friend, perms);
        }
        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for UUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            return m_remoteUserServices.GetUserFriendList(friendlistowner);
        }

        #endregion

        /// Appearance
        public AvatarAppearance GetUserAppearance(UUID user)
        {
            return m_remoteUserServices.GetUserAppearance(user);
        }

        public void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            m_remoteUserServices.UpdateUserAppearance(user, appearance);
        }

        #region IMessagingService

        public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids)
        {
            return m_remoteUserServices.GetFriendRegionInfos(uuids);
        }
        #endregion

    }
}
