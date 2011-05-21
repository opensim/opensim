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

using OpenMetaverse;
using OpenSim.Framework;
using System;
using System.Collections.Generic;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Friends;
using OpenSim.Data;
using Nini.Config;
using log4net;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

namespace OpenSim.Services.HypergridService
{
    public class HGFriendsService : FriendsService, IFriendsService
    {
        public HGFriendsService(IConfigSource config) : base(config)
        {
        }

        /// <summary>
        /// Overrides base. 
        /// Storing new friendships from the outside is a tricky, sensitive operation, and it 
        /// needs to be done under certain restrictions.
        /// First of all, if the friendship already exists, this is a no-op. In other words, 
        /// we cannot change just the flags, it needs to be a new friendship.
        /// Second, we store it as flags=0 always, independent of what the caller sends. The
        /// owner of the friendship needs to confirm when it gets back home.
        /// </summary>
        /// <param name="PrincipalID"></param>
        /// <param name="Friend"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public override bool StoreFriend(string PrincipalID, string Friend, int flags)
        {
            UUID userID;
            if (UUID.TryParse(PrincipalID, out userID))
            {
                FriendsData[] friendsData = m_Database.GetFriends(userID.ToString());
                List<FriendsData> fList = new List<FriendsData>(friendsData);
                if (fList.Find(delegate(FriendsData fdata)
                    {
                        return fdata.Friend == Friend;
                    }) != null)
                    return false;
            }
            else
                return false;

            FriendsData d = new FriendsData();
            d.PrincipalID = PrincipalID;
            d.Friend = Friend;
            d.Data = new Dictionary<string, string>();
            d.Data["Flags"] = "0";

            return m_Database.Store(d);
        }

        /// <summary>
        /// Overrides base. Cannot delete friendships while away from home.
        /// </summary>
        /// <param name="PrincipalID"></param>
        /// <param name="Friend"></param>
        /// <returns></returns>
        public override bool Delete(UUID PrincipalID, string Friend)
        {
            return false;
        }

    }
}
