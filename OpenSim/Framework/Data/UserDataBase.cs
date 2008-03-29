using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Framework.Data
{
    public abstract class UserDataBase : IUserData
    {
        public abstract UserProfileData GetUserByUUID(LLUUID user);
        public abstract UserProfileData GetUserByName(string fname, string lname);        
        public abstract UserAgentData GetAgentByUUID(LLUUID user);
        public abstract UserAgentData GetAgentByName(string name);
        public abstract UserAgentData GetAgentByName(string fname, string lname);
        public abstract void StoreWebLoginKey(LLUUID agentID, LLUUID webLoginKey);
        public abstract void AddNewUserProfile(UserProfileData user);
        public abstract bool UpdateUserProfile(UserProfileData user);
        public abstract void UpdateUserCurrentRegion(LLUUID avatarid, LLUUID regionuuid);
        public abstract void AddNewUserAgent(UserAgentData agent);
        public abstract void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms);
        public abstract void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend);
        public abstract void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms);
        public abstract List<FriendListItem> GetUserFriendList(LLUUID friendlistowner);
        public abstract bool MoneyTransferRequest(LLUUID from, LLUUID to, uint amount);
        public abstract bool InventoryTransferRequest(LLUUID from, LLUUID to, LLUUID inventory);
        public abstract string GetVersion();
        public abstract string getName();
        public abstract void Initialise();
        public abstract List<OpenSim.Framework.AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query);
    }
}
