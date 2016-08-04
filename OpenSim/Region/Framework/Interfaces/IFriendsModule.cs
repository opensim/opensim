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
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IFriendsModule
    {
        /// <summary>
        /// Are friends cached on this simulator for a particular user?
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        bool AreFriendsCached(UUID userID);

        /// <summary>
        /// Get friends from local cache only
        /// </summary>
        /// <param name="userID"></param>
        /// <returns>
        /// An empty array if the user has no friends or friends have not been cached.
        /// </returns>
        FriendInfo[] GetFriendsFromCache(UUID userID);

        /// <summary>
        /// Add a friendship between two users.
        /// </summary>
        /// <remarks>
        /// Ultimately, it would be more useful to take in a user account here rather than having to have a user
        /// present in the scene.
        /// </remarks>
        /// <param name="client"></param>
        /// <param name="friendID"></param>
        void AddFriendship(IClientAPI client, UUID friendID);

        /// <summary>
        /// Remove a friendship between two users.
        /// </summary>
        /// <remarks>
        /// Ultimately, it would be more useful to take in a user account here rather than having to have a user
        /// present in the scene.
        /// </remarks>
        /// <param name="client"></param>
        /// <param name="exFriendID"></param>
        void RemoveFriendship(IClientAPI client, UUID exFriendID);

        /// <summary>
        /// Get permissions granted by a friend.
        /// </summary>
        /// <param name="userID">The user.</param>
        /// <param name="friendID">The friend that granted.</param>
        /// <returns>The permissions.  These come from the FriendRights enum.</returns>
        int GetRightsGrantedByFriend(UUID userID, UUID friendID);

        /// <summary>
        /// Grant permissions for a friend.
        /// </summary>
        /// <remarks>
        /// This includes giving them the ability to see when the user is online and permission to edit the user's
        /// objects.
        /// Granting lower permissions than the friend currently has will rescind the extra permissions.
        /// </remarks>
        /// <param name="remoteClient">The user granting the permissions.</param>
        /// <param name="friendID">The friend.</param>
        /// <param name="perms">These come from the FriendRights enum.</param>
        void GrantRights(IClientAPI remoteClient, UUID friendID, int perms);

        void IsNowRoot(ScenePresence sp);
        bool SendFriendsOnlineIfNeeded(IClientAPI client);
    }
}