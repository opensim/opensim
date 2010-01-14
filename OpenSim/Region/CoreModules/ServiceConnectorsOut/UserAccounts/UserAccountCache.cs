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
using System.Reflection;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.UserAccounts
{
    public class UserAccountCache
    {
        //private static readonly ILog m_log =
        //        LogManager.GetLogger(
        //        MethodBase.GetCurrentMethod().DeclaringType);
        
        private ICnmCache<UUID, UserAccount> m_UUIDCache;
        private Dictionary<string, UUID> m_NameCache;

        public UserAccountCache()
        {
            // Warning: the size values are a bit fuzzy. What matters
            // most for this cache is the count value (128 entries).
            m_UUIDCache = CnmSynchronizedCache<UUID, UserAccount>.Synchronized(new CnmMemoryCache<UUID, UserAccount>(
                        128, 128*512, TimeSpan.FromMinutes(30.0)));
            m_NameCache = new Dictionary<string, UUID>(); // this one is unbound
        }

        public void Cache(UserAccount account)
        {
            m_UUIDCache.Set(account.PrincipalID, account, 512);
            m_NameCache[account.Name] = account.PrincipalID;

            //m_log.DebugFormat("[USER CACHE]: cached user {0} {1}", account.FirstName, account.LastName);
        }

        public UserAccount Get(UUID userID)
        {
            UserAccount account = null;
            if (m_UUIDCache.TryGetValue(userID, out account))
            {
                //m_log.DebugFormat("[USER CACHE]: Account {0} {1} found in cache", account.FirstName, account.LastName);
                return account;
            }

            return null;
        }

        public UserAccount Get(string name)
        {
            if (!m_NameCache.ContainsKey(name))
                return null;

            UserAccount account = null;
            if (m_UUIDCache.TryGetValue(m_NameCache[name], out account))
                return account;

            return null;
        }
    }
}
