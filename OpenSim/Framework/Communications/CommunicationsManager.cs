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
using libsecondlife;
using log4net;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Communications
{
    public class CommunicationsManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IUserService m_userService;
        protected Dictionary<LLUUID, string[]> m_nameRequestCache = new Dictionary<LLUUID, string[]>();

        public IUserService UserService
        {
            get { return m_userService; }
        }

        protected IGridServices m_gridService;

        public IGridServices GridService
        {
            get { return m_gridService; }
        }


        protected IInterRegionCommunications m_interRegion;

        public IInterRegionCommunications InterRegion
        {
            get { return m_interRegion; }
        }

        protected UserProfileCacheService m_userProfileCacheService;

        public UserProfileCacheService UserProfileCacheService
        {
            get { return m_userProfileCacheService; }
        }

      //  protected AgentAssetTransactionsManager m_transactionsManager;

       // public AgentAssetTransactionsManager TransactionsManager
      //  {
      //      get { return m_transactionsManager; }
      //  }

        protected IAvatarService m_avatarService;

        public IAvatarService AvatarService
        {
            get { return m_avatarService; }
        }

        protected AssetCache m_assetCache;

        public AssetCache AssetCache
        {
            get { return m_assetCache; }
        }

        protected NetworkServersInfo m_networkServersInfo;

        public NetworkServersInfo NetworkServersInfo
        {
            get { return m_networkServersInfo; }
        }

        public CommunicationsManager(NetworkServersInfo serversInfo, BaseHttpServer httpServer, AssetCache assetCache,
                                     bool dumpAssetsToFile)
        {
            m_networkServersInfo = serversInfo;
            m_assetCache = assetCache;
            m_userProfileCacheService = new UserProfileCacheService(this);
         //   m_transactionsManager = new AgentAssetTransactionsManager(this, dumpAssetsToFile);
        }

        #region Inventory
        protected string m_defaultInventoryHost = "default";

        protected List<IInventoryServices> m_inventoryServices = new List<IInventoryServices>();
        // protected IInventoryServices m_inventoryService;

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

        public virtual void AddInventoryService(IInventoryServices service)
        {
            lock (m_inventoryServices)
            {
                m_inventoryServices.Add(service);
            }
        }

        #endregion

        public void doCreate(string[] cmmdParams)
        {
            switch (cmmdParams[0])
            {
                case "user":
                    string firstName;
                    string lastName;
                    string password;
                    uint regX = 1000;
                    uint regY = 1000;

                    if (cmmdParams.Length < 2)
                        firstName = MainConsole.Instance.CmdPrompt("First name", "Default");
                    else firstName = cmmdParams[1];

                    if ( cmmdParams.Length < 3 )
                        lastName = MainConsole.Instance.CmdPrompt("Last name", "User");
                    else lastName = cmmdParams[2];

                    if ( cmmdParams.Length < 4 )
                        password = MainConsole.Instance.PasswdPrompt("Password");
                    else password = cmmdParams[3];

                    if ( cmmdParams.Length < 5 )
                        regX = Convert.ToUInt32(MainConsole.Instance.CmdPrompt("Start Region X", regX.ToString()));
                    else regX = Convert.ToUInt32(cmmdParams[4]);

                    if ( cmmdParams.Length < 6 )
                        regY = Convert.ToUInt32(MainConsole.Instance.CmdPrompt("Start Region Y", regY.ToString()));
                    else regY = Convert.ToUInt32(cmmdParams[5]);        


                    if (null == m_userService.GetUserProfile(firstName, lastName))
                    {
                        AddUser(firstName, lastName, password, regX, regY);
                    }
                    else
                    {
                        m_log.ErrorFormat("[USERS]: A user with the name {0} {1} already exists!", firstName, lastName);
                    }
                    break;
            }
        }

        /// <summary>
        /// Persistently adds a user to OpenSim.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="password"></param>
        /// <param name="regX"></param>
        /// <param name="regY"></param>
        /// <returns>The UUID of the added user.  Returns null if the add was unsuccessful</returns>
        public LLUUID AddUser(string firstName, string lastName, string password, uint regX, uint regY)
        {
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + String.Empty);

            m_userService.AddUserProfile(firstName, lastName, md5PasswdHash, regX, regY);
            UserProfileData userProf = UserService.GetUserProfile(firstName, lastName);
            if (userProf == null)
            {
                return LLUUID.Zero;
            }
            else
            {
                InventoryService.CreateNewUserInventory(userProf.ID);
                m_log.Info("[USERS]: Created new inventory set for " + firstName + " " + lastName);
                return userProf.ID;
            }
        }

        #region Friend Methods

        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            m_userService.AddNewUserFriend(friendlistowner, friend, perms);
        }

        /// <summary>
        /// Logs off a user and does the appropriate communications
        /// </summary>
        /// <param name="userid"></param>
        /// <param name="regionid"></param>
        /// <param name="regionhandle"></param>
        /// <param name="posx"></param>
        /// <param name="posy"></param>
        /// <param name="posz"></param>
        public void LogOffUser(LLUUID userid, LLUUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            m_userService.LogOffUser(userid, regionid, regionhandle, posx, posy, posz);

        }

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        public void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend)
        {
            m_userService.RemoveUserFriend(friendlistowner, friend);
        }

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            m_userService.UpdateUserFriendPerms(friendlistowner, friend, perms);
        }

        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for LLUUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        public List<FriendListItem> GetUserFriendList(LLUUID friendlistowner)
        {
            return m_userService.GetUserFriendList(friendlistowner);
        }

        #endregion

        #region Packet Handlers

        public void UpdateAvatarPropertiesRequest(IClientAPI remote_client, UserProfileData UserProfile)
        {
            m_userService.UpdateUserProfileProperties(UserProfile);
            return;
        }

        public void HandleUUIDNameRequest(LLUUID uuid, IClientAPI remote_client)
        {
            if (uuid == m_userProfileCacheService.libraryRoot.Owner)
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

        private string[] doUUIDNameRequest(LLUUID uuid)
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
                    // LLUUID profileId = profileData.ID;
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

        public bool UUIDNameCachedTest(LLUUID uuid)
        {
            lock (m_nameRequestCache)
                return m_nameRequestCache.ContainsKey(uuid);
        }

        public string UUIDNameRequestString(LLUUID uuid)
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

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> pickerlist = m_userService.GenerateAgentPickerRequestResponse(queryID, query);
            return pickerlist;
        }

        #endregion
    }
}
