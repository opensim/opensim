/*
* Copyright (c) Tribal Media AB, http://tribalmedia.se/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * The name of Tribal Media AB may not be used to endorse or promote products
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
* 
*/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace TribalMedia.Framework.Data
{
    public abstract class BaseDatabaseConnector
    {
        protected string m_connectionString;

        public BaseDatabaseConnector(string connectionString)
        {
            m_connectionString = connectionString;
        }

        public abstract DbConnection GetNewConnection();
        public abstract string CreateParamName(string fieldName);

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

        public string CreateCondition(BaseTableMapper mapper, DbCommand command, string fieldName, object key)
        {
            string keyFieldParamName = mapper.CreateParamName(fieldName);

            DbParameter param = command.CreateParameter();
            param.ParameterName = keyFieldParamName;
            param.Value = ConvertToDbType(key);
            command.Parameters.Add(param);

            return String.Format("{0}={1}", fieldName, keyFieldParamName);
        }

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

        public virtual object ConvertToDbType(object value)
        {
            return value;
        }

        public abstract BaseDataReader CreateReader(IDataReader reader);
    }
}