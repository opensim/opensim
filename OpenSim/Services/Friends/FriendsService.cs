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
using OpenSim.Data;
using Nini.Config;
using log4net;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

namespace OpenSim.Services.Friends
{
    public class FriendsService : FriendsServiceBase, IFriendsService
    {
        public FriendsService(IConfigSource config) : base(config)
        {
        }

        public virtual FriendInfo[] GetFriends(UUID PrincipalID)
        {
            FriendsData[] data = m_Database.GetFriends(PrincipalID);
            List<FriendInfo> info = new List<FriendInfo>();

            foreach (FriendsData d in data)
            {
                FriendInfo i = new FriendInfo();

                i.PrincipalID = new UUID(d.PrincipalID);
                i.Friend = d.Friend;
                i.MyFlags = Convert.ToInt32(d.Data["Flags"]);
                i.TheirFlags = Convert.ToInt32(d.Data["TheirFlags"]);

                info.Add(i);
            }

            return info.ToArray();
        }

        public virtual FriendInfo[] GetFriends(string PrincipalID)
        {
            FriendsData[] data = m_Database.GetFriends(PrincipalID);
            List<FriendInfo> info = new List<FriendInfo>();

            foreach (FriendsData d in data)
            {
                FriendInfo i = new FriendInfo();

                if (!UUID.TryParse(d.PrincipalID, out i.PrincipalID))
                {
                    string tmp = string.Empty;
                    if (!Util.ParseUniversalUserIdentifier(d.PrincipalID, out i.PrincipalID, out tmp, out tmp, out tmp, out tmp))
                        // bad record. ignore this entry
                        continue;
                }
                i.Friend = d.Friend;
                i.MyFlags = Convert.ToInt32(d.Data["Flags"]);
                i.TheirFlags = Convert.ToInt32(d.Data["TheirFlags"]);

                info.Add(i);
            }

            return info.ToArray();
        }

        public virtual bool StoreFriend(string PrincipalID, string Friend, int flags)
        {
            FriendsData d = new FriendsData();

            d.PrincipalID = PrincipalID;
            d.Friend = Friend;
            d.Data = new Dictionary<string, string>();
            d.Data["Flags"] = flags.ToString();

            return m_Database.Store(d);
        }

        public bool Delete(string principalID, string friend)
        {
            return m_Database.Delete(principalID, friend);
        }

        public virtual bool Delete(UUID PrincipalID, string Friend)
        {
            return m_Database.Delete(PrincipalID, Friend);
        }

    }
}
