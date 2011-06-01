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
using System.Data.SqlClient;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using System.Text;

namespace OpenSim.Data.MSSQL
{
    public class MSSQLGenericTableHandler<T> where T : class, new()
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_ConnectionString;
        protected MSSQLManager m_database; //used for parameter type translation
        protected Dictionary<string, FieldInfo> m_Fields =
                new Dictionary<string, FieldInfo>();

        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;

        public MSSQLGenericTableHandler(string connectionString,
                string realm, string storeName)
        {
            m_Realm = realm;
            
            m_ConnectionString = connectionString;

            if (storeName != String.Empty)
            {
                using (SqlConnection conn = new SqlConnection(m_ConnectionString))
                {
                    conn.Open();
                    Migration m = new Migration(conn, GetType().Assembly, storeName);
                    m.Update();
                }

            }
            m_database = new MSSQLManager(m_ConnectionString);

            Type t = typeof(T);
            FieldInfo[] fields = t.GetFields(BindingFlags.Public |
                                             BindingFlags.Instance |
                                             BindingFlags.DeclaredOnly);

            if (fields.Length == 0)
                return;

            foreach (FieldInfo f in fields)
            {
                if (f.Name != "Data")
                    m_Fields[f.Name] = f;
                else
                    m_DataField = f;
            }

        }

        private void CheckColumnNames(SqlDataReader reader)
        {
            if (m_ColumnNames != null)
                return;

            m_ColumnNames = new List<string>();

            DataTable schemaTable = reader.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] != null &&
                        (!m_Fields.ContainsKey(row["ColumnName"].ToString())))
                    m_ColumnNames.Add(row["ColumnName"].ToString());

            }
        }

        private List<string> GetConstraints()
        {
            List<string> constraints = new List<string>();
            string query = string.Format(@"SELECT 
                            COL_NAME(ic.object_id,ic.column_id) AS column_name
                            FROM sys.indexes AS i
                            INNER JOIN sys.index_columns AS ic 
                              ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                            WHERE i.is_primary_key = 1 
                            AND i.object_id = OBJECT_ID('{0}');", m_Realm);
            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        // query produces 0 to many rows of single column, so always add the first item in each row
                        constraints.Add((string)rdr[0]);
                    }
                }
                return constraints;
            }
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

            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand())
            {

                for (int i = 0; i < fields.Length; i++)
                {
                    cmd.Parameters.Add(m_database.CreateParameter(fields[i], keys[i]));
                    terms.Add("[" + fields[i] + "] = @" + fields[i]);
                }

                string where = String.Join(" AND ", terms.ToArray());

                string query = String.Format("SELECT * FROM {0} WHERE {1}",
                        m_Realm, where);

                cmd.Connection = conn;
                cmd.CommandText = query;
                conn.Open();
                return DoQuery(cmd);
            }
        }

        protected T[] DoQuery(SqlCommand cmd)
        {
            List<T> result = new List<T>();
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                if (reader == null)
                    return new T[0];

                CheckColumnNames(reader);                

                while (reader.Read())
                {
                    T row = new T();

                    foreach (string name in m_Fields.Keys)
                    {
                        if (m_Fields[name].GetValue(row) is bool)
                        {
                            int v = Convert.ToInt32(reader[name]);
                            m_Fields[name].SetValue(row, v != 0 ? true : false);
                        }
                        else if (m_Fields[name].GetValue(row) is UUID)
                        {
                            UUID uuid = UUID.Zero;

                            UUID.TryParse(reader[name].ToString(), out uuid);
                            m_Fields[name].SetValue(row, uuid);
                        }
                        else if (m_Fields[name].GetValue(row) is int)
                        {
                            int v = Convert.ToInt32(reader[name]);
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
                return result.ToArray();
            }
        }

        public virtual T[] Get(string where)
        {
            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand())
            {

                string query = String.Format("SELECT * FROM {0} WHERE {1}",
                        m_Realm, where);
                cmd.Connection = conn;
                cmd.CommandText = query;

                //m_log.WarnFormat("[MSSQLGenericTable]: SELECT {0} WHERE {1}", m_Realm, where);

                conn.Open();
                return DoQuery(cmd);
            }
        }

        public virtual bool Store(T row)
        {
            List<string> constraintFields = GetConstraints();
            List<KeyValuePair<string, string>> constraints = new List<KeyValuePair<string, string>>();

            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand())
            {

                StringBuilder query = new StringBuilder();
                List<String> names = new List<String>();
                List<String> values = new List<String>();

                foreach (FieldInfo fi in m_Fields.Values)
                {
                    names.Add(fi.Name);
                    values.Add("@" + fi.Name);
                    // Temporarily return more information about what field is unexpectedly null for
                    // http://opensimulator.org/mantis/view.php?id=5403.  This might be due to a bug in the 
                    // InventoryTransferModule or we may be required to substitute a DBNull here.
                    if (fi.GetValue(row) == null)
                        throw new NullReferenceException(
                            string.Format(
                                "[MSSQL GENERIC TABLE HANDLER]: Trying to store field {0} for {1} which is unexpectedly null",
                                fi.Name, row));

                    if (constraintFields.Count > 0 && constraintFields.Contains(fi.Name))
                    {
                        constraints.Add(new KeyValuePair<string, string>(fi.Name, fi.GetValue(row).ToString()));
                    }
                    cmd.Parameters.Add(m_database.CreateParameter(fi.Name, fi.GetValue(row).ToString()));
                }

                if (m_DataField != null)
                {
                    Dictionary<string, string> data =
                            (Dictionary<string, string>)m_DataField.GetValue(row);

                    foreach (KeyValuePair<string, string> kvp in data)
                    {
                        if (constraintFields.Count > 0 && constraintFields.Contains(kvp.Key))
                        {
                            constraints.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Key));
                        }
                        names.Add(kvp.Key);
                        values.Add("@" + kvp.Key);
                        cmd.Parameters.Add(m_database.CreateParameter("@" + kvp.Key, kvp.Value));
                    }

                }

                query.AppendFormat("UPDATE {0} SET ", m_Realm);
                int i = 0;
                for (i = 0; i < names.Count - 1; i++)
                {
                    query.AppendFormat("[{0}] = {1}, ", names[i], values[i]);
                }
                query.AppendFormat("[{0}] = {1} ", names[i], values[i]);
                if (constraints.Count > 0)
                {
                    List<string> terms = new List<string>();
                    for (int j = 0; j < constraints.Count; j++)
                    {
                        terms.Add(" [" + constraints[j].Key + "] = @" + constraints[j].Key);
                    }
                    string where = String.Join(" AND ", terms.ToArray());
                    query.AppendFormat(" WHERE {0} ", where);
                    
                }
                cmd.Connection = conn;
                cmd.CommandText = query.ToString();
                
                conn.Open();
                if (cmd.ExecuteNonQuery() > 0)
                {
                    //m_log.WarnFormat("[MSSQLGenericTable]: Updating {0}", m_Realm);
                    return true;
                }
                else
                {
                    // assume record has not yet been inserted

                    query = new StringBuilder();
                    query.AppendFormat("INSERT INTO {0} ([", m_Realm);
                    query.Append(String.Join("],[", names.ToArray()));
                    query.Append("]) values (" + String.Join(",", values.ToArray()) + ")");
                    cmd.Connection = conn;
                    cmd.CommandText = query.ToString();
                    //m_log.WarnFormat("[MSSQLGenericTable]: Inserting into {0}", m_Realm);
                    if (conn.State != ConnectionState.Open)
                        conn.Open();
                    if (cmd.ExecuteNonQuery() > 0)
                        return true;
                }

                return false;
            }
        }

        public virtual bool Delete(string field, string key)
        {
            return Delete(new string[] { field }, new string[] { key });
        }

        public virtual bool Delete(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return false;

            List<string> terms = new List<string>();

            using (SqlConnection conn = new SqlConnection(m_ConnectionString))
            using (SqlCommand cmd = new SqlCommand())
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    cmd.Parameters.Add(m_database.CreateParameter(fields[i], keys[i]));
                    terms.Add("[" + fields[i] + "] = @" + fields[i]);
                }

                string where = String.Join(" AND ", terms.ToArray());

                string query = String.Format("DELETE FROM {0} WHERE {1}", m_Realm, where);

                cmd.Connection = conn;
                cmd.CommandText = query;
                conn.Open();

                if (cmd.ExecuteNonQuery() > 0)
                {
                    //m_log.Warn("[MSSQLGenericTable]: " + deleteCommand);
                    return true;
                }
                return false;
            }
        }
    }
}
