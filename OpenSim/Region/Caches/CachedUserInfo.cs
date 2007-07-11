using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Data;
using libsecondlife;

namespace OpenSim.Region.Caches
{
    public class CachedUserInfo
    {
        public UserProfileData UserProfile;
        //public Dictionary<LLUUID, InventoryFolder> Folders = new Dictionary<LLUUID, InventoryFolder>();
        public InventoryFolder RootFolder;

        public CachedUserInfo()
        {

        }
    }
}
