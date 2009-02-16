/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Data;
using MySql.Data.MySqlClient;
using ExtensionLoader;
using ExtensionLoader.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Grid.AssetInventoryServer.Extensions;
using OpenSim.Data;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim
{
    public class OpenSimAssetStoragePlugin : IAssetStorageProvider
    {
        const string EXTENSION_NAME = "OpenSimAssetStorage"; // Used in metrics reporting

        private AssetInventoryServer server;
        private IAssetDataPlugin m_assetProvider;

        public OpenSimAssetStoragePlugin()
        {
        }

        #region IAssetStorageProvider implementation

        public BackendResponse TryFetchMetadata(UUID assetID, out Metadata metadata)
        {
            metadata = null;
            BackendResponse ret;

            using (MySqlConnection dbConnection = new MySqlConnection(DBConnString.GetConnectionString(server.ConfigFile)))
            {
                IDataReader reader;

                try
                {
                    dbConnection.Open();

                    IDbCommand command = dbConnection.CreateCommand();
                    command.CommandText = String.Format("SELECT name,description,assetType,temporary FROM assets WHERE id='{0}'", assetID.ToString());
                    reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        metadata = new Metadata();
                        metadata.CreationDate = OpenMetaverse.Utils.Epoch;
                        metadata.SHA1 = null;
                        metadata.ID = assetID;
                        metadata.Name = reader.GetString(0);
                        metadata.Description = reader.GetString(1);
                        metadata.ContentType = Utils.SLAssetTypeToContentType(reader.GetInt32(2));
                        metadata.Temporary = reader.GetBoolean(3);

                        ret = BackendResponse.Success;
                    }
                    else
                    {
                        ret = BackendResponse.NotFound;
                    }
                }
                catch (MySqlException ex)
                {
                    Logger.Log.Error("Connection to MySQL backend failed: " + ex.Message);
                    ret = BackendResponse.Failure;
                }
            }

            server.MetricsProvider.LogAssetMetadataFetch(EXTENSION_NAME, ret, assetID, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchData(UUID assetID, out byte[] assetData)
        {
            assetData = null;
            BackendResponse ret;

            using (MySqlConnection dbConnection = new MySqlConnection(DBConnString.GetConnectionString(server.ConfigFile)))
            {
                IDataReader reader;

                try
                {
                    dbConnection.Open();

                    IDbCommand command = dbConnection.CreateCommand();
                    command.CommandText = String.Format("SELECT data FROM assets WHERE id='{0}'", assetID.ToString());
                    reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        assetData = (byte[])reader.GetValue(0);
                        ret = BackendResponse.Success;
                    }
                    else
                    {
                        ret = BackendResponse.NotFound;
                    }
                }
                catch (MySqlException ex)
                {
                    Logger.Log.Error("Connection to MySQL backend failed: " + ex.Message);
                    ret = BackendResponse.Failure;
                }
            }

            server.MetricsProvider.LogAssetDataFetch(EXTENSION_NAME, ret, assetID, (assetData != null ? assetData.Length : 0), DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchDataMetadata(UUID assetID, out Metadata metadata, out byte[] assetData)
        {
            metadata = null;
            assetData = null;
            //BackendResponse ret;

            AssetBase asset = m_assetProvider.FetchAsset(assetID);

            if (asset != null)
            {
                metadata = new Metadata();
                metadata.ID = asset.Metadata.FullID;
                metadata.CreationDate = OpenMetaverse.Utils.Epoch;
                metadata.SHA1 = null;
                metadata.Name = asset.Metadata.Name;
                metadata.Description = asset.Metadata.Description;
                metadata.ContentType = Utils.SLAssetTypeToContentType(asset.Metadata.Type);
                metadata.Temporary = asset.Metadata.Temporary;

                assetData = asset.Data;
            }
            else return BackendResponse.NotFound;

            return BackendResponse.Success;
        }

        public BackendResponse TryCreateAsset(Metadata metadata, byte[] assetData, out UUID assetID)
        {
            assetID = metadata.ID = UUID.Random();
            return TryCreateAsset(metadata, assetData);
        }

        public BackendResponse TryCreateAsset(Metadata metadata, byte[] assetData)
        {
            BackendResponse ret;

            using (MySqlConnection dbConnection = new MySqlConnection(DBConnString.GetConnectionString(server.ConfigFile)))
            {
                try
                {
                    dbConnection.Open();

                    MySqlCommand command = new MySqlCommand(
                        "REPLACE INTO assets (name,description,assetType,local,temporary,data,id) VALUES " +
                        "(?name,?description,?assetType,?local,?temporary,?data,?id)", dbConnection);

                    command.Parameters.AddWithValue("?name", metadata.Name);
                    command.Parameters.AddWithValue("?description", metadata.Description);
                    command.Parameters.AddWithValue("?assetType", Utils.ContentTypeToSLAssetType(metadata.ContentType));
                    command.Parameters.AddWithValue("?local", 0);
                    command.Parameters.AddWithValue("?temporary", metadata.Temporary);
                    command.Parameters.AddWithValue("?data", assetData);
                    command.Parameters.AddWithValue("?id", metadata.ID.ToString());

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected == 1)
                    {
                        ret = BackendResponse.Success;
                    }
                    else if (rowsAffected == 2)
                    {
                        Logger.Log.Info("Replaced asset " + metadata.ID.ToString());
                        ret = BackendResponse.Success;
                    }
                    else
                    {
                        Logger.Log.ErrorFormat("MySQL REPLACE query affected {0} rows", rowsAffected);
                        ret = BackendResponse.Failure;
                    }
                }
                catch (MySqlException ex)
                {
                    Logger.Log.Error("Connection to MySQL backend failed: " + ex.Message);
                    ret = BackendResponse.Failure;
                }
            }

            server.MetricsProvider.LogAssetCreate(EXTENSION_NAME, ret, metadata.ID, assetData.Length, DateTime.Now);
            return ret;
        }

        public int ForEach(Action<Metadata> action, int start, int count)
        {
            int rowCount = 0;

            using (MySqlConnection dbConnection = new MySqlConnection(DBConnString.GetConnectionString(server.ConfigFile)))
            {
                MySqlDataReader reader;

                try
                {
                    dbConnection.Open();

                    MySqlCommand command = dbConnection.CreateCommand();
                    command.CommandText = String.Format("SELECT name,description,assetType,temporary,data,id FROM assets LIMIT {0}, {1}",
                        start, count);
                    reader = command.ExecuteReader();
                }
                catch (MySqlException ex)
                {
                    Logger.Log.Error("Connection to MySQL backend failed: " + ex.Message);
                    return 0;
                }

                while (reader.Read())
                {
                    Metadata metadata = new Metadata();
                    metadata.CreationDate = OpenMetaverse.Utils.Epoch;
                    metadata.Description = reader.GetString(1);
                    metadata.ID = UUID.Parse(reader.GetString(5));
                    metadata.Name = reader.GetString(0);
                    metadata.SHA1 = OpenMetaverse.Utils.SHA1((byte[])reader.GetValue(4));
                    metadata.Temporary = reader.GetBoolean(3);
                    metadata.ContentType = Utils.SLAssetTypeToContentType(reader.GetInt32(2));

                    action(metadata);
                    ++rowCount;
                }

                reader.Close();
            }

            return rowCount;
        }

        #endregion IAssetStorageProvider implementation

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            try
            {
                m_assetProvider = DataPluginFactory.LoadAssetDataPlugin("OpenSim.Data.MySQL.dll", server.ConfigFile.Configs["MySQL"].GetString("database_connect", null));
                if (m_assetProvider == null)
                {
                    Logger.Log.Error("[ASSET]: Failed to load a database plugin, server halting.");
                    Environment.Exit(-1);
                }
                else
                    Logger.Log.InfoFormat("[ASSET]: Loaded storage backend: {0}", Version);
            }
            catch (Exception e)
            {
                Logger.Log.WarnFormat("[ASSET]: Failure loading data plugin: {0}", e.ToString());
            }
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            Logger.Log.InfoFormat("[ASSET]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
        }

        public string Version
        {
            get { return m_assetProvider.Version; }
        }

        public string Name
        {
            get { return "AssetInventoryServer OpenSim asset storage provider"; }
        }

        #endregion IPlugin implementation
    }
}
