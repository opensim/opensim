using System;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Services.Interfaces
{
    public interface IUserProfilesService
    {
        #region Classifieds
        OSD AvatarClassifiedsRequest(UUID creatorId);
        bool ClassifiedUpdate(UserClassifiedAdd ad, ref string result);
        bool ClassifiedInfoRequest(ref UserClassifiedAdd ad, ref string result);
        bool ClassifiedDelete(UUID recordId);
        #endregion Classifieds
        
        #region Picks
        OSD AvatarPicksRequest(UUID creatorId);
        bool PickInfoRequest(ref UserProfilePick pick, ref string result);
        bool PicksUpdate(ref UserProfilePick pick, ref string result);
        bool PicksDelete(UUID pickId);
        #endregion Picks
        
        #region Notes
        bool AvatarNotesRequest(ref UserProfileNotes note);
        bool NotesUpdate(ref UserProfileNotes note, ref string result);
        #endregion Notes
        
        #region Profile Properties
        bool AvatarPropertiesRequest(ref UserProfileProperties prop, ref string result);
        bool AvatarPropertiesUpdate(ref UserProfileProperties prop, ref string result);
        #endregion Profile Properties
        
        #region Interests
        bool AvatarInterestsUpdate(UserProfileProperties prop, ref string result);
        #endregion Interests

        #region Utility
        OSD AvatarImageAssetsRequest(UUID avatarId);
        #endregion Utility

        #region UserData
        bool RequestUserAppData(ref UserAppData prop, ref string result);
        bool SetUserAppData(UserAppData prop, ref string result);
        #endregion UserData
    }
}

