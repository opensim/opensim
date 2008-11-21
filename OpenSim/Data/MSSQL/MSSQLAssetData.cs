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
using System.Data;
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
        private long TicksToEpoch; 
        /// <summary>
        /// Database manager
        /// </summary>
        private MSSQLManager database;

        #region IPlugin Members

        override public void Dispose() { }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        // [Obsolete("Cannot be default-initialized!")]
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
            TicksToEpoch = new System.DateTime(1970, 1, 1).Ticks;

            if (!string.IsNullOrEmpty(connectionString))
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
                    "INSERT INTO assets ([id], [name], [description], [assetType], [local], [temporary], [create_time], [access_time], [data])" +
                    " VALUES " +
                    "(@id, @name, @description, @assetType, @local, @temporary, @create_time, @access_time, @data)"))
            {
                int now = (int)((System.DateTime.Now.Ticks - TicksToEpoch) / 10000000);
                command.Parameters.Add(database.CreateParameter("id", asset.FullID));
                command.Parameters.Add(database.CreateParameter("name", asset.Name));
                command.Parameters.Add(database.CreateParameter("description", asset.Description));
                command.Parameters.Add(database.CreateParameter("assetType", asset.Type));
                command.Parameters.Add(database.CreateParameter("local", asset.Local));
                command.Parameters.Add(database.CreateParameter("temporary", asset.Temporary));
                command.Parameters.Add(database.CreateParameter("access_time", now));
                command.Parameters.Add(database.CreateParameter("create_time", now));
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

// Commented out since currently unused - this probably should be called in FetchAsset()       
//        private void UpdateAccessTime(AssetBase asset)
//        {
//            using (AutoClosingSqlCommand cmd = database.Query("UPDATE assets SET access_time = @access_time WHERE id=@id"))
//            {
//                int now = (int)((System.DateTime.Now.Ticks - TicksToEpoch) / 10000000);
//                cmd.Parameters.AddWithValue("@id", asset.FullID.ToString());
//                cmd.Parameters.AddWithValue("@access_time", now);
//                try
//                {
//                    cmd.ExecuteNonQuery();
//                }
//                catch (Exception e)
//                {
//                    m_log.Error(e.ToString());
//                }
//            }
//        }

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
    }
}
