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
using System.Reflection;
using System.Data;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    public class MySqlAuthenticationData : MySqlFramework, IAuthenticationData
    {
        private string m_Realm;
        private List<string> m_ColumnNames;
        private int m_LastExpire;
        // private string m_connectionString;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlAuthenticationData(string connectionString, string realm)
                : base(connectionString)
        {
            m_Realm = realm;
            m_connectionString = connectionString;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "AuthStore");
                m.Update();
            }
        }

        public AuthenticationData Get(UUID principalID)
        {
            AuthenticationData ret = new AuthenticationData();
            ret.Data = new Dictionary<string, object>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("select * from `" + m_Realm + "` where UUID = ?principalID", dbcon))
                {
                    cmd.Parameters.AddWithValue("?principalID", principalID.ToString());

                    IDataReader result = cmd.ExecuteReader();

                    if (result.Read())
                    {
                        ret.PrincipalID = principalID;

                        CheckColumnNames(result);

                        foreach (string s in m_ColumnNames)
                        {
                            if (s == "UUID")
                                continue;

                            ret.Data[s] = result[s].ToString();
                        }

                        return ret;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        private void CheckColumnNames(IDataReader result)
        {
            if (m_ColumnNames != null)
                return;

            List<string> columnNames = new List<string>();

            DataTable schemaTable = result.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
                columnNames.Add(row["ColumnName"].ToString());

            m_ColumnNames = columnNames;
        }

        public bool Store(AuthenticationData data)
        {
            if (data.Data.ContainsKey("UUID"))
                data.Data.Remove("UUID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string update = "update `"+m_Realm+"` set ";
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        update += ", ";
                    update += "`" + field + "` = ?"+field;

                    first = false;

                    cmd.Parameters.AddWithValue("?"+field, data.Data[field]);
                }

                update += " where UUID = ?principalID";

                cmd.CommandText = update;
                cmd.Parameters.AddWithValue("?principalID", data.PrincipalID.ToString());

                if (ExecuteNonQuery(cmd) < 1)
                {
                    string insert = "insert into `" + m_Realm + "` (`UUID`, `" +
                            String.Join("`, `", fields) +
                            "`) values (?principalID, ?" + String.Join(", ?", fields) + ")";

                    cmd.CommandText = insert;

                    if (ExecuteNonQuery(cmd) < 1)
                        return false;
                }
            }

            return true;
        }

        public bool SetDataItem(UUID principalID, string item, string value)
        {
            using (MySqlCommand cmd
                = new MySqlCommand("update `" + m_Realm + "` set `" + item + "` = ?" + item + " where UUID = ?UUID"))
            {
                cmd.Parameters.AddWithValue("?"+item, value);
                cmd.Parameters.AddWithValue("?UUID", principalID.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool SetToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd
                = new MySqlCommand(
                    "insert into tokens (UUID, token, validity) values (?principalID, ?token, date_add(now(), interval ?lifetime minute))"))
            {
                cmd.Parameters.AddWithValue("?principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("?token", token);
                cmd.Parameters.AddWithValue("?lifetime", lifetime.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        public bool CheckToken(UUID principalID, string token, int lifetime)
        {
            if (System.Environment.TickCount - m_LastExpire > 30000)
                DoExpire();

            using (MySqlCommand cmd
                = new MySqlCommand(
                    "update tokens set validity = date_add(now(), interval ?lifetime minute) where UUID = ?principalID and token = ?token and validity > now()"))
            {
                cmd.Parameters.AddWithValue("?principalID", principalID.ToString());
                cmd.Parameters.AddWithValue("?token", token);
                cmd.Parameters.AddWithValue("?lifetime", lifetime.ToString());

                if (ExecuteNonQuery(cmd) > 0)
                    return true;
            }

            return false;
        }

        private void DoExpire()
        {
            using (MySqlCommand cmd = new MySqlCommand("delete from tokens where validity < now()"))
            {
                ExecuteNonQuery(cmd);
            }

            m_LastExpire = System.Environment.TickCount;
        }
    }
}