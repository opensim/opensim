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
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The database manager
        /// </summary>
        /// <summary>
        /// Artificial constructor called upon plugin load
        /// </summary>
        private const string SelectAssetSQL = "select * from assets where UUID=:UUID";
        private const string DeleteAssetSQL = "delete from assets where UUID=:UUID";
        private const string InsertAssetSQL = "insert into assets(UUID, Name, Description, Type, InvType, Local, Temporary, Data) values(:UUID, :Name, :Description, :Type, :InvType, :Local, :Temporary, :Data)";
        private const string UpdateAssetSQL = "update assets set Name=:Name, Description=:Description, Type=:Type, InvType=:InvType, Local=:Local, Temporary=:Temporary, Data=:Data where UUID=:UUID"; 
        private const string assetSelect = "select * from assets";

        private SqliteConnection m_conn;

        public void Initialise(string dbfile, string dbname)
        {
            m_conn = new SqliteConnection("URI=file:" + dbfile + ",version=3");
            m_conn.Open();
            TestTables(m_conn);
            return;
        }

        public AssetBase FetchAsset(LLUUID uuid)
        {
            
            using (SqliteCommand cmd = new SqliteCommand(SelectAssetSQL, m_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(uuid)));
                using (IDataReader reader = cmd.ExecuteReader()) 
                {
                    if (reader.Read())
                    {
                        AssetBase asset = buildAsset(reader);
                        reader.Close();
                        return asset;
                    }
                    else
                    {
                        reader.Close();
                        return null;
                    }
                }
            }
        }

        public void CreateAsset(AssetBase asset)
        {
            m_log.Info("[SQLITE]: Creating Asset " + Util.ToRawUuidString(asset.FullID));
            if (ExistsAsset(asset.FullID))
            {
                m_log.Info("[SQLITE]: Asset exists, updating instead.  You should fix the caller for this!");
                UpdateAsset(asset);
            }
            else 
            {
                using (SqliteCommand cmd = new SqliteCommand(InsertAssetSQL, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(asset.FullID)));
                    cmd.Parameters.Add(new SqliteParameter(":Name", asset.Name));
                    cmd.Parameters.Add(new SqliteParameter(":Description", asset.Description));
                    cmd.Parameters.Add(new SqliteParameter(":Type", asset.Type));
                    cmd.Parameters.Add(new SqliteParameter(":InvType", asset.InvType));
                    cmd.Parameters.Add(new SqliteParameter(":Local", asset.Local));
                    cmd.Parameters.Add(new SqliteParameter(":Temporary", asset.Temporary));
                    cmd.Parameters.Add(new SqliteParameter(":Data", asset.Data));
                    
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateAsset(AssetBase asset)
        {
            LogAssetLoad(asset);
            
            using (SqliteCommand cmd = new SqliteCommand(UpdateAssetSQL, m_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(asset.FullID)));
                cmd.Parameters.Add(new SqliteParameter(":Name", asset.Name));
                cmd.Parameters.Add(new SqliteParameter(":Description", asset.Description));
                cmd.Parameters.Add(new SqliteParameter(":Type", asset.Type));
                cmd.Parameters.Add(new SqliteParameter(":InvType", asset.InvType));
                cmd.Parameters.Add(new SqliteParameter(":Local", asset.Local));
                cmd.Parameters.Add(new SqliteParameter(":Temporary", asset.Temporary));
                cmd.Parameters.Add(new SqliteParameter(":Data", asset.Data));
                
                cmd.ExecuteNonQuery();
            }

        }

        private void LogAssetLoad(AssetBase asset)
        {
            string temporary = asset.Temporary ? "Temporary" : "Stored";
            string local = asset.Local ? "Local" : "Remote";

            m_log.Info("[SQLITE]: " +
                                     string.Format("Loaded {6} {5} Asset: [{0}][{3}/{4}] \"{1}\":{2} ({7} bytes)",
                                                   asset.FullID, asset.Name, asset.Description, asset.Type,
                                                   asset.InvType, temporary, local, asset.Data.Length));
        }

        public bool ExistsAsset(LLUUID uuid)
        {
            using (SqliteCommand cmd = new SqliteCommand(SelectAssetSQL, m_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(uuid)));
                using (IDataReader reader = cmd.ExecuteReader()) 
                {
                    if(reader.Read())
                    {
                        reader.Close();
                        return true;
                    }
                    else 
                    {
                        reader.Close();
                        return false;
                    }
                }
            }
        }

        public void DeleteAsset(LLUUID uuid)
        {
            using (SqliteCommand cmd = new SqliteCommand(DeleteAssetSQL, m_conn))
            {
                cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(uuid)));
                
                cmd.ExecuteNonQuery();
            }
        }

        public void CommitAssets() // force a sync to the database
        {
            m_log.Info("[SQLITE]: Attempting commit");
            // lock (ds)
            //             {
            //                 da.Update(ds, "assets");
            //                 ds.AcceptChanges();
            // }
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

        private AssetBase buildAsset(IDataReader row)
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


        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        private void InitDB(SqliteConnection conn)
        {
            string createAssets = defineTable(createAssetsTable());
            SqliteCommand pcmd = new SqliteCommand(createAssets, conn);
            pcmd.ExecuteNonQuery();
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
                m_log.Info("[SQLITE]: SQLite Database doesn't exist... creating");
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