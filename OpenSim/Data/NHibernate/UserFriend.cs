using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Data.NHibernate
{
    public class UserFriend
    {
        public UserFriend()
        {
        }

        public UserFriend(UUID userFriendID, UUID ownerID, UUID friendID, uint friendPermissions)
        {
            this.UserFriendID = userFriendID;
            this.OwnerID = ownerID;
            this.FriendID = friendID;
            this.FriendPermissions = friendPermissions;
        }

        private UUID userFriendId;
        public UUID UserFriendID
        { 
            get { return userFriendId; } 
            set { userFriendId = value; } 
        }
        private UUID ownerId;
        public UUID OwnerID
        {
            get { return ownerId; }
            set { ownerId = value; }
        }
        private UUID friendId;
        public UUID FriendID
        {
            get { return friendId; }
            set { friendId = value; }
        }
        private uint friendPermissions;
        public uint FriendPermissions
        {
            get { return friendPermissions; }
            set { friendPermissions = value; }
        }

    }
}
