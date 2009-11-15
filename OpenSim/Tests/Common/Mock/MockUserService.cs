
using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Services.Interfaces;

namespace OpenSim.Tests.Common
{        
    public class MockUserService : IUserService
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
