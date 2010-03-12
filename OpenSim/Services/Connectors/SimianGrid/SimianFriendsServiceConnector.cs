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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Stores and retrieves friend lists from the SimianGrid backend
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class SimianFriendsServiceConnector : IFriendsService, ISharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_serverUrl = String.Empty;

        #region ISharedRegionModule

        public Type ReplaceableInterface { get { return null; } }
        public void RegionLoaded(Scene scene) { }
        public void PostInitialise() { }
        public void Close() { }

        public SimianFriendsServiceConnector() { }
        public string Name { get { return "SimianFriendsServiceConnector"; } }
        public void AddRegion(Scene scene) { if (!String.IsNullOrEmpty(m_serverUrl)) { scene.RegisterModuleInterface<IFriendsService>(this); } }
        public void RemoveRegion(Scene scene) { if (!String.IsNullOrEmpty(m_serverUrl)) { scene.UnregisterModuleInterface<IFriendsService>(this); } }

        #endregion ISharedRegionModule

        public SimianFriendsServiceConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public void Initialise(IConfigSource source)
        {
            IConfig assetConfig = source.Configs["FriendsService"];
            if (assetConfig == null)
            {
                m_log.Info("[FRIENDS CONNECTOR]: FriendsService missing from OpenSim.ini, skipping SimianFriendsServiceConnector");
                return;
            }

            string serviceURI = assetConfig.GetString("FriendsServerURI");
            if (String.IsNullOrEmpty(serviceURI))
            {
                m_log.Error("[FRIENDS CONNECTOR]: No Server URI named in section FriendsService");
                throw new Exception("Friends connector init error");
            }

            m_serverUrl = serviceURI;
        }

        #region IFriendsService

        public FriendInfo[] GetFriends(UUID principalID)
        {
            Dictionary<UUID, FriendInfo> friends = new Dictionary<UUID, FriendInfo>();

            OSDArray friendsArray = GetFriended(principalID);
            OSDArray friendedMeArray = GetFriendedBy(principalID);

            // Load the list of friends and their granted permissions
            for (int i = 0; i < friendsArray.Count; i++)
            {
                OSDMap friendEntry = friendsArray[i] as OSDMap;
                if (friendEntry != null)
                {
                    UUID friendID = friendEntry["Key"].AsUUID();

                    FriendInfo friend = new FriendInfo();
                    friend.PrincipalID = principalID;
                    friend.Friend = friendID.ToString();
                    friend.MyFlags = friendEntry["Value"].AsInteger();
                    friend.TheirFlags = -1;

                    friends[friendID] = friend;
                }
            }

            // Load the permissions those friends have granted to this user
            for (int i = 0; i < friendedMeArray.Count; i++)
            {
                OSDMap friendedMeEntry = friendedMeArray[i] as OSDMap;
                if (friendedMeEntry != null)
                {
                    UUID friendID = friendedMeEntry["OwnerID"].AsUUID();

                    FriendInfo friend;
                    if (friends.TryGetValue(friendID, out friend))
                        friend.TheirFlags = friendedMeEntry["Value"].AsInteger();
                }
            }

            // Convert the dictionary of friends to an array and return it
            FriendInfo[] array = new FriendInfo[friends.Count];
            int j = 0;
            foreach (FriendInfo friend in friends.Values)
                array[j++] = friend;

            return array;
        }

        public bool StoreFriend(UUID principalID, string friend, int flags)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddGeneric" },
                { "OwnerID", principalID.ToString() },
                { "Type", "Friend" },
                { "Key", friend },
                { "Value", flags.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Error("[FRIENDS CONNECTOR]: Failed to store friend " + friend + " for user " + principalID + ": " + response["Message"].AsString());

            return success;
        }

        public bool Delete(UUID principalID, string friend)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "RemoveGeneric" },
                { "OwnerID", principalID.ToString() },
                { "Type", "Friend" },
                { "Key", friend }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Error("[FRIENDS CONNECTOR]: Failed to remove friend " + friend + " for user " + principalID + ": " + response["Message"].AsString());

            return success;
        }

        #endregion IFriendsService

        private OSDArray GetFriended(UUID ownerID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "OwnerID", ownerID.ToString() },
                { "Type", "Friend" }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Entries"] is OSDArray)
            {
                return (OSDArray)response["Entries"];
            }
            else
            {
                m_log.Warn("[FRIENDS CONNECTOR]: Failed to retrieve friends for user " + ownerID + ": " + response["Message"].AsString());
                return new OSDArray(0);
            }
        }

        private OSDArray GetFriendedBy(UUID ownerID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetGenerics" },
                { "Key", ownerID.ToString() },
                { "Type", "Friend" }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["Entries"] is OSDArray)
            {
                return (OSDArray)response["Entries"];
            }
            else
            {
                m_log.Warn("[FRIENDS CONNECTOR]: Failed to retrieve reverse friends for user " + ownerID + ": " + response["Message"].AsString());
                return new OSDArray(0);
            }
        }
    }
}
