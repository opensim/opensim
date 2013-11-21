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
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Connection string for ADO.net
        /// </summary>
        private readonly string connectionString;

        /// <summary>
        /// Initialize the manager and set the connectionstring
        /// </summary>
        /// <param name="connection"></param>
        public MSSQLManager(string connection)
        {
            connectionString = connection;
        }

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
            if (type == typeof(DateTime))
            {
                return SqlDbType.DateTime;
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

        /// <summary>
        /// Checks if we need to do some migrations to the database
        /// </summary>
        /// <param name="migrationStore">migrationStore.</param>
        public void CheckMigration(string migrationStore)
        {
            using (SqlConnection connection = new SqlConnection(connectionString)) 
            {
                connection.Open();
                Assembly assem = GetType().Assembly;
                MSSQLMigration migration = new MSSQLMigration(connection, assem, migrationStore);

                migration.Update();
            }
        }

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
