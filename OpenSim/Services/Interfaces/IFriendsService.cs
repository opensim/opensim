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
using OpenMetaverse;
using OpenSim.Framework;
using System.Collections.Generic;

namespace OpenSim.Services.Interfaces
{
    public class FriendInfo
    {
        public UUID PrincipalID;
        public string Friend;

        /// <summary>
        /// The permissions that this user has granted to the friend.
        /// </summary>
        public int MyFlags;

        /// <summary>
        /// The permissions that the friend has granted to this user.
        /// </summary>
        public int TheirFlags;

        public FriendInfo()
        {
        }

        public FriendInfo(Dictionary<string, object> kvp)
        {
            PrincipalID = UUID.Zero;
            if (kvp.ContainsKey("PrincipalID") && kvp["PrincipalID"] != null)
                UUID.TryParse(kvp["PrincipalID"].ToString(), out PrincipalID);
            Friend = string.Empty;
            if (kvp.ContainsKey("Friend") && kvp["Friend"] != null)
                Friend = kvp["Friend"].ToString();
            MyFlags = (int)FriendRights.None;
            if (kvp.ContainsKey("MyFlags") && kvp["MyFlags"] != null)
                Int32.TryParse(kvp["MyFlags"].ToString(), out MyFlags);
            TheirFlags = 0;
            if (kvp.ContainsKey("TheirFlags") && kvp["TheirFlags"] != null)
                Int32.TryParse(kvp["TheirFlags"].ToString(), out TheirFlags);
        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["PrincipalID"] = PrincipalID.ToString();
            result["Friend"] = Friend;
            result["MyFlags"] = MyFlags.ToString();
            result["TheirFlags"] = TheirFlags.ToString();

            return result;
        }
    }

    public interface IFriendsService
    {
        FriendInfo[] GetFriends(UUID PrincipalID);
        FriendInfo[] GetFriends(string PrincipalID);
        bool StoreFriend(string PrincipalID, string Friend, int flags);
        bool Delete(UUID PrincipalID, string Friend);
        bool Delete(string PrincipalID, string Friend);
    }
}
