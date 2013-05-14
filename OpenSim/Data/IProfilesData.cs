using System;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

namespace OpenSim.Data
{

    public interface IProfilesData
    {
        OSDArray GetClassifiedRecords(UUID creatorId);
        bool UpdateClassifiedRecord(UserClassifiedAdd ad, ref string result);
        bool DeleteClassifiedRecord(UUID recordId);
        OSDArray GetAvatarPicks(UUID avatarId);
        UserProfilePick GetPickInfo(UUID avatarId, UUID pickId);
        bool UpdatePicksRecord(UserProfilePick pick);
        bool DeletePicksRecord(UUID pickId);
        bool GetAvatarNotes(ref UserProfileNotes note);
        bool UpdateAvatarNotes(ref UserProfileNotes note, ref string result);
        bool GetAvatarProperties(ref UserProfileProperties props, ref string result);
        bool UpdateAvatarProperties(ref UserProfileProperties props, ref string result);
        bool UpdateAvatarInterests(UserProfileProperties up, ref string result);
        bool GetClassifiedInfo(ref UserClassifiedAdd ad, ref string result);
        bool GetUserAppData(ref UserAppData props, ref string result);
        bool SetUserAppData(UserAppData props, ref string result);
        OSDArray GetUserImageAssets(UUID avatarId);
    }
}

