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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Data.Null
{
    public class NullFriendsData : IFriendsData
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static List<FriendsData> m_Data = new List<FriendsData>();

        public NullFriendsData(string connectionString, string realm)
        {
        }

        /// <summary>
        /// Clear all friends data
        /// </summary>
        /// <remarks>
        /// This is required by unit tests to clear the static data between test runs.
        /// </remarks>
        public static void Clear()
        {
            lock (m_Data)
                m_Data.Clear();
        }

        public FriendsData[] GetFriends(UUID principalID)
        {
            return GetFriends(principalID.ToString());
        }

        /// <summary>
        /// Tries to implement the Get [] semantics, but it cuts corners.
        /// Specifically, it gets all friendships even if they weren't accepted yet.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public FriendsData[] GetFriends(string userID)
        {
            lock (m_Data)
            {
                List<FriendsData> lst = m_Data.FindAll(fdata =>
                {
                    return fdata.PrincipalID == userID.ToString();
                });

                if (lst != null)
                {
                    lst.ForEach(f =>
                    {
                        FriendsData f2 = m_Data.Find(candidateF2 => f.Friend == candidateF2.PrincipalID);
                        if (f2 != null)
                            f.Data["TheirFlags"] = f2.Data["Flags"];

    //                    m_log.DebugFormat(
    //                        "[NULL FRIENDS DATA]: Got {0} {1} {2} for {3}",
    //                        f.Friend, f.Data["Flags"], f2 != null ? f.Data["TheirFlags"] : "not found!", f.PrincipalID);
                    });

    //                m_log.DebugFormat("[NULL FRIENDS DATA]: Got {0} friends for {1}", lst.Count, userID);

                    return lst.ToArray();
                }
            }

            return new FriendsData[0];
        }

        public bool Store(FriendsData data)
        {
            if (data == null)
                return false;

//            m_log.DebugFormat(
//                "[NULL FRIENDS DATA]: Storing {0} {1} {2}", data.PrincipalID, data.Friend, data.Data["Flags"]);

            lock (m_Data)
                m_Data.Add(data);

            return true;
        }

        public bool Delete(UUID principalID, string friend)
        {
            return Delete(principalID.ToString(), friend);
        }

        public bool Delete(string userID, string friendID)
        {
            lock (m_Data)
            {
                List<FriendsData> lst = m_Data.FindAll(delegate(FriendsData fdata) { return fdata.PrincipalID == userID.ToString(); });
                if (lst != null)
                {
                    FriendsData friend = lst.Find(delegate(FriendsData fdata) { return fdata.Friend == friendID; });
                    if (friendID != null)
                    {
    //                    m_log.DebugFormat(
    //                        "[NULL FRIENDS DATA]: Deleting friend {0} {1} for {2}",
    //                        friend.Friend, friend.Data["Flags"], friend.PrincipalID);

                        m_Data.Remove(friend);
                        return true;
                    }
                }
            }

            return false;
        }

    }
}
