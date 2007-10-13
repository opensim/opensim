using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Data.MySQL
{
    class MySQLAssetData : IAssetProvider   
    {
        MySQLManager _dbConnection;
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

            MySqlCommand cmd = new MySqlCommand("SELECT name, description, assetType, invType, local, temporary, data FROM assets WHERE id=?id", _dbConnection.Connection);
            MySqlParameter p = cmd.Parameters.Add("?id", MySqlDbType.Binary, 16);
            p.Value = assetID.GetBytes();
            using (MySqlDataReader dbReader = cmd.ExecuteReader(System.Data.CommandBehavior.SingleRow))
            {
                if (dbReader.Read())
                {
                    asset = new AssetBase();
                    asset.Data = (byte[])dbReader["data"];
                    asset.Description = (string)dbReader["description"];
                    asset.FullID = assetID;
                    asset.InvType = (sbyte)dbReader["invType"];
                    asset.Local = ((sbyte)dbReader["local"])!=0?true:false;
                    asset.Name = (string)dbReader["name"];
                    asset.Type = (sbyte)dbReader["assetType"];
                }
            }
            return asset;
        }

        public void CreateAsset(AssetBase asset)
        {
            MySqlCommand cmd = new MySqlCommand("REPLACE INTO assets(id, name, description, assetType, invType, local, temporary, data)" +
                                                             "VALUES(?id, ?name, ?description, ?assetType, ?invType, ?local, ?temporary, ?data)", _dbConnection.Connection);
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
