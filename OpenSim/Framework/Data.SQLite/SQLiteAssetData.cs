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
* 
*/
using System;
using System.Data;
using System.Reflection;
using libsecondlife;
using Mono.Data.SqliteClient;
using OpenSim.Framework.Console;

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

            lock (ds)
            {
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
            LogAssetLoad(asset);

            DataTable assets = ds.Tables["assets"];
            lock (ds)
            {
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
            }
        }

        private void LogAssetLoad(AssetBase asset)
        {
            string temporary = asset.Temporary ? "Temporary" : "Stored";
            string local = asset.Local ? "Local" : "Remote";

            MainLog.Instance.Verbose("ASSETSTORAGE",
                                     string.Format("Loaded {6} {5} Asset: [{0}][{3}/{4}] \"{1}\":{2} ({7} bytes)",
                                                   asset.FullID, asset.Name, asset.Description, asset.Type,
                                                   asset.InvType, temporary, local, asset.Data.Length));
        }

        public bool ExistsAsset(LLUUID uuid)
        {
            DataRow row = ds.Tables["assets"].Rows.Find(uuid);
            return (row != null);
        }

        public void DeleteAsset(LLUUID uuid)
        {
            lock (ds)
            {
                DataRow row = ds.Tables["assets"].Rows.Find(uuid);
                if (row != null)
                {
                    row.Delete();
                }
            }
        }

        public void CommitAssets() // force a sync to the database
        {
            MainLog.Instance.Verbose("AssetStorage", "Attempting commit");
            lock (ds)
            {
                da.Update(ds, "assets");
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

            createCol(assets, "UUID", typeof (String));
            createCol(assets, "Name", typeof (String));
            createCol(assets, "Description", typeof (String));
            createCol(assets, "Type", typeof (Int32));
            createCol(assets, "InvType", typeof (Int32));
            createCol(assets, "Local", typeof (Boolean));
            createCol(assets, "Temporary", typeof (Boolean));
            createCol(assets, "Data", typeof (Byte[]));
            // Add in contraints
            assets.PrimaryKey = new DataColumn[] {assets.Columns["UUID"]};
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

            asset.FullID = new LLUUID((String) row["UUID"]);
            asset.Name = (String) row["Name"];
            asset.Description = (String) row["Description"];
            asset.Type = Convert.ToSByte(row["Type"]);
            asset.InvType = Convert.ToSByte(row["InvType"]);
            asset.Local = Convert.ToBoolean(row["Local"]);
            asset.Temporary = Convert.ToBoolean(row["Temporary"]);
            asset.Data = (byte[]) row["Data"];
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
            foreach (DataColumn col in ds.Tables["assets"].Columns)
            {
                if (row[col] == null)
                {
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
            delete.Parameters.Add(createSqliteParameter("UUID", typeof (String)));
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
            try
            {
                pDa.Fill(tmpDS, "assets");
            }
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Verbose("DATASTORE", "SQLite Database doesn't exist... creating");
                InitDB(conn);
            }
            return true;
        }

        #region IPlugin interface

        public string Version
        {
            get
            {
                Module module = GetType().Module;
                string dllName = module.Assembly.ManifestModule.Name;
                Version dllVersion = module.Assembly.GetName().Version;

                return
                    string.Format("{0}.{1}.{2}.{3}", dllVersion.Major, dllVersion.Minor, dllVersion.Build,
                                  dllVersion.Revision);
            }
        }

        public void Initialise()
        {
            Initialise("AssetStorage.db", "");
        }

        public string Name
        {
            get { return "SQLite Asset storage engine"; }
        }

        #endregion
    }
}
