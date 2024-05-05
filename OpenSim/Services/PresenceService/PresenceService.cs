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
using System.Net;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.PresenceService
{
    public class PresenceService : PresenceServiceBase, IPresenceService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_allowDuplicatePresences = false;
        const int EXPIREMS = 300000;
        static ExpiringCacheOS<UUID, PresenceData> BySessionCache = new ExpiringCacheOS<UUID, PresenceData>(60000);
        static ExpiringCacheOS<string, PresenceData> ByUserCache = new ExpiringCacheOS<string, PresenceData>(60000);

        public PresenceService(IConfigSource config)
            : base(config)
        {
            m_log.Debug("[PRESENCE SERVICE]: Starting presence service");

            IConfig presenceConfig = config.Configs["PresenceService"];
            if (presenceConfig != null)
            {
                m_allowDuplicatePresences = presenceConfig.GetBoolean("AllowDuplicatePresences", m_allowDuplicatePresences);
            }
        }

        public bool LoginAgent(string userID, UUID sessionID, UUID secureSessionID)
        {
            bool inCache = ByUserCache.TryGetValue(userID, out PresenceData prevUser);
            if (!inCache)
            {
                PresenceData[] dataprv = m_Database.Get("UserID", userID);
                if (dataprv.Length > 0)
                    prevUser = dataprv[0];
            }

            if (!m_allowDuplicatePresences && (prevUser != null))
            {
                m_Database.Delete("UserID", userID.ToString());
                if(inCache)
                {
                    BySessionCache.Remove(prevUser.SessionID);
                    ByUserCache.Remove(userID);
                }
            }

            PresenceData data = new PresenceData();

            data.UserID = userID;
            data.RegionID = UUID.Zero;
            data.SessionID = sessionID;
            data.Data = new Dictionary<string, string>();
            data.Data["SecureSessionID"] = secureSessionID.ToString();

            m_Database.Store(data);
            BySessionCache.Add(sessionID, data, EXPIREMS);
            ByUserCache.Add(userID, data, EXPIREMS);

            string prevUserStr = "";
            if (prevUser != null)
                prevUserStr = string.Format(". This user was already logged-in: session {0}, region {1}", prevUser.SessionID, prevUser.RegionID);

            m_log.DebugFormat("[PRESENCE SERVICE]: LoginAgent: session {0}, user {1}, region {2}, secure session {3}{4}",
                data.SessionID, data.UserID, data.RegionID, secureSessionID, prevUserStr);

            return true;
        }

        public bool LogoutAgent(UUID sessionID)
        {
             bool inCache = BySessionCache.TryGetValue(sessionID, out PresenceData presence);
             if(!inCache)
                presence = m_Database.Get(sessionID);

            m_log.DebugFormat("[PRESENCE SERVICE]: LogoutAgent: session {0}, user {1}, region {2}",
                sessionID,
                (presence == null) ? null : presence.UserID,
                (presence == null) ? null : presence.RegionID.ToString());

            bool ret = m_Database.Delete("SessionID", sessionID.ToString());
            if(inCache)
            {
                BySessionCache.Remove(sessionID);
                if(presence is not null)
                    ByUserCache.Remove(presence.UserID);
            }
            return ret;
        }

        public bool LogoutRegionAgents(UUID regionID)
        {
            PresenceData[] prevSessions = GetRegionAgents(regionID);
            if ((prevSessions is null) || (prevSessions.Length == 0))
                return true;

            m_log.DebugFormat("[PRESENCE SERVICE]: Logout users in region {0}", regionID);
            for (int i = 0; i < prevSessions.Length; ++i)
            {
                PresenceData pd = prevSessions[i];
                BySessionCache.Remove(pd.SessionID);
                ByUserCache.Remove(pd.UserID);
            }

            // There's a small chance that LogoutRegionAgents() will logout different users than the
            // list that was logged above, but it's unlikely and not worth dealing with.

            m_Database.LogoutRegionAgents(regionID);

            return true;
        }

        public bool ReportAgent(UUID sessionID, UUID regionID)
        {
            try
            {
                bool inCache = BySessionCache.TryGetValue(sessionID, out PresenceData presence);
                if(!inCache)
                    presence = m_Database.Get(sessionID);

                bool success;
                if (presence == null)
                    success = false;
                else
                    success = m_Database.ReportAgent(sessionID, regionID);

                m_log.DebugFormat("[PRESENCE SERVICE]: ReportAgent{0}: session {1}, user {2}, region {3}. Previously: {4}",
                    success ? "" : " failed",
                    sessionID, (presence == null) ? null : presence.UserID, regionID,
                    (presence == null) ? "not logged-in" : "region " + presence.RegionID);

                if (success)
                {
                    presence.RegionID = regionID;
                    BySessionCache.Add(sessionID, presence, EXPIREMS); // lastseen seems unused
                    ByUserCache.Add(presence.UserID, presence, EXPIREMS); // lastseen seems unused
                }
                else if (inCache)
                {
                    BySessionCache.Remove(sessionID);
                    ByUserCache.Remove(presence.UserID);
                }

                return success;
            }
            catch (Exception e)
            {
                m_log.Debug(string.Format("[PRESENCE SERVICE]: ReportAgent for session {0} threw exception ", sessionID), e);
                return false;
            }
        }

        public PresenceInfo GetAgent(UUID sessionID)
        {
            if(!BySessionCache.TryGetValue(sessionID, out PresenceData data))
                data = m_Database.Get(sessionID);

            if (data == null)
                return null;

            BySessionCache.Add(sessionID, data, EXPIREMS);
            ByUserCache.Add(data.UserID, data, EXPIREMS);

            var ret = new PresenceInfo()
            {
                UserID = data.UserID,
                RegionID = data.RegionID
            };
            return ret;
        }

        public PresenceInfo[] GetAgents(string[] userIDs)
        {
            var info = new List<PresenceInfo>(userIDs.Length);
            PresenceInfo ret;
            foreach (string userIDStr in userIDs)
            {
                if(ByUserCache.TryGetValue(userIDStr, out PresenceData pd))
                {
                    ByUserCache.Add(pd.UserID, pd, EXPIREMS);
                    BySessionCache.Add(pd.SessionID, pd, EXPIREMS);
                    ret = new PresenceInfo()
                    {
                        UserID = pd.UserID,
                        RegionID = pd.RegionID
                    };
                    info.Add(ret);
                }
                else
                {
                    PresenceData[] data = m_Database.Get("UserID", userIDStr);
                    if(data.Length == 0)
                        continue;
                    PresenceData d = data[0];
                    ByUserCache.Add(d.UserID, d, EXPIREMS);
                    BySessionCache.Add(d.SessionID, d, EXPIREMS);
                    ret = new PresenceInfo()
                    {
                        UserID = d.UserID,
                        RegionID = d.RegionID
                    };
                    info.Add(ret);
                }
                //m_log.DebugFormat(
                //    "[PRESENCE SERVICE]: GetAgents for {0} found {1} presences", userIDStr, data.Length);
            }

            return info.ToArray();
        }

        private PresenceData[] GetRegionAgents(UUID regionID)
        {
            return m_Database.Get("RegionID", regionID.ToString());
        }

    }
}