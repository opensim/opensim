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
    public abstract class UserDataBase : IUserDataPlugin
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private Dictionary<UUID, AvatarAppearance> aplist = new Dictionary<UUID, AvatarAppearance>();

        public abstract UserProfileData GetUserByUUID(UUID user);
        public abstract UserProfileData GetUserByName(string fname, string lname);
        public abstract UserAgentData GetAgentByUUID(UUID user);
        public abstract UserAgentData GetAgentByName(string name);
        public abstract UserAgentData GetAgentByName(string fname, string lname);
        public UserProfileData GetUserByUri(Uri uri) { return null; }
        public abstract void StoreWebLoginKey(UUID agentID, UUID webLoginKey);
        public abstract void AddNewUserProfile(UserProfileData user);
        
        public virtual void AddTemporaryUserProfile(UserProfileData userProfile)
        {
            // Deliberately blank - database plugins shouldn't store temporary profiles.
        }
        
        public abstract bool UpdateUserProfile(UserProfileData user);
        public abstract void AddNewUserAgent(UserAgentData agent);
        public abstract void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms);
        public abstract void RemoveUserFriend(UUID friendlistowner, UUID friend);
        public abstract void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms);
        public abstract List<FriendListItem> GetUserFriendList(UUID friendlistowner);
        public abstract Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids);
        public abstract bool MoneyTransferRequest(UUID from, UUID to, uint amount);
        public abstract bool InventoryTransferRequest(UUID from, UUID to, UUID inventory);
        public abstract List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query);
        public abstract AvatarAppearance GetUserAppearance(UUID user);
        public abstract void UpdateUserAppearance(UUID user, AvatarAppearance appearance);
        // public virtual AvatarAppearance GetUserAppearance(UUID user) {
        //     AvatarAppearance aa = null;
        //     try {
        //         aa = aplist[user];
        //         m_log.Info("[APPEARANCE] Found appearance for " + user.ToString() + aa.ToString());
        //     } catch (System.Collections.Generic.KeyNotFoundException e) {
        //         m_log.Info("[APPEARANCE] No appearance found for " + user.ToString());
        //     }
        //     return aa;
        // }
        // public virtual void UpdateUserAppearance(UUID user, AvatarAppearance appearance) {
        //     aplist[user] = appearance;
        //     m_log.Info("[APPEARANCE] Setting appearance for " + user.ToString() + appearance.ToString());
        // }
        public abstract void ResetAttachments(UUID userID);

        public abstract void LogoutUsers(UUID regionID);

        public abstract string Version {get;}
        public abstract string Name {get;}
        public abstract void Initialise(string connect);
        public abstract void Initialise();
        public abstract void Dispose();
   }
}
