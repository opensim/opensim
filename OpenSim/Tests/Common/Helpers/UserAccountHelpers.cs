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
using OpenMetaverse;

using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Tests.Common
{
    /// <summary>
    /// Utility functions for carrying out user profile related tests.
    /// </summary>
    public static class UserAccountHelpers
    {
//        /// <summary>
//        /// Create a test user with a standard inventory
//        /// </summary>
//        /// <param name="commsManager"></param>
//        /// <param name="callback">
//        /// Callback to invoke when inventory has been loaded.  This is required because
//        /// loading may be asynchronous, even on standalone
//        /// </param>
//        /// <returns></returns>
//        public static CachedUserInfo CreateUserWithInventory(
//            CommunicationsManager commsManager, OnInventoryReceivedDelegate callback)
//        {
//            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000099");
//            return CreateUserWithInventory(commsManager, userId, callback);
//        }
//
//        /// <summary>
//        /// Create a test user with a standard inventory
//        /// </summary>
//        /// <param name="commsManager"></param>
//        /// <param name="userId">User ID</param>
//        /// <param name="callback">
//        /// Callback to invoke when inventory has been loaded.  This is required because
//        /// loading may be asynchronous, even on standalone
//        /// </param>
//        /// <returns></returns>
//        public static CachedUserInfo CreateUserWithInventory(
//            CommunicationsManager commsManager, UUID userId, OnInventoryReceivedDelegate callback)
//        {
//            return CreateUserWithInventory(commsManager, "Bill", "Bailey", userId, callback);
//        }
//
//        /// <summary>
//        /// Create a test user with a standard inventory
//        /// </summary>
//        /// <param name="commsManager"></param>
//        /// <param name="firstName">First name of user</param>
//        /// <param name="lastName">Last name of user</param>
//        /// <param name="userId">User ID</param>
//        /// <param name="callback">
//        /// Callback to invoke when inventory has been loaded.  This is required because
//        /// loading may be asynchronous, even on standalone
//        /// </param>
//        /// <returns></returns>
//        public static CachedUserInfo CreateUserWithInventory(
//            CommunicationsManager commsManager, string firstName, string lastName, 
//            UUID userId, OnInventoryReceivedDelegate callback)
//        {
//            return CreateUserWithInventory(commsManager, firstName, lastName, "troll", userId, callback);
//        }
//
//        /// <summary>
//        /// Create a test user with a standard inventory
//        /// </summary>
//        /// <param name="commsManager"></param>
//        /// <param name="firstName">First name of user</param>
//        /// <param name="lastName">Last name of user</param>
//        /// <param name="password">Password</param>
//        /// <param name="userId">User ID</param>
//        /// <param name="callback">
//        /// Callback to invoke when inventory has been loaded.  This is required because
//        /// loading may be asynchronous, even on standalone
//        /// </param>
//        /// <returns></returns>
//        public static CachedUserInfo CreateUserWithInventory(
//            CommunicationsManager commsManager, string firstName, string lastName, string password,
//            UUID userId, OnInventoryReceivedDelegate callback)
//        {
//            LocalUserServices lus = (LocalUserServices)commsManager.UserService;
//            lus.AddUser(firstName, lastName, password, "bill@bailey.com", 1000, 1000, userId);
//
//            CachedUserInfo userInfo = commsManager.UserProfileCacheService.GetUserDetails(userId);
//            userInfo.OnInventoryReceived += callback;
//            userInfo.FetchInventory();
//
//            return userInfo;
//        }

        public static UserAccount CreateUserWithInventory(Scene scene)
        {
            return CreateUserWithInventory(scene, TestHelpers.ParseTail(99));
        }

        public static UserAccount CreateUserWithInventory(Scene scene, UUID userId)
        {
            return CreateUserWithInventory(scene, "Bill", "Bailey", userId, "troll");
        }

        public static UserAccount CreateUserWithInventory(Scene scene, int userId)
        {
            return CreateUserWithInventory(scene, "Bill", "Bailey", TestHelpers.ParseTail(userId), "troll");
        }

        public static UserAccount CreateUserWithInventory(
            Scene scene, string firstName, string lastName, UUID userId, string pw)
        {
            UserAccount ua = new UserAccount(userId) { FirstName = firstName, LastName = lastName };
            CreateUserWithInventory(scene, ua, pw);
            return ua;
        }

        public static UserAccount CreateUserWithInventory(
            Scene scene, string firstName, string lastName, int userId, string pw)
        {
            UserAccount ua
                = new UserAccount(TestHelpers.ParseTail(userId)) { FirstName = firstName, LastName = lastName };
            CreateUserWithInventory(scene, ua, pw);
            return ua;
        }
        
        public static void CreateUserWithInventory(Scene scene, UserAccount ua, string pw)
        {
            // FIXME: This should really be set up by UserAccount itself
            ua.ServiceURLs = new Dictionary<string, object>();
            scene.UserAccountService.StoreUserAccount(ua);
            scene.InventoryService.CreateUserInventory(ua.PrincipalID);
            scene.AuthenticationService.SetPassword(ua.PrincipalID, pw);
        }
    }
}