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
        bool LinkRegion(string regionDescriptor, out UUID regionID, out ulong regionHandle, out string externalName, out string imageURL, out string reason);
        GridRegion GetHyperlinkRegion(UUID regionID);

        bool LoginAgent(AgentCircuitData aCircuit, GridRegion destination, out string reason);

    }

    public interface IUserAgentService
    {
        bool LoginAgentToGrid(AgentCircuitData agent, GridRegion gatekeeper, GridRegion finalDestination, bool fromLogin, out string reason);
        void LogoutAgent(UUID userID, UUID sessionID);
        GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt);
        Dictionary<string, object> GetServerURLs(UUID userID);
        Dictionary<string,object> GetUserInfo(UUID userID);

        string LocateUser(UUID userID);
        // Tries to get the universal user identifier for the targetUserId
        // on behalf of the userID
        string GetUUI(UUID userID, UUID targetUserID);

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
