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

        #region User Preferences
        bool UserPreferencesRequest(ref UserPreferences pref, ref string result);
        bool UserPreferencesUpdate(ref UserPreferences pref, ref string result);
        #endregion User Preferences

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

