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
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Framework.Communications.Tests
{
    /// <summary>
    /// Asset cache tests
    /// </summary>
    [TestFixture] 
    public class AssetCacheTests
    {
        private class FakeUserService : IUserService
        {
            public void AddTemporaryUserProfile(UserProfileData userProfile)
            {
                throw new NotImplementedException();
            }
            
            public UserProfileData GetUserProfile(string firstName, string lastName)
            {
                throw new NotImplementedException();
            }

            public UserProfileData GetUserProfile(UUID userId)
            {
                throw new NotImplementedException();
            }

            public UserProfileData GetUserProfile(Uri uri)
            {
                UserProfileData userProfile = new UserProfileData();

//                userProfile.ID = new UUID(Util.GetHashGuid(uri.ToString(), AssetCache.AssetInfo.Secret));

                return userProfile;
            }

            public Uri GetUserUri(UserProfileData userProfile)
            {
                throw new NotImplementedException();
            }

            public UserAgentData GetAgentByUUID(UUID userId)
            {
                throw new NotImplementedException();
            }

            public void ClearUserAgent(UUID avatarID)
            {
                throw new NotImplementedException();
            }

            public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID QueryID, string Query)
            {
                throw new NotImplementedException();
            }

            public UserProfileData SetupMasterUser(string firstName, string lastName)
            {
                throw new NotImplementedException();
            }

            public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
            {
                throw new NotImplementedException();
            }

            public UserProfileData SetupMasterUser(UUID userId)
            {
                throw new NotImplementedException();
            }

            public bool UpdateUserProfile(UserProfileData data)
            {
                throw new NotImplementedException();
            }

            public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
            {
                throw new NotImplementedException();
            }

            public void RemoveUserFriend(UUID friendlistowner, UUID friend)
            {
                throw new NotImplementedException();
            }

            public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
            {
                throw new NotImplementedException();
            }

            public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
            {
                throw new NotImplementedException();
            }

            public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
            {
                throw new NotImplementedException();
            }

            public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
            {
                throw new NotImplementedException();
            }

            public bool VerifySession(UUID userID, UUID sessionID)
            {
                return true;
            }

            public void SetInventoryService(IInventoryService inv)
            {
                throw new NotImplementedException();
            }

            public virtual bool AuthenticateUserByPassword(UUID userID, string password)
            {
                throw new NotImplementedException();
            }
        }
    }
}
