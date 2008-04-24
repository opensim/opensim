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
using System.Reflection;
using libsecondlife;
using log4net;
using MySql.Data.MySqlClient;
using OpenSim.Framework;

namespace OpenSim.Data.MySQL
{
    internal class MySQLAssetData : AssetDataBase, IPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MySQLManager _dbConnection;

        #region IAssetProvider Members

        private void UpgradeAssetsTable(string oldVersion)
        {
            // null as the version, indicates that the table didn't exist
            if (oldVersion == null)
            {
                m_log.Info("[ASSETS]: Creating new database tables");
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

        override public AssetBase FetchAsset(LLUUID assetID)
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
                
                try
                {
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
                        dbReader.Close();
                        cmd.Dispose();
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[ASSETS]: MySql failure fetching asset {0}" + Environment.NewLine + e.ToString()
                        + Environment.NewLine + "Attempting reconnection", assetID);
                    _dbConnection.Reconnect();
                }
            }
            return asset;
        }

        override public void CreateAsset(AssetBase asset)
        {            
            lock (_dbConnection)
            {
                MySqlCommand cmd =
                    new MySqlCommand(
                        "REPLACE INTO assets(id, name, description, assetType, invType, local, temporary, data)" +
                        "VALUES(?id, ?name, ?description, ?assetType, ?invType, ?local, ?temporary, ?data)",
                        _dbConnection.Connection);
            
                // need to ensure we dispose
                try
                {            
                    using (cmd)
                    {
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
                        cmd.Dispose();
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[ASSETS]: " +
                        "MySql failure creating asset {0} with name {1}" + Environment.NewLine + e.ToString()
                        + Environment.NewLine + "Attempting reconnection", asset.FullID, asset.Name);
                    _dbConnection.Reconnect();
                }   
            }
        }

        override public void UpdateAsset(AssetBase asset)
        {
            CreateAsset(asset);
        }

        override public bool ExistsAsset(LLUUID uuid)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        /// <summary>
        /// All writes are immediately commited to the database, so this is a no-op
        /// </summary>
        override public void CommitAssets()
        {
        }

        #endregion

        #region IPlugin Members

        override public void Initialise(string connect)
        {
            // TODO: This will let you pass in the connect string in
            // the config, though someone will need to write that.
            if (connect == String.Empty) {
                // This is old seperate config file
                Initialise();
            } else {
                _dbConnection = new MySQLManager(connect);
                TestTables();
            }
        }

        override public void Initialise()
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

        override public string Version
        {
            get { return _dbConnection.getVersion(); }
        }

        override public string Name
        {
            get { return "MySQL Asset storage engine"; }
        }

        #endregion
    }
}
