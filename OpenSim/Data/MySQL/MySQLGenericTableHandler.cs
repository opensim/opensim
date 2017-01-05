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
using log4net;
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.MySQL
{
    public class MySQLGenericTableHandler<T> : MySqlFramework where T: class, new()
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<string, FieldInfo> m_Fields =
                new Dictionary<string, FieldInfo>();

        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySQLGenericTableHandler(string connectionString,
                string realm, string storeName) : base(connectionString)
        {
            m_Realm = realm;
            m_connectionString = connectionString;

            if (storeName != String.Empty)
            {
                using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
                {
                    dbcon.Open();
                    Migration m = new Migration(dbcon, Assembly, storeName);
                    m.Update();
                }
            }

            Type t = typeof(T);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            if (fields.Length == 0)
                return;

            foreach (FieldInfo f in  fields)
            {
                if (f.Name != "Data")
                    m_Fields[f.Name] = f;
                else
                    m_DataField = f;
            }
        }

        private void CheckColumnNames(IDataReader reader)
        {
            if (m_ColumnNames != null)
                return;

            List<string> columnNames = new List<string>();

            DataTable schemaTable = reader.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] != null &&
                        (!m_Fields.ContainsKey(row["ColumnName"].ToString())))
                    columnNames.Add(row["ColumnName"].ToString());
            }

            m_ColumnNames = columnNames;
        }

        public virtual T[] Get(string field, string key)
        {
            return Get(new string[] { field }, new string[] { key });
        }

        public virtual T[] Get(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return new T[0];

            List<string> terms = new List<string>();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                for (int i = 0 ; i < fields.Length ; i++)
                {
                    cmd.Parameters.AddWithValue(fields[i], keys[i]);
                    terms.Add("`" + fields[i] + "` = ?" + fields[i]);
                }

                string where = String.Join(" and ", terms.ToArray());

                string query = String.Format("select * from {0} where {1}",
                                             m_Realm, where);

                cmd.CommandText = query;

                return DoQuery(cmd);
            }
        }

        protected T[] DoQuery(MySqlCommand cmd)
        {
            List<T> result = new List<T>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                cmd.Connection = dbcon;

                using (IDataReader reader = cmd.ExecuteReader())
                {
                    if (reader == null)
                        return new T[0];

                    CheckColumnNames(reader);

                    while (reader.Read())
                    {
                        T row = new T();

                        foreach (string name in m_Fields.Keys)
                        {
                            if (reader[name] is DBNull)
                            {
                                continue;
                            }
                            if (m_Fields[name].FieldType == typeof(bool))
                            {
                                int v = Convert.ToInt32(reader[name]);
                                m_Fields[name].SetValue(row, v != 0 ? true : false);
                            }
                            else if (m_Fields[name].FieldType == typeof(UUID))
                            {
                                m_Fields[name].SetValue(row, DBGuid.FromDB(reader[name]));
                            }
                            else if (m_Fields[name].FieldType == typeof(int))
                            {
                                int v = Convert.ToInt32(reader[name]);
                                m_Fields[name].SetValue(row, v);
                            }
                            else if (m_Fields[name].FieldType == typeof(uint))
                            {
                                uint v = Convert.ToUInt32(reader[name]);
                                m_Fields[name].SetValue(row, v);
                            }
                            else
                            {
                                m_Fields[name].SetValue(row, reader[name]);
                            }
                        }

                        if (m_DataField != null)
                        {
                            Dictionary<string, string> data =
                                new Dictionary<string, string>();

                            foreach (string col in m_ColumnNames)
                            {
                                data[col] = reader[col].ToString();
                                if (data[col] == null)
                                    data[col] = String.Empty;
                            }

                            m_DataField.SetValue(row, data);
                        }

                        result.Add(row);
                    }
                }
            }

            return result.ToArray();
        }

        public virtual T[] Get(string where)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                string query = String.Format("select * from {0} where {1}",
                                             m_Realm, where);

                cmd.CommandText = query;

                return DoQuery(cmd);
            }
        }

        public virtual bool Store(T row)
        {
//            m_log.DebugFormat("[MYSQL GENERIC TABLE HANDLER]: Store(T row) invoked");

            using (MySqlCommand cmd = new MySqlCommand())
            {
                string query = "";
                List<String> names = new List<String>();
                List<String> values = new List<String>();

                foreach (FieldInfo fi in m_Fields.Values)
                {
                    names.Add(fi.Name);
                    values.Add("?" + fi.Name);

                    // Temporarily return more information about what field is unexpectedly null for
                    // http://opensimulator.org/mantis/view.php?id=5403.  This might be due to a bug in the
                    // InventoryTransferModule or we may be required to substitute a DBNull here.
                    if (fi.GetValue(row) == null)
                        throw new NullReferenceException(
                            string.Format(
                                "[MYSQL GENERIC TABLE HANDLER]: Trying to store field {0} for {1} which is unexpectedly null",
                                fi.Name, row));

                    cmd.Parameters.AddWithValue(fi.Name, fi.GetValue(row).ToString());
                }

                if (m_DataField != null)
                {
                    Dictionary<string, string> data =
                        (Dictionary<string, string>)m_DataField.GetValue(row);

                    foreach (KeyValuePair<string, string> kvp in data)
                    {
                        names.Add(kvp.Key);
                        values.Add("?" + kvp.Key);
                        cmd.Parameters.AddWithValue("?" + kvp.Key, kvp.Value);
                    }
                }

                query = String.Format("replace into {0} (`", m_Realm) + String.Join("`,`", names.ToArray()) + "`) values (" + String.Join(",", values.ToArray()) + ")";

                cmd.CommandText = query;

                if (ExecuteNonQuery(cmd) > 0)
                    return true;

                return false;
            }
        }

        public virtual bool Delete(string field, string key)
        {
            return Delete(new string[] { field }, new string[] { key });
        }

        public virtual bool Delete(string[] fields, string[] keys)
        {
//            m_log.DebugFormat(
//                "[MYSQL GENERIC TABLE HANDLER]: Delete(string[] fields, string[] keys) invoked with {0}:{1}",
//                string.Join(",", fields), string.Join(",", keys));

            if (fields.Length != keys.Length)
                return false;

            List<string> terms = new List<string>();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                for (int i = 0 ; i < fields.Length ; i++)
                {
                    cmd.Parameters.AddWithValue(fields[i], keys[i]);
                    terms.Add("`" + fields[i] + "` = ?" + fields[i]);
                }

                string where = String.Join(" and ", terms.ToArray());

                string query = String.Format("delete from {0} where {1}", m_Realm, where);

                cmd.CommandText = query;

                return ExecuteNonQuery(cmd) > 0;
            }
        }

        public long GetCount(string field, string key)
        {
            return GetCount(new string[] { field }, new string[] { key });
        }

        public long GetCount(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return 0;

            List<string> terms = new List<string>();

            using (MySqlCommand cmd = new MySqlCommand())
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    cmd.Parameters.AddWithValue(fields[i], keys[i]);
                    terms.Add("`" + fields[i] + "` = ?" + fields[i]);
                }

                string where = String.Join(" and ", terms.ToArray());

                string query = String.Format("select count(*) from {0} where {1}",
                                             m_Realm, where);

                cmd.CommandText = query;

                Object result = DoQueryScalar(cmd);

                return Convert.ToInt64(result);
            }
        }

        public long GetCount(string where)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                string query = String.Format("select count(*) from {0} where {1}",
                                             m_Realm, where);

                cmd.CommandText = query;

                object result = DoQueryScalar(cmd);

                return Convert.ToInt64(result);
            }
        }

        public object DoQueryScalar(MySqlCommand cmd)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                cmd.Connection = dbcon;

                return cmd.ExecuteScalar();
            }
        }

    }
}
