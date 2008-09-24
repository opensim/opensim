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
using OpenMetaverse;
using log4net;
using MySql.Data.MySqlClient;
using OpenSim.Framework;


namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Asset Server
    /// </summary>
    public class MySQLAssetData : AssetDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MySQLManager _dbConnection;

        #region IPlugin Members

        /// <summary>
        /// <para>Initialises Asset interface</para>
        /// <para>
        /// <list type="bullet">
        /// <item>Loads and initialises the MySQL storage plugin.</item>
        /// <item>Warns and uses the obsolete mysql_connection.ini if connect string is empty.</item>
        /// <item>Check for migration</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string</param>
        override public void Initialise(string connect)
        {
            // TODO: This will let you pass in the connect string in
            // the config, though someone will need to write that.
            if (connect == String.Empty)
            {
                // This is old seperate config file
                m_log.Warn("no connect string, using old mysql_connection.ini instead");
                Initialise();
            }
            else
            {
                _dbConnection = new MySQLManager(connect);
            }

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration(_dbConnection.Connection, assem, "AssetStore");

            m.Update();
        }

        /// <summary>
        /// <para>Initialises Asset interface</para>
        /// <para>
        /// <list type="bullet">
        /// <item>Loads and initialises the MySQL storage plugin</item>
        /// <item>uses the obsolete mysql_connection.ini</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string</param>
        /// <remarks>Probably DEPRECATED and shouldn't be used</remarks>
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

        }

        override public void Dispose() { }

        #region IAssetProviderPlugin Members

        /// <summary>
        /// Fetch Asset <paramref name="assetID"/> from database
        /// </summary>
        /// <param name="assetID">Asset UUID to fetch</param>
        /// <returns>Return the asset</returns>
        /// <remarks>On failure : throw an exception and attempt to reconnect to database</remarks>
        override public AssetBase FetchAsset(UUID assetID)
        {
            AssetBase asset = null;
            lock (_dbConnection)
            {
                _dbConnection.CheckConnection();

                MySqlCommand cmd =
                    new MySqlCommand(
                        "SELECT name, description, assetType, local, temporary, data FROM assets WHERE id=?id",
                        _dbConnection.Connection);
                cmd.Parameters.AddWithValue("?id", assetID.ToString());

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
                        "[ASSETS DB]: MySql failure fetching asset {0}" + Environment.NewLine + e.ToString()
                        + Environment.NewLine + "Reconnecting", assetID);
                    _dbConnection.Reconnect();
                }
            }
            return asset;
        }

        /// <summary>
        /// Create an asset in database, or update it if existing.
        /// </summary>
        /// <param name="asset">Asset UUID to create</param>
        /// <remarks>On failure : Throw an exception and attempt to reconnect to database</remarks>
        override public void CreateAsset(AssetBase asset)
        {
            lock (_dbConnection)
            {
                //m_log.Info("[ASSET DB]: Creating Asset " + Util.ToRawUuidString(asset.FullID));
                if (ExistsAsset(asset.FullID))
                {
                    //m_log.Info("[ASSET DB]: Asset exists already, ignoring.");
                    return;
                }

                _dbConnection.CheckConnection();

                MySqlCommand cmd =
                    new MySqlCommand(
                        "REPLACE INTO assets(id, name, description, assetType, local, temporary, data)" +
                        "VALUES(?id, ?name, ?description, ?assetType, ?local, ?temporary, ?data)",
                        _dbConnection.Connection);

                // need to ensure we dispose
                try
                {
                    using (cmd)
                    {
                        cmd.Parameters.AddWithValue("?id", asset.FullID.ToString());
                        cmd.Parameters.AddWithValue("?name", asset.Name);
                        cmd.Parameters.AddWithValue("?description", asset.Description);
                        cmd.Parameters.AddWithValue("?assetType", asset.Type);
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
                        "[ASSETS DB]: " +
                        "MySql failure creating asset {0} with name {1}" + Environment.NewLine + e.ToString()
                        + Environment.NewLine + "Attempting reconnection", asset.FullID, asset.Name);
                    _dbConnection.Reconnect();
                }
            }
        }

        /// <summary>
        /// Update a asset in database, see <see cref="CreateAsset"/>
        /// </summary>
        /// <param name="asset">Asset UUID to update</param>
        override public void UpdateAsset(AssetBase asset)
        {
            CreateAsset(asset);
        }

        /// <summary>
        /// check if the asset UUID exist in database
        /// </summary>
        /// <param name="uuid">The asset UUID</param>
        /// <returns>true if exist.</returns>
        override public bool ExistsAsset(UUID uuid)
        {
            bool assetExists = false;

            lock (_dbConnection)
            {
                _dbConnection.CheckConnection();

                MySqlCommand cmd =
                    new MySqlCommand(
                        "SELECT id FROM assets WHERE id=?id",
                        _dbConnection.Connection);

                cmd.Parameters.AddWithValue("?id", uuid.ToString());

                try
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (dbReader.Read())
                        {
                            assetExists = true;
                        }

                        dbReader.Close();
                        cmd.Dispose();
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[ASSETS DB]: MySql failure fetching asset {0}" + Environment.NewLine + e.ToString()
                        + Environment.NewLine + "Attempting reconnection", uuid);
                    _dbConnection.Reconnect();
                }
            }

            return assetExists;
        }

        #endregion

        /// <summary>
        /// Database provider version
        /// </summary>
        override public string Version
        {
            get { return _dbConnection.getVersion(); }
        }

        /// <summary>
        /// The name of this DB provider
        /// </summary>
        override public string Name
        {
            get { return "MySQL Asset storage engine"; }
        }

        #endregion
    }
}
