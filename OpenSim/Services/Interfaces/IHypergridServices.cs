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
using System.Net;
using System.Collections.Generic;

using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IGatekeeperService
    {
        bool LinkRegion(string regionDescriptor, out UUID regionID, out ulong regionHandle, out string externalName, out string imageURL, out string reason, out int sizeX, out int sizeY);

        /// <summary>
        /// Returns the region a Hypergrid visitor should enter.
        /// </summary>
        /// <remarks>
        /// Usually the returned region will be the requested region. But the grid can choose to
        /// redirect the user to another region: e.g., a default gateway region.
        /// </remarks>
        /// <param name="regionID">The region the visitor *wants* to enter</param>
        /// <param name="agentID">The visitor's User ID. Will be missing (UUID.Zero) in older OpenSims.</param>
        /// <param name="agentHomeURI">The visitor's Home URI. Will be missing (null) in older OpenSims.</param>
        /// <param name="message">[out] A message to show to the user (optional, may be null)</param>
        /// <returns>The region the visitor should enter, or null if no region can be found / is allowed</returns>
        GridRegion GetHyperlinkRegion(UUID regionID, UUID agentID, string agentHomeURI, out string message);

        bool LoginAgent(GridRegion source, AgentCircuitData aCircuit, GridRegion destination, out string reason);

    }

    public interface IUserAgentService
    {
        bool LoginAgentToGrid(GridRegion source, AgentCircuitData agent, GridRegion gatekeeper, GridRegion finalDestination, bool fromLogin, out string reason);

        void LogoutAgent(UUID userID, UUID sessionID);

        /// <summary>
        /// Returns the home region of a remote user.
        /// </summary>
        /// <returns>On success: the user's home region. If the user doesn't exist: null.</returns>
        /// <remarks>Throws an exception if an error occurs (e.g., can't contact the server).</remarks>
        GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt);

        /// <summary>
        /// Returns the Server URLs of a remote user.
        /// </summary>
        /// <returns>On success: the user's Server URLs. If the user doesn't exist: an empty dictionary.</returns>
        /// <remarks>Throws an exception if an error occurs (e.g., can't contact the server).</remarks>
        Dictionary<string, object> GetServerURLs(UUID userID);

        /// <summary>
        /// Returns the UserInfo of a remote user.
        /// </summary>
        /// <returns>On success: the user's UserInfo. If the user doesn't exist: an empty dictionary.</returns>
        /// <remarks>Throws an exception if an error occurs (e.g., can't contact the server).</remarks>
        Dictionary<string, object> GetUserInfo(UUID userID);

        /// <summary>
        /// Returns the current location of a remote user.
        /// </summary>
        /// <returns>On success: the user's Server URLs. If the user doesn't exist: "".</returns>
        /// <remarks>Throws an exception if an error occurs (e.g., can't contact the server).</remarks>
        string LocateUser(UUID userID);

        /// <summary>
        /// Returns the Universal User Identifier for 'targetUserID' on behalf of 'userID'.
        /// </summary>
        /// <returns>On success: the user's UUI. If the user doesn't exist: "".</returns>
        /// <remarks>Throws an exception if an error occurs (e.g., can't contact the server).</remarks>
        string GetUUI(UUID userID, UUID targetUserID);

        /// <summary>
        /// Returns the remote user that has the given name.
        /// </summary>
        /// <returns>On success: the user's UUID. If the user doesn't exist: UUID.Zero.</returns>
        /// <remarks>Throws an exception if an error occurs (e.g., can't contact the server).</remarks>
        UUID GetUUID(String first, String last);

        // Returns the local friends online
        [Obsolete]
        List<UUID> StatusNotification(List<string> friends, UUID userID, bool online);

        bool IsAgentComingHome(UUID sessionID, string thisGridExternalName);
        bool VerifyAgent(UUID sessionID, string token);
        bool VerifyClient(UUID sessionID, string reportedIP);
    }

    public interface IInstantMessage
    {
        bool IncomingInstantMessage(GridInstantMessage im);
        bool OutgoingInstantMessage(GridInstantMessage im, string url, bool foreigner);
    }
    public interface IFriendsSimConnector
    {
        bool StatusNotify(UUID userID, UUID friendID, bool online);
        bool LocalFriendshipOffered(UUID toID, GridInstantMessage im);
        bool LocalFriendshipApproved(UUID userID, string userName, UUID friendID);
    }

    public interface IHGFriendsService
    {
        int GetFriendPerms(UUID userID, UUID friendID);
        bool NewFriendship(FriendInfo finfo, bool verified);
        bool DeleteFriendship(FriendInfo finfo, string secret);
        bool FriendshipOffered(UUID from, string fromName, UUID to, string message);
        bool ValidateFriendshipOffered(UUID fromID, UUID toID);
        // Returns the local friends online
        List<UUID> StatusNotification(List<string> friends, UUID userID, bool online);
    }

    public interface IInstantMessageSimConnector
    {
        bool SendInstantMessage(GridInstantMessage im);
    }
}
