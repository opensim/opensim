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
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Scenes.Tests
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

        public void Initialise() {}
        public void Dispose() {}
        
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

        public bool UpdateUserProfile(UserProfileData user) 
        { 
            m_userProfilesByUuid[user.ID] = user;
            m_userProfilesByName[user.FirstName + " " + user.SurName] = user;
            
            return true;
        }

        public List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query) { return null; }

        public UserAgentData GetAgentByUUID(UUID user) { return null; }

        public UserAgentData GetAgentByName(string name) { return null; }

        public UserAgentData GetAgentByName(string fname, string lname) { return null; }

        public void StoreWebLoginKey(UUID agentID, UUID webLoginKey) {}

        public void AddNewUserAgent(UserAgentData agent) {}

        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms) {}
        
        public void RemoveUserFriend(UUID friendlistowner, UUID friend) {}

        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms) {}

        public List<FriendListItem> GetUserFriendList(UUID friendlistowner) { return null; }
        
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