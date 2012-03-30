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
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Statistics;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

namespace OpenSim.Region.OptionalModules.Avatar.Friends
{
    /// <summary>
    /// A module that just holds commands for inspecting avatar appearance.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "FriendsCommandModule")]
    public class FriendsCommandsModule : ISharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private IFriendsModule m_friendsModule;
        private IUserManagement m_userManagementModule;

//        private IAvatarFactoryModule m_avatarFactory;
        
        public string Name { get { return "Appearance Information Module"; } }        
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
//            m_log.DebugFormat("[FRIENDS COMMAND MODULE]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
//            m_log.DebugFormat("[FRIENDS COMMAND MODULE]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
//            m_log.DebugFormat("[FRIENDS COMMAND MODULE]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
//            m_log.DebugFormat("[FRIENDS COMMANDO MODULE]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }
        
        public void RemoveRegion(Scene scene)
        {
//            m_log.DebugFormat("[FRIENDS COMMAND MODULE]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }

        public void RegionLoaded(Scene scene)
        {
//            m_log.DebugFormat("[APPEARANCE INFO MODULE]: REGION {0} LOADED", scene.RegionInfo.RegionName);

            if (m_scene == null)
                m_scene = scene;

            m_friendsModule = m_scene.RequestModuleInterface<IFriendsModule>();
            m_userManagementModule = m_scene.RequestModuleInterface<IUserManagement>();

            if (m_friendsModule != null && m_userManagementModule != null)
            {
                m_scene.AddCommand(
                    "Friends", this, "friends show cache",
                    "friends show cache [<first-name> <last-name>]",
                    "Show the friends cache for the given user",
                    HandleFriendsShowCacheCommand);
            }
        }

        protected void HandleFriendsShowCacheCommand(string module, string[] cmd)
        {
            if (cmd.Length != 5)
            {
                MainConsole.Instance.OutputFormat("Usage: friends show cache [<first-name> <last-name>]");
                return;
            }

            string firstName = cmd[3];
            string lastName = cmd[4];

            UUID userId = m_userManagementModule.GetUserIdByName(firstName, lastName);

//            UserAccount ua
//                = m_Scenes[0].UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, firstName, lastName);

            if (userId == UUID.Zero)
            {
                MainConsole.Instance.OutputFormat("No such user as {0} {1}", firstName, lastName);
                return;
            }

            if (m_friendsModule.AreFriendsCached(userId))
            {
                MainConsole.Instance.OutputFormat("No friends cached on this simulator for {0} {1}", firstName, lastName);
                return;
            }

            MainConsole.Instance.OutputFormat("Cached friends for {0} {1}:", firstName, lastName);

            MainConsole.Instance.OutputFormat("UUID\n");

            FriendInfo[] friends = m_friendsModule.GetFriendsFromCache(userId);

            foreach (FriendInfo friend in friends)
            {
//                MainConsole.Instance.OutputFormat(friend.PrincipalID.ToString());

//                string friendFirstName, friendLastName;
//
//                UserAccount friendUa
//                    = m_Scenes[0].UserAccountService.GetUserAccount(m_Scenes[0].RegionInfo.ScopeID, friend.PrincipalID);

                UUID friendId;
                string friendName;

                if (UUID.TryParse(friend.Friend, out friendId))
                    friendName = m_userManagementModule.GetUserName(friendId);
                else
                    friendName = friend.Friend;

                MainConsole.Instance.OutputFormat("{0} {1} {2}", friendName, friend.MyFlags, friend.TheirFlags);
            }
        }
    }
}