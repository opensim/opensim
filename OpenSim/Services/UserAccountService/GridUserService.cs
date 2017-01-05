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
using Nini.Config;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;

namespace OpenSim.Services.UserAccountService
{
    public class GridUserService : GridUserServiceBase, IGridUserService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool m_Initialized;

        public GridUserService(IConfigSource config) : base(config)
        {
            m_log.Debug("[GRID USER SERVICE]: Starting user grid service");

            if (!m_Initialized)
            {
                m_Initialized = true;

                MainConsole.Instance.Commands.AddCommand(
                    "Users", false,
                    "show grid user",
                    "show grid user <ID>",
                    "Show grid user entry or entries that match or start with the given ID.  This will normally be a UUID.",
                    "This is for debug purposes to see what data is found for a particular user id.",
                    HandleShowGridUser);

                MainConsole.Instance.Commands.AddCommand(
                    "Users", false,
                    "show grid users online",
                    "show grid users online",
                    "Show number of grid users registered as online.",
                    "This number may not be accurate as a region may crash or not be cleanly shutdown and leave grid users shown as online\n."
                    + "For this reason, users online for more than 5 days are not currently counted",
                    HandleShowGridUsersOnline);
            }
        }

        protected void HandleShowGridUser(string module, string[] cmdparams)
        {
            if (cmdparams.Length != 4)
            {
                MainConsole.Instance.Output("Usage: show grid user <UUID>");
                return;
            }

            GridUserData[] data = m_Database.GetAll(cmdparams[3]);

            foreach (GridUserData gu in data)
            {
                ConsoleDisplayList cdl = new ConsoleDisplayList();

                cdl.AddRow("User ID", gu.UserID);

                foreach (KeyValuePair<string,string> kvp in gu.Data)
                    cdl.AddRow(kvp.Key, kvp.Value);

                MainConsole.Instance.Output(cdl.ToString());
            }

            MainConsole.Instance.OutputFormat("Entries: {0}", data.Length);
        }

        protected void HandleShowGridUsersOnline(string module, string[] cmdparams)
        {
//            if (cmdparams.Length != 4)
//            {
//                MainConsole.Instance.Output("Usage: show grid users online");
//                return;
//            }

//            int onlineCount;
            int onlineRecentlyCount = 0;

            DateTime now = DateTime.UtcNow;

            foreach (GridUserData gu in m_Database.GetAll(""))
            {
                if (bool.Parse(gu.Data["Online"]))
                {
//                    onlineCount++;

                    int unixLoginTime = int.Parse(gu.Data["Login"]);

                    if ((now - Util.ToDateTime(unixLoginTime)).Days < 5)
                        onlineRecentlyCount++;
                }
            }

            MainConsole.Instance.OutputFormat("Users online: {0}", onlineRecentlyCount);
        }

        private GridUserData GetGridUserData(string userID)
        {
            GridUserData d = null;
            if (userID.Length > 36) // it's a UUI
            {
                d = m_Database.Get(userID);
            }
            else // it's a UUID
            {
                GridUserData[] ds = m_Database.GetAll(userID);
                if (ds == null)
                    return null;

                if (ds.Length > 0)
                {
                    d = ds[0];
                    foreach (GridUserData dd in ds)
                        if (dd.UserID.Length > d.UserID.Length) // find the longest
                            d = dd;
                }
            }

            return d;
        }

        public virtual GridUserInfo GetGridUserInfo(string userID)
        {
            GridUserData d = GetGridUserData(userID);

            if (d == null)
                return null;

            GridUserInfo info = new GridUserInfo();
            info.UserID = d.UserID;
            info.HomeRegionID = new UUID(d.Data["HomeRegionID"]);
            info.HomePosition = Vector3.Parse(d.Data["HomePosition"]);
            info.HomeLookAt = Vector3.Parse(d.Data["HomeLookAt"]);

            info.LastRegionID = new UUID(d.Data["LastRegionID"]);
            info.LastPosition = Vector3.Parse(d.Data["LastPosition"]);
            info.LastLookAt = Vector3.Parse(d.Data["LastLookAt"]);

            info.Online = bool.Parse(d.Data["Online"]);
            info.Login = Util.ToDateTime(Convert.ToInt32(d.Data["Login"]));
            info.Logout = Util.ToDateTime(Convert.ToInt32(d.Data["Logout"]));

            return info;
        }

        public virtual GridUserInfo[] GetGridUserInfo(string[] userIDs)
        {
            List<GridUserInfo> ret = new List<GridUserInfo>();

            foreach (string id in userIDs)
                ret.Add(GetGridUserInfo(id));

            return ret.ToArray();
        }

        public GridUserInfo LoggedIn(string userID)
        {
            m_log.DebugFormat("[GRID USER SERVICE]: User {0} is online", userID);

            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["Online"] = true.ToString();
            d.Data["Login"] = Util.UnixTimeSinceEpoch().ToString();

            m_Database.Store(d);

            return GetGridUserInfo(userID);
        }

        public bool LoggedOut(string userID, UUID sessionID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            m_log.DebugFormat("[GRID USER SERVICE]: User {0} is offline", userID);

            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["Online"] = false.ToString();
            d.Data["Logout"] = Util.UnixTimeSinceEpoch().ToString();
            d.Data["LastRegionID"] = regionID.ToString();
            d.Data["LastPosition"] = lastPosition.ToString();
            d.Data["LastLookAt"] = lastLookAt.ToString();

            return m_Database.Store(d);
        }

        public bool SetHome(string userID, UUID homeID, Vector3 homePosition, Vector3 homeLookAt)
        {
            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["HomeRegionID"] = homeID.ToString();
            d.Data["HomePosition"] = homePosition.ToString();
            d.Data["HomeLookAt"] = homeLookAt.ToString();

            return m_Database.Store(d);
        }

        public bool SetLastPosition(string userID, UUID sessionID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
//            m_log.DebugFormat("[GRID USER SERVICE]: SetLastPosition for {0}", userID);

            GridUserData d = GetGridUserData(userID);

            if (d == null)
            {
                d = new GridUserData();
                d.UserID = userID;
            }

            d.Data["LastRegionID"] = regionID.ToString();
            d.Data["LastPosition"] = lastPosition.ToString();
            d.Data["LastLookAt"] = lastLookAt.ToString();

            return m_Database.Store(d);
        }
    }
}