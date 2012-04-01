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
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_allowDuplicatePresences = false;

        public PresenceService(IConfigSource config)
            : base(config)
        {
            m_log.Debug("[PRESENCE SERVICE]: Starting presence service");

            IConfig presenceConfig = config.Configs["PresenceService"];
            if (presenceConfig != null)
            {
                m_allowDuplicatePresences =
                        presenceConfig.GetBoolean("AllowDuplicatePresences",
                                                  m_allowDuplicatePresences);
            }
        }

        public bool LoginAgent(string userID, UUID sessionID,
                UUID secureSessionID)
        {
            //PresenceData[] d = m_Database.Get("UserID", userID);
            //m_Database.Get("UserID", userID);

            if (!m_allowDuplicatePresences)
                m_Database.Delete("UserID", userID.ToString());

            PresenceData data = new PresenceData();

            data.UserID = userID;
            data.RegionID = UUID.Zero;
            data.SessionID = sessionID;
            data.Data = new Dictionary<string, string>();
            data.Data["SecureSessionID"] = secureSessionID.ToString();
            
            m_Database.Store(data);

            m_log.DebugFormat("[PRESENCE SERVICE]: LoginAgent {0} with session {1} and ssession {2}",
                userID, sessionID, secureSessionID);
            return true;
        }

        public bool LogoutAgent(UUID sessionID)
        {
            m_log.DebugFormat("[PRESENCE SERVICE]: Session {0} logout", sessionID);
            return m_Database.Delete("SessionID", sessionID.ToString());
        }

        public bool LogoutRegionAgents(UUID regionID)
        {
            m_Database.LogoutRegionAgents(regionID);

            return true;
        }


        public bool ReportAgent(UUID sessionID, UUID regionID)
        {
//            m_log.DebugFormat("[PRESENCE SERVICE]: ReportAgent with session {0} in region {1}", sessionID, regionID);
            try
            {
                PresenceData pdata = m_Database.Get(sessionID);
                if (pdata == null)
                    return false;
                if (pdata.Data == null)
                    return false;

                return m_Database.ReportAgent(sessionID, regionID);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE SERVICE]: ReportAgent threw exception {0}", e.StackTrace);
                return false;
            }
        }

        public PresenceInfo GetAgent(UUID sessionID)
        {
            PresenceInfo ret = new PresenceInfo();
            
            PresenceData data = m_Database.Get(sessionID);
            if (data == null)
                return null;

            ret.UserID = data.UserID;
            ret.RegionID = data.RegionID;

            return ret;
        }

        public PresenceInfo[] GetAgents(string[] userIDs)
        {
            List<PresenceInfo> info = new List<PresenceInfo>();

            foreach (string userIDStr in userIDs)
            {
                PresenceData[] data = m_Database.Get("UserID",
                        userIDStr);

                foreach (PresenceData d in data)
                {
                    PresenceInfo ret = new PresenceInfo();

                    ret.UserID = d.UserID;
                    ret.RegionID = d.RegionID;

                    info.Add(ret);
                }

//                m_log.DebugFormat(
//                    "[PRESENCE SERVICE]: GetAgents for {0} found {1} presences", userIDStr, data.Length);
            }

            return info.ToArray();
        }
    }
}