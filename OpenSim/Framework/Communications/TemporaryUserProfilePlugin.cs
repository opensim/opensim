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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Data;

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// Plugin for managing temporary user profiles.
    /// </summary>
    public class TemporaryUserProfilePlugin : IUserDataPlugin
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Dictionary<UUID, UserProfileData> m_profiles = new Dictionary<UUID, UserProfileData>();
        
        public string Name { get { return "TemporaryUserProfilePlugin"; } }
        public string Version { get { return "0.1"; } }
        public void Initialise() {}
        public void Initialise(string connect) {}
        public void Dispose() {}
        
        public UserProfileData GetUserByUUID(UUID user)
        {
            //m_log.DebugFormat("[TEMP USER PROFILE]: Received request for {0}", user);
            
            lock (m_profiles)
            {
                if (m_profiles.ContainsKey(user))
                    return m_profiles[user];
                else
                    return null;
            }
        }
        
        public UserProfileData GetUserByName(string fname, string lname)
        {
            // We deliberately don't look up a temporary profile by name so that we don't obscure non-temporary 
            // profiles.
            
            return null;
        }
        
        public virtual void AddTemporaryUserProfile(UserProfileData userProfile)
        {
            //m_log.DebugFormat("[TEMP USER PROFILE]: Adding {0} {1}", userProfile.Name, userProfile.ID);
            
            lock (m_profiles)
            {
                m_profiles[userProfile.ID] = userProfile;
            }
        }
        
        public UserProfileData GetUserByUri(Uri uri) { return null; }
        public List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query) { return null; }
        public UserAgentData GetAgentByUUID(UUID user) { return null; }
        public UserAgentData GetAgentByName(string name) { return null; }
        public UserAgentData GetAgentByName(string fname, string lname) { return null; }
        public void StoreWebLoginKey(UUID agentID, UUID webLoginKey) {}
        public void AddNewUserProfile(UserProfileData user) {}
        public bool UpdateUserProfile(UserProfileData user) { return false; }
        public void AddNewUserAgent(UserAgentData agent) {}
        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms) {}
        public void RemoveUserFriend(UUID friendlistowner, UUID friend) {}
        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms) {}
        public List<FriendListItem> GetUserFriendList(UUID friendlistowner) { return null; }
        public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids) { return null; }
        public bool MoneyTransferRequest(UUID from, UUID to, uint amount) { return false; }
        public bool InventoryTransferRequest(UUID from, UUID to, UUID inventory) { return false; }
        public AvatarAppearance GetUserAppearance(UUID user) { return null; }
        public void UpdateUserAppearance(UUID user, AvatarAppearance appearance) {}
        public void ResetAttachments(UUID userID) {}
        public void LogoutUsers(UUID regionID) {}
    }
}
