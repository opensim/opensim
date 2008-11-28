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
using OpenMetaverse;
using log4net;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// This class manages references to OpenSim non-region services (asset, inventory, user, etc.)
    /// </summary>
    public class CommunicationsManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<UUID, string[]> m_nameRequestCache = new Dictionary<UUID, string[]>();

        public IUserService UserService
        {
            get { return m_userService; }
        }
        protected IUserService m_userService;

        public IMessagingService MessageService
        {
            get { return m_messageService; }
        }
        protected IMessagingService m_messageService;

        public IGridServices GridService
        {
            get { return m_gridService; }
        }
        protected IGridServices m_gridService;

        public IInterRegionCommunications InterRegion
        {
            get { return m_interRegion; }
        }
        protected IInterRegionCommunications m_interRegion;

        public UserProfileCacheService UserProfileCacheService
        {
            get { return m_userProfileCacheService; }
        }
        protected UserProfileCacheService m_userProfileCacheService;

        // protected AgentAssetTransactionsManager m_transactionsManager;

        // public AgentAssetTransactionsManager TransactionsManager
        // {
        //     get { return m_transactionsManager; }
        // }

        public IAvatarService AvatarService
        {
            get { return m_avatarService; }
        }
        protected IAvatarService m_avatarService;

        public AssetCache AssetCache
        {
            get { return m_assetCache; }
        }
        protected AssetCache m_assetCache;

        public IInterServiceInventoryServices InterServiceInventoryService
        {
            get { return m_interServiceInventoryService; }
        }
        protected IInterServiceInventoryServices m_interServiceInventoryService;

        public NetworkServersInfo NetworkServersInfo
        {
            get { return m_networkServersInfo; }
        }
        protected NetworkServersInfo m_networkServersInfo;
             
        /// <summary>
        /// Interface to user service for administrating users.
        /// </summary>
        public IUserServiceAdmin UserServiceAdmin
        {
            get { return m_userServiceAdmin; }
        }        
        protected IUserServiceAdmin m_userServiceAdmin;        

        public BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }
        protected BaseHttpServer m_httpServer;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serversInfo"></param>
        /// <param name="httpServer"></param>
        /// <param name="assetCache"></param>
        /// <param name="dumpAssetsToFile"></param>
        public CommunicationsManager(NetworkServersInfo serversInfo, BaseHttpServer httpServer, AssetCache assetCache,
                                     bool dumpAssetsToFile, LibraryRootFolder libraryRootFolder)
        {
            m_networkServersInfo = serversInfo;
            m_assetCache = assetCache;
            m_userProfileCacheService = new UserProfileCacheService(this, libraryRootFolder);
            m_httpServer = httpServer;
         //   m_transactionsManager = new AgentAssetTransactionsManager(this, dumpAssetsToFile);
        }

        #region Inventory
        protected string m_defaultInventoryHost = "default";

        protected List<IInventoryServices> m_inventoryServices = new List<IInventoryServices>();
        // protected IInventoryServices m_inventoryService;
        protected List<ISecureInventoryService> m_secureinventoryServices = new List<ISecureInventoryService>();

        public ISecureInventoryService SecureInventoryService
        {
            get
            {
                if (m_secureinventoryServices.Count > 0)
                {
                    // return m_inventoryServices[0];
                    ISecureInventoryService invService;
                    if (TryGetSecureInventoryService(m_defaultInventoryHost, out invService))
                    {
                        return invService;
                    }
                }
                return null;
            }
        }

        public IInventoryServices InventoryService
        {
            get
            {
                if (m_inventoryServices.Count > 0)
                {
                    // return m_inventoryServices[0];
                    IInventoryServices invService;
                    if (TryGetInventoryService(m_defaultInventoryHost, out invService))
                    {
                        return invService;
                    }
                }
                return null;
            }
        }

        public bool TryGetSecureInventoryService(string host, out ISecureInventoryService inventoryService)
        {
            if ((host == string.Empty) || (host == "default"))
            {
                host = m_defaultInventoryHost;
            }

            lock (m_secureinventoryServices)
            {
                foreach (ISecureInventoryService service in m_secureinventoryServices)
                {
                    if (service.Host == host)
                    {
                        inventoryService = service;
                        return true;
                    }
                }
            }

            inventoryService = null;
            return false;
        }

        public bool TryGetInventoryService(string host, out IInventoryServices inventoryService)
        {
            if ((host == string.Empty) || (host == "default"))
            {
                host = m_defaultInventoryHost;
            }

            lock (m_inventoryServices)
            {
                foreach (IInventoryServices service in m_inventoryServices)
                {
                    if (service.Host == host)
                    {
                        inventoryService = service;
                        return true;
                    }
                }
            }

            inventoryService = null;
            return false;
        }

        public virtual void AddInventoryService(string hostUrl)
        {

        }

        public virtual void AddSecureInventoryService(string hostUrl)
        {

        }

        public virtual void AddSecureInventoryService(ISecureInventoryService service)
        {
            lock (m_secureinventoryServices)
            {
                m_secureinventoryServices.Add(service);
            }
        }

        public virtual void AddInventoryService(IInventoryServices service)
        {
            lock (m_inventoryServices)
            {
                m_inventoryServices.Add(service);
            }
        }

        #endregion

        #region Friend Methods

        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            m_userService.AddNewUserFriend(friendlistowner, friend, perms);
        }

        /// <summary>
        /// Logs off a user and does the appropriate communications
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="position"></param>
        /// <param name="lookat"></param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            m_userService.LogOffUser(userid, regionid, regionhandle, position, lookat);
        }

        /// <summary>
        /// Logs off a user and does the appropriate communications (deprecated as of 2008-08-27)
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="posx"></param>
        /// <param name="posy"></param>
        /// <param name="posz"></param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            m_userService.LogOffUser(userid, regionid, regionhandle, posx, posy, posz);
        }

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            m_userService.RemoveUserFriend(friendlistowner, friend);
        }

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            m_userService.UpdateUserFriendPerms(friendlistowner, friend, perms);
        }

        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for UUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            return m_userService.GetUserFriendList(friendlistowner);
        }

        public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos(List<UUID> uuids)
        {
            return m_messageService.GetFriendRegionInfos(uuids);
        }

        public List<UUID> InformFriendsInOtherRegion(UUID agentId, ulong destRegionHandle, List<UUID> friends, bool online)
        {
            return m_interRegion.InformFriendsInOtherRegion(agentId, destRegionHandle, friends, online);
        }

        public bool TriggerTerminateFriend(ulong regionHandle, UUID agentID, UUID exFriendID)
        {
            return m_interRegion.TriggerTerminateFriend(regionHandle, agentID, exFriendID);
        }

        #endregion

        #region Packet Handlers

        public void UpdateAvatarPropertiesRequest(IClientAPI remote_client, UserProfileData UserProfile)
        {
            m_userService.UpdateUserProfile(UserProfile);
            return;
        }

        public void HandleUUIDNameRequest(UUID uuid, IClientAPI remote_client)
        {
            if (uuid == m_userProfileCacheService.LibraryRoot.Owner)
            {
                remote_client.SendNameReply(uuid, "Mr", "OpenSim");
            }
            else
            {
                string[] names = doUUIDNameRequest(uuid);
                if (names.Length == 2)
                {
                    remote_client.SendNameReply(uuid, names[0], names[1]);
                }

            }
        }

        private string[] doUUIDNameRequest(UUID uuid)
        {
            string[] returnstring = new string[0];
            bool doLookup = false;

            lock (m_nameRequestCache)
            {
                if (m_nameRequestCache.ContainsKey(uuid))
                {
                    returnstring = m_nameRequestCache[uuid];
                }
                else
                {
                    // we don't want to lock the dictionary while we're doing the lookup
                    doLookup = true;
                }
            }

            if (doLookup) {
                UserProfileData profileData = m_userService.GetUserProfile(uuid);
                if (profileData != null)
                {
                    returnstring = new string[2];
                    // UUID profileId = profileData.ID;
                    returnstring[0] = profileData.FirstName;
                    returnstring[1] = profileData.SurName;
                    lock (m_nameRequestCache)
                    {
                        if (!m_nameRequestCache.ContainsKey(uuid))
                            m_nameRequestCache.Add(uuid, returnstring);
                    }
                }
            }
            
            return returnstring;
        }

        public bool UUIDNameCachedTest(UUID uuid)
        {
            lock (m_nameRequestCache)
                return m_nameRequestCache.ContainsKey(uuid);
        }

        public string UUIDNameRequestString(UUID uuid)
        {
            string[] names = doUUIDNameRequest(uuid);
            if (names.Length == 2)
            {
                string firstname = names[0];
                string lastname = names[1];

                return firstname + " " + lastname;

            }
            return "(hippos)";
        }

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> pickerlist = m_userService.GenerateAgentPickerRequestResponse(queryID, query);
            return pickerlist;
        }

        #endregion
    }
}
