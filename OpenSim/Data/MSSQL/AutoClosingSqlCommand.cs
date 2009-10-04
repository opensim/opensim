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

using System.Data;
using System.Data.SqlClient;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// Encapsulates a SqlCommand object but ensures that when it is disposed, its connection is closed and disposed also.
    /// </summary>
    internal class AutoClosingSqlCommand : IDbCommand
    {
        private SqlCommand realCommand;

        public AutoClosingSqlCommand(SqlCommand cmd)
        {
            realCommand = cmd;
        }

        #region IDbCommand Members

        public void Cancel()
        {
            realCommand.Cancel();
        }

        public string CommandText
        {
            get
            {
                return realCommand.CommandText;
            }
            set
            {
                realCommand.CommandText = value;
            }
        }

        public int CommandTimeout
        {
            get
            {
                return realCommand.CommandTimeout;
            }
            set
            {
                realCommand.CommandTimeout = value;
            }
        }

        public CommandType CommandType
        {
            get
            {
                return realCommand.CommandType;
            }
            set
            {
                realCommand.CommandType = value;
            }
        }

        IDbConnection IDbCommand.Connection
        {
            get
            {
                return realCommand.Connection;
            }
            set
            {
                realCommand.Connection = (SqlConnection) value;
            }
        }

        public SqlConnection Connection
        {
            get
            {
                return realCommand.Connection;
            }
        }

        IDbDataParameter IDbCommand.CreateParameter()
        {
            return realCommand.CreateParameter();
        }

        public SqlParameter CreateParameter()
        {
            return realCommand.CreateParameter();
        }

        public int ExecuteNonQuery()
        {
            return realCommand.ExecuteNonQuery();
        }

        IDataReader IDbCommand.ExecuteReader(CommandBehavior behavior)
        {
            return realCommand.ExecuteReader(behavior);
        }

        public SqlDataReader ExecuteReader(CommandBehavior behavior)
        {
            return realCommand.ExecuteReader(behavior);
        }

        IDataReader IDbCommand.ExecuteReader()
        {
            return realCommand.ExecuteReader();
        }

        public SqlDataReader ExecuteReader()
        {
            return realCommand.ExecuteReader();
        }

        public object ExecuteScalar()
        {
            return realCommand.ExecuteScalar();
        }

        IDataParameterCollection IDbCommand.Parameters
        {
            get { return realCommand.Parameters; }
        }

        public SqlParameterCollection Parameters
        {
            get { return realCommand.Parameters; }
        }

        public void Prepare()
        {
            realCommand.Prepare();
        }

//        IDbTransaction IDbCommand.Transaction
//        {
//            get
//            {
//                return realCommand.Transaction;
//            }
//            set
//            {
//                realCommand.Transaction = (SqlTransaction) value;
//            }
//        }

        public IDbTransaction Transaction
        {
            get { return realCommand.Transaction; }
            set { realCommand.Transaction = (SqlTransaction)value; }
        }

        UpdateRowSource IDbCommand.UpdatedRowSource
        {
            get
            {
                return realCommand.UpdatedRowSource;
            }
            set
            {
                realCommand.UpdatedRowSource = value;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            SqlConnection conn = realCommand.Connection;
            try { realCommand.Dispose(); }
            finally
            {
                try { conn.Dispose(); }
                finally { }
            }
        }

        #endregion
    }
}
