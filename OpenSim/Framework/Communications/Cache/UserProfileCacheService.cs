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

using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Framework.Communications.Cache
{
    /// <summary>
    /// Holds user profile information and retrieves it from backend services.
    /// </summary>
    public class UserProfileCacheService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Standard format for names.
        /// </value>
        public const string NAME_FORMAT = "{0} {1}";
        
        /// <summary>
        /// The comms manager holds references to services (user, grid, inventory, etc.)
        /// </summary>
        private readonly CommunicationsManager m_commsManager;

        /// <summary>
        /// User profiles indexed by UUID
        /// </summary>
        private readonly Dictionary<UUID, CachedUserInfo> m_userProfilesById 
            = new Dictionary<UUID, CachedUserInfo>();
        
        /// <summary>
        /// User profiles indexed by name
        /// </summary>
        private readonly Dictionary<string, CachedUserInfo> m_userProfilesByName 
            = new Dictionary<string, CachedUserInfo>();
        
        /// <summary>
        /// The root library folder.
        /// </summary>
        public readonly InventoryFolderImpl LibraryRoot;

        private IInventoryService m_InventoryService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        /// <param name="libraryRootFolder"></param>
        public UserProfileCacheService(CommunicationsManager commsManager, LibraryRootFolder libraryRootFolder)
        {
            m_commsManager = commsManager;
            LibraryRoot = libraryRootFolder;
        }

        public void SetInventoryService(IInventoryService invService)
        {
            m_InventoryService = invService;
        }

        /// <summary>
        /// A new user has moved into a region in this instance so retrieve their profile from the user service.
        /// </summary>
        /// 
        /// It isn't strictly necessary to make this call since user data can be lazily requested later on.  However, 
        /// it might be helpful in order to avoid an initial response delay later on
        /// 
        /// <param name="userID"></param>
        public void AddNewUser(UUID userID)
        {
            if (userID == UUID.Zero)
                return;
            
            //m_log.DebugFormat("[USER CACHE]: Adding user profile for {0}", userID);
            GetUserDetails(userID);
        }

        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        public bool RemoveUser(UUID userId)
        {
            if (!RemoveFromCaches(userId))
            {
                m_log.WarnFormat(
                    "[USER CACHE]: Tried to remove the profile of user {0}, but this was not in the scene", userId);
                
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get details of the given user.
        /// </summary>
        /// If the user isn't in cache then the user is requested from the profile service.
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        public CachedUserInfo GetUserDetails(string fname, string lname)
        {
            lock (m_userProfilesByName)
            {
                CachedUserInfo userInfo;
                
                if (m_userProfilesByName.TryGetValue(string.Format(NAME_FORMAT, fname, lname), out userInfo))
                {
                    return userInfo;
                }
                else
                {
                    UserProfileData userProfile = m_commsManager.UserService.GetUserProfile(fname, lname);
                
                    if (userProfile != null)
                        return AddToCaches(userProfile);
                    else
                        return null;
                }
            }
        }
        
        /// <summary>
        /// Get details of the given user.
        /// </summary>
        /// If the user isn't in cache then the user is requested from the profile service.
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        public CachedUserInfo GetUserDetails(UUID userID)
        {
            if (userID == UUID.Zero)
                return null;

            lock (m_userProfilesById)
            {
                if (m_userProfilesById.ContainsKey(userID))
                {
                    return m_userProfilesById[userID];
                }
                else
                {
                    UserProfileData userProfile = m_commsManager.UserService.GetUserProfile(userID);
                    if (userProfile != null)
                        return AddToCaches(userProfile);
                    else
                        return null;
                }
            }
        }
        
        /// <summary>
        /// Update an existing profile
        /// </summary>
        /// <param name="userProfile"></param>
        /// <returns>true if a user profile was found to update, false otherwise</returns>
        // Commented out for now.  The implementation needs to be improved by protecting against race conditions,
        // probably by making sure that the update doesn't use the UserCacheInfo.UserProfile directly (possibly via
        // returning a read only class from the cache).
//        public bool StoreProfile(UserProfileData userProfile)
//        {
//            lock (m_userProfilesById)
//            {
//                CachedUserInfo userInfo = GetUserDetails(userProfile.ID);
//
//                if (userInfo != null)
//                {
//                    userInfo.m_userProfile = userProfile;
//                    m_commsManager.UserService.UpdateUserProfile(userProfile);
//
//                    return true;
//                }
//            }
//
//            return false;
//        }
        
        /// <summary>
        /// Populate caches with the given user profile
        /// </summary>
        /// <param name="userProfile"></param>
        protected CachedUserInfo AddToCaches(UserProfileData userProfile)
        {
            CachedUserInfo createdUserInfo = new CachedUserInfo(m_InventoryService, userProfile);
            
            lock (m_userProfilesById)
            {
                m_userProfilesById[createdUserInfo.UserProfile.ID] = createdUserInfo;
                
                lock (m_userProfilesByName)
                {
                    m_userProfilesByName[createdUserInfo.UserProfile.Name] = createdUserInfo;
                }
            }
            
            return createdUserInfo;
        }
        
        /// <summary>
        /// Remove profile belong to the given uuid from the caches
        /// </summary>
        /// <param name="userUuid"></param>
        /// <returns>true if there was a profile to remove, false otherwise</returns>
        protected bool RemoveFromCaches(UUID userId)
        {
            lock (m_userProfilesById)
            {
                if (m_userProfilesById.ContainsKey(userId))
                {
                    CachedUserInfo userInfo = m_userProfilesById[userId];
                    m_userProfilesById.Remove(userId);
                    
                    lock (m_userProfilesByName)
                    {
                        m_userProfilesByName.Remove(userInfo.UserProfile.Name);
                    }
                    
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Preloads User data into the region cache. Modules may use this service to add non-standard clients
        /// </summary>
        /// <param name="userData"></param>
        public void PreloadUserCache(UserProfileData userData)
        {
            AddToCaches(userData);
        }
    }
}
