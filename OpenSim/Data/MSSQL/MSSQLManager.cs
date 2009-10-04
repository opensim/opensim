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
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A management class for the MS SQL Storage Engine
    /// </summary>
    public class MSSQLManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Connection string for ADO.net
        /// </summary>
        private readonly string connectionString;

        public MSSQLManager(string dataSource, string initialCatalog, string persistSecurityInfo, string userId,
                            string password)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = dataSource;
            builder.InitialCatalog = initialCatalog;
            builder.PersistSecurityInfo = Convert.ToBoolean(persistSecurityInfo);
            builder.UserID = userId;
            builder.Password = password;
            builder.ApplicationName = Assembly.GetEntryAssembly().Location;

            connectionString = builder.ToString();
        }

        /// <summary>
        /// Initialize the manager and set the connectionstring
        /// </summary>
        /// <param name="connection"></param>
        public MSSQLManager(string connection)
        {
            connectionString = connection;
        }

        public SqlConnection DatabaseConnection()
        {
            SqlConnection conn = new SqlConnection(connectionString);

            //TODO is this good??? Opening connection here
            conn.Open();

            return conn;
        }

        #region Obsolete functions, can be removed!

        /// <summary>
        ///
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        [Obsolete("Do not use!")]
        protected static void createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        /// <summary>
        /// Define Table function
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
/*
        [Obsolete("Do not use!")]
        protected static string defineTable(DataTable dt)
        {
            string sql = "create table " + dt.TableName + "(";
            string subsql = String.Empty;
            foreach (DataColumn col in dt.Columns)
            {
                if (subsql.Length > 0)
                {
                    // a map function would rock so much here
                    subsql += ",\n";
                }

                subsql += col.ColumnName + " " + SqlType(col.DataType);
                if (col == dt.PrimaryKey[0])
                {
                    subsql += " primary key";
                }
            }
            sql += subsql;
            sql += ")";
            return sql;
        }
*/

        #endregion

        /// <summary>
        /// Type conversion function
        /// </summary>
        /// <param name="type">a type</param>
        /// <returns>a sqltype</returns>
        /// <remarks>this is something we'll need to implement for each db slightly differently.</remarks>
/*
        [Obsolete("Used by a obsolete methods")]
        public static string SqlType(Type type)
        {
            if (type == typeof(String))
            {
                return "varchar(255)";
            }
            if (type == typeof(Int32))
            {
                return "integer";
            }
            if (type == typeof(Double))
            {
                return "float";
            }
            if (type == typeof(Byte[]))
            {
                return "image";
            }
            return "varchar(255)";
        }
*/

        /// <summary>
        /// Type conversion to a SQLDbType functions
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal SqlDbType DbtypeFromType(Type type)
        {
            if (type == typeof(string))
            {
                return SqlDbType.VarChar;
            }
            if (type == typeof(double))
            {
                return SqlDbType.Float;
            }
            if (type == typeof(Single))
            {
                return SqlDbType.Float;
            }
            if (type == typeof(int))
            {
                return SqlDbType.Int;
            }
            if (type == typeof(bool))
            {
                return SqlDbType.Bit;
            }
            if (type == typeof(UUID))
            {
                return SqlDbType.UniqueIdentifier;
            }
            if (type == typeof(sbyte))
            {
                return SqlDbType.Int;
            }
            if (type == typeof(Byte[]))
            {
                return SqlDbType.Image;
            }
            if (type == typeof(uint) || type == typeof(ushort))
            {
                return SqlDbType.Int;
            }
            if (type == typeof(ulong))
            {
                return SqlDbType.BigInt;
            }
            return SqlDbType.VarChar;
        }

        /// <summary>
        /// Creates value for parameter.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        private static object CreateParameterValue(object value)
        {
            Type valueType = value.GetType();

            if (valueType == typeof(UUID)) //TODO check if this works
            {
                return ((UUID) value).Guid;
            }
            if (valueType == typeof(UUID))
            {
                return ((UUID)value).Guid;
            }
            if (valueType == typeof(bool))
            {
                return (bool)value ? 1 : 0;
            }
            if (valueType == typeof(Byte[]))
            {
                return value;
            }
            if (valueType == typeof(int))
            {
                return value;
            }
            return value;
        }

        /// <summary>
        /// Create a parameter for a command
        /// </summary>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <param name="parameterObject">parameter object.</param>
        /// <returns></returns>
        internal SqlParameter CreateParameter(string parameterName, object parameterObject)
        {
            return CreateParameter(parameterName, parameterObject, false);
        }

        /// <summary>
        /// Creates the parameter for a command.
        /// </summary>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <param name="parameterObject">parameter object.</param>
        /// <param name="parameterOut">if set to <c>true</c> parameter is a output parameter</param>
        /// <returns></returns>
        internal SqlParameter CreateParameter(string parameterName, object parameterObject, bool parameterOut)
        {
            //Tweak so we dont always have to add @ sign
            if (!parameterName.StartsWith("@")) parameterName = "@" + parameterName;

            //HACK if object is null, it is turned into a string, there are no nullable type till now
            if (parameterObject == null) parameterObject = "";

            SqlParameter parameter = new SqlParameter(parameterName, DbtypeFromType(parameterObject.GetType()));

            if (parameterOut)
            {
                parameter.Direction = ParameterDirection.Output;
            }
            else
            {
                parameter.Direction = ParameterDirection.Input;
                parameter.Value = CreateParameterValue(parameterObject);
            }

            return parameter;
        }

        private static readonly Dictionary<string, string> emptyDictionary = new Dictionary<string, string>();

        /// <summary>
        /// Run a query and return a sql db command
        /// </summary>
        /// <param name="sql">The SQL query.</param>
        /// <returns></returns>
        internal AutoClosingSqlCommand Query(string sql)
        {
            return Query(sql, emptyDictionary);
        }

        /// <summary>
        /// Runs a query with protection against SQL Injection by using parameterised input.
        /// </summary>
        /// <param name="sql">The SQL string - replace any variables such as WHERE x = "y" with WHERE x = @y</param>
        /// <param name="parameters">The parameters - index so that @y is indexed as 'y'</param>
        /// <returns>A Sql DB Command</returns>
        internal AutoClosingSqlCommand Query(string sql, Dictionary<string, string> parameters)
        {
            SqlCommand dbcommand = DatabaseConnection().CreateCommand();
            dbcommand.CommandText = sql;
            foreach (KeyValuePair<string, string> param in parameters)
            {
                dbcommand.Parameters.AddWithValue(param.Key, param.Value);
            }

            return new AutoClosingSqlCommand(dbcommand);
        }

        /// <summary>
        /// Runs a query with protection against SQL Injection by using parameterised input.
        /// </summary>
        /// <param name="sql">The SQL string - replace any variables such as WHERE x = "y" with WHERE x = @y</param>
        /// <param name="sqlParameter">A parameter - use createparameter to create parameter</param>
        /// <returns></returns>
        internal AutoClosingSqlCommand Query(string sql, SqlParameter sqlParameter)
        {
            SqlCommand dbcommand = DatabaseConnection().CreateCommand();
            dbcommand.CommandText = sql;
            dbcommand.Parameters.Add(sqlParameter);

            return new AutoClosingSqlCommand(dbcommand);
        }

        /// <summary>
        /// Checks if we need to do some migrations to the database
        /// </summary>
        /// <param name="migrationStore">migrationStore.</param>
        public void CheckMigration(string migrationStore)
        {
            using (SqlConnection connection = DatabaseConnection())
            {
                Assembly assem = GetType().Assembly;
                MSSQLMigration migration = new MSSQLMigration(connection, assem, migrationStore);

                migration.Update();
            }
        }

        #region Old Testtable functions

        /// <summary>
        /// Execute a SQL statement stored in a resource, as a string
        /// </summary>
        /// <param name="name">the ressource string</param>
        public void ExecuteResourceSql(string name)
        {
            using (IDbCommand cmd = Query(getResourceString(name), new Dictionary<string, string>()))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Given a list of tables, return the version of the tables, as seen in the database
        /// </summary>
        /// <param name="tableList"></param>
        public void GetTableVersion(Dictionary<string, string> tableList)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["dbname"] = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

            using (IDbCommand tablesCmd =
                Query("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_CATALOG=@dbname", param))
            using (IDataReader tables = tablesCmd.ExecuteReader())
            {
                while (tables.Read())
                {
                    try
                    {
                        string tableName = (string)tables["TABLE_NAME"];
                        if (tableList.ContainsKey(tableName))
                            tableList[tableName] = tableName;
                    }
                    catch (Exception e)
                    {
                        m_log.Error(e.ToString());
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string getResourceString(string name)
        {
            Assembly assem = GetType().Assembly;
            string[] names = assem.GetManifestResourceNames();

            foreach (string s in names)
                if (s.EndsWith(name))
                    using (Stream resource = assem.GetManifestResourceStream(s))
                    {
                        using (StreamReader resourceReader = new StreamReader(resource))
                        {
                            string resourceString = resourceReader.ReadToEnd();
                            return resourceString;
                        }
                    }
            throw new Exception(string.Format("Resource '{0}' was not found", name));
        }

        #endregion

        /// <summary>
        /// Returns the version of this DB provider
        /// </summary>
        /// <returns>A string containing the DB provider</returns>
        public string getVersion()
        {
            Module module = GetType().Module;
            // string dllName = module.Assembly.ManifestModule.Name;
            Version dllVersion = module.Assembly.GetName().Version;

            return
                string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                              dllVersion.Revision);
        }
    }
}
