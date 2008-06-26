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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Data.Common;

namespace OpenSim.Data.Base
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseDatabaseConnector
    {
        protected string m_connectionString;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionString"></param>
        public BaseDatabaseConnector(string connectionString)
        {
            m_connectionString = connectionString;
        }

        public abstract DbConnection GetNewConnection();
        public abstract string CreateParamName(string fieldName);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="connection"></param>
        /// <param name="fieldName"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public DbCommand CreateSelectCommand(BaseTableMapper mapper, DbConnection connection, string fieldName, object key)
        {
            string table = mapper.TableName;

            DbCommand command = connection.CreateCommand();

            string conditionString = CreateCondition(mapper, command, fieldName, key);

            string query =
                String.Format("select * from {0} where {1}", table, conditionString);

            command.CommandText = query;
            command.CommandType = CommandType.Text;

            return command;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="command"></param>
        /// <param name="fieldName"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public string CreateCondition(BaseTableMapper mapper, DbCommand command, string fieldName, object key)
        {
            string keyFieldParamName = mapper.CreateParamName(fieldName);

            DbParameter param = command.CreateParameter();
            param.ParameterName = keyFieldParamName;
            param.Value = ConvertToDbType(key);
            command.Parameters.Add(param);

            return String.Format("{0}={1}", fieldName, keyFieldParamName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="connection"></param>
        /// <param name="rowMapper"></param>
        /// <param name="primaryKey"></param>
        /// <returns></returns>
        public DbCommand CreateUpdateCommand(BaseTableMapper mapper, DbConnection connection, object rowMapper, object primaryKey)
        {
            string table = mapper.TableName;

            List<string> fieldNames = new List<string>();

            DbCommand command = connection.CreateCommand();

            foreach (BaseFieldMapper fieldMapper in mapper.Schema.Fields.Values)
            {
                if (fieldMapper != mapper.KeyFieldMapper)
                {
                    fieldMapper.ExpandField(rowMapper, command, fieldNames);
                }
            }

            List<string> assignments = new List<string>();

            foreach (string field in fieldNames)
            {
                assignments.Add(String.Format("{0}={1}", field, mapper.CreateParamName(field)));
            }

            string conditionString = mapper.CreateCondition(command, mapper.KeyFieldMapper.FieldName, primaryKey);

            command.CommandText =
                String.Format("update {0} set {1} where {2}", table, String.Join(", ", assignments.ToArray()),
                              conditionString);

            return command;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="connection"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public DbCommand CreateInsertCommand(BaseTableMapper mapper, DbConnection connection, object obj)
        {
            string table = mapper.TableName;

            List<string> fieldNames = new List<string>();

            DbCommand command = connection.CreateCommand();

            foreach (BaseFieldMapper fieldMapper in mapper.Schema.Fields.Values)
            {
                fieldMapper.ExpandField(obj, command, fieldNames);
            }

            List<string> paramNames = new List<string>();

            foreach (string field in fieldNames)
            {
                paramNames.Add(mapper.CreateParamName(field));
            }

            command.CommandText =
                String.Format("insert into {0} ({1}) values ({2})", table, String.Join(", ", fieldNames.ToArray()),
                              String.Join(", ", paramNames.ToArray()));

            return command;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual object ConvertToDbType(object value)
        {
            return value;
        }

        public abstract BaseDataReader CreateReader(IDataReader reader);
    }
}
