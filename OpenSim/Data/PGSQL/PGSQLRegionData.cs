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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ''AS IS'' AND ANY
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
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using RegionFlags = OpenSim.Framework.RegionFlags;
using Npgsql;

namespace OpenSim.Data.PGSQL
{
    /// <summary>
    /// A PGSQL Interface for the Region Server.
    /// </summary>
    public class PGSQLRegionData : IRegionData
    {
        private string m_Realm;
        private List<string> m_ColumnNames = null;
        private string m_ConnectionString;
        private PGSQLManager m_database;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<string, string> m_FieldTypes = new Dictionary<string, string>();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public PGSQLRegionData(string connectionString, string realm)
        {
            m_Realm = realm;
            m_ConnectionString = connectionString;
            m_database = new PGSQLManager(connectionString);

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            {
                conn.Open();
                Migration m = new Migration(conn, GetType().Assembly, "GridStore");
                m.Update();
            }
            LoadFieldTypes();
         }

        private void LoadFieldTypes()
        {
            m_FieldTypes = new Dictionary<string, string>();

            string query = string.Format(@"select column_name,data_type
                        from INFORMATION_SCHEMA.COLUMNS
                       where table_name = lower('{0}');

                ", m_Realm);
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                conn.Open();
                using (NpgsqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        // query produces 0 to many rows of single column, so always add the first item in each row
                        m_FieldTypes.Add((string)rdr[0], (string)rdr[1]);
                    }
                }
            }
        }

        public List<RegionData> Get(string regionName, UUID scopeID)
        {
            string sql = "select * from "+m_Realm+" where lower(\"regionName\") like lower(:regionName) ";
            if (scopeID != UUID.Zero)
                sql += " and \"ScopeID\" = :scopeID";
            sql += " order by lower(\"regionName\")";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("regionName", regionName));
                if (scopeID != UUID.Zero)
                    cmd.Parameters.Add(m_database.CreateParameter("scopeID", scopeID));
                conn.Open();
                return RunCommand(cmd);
            }
        }

        //BA MOD...
        public RegionData GetSpecific(string regionName, UUID scopeID)
        {
            string sql = "select * from " + m_Realm + " where lower(\"regionName\") = lower(:regionName) ";
            if (scopeID != UUID.Zero)
                sql += " and \"ScopeID\" = :scopeID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("regionName", regionName));
                if (scopeID != UUID.Zero)
                    cmd.Parameters.Add(m_database.CreateParameter("scopeID", scopeID));
                conn.Open();
                List<RegionData> ret = RunCommand(cmd);
                if (ret.Count == 0)
                    return null;

                return ret[0];
            }
        }

        public RegionData Get(int posX, int posY, UUID scopeID)
        {
            // extend database search for maximum region size area
            string sql = "select * from "+m_Realm+" where \"locX\" between :startX and :endX and \"locY\" between :startY and :endY";
            if (scopeID != UUID.Zero)
                sql += " and \"ScopeID\" = :scopeID";

            int startX = posX - (int)Constants.MaximumRegionSize;
            int startY = posY - (int)Constants.MaximumRegionSize;
            int endX = posX;
            int endY = posY;

            List<RegionData> ret;
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("startX", startX));
                cmd.Parameters.Add(m_database.CreateParameter("startY", startY));
                cmd.Parameters.Add(m_database.CreateParameter("endX", endX));
                cmd.Parameters.Add(m_database.CreateParameter("endY", endY));
                if (scopeID != UUID.Zero)
                    cmd.Parameters.Add(m_database.CreateParameter("scopeID", scopeID));
                conn.Open();
                ret = RunCommand(cmd);
            }

            if (ret.Count == 0)
                return null;

            // Find the first that contains pos
            RegionData rg = null;
            foreach (RegionData r in ret)
            {
                if (posX >= r.posX && posX < r.posX + r.sizeX
                    && posY >= r.posY && posY < r.posY + r.sizeY)
                {
                    rg = r;
                    break;
                }
            }

            return rg;
        }

        public RegionData Get(UUID regionID, UUID scopeID)
        {
            string sql = "select * from "+m_Realm+" where uuid = :regionID";
            if (scopeID != UUID.Zero)
                sql += " and \"ScopeID\" = :scopeID";
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("regionID", regionID));
                if (scopeID != UUID.Zero)
                    cmd.Parameters.Add(m_database.CreateParameter("scopeID", scopeID));
                conn.Open();
                List<RegionData> ret = RunCommand(cmd);
                if (ret.Count == 0)
                    return null;

                return ret[0];
            }
        }

        public List<RegionData> Get(int startX, int startY, int endX, int endY, UUID scopeID)
        {
            // extend database search for maximum region size area
            string sql = "select * from "+m_Realm+" where \"locX\" between :startX and :endX and \"locY\" between :startY and :endY";
            if (scopeID != UUID.Zero)
                sql += " and \"ScopeID\" = :scopeID";

            int qstartX = startX - (int)Constants.MaximumRegionSize;
            int qstartY = startY - (int)Constants.MaximumRegionSize;

            List<RegionData> dbret;
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("startX", qstartX));
                cmd.Parameters.Add(m_database.CreateParameter("startY", qstartY));
                cmd.Parameters.Add(m_database.CreateParameter("endX", endX));
                cmd.Parameters.Add(m_database.CreateParameter("endY", endY));
                if (scopeID != UUID.Zero)
                    cmd.Parameters.Add(m_database.CreateParameter("scopeID", scopeID));
                conn.Open();

                dbret = RunCommand(cmd);
            }

            List<RegionData> ret = new List<RegionData>();

            if(dbret.Count == 0)
                return ret;

            foreach (RegionData r in dbret)
            {
                if (r.posX + r.sizeX > startX && r.posX <= endX
                    && r.posY + r.sizeY > startY && r.posY <= endY)
                    ret.Add(r);
            }
            return ret;
        }

        public List<RegionData> RunCommand(NpgsqlCommand cmd)
        {
            List<RegionData> retList = new List<RegionData>();

            NpgsqlDataReader result = cmd.ExecuteReader();

            while (result.Read())
            {
                RegionData ret = new RegionData();
                ret.Data = new Dictionary<string, object>();

                UUID regionID;
                UUID.TryParse(result["uuid"].ToString(), out regionID);
                ret.RegionID = regionID;
                UUID scope;
                UUID.TryParse(result["ScopeID"].ToString(), out scope);
                ret.ScopeID = scope;
                ret.RegionName = result["regionName"].ToString();
                ret.posX = Convert.ToInt32(result["locX"]);
                ret.posY = Convert.ToInt32(result["locY"]);
                ret.sizeX = Convert.ToInt32(result["sizeX"]);
                ret.sizeY = Convert.ToInt32(result["sizeY"]);

                if (m_ColumnNames == null)
                {
                    m_ColumnNames = new List<string>();

                    DataTable schemaTable = result.GetSchemaTable();
                    foreach (DataRow row in schemaTable.Rows)
                        m_ColumnNames.Add(row["ColumnName"].ToString());
                }

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

                    ret.Data[s] = result[s].ToString();
                }

                retList.Add(ret);
            }
            return retList;
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

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {

                string update = "update " + m_Realm + " set \"locX\"=:posX, \"locY\"=:posY, \"sizeX\"=:sizeX, \"sizeY\"=:sizeY ";

                foreach (string field in fields)
                {

                    update += ", ";
                    update += " \"" + field + "\" = :" + field;

                    if (m_FieldTypes.ContainsKey(field))
                        cmd.Parameters.Add(m_database.CreateParameter(field, data.Data[field], m_FieldTypes[field]));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter(field, data.Data[field]));
                }

                update += " where uuid = :regionID";

                if (data.ScopeID != UUID.Zero)
                    update += " and \"ScopeID\" = :scopeID";

                cmd.CommandText = update;
                cmd.Connection = conn;
                cmd.Parameters.Add(m_database.CreateParameter("regionID", data.RegionID));
                cmd.Parameters.Add(m_database.CreateParameter("regionName", data.RegionName));
                cmd.Parameters.Add(m_database.CreateParameter("scopeID", data.ScopeID));
                cmd.Parameters.Add(m_database.CreateParameter("posX", data.posX));
                cmd.Parameters.Add(m_database.CreateParameter("posY", data.posY));
                cmd.Parameters.Add(m_database.CreateParameter("sizeX", data.sizeX));
                cmd.Parameters.Add(m_database.CreateParameter("sizeY", data.sizeY));
                conn.Open();
                try
                {
                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        string insert = "insert into " + m_Realm + " (uuid, \"ScopeID\", \"locX\", \"locY\", \"sizeX\", \"sizeY\", \"regionName\", \"" +
                                String.Join("\", \"", fields) +
                                "\") values (:regionID, :scopeID, :posX, :posY, :sizeX, :sizeY, :regionName, :" + String.Join(", :", fields) + ")";

                        cmd.CommandText = insert;

                        try
                        {
                            if (cmd.ExecuteNonQuery() < 1)
                            {
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            m_log.Warn("[PGSQL Grid]: Error inserting into Regions table: " + ex.Message + ", INSERT sql: " + insert);
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_log.Warn("[PGSQL Grid]: Error updating Regions table: " + ex.Message + ", UPDATE sql: " + update);
                }
            }

            return true;
        }

        public bool SetDataItem(UUID regionID, string item, string value)
        {
            string sql = "update " + m_Realm +
                    " set \"" + item + "\" = :" + item + " where uuid = :UUID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("" + item, value));
                cmd.Parameters.Add(m_database.CreateParameter("UUID", regionID));
                conn.Open();
                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }

        public bool Delete(UUID regionID)
        {
            string sql = "delete from " + m_Realm +
                    " where uuid = :UUID";
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("UUID", regionID));
                conn.Open();
                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }

        public List<RegionData> GetDefaultRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.DefaultRegion, scopeID);
        }

        public List<RegionData> GetDefaultHypergridRegions(UUID scopeID)
        {
            return Get((int)RegionFlags.DefaultHGRegion, scopeID);
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
            string sql = "SELECT * FROM " + m_Realm + " WHERE (\"flags\" & " + regionFlags.ToString() + ") <> 0";
            if (scopeID != UUID.Zero)
                sql += " AND \"ScopeID\" = :scopeID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("scopeID", scopeID));
                conn.Open();
                return RunCommand(cmd);
            }
        }
    }
}
