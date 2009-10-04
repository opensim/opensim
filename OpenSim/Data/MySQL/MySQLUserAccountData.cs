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
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    public class MySqlUserAccountData : MySqlFramework, IUserAccountData
    {
        private string m_Realm;
        private List<string> m_ColumnNames = null;
//        private int m_LastExpire = 0;

        public MySqlUserAccountData(string connectionString, string realm)
                : base(connectionString)
        {
            m_Realm = realm;

            Migration m = new Migration(m_Connection, GetType().Assembly, "UserStore");
            m.Update();
        }

        public List<UserAccountData> Query(UUID principalID, UUID scopeID, string query)
        {
            return null;
        }

        public UserAccountData Get(UUID principalID, UUID scopeID)
        {
            UserAccountData ret = new UserAccountData();
            ret.Data = new Dictionary<string, object>();

            string command = "select * from `"+m_Realm+"` where UUID = ?principalID";
            if (scopeID != UUID.Zero)
                command += " and ScopeID = ?scopeID";

            using (MySqlCommand cmd = new MySqlCommand(command))
            {
                cmd.Parameters.AddWithValue("?principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("?scopeID", scopeID.ToString());

                using (IDataReader result = ExecuteReader(cmd))
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

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string update = "update `" + m_Realm + "` set ";
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        update += ", ";
                    update += "`" + field + "` = ?" + field;

                    first = false;

                    cmd.Parameters.AddWithValue("?" + field, data.Data[field]);
                }

                update += " where UUID = ?principalID";

                if (data.ScopeID != UUID.Zero)
                    update += " and ScopeID = ?scopeID";

                cmd.CommandText = update;
                cmd.Parameters.AddWithValue("?principalID", data.PrincipalID.ToString());
                cmd.Parameters.AddWithValue("?scopeID", data.ScopeID.ToString());

                if (ExecuteNonQuery(cmd) < 1)
                {
                    string insert = "insert into `" + m_Realm + "` (`UUID`, `ScopeID`, `" +
                            String.Join("`, `", fields) +
                            "`) values (?principalID, ?scopeID, ?" + String.Join(", ?", fields) + ")";

                    cmd.CommandText = insert;

                    if (ExecuteNonQuery(cmd) < 1)
                    {
                        cmd.Dispose();
                        return false;
                    }
                }
            }

            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            using (MySqlCommand cmd = new MySqlCommand("update `" + m_Realm + "` set `" +
                item + "` = ?" + item + " where UUID = ?UUID"))
            {
                cmd.Parameters.AddWithValue("?" + item, value);
                cmd.Parameters.AddWithValue("?UUID", principalID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }
    }
}
