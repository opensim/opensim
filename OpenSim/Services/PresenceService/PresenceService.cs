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

        public PresenceService(IConfigSource config)
            : base(config)
        {
            m_log.Debug("[PRESENCE SERVICE]: Starting presence service");
        }

        public bool LoginAgent(string userID, UUID sessionID,
                UUID secureSessionID)
        {
            m_Database.Prune(userID);

            PresenceData[] d = m_Database.Get("UserID", userID);

            PresenceData data = new PresenceData();

            data.UserID = userID;
            data.RegionID = UUID.Zero;
            data.SessionID = sessionID;
            data.Data = new Dictionary<string, string>();
            data.Data["SecureSessionID"] = secureSessionID.ToString();
            data.Data["Online"] = "true";
            data.Data["Login"] = Util.UnixTimeSinceEpoch().ToString();
            if (d != null && d.Length > 0)
            {
                data.Data["HomeRegionID"] = d[0].Data["HomeRegionID"];
                data.Data["HomePosition"] = d[0].Data["HomePosition"];
                data.Data["HomeLookAt"] = d[0].Data["HomeLookAt"];
            }
            else
            {
                data.Data["HomeRegionID"] = UUID.Zero.ToString();
                data.Data["HomePosition"] = new Vector3(128, 128, 0).ToString();
                data.Data["HomeLookAt"] = new Vector3(0, 1, 0).ToString();
            }
            
            m_Database.Store(data);

            return true;
        }

        public bool LogoutAgent(UUID sessionID)
        {
            PresenceData data = m_Database.Get(sessionID);
            if (data == null)
                return false;

            PresenceData[] d = m_Database.Get("UserID", data.UserID);

            m_log.WarnFormat("[PRESENCE SERVICE]: LogoutAgent {0} with {1} sessions currently present", data.UserID, d.Length);
            if (d.Length > 1)
            {
                m_Database.Delete("UserID", data.UserID);
            }

            data.Data["Online"] = "false";
            data.Data["Logout"] = Util.UnixTimeSinceEpoch().ToString();

            m_Database.Store(data);

            return true;
        }

        public bool LogoutRegionAgents(UUID regionID)
        {
            m_Database.LogoutRegionAgents(regionID);

            return true;
        }


        public bool ReportAgent(UUID sessionID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            m_log.DebugFormat("[PRESENCE SERVICE]: ReportAgent with session {0} in region {1}", sessionID, regionID);
            try
            {
                PresenceData pdata = m_Database.Get(sessionID);
                if (pdata == null)
                    return false;
                if (pdata.Data == null)
                    return false;

                if (!pdata.Data.ContainsKey("Online") || (pdata.Data.ContainsKey("Online") && pdata.Data["Online"] == "false"))
                {
                    m_log.WarnFormat("[PRESENCE SERVICE]: Someone tried to report presence of an agent who's not online");
                    return false;
                }

                return m_Database.ReportAgent(sessionID, regionID,
                            position.ToString(), lookAt.ToString());
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
            if (data.Data.ContainsKey("Online"))
                ret.Online = bool.Parse(data.Data["Online"]);
            if (data.Data.ContainsKey("Login"))
                ret.Login = Util.ToDateTime(Convert.ToInt32(data.Data["Login"]));
            if (data.Data.ContainsKey("Logout"))
                ret.Logout = Util.ToDateTime(Convert.ToInt32(data.Data["Logout"]));
            if (data.Data.ContainsKey("Position"))
                ret.Position = Vector3.Parse(data.Data["Position"]);
            if (data.Data.ContainsKey("LookAt"))
                ret.LookAt = Vector3.Parse(data.Data["LookAt"]);
            if (data.Data.ContainsKey("HomeRegionID"))
                ret.HomeRegionID = new UUID(data.Data["HomeRegionID"]);
            if (data.Data.ContainsKey("HomePosition"))
                ret.HomePosition = Vector3.Parse(data.Data["HomePosition"]);
            if (data.Data.ContainsKey("HomeLookAt"))
                ret.HomeLookAt = Vector3.Parse(data.Data["HomeLookAt"]);

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
                    ret.Online = bool.Parse(d.Data["Online"]);
                    ret.Login = Util.ToDateTime(Convert.ToInt32(
                            d.Data["Login"]));
                    ret.Logout = Util.ToDateTime(Convert.ToInt32(
                            d.Data["Logout"]));
                    ret.Position = Vector3.Parse(d.Data["Position"]);
                    ret.LookAt = Vector3.Parse(d.Data["LookAt"]);
                    ret.HomeRegionID = new UUID(d.Data["HomeRegionID"]);
                    ret.HomePosition = Vector3.Parse(d.Data["HomePosition"]);
                    ret.HomeLookAt = Vector3.Parse(d.Data["HomeLookAt"]);

                    info.Add(ret);
                }
            }

            return info.ToArray();
        }

        public bool SetHomeLocation(string userID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            return m_Database.SetHomeLocation(userID, regionID, position, lookAt);
        }
    }
}
