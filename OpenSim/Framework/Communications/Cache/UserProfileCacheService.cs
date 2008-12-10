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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using log4net;

namespace OpenSim.Framework.Communications.Cache
{
    /// <summary>
    /// Holds user profile information and retrieves it from backend services.
    /// </summary>
    public class UserProfileCacheService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The comms manager holds references to services (user, grid, inventory, etc.)
        /// </summary>
        private readonly CommunicationsManager m_commsManager;

        /// <summary>
        /// Each user has a cached profile.
        /// </summary>
        private readonly Dictionary<UUID, CachedUserInfo> m_userProfiles = new Dictionary<UUID, CachedUserInfo>();

        /// <summary>
        /// The root library folder.
        /// </summary>
        public readonly LibraryRootFolder LibraryRoot;

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

        /// <summary>
        /// A new user has moved into a region in this instance so retrieve their profile from the user service.
        /// </summary>
        /// <param name="userID"></param>
        public void AddNewUser(UUID userID)
        {
            if (userID == UUID.Zero)
                return;
            
            m_log.DebugFormat("[USER CACHE]: Adding user profile for {0}", userID);
            GetUserDetails(userID);
        }

        /// <summary>
        /// Remove this user's profile cache.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>true if the user was successfully removed, false otherwise</returns>
        public bool RemoveUser(UUID userId)
        {
            lock (m_userProfiles)
            {
                if (m_userProfiles.ContainsKey(userId))
                {
                    m_log.DebugFormat("[USER CACHE]: Removing user {0}", userId);
                    m_userProfiles.Remove(userId);
                    return true;
                }
            }

            m_log.WarnFormat(
                "[USER CACHE]: Tried to remove the profile of user {0}, but this was not in the scene", userId);

            return false;
        }

        /// <summary>
        /// Request the inventory data for the given user.  This will occur asynchronously if running on a grid
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="userInfo"></param>
        public void RequestInventoryForUser(UUID userID)
        {
            CachedUserInfo userInfo = GetUserDetails(userID);
            if (userInfo != null)
            {
                if (m_commsManager.SecureInventoryService != null)
                {
                    m_commsManager.SecureInventoryService.RequestInventoryForUser(userID, userInfo.SessionID, userInfo.InventoryReceive);
                }
                else
                {
                    m_commsManager.InventoryService.RequestInventoryForUser(userID, userInfo.InventoryReceive);
                }
                //IInventoryServices invService = userInfo.GetInventoryService();
                //if (invService != null)
                //{
                //    invService.RequestInventoryForUser(userID, userInfo.InventoryReceive);
                //}
            }
            else
            {
                m_log.ErrorFormat("[USER CACHE]: RequestInventoryForUser() - user profile for user {0} not found", userID);
            }
        }

        /// <summary>
        /// Get cached details of the given user.  If the user isn't in cache then the user is requested from the 
        /// profile service.  
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>null if no user details are found</returns>
        public CachedUserInfo GetUserDetails(UUID userID)
        {
            if (userID == UUID.Zero)
                return null;

            lock (m_userProfiles)
            {
                if (m_userProfiles.ContainsKey(userID))
                {
                    return m_userProfiles[userID];
                }
                else
                {
                    UserProfileData userprofile = m_commsManager.UserService.GetUserProfile(userID);
                    if (userprofile != null)
                    {
                        CachedUserInfo userinfo = new CachedUserInfo(m_commsManager, userprofile);
                        m_userProfiles.Add(userID, userinfo);
                        return userinfo;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Preloads User data into the region cache. Modules may use this service to add non-standard clients
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="userData"></param>
        public void PreloadUserCache(UUID userID, UserProfileData userData)
        {
            if (userID == UUID.Zero)
                return;

            lock (m_userProfiles)
            {
                if (m_userProfiles.ContainsKey(userID))
                {
                    return;
                }
                else
                {
                    CachedUserInfo userInfo = new CachedUserInfo(m_commsManager, userData);
                    m_userProfiles.Add(userID, userInfo);
                }
            }
        }
    }
}
