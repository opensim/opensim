using System;
using System.Reflection;
using System.Text;
using Nini.Config;
using log4net;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.UserAccountService;
using OpenSim.Data;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

namespace OpenSim.Services.ProfilesService
{
    public class UserProfilesService: UserProfilesServiceBase, IUserProfilesService
    {
        static readonly ILog m_log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
        
        IUserAccountService userAccounts;
        IAuthenticationService authService;

        public UserProfilesService(IConfigSource config, string configName):
            base(config, configName)
        {
            IConfig Config = config.Configs[configName];
            if (Config == null)
            {
                m_log.Warn("[PROFILES]: No configuration found!");
                return;
            }
            Object[] args = null;
            
            args = new Object[] { config };
            string accountService = Config.GetString("UserAccountService", String.Empty);
            if (accountService != string.Empty)
                userAccounts = ServerUtils.LoadPlugin<IUserAccountService>(accountService, args);
            
            args = new Object[] { config };
            string authServiceConfig = Config.GetString("AuthenticationServiceModule", String.Empty);
            if (accountService != string.Empty)
                authService = ServerUtils.LoadPlugin<IAuthenticationService>(authServiceConfig, args);
        }
        
        #region Classifieds
        public OSD AvatarClassifiedsRequest(UUID creatorId)
        {
            OSDArray records = ProfilesData.GetClassifiedRecords(creatorId);
            
            return records;
        }
        
        public bool ClassifiedUpdate(UserClassifiedAdd ad, ref string result)
        {
            if(!ProfilesData.UpdateClassifiedRecord(ad, ref result))
            {
                return false;
            }
            result = "success";
            return true;
        }
        
        public bool ClassifiedDelete(UUID recordId)
        {
            if(ProfilesData.DeleteClassifiedRecord(recordId))
                return true;
            
            return false;
        }
        
        public bool ClassifiedInfoRequest(ref UserClassifiedAdd ad, ref string result)
        {
            if(ProfilesData.GetClassifiedInfo(ref ad, ref result))
                return true;
            
            return false;
        }
        #endregion Classifieds
        
        #region Picks
        public OSD AvatarPicksRequest(UUID creatorId)
        {
            OSDArray records = ProfilesData.GetAvatarPicks(creatorId);
            
            return records;
        }
        
        public bool PickInfoRequest(ref UserProfilePick pick, ref string result)
        {
            pick = ProfilesData.GetPickInfo(pick.CreatorId, pick.PickId);
            result = "OK";
            return true;
        }
        
        public bool PicksUpdate(ref UserProfilePick pick, ref string result)
        {
            return ProfilesData.UpdatePicksRecord(pick);
        }
        
        public bool PicksDelete(UUID pickId)
        {
            return ProfilesData.DeletePicksRecord(pickId);
        }
        #endregion Picks
        
        #region Notes
        public bool AvatarNotesRequest(ref UserProfileNotes note)
        {
            return ProfilesData.GetAvatarNotes(ref note);
        }
        
        public bool NotesUpdate(ref UserProfileNotes note, ref string result)
        {
            return ProfilesData.UpdateAvatarNotes(ref note, ref result);
        }
        #endregion Notes
        
        #region Profile Properties
        public bool AvatarPropertiesRequest(ref UserProfileProperties prop, ref string result)
        {
            return ProfilesData.GetAvatarProperties(ref prop, ref result);
        }
        
        public bool AvatarPropertiesUpdate(ref UserProfileProperties prop, ref string result)
        {
            return ProfilesData.UpdateAvatarProperties(ref prop, ref result);
        }
        #endregion Profile Properties
        
        #region Interests
        public bool AvatarInterestsUpdate(UserProfileProperties prop, ref string result)
        {
            return ProfilesData.UpdateAvatarInterests(prop, ref result);
        }
        #endregion Interests

        #region Utility
        public OSD AvatarImageAssetsRequest(UUID avatarId)
        {
            OSDArray records = ProfilesData.GetUserImageAssets(avatarId);
            return records;
        }
        #endregion Utility

        #region UserData
        public bool RequestUserAppData(ref UserAppData prop, ref string result)
        {
            return ProfilesData.GetUserAppData(ref prop, ref result);
        }
        
        public bool SetUserAppData(UserAppData prop, ref string result)
        {
            return true;
        }
        #endregion UserData
    }
}

