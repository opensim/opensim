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
using libsecondlife;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A MSSQL Interface for the Asset server
    /// </summary>
    internal class MSSQLAssetData : AssetDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MSSQLManager database;

        #region IAssetProvider Members

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
        }

        /// <summary>
        /// Fetch Asset from database
        /// </summary>
        /// <param name="assetID">the asset UUID</param>
        /// <returns></returns>
        override public AssetBase FetchAsset(LLUUID assetID)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["id"] = assetID.ToString();

            using (IDbCommand result = database.Query("SELECT * FROM assets WHERE id = @id", param))
            using (IDataReader reader = result.ExecuteReader())
            {
                return database.getAssetRow(reader);
            }
        }

        /// <summary>
        /// Create asset in database
        /// </summary>
        /// <param name="asset">the asset</param>
        override public void CreateAsset(AssetBase asset)
        {
            if (ExistsAsset((LLUUID) asset.FullID))
            {
                return;
            }


            using (AutoClosingSqlCommand cmd =
                database.Query(
                    "INSERT INTO assets ([id], [name], [description], [assetType], [local], [temporary], [data])" +
                    " VALUES " +
                    "(@id, @name, @description, @assetType, @local, @temporary, @data)"))
            {

                //SqlParameter p = cmd.Parameters.Add("id", SqlDbType.NVarChar);
                //p.Value = asset.FullID.ToString();
                cmd.Parameters.AddWithValue("id", asset.FullID.ToString());
                cmd.Parameters.AddWithValue("name", asset.Name);
                cmd.Parameters.AddWithValue("description", asset.Description);
                SqlParameter e = cmd.Parameters.Add("assetType", SqlDbType.TinyInt);
                e.Value = asset.Type;
                SqlParameter g = cmd.Parameters.Add("local", SqlDbType.TinyInt);
                g.Value = asset.Local;
                SqlParameter h = cmd.Parameters.Add("temporary", SqlDbType.TinyInt);
                h.Value = asset.Temporary;
                SqlParameter i = cmd.Parameters.Add("data", SqlDbType.Image);
                i.Value = asset.Data;

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Update asset in database
        /// </summary>
        /// <param name="asset">the asset</param>
        override public void UpdateAsset(AssetBase asset)
        {
            using (IDbCommand command = database.Query("UPDATE assets set id = @id, " +
                                                "name = @name, " +
                                                "description = @description," +
                                                "assetType = @assetType," +
                                                "local = @local," +
                                                "temporary = @temporary," +
                                                "data = @data where " +
                                                "id = @keyId;"))
            {
                SqlParameter param1 = new SqlParameter("@id", asset.FullID.ToString());
                SqlParameter param2 = new SqlParameter("@name", asset.Name);
                SqlParameter param3 = new SqlParameter("@description", asset.Description);
                SqlParameter param4 = new SqlParameter("@assetType", asset.Type);
                SqlParameter param6 = new SqlParameter("@local", asset.Local);
                SqlParameter param7 = new SqlParameter("@temporary", asset.Temporary);
                SqlParameter param8 = new SqlParameter("@data", asset.Data);
                SqlParameter param9 = new SqlParameter("@keyId", asset.FullID.ToString());
                command.Parameters.Add(param1);
                command.Parameters.Add(param2);
                command.Parameters.Add(param3);
                command.Parameters.Add(param4);
                command.Parameters.Add(param6);
                command.Parameters.Add(param7);
                command.Parameters.Add(param8);
                command.Parameters.Add(param9);

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
        override public bool ExistsAsset(LLUUID uuid)
        {
            if (FetchAsset(uuid) != null)
            {
                return true;
            }
            return false;
        }

        #endregion

        #region IPlugin Members

        override public void Dispose() { }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// <para>
        /// TODO: this would allow you to pass in connnect info as
        /// a string instead of file, if someone writes the support
        /// </para>
        /// </summary>
        /// <param name="connect">connect string</param>
        override public void Initialise(string connect)
        {
            Initialise();
        }

        /// <summary>
        /// Initialises asset interface
        /// </summary>
        /// <remarks>it use mssql_connection.ini</remarks>
        override public void Initialise()
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

            TestTables();
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
    }
}
