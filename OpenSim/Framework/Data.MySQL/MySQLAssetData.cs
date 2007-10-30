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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using libsecondlife;
using MySql.Data.MySqlClient;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.MySQL
{
    internal class MySQLAssetData : IAssetProvider
    {
        private MySQLManager _dbConnection;

        #region IAssetProvider Members

        private void UpgradeAssetsTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                MainLog.Instance.Notice("ASSETS", "Creating new database tables");
                _dbConnection.ExecuteResourceSql("CreateAssetsTable.sql");
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
            _dbConnection.GetTableVersion(tableList);

            UpgradeAssetsTable(tableList["assets"]);
        }

        public AssetBase FetchAsset(LLUUID assetID)
        {
            AssetBase asset = null;
            lock (_dbConnection)
            {
                MySqlCommand cmd =
                    new MySqlCommand(
                        "SELECT name, description, assetType, invType, local, temporary, data FROM assets WHERE id=?id",
                        _dbConnection.Connection);
                MySqlParameter p = cmd.Parameters.Add("?id", MySqlDbType.Binary, 16);
                p.Value = assetID.GetBytes();
                using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (dbReader.Read())
                    {
                        asset = new AssetBase();
                        asset.Data = (byte[]) dbReader["data"];
                        asset.Description = (string) dbReader["description"];
                        asset.FullID = assetID;
                        asset.InvType = (sbyte) dbReader["invType"];
                        asset.Local = ((sbyte) dbReader["local"]) != 0 ? true : false;
                        asset.Name = (string) dbReader["name"];
                        asset.Type = (sbyte) dbReader["assetType"];
                    }
                }
            }
            return asset;
        }

        public void CreateAsset(AssetBase asset)
        {
            MySqlCommand cmd =
                new MySqlCommand(
                    "REPLACE INTO assets(id, name, description, assetType, invType, local, temporary, data)" +
                    "VALUES(?id, ?name, ?description, ?assetType, ?invType, ?local, ?temporary, ?data)",
                    _dbConnection.Connection);
            MySqlParameter p = cmd.Parameters.Add("?id", MySqlDbType.Binary, 16);
            p.Value = asset.FullID.GetBytes();
            cmd.Parameters.AddWithValue("?name", asset.Name);
            cmd.Parameters.AddWithValue("?description", asset.Description);
            cmd.Parameters.AddWithValue("?assetType", asset.Type);
            cmd.Parameters.AddWithValue("?invType", asset.InvType);
            cmd.Parameters.AddWithValue("?local", asset.Local);
            cmd.Parameters.AddWithValue("?temporary", asset.Temporary);
            cmd.Parameters.AddWithValue("?data", asset.Data);
            cmd.ExecuteNonQuery();
        }

        public void UpdateAsset(AssetBase asset)
        {
            CreateAsset(asset);
        }

        public bool ExistsAsset(LLUUID uuid)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// All writes are immediately commited to the database, so this is a no-op
        /// </summary>
        public void CommitAssets()
        {
        }

        #endregion

        #region IPlugin Members

        public void Initialise()
        {
            IniFile GridDataMySqlFile = new IniFile("mysql_connection.ini");
            string hostname = GridDataMySqlFile.ParseFileReadValue("hostname");
            string database = GridDataMySqlFile.ParseFileReadValue("database");
            string username = GridDataMySqlFile.ParseFileReadValue("username");
            string password = GridDataMySqlFile.ParseFileReadValue("password");
            string pooling = GridDataMySqlFile.ParseFileReadValue("pooling");
            string port = GridDataMySqlFile.ParseFileReadValue("port");

            _dbConnection = new MySQLManager(hostname, database, username, password, pooling, port);

            TestTables();
        }

        public string Version
        {
            get { return _dbConnection.getVersion(); }
        }

        public string Name
        {
            get { return "MySQL Asset storage engine"; }
        }

        #endregion
    }
}