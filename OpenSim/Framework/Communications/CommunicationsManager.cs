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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using libsecondlife;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Framework.Data;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Communications
{
    public class CommunicationsManager
    {
        private AssetCache m_assetCache;
        private IGridServices m_gridServer;
        private IInterRegionCommunications m_interRegion;
        private IInventoryServices m_inventoryServer;
        private AssetTransactionManager m_transactionsManager;
        private UserProfileCache m_userProfiles;
        private IUserServices m_userServer;
        private NetworkServersInfo m_networkServersInfo;

        public CommunicationsManager(NetworkServersInfo serversInfo, BaseHttpServer httpServer, AssetCache assetCache)
        {
            this.m_networkServersInfo = serversInfo;
            m_assetCache = assetCache;
            m_userProfiles = new UserProfileCache(this);
            m_transactionsManager = new AssetTransactionManager(this);
        }

        public IUserServices UserServer
        {
            get { return m_userServer; }
            set { m_userServer = value; }
        }

        public IGridServices GridServer
        {
            get { return m_gridServer; }
            set { m_gridServer = value; }
        }

        public IInventoryServices InventoryServer
        {
            get { return m_inventoryServer; }
            set { m_inventoryServer = value; }
        }

        public IInterRegionCommunications InterRegion
        {
            get { return m_interRegion; }
            set { m_interRegion = value; }
        }

        public UserProfileCache UserProfiles
        {
            get { return m_userProfiles; }
            set { m_userProfiles = value; }
        }

        public AssetTransactionManager TransactionsManager
        {
            get { return m_transactionsManager; }
            set { m_transactionsManager = value; }
        }

        public AssetCache AssetCache
        {
            get { return m_assetCache; }
            set { m_assetCache = value; }
        }

        public NetworkServersInfo NetworkServersInfo
        {
            get { return m_networkServersInfo; }
            set { m_networkServersInfo = value; }
        }

        #region Packet Handlers

        public void HandleUUIDNameRequest(LLUUID uuid, IClientAPI remote_client)
        {
            if (uuid == m_userProfiles.libraryRoot.agentID)
            {
                remote_client.SendNameReply(uuid, "Mr", "OpenSim");
            }
            else
            {
                UserProfileData profileData = m_userServer.GetUserProfile(uuid);
                if (profileData != null)
                {
                    LLUUID profileId = profileData.UUID;
                    string firstname = profileData.username;
                    string lastname = profileData.surname;

                    remote_client.SendNameReply(profileId, firstname, lastname);
                }
            }
        }

        #endregion
    }
}