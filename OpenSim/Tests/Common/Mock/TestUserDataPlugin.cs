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
using OpenSim.Data;

namespace OpenSim.Tests.Common.Mock
{
    /// <summary>
    /// In memory user data provider.  Might be quite useful as a proper user data plugin, though getting mono addins
    /// to load any plugins when running unit tests has proven impossible so far.  Currently no locking since unit
    /// tests are single threaded.
    /// </summary>
    public class TestUserDataPlugin : IUserDataPlugin
    {
        public string Version { get { return "0"; } }
        public string Name { get { return "TestUserDataPlugin"; } }

        /// <summary>
        /// User profiles keyed by name
        /// </summary>
        private Dictionary<string, UserProfileData> m_userProfilesByName = new Dictionary<string, UserProfileData>();

        /// <summary>
        /// User profiles keyed by uuid
        /// </summary>
        private Dictionary<UUID, UserProfileData> m_userProfilesByUuid = new Dictionary<UUID, UserProfileData>();

        /// <summary>
        /// User profiles and their agents
        /// </summary>
        private Dictionary<UUID, UserAgentData> m_agentByProfileUuid = new Dictionary<UUID, UserAgentData>();

        /// <summary>
        /// Friends list by uuid
        /// </summary>
        private Dictionary<UUID, List<FriendListItem>> m_friendsListByUuid = new Dictionary<UUID, List<FriendListItem>>();

        public void Initialise() {}
        public void Dispose() {}
        
        public void AddTemporaryUserProfile(UserProfileData userProfile)
        {
            // Not interested
        }

        public void AddNewUserProfile(UserProfileData user)
        {
            UpdateUserProfile(user);
        }

        public UserProfileData GetUserByUUID(UUID user)
        {
            UserProfileData userProfile = null;
            m_userProfilesByUuid.TryGetValue(user, out userProfile);

            return userProfile;
        }

        public UserProfileData GetUserByName(string fname, string lname)
        {
            UserProfileData userProfile = null;
            m_userProfilesByName.TryGetValue(fname + " " + lname, out userProfile);

            return userProfile;
        }
        
        public UserProfileData GetUserByUri(Uri uri) { return null; }

        public bool UpdateUserProfile(UserProfileData user)
        {
            m_userProfilesByUuid[user.ID] = user;
            m_userProfilesByName[user.FirstName + " " + user.SurName] = user;

            return true;
        }

        public List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query) { return null; }

        public UserAgentData GetAgentByUUID(UUID user)
        {
            UserAgentData userAgent = null;
            m_agentByProfileUuid.TryGetValue(user, out userAgent);

            return userAgent;
        }

        public UserAgentData GetAgentByName(string name)
        {
            UserProfileData userProfile = null;
            m_userProfilesByName.TryGetValue(name, out userProfile);
            UserAgentData userAgent = null;
            m_agentByProfileUuid.TryGetValue(userProfile.ID, out userAgent);

            return userAgent;
        }

        public UserAgentData GetAgentByName(string fname, string lname)
        {
            UserProfileData userProfile = GetUserByName(fname,lname);
            UserAgentData userAgent = null;
            m_agentByProfileUuid.TryGetValue(userProfile.ID, out userAgent);

            return userAgent;
        }

        public void StoreWebLoginKey(UUID agentID, UUID webLoginKey) {}

        public void AddNewUserAgent(UserAgentData agent)
        {
            m_agentByProfileUuid[agent.ProfileID] = agent;
        }
        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            FriendListItem newfriend = new FriendListItem();
            newfriend.FriendPerms = perms;
            newfriend.Friend = friend;
            newfriend.FriendListOwner = friendlistowner;

            if (!m_friendsListByUuid.ContainsKey(friendlistowner))
            {
                List<FriendListItem> friendslist = new List<FriendListItem>();
                m_friendsListByUuid[friendlistowner] = friendslist;

            }
            m_friendsListByUuid[friendlistowner].Add(newfriend);
        }

        public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            if (m_friendsListByUuid.ContainsKey(friendlistowner))
            {
                List<FriendListItem> friendslist = m_friendsListByUuid[friendlistowner];
                foreach (FriendListItem frienditem in friendslist)
                {
                    if (frienditem.Friend == friend)
                    {
                        friendslist.Remove(frienditem);
                        break;
                    }
                }
            }
        }

        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            if (m_friendsListByUuid.ContainsKey(friendlistowner))
            {
                List<FriendListItem> friendslist = m_friendsListByUuid[friendlistowner];
                foreach (FriendListItem frienditem in friendslist)
                {
                    if (frienditem.Friend == friend)
                    {
                        frienditem.FriendPerms = perms;
                        break;
                    }
                }
            }
        }

        public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            if (m_friendsListByUuid.ContainsKey(friendlistowner))
            {
                return m_friendsListByUuid[friendlistowner];
            }
            else
                return new List<FriendListItem>();


        }

        public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids) { return null; }

        public bool MoneyTransferRequest(UUID from, UUID to, uint amount) { return false; }

        public bool InventoryTransferRequest(UUID from, UUID to, UUID inventory) { return false; }

        public void Initialise(string connect) { return; }

        public AvatarAppearance GetUserAppearance(UUID user) { return null; }

        public void UpdateUserAppearance(UUID user, AvatarAppearance appearance) {}

        public void ResetAttachments(UUID userID) {}

        public void LogoutUsers(UUID regionID) {}
    }
}
