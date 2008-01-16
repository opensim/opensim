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
using System.Data;
using System.Data.Common;
using TribalMedia.Framework.Data;

namespace TribalMedia.Framework.Data
{
    public abstract class BaseTableMapper
    {
        private readonly BaseDatabaseConnector m_database;
        private readonly object m_syncRoot = new object();

        protected void WithConnection(Action<DbConnection> action)
        {
            lock (m_syncRoot)
            {
                DbConnection m_connection = m_database.GetNewConnection();

                if (m_connection.State != ConnectionState.Open)
                {
                    m_connection.Open();
                }

                action(m_connection);

                if (m_connection.State == ConnectionState.Open)
                {
                    m_connection.Close();
                }
            }
        }

        private readonly string m_tableName;
        public string TableName
        {
            get { return m_tableName; }
        }

        protected BaseSchema m_schema;
        public BaseSchema Schema
        {
            get { return m_schema; }
        }

        protected BaseFieldMapper m_keyFieldMapper;
        public BaseFieldMapper KeyFieldMapper
        {
            get { return m_keyFieldMapper; }
        }

        public BaseTableMapper(BaseDatabaseConnector database, string tableName)
        {
            m_database = database;
            m_tableName = tableName.ToLower(); // Stupid MySQL hack.
        }

        public string CreateParamName(string fieldName)
        {
            return m_database.CreateParamName(fieldName);
        }

        protected DbCommand CreateSelectCommand(DbConnection connection, string fieldName, object primaryKey)
        {
            return m_database.CreateSelectCommand(this, connection, fieldName, primaryKey);
        }

        public string CreateCondition(DbCommand command, string fieldName, object key)
        {
            return m_database.CreateCondition(this, command, fieldName, key);
        }

        public DbCommand CreateInsertCommand(DbConnection connection, object obj)
        {
            return m_database.CreateInsertCommand(this, connection, obj);
        }

        public DbCommand CreateUpdateCommand(DbConnection connection, object rowMapper, object primaryKey)
        {
            return m_database.CreateUpdateCommand(this, connection, rowMapper, primaryKey);
        }

        public object ConvertToDbType(object value)
        {
            return m_database.ConvertToDbType(value);
        }

        protected virtual BaseDataReader CreateReader(IDataReader reader)
        {
            return m_database.CreateReader(reader);
        }
    }

    public abstract class BaseTableMapper<TRowMapper, TPrimaryKey> : BaseTableMapper
    {
        public BaseTableMapper(BaseDatabaseConnector database, string tableName)
            : base(database, tableName)
        {
        }

        public bool TryGetValue(TPrimaryKey primaryKey, out TRowMapper value)
        {
            TRowMapper result = default(TRowMapper);
            bool success = false;

            WithConnection(delegate(DbConnection connection)
                               {
                                   using (
                                       DbCommand command =
                                           CreateSelectCommand(connection, KeyFieldMapper.FieldName, primaryKey))
                                   {
                                       using (IDataReader reader = command.ExecuteReader())
                                       {
                                           if (reader.Read())
                                           {
                                               result = FromReader( CreateReader(reader));
                                               success = true;
                                           }
                                           else
                                           {
                                               success = false;
                                           }
                                       }
                                   }
                               });

            value = result;

            return success;
        }

        public virtual bool Remove(TPrimaryKey id)
        {
            int deleted = 0;

            WithConnection(delegate(DbConnection connection)
                               {
                                   using (
                                       DbCommand command =
                                           CreateDeleteCommand(connection, KeyFieldMapper.FieldName, id))
                                   {
                                       deleted = command.ExecuteNonQuery();
                                   }
                               });

            if (deleted == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        public DbCommand CreateDeleteCommand(DbConnection connection, string fieldName, TPrimaryKey primaryKey)
        {
            string table = TableName;

            DbCommand command = connection.CreateCommand();

            string conditionString = CreateCondition(command, fieldName, primaryKey);

            string query =
                String.Format("delete from {0} where {1}", table, conditionString);

            command.CommandText = query;
            command.CommandType = CommandType.Text;

            return command;
        }

        public virtual bool Update(TPrimaryKey primaryKey, TRowMapper value)
        {
            int updated = 0;

            WithConnection(delegate(DbConnection connection)
                               {
                                   using (DbCommand command = CreateUpdateCommand(connection, value, primaryKey))
                                   {
                                       updated = command.ExecuteNonQuery();
                                   }
                               });

            if (updated == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual bool Add(TRowMapper value)
        {
            int added = 0;

            WithConnection(delegate(DbConnection connection)
                               {
                                   using (DbCommand command = CreateInsertCommand(connection, value))
                                   {
                                       added = command.ExecuteNonQuery();
                                   }
                               });

            if (added == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public abstract TRowMapper FromReader(BaseDataReader reader);
    }
}