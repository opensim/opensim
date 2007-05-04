using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenGrid.Framework.Data
{
    public class UserProfileData
    {
        string username;    // The configurable part of the users username
        string surname;     // The users surname (can be used to indicate user class - eg 'Test User' or 'Test Admin')

        ulong homeRegion;       // RegionHandle of home
        LLVector3 homeLocation; // Home Location inside the sim

        int created;    // UNIX Epoch Timestamp (User Creation)
        int lastLogin;  // UNIX Epoch Timestamp (Last Login Time)

        string userInventoryURI; // URI to inventory server for this user
        string userAssetURI;     // URI to asset server for this user

        uint profileCanDoMask; // Profile window "I can do" mask
        uint profileWantDoMask; // Profile window "I want to" mask

        string profileAboutText; // My about window text
        string profileFirstText; // First Life Text

        LLUUID profileImage; // My avatars profile image
        LLUUID profileFirstImage; // First-life image


    }
}
