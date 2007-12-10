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
* 
*/
using System;
using libsecondlife;
using OpenSim.Framework.Communications.Cache;
using System.Collections.Generic;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Communications
{
    public class CommunicationsManager
    {
        protected IUserService m_userService;

        public IUserService UserService
        {
            get { return m_userService; }
        }

        protected IGridServices m_gridService;

        public IGridServices GridService
        {
            get { return m_gridService; }
        }

        protected IInventoryServices m_inventoryService;

        public IInventoryServices InventoryService
        {
            get { return m_inventoryService; }
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

        protected AssetTransactionManager m_transactionsManager;

        public AssetTransactionManager TransactionsManager
        {
            get { return m_transactionsManager; }
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
            m_transactionsManager = new AssetTransactionManager(this, dumpAssetsToFile);
        }

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
                    {
                        firstName = MainLog.Instance.CmdPrompt("First name", "Default");
                        lastName = MainLog.Instance.CmdPrompt("Last name", "User");
                        password = MainLog.Instance.PasswdPrompt("Password");
                        regX = Convert.ToUInt32(MainLog.Instance.CmdPrompt("Start Region X", "1000"));
                        regY = Convert.ToUInt32(MainLog.Instance.CmdPrompt("Start Region Y", "1000"));
                    }
                    else
                    {
                        firstName = cmmdParams[1];
                        lastName = cmmdParams[2];
                        password = cmmdParams[3];
                        regX = Convert.ToUInt32(cmmdParams[4]);
                        regY = Convert.ToUInt32(cmmdParams[5]);
                    }

                    AddUser(firstName, lastName, password, regX, regY);
                    break;
            }
        }

        public LLUUID AddUser(string firstName, string lastName, string password, uint regX, uint regY)
        {
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + "");

            m_userService.AddUserProfile(firstName, lastName, md5PasswdHash, regX, regY);
            UserProfileData userProf = UserService.GetUserProfile(firstName, lastName);
            if (userProf == null)
            {
                return LLUUID.Zero;
            }
            else
            {
                m_inventoryService.CreateNewUserInventory(userProf.UUID);
                System.Console.WriteLine("Created new inventory set for " + firstName + " " + lastName);
                return userProf.UUID;
            }
        }

        #region Packet Handlers

        public void HandleUUIDNameRequest(LLUUID uuid, IClientAPI remote_client)
        {
            if (uuid == m_userProfileCacheService.libraryRoot.agentID)
            {
                remote_client.SendNameReply(uuid, "Mr", "OpenSim");
            }
            else
            {
                UserProfileData profileData = m_userService.GetUserProfile(uuid);
                if (profileData != null)
                {
                    LLUUID profileId = profileData.UUID;
                    string firstname = profileData.username;
                    string lastname = profileData.surname;

                    remote_client.SendNameReply(profileId, firstname, lastname);
                }
            }
        }
        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> pickerlist = m_userService.GenerateAgentPickerRequestResponse(queryID, query);  
            return pickerlist;
        }

        #endregion
    }
}
