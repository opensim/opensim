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
using System.Data;
using System.Reflection;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using RegionFlags = OpenSim.Framework.RegionFlags;

namespace OpenSim.Data.MySQL
{
    public class MySqlRegionData : MySqlFramework, IRegionData
    {
        private string m_Realm;
        private List<string> m_ColumnNames;
        //private string m_connectionString;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlRegionData(string connectionString, string realm)
                : base(connectionString)
        {
            m_Realm = realm;
            m_connectionString = connectionString;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "GridStore");
                m.Update();
            }
        }

        public List<RegionData> Get(string regionName, UUID scopeID)
        {
            string command = "select * from `"+m_Realm+"` where regionName like ?regionName";
            if (scopeID != UUID.Zero)
                command += " and ScopeID = ?scopeID";

            command += " order by regionName";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?regionName", regionName);
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                return RunCommand(cmd);
            }
        }

        public RegionData Get(int posX, int posY, UUID scopeID)
        {
            string command = "select * from `"+m_Realm+"` where locX = ?posX and locY = ?posY";
            if (scopeID != UUID.Zero)
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?posX", posX.ToString());
                cmd.Parameters.AddWithValue("?posY", posY.ToString());
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                List<RegionData> ret = RunCommand(cmd);
                if (ret.Count == 0)
                    return null;

                return ret[0];
            }
        }

        public RegionData Get(UUID regionID, UUID scopeID)
        {
            string command = "select * from `"+m_Realm+"` where uuid = ?regionID";
            if (scopeID != UUID.Zero)
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?regionID", regionID.ToString());
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                List<RegionData> ret = RunCommand(cmd);
                if (ret.Count == 0)
                    return null;

                return ret[0];
            }
        }

        public List<RegionData> Get(int startX, int startY, int endX, int endY, UUID scopeID)
        {
            string command = "select * from `"+m_Realm+"` where locX between ?startX and ?endX and locY between ?startY and ?endY";
            if (scopeID != UUID.Zero)
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?startX", startX.ToString());
                cmd.Parameters.AddWithValue("?startY", startY.ToString());
                cmd.Parameters.AddWithValue("?endX", endX.ToString());
                cmd.Parameters.AddWithValue("?endY", endY.ToString());
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                return RunCommand(cmd);
            }
        }

        public List<RegionData> RunCommand(MySqlCommand cmd)
        {
            List<RegionData> retList = new List<RegionData>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                cmd.Connection = dbcon;

                using (IDataReader result = cmd.ExecuteReader())
                {
                    while (result.Read())
                    {
                        RegionData ret = new RegionData();
                        ret.Data = new Dictionary<string, object>();

                        ret.RegionID = DBGuid.FromDB(result["uuid"]);
                        ret.ScopeID = DBGuid.FromDB(result["ScopeID"]);

                        ret.RegionName = result["regionName"].ToString();
                        ret.posX = Convert.ToInt32(result["locX"]);
                        ret.posY = Convert.ToInt32(result["locY"]);
                        ret.sizeX = Convert.ToInt32(result["sizeX"]);
                        ret.sizeY = Convert.ToInt32(result["sizeY"]);

                        CheckColumnNames(result);

                        foreach (string s in m_ColumnNames)
                        {
                            if (s == "uuid")
                                continue;
                            if (s == "ScopeID")
                                continue;
                            if (s == "regionName")
                                continue;
                            if (s == "locX")
                                continue;
                            if (s == "locY")
                                continue;

                            object value = result[s];
                            if (value is DBNull)
                                ret.Data[s] = null;
                            else
                                ret.Data[s] = result[s].ToString();
                        }

                        retList.Add(ret);
                    }
                }
            }

            return retList;
        }

        private void CheckColumnNames(IDataReader result)
        {
            if (m_ColumnNames != null)
                return;

            List<string> columnNames = new List<string>();

            DataTable schemaTable = result.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] != null)
                    columnNames.Add(row["ColumnName"].ToString());
            }

            m_ColumnNames = columnNames;
        }

        public bool Store(RegionData data)
        {
            if (data.Data.ContainsKey("uuid"))
                data.Data.Remove("uuid");
            if (data.Data.ContainsKey("ScopeID"))
                data.Data.Remove("ScopeID");
            if (data.Data.ContainsKey("regionName"))
                data.Data.Remove("regionName");
            if (data.Data.ContainsKey("posX"))
                data.Data.Remove("posX");
            if (data.Data.ContainsKey("posY"))
                data.Data.Remove("posY");
            if (data.Data.ContainsKey("sizeX"))
                data.Data.Remove("sizeX");
            if (data.Data.ContainsKey("sizeY"))
                data.Data.Remove("sizeY");
            if (data.Data.ContainsKey("locX"))
                data.Data.Remove("locX");
            if (data.Data.ContainsKey("locY"))
                data.Data.Remove("locY");

            if (data.RegionName.Length > 128)
                data.RegionName = data.RegionName.Substring(0, 128);

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string update = "update `" + m_Realm + "` set locX=?posX, locY=?posY, sizeX=?sizeX, sizeY=?sizeY";
                foreach (string field in fields)
                {
                    update += ", ";
                    update += "`" + field + "` = ?" + field;

                    cmd.Parameters.AddWithValue("?" + field, data.Data[field]);
                }

                update += " where uuid = ?regionID";

                if (data.ScopeID != UUID.Zero)
                    update += " and ScopeID = ?scopeID";

                cmd.CommandText = update;
                cmd.Parameters.AddWithValue("?regionID", data.RegionID.ToString());
                cmd.Parameters.AddWithValue("?regionName", data.RegionName);
                cmd.Parameters.AddWithValue("?scopeID", data.ScopeID.ToString());
                cmd.Parameters.AddWithValue("?posX", data.posX.ToString());
                cmd.Parameters.AddWithValue("?posY", data.posY.ToString());
                cmd.Parameters.AddWithValue("?sizeX", data.sizeX.ToString());
                cmd.Parameters.AddWithValue("?sizeY", data.sizeY.ToString());

                if (ExecuteNonQuery(cmd) < 1)
                {
                    string insert = "insert into `" + m_Realm + "` (`uuid`, `ScopeID`, `locX`, `locY`, `sizeX`, `sizeY`, `regionName`, `" +
                            String.Join("`, `", fields) +
                            "`) values ( ?regionID, ?scopeID, ?posX, ?posY, ?sizeX, ?sizeY, ?regionName, ?" + String.Join(", ?", fields) + ")";

                    cmd.CommandText = insert;

                    if (ExecuteNonQuery(cmd) < 1)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool SetDataItem(UUID regionID, string item, string value)
        {
            using (MySqlCommand cmd = new MySqlCommand("update `" + m_Realm + "` set `" + item + "` = ?" + item + " where uuid = ?UUID"))
            {
                cmd.Parameters.AddWithValue("?" + item, value);
                cmd.Parameters.AddWithValue("?UUID", regionID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool Delete(UUID regionID)
        {
            using (MySqlCommand cmd = new MySqlCommand("delete from `" + m_Realm + "` where uuid = ?UUID"))
            {
                cmd.Parameters.AddWithValue("?UUID", regionID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public List<RegionData> GetDefaultRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.DefaultRegion, scopeID);
        }

        public List<RegionData> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            List<RegionData> regions = Get((int)RegionFlags.FallbackRegion, scopeID);
            RegionDataDistanceCompare distanceComparer = new RegionDataDistanceCompare(x, y);
            regions.Sort(distanceComparer);
            return regions;
        }

        public List<RegionData> GetHyperlinks(UUID scopeID)
        {
            return Get((int)RegionFlags.Hyperlink, scopeID);
        }

        private List<RegionData> Get(int regionFlags, UUID scopeID)
        {
            string command = "select * from `" + m_Realm + "` where (flags & " + regionFlags.ToString() + ") <> 0";
            if (scopeID != UUID.Zero)
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());
    
                return RunCommand(cmd);
            }
        }
    }
}