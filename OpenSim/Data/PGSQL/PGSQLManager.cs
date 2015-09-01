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
using System.IO;
using System.Reflection;
using OpenSim.Framework;
using log4net;
using OpenMetaverse;
using Npgsql;
using NpgsqlTypes;

namespace OpenSim.Data.PGSQL
{
    /// <summary>
    /// A management class for the MS SQL Storage Engine
    /// </summary>
    public class PGSQLManager
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
        public PGSQLManager(string connection)
        {
            connectionString = connection;
            InitializeMonoSecurity();
        }

        public void InitializeMonoSecurity()
        {
            if (!Util.IsPlatformMono)
            {
                if (AppDomain.CurrentDomain.GetData("MonoSecurityPostgresAdded") == null)
                {
                    AppDomain.CurrentDomain.SetData("MonoSecurityPostgresAdded", "true");

                    AppDomain currentDomain = AppDomain.CurrentDomain;
                    currentDomain.AssemblyResolve += new ResolveEventHandler(ResolveEventHandlerMonoSec);
                }
            }
        }

        private System.Reflection.Assembly ResolveEventHandlerMonoSec(object sender, ResolveEventArgs args)
        {
            Assembly MyAssembly = null;

            if (args.Name.Substring(0, args.Name.IndexOf(",")) == "Mono.Security")
            {
                MyAssembly = Assembly.LoadFrom("lib/NET/Mono.Security.dll");
            }

            //Return the loaded assembly.
            return MyAssembly;
        }

        /// <summary>
        /// Type conversion to a SQLDbType functions
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal NpgsqlDbType DbtypeFromType(Type type)
        {
            if (type == typeof(string))
            {
                return NpgsqlDbType.Varchar;
            }
            if (type == typeof(double))
            {
                return NpgsqlDbType.Double;
            }
            if (type == typeof(Single))
            {
                return NpgsqlDbType.Double;
            }
            if (type == typeof(int))
            {
                return NpgsqlDbType.Integer;
            }
            if (type == typeof(bool))
            {
                return NpgsqlDbType.Boolean;
            }
            if (type == typeof(UUID))
            {
                return NpgsqlDbType.Uuid;
            }
            if (type == typeof(byte))
            {
                return NpgsqlDbType.Smallint;
            }
            if (type == typeof(sbyte))
            {
                return NpgsqlDbType.Integer;
            }
            if (type == typeof(Byte[]))
            {
                return NpgsqlDbType.Bytea;
            }
            if (type == typeof(uint) || type == typeof(ushort))
            {
                return NpgsqlDbType.Integer;
            }
            if (type == typeof(ulong))
            {
                return NpgsqlDbType.Bigint;
            }
            if (type == typeof(DateTime))
            {
                return NpgsqlDbType.Timestamp;
            }

            return NpgsqlDbType.Varchar;
        }

        internal NpgsqlDbType DbtypeFromString(Type type, string PGFieldType)
        {
            if (PGFieldType == "")
            {
                return DbtypeFromType(type);
            }

            if (PGFieldType == "character varying")
            {
                return NpgsqlDbType.Varchar;
            }
            if (PGFieldType == "double precision")
            {
                return NpgsqlDbType.Double;
            }
            if (PGFieldType == "integer")
            {
                return NpgsqlDbType.Integer;
            }
            if (PGFieldType == "smallint")
            {
                return NpgsqlDbType.Smallint;
            }
            if (PGFieldType == "boolean")
            {
                return NpgsqlDbType.Boolean;
            }
            if (PGFieldType == "uuid")
            {
                return NpgsqlDbType.Uuid;
            }
            if (PGFieldType == "bytea")
            {
                return NpgsqlDbType.Bytea;
            }

            return DbtypeFromType(type);
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
                return (bool)value;
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
        ///  Create value for parameter based on PGSQL Schema
        /// </summary>
        /// <param name="value"></param>
        /// <param name="PGFieldType"></param>
        /// <returns></returns>
        internal static object CreateParameterValue(object value, string PGFieldType)
        {
            if (PGFieldType == "uuid")
            {
                UUID uidout;
                UUID.TryParse(value.ToString(), out uidout);
                return uidout;
            }
            if (PGFieldType == "integer")
            {
                int intout;
                int.TryParse(value.ToString(), out intout);
                return intout;
            }
            if (PGFieldType == "boolean")
            {
                return (value.ToString() == "true");
            }
            if (PGFieldType == "timestamp with time zone")
            {
                return (DateTime)value;
            }
            if (PGFieldType == "timestamp without time zone")
            {
                return (DateTime)value;
            }
            if (PGFieldType == "double precision")
            {
                return (Double)value;
            }
            return CreateParameterValue(value);
        }

        /// <summary>
        /// Create a parameter for a command
        /// </summary>
        /// <param name="parameterName">Name of the parameter.</param>
        /// <param name="parameterObject">parameter object.</param>
        /// <returns></returns>
        internal NpgsqlParameter CreateParameter(string parameterName, object parameterObject)
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
        internal NpgsqlParameter CreateParameter(string parameterName, object parameterObject, bool parameterOut)
        {
            //Tweak so we dont always have to add : sign
            if (parameterName.StartsWith(":")) parameterName = parameterName.Replace(":","");

            //HACK if object is null, it is turned into a string, there are no nullable type till now
            if (parameterObject == null) parameterObject = "";

            NpgsqlParameter parameter = new NpgsqlParameter(parameterName, DbtypeFromType(parameterObject.GetType()));

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
        /// Create a parameter with PGSQL schema type
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="parameterObject"></param>
        /// <param name="PGFieldType"></param>
        /// <returns></returns>
        internal NpgsqlParameter CreateParameter(string parameterName, object parameterObject, string PGFieldType)
        {
            //Tweak so we dont always have to add : sign
            if (parameterName.StartsWith(":")) parameterName = parameterName.Replace(":", "");

            //HACK if object is null, it is turned into a string, there are no nullable type till now
            if (parameterObject == null) parameterObject = "";

            NpgsqlParameter parameter = new NpgsqlParameter(parameterName, DbtypeFromString(parameterObject.GetType(), PGFieldType));

            parameter.Direction = ParameterDirection.Input;
            parameter.Value = CreateParameterValue(parameterObject, PGFieldType);

            return parameter;
        }

        /// <summary>
        /// Checks if we need to do some migrations to the database
        /// </summary>
        /// <param name="migrationStore">migrationStore.</param>
        public void CheckMigration(string migrationStore)
        {
            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString)) 
            {
                connection.Open();
                Assembly assem = GetType().Assembly;
                PGSQLMigration migration = new PGSQLMigration(connection, assem, migrationStore);

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
