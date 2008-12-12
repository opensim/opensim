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

using OpenMetaverse;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Framework.Communications.Tests
{    
    /// <summary>
    /// Utility functions for carrying out user profile relate tests.
    /// </summary>
    public class UserProfileTestUtils
    {
        /// <summary>
        /// Set up standard services required for user tests.
        /// </summary>
        /// <returns>CommunicationsManager used to access these services</returns>
        public static CommunicationsManager SetupServices()
        {
            return SetupServices(new TestUserDataPlugin(), new TestInventoryDataPlugin());
        }
        
        /// <summary>
        /// Set up standard services required for user tests.
        /// </summary>
        /// <param name="userDataPlugin"></param>
        /// <param name="inventoryDataPlugin"></param>
        /// <returns>CommunicationsManager used to access these services</returns>
        public static CommunicationsManager SetupServices(
            IUserDataPlugin userDataPlugin, IInventoryDataPlugin inventoryDataPlugin)
        {
            CommunicationsManager commsManager = new TestCommunicationsManager();
            
            ((LocalUserServices)commsManager.UserService).AddPlugin(userDataPlugin);
            ((LocalInventoryService)commsManager.InventoryService).AddPlugin(inventoryDataPlugin);
            
            return commsManager;
        }
        
        /// <summary>
        /// Create a test user with a standard inventory
        /// </summary>
        /// <param name="commsManager"></param>
        /// <returns></returns>
        public static CachedUserInfo CreateUserWithInventory(CommunicationsManager commsManager)
        {
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000099");
            return CreateUserWithInventory(commsManager, userId);
        }        
        
        /// <summary>
        /// Create a test user with a standard inventory
        /// </summary>
        /// <param name="commsManager"></param>
        /// <param name="userId">Explicit user id to use for user creation</param>
        /// <returns></returns>
        public static CachedUserInfo CreateUserWithInventory(CommunicationsManager commsManager, UUID userId)
        {                        
            LocalUserServices lus = (LocalUserServices)commsManager.UserService;           

            lus.AddUser("Bill", "Bailey", "troll", "bill@bailey.com", 1000, 1000, userId);
            
            commsManager.UserProfileCacheService.RequestInventoryForUser(userId);
            
            return commsManager.UserProfileCacheService.GetUserDetails(userId);            
        }
    }
}
