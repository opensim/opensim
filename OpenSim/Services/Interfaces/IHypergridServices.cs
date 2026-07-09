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
        bool LinkLocalRegion(string regionDescriptor, out UUID regionID, out ulong regionHandle, out string externalName, out string imageURL, out string reason, out int sizeX, out int sizeY);

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

        /// <summary>
        /// Delivers a friend status notification to a Hypergrid visitor currently on
        /// this grid: resolves the visitor's current sim from this grid's presence
        /// data and forwards the event grid-internally, so it reaches the visitor
        /// even after intra-grid teleports the home grid never sees. The caller must
        /// present the visitor's session ID, which only the visitor's viewer and
        /// their home grid know, so third parties can't probe user locations or
        /// spoof status events. The friendship itself is validated by the caller
        /// (the visitor's home grid); this grid only checks the session capability.
        /// </summary>
        /// <param name="sessionID">The visitor's session ID, as stored by the home grid at HG login</param>
        /// <param name="userID">The visitor's User ID; must match the session</param>
        /// <param name="friendID">The friend whose status changed</param>
        /// <param name="online">true = came online, false = went offline</param>
        /// <returns>true if the visitor was found and the notification was forwarded to their sim</returns>
        bool StatusNotify(UUID sessionID, UUID userID, UUID friendID, bool online);
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
        /// <returns>On success: the external grid URL where the user is traveling. If the user doesn't exist or is at home: "".</returns>
        /// <remarks>Throws an exception if an error occurs (e.g., can't contact the server).</remarks>
        string LocateUser(UUID userID);

        /// <summary>
        /// Sends a friend status notification to a local user who is traveling on a
        /// foreign grid. Prefers handing delivery to the visited grid's gatekeeper
        /// (status_notify), which resolves the traveler's current sim from its own
        /// presence data; falls back to the sim URI recorded at grid entry when the
        /// visited grid is an older OpenSim. Does network I/O; call from a worker thread.
        /// </summary>
        /// <param name="userID">The traveling local user (the recipient)</param>
        /// <param name="friendID">The friend whose status changed</param>
        /// <param name="online">true = came online, false = went offline</param>
        /// <returns>true if a delivery attempt was made</returns>
        bool StatusNotifyTravelingAgent(UUID userID, UUID friendID, bool online);

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

        /// <summary>
        /// A local user's status changed at home; deliver it to those of their local
        /// friends who are traveling on a foreign grid. Called by this grid's own sims
        /// with the friends that had no home presence (travelers and offline friends
        /// look identical there — the home presence row is deleted while abroad).
        /// Friends without a travel record (simply offline) are filtered out by one
        /// cheap indexed read; only exact local (plain UUID) friendships are delivered,
        /// foreign friends flow through StatusNotification with their secrets instead.
        /// This method (like GetTravelingFriends) is reachable on the same publicly
        /// exposed port as StatusNotification, so sessionID acts as the auth
        /// capability in place of a per-friendship secret — see its own doc.
        /// </summary>
        /// <param name="userID">The local user whose status changed</param>
        /// <param name="sessionID">userID's own live session on this grid, as held by
        /// their connected sim. Verified against Presence before anything is delivered:
        /// only userID's own sim (or their viewer) can know this value, so it proves
        /// the request is genuine rather than an arbitrary caller who merely knows two
        /// account UUIDs — this endpoint is otherwise unauthenticated.</param>
        /// <param name="friends">Candidate local friend IDs with no home presence</param>
        /// <param name="online">true = came online, false = went offline</param>
        void StatusNotifyTravelingFriends(UUID userID, UUID sessionID, List<string> friends, bool online);

        /// <summary>
        /// Returns which of the given candidates are local friends of userID currently
        /// traveling on a foreign grid. Used by the sims to include travelers in the
        /// login-time online-friends snapshot (they have no home presence row while
        /// abroad, so presence alone reports them offline). Candidates without a
        /// travel record are filtered by one cheap indexed read; only exact local
        /// (plain UUID) friendships are reported.
        /// </summary>
        /// <param name="userID">The local user logging in</param>
        /// <param name="sessionID">userID's own live session on this grid — same auth
        /// capability role as on StatusNotifyTravelingFriends; this is a public,
        /// otherwise-unauthenticated endpoint and must not answer for an arbitrary
        /// caller who only knows userID.</param>
        /// <param name="friends">Candidate local friend IDs with no home presence</param>
        /// <returns>The subset of candidates that are friends and traveling</returns>
        List<UUID> GetTravelingFriends(UUID userID, UUID sessionID, List<string> friends);
    }

    public interface IInstantMessageSimConnector
    {
        bool SendInstantMessage(GridInstantMessage im);
    }
}
