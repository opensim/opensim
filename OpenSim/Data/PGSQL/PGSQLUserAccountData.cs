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
using System.Text;
using Npgsql;
using log4net;
using System.Reflection;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLUserAccountData : PGSQLGenericTableHandler<UserAccountData>,IUserAccountData
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        
        public PGSQLUserAccountData(string connectionString, string realm) :
            base(connectionString, realm, "UserAccount")
        {
        }
        
        /* 
        private string m_Realm;
        private List<string> m_ColumnNames = null;
        private PGSQLManager m_database;

        public PGSQLUserAccountData(string connectionString, string realm) :
            base(connectionString, realm, "UserAccount")
        {
            m_Realm = realm;
            m_ConnectionString = connectionString;
            m_database = new PGSQLManager(connectionString);

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            {
                conn.Open();
                Migration m = new Migration(conn, GetType().Assembly, "UserAccount");
                m.Update();
            }
        }
        */
        /*
        public List<UserAccountData> Query(UUID principalID, UUID scopeID, string query)
        {
            return null;
        }
        */
        /*
        public override UserAccountData[] Get(string[] fields, string[] keys)
        {
            UserAccountData[] retUA = base.Get(fields,keys);

            if (retUA.Length > 0)
            {
                Dictionary<string, string> data = retUA[0].Data;
                Dictionary<string, string> data2 = new Dictionary<string, string>();

                foreach (KeyValuePair<string,string> chave in data)
                {
                    string s2 = chave.Key;

                    data2[s2] = chave.Value;

                    if (!m_FieldTypes.ContainsKey(chave.Key))
                    {
                        string tipo = "";
                        m_FieldTypes.TryGetValue(chave.Key, out tipo);
                        m_FieldTypes.Add(s2, tipo);
                    }
                }
                foreach (KeyValuePair<string, string> chave in data2)
                {
                    if (!retUA[0].Data.ContainsKey(chave.Key))
                        retUA[0].Data.Add(chave.Key, chave.Value);
                }
            }

            return retUA;
        }
        */
        /*
        public UserAccountData Get(UUID principalID, UUID scopeID)
        {
            UserAccountData ret = new UserAccountData();
            ret.Data = new Dictionary<string, string>();

            string sql = string.Format(@"select * from {0} where ""PrincipalID"" = :principalID", m_Realm);
            if (scopeID != UUID.Zero)
                sql += @" and ""ScopeID"" = :scopeID";

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(m_database.CreateParameter("principalID", principalID));
                cmd.Parameters.Add(m_database.CreateParameter("scopeID", scopeID));
                
                conn.Open();
                using (NpgsqlDataReader result = cmd.ExecuteReader())
                {
                    if (result.Read())
                    {
                        ret.PrincipalID = principalID;
                        UUID scope;
                        UUID.TryParse(result["scopeid"].ToString(), out scope);
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
                            string s2 = s;
                            if (s2 == "uuid")
                                continue;
                            if (s2 == "scopeid")
                                continue;

                            ret.Data[s] = result[s].ToString();
                        }
                        return ret;
                    }
                }
            }
            return null;
        }
        
        
        public override bool Store(UserAccountData data)
        {
            if (data.Data.ContainsKey("PrincipalID"))
                data.Data.Remove("PrincipalID");
            if (data.Data.ContainsKey("ScopeID"))
                data.Data.Remove("ScopeID");

            string[] fields = new List<string>(data.Data.Keys).ToArray();

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                m_log.DebugFormat("[USER]: Try to update user {0} {1}", data.FirstName, data.LastName);

                StringBuilder updateBuilder = new StringBuilder();
                updateBuilder.AppendFormat("update {0} set ", m_Realm);
                bool first = true;
                foreach (string field in fields)
                {
                    if (!first)
                        updateBuilder.Append(", ");
                    updateBuilder.AppendFormat("\"{0}\" = :{0}", field);

                    first = false;
                    if (m_FieldTypes.ContainsKey(field))
                        cmd.Parameters.Add(m_database.CreateParameter("" + field, data.Data[field], m_FieldTypes[field]));
                    else
                        cmd.Parameters.Add(m_database.CreateParameter("" + field, data.Data[field]));
                }

                updateBuilder.Append(" where \"PrincipalID\" = :principalID");

                if (data.ScopeID != UUID.Zero)
                    updateBuilder.Append(" and \"ScopeID\" = :scopeID");

                cmd.CommandText = updateBuilder.ToString();
                cmd.Connection = conn;
                cmd.Parameters.Add(m_database.CreateParameter("principalID", data.PrincipalID));
                cmd.Parameters.Add(m_database.CreateParameter("scopeID", data.ScopeID));

                m_log.DebugFormat("[USER]: SQL update user {0} ", cmd.CommandText);

                conn.Open();

                m_log.DebugFormat("[USER]: CON opened update user {0} ", cmd.CommandText);

                int conta = 0;
                try
                {
                    conta = cmd.ExecuteNonQuery();
                }
                catch (Exception e){
                    m_log.ErrorFormat("[USER]: ERROR opened update user {0} ", e.Message);
                }
                

                if (conta < 1)
                {
                    m_log.DebugFormat("[USER]: Try to insert user {0} {1}", data.FirstName, data.LastName);

                    StringBuilder insertBuilder = new StringBuilder();
                    insertBuilder.AppendFormat(@"insert into {0} (""PrincipalID"", ""ScopeID"", ""FirstName"", ""LastName"", """, m_Realm);
                    insertBuilder.Append(String.Join(@""", """, fields));
                    insertBuilder.Append(@""") values (:principalID, :scopeID, :FirstName, :LastName, :");
                    insertBuilder.Append(String.Join(", :", fields));
                    insertBuilder.Append(");");

                    cmd.Parameters.Add(m_database.CreateParameter("FirstName", data.FirstName));
                    cmd.Parameters.Add(m_database.CreateParameter("LastName", data.LastName));

                    cmd.CommandText = insertBuilder.ToString();

                    if (cmd.ExecuteNonQuery() < 1)
                    {
                        return false;
                    }
                }
                else
                    m_log.DebugFormat("[USER]: User {0} {1} exists", data.FirstName, data.LastName);
            }
            return true;
        }
        

        public bool Store(UserAccountData data, UUID principalID, string token)
        {
            return false;
        }

        
        public bool SetDataItem(UUID principalID, string item, string value)
        {
            string sql = string.Format(@"update {0} set {1} = :{1} where ""UUID"" = :UUID", m_Realm, item);
            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
            {
                if (m_FieldTypes.ContainsKey(item))
                    cmd.Parameters.Add(m_database.CreateParameter("" + item, value, m_FieldTypes[item]));
                else
                    cmd.Parameters.Add(m_database.CreateParameter("" + item, value));

                cmd.Parameters.Add(m_database.CreateParameter("UUID", principalID));
                conn.Open();

                if (cmd.ExecuteNonQuery() > 0)
                    return true;
            }
            return false;
        }
        */
        /*
        public UserAccountData[] Get(string[] keys, string[] vals)
        {
            return null;
        }
        */

        public UserAccountData[] GetUsers(UUID scopeID, string query)
        {
            string[] words = query.Split(new char[] { ' ' });

            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < 3)
                {
                    if (i != words.Length - 1)
                        Array.Copy(words, i + 1, words, i, words.Length - i - 1);
                    Array.Resize(ref words, words.Length - 1);
                }
            }

            if (words.Length == 0)
                return new UserAccountData[0];

            if (words.Length > 2)
                return new UserAccountData[0];

            string sql = "";
            UUID scope_id;
            UUID.TryParse(scopeID.ToString(), out scope_id); 

            using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
            using (NpgsqlCommand cmd = new NpgsqlCommand())
            {
                if (words.Length == 1)
                {
                    sql = String.Format(@"select * from {0} where (""ScopeID""=:ScopeID or ""ScopeID""=:UUIDZero) and (""FirstName"" ilike :search or ""LastName"" ilike :search)", m_Realm);
                    cmd.Parameters.Add(m_database.CreateParameter("scopeID", (UUID)scope_id));
                    cmd.Parameters.Add (m_database.CreateParameter("UUIDZero", (UUID)UUID.Zero));
                    cmd.Parameters.Add(m_database.CreateParameter("search", "%" + words[0] + "%"));
                }
                else
                {
                    sql = String.Format(@"select * from {0} where (""ScopeID""=:ScopeID or ""ScopeID""=:UUIDZero) and (""FirstName"" ilike :searchFirst or ""LastName"" ilike :searchLast)", m_Realm);
                    cmd.Parameters.Add(m_database.CreateParameter("searchFirst", "%" + words[0] + "%"));
                    cmd.Parameters.Add(m_database.CreateParameter("searchLast", "%" + words[1] + "%"));
                    cmd.Parameters.Add (m_database.CreateParameter("UUIDZero", (UUID)UUID.Zero));
                    cmd.Parameters.Add(m_database.CreateParameter("ScopeID", (UUID)scope_id));
                }
                cmd.Connection = conn;
                cmd.CommandText = sql;
                conn.Open();
                return DoQuery(cmd);
            }
        }
    }
}
