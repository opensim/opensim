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

        public UUID UserFriendID { get; set; }
        public UUID OwnerID { get; set; }
        public UUID FriendID { get; set; }
        public uint FriendPermissions { get; set; }

    }
}
