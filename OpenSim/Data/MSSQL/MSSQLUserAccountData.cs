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
using OpenMetaverse;
using OpenSim.Framework;
using System.Data.SqlClient;
using System.Text;

namespace OpenSim.Data.MSSQL
{
    public class MSSQLUserAccountData : IUserAccountData
    {
        private string m_Realm;
        private List<string> m_ColumnNames = null;
        private string m_ConnectionString;
        private MSSQLManager m_database;

        public MSSQLUserAccountData(string connectionString, string realm)
        {
            m_Realm = realm;
            m_ConnectionString = connectionString;
            m_database = new MSSQLManager(connectionString);

            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            {
                conn.Open();
                Migration m = new Migration(conn, GetType().Assembly, "UserStore");
                m.Update();
            }
        }

        public List<UserAccountData> Query(UUID principalID, UUID scopeID, string query)
        {
            return null;
        }

        public UserAccountData Get(UUID principalID, UUID scopeID)
        {
            UserAccountData ret = new UserAccountData();
            ret.Data = new Dictionary<string, object>();

            string sql = string.Format("select * from {0} where UUID = @principalID", m_Realm);
            if (scopeID != UUID.Zero)
                sql += " and ScopeID = @scopeID";

            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("@principalID", principalID));
                cmd.Parameters.Add(m_database.CreateParameter("@scopeID", scopeID));
                
                conn.Open();
                using (SqlDataReader result = cmd.ExecuteReader())
                {
                    if (result.Read())
                    {
                        ret.PrincipalID = principalID;
                        UUID scope;
                        UUID.TryParse(result["ScopeID"].ToString(), out scope);
                        ret.ScopeID = scope;

                        if (m_ColumnNames == null)
                        {
                            m_ColumnNames = new List<string>();

                            DataTable schemaTable = result.GetSchemaTable();
                            foreach (DataRow row in schemaTable.Rows)
                                m_ColumnNames.Add(row["ColumnName"].ToString());
                        }

                        foreach (string s in m_ColumnNames)
                        {
                            if (s == "UUID")
                                continue;
                            if (s == "ScopeID")
                                continue;

                            ret.Data[s] = result[s].ToString();
                        }
                        return ret;
                    }
                }
            }
            return null;
        }

        public bool Store(UserAccountData data)
        {
            if (data.Data.ContainsKey("UUID"))
                data.Data.Remove("UUID");
            if (data.Data.ContainsKey("ScopeID"))
                data.Data.Remove("ScopeID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand())
            {
                StringBuilder updateBuilder = new StringBuilder();
                updateBuilder.AppendFormat("update {0} set ", m_Realm);
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        updateBuilder.Append(", ");
                    updateBuilder.AppendFormat("{0} = @{0}", field);

                    first = false;
                    cmd.Parameters.Add(m_database.CreateParameter("@" + field, data.Data[field]));
                }

                updateBuilder.Append(" where UUID = @principalID");

                if (data.ScopeID != UUID.Zero)
                    updateBuilder.Append(" and ScopeID = @scopeID");

                cmd.CommandText = updateBuilder.ToString();
                cmd.Connection = conn;
                cmd.Parameters.Add(m_database.CreateParameter("@principalID", data.PrincipalID));
                cmd.Parameters.Add(m_database.CreateParameter("@scopeID", data.ScopeID));
                conn.Open();

                if (cmd.ExecuteNonQuery() < 1)
                {
                    StringBuilder insertBuilder = new StringBuilder();
                    insertBuilder.AppendFormat("insert into {0} (UUID, ScopeID, ", m_Realm);
                    insertBuilder.Append(String.Join(", ", fields));
                    insertBuilder.Append(") values (@principalID, @scopeID, @");
                    insertBuilder.Append(String.Join(", @", fields));
                    insertBuilder.Append(")");

                    cmd.CommandText = insertBuilder.ToString();

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            string sql = string.Format("update {0} set {1} = @{1} where UUID = @UUID", m_Realm, item);
            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("@" + item, value));
                cmd.Parameters.Add(m_database.CreateParameter("@UUID", principalID));

                conn.Open();

                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }
    }
}
