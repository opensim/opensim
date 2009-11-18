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
    public class MySQLGenericTableHandler<T> : MySqlFramework where T: struct
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        protected Dictionary<string, FieldInfo> m_Fields =
                new Dictionary<string, FieldInfo>();

        protected List<string> m_ColumnNames = null;
        protected string m_Realm;
        protected FieldInfo m_DataField = null;

        public MySQLGenericTableHandler(string connectionString,
                string realm, string storeName) : base(connectionString)
        {
            m_Realm = realm;
            if (storeName != String.Empty)
            {
                Assembly assem = GetType().Assembly;

                Migration m = new Migration(m_Connection, assem, storeName);
                m.Update();
            }

            Type t = typeof(T);
            FieldInfo[] fields = t.GetFields(BindingFlags.NonPublic |
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

            m_ColumnNames = new List<string>();

            DataTable schemaTable = reader.GetSchemaTable();
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["ColumnName"] != null &&
                        (!m_Fields.ContainsKey(row["ColumnName"].ToString())))
                    m_ColumnNames.Add(row["ColumnName"].ToString());
            }
        }

        public T[] Get(string field, string key)
        {
            return Get(new string[] { field }, new string[] { key });
        }

        public T[] Get(string[] fields, string[] keys)
        {
            if (fields.Length != keys.Length)
                return new T[0];

            List<string> terms = new List<string>();

            MySqlCommand cmd = new MySqlCommand();

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

        protected T[] DoQuery(MySqlCommand cmd)
        {
            IDataReader reader = ExecuteReader(cmd);
            if (reader == null)
                return new T[0];

            CheckColumnNames(reader);

            List<T> result = new List<T>();

            while(reader.Read())
            {
                T row = new T();

                foreach (string name in m_Fields.Keys)
                {
                    if (m_Fields[name].GetValue(row) is bool)
                    {
                        int v = Convert.ToInt32(reader[name]);
                        m_Fields[name].SetValue(row, v != 0 ? true : false);
                    }
                    else if(m_Fields[name].GetValue(row) is UUID)
                    {
                        UUID uuid = UUID.Zero;

                        UUID.TryParse(reader[name].ToString(), out uuid);
                        m_Fields[name].SetValue(row, uuid);
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
                        data[col] = reader[col].ToString();

                    m_DataField.SetValue(row, data);
                }

                result.Add(row);
            }

            CloseReaderCommand(cmd);

            return result.ToArray();
        }

        public T[] Get(string where)
        {
            MySqlCommand cmd = new MySqlCommand();

            string query = String.Format("select * from {0} where {1}",
                    m_Realm, where);

            cmd.CommandText = query;

            return DoQuery(cmd);
        }

        public bool Store(T row)
        {
            MySqlCommand cmd = new MySqlCommand();

            string query = "";

            return false;
        }

        public bool Delete(string field, string val)
        {
            MySqlCommand cmd = new MySqlCommand();

            cmd.CommandText = String.Format("delete from {0} where `{1}` = ?{1}", m_Realm, field);
            cmd.Parameters.AddWithValue(field, val);

            if (ExecuteNonQuery(cmd) > 0)
                return true;

            return false;
        }
    }
}
