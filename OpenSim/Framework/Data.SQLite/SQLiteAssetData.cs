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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.IO;
using libsecondlife;
using OpenSim.Framework.Utilities;
using System.Data;
using System.Data.SqlTypes;
using Mono.Data.SqliteClient;
using OpenSim.Framework.Console;
using OpenSim.Framework.Types;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Framework.Data.SQLite
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class SQLiteAssetData : SQLiteBase, IAssetProvider
    {
        /// <summary>
        /// The database manager
        /// </summary>

        /// <summary>
        /// Artificial constructor called upon plugin load
        /// </summary>
        private const string assetSelect = "select * from assets";
        private DataSet ds;
        private SqliteDataAdapter da;
        
        public void Initialise(string dbfile, string dbname)
        {
            SqliteConnection conn = new SqliteConnection("URI=file:" + dbfile + ",version=3");
            TestTables(conn);
            
            ds = new DataSet();
            da = new SqliteDataAdapter(new SqliteCommand(assetSelect, conn));
 
            ds.Tables.Add(createAssetsTable());
            
            setupAssetCommands(da, conn);
            try
            {
                da.Fill(ds.Tables["assets"]);
            }
            catch (Exception)
            {
                MainLog.Instance.Verbose("AssetStorage", "Caught fill error on asset table");
            }

            
            return;
        }

        public AssetBase FetchAsset(LLUUID uuid) 
        {
            AssetBase asset = new AssetBase();
            DataRow row = ds.Tables["assets"].Rows.Find(uuid);
            if (row != null)
            {
                return buildAsset(row);
            }
            else
            {
                return null;
            }
        }
        
        public void CreateAsset(AssetBase asset) 
        {
            // no difference for now
            UpdateAsset(asset);
        }
        
        public void UpdateAsset(AssetBase asset) 
        {
            MainLog.Instance.Verbose("AssetStorage", 
                                     "Asset: " + asset.FullID + 
                                     ", Name: " + asset.Name +
                                     ", Description: " + asset.Description +
                                     ", Type: " + asset.Type +
                                     ", InvType: " + asset.InvType +
                                     ", Temporary: " + asset.Temporary +
                                     ", Local: " + asset.Local + 
                                     ", Data Length: " + asset.Data.Length );
            DataTable assets = ds.Tables["assets"];
            DataRow row = assets.Rows.Find(asset.FullID);
            if (row == null) 
            {
                row = assets.NewRow();
                fillAssetRow(row, asset);
                assets.Rows.Add(row);
            }
            else 
            {
                fillAssetRow(row, asset);
            }
            if (ds.HasChanges()) {
                DataSet changed = ds.GetChanges();
                da.Update(changed, "assets");
                ds.AcceptChanges();
            }
        }

        public bool ExistsAsset(LLUUID uuid)
        {
            DataRow row = ds.Tables["assets"].Rows.Find(uuid);
            return (row != null);
        }
        
        public void CommitAssets() // force a sync to the database
        {
            MainLog.Instance.Verbose("AssetStorage", "Attempting commit");
            if (ds.HasChanges()) {
                DataSet changed = ds.GetChanges();
                da.Update(changed, "assets");
                ds.AcceptChanges();
            }
        }
        
        /***********************************************************************
         *
         *  Database Definition Functions
         * 
         *  This should be db agnostic as we define them in ADO.NET terms
         *
         **********************************************************************/
        
        private DataTable createAssetsTable()
        {
            DataTable assets = new DataTable("assets");

            createCol(assets, "UUID", typeof(System.String));
            createCol(assets, "Name", typeof(System.String));
            createCol(assets, "Description", typeof(System.String));
            createCol(assets, "Type", typeof(System.Int32));
            createCol(assets, "InvType", typeof(System.Int32));
            createCol(assets, "Local", typeof(System.Boolean));
            createCol(assets, "Temporary", typeof(System.Boolean));
            createCol(assets, "Data", typeof(System.Byte[]));
            // Add in contraints
            assets.PrimaryKey = new DataColumn[] { assets.Columns["UUID"] };
            return assets;
        }
        
        /***********************************************************************
         *  
         *  Convert between ADO.NET <=> OpenSim Objects
         *
         *  These should be database independant
         *
         **********************************************************************/

        private AssetBase buildAsset(DataRow row)
        {
            // TODO: this doesn't work yet because something more
            // interesting has to be done to actually get these values
            // back out.  Not enough time to figure it out yet.
            AssetBase asset = new AssetBase();
            
            asset.FullID = new LLUUID((String)row["UUID"]);
            asset.Name = (String)row["Name"];
            asset.Description = (String)row["Description"];
            asset.Type = Convert.ToSByte(row["Type"]);
            asset.InvType = Convert.ToSByte(row["InvType"]);
            asset.Local = Convert.ToBoolean(row["Local"]);
            asset.Temporary = Convert.ToBoolean(row["Temporary"]);
            asset.Data = (byte[])row["Data"];
            return asset;
        }

        
        private void fillAssetRow(DataRow row, AssetBase asset)
        {
            row["UUID"] = asset.FullID;
            row["Name"] = asset.Name;
            if (asset.Description != null)
            {
                row["Description"] = asset.Description;
            }
            else
            {
                row["Description"] = " ";
            }
            row["Type"] = asset.Type;
            row["InvType"] = asset.InvType;
            row["Local"] = asset.Local;
            row["Temporary"] = asset.Temporary;
            row["Data"] = asset.Data;

            // ADO.NET doesn't handle NULL very well
            foreach (DataColumn col in ds.Tables["assets"].Columns) {
                if (row[col] == null) {
                    row[col] = "";
                }
            }
        }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        private void setupAssetCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("assets", ds.Tables["assets"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("assets", "UUID=:UUID", ds.Tables["assets"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from assets where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof(System.String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }
        
        private void InitDB(SqliteConnection conn)
        {
            string createAssets = defineTable(createAssetsTable());
            SqliteCommand pcmd = new SqliteCommand(createAssets, conn);
            conn.Open();
            pcmd.ExecuteNonQuery();
            conn.Close(); 
        }

        private bool TestTables(SqliteConnection conn)
        {
            SqliteCommand cmd = new SqliteCommand(assetSelect, conn);
            SqliteDataAdapter pDa = new SqliteDataAdapter(cmd);
            DataSet tmpDS = new DataSet();
            try {
                pDa.Fill(tmpDS, "assets");
            } catch (Mono.Data.SqliteClient.SqliteSyntaxException) {
                MainLog.Instance.Verbose("DATASTORE", "SQLite Database doesn't exist... creating");
                InitDB(conn);
            }
            return true;
        }

    }
}
