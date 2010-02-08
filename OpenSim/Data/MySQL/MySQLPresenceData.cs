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
using System.Data;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Grid Server
    /// </summary>
    public class MySQLPresenceData : MySQLGenericTableHandler<PresenceData>,
            IPresenceData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MySQLPresenceData(string connectionString, string realm) :
                base(connectionString, realm, "Presence")
        {
        }

        public PresenceData Get(UUID sessionID)
        {
            PresenceData[] ret = Get("SessionID",
                    sessionID.ToString());

            if (ret.Length == 0)
                return null;

            return ret[0];
        }

        public void LogoutRegionAgents(UUID regionID)
        {
            MySqlCommand cmd = new MySqlCommand();

            cmd.CommandText = String.Format("update {0} set Online='false' where `RegionID`=?RegionID", m_Realm);

            cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());

            ExecuteNonQuery(cmd);
        }

        public bool ReportAgent(UUID sessionID, UUID regionID, string position,
                string lookAt)
        {
            PresenceData[] pd = Get("SessionID", sessionID.ToString());
            if (pd.Length == 0)
                return false;

            MySqlCommand cmd = new MySqlCommand();

            cmd.CommandText = String.Format("update {0} set RegionID=?RegionID, Position=?Position, LookAt=?LookAt, Online='true' where `SessionID`=?SessionID", m_Realm);

            cmd.Parameters.AddWithValue("?SessionID", sessionID.ToString());
            cmd.Parameters.AddWithValue("?RegionID", regionID.ToString());
            cmd.Parameters.AddWithValue("?Position", position.ToString());
            cmd.Parameters.AddWithValue("?LookAt", lookAt.ToString());

            if (ExecuteNonQuery(cmd) == 0)
                return false;

            return true;
        }

        public bool SetHomeLocation(string userID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            PresenceData[] pd = Get("UserID", userID);
            if (pd.Length == 0)
                return false;

            MySqlCommand cmd = new MySqlCommand();

            cmd.CommandText = String.Format("update {0} set HomeRegionID=?HomeRegionID, HomePosition=?HomePosition, HomeLookAt=?HomeLookAt where UserID=?UserID", m_Realm);

            cmd.Parameters.AddWithValue("?UserID", userID);
            cmd.Parameters.AddWithValue("?HomeRegionID", regionID.ToString());
            cmd.Parameters.AddWithValue("?HomePosition", position);
            cmd.Parameters.AddWithValue("?HomeLookAt", lookAt);

            if (ExecuteNonQuery(cmd) == 0)
                return false;

            return true;
        }

        public void Prune(string userID)
        {
            MySqlCommand cmd = new MySqlCommand();

            cmd.CommandText = String.Format("select * from {0} where UserID=?UserID", m_Realm);

            cmd.Parameters.AddWithValue("?UserID", userID);

            using (IDataReader reader = cmd.ExecuteReader())
            {

                List<UUID> deleteSessions = new List<UUID>();
                int online = 0;

                while(reader.Read())
                {
                    if (bool.Parse(reader["Online"].ToString()))
                        online++;
                    else
                        deleteSessions.Add(new UUID(reader["SessionID"].ToString()));
                }

                if (online == 0 && deleteSessions.Count > 0)
                    deleteSessions.RemoveAt(0);

                foreach (UUID s in deleteSessions)
                    Delete("SessionID", s.ToString());
            }
        }
    }
}
