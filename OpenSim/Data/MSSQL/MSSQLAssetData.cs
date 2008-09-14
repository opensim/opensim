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
using System.Data.SqlClient;
using System.Reflection;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A MSSQL Interface for the Asset server
    /// </summary>
    internal class MSSQLAssetData : AssetDataBase
    {
        private const string _migrationStore = "AssetStore";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager
        /// </summary>
        private MSSQLManager database;

        #region IPlugin Members

        override public void Dispose() { }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        override public void Initialise()
        {
            m_log.Info("[MSSQLUserData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        /// <summary>
        /// Initialises asset interface
        /// </summary>
        /// <para>
        /// a string instead of file, if someone writes the support
        /// </para>
        /// <param name="connectionString">connect string</param>
        override public void Initialise(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                database = new MSSQLManager(connectionString);
            }
            else
            {

                IniFile gridDataMSSqlFile = new IniFile("mssql_connection.ini");
                string settingDataSource = gridDataMSSqlFile.ParseFileReadValue("data_source");
                string settingInitialCatalog = gridDataMSSqlFile.ParseFileReadValue("initial_catalog");
                string settingPersistSecurityInfo = gridDataMSSqlFile.ParseFileReadValue("persist_security_info");
                string settingUserId = gridDataMSSqlFile.ParseFileReadValue("user_id");
                string settingPassword = gridDataMSSqlFile.ParseFileReadValue("password");

                database =
                    new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                     settingPassword);
            }

            //TODO can be removed at some time!!
            TestTables();

            //New migration to check for DB changes
            database.CheckMigration(_migrationStore);
        }

        /// <summary>
        /// Database provider version.
        /// </summary>
        override public string Version
        {
            get { return database.getVersion(); }
        }

        /// <summary>
        /// The name of this DB provider.
        /// </summary>
        override public string Name
        {
            get { return "MSSQL Asset storage engine"; }
        }

        #endregion

        #region IAssetProviderPlugin Members

        /// <summary>
        /// Fetch Asset from database
        /// </summary>
        /// <param name="assetID">the asset UUID</param>
        /// <returns></returns>
        override public AssetBase FetchAsset(UUID assetID)
        {
            using (AutoClosingSqlCommand command = database.Query("SELECT * FROM assets WHERE id = @id"))
            {
                command.Parameters.Add(database.CreateParameter("id", assetID));
                using (IDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        AssetBase asset = new AssetBase();
                        // Region Main
                        asset.FullID = new UUID((string)reader["id"]);
                        asset.Name = (string)reader["name"];
                        asset.Description = (string)reader["description"];
                        asset.Type = Convert.ToSByte(reader["assetType"]);
                        asset.Local = Convert.ToBoolean(reader["local"]);
                        asset.Temporary = Convert.ToBoolean(reader["temporary"]);
                        asset.Data = (byte[])reader["data"];
                        return asset;
                    }
                    return null; // throw new Exception("No rows to return");
                }
            }
        }

        /// <summary>
        /// Create asset in database
        /// </summary>
        /// <param name="asset">the asset</param>
        override public void CreateAsset(AssetBase asset)
        {
            if (ExistsAsset(asset.FullID))
            {
                return;
            }

            using (AutoClosingSqlCommand command = database.Query(
                    "INSERT INTO assets ([id], [name], [description], [assetType], [local], [temporary], [data])" +
                    " VALUES " +
                    "(@id, @name, @description, @assetType, @local, @temporary, @data)"))
            {
                //SqlParameter p = cmd.Parameters.Add("id", SqlDbType.NVarChar);
                //p.Value = asset.FullID.ToString();
                command.Parameters.Add(database.CreateParameter("id", asset.FullID));
                command.Parameters.Add(database.CreateParameter("name", asset.Name));
                command.Parameters.Add(database.CreateParameter("description", asset.Description));
                command.Parameters.Add(database.CreateParameter("assetType", asset.Type));
                command.Parameters.Add(database.CreateParameter("local", asset.Local));
                command.Parameters.Add(database.CreateParameter("temporary", asset.Temporary));
                command.Parameters.Add(database.CreateParameter("data", asset.Data));

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Update asset in database
        /// </summary>
        /// <param name="asset">the asset</param>
        override public void UpdateAsset(AssetBase asset)
        {
            using (AutoClosingSqlCommand command = database.Query("UPDATE assets set id = @id, " +
                                                "name = @name, " +
                                                "description = @description," +
                                                "assetType = @assetType," +
                                                "local = @local," +
                                                "temporary = @temporary," +
                                                "data = @data where " +
                                                "id = @keyId;"))
            {
                command.Parameters.Add(database.CreateParameter("id", asset.FullID));
                command.Parameters.Add(database.CreateParameter("name", asset.Name));
                command.Parameters.Add(database.CreateParameter("description", asset.Description));
                command.Parameters.Add(database.CreateParameter("assetType", asset.Type));
                command.Parameters.Add(database.CreateParameter("local", asset.Local));
                command.Parameters.Add(database.CreateParameter("temporary", asset.Temporary));
                command.Parameters.Add(database.CreateParameter("data", asset.Data));
                command.Parameters.Add(database.CreateParameter("@keyId", asset.FullID));

                try
                {
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                }
            }
        }

        /// <summary>
        /// Check if asset exist in database
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>true if exist.</returns>
        override public bool ExistsAsset(UUID uuid)
        {
            if (FetchAsset(uuid) != null)
            {
                return true;
            }
            return false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Migration method
        /// <list type="bullet">
        /// <item>Execute "CreateAssetsTable.sql" if tableName == null</item>
        /// </list>
        /// </summary>
        /// <param name="tableName">Name of table</param>
        private void UpgradeAssetsTable(string tableName)
        {
            // null as the version, indicates that the table didn't exist
            if (tableName == null)
            {
                m_log.Info("[ASSET DB]: Creating new database tables");
                database.ExecuteResourceSql("CreateAssetsTable.sql");
                return;
            }
        }

        /// <summary>
        /// Ensure that the assets related tables exists and are at the latest version
        /// </summary>
        private void TestTables()
        {
            Dictionary<string, string> tableList = new Dictionary<string, string>();

            tableList["assets"] = null;
            database.GetTableVersion(tableList);

            UpgradeAssetsTable(tableList["assets"]);

            //Special for Migrations
            using (AutoClosingSqlCommand cmd = database.Query("select * from migrations where name = '" + _migrationStore + "'"))
            {
                try
                {
                    bool insert = true;
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) insert = false;
                    }
                    if (insert)
                    {
                        cmd.CommandText = "insert into migrations(name, version) values('" + _migrationStore + "', 1)";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                    //No migrations table
                    //HACK create one and add data
                    cmd.CommandText = "create table migrations(name varchar(100), version int)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "insert into migrations(name, version) values('migrations', 1)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "insert into migrations(name, version) values('" + _migrationStore + "', 1)";
                    cmd.ExecuteNonQuery();
                }
            }

        }

        #endregion
    }
}
