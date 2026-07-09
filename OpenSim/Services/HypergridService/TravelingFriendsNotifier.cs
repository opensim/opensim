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
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

using OpenMetaverse;
using log4net;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Given local friend-ID candidates that have no live local presence,
    /// determines which are traveling on a foreign grid (one cheap indexed
    /// LocateUser read per candidate, so the common "simply offline" case
    /// never touches the Friends store) and either delivers their status or
    /// reports which are traveling. Shared by HGFriendsService.StatusNotification
    /// (local branch), HGFriendsService.StatusNotifyTravelingFriends/
    /// GetTravelingFriends (the statusnotify_traveling / gettravelingfriends
    /// wire methods), and UserAgentService.NotifyFriendsOfStatus.
    /// HG-only plumbing: lives entirely in this Robust HG service project;
    /// the sim-side base FriendsModule never references it.
    /// </summary>
    internal static class TravelingFriendsNotifier
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Delivers a friend's status change to whichever of the given
        /// candidates turn out to be traveling on a foreign grid.</summary>
        /// <returns>Delivered friend UUIDs. Only meaningful when online is
        /// true (matches the existing StatusNotification/StatusNotifyTravelingFriends
        /// wire-response convention: an offline reply is always empty).</returns>
        internal static List<UUID> DeliverStatusToTravelers(
            IUserAgentService userAgentService,
            IEnumerable<string> candidateFriendIDs,
            UUID counterpartID,
            bool online,
            Func<UUID, bool> verifyFriend = null)
        {
            List<UUID> delivered = new();
            if (userAgentService is null)
                return delivered;

            foreach (string candidate in candidateFriendIDs)
            {
                if (!UUID.TryParse(candidate, out UUID id))
                    continue;

                // cheap DB-only check so the common "friend is just offline"
                // case never touches the Friends store; delivery itself is
                // done off-thread below
                if (string.IsNullOrEmpty(userAgentService.LocateUser(id)))
                    continue;

                if (verifyFriend != null && !verifyFriend(id))
                    continue;

                m_log.DebugFormat("[TRAVELING FRIENDS NOTIFIER]: {0} is traveling; forwarding status of {1}",
                    id, counterpartID);

                Util.FireAndForget(o =>
                {
                    userAgentService.StatusNotifyTravelingAgent(id, counterpartID, online);
                }, null, "TravelingFriendsNotifier.DeliverStatusToTraveler");

                if (online)
                    delivered.Add(id);
            }

            return delivered;
        }

        /// <summary>Read-only counterpart for the login-time online snapshot
        /// (GetTravelingFriends): same gate, no delivery.</summary>
        internal static List<UUID> FilterTraveling(
            IUserAgentService userAgentService,
            IEnumerable<string> candidateFriendIDs,
            Func<UUID, bool> verifyFriend = null)
        {
            List<UUID> traveling = new();
            if (userAgentService is null)
                return traveling;

            foreach (string candidate in candidateFriendIDs)
            {
                if (!UUID.TryParse(candidate, out UUID id))
                    continue;
                if (string.IsNullOrEmpty(userAgentService.LocateUser(id)))
                    continue;
                if (verifyFriend != null && !verifyFriend(id))
                    continue;
                traveling.Add(id);
            }

            return traveling;
        }

        /// <summary>Exact local (plain-UUID) friendship match, plus the same
        /// CanSeeOnline right the sim itself already filters by before ever
        /// reaching this grid — checked again here as defense in depth, since
        /// this is called from wire-facing methods that authenticate the caller
        /// but not, on their own, whether that friendship still grants
        /// visibility. Foreign friend rows carry secrets and are validated by
        /// StatusNotification instead.</summary>
        internal static bool IsLocalFriend(IFriendsService friendsService, UUID candidateID, UUID counterpartID)
        {
            FriendInfo[] finfos = friendsService?.GetFriends(candidateID);
            if (finfos is null)
                return false;

            string counterpartStr = counterpartID.ToString();
            foreach (FriendInfo finfo in finfos)
                if (finfo.Friend == counterpartStr)
                    // TheirFlags here is counterpartID's grant to candidateID
                    // (this row belongs to candidateID, "Their" = the other side)
                    return finfo.TheirFlags != -1 && (finfo.TheirFlags & (int)FriendRights.CanSeeOnline) != 0;

            return false;
        }
    }
}
