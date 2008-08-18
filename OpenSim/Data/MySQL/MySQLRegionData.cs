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
using System.IO;
using System.Reflection;
using System.Threading;
using libsecondlife;
using log4net;
using MySql.Data.MySqlClient;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Region Server
    /// </summary>
    public class MySQLDataStore : IRegionDataStore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string m_primSelect = "select * from prims";
        private const string m_shapeSelect = "select * from primshapes";
        private const string m_itemsSelect = "select * from primitems";
        private const string m_terrainSelect = "select * from terrain limit 1";
        private const string m_landSelect = "select * from land";
        private const string m_landAccessListSelect = "select * from landaccesslist";
        private const string m_regionSettingsSelect = "select * from regionsettings";
        private const string m_waitTimeoutSelect = "select @@wait_timeout";

        private MySqlConnection m_connection;
        private string m_connectionString;

        /// <summary>
        /// Wait timeout for our connection in ticks.
        /// </summary>
        private long m_waitTimeout;

        /// <summary>
        /// Make our storage of the timeout this amount smaller than it actually is, to give us a margin on long
        /// running database operations.
        /// </summary>
        private long m_waitTimeoutLeeway = 60 * TimeSpan.TicksPerSecond;

        /// <summary>
        /// Holds the last tick time that the connection was used.
        /// </summary>
        private long m_lastConnectionUse;

        private DataSet m_dataSet;
        private MySqlDataAdapter m_primDataAdapter;
        private MySqlDataAdapter m_shapeDataAdapter;
        private MySqlDataAdapter m_itemsDataAdapter;
        private MySqlDataAdapter m_terrainDataAdapter;
        private MySqlDataAdapter m_landDataAdapter;
        private MySqlDataAdapter m_landAccessListDataAdapter;
        private MySqlDataAdapter m_regionSettingsDataAdapter;

        private DataTable m_primTable;
        private DataTable m_shapeTable;
        private DataTable m_itemsTable;
        private DataTable m_terrainTable;
        private DataTable m_landTable;
        private DataTable m_landAccessListTable;
        private DataTable m_regionSettingsTable;

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        /// <summary>
        /// see IRegionDataStore
        /// </summary>
        /// <param name="connectionstring"></param>
        public void Initialise(string connectionString)
        {
            m_connectionString = connectionString;

            m_dataSet = new DataSet();

            int passPosition = 0;
            int passEndPosition = 0;
            string displayConnectionString = null;

            try
            {  // hide the password in the connection string
                passPosition = m_connectionString.IndexOf("password", StringComparison.OrdinalIgnoreCase);
                passPosition = m_connectionString.IndexOf("=", passPosition);
                if (passPosition < m_connectionString.Length)
                    passPosition += 1;
                passEndPosition = m_connectionString.IndexOf(";", passPosition);

                displayConnectionString = m_connectionString.Substring(0, passPosition);
                displayConnectionString += "***";
                displayConnectionString += m_connectionString.Substring(passEndPosition, m_connectionString.Length - passEndPosition);
            }
            catch (Exception e )
            {
                m_log.Debug("Exception: password not found in connection string\n" + e.ToString());
            }

            m_log.Info("[REGION DB]: MySql - connecting: " + displayConnectionString);
            m_connection = new MySqlConnection(m_connectionString);
            m_connection.Open();

            GetWaitTimeout();

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration(m_connection, assem, "RegionStore");

            // TODO: After rev 6000, remove this.  People should have
            // been rolled onto the new migration code by then.
            TestTables(m_connection, m);

            m.Update();

            MySqlCommand primSelectCmd = new MySqlCommand(m_primSelect, m_connection);
            m_primDataAdapter = new MySqlDataAdapter(primSelectCmd);

            MySqlCommand shapeSelectCmd = new MySqlCommand(m_shapeSelect, m_connection);
            m_shapeDataAdapter = new MySqlDataAdapter(shapeSelectCmd);

            MySqlCommand itemsSelectCmd = new MySqlCommand(m_itemsSelect, m_connection);
            m_itemsDataAdapter = new MySqlDataAdapter(itemsSelectCmd);

            MySqlCommand terrainSelectCmd = new MySqlCommand(m_terrainSelect, m_connection);
            m_terrainDataAdapter = new MySqlDataAdapter(terrainSelectCmd);

            MySqlCommand landSelectCmd = new MySqlCommand(m_landSelect, m_connection);
            m_landDataAdapter = new MySqlDataAdapter(landSelectCmd);

            MySqlCommand landAccessListSelectCmd = new MySqlCommand(m_landAccessListSelect, m_connection);
            m_landAccessListDataAdapter = new MySqlDataAdapter(landAccessListSelectCmd);

            MySqlCommand regionSettingsSelectCmd = new MySqlCommand(m_regionSettingsSelect, m_connection);
            m_regionSettingsDataAdapter = new MySqlDataAdapter(regionSettingsSelectCmd);

            lock (m_dataSet)
            {
                m_primTable = createPrimTable();
                m_dataSet.Tables.Add(m_primTable);
                SetupPrimCommands(m_primDataAdapter, m_connection);
                m_primDataAdapter.Fill(m_primTable);

                m_shapeTable = createShapeTable();
                m_dataSet.Tables.Add(m_shapeTable);
                SetupShapeCommands(m_shapeDataAdapter, m_connection);
                m_shapeDataAdapter.Fill(m_shapeTable);

                m_itemsTable = createItemsTable();
                m_dataSet.Tables.Add(m_itemsTable);
                SetupItemsCommands(m_itemsDataAdapter, m_connection);
                m_itemsDataAdapter.Fill(m_itemsTable);

                m_terrainTable = createTerrainTable();
                m_dataSet.Tables.Add(m_terrainTable);
                SetupTerrainCommands(m_terrainDataAdapter, m_connection);
                m_terrainDataAdapter.Fill(m_terrainTable);

                m_landTable = createLandTable();
                m_dataSet.Tables.Add(m_landTable);
                setupLandCommands(m_landDataAdapter, m_connection);
                m_landDataAdapter.Fill(m_landTable);

                m_landAccessListTable = createLandAccessListTable();
                m_dataSet.Tables.Add(m_landAccessListTable);
                setupLandAccessCommands(m_landAccessListDataAdapter, m_connection);
                m_landAccessListDataAdapter.Fill(m_landAccessListTable);

                m_regionSettingsTable = createRegionSettingsTable();
                m_dataSet.Tables.Add(m_regionSettingsTable);
                SetupRegionSettingsCommands(m_regionSettingsDataAdapter, m_connection);
                m_regionSettingsDataAdapter.Fill(m_regionSettingsTable);
            }
        }

        /// <summary>
        /// Get the wait_timeout value for our connection
        /// </summary>
        protected void GetWaitTimeout()
        {
            MySqlCommand cmd = new MySqlCommand(m_waitTimeoutSelect, m_connection);

            using (MySqlDataReader dbReader = cmd.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (dbReader.Read())
                {
                    m_waitTimeout
                        = Convert.ToInt32(dbReader["@@wait_timeout"]) * TimeSpan.TicksPerSecond + m_waitTimeoutLeeway;
                }

                dbReader.Close();
                cmd.Dispose();
            }

            m_lastConnectionUse = System.DateTime.Now.Ticks;

            m_log.DebugFormat(
                "[REGION DB]: Connection wait timeout {0} seconds", m_waitTimeout / TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// Should be called before any db operation.  This checks to see if the connection has not timed out
        /// </summary>
        protected void CheckConnection()
        {
            //m_log.Debug("[REGION DB]: Checking connection");

            long timeNow = System.DateTime.Now.Ticks;
            if (timeNow - m_lastConnectionUse > m_waitTimeout || m_connection.State != ConnectionState.Open)
            {
                m_log.DebugFormat("[REGION DB]: Database connection has gone away - reconnecting");

                lock (m_connection)
                {
                    m_connection.Close();
                    m_connection = new MySqlConnection(m_connectionString);
                    m_connection.Open();
                }
            }

            // Strictly, we should set this after the actual db operation.  But it's more convenient to set here rather
            // than require the code to call another method - the timeout leeway should be large enough to cover the
            // inaccuracy.
            m_lastConnectionUse = timeNow;
        }

        /// <summary>
        /// Given a list of tables, return the version of the tables, as seen in the database
        /// </summary>
        /// <param name="tableList">The list of table</param>
        /// <param name="dbcon">The database connection handler</param>
        public void GetTableVersion(Dictionary<string, string> tableList, MySqlConnection dbcon)
        {
            lock (dbcon)
            {
                MySqlCommand tablesCmd =
                    new MySqlCommand(
                        "SELECT TABLE_NAME, TABLE_COMMENT FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=?dbname",
                        dbcon);
                tablesCmd.Parameters.AddWithValue("?dbname", dbcon.Database);

                CheckConnection();
                using (MySqlDataReader tables = tablesCmd.ExecuteReader())
                {
                    while (tables.Read())
                    {
                        try
                        {
                            string tableName = (string)tables["TABLE_NAME"];
                            string comment = (string)tables["TABLE_COMMENT"];
                            if (tableList.ContainsKey(tableName))
                            {
                                tableList[tableName] = comment;
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Error(e.ToString());
                        }
                    }
                    tables.Close();
                }
            }
        }
        // private void TestTablesVersionable(MySqlConnection dbconn)
        // {
        //     Dictionary<string, string> tableList = new Dictionary<string, string>();

        //     tableList["land"] = null;
        //     dbconn.Open();
        //     GetTableVersion(tableList,dbconn);

        //     UpgradeLandTable(tableList["land"], dbconn);
        //     //database.Close();

        // }

        /// <summary>
        /// Execute a SQL statement stored in a resource, as a string
        /// </summary>
        /// <param name="name">the ressource name</param>
        /// <param name="dbcon">The database connection handler</param>
        public void ExecuteResourceSql(string name, MySqlConnection dbcon)
        {
            MySqlCommand cmd = new MySqlCommand(getResourceString(name), dbcon);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Extract a named string resource from the embedded resources
        /// </summary>
        /// <param name="name">name of embedded resource</param>
        /// <returns>string contained within the embedded resource</returns>
        private string getResourceString(string name)
        {
            Assembly assem = GetType().Assembly;
            string[] names = assem.GetManifestResourceNames();

            foreach (string s in names)
            {
                if (s.EndsWith(name))
                {
                    using (Stream resource = assem.GetManifestResourceStream(s))
                    {
                        using (StreamReader resourceReader = new StreamReader(resource))
                        {
                            string resourceString = resourceReader.ReadToEnd();
                            return resourceString;
                        }
                    }
                }
            }
            throw new Exception(string.Format("Resource '{0}' was not found", name));
        }

        /// <summary>
        /// <list type="bullet">
        /// <item>Execute CreateLandTable.sql if oldVersion == null</item>
        /// <item>Execute UpgradeLandTable.sqm if oldVersion contain "Rev."</item>
        /// </list>
        /// </summary>
        /// <param name="oldVersion"></param>
        /// <param name="dbconn">The database connection handler</param>
        // private void UpgradeLandTable(string oldVersion, MySqlConnection dbconn)
        // {
        //     // null as the version, indicates that the table didn't exist
        //     if (oldVersion == null)
        //     {
        //         ExecuteResourceSql("CreateLandTable.sql",dbconn);
        //         oldVersion = "Rev. 2; InnoDB free: 0 kB";
        //     }
        //     if (!oldVersion.Contains("Rev."))
        //     {
        //         ExecuteResourceSql("UpgradeLandTableToVersion2.sql", dbconn);
        //     }
        // }

        /// <summary>
        /// Adds an object into region storage
        /// </summary>
        /// <param name="obj">The object</param>
        /// <param name="regionUUID">The region UUID</param>
        public void StoreObject(SceneObjectGroup obj, LLUUID regionUUID)
        {
            lock (m_dataSet)
            {
                foreach (SceneObjectPart prim in obj.Children.Values)
                {
                    if ((prim.GetEffectiveObjectFlags() & (uint)LLObject.ObjectFlags.Physics) == 0
                        && (prim.GetEffectiveObjectFlags() & (uint)LLObject.ObjectFlags.Temporary) == 0
                        && (prim.GetEffectiveObjectFlags() & (uint)LLObject.ObjectFlags.TemporaryOnRez) == 0)
                    {
                        //m_log.Info("[REGION DB]: Adding obj: " + obj.UUID + " to region: " + regionUUID);
                        addPrim(prim, obj.UUID, regionUUID);
                    }
                    else
                    {
                        // m_log.Info("[DATASTORE]: Ignoring Physical obj: " + obj.UUID + " in region: " + regionUUID);
                    }
                }
                Commit();
            }
        }

        /// <summary>
        /// removes an object from region storage
        /// </summary>
        /// <param name="obj">The object</param>
        /// <param name="regionUUID">The Region UUID</param>
        public void RemoveObject(LLUUID obj, LLUUID regionUUID)
        {
            m_log.InfoFormat("[REGION DB]: Removing obj: {0} from region: {1}", obj.UUID, regionUUID);

            DataTable prims = m_primTable;
            DataTable shapes = m_shapeTable;

            string selectExp = "SceneGroupID = '" + Util.ToRawUuidString(obj) + "'";
            lock (m_dataSet)
            {
                DataRow[] primRows = prims.Select(selectExp);
                foreach (DataRow row in primRows)
                {
                    // Remove shapes row
                    LLUUID uuid = new LLUUID((string) row["UUID"]);
                    DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(uuid));
                    if (shapeRow != null)
                    {
                        shapeRow.Delete();
                    }

                        RemoveItems(uuid);

                    // Remove prim row
                    row.Delete();
                }
                Commit();
            }
        }

        /// <summary>
        /// Remove all persisted items of the given prim.
        /// The caller must acquire the necessrary synchronization locks and commit or rollback changes.
        /// </summary>
        /// <param name="uuid">the Item UUID</param>
        private void RemoveItems(LLUUID uuid)
        {
            String sql = String.Format("primID = '{0}'", uuid);
            DataRow[] itemRows = m_itemsTable.Select(sql);

            foreach (DataRow itemRow in itemRows)
            {
                itemRow.Delete();
            }
        }

        /// <summary>
        /// Load persisted objects from region storage.
        /// </summary>
        /// <param name="regionUUID">the Region UUID</param>
        /// <returns>List of loaded groups</returns>
        public List<SceneObjectGroup> LoadObjects(LLUUID regionUUID)
        {
            Dictionary<LLUUID, SceneObjectGroup> createdObjects = new Dictionary<LLUUID, SceneObjectGroup>();

            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            DataTable prims = m_primTable;
            DataTable shapes = m_shapeTable;

            string byRegion = "RegionUUID = '" + Util.ToRawUuidString(regionUUID) + "'";
            string orderByParent = "ParentID ASC";

            lock (m_dataSet)
            {
                CheckConnection();
                DataRow[] primsForRegion = prims.Select(byRegion, orderByParent);
                m_log.Info("[REGION DB]: " +
                                         "Loaded " + primsForRegion.Length + " prims for region: " + regionUUID);

                foreach (DataRow primRow in primsForRegion)
                {
                    try
                    {
                        string uuid = (string) primRow["UUID"];
                        string objID = (string) primRow["SceneGroupID"];

                        SceneObjectPart prim = buildPrim(primRow);

                        if (uuid == objID) //is new SceneObjectGroup ?
                        {
                            SceneObjectGroup group = new SceneObjectGroup();

                            DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                m_log.Info(
                                    "No shape found for prim in storage, so setting default box shape");
                                prim.Shape = PrimitiveBaseShape.Default;
                            }
                            group.AddPart(prim);
                            group.RootPart = prim;

                            createdObjects.Add(group.UUID, group);
                            retvals.Add(group);
                        }
                        else
                        {
                            DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                m_log.Info(
                                    "No shape found for prim in storage, so setting default box shape");
                                prim.Shape = PrimitiveBaseShape.Default;
                            }
                            createdObjects[new LLUUID(objID)].AddPart(prim);
                        }

                            LoadItems(prim);
                        }
                    catch (Exception e)
                    {
                        m_log.Error("[REGION DB]: Failed create prim object, exception and data follows");
                        m_log.Info("[REGION DB]: " + e.ToString());
                        foreach (DataColumn col in prims.Columns)
                        {
                            m_log.Info("[REGION DB]: Col: " + col.ColumnName + " => " + primRow[col]);
                        }
                    }
                }
            }
            return retvals;
        }

        /// <summary>
        /// Load in a prim's persisted inventory.
        /// </summary>
        /// <param name="prim">The prim</param>
        private void LoadItems(SceneObjectPart prim)
        {
            lock (m_dataSet)
            {
                CheckConnection();
                //m_log.InfoFormat("[DATASTORE]: Loading inventory for {0}, {1}", prim.Name, prim.UUID);

                DataTable dbItems = m_itemsTable;

                String sql = String.Format("primID = '{0}'", prim.UUID.ToString());
                DataRow[] dbItemRows = dbItems.Select(sql);

                IList<TaskInventoryItem> inventory = new List<TaskInventoryItem>();

                foreach (DataRow row in dbItemRows)
                {
                    TaskInventoryItem item = buildItem(row);
                    inventory.Add(item);

                    //m_log.DebugFormat("[DATASTORE]: Restored item {0}, {1}", item.Name, item.ItemID);
                }

                prim.RestoreInventoryItems(inventory);

                // XXX A nasty little hack to recover the folder id for the prim (which is currently stored in
                // every item).  This data should really be stored in the prim table itself.
                if (dbItemRows.Length > 0)
                {
                    prim.FolderID = inventory[0].ParentID;
                }
            }
        }

        /// <summary>
        /// Store a terrain revision in region storage
        /// </summary>
        /// <param name="ter">HeightField data</param>
        /// <param name="regionID">region UUID</param>
        public void StoreTerrain(double[,] ter, LLUUID regionID)
        {
            int revision = 1;
            m_log.Info("[REGION DB]: Storing terrain revision r" + revision.ToString());

            lock (m_dataSet)
            {
                MySqlCommand delete = new MySqlCommand("delete from terrain where RegionUUID=?RegionUUID", m_connection);
                MySqlCommand cmd = new MySqlCommand("insert into terrain(RegionUUID, Revision, Heightfield)" +
                                                    " values(?RegionUUID, ?Revision, ?Heightfield)", m_connection);
                using (cmd)
                {
                    delete.Parameters.Add(new MySqlParameter("?RegionUUID", Util.ToRawUuidString(regionID)));

                    CheckConnection();
                    delete.ExecuteNonQuery();

                    cmd.Parameters.Add(new MySqlParameter("?RegionUUID", Util.ToRawUuidString(regionID)));
                    cmd.Parameters.Add(new MySqlParameter("?Revision", revision));
                    cmd.Parameters.Add(new MySqlParameter("?Heightfield", serializeTerrain(ter)));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Load the latest terrain revision from region storage
        /// </summary>
        /// <param name="regionID">the region UUID</param>
        /// <returns>Heightfield data</returns>
        public double[,] LoadTerrain(LLUUID regionID)
        {
            double[,] terret = new double[256,256];
            terret.Initialize();

            MySqlCommand cmd = new MySqlCommand(
                @"select RegionUUID, Revision, Heightfield from terrain
                where RegionUUID=?RegionUUID order by Revision desc limit 1"
                , m_connection);

            // MySqlParameter param = new MySqlParameter();
            cmd.Parameters.Add(new MySqlParameter("?RegionUUID", Util.ToRawUuidString(regionID)));

            if (m_connection.State != ConnectionState.Open)
            {
                m_connection.Open();
            }

            lock (m_dataSet)
            {
                CheckConnection();
                using (MySqlDataReader row = cmd.ExecuteReader())
                {
                    int rev = 0;
                    if (row.Read())
                    {
                        MemoryStream str = new MemoryStream((byte[]) row["Heightfield"]);
                        BinaryReader br = new BinaryReader(str);
                        for (int x = 0; x < 256; x++)
                        {
                            for (int y = 0; y < 256; y++)
                            {
                                terret[x, y] = br.ReadDouble();
                            }
                        }
                        rev = (int) row["Revision"];
                    }
                    else
                    {
                        m_log.Info("[REGION DB]: No terrain found for region");
                        return null;
                    }

                    m_log.Info("[REGION DB]: Loaded terrain revision r" + rev.ToString());
                }
            }
            return terret;
        }

        /// <summary>
        /// <list type="bullet">
        /// <item>delete from land where UUID=globalID</item>
        /// <item>delete from landaccesslist where LandUUID=globalID</item>
        /// </list>
        /// </summary>
        /// <param name="globalID"></param>
        public void RemoveLandObject(LLUUID globalID)
        {
            lock (m_dataSet)
            {
                CheckConnection();
                using (MySqlCommand cmd = new MySqlCommand("delete from land where UUID=?UUID", m_connection))
                {
                    cmd.Parameters.Add(new MySqlParameter("?UUID", Util.ToRawUuidString(globalID)));
                    cmd.ExecuteNonQuery();
                }

                using (
                    MySqlCommand cmd = new MySqlCommand("delete from landaccesslist where LandUUID=?UUID", m_connection)
                    )
                {
                    cmd.Parameters.Add(new MySqlParameter("?UUID", Util.ToRawUuidString(globalID)));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="parcel"></param>
        public void StoreLandObject(ILandObject parcel)
        {
            lock (m_dataSet)
            {
                CheckConnection();
                DataTable land = m_landTable;
                DataTable landaccesslist = m_landAccessListTable;

                DataRow landRow = land.Rows.Find(Util.ToRawUuidString(parcel.landData.GlobalID));
                if (landRow == null)
                {
                    landRow = land.NewRow();
                    fillLandRow(landRow, parcel.landData, parcel.regionUUID);
                    land.Rows.Add(landRow);
                }
                else
                {
                    fillLandRow(landRow, parcel.landData, parcel.regionUUID);
                }

                using (
                    MySqlCommand cmd =
                        new MySqlCommand("delete from landaccesslist where LandUUID=?LandUUID", m_connection))
                {
                    cmd.Parameters.Add(new MySqlParameter("?LandUUID", Util.ToRawUuidString(parcel.landData.GlobalID)));
                    cmd.ExecuteNonQuery();
                }

                foreach (ParcelManager.ParcelAccessEntry entry in parcel.landData.ParcelAccessList)
                {
                    DataRow newAccessRow = landaccesslist.NewRow();
                    fillLandAccessRow(newAccessRow, entry, parcel.landData.GlobalID);
                    landaccesslist.Rows.Add(newAccessRow);
                }

                Commit();
            }
        }

        public RegionSettings LoadRegionSettings(LLUUID regionUUID)
        {
            lock (m_dataSet)
            {
                CheckConnection();
                DataTable regionsettings = m_regionSettingsTable;
                string searchExp = "regionUUID = '" + regionUUID.ToString() + "'";
                DataRow[] rawsettings = regionsettings.Select(searchExp);
                if (rawsettings.Length == 0)
                {
                    RegionSettings rs = new RegionSettings();
                    rs.RegionUUID = regionUUID;
                    rs.OnSave += StoreRegionSettings;

                    StoreRegionSettings(rs);

                    return rs;
                }
                DataRow row = rawsettings[0];

                RegionSettings newSettings =  buildRegionSettings(row);
                newSettings.OnSave += StoreRegionSettings;

                return newSettings;
            }
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
            lock (m_dataSet)
            {
                CheckConnection();
                DataTable regionsettings = m_dataSet.Tables["regionsettings"];

                DataRow settingsRow = regionsettings.Rows.Find(rs.RegionUUID.ToString());
                if (settingsRow == null)
                {
                    settingsRow = regionsettings.NewRow();
                    fillRegionSettingsRow(settingsRow, rs);
                    regionsettings.Rows.Add(settingsRow);
                }
                else
                {
                    fillRegionSettingsRow(settingsRow, rs);
                }

                Commit();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionUUID"></param>
        /// <returns></returns>
        public List<LandData> LoadLandObjects(LLUUID regionUUID)
        {
            List<LandData> landDataForRegion = new List<LandData>();
            lock (m_dataSet)
            {
                CheckConnection();
                DataTable land = m_landTable;
                DataTable landaccesslist = m_landAccessListTable;
                string searchExp = "RegionUUID = '" + Util.ToRawUuidString(regionUUID) + "'";
                DataRow[] rawDataForRegion = land.Select(searchExp);
                foreach (DataRow rawDataLand in rawDataForRegion)
                {
                    LandData newLand = buildLandData(rawDataLand);
                    string accessListSearchExp = "LandUUID = '" + Util.ToRawUuidString(newLand.GlobalID) + "'";
                    DataRow[] rawDataForLandAccessList = landaccesslist.Select(accessListSearchExp);
                    foreach (DataRow rawDataLandAccess in rawDataForLandAccessList)
                    {
                        newLand.ParcelAccessList.Add(buildLandAccessData(rawDataLandAccess));
                    }

                    landDataForRegion.Add(newLand);
                }
            }
            return landDataForRegion;
        }

        /// <summary>
        ///
        /// </summary>
        public void Commit()
        {
            lock (m_dataSet)
            {
                CheckConnection();
                // DisplayDataSet(m_dataSet, "Region DataSet");

                m_primDataAdapter.Update(m_primTable);
                m_shapeDataAdapter.Update(m_shapeTable);

                    m_itemsDataAdapter.Update(m_itemsTable);

                m_terrainDataAdapter.Update(m_terrainTable);
                m_landDataAdapter.Update(m_landTable);
                m_landAccessListDataAdapter.Update(m_landAccessListTable);
                m_regionSettingsDataAdapter.Update(m_regionSettingsTable);

                m_dataSet.AcceptChanges();
            }
        }

        /// <summary>
        /// See <see cref="Commit"/>
        /// </summary>
        public void Shutdown()
        {
            Commit();
        }

        /***********************************************************************
         *
         *  Database Definition Functions
         *
         *  This should be db agnostic as we define them in ADO.NET terms
         *
         **********************************************************************/

        /// <summary>
        ///
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static DataColumn createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
            return col;
        }

        /// <summary>
        /// Create the "terrain" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createTerrainTable()
        {
            DataTable terrain = new DataTable("terrain");

            createCol(terrain, "RegionUUID", typeof (String));
            createCol(terrain, "Revision", typeof (Int32));
            createCol(terrain, "Heightfield", typeof (Byte[]));
            return terrain;
        }

        /// <summary>
        /// Create the "regionsettings" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createRegionSettingsTable()
        {
            DataTable regionsettings = new DataTable("regionsettings");
            createCol(regionsettings, "regionUUID", typeof(String));
            createCol(regionsettings, "block_terraform", typeof (Int32));
            createCol(regionsettings, "block_fly", typeof (Int32));
            createCol(regionsettings, "allow_damage", typeof (Int32));
            createCol(regionsettings, "restrict_pushing", typeof (Int32));
            createCol(regionsettings, "allow_land_resell", typeof (Int32));
            createCol(regionsettings, "allow_land_join_divide", typeof (Int32));
            createCol(regionsettings, "block_show_in_search", typeof (Int32));
            createCol(regionsettings, "agent_limit", typeof (Int32));
            createCol(regionsettings, "object_bonus", typeof (Double));
            createCol(regionsettings, "maturity", typeof (Int32));
            createCol(regionsettings, "disable_scripts", typeof (Int32));
            createCol(regionsettings, "disable_collisions", typeof (Int32));
            createCol(regionsettings, "disable_physics", typeof (Int32));
            createCol(regionsettings, "terrain_texture_1", typeof(String));
            createCol(regionsettings, "terrain_texture_2", typeof(String));
            createCol(regionsettings, "terrain_texture_3", typeof(String));
            createCol(regionsettings, "terrain_texture_4", typeof(String));
            createCol(regionsettings, "elevation_1_nw", typeof (Double));
            createCol(regionsettings, "elevation_2_nw", typeof (Double));
            createCol(regionsettings, "elevation_1_ne", typeof (Double));
            createCol(regionsettings, "elevation_2_ne", typeof (Double));
            createCol(regionsettings, "elevation_1_se", typeof (Double));
            createCol(regionsettings, "elevation_2_se", typeof (Double));
            createCol(regionsettings, "elevation_1_sw", typeof (Double));
            createCol(regionsettings, "elevation_2_sw", typeof (Double));
            createCol(regionsettings, "water_height", typeof (Double));
            createCol(regionsettings, "terrain_raise_limit", typeof (Double));
            createCol(regionsettings, "terrain_lower_limit", typeof (Double));
            createCol(regionsettings, "use_estate_sun", typeof (Int32));
            createCol(regionsettings, "sandbox", typeof (Int32));
            createCol(regionsettings, "fixed_sun", typeof (Int32));
            createCol(regionsettings, "sun_position", typeof (Double));
            createCol(regionsettings, "covenant", typeof(String));

            regionsettings.PrimaryKey = new DataColumn[] {regionsettings.Columns["RegionUUID"]};

            return regionsettings;
        }

        /// <summary>
        /// Create the "prims" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createPrimTable()
        {
            DataTable prims = new DataTable("prims");

            createCol(prims, "UUID", typeof (String));
            createCol(prims, "RegionUUID", typeof (String));
            createCol(prims, "ParentID", typeof (Int32));
            createCol(prims, "CreationDate", typeof (Int32));
            createCol(prims, "Name", typeof (String));
            createCol(prims, "SceneGroupID", typeof (String));
            // various text fields
            createCol(prims, "Text", typeof (String));
            createCol(prims, "Description", typeof (String));
            createCol(prims, "SitName", typeof (String));
            createCol(prims, "TouchName", typeof (String));
            // permissions
            createCol(prims, "ObjectFlags", typeof (Int32));
            createCol(prims, "CreatorID", typeof (String));
            createCol(prims, "OwnerID", typeof (String));
            createCol(prims, "GroupID", typeof (String));
            createCol(prims, "LastOwnerID", typeof (String));
            createCol(prims, "OwnerMask", typeof (Int32));
            createCol(prims, "NextOwnerMask", typeof (Int32));
            createCol(prims, "GroupMask", typeof (Int32));
            createCol(prims, "EveryoneMask", typeof (Int32));
            createCol(prims, "BaseMask", typeof (Int32));
            // vectors
            createCol(prims, "PositionX", typeof (Double));
            createCol(prims, "PositionY", typeof (Double));
            createCol(prims, "PositionZ", typeof (Double));
            createCol(prims, "GroupPositionX", typeof (Double));
            createCol(prims, "GroupPositionY", typeof (Double));
            createCol(prims, "GroupPositionZ", typeof (Double));
            createCol(prims, "VelocityX", typeof (Double));
            createCol(prims, "VelocityY", typeof (Double));
            createCol(prims, "VelocityZ", typeof (Double));
            createCol(prims, "AngularVelocityX", typeof (Double));
            createCol(prims, "AngularVelocityY", typeof (Double));
            createCol(prims, "AngularVelocityZ", typeof (Double));
            createCol(prims, "AccelerationX", typeof (Double));
            createCol(prims, "AccelerationY", typeof (Double));
            createCol(prims, "AccelerationZ", typeof (Double));
            // quaternions
            createCol(prims, "RotationX", typeof (Double));
            createCol(prims, "RotationY", typeof (Double));
            createCol(prims, "RotationZ", typeof (Double));
            createCol(prims, "RotationW", typeof (Double));
            // sit target
            createCol(prims, "SitTargetOffsetX", typeof (Double));
            createCol(prims, "SitTargetOffsetY", typeof (Double));
            createCol(prims, "SitTargetOffsetZ", typeof (Double));

            createCol(prims, "SitTargetOrientW", typeof (Double));
            createCol(prims, "SitTargetOrientX", typeof (Double));
            createCol(prims, "SitTargetOrientY", typeof (Double));
            createCol(prims, "SitTargetOrientZ", typeof (Double));


            // Add in contraints
            prims.PrimaryKey = new DataColumn[] {prims.Columns["UUID"]};

            return prims;
        }

        /// <summary>
        /// Create the "land" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createLandTable()
        {
            DataTable land = new DataTable("land");
            createCol(land, "UUID", typeof (String));
            createCol(land, "RegionUUID", typeof (String));
            createCol(land, "LocalLandID", typeof (Int32));

            // Bitmap is a byte[512]
            createCol(land, "Bitmap", typeof (Byte[]));

            createCol(land, "Name", typeof (String));
            createCol(land, "Description", typeof (String));
            createCol(land, "OwnerUUID", typeof (String));
            createCol(land, "IsGroupOwned", typeof (Int32));
            createCol(land, "Area", typeof (Int32));
            createCol(land, "AuctionID", typeof (Int32)); //Unemplemented
            createCol(land, "Category", typeof (Int32)); //Enum libsecondlife.Parcel.ParcelCategory
            createCol(land, "ClaimDate", typeof (Int32));
            createCol(land, "ClaimPrice", typeof (Int32));
            createCol(land, "GroupUUID", typeof (String));
            createCol(land, "SalePrice", typeof (Int32));
            createCol(land, "LandStatus", typeof (Int32)); //Enum. libsecondlife.Parcel.ParcelStatus
            createCol(land, "LandFlags", typeof (Int32));
            createCol(land, "LandingType", typeof (Int32));
            createCol(land, "MediaAutoScale", typeof (Int32));
            createCol(land, "MediaTextureUUID", typeof (String));
            createCol(land, "MediaURL", typeof (String));
            createCol(land, "MusicURL", typeof (String));
            createCol(land, "PassHours", typeof (Double));
            createCol(land, "PassPrice", typeof (Int32));
            createCol(land, "SnapshotUUID", typeof (String));
            createCol(land, "UserLocationX", typeof (Double));
            createCol(land, "UserLocationY", typeof (Double));
            createCol(land, "UserLocationZ", typeof (Double));
            createCol(land, "UserLookAtX", typeof (Double));
            createCol(land, "UserLookAtY", typeof (Double));
            createCol(land, "UserLookAtZ", typeof (Double));
            createCol(land, "AuthBuyerID", typeof (String));

            land.PrimaryKey = new DataColumn[] {land.Columns["UUID"]};

            return land;
        }

        /// <summary>
        /// Create the "landaccesslist" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createLandAccessListTable()
        {
            DataTable landaccess = new DataTable("landaccesslist");
            createCol(landaccess, "LandUUID", typeof (String));
            createCol(landaccess, "AccessUUID", typeof (String));
            createCol(landaccess, "Flags", typeof (Int32));

            return landaccess;
        }

        /// <summary>
        /// Create the "primshapes" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createShapeTable()
        {
            DataTable shapes = new DataTable("primshapes");
            createCol(shapes, "UUID", typeof (String));
            // shape is an enum
            createCol(shapes, "Shape", typeof (Int32));
            // vectors
            createCol(shapes, "ScaleX", typeof (Double));
            createCol(shapes, "ScaleY", typeof (Double));
            createCol(shapes, "ScaleZ", typeof (Double));
            // paths
            createCol(shapes, "PCode", typeof (Int32));
            createCol(shapes, "PathBegin", typeof (Int32));
            createCol(shapes, "PathEnd", typeof (Int32));
            createCol(shapes, "PathScaleX", typeof (Int32));
            createCol(shapes, "PathScaleY", typeof (Int32));
            createCol(shapes, "PathShearX", typeof (Int32));
            createCol(shapes, "PathShearY", typeof (Int32));
            createCol(shapes, "PathSkew", typeof (Int32));
            createCol(shapes, "PathCurve", typeof (Int32));
            createCol(shapes, "PathRadiusOffset", typeof (Int32));
            createCol(shapes, "PathRevolutions", typeof (Int32));
            createCol(shapes, "PathTaperX", typeof (Int32));
            createCol(shapes, "PathTaperY", typeof (Int32));
            createCol(shapes, "PathTwist", typeof (Int32));
            createCol(shapes, "PathTwistBegin", typeof (Int32));
            // profile
            createCol(shapes, "ProfileBegin", typeof (Int32));
            createCol(shapes, "ProfileEnd", typeof (Int32));
            createCol(shapes, "ProfileCurve", typeof (Int32));
            createCol(shapes, "ProfileHollow", typeof (Int32));
            createCol(shapes, "State", typeof(Int32));
            createCol(shapes, "Texture", typeof (Byte[]));
            createCol(shapes, "ExtraParams", typeof (Byte[]));

            shapes.PrimaryKey = new DataColumn[] {shapes.Columns["UUID"]};

            return shapes;
        }

        /// <summary>
        /// Create the "primitems" table
        /// </summary>
        /// <returns></returns>
        private static DataTable createItemsTable()
        {
            DataTable items = new DataTable("primitems");

            createCol(items, "itemID", typeof (String));
            createCol(items, "primID", typeof (String));
            createCol(items, "assetID", typeof (String));
            createCol(items, "parentFolderID", typeof (String));

            createCol(items, "invType", typeof (Int32));
            createCol(items, "assetType", typeof (Int32));

            createCol(items, "name", typeof (String));
            createCol(items, "description", typeof (String));

            createCol(items, "creationDate", typeof (Int64));
            createCol(items, "creatorID", typeof (String));
            createCol(items, "ownerID", typeof (String));
            createCol(items, "lastOwnerID", typeof (String));
            createCol(items, "groupID", typeof (String));

            createCol(items, "nextPermissions", typeof (Int32));
            createCol(items, "currentPermissions", typeof (Int32));
            createCol(items, "basePermissions", typeof (Int32));
            createCol(items, "everyonePermissions", typeof (Int32));
            createCol(items, "groupPermissions", typeof (Int32));
            createCol(items, "flags", typeof (Int32));

            items.PrimaryKey = new DataColumn[] {items.Columns["itemID"]};

            return items;
        }

        /***********************************************************************
         *
         *  Convert between ADO.NET <=> OpenSim Objects
         *
         *  These should be database independant
         *
         **********************************************************************/

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private SceneObjectPart buildPrim(DataRow row)
        {
            SceneObjectPart prim = new SceneObjectPart();
            prim.UUID = new LLUUID((String) row["UUID"]);
            // explicit conversion of integers is required, which sort
            // of sucks.  No idea if there is a shortcut here or not.
            prim.ParentID = Convert.ToUInt32(row["ParentID"]);
            prim.CreationDate = Convert.ToInt32(row["CreationDate"]);
            prim.Name = (String) row["Name"];
            // various text fields
            prim.Text = (String) row["Text"];
            prim.Description = (String) row["Description"];
            prim.SitName = (String) row["SitName"];
            prim.TouchName = (String) row["TouchName"];
            // permissions
            prim.ObjectFlags = Convert.ToUInt32(row["ObjectFlags"]);
            prim.CreatorID = new LLUUID((String) row["CreatorID"]);
            prim.OwnerID = new LLUUID((String) row["OwnerID"]);
            prim.GroupID = new LLUUID((String) row["GroupID"]);
            prim.LastOwnerID = new LLUUID((String) row["LastOwnerID"]);
            prim.OwnerMask = Convert.ToUInt32(row["OwnerMask"]);
            prim.NextOwnerMask = Convert.ToUInt32(row["NextOwnerMask"]);
            prim.GroupMask = Convert.ToUInt32(row["GroupMask"]);
            prim.EveryoneMask = Convert.ToUInt32(row["EveryoneMask"]);
            prim.BaseMask = Convert.ToUInt32(row["BaseMask"]);
            // vectors
            prim.OffsetPosition = new LLVector3(
                Convert.ToSingle(row["PositionX"]),
                Convert.ToSingle(row["PositionY"]),
                Convert.ToSingle(row["PositionZ"])
                );
            prim.GroupPosition = new LLVector3(
                Convert.ToSingle(row["GroupPositionX"]),
                Convert.ToSingle(row["GroupPositionY"]),
                Convert.ToSingle(row["GroupPositionZ"])
                );
            prim.Velocity = new LLVector3(
                Convert.ToSingle(row["VelocityX"]),
                Convert.ToSingle(row["VelocityY"]),
                Convert.ToSingle(row["VelocityZ"])
                );
            prim.AngularVelocity = new LLVector3(
                Convert.ToSingle(row["AngularVelocityX"]),
                Convert.ToSingle(row["AngularVelocityY"]),
                Convert.ToSingle(row["AngularVelocityZ"])
                );
            prim.Acceleration = new LLVector3(
                Convert.ToSingle(row["AccelerationX"]),
                Convert.ToSingle(row["AccelerationY"]),
                Convert.ToSingle(row["AccelerationZ"])
                );
            // quaternions
            prim.RotationOffset = new LLQuaternion(
                Convert.ToSingle(row["RotationX"]),
                Convert.ToSingle(row["RotationY"]),
                Convert.ToSingle(row["RotationZ"]),
                Convert.ToSingle(row["RotationW"])
                );
            try
            {
                prim.SitTargetPositionLL = new LLVector3(
                                                         Convert.ToSingle(row["SitTargetOffsetX"]),
                                                         Convert.ToSingle(row["SitTargetOffsetY"]),
                                                         Convert.ToSingle(row["SitTargetOffsetZ"]));
                prim.SitTargetOrientationLL = new LLQuaternion(
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientX"]),
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientY"]),
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientZ"]),
                                                               Convert.ToSingle(
                                                                                row["SitTargetOrientW"]));
            }
            catch (InvalidCastException)
            {
                // Database table was created before we got here and needs to be created! :P

                lock (m_dataSet)
                {
                    using (
                        MySqlCommand cmd =
                            new MySqlCommand(
                                "ALTER TABLE `prims` ADD COLUMN `SitTargetOffsetX` float NOT NULL default 0,  ADD COLUMN `SitTargetOffsetY` float NOT NULL default 0, ADD COLUMN `SitTargetOffsetZ` float NOT NULL default 0, ADD COLUMN `SitTargetOrientW` float NOT NULL default 0, ADD COLUMN `SitTargetOrientX` float NOT NULL default 0, ADD COLUMN `SitTargetOrientY` float NOT NULL default 0, ADD COLUMN `SitTargetOrientZ` float NOT NULL default 0;",
                                m_connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return prim;
        }


        /// <summary>
        /// Build a prim inventory item from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static TaskInventoryItem buildItem(DataRow row)
        {
            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ItemID        = new LLUUID((String)row["itemID"]);
            taskItem.ParentPartID  = new LLUUID((String)row["primID"]);
            taskItem.AssetID       = new LLUUID((String)row["assetID"]);
            taskItem.ParentID      = new LLUUID((String)row["parentFolderID"]);

            taskItem.InvType       = Convert.ToInt32(row["invType"]);
            taskItem.Type          = Convert.ToInt32(row["assetType"]);

            taskItem.Name          = (String)row["name"];
            taskItem.Description   = (String)row["description"];
            taskItem.CreationDate  = Convert.ToUInt32(row["creationDate"]);
            taskItem.CreatorID     = new LLUUID((String)row["creatorID"]);
            taskItem.OwnerID       = new LLUUID((String)row["ownerID"]);
            taskItem.LastOwnerID   = new LLUUID((String)row["lastOwnerID"]);
            taskItem.GroupID       = new LLUUID((String)row["groupID"]);

            taskItem.NextPermissions = Convert.ToUInt32(row["nextPermissions"]);
            taskItem.CurrentPermissions     = Convert.ToUInt32(row["currentPermissions"]);
            taskItem.BasePermissions      = Convert.ToUInt32(row["basePermissions"]);
            taskItem.EveryonePermissions  = Convert.ToUInt32(row["everyonePermissions"]);
            taskItem.GroupPermissions     = Convert.ToUInt32(row["groupPermissions"]);
            taskItem.Flags         = Convert.ToUInt32(row["flags"]);

            return taskItem;
        }

        private static RegionSettings buildRegionSettings(DataRow row)
        {
            RegionSettings newSettings = new RegionSettings();

            newSettings.RegionUUID = new LLUUID((string) row["regionUUID"]);
            newSettings.BlockTerraform = Convert.ToBoolean(row["block_terraform"]);
            newSettings.AllowDamage = Convert.ToBoolean(row["allow_damage"]);
            newSettings.BlockFly = Convert.ToBoolean(row["block_fly"]);
            newSettings.RestrictPushing = Convert.ToBoolean(row["restrict_pushing"]);
            newSettings.AllowLandResell = Convert.ToBoolean(row["allow_land_resell"]);
            newSettings.AllowLandJoinDivide = Convert.ToBoolean(row["allow_land_join_divide"]);
            newSettings.BlockShowInSearch = Convert.ToBoolean(row["block_show_in_search"]);
            newSettings.AgentLimit = Convert.ToInt32(row["agent_limit"]);
            newSettings.ObjectBonus = Convert.ToDouble(row["object_bonus"]);
            newSettings.Maturity = Convert.ToInt32(row["maturity"]);
            newSettings.DisableScripts = Convert.ToBoolean(row["disable_scripts"]);
            newSettings.DisableCollisions = Convert.ToBoolean(row["disable_collisions"]);
            newSettings.DisablePhysics = Convert.ToBoolean(row["disable_physics"]);
            newSettings.TerrainTexture1 = new LLUUID((String) row["terrain_texture_1"]);
            newSettings.TerrainTexture2 = new LLUUID((String) row["terrain_texture_2"]);
            newSettings.TerrainTexture3 = new LLUUID((String) row["terrain_texture_3"]);
            newSettings.TerrainTexture4 = new LLUUID((String) row["terrain_texture_4"]);
            newSettings.Elevation1NW = Convert.ToDouble(row["elevation_1_nw"]);
            newSettings.Elevation2NW = Convert.ToDouble(row["elevation_2_nw"]);
            newSettings.Elevation1NE = Convert.ToDouble(row["elevation_1_ne"]);
            newSettings.Elevation2NE = Convert.ToDouble(row["elevation_2_ne"]);
            newSettings.Elevation1SE = Convert.ToDouble(row["elevation_1_se"]);
            newSettings.Elevation2SE = Convert.ToDouble(row["elevation_2_se"]);
            newSettings.Elevation1SW = Convert.ToDouble(row["elevation_1_sw"]);
            newSettings.Elevation2SW = Convert.ToDouble(row["elevation_2_sw"]);
            newSettings.WaterHeight = Convert.ToDouble(row["water_height"]);
            newSettings.TerrainRaiseLimit = Convert.ToDouble(row["terrain_raise_limit"]);
            newSettings.TerrainLowerLimit = Convert.ToDouble(row["terrain_lower_limit"]);
            newSettings.UseEstateSun = Convert.ToBoolean(row["use_estate_sun"]);
            newSettings.Sandbox = Convert.ToBoolean(row["sandbox"]);
            newSettings.FixedSun = Convert.ToBoolean(row["fixed_sun"]);
            newSettings.SunPosition = Convert.ToDouble(row["sun_position"]);
            newSettings.Covenant = new LLUUID((String) row["covenant"]);

            return newSettings;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static LandData buildLandData(DataRow row)
        {
            LandData newData = new LandData();

            newData.GlobalID = new LLUUID((String) row["UUID"]);
            newData.LocalID = Convert.ToInt32(row["LocalLandID"]);

            // Bitmap is a byte[512]
            newData.Bitmap = (Byte[]) row["Bitmap"];

            newData.Name = (String) row["Name"];
            newData.Description = (String) row["Description"];
            newData.OwnerID = (String) row["OwnerUUID"];
            newData.IsGroupOwned = Convert.ToBoolean(row["IsGroupOwned"]);
            newData.Area = Convert.ToInt32(row["Area"]);
            newData.AuctionID = Convert.ToUInt32(row["AuctionID"]); //Unemplemented
            newData.Category = (Parcel.ParcelCategory) Convert.ToInt32(row["Category"]);
                //Enum libsecondlife.Parcel.ParcelCategory
            newData.ClaimDate = Convert.ToInt32(row["ClaimDate"]);
            newData.ClaimPrice = Convert.ToInt32(row["ClaimPrice"]);
            newData.GroupID = new LLUUID((String) row["GroupUUID"]);
            newData.SalePrice = Convert.ToInt32(row["SalePrice"]);
            newData.Status = (Parcel.ParcelStatus) Convert.ToInt32(row["LandStatus"]);
                //Enum. libsecondlife.Parcel.ParcelStatus
            newData.Flags = Convert.ToUInt32(row["LandFlags"]);
            newData.LandingType = Convert.ToByte(row["LandingType"]);
            newData.MediaAutoScale = Convert.ToByte(row["MediaAutoScale"]);
            newData.MediaID = new LLUUID((String) row["MediaTextureUUID"]);
            newData.MediaURL = (String) row["MediaURL"];
            newData.MusicURL = (String) row["MusicURL"];
            newData.PassHours = Convert.ToSingle(row["PassHours"]);
            newData.PassPrice = Convert.ToInt32(row["PassPrice"]);
            LLUUID authedbuyer = LLUUID.Zero;
            LLUUID snapshotID = LLUUID.Zero;

            Helpers.TryParse((string)row["AuthBuyerID"], out authedbuyer);
            Helpers.TryParse((string)row["SnapshotUUID"], out snapshotID);

            newData.AuthBuyerID = authedbuyer;
            newData.SnapshotID = snapshotID;
            try
            {
                newData.UserLocation =
                    new LLVector3(Convert.ToSingle(row["UserLocationX"]), Convert.ToSingle(row["UserLocationY"]),
                                  Convert.ToSingle(row["UserLocationZ"]));
                newData.UserLookAt =
                    new LLVector3(Convert.ToSingle(row["UserLookAtX"]), Convert.ToSingle(row["UserLookAtY"]),
                                  Convert.ToSingle(row["UserLookAtZ"]));
            }
            catch (InvalidCastException)
            {
                newData.UserLocation = LLVector3.Zero;
                newData.UserLookAt = LLVector3.Zero;
                m_log.ErrorFormat("[PARCEL]: unable to get parcel telehub settings for {1}", newData.Name);
            }

            newData.ParcelAccessList = new List<ParcelManager.ParcelAccessEntry>();

            return newData;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static ParcelManager.ParcelAccessEntry buildLandAccessData(DataRow row)
        {
            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = new LLUUID((string) row["AccessUUID"]);
            entry.Flags = (ParcelManager.AccessList) Convert.ToInt32(row["Flags"]);
            entry.Time = new DateTime();
            return entry;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private static Array serializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream(65536*sizeof (double));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < 256; x++)
                for (int y = 0; y < 256; y++)
                {
                    double height = val[x, y];
                    if (height == 0.0)
                        height = double.Epsilon;

                    bw.Write(height);
                }

            return str.ToArray();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="prim"></param>
        /// <param name="sceneGroupID"></param>
        /// <param name="regionUUID"></param>
        private void fillPrimRow(DataRow row, SceneObjectPart prim, LLUUID sceneGroupID, LLUUID regionUUID)
        {
            row["UUID"] = Util.ToRawUuidString(prim.UUID);
            row["RegionUUID"] = Util.ToRawUuidString(regionUUID);
            row["ParentID"] = prim.ParentID;
            row["CreationDate"] = prim.CreationDate;
            row["Name"] = prim.Name;
            row["SceneGroupID"] = Util.ToRawUuidString(sceneGroupID);
                // the UUID of the root part for this SceneObjectGroup
            // various text fields
            row["Text"] = prim.Text;
            row["Description"] = prim.Description;
            row["SitName"] = prim.SitName;
            row["TouchName"] = prim.TouchName;
            // permissions
            row["ObjectFlags"] = prim.ObjectFlags;
            row["CreatorID"] = Util.ToRawUuidString(prim.CreatorID);
            row["OwnerID"] = Util.ToRawUuidString(prim.OwnerID);
            row["GroupID"] = Util.ToRawUuidString(prim.GroupID);
            row["LastOwnerID"] = Util.ToRawUuidString(prim.LastOwnerID);
            row["OwnerMask"] = prim.OwnerMask;
            row["NextOwnerMask"] = prim.NextOwnerMask;
            row["GroupMask"] = prim.GroupMask;
            row["EveryoneMask"] = prim.EveryoneMask;
            row["BaseMask"] = prim.BaseMask;
            // vectors
            row["PositionX"] = prim.OffsetPosition.X;
            row["PositionY"] = prim.OffsetPosition.Y;
            row["PositionZ"] = prim.OffsetPosition.Z;
            row["GroupPositionX"] = prim.GroupPosition.X;
            row["GroupPositionY"] = prim.GroupPosition.Y;
            row["GroupPositionZ"] = prim.GroupPosition.Z;
            row["VelocityX"] = prim.Velocity.X;
            row["VelocityY"] = prim.Velocity.Y;
            row["VelocityZ"] = prim.Velocity.Z;
            row["AngularVelocityX"] = prim.AngularVelocity.X;
            row["AngularVelocityY"] = prim.AngularVelocity.Y;
            row["AngularVelocityZ"] = prim.AngularVelocity.Z;
            row["AccelerationX"] = prim.Acceleration.X;
            row["AccelerationY"] = prim.Acceleration.Y;
            row["AccelerationZ"] = prim.Acceleration.Z;
            // quaternions
            row["RotationX"] = prim.RotationOffset.X;
            row["RotationY"] = prim.RotationOffset.Y;
            row["RotationZ"] = prim.RotationOffset.Z;
            row["RotationW"] = prim.RotationOffset.W;

            try
            {
                // Sit target
                LLVector3 sitTargetPos = prim.SitTargetPositionLL;
                row["SitTargetOffsetX"] = sitTargetPos.X;
                row["SitTargetOffsetY"] = sitTargetPos.Y;
                row["SitTargetOffsetZ"] = sitTargetPos.Z;

                LLQuaternion sitTargetOrient = prim.SitTargetOrientationLL;
                row["SitTargetOrientW"] = sitTargetOrient.W;
                row["SitTargetOrientX"] = sitTargetOrient.X;
                row["SitTargetOrientY"] = sitTargetOrient.Y;
                row["SitTargetOrientZ"] = sitTargetOrient.Z;
            }
            catch (MySqlException)
            {
                // Database table was created before we got here and needs to be created! :P

                using (
                    MySqlCommand cmd =
                        new MySqlCommand(
                            "ALTER TABLE `prims` ADD COLUMN `SitTargetOffsetX` float NOT NULL default 0,  ADD COLUMN `SitTargetOffsetY` float NOT NULL default 0, ADD COLUMN `SitTargetOffsetZ` float NOT NULL default 0, ADD COLUMN `SitTargetOrientW` float NOT NULL default 0, ADD COLUMN `SitTargetOrientX` float NOT NULL default 0, ADD COLUMN `SitTargetOrientY` float NOT NULL default 0, ADD COLUMN `SitTargetOrientZ` float NOT NULL default 0;",
                            m_connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="taskItem"></param>
        private static void fillItemRow(DataRow row, TaskInventoryItem taskItem)
        {
            row["itemID"] = taskItem.ItemID;
            row["primID"] = taskItem.ParentPartID;
            row["assetID"] = taskItem.AssetID;
            row["parentFolderID"] = taskItem.ParentID;

            row["invType"] = taskItem.InvType;
            row["assetType"] = taskItem.Type;

            row["name"] = taskItem.Name;
            row["description"] = taskItem.Description;
            row["creationDate"] = taskItem.CreationDate;
            row["creatorID"] = taskItem.CreatorID;
            row["ownerID"] = taskItem.OwnerID;
            row["lastOwnerID"] = taskItem.LastOwnerID;
            row["groupID"] = taskItem.GroupID;
            row["nextPermissions"] = taskItem.NextPermissions;
            row["currentPermissions"] = taskItem.CurrentPermissions;
            row["basePermissions"] = taskItem.BasePermissions;
            row["everyonePermissions"] = taskItem.EveryonePermissions;
            row["groupPermissions"] = taskItem.GroupPermissions;
            row["flags"] = taskItem.Flags;
        }

        /// <summary>
        ///
        /// </summary>
        private static void fillRegionSettingsRow(DataRow row, RegionSettings settings)
        {
            row["regionUUID"] = settings.RegionUUID.ToString();
            row["block_terraform"] = settings.BlockTerraform;
            row["block_fly"] = settings.BlockFly;
            row["allow_damage"] = settings.AllowDamage;
            row["restrict_pushing"] = settings.RestrictPushing;
            row["allow_land_resell"] = settings.AllowLandResell;
            row["allow_land_join_divide"] = settings.AllowLandJoinDivide;
            row["block_show_in_search"] = settings.BlockShowInSearch;
            row["agent_limit"] = settings.AgentLimit;
            row["object_bonus"] = settings.ObjectBonus;
            row["maturity"] = settings.Maturity;
            row["disable_scripts"] = settings.DisableScripts;
            row["disable_collisions"] = settings.DisableCollisions;
            row["disable_physics"] = settings.DisablePhysics;
            row["terrain_texture_1"] = settings.TerrainTexture1.ToString();
            row["terrain_texture_2"] = settings.TerrainTexture2.ToString();
            row["terrain_texture_3"] = settings.TerrainTexture3.ToString();
            row["terrain_texture_4"] = settings.TerrainTexture4.ToString();
            row["elevation_1_nw"] = settings.Elevation1NW;
            row["elevation_2_nw"] = settings.Elevation2NW;
            row["elevation_1_ne"] = settings.Elevation1NE;
            row["elevation_2_ne"] = settings.Elevation2NE;
            row["elevation_1_se"] = settings.Elevation1SE;
            row["elevation_2_se"] = settings.Elevation2SE;
            row["elevation_1_sw"] = settings.Elevation1SW;
            row["elevation_2_sw"] = settings.Elevation2SW;
            row["water_height"] = settings.WaterHeight;
            row["terrain_raise_limit"] = settings.TerrainRaiseLimit;
            row["terrain_lower_limit"] = settings.TerrainLowerLimit;
            row["use_estate_sun"] = settings.UseEstateSun;
            row["sandbox"] = settings.Sandbox;
            row["fixed_sun"] = settings.FixedSun;
            row["sun_position"] = settings.SunPosition;
            row["covenant"] = settings.Covenant.ToString();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="land"></param>
        /// <param name="regionUUID"></param>
        private static void fillLandRow(DataRow row, LandData land, LLUUID regionUUID)
        {
            row["UUID"] = Util.ToRawUuidString(land.GlobalID);
            row["RegionUUID"] = Util.ToRawUuidString(regionUUID);
            row["LocalLandID"] = land.LocalID;

            // Bitmap is a byte[512]
            row["Bitmap"] = land.Bitmap;

            row["Name"] = land.Name;
            row["Description"] = land.Description;
            row["OwnerUUID"] = Util.ToRawUuidString(land.OwnerID);
            row["IsGroupOwned"] = land.IsGroupOwned;
            row["Area"] = land.Area;
            row["AuctionID"] = land.AuctionID; //Unemplemented
            row["Category"] = land.Category; //Enum libsecondlife.Parcel.ParcelCategory
            row["ClaimDate"] = land.ClaimDate;
            row["ClaimPrice"] = land.ClaimPrice;
            row["GroupUUID"] = Util.ToRawUuidString(land.GroupID);
            row["SalePrice"] = land.SalePrice;
            row["LandStatus"] = land.Status; //Enum. libsecondlife.Parcel.ParcelStatus
            row["LandFlags"] = land.Flags;
            row["LandingType"] = land.LandingType;
            row["MediaAutoScale"] = land.MediaAutoScale;
            row["MediaTextureUUID"] = Util.ToRawUuidString(land.MediaID);
            row["MediaURL"] = land.MediaURL;
            row["MusicURL"] = land.MusicURL;
            row["PassHours"] = land.PassHours;
            row["PassPrice"] = land.PassPrice;
            row["SnapshotUUID"] = Util.ToRawUuidString(land.SnapshotID);
            row["UserLocationX"] = land.UserLocation.X;
            row["UserLocationY"] = land.UserLocation.Y;
            row["UserLocationZ"] = land.UserLocation.Z;
            row["UserLookAtX"] = land.UserLookAt.X;
            row["UserLookAtY"] = land.UserLookAt.Y;
            row["UserLookAtZ"] = land.UserLookAt.Z;
            row["AuthBuyerID"] = land.AuthBuyerID;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="entry"></param>
        /// <param name="parcelID"></param>
        private static void fillLandAccessRow(DataRow row, ParcelManager.ParcelAccessEntry entry, LLUUID parcelID)
        {
            row["LandUUID"] = Util.ToRawUuidString(parcelID);
            row["AccessUUID"] = Util.ToRawUuidString(entry.AgentID);
            row["Flags"] = entry.Flags;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private PrimitiveBaseShape buildShape(DataRow row)
        {
            PrimitiveBaseShape s = new PrimitiveBaseShape();
            s.Scale = new LLVector3(
                Convert.ToSingle(row["ScaleX"]),
                Convert.ToSingle(row["ScaleY"]),
                Convert.ToSingle(row["ScaleZ"])
                );
            // paths
            s.PCode = Convert.ToByte(row["PCode"]);
            s.PathBegin = Convert.ToUInt16(row["PathBegin"]);
            s.PathEnd = Convert.ToUInt16(row["PathEnd"]);
            s.PathScaleX = Convert.ToByte(row["PathScaleX"]);
            s.PathScaleY = Convert.ToByte(row["PathScaleY"]);
            s.PathShearX = Convert.ToByte(row["PathShearX"]);
            s.PathShearY = Convert.ToByte(row["PathShearY"]);
            s.PathSkew = Convert.ToSByte(row["PathSkew"]);
            s.PathCurve = Convert.ToByte(row["PathCurve"]);
            s.PathRadiusOffset = Convert.ToSByte(row["PathRadiusOffset"]);
            s.PathRevolutions = Convert.ToByte(row["PathRevolutions"]);
            s.PathTaperX = Convert.ToSByte(row["PathTaperX"]);
            s.PathTaperY = Convert.ToSByte(row["PathTaperY"]);
            s.PathTwist = Convert.ToSByte(row["PathTwist"]);
            s.PathTwistBegin = Convert.ToSByte(row["PathTwistBegin"]);
            // profile
            s.ProfileBegin = Convert.ToUInt16(row["ProfileBegin"]);
            s.ProfileEnd = Convert.ToUInt16(row["ProfileEnd"]);
            s.ProfileCurve = Convert.ToByte(row["ProfileCurve"]);
            s.ProfileHollow = Convert.ToUInt16(row["ProfileHollow"]);

            byte[] textureEntry = (byte[]) row["Texture"];
            s.TextureEntry = textureEntry;

            s.ExtraParams = (byte[]) row["ExtraParams"];

            try
            {
                s.State = Convert.ToByte(row["State"]);
            }
            catch (InvalidCastException)
            {
                // Database table was created before we got here and needs to be created! :P
                lock (m_dataSet)
                {
                    using (
                        MySqlCommand cmd =
                            new MySqlCommand(
                                "ALTER TABLE `primshapes` ADD COLUMN `State` int NOT NULL default 0;",
                                m_connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            return s;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="row"></param>
        /// <param name="prim"></param>
        private void fillShapeRow(DataRow row, SceneObjectPart prim)
        {
            PrimitiveBaseShape s = prim.Shape;
            row["UUID"] = Util.ToRawUuidString(prim.UUID);
            // shape is an enum
            row["Shape"] = 0;
            // vectors
            row["ScaleX"] = s.Scale.X;
            row["ScaleY"] = s.Scale.Y;
            row["ScaleZ"] = s.Scale.Z;
            // paths
            row["PCode"] = s.PCode;
            row["PathBegin"] = s.PathBegin;
            row["PathEnd"] = s.PathEnd;
            row["PathScaleX"] = s.PathScaleX;
            row["PathScaleY"] = s.PathScaleY;
            row["PathShearX"] = s.PathShearX;
            row["PathShearY"] = s.PathShearY;
            row["PathSkew"] = s.PathSkew;
            row["PathCurve"] = s.PathCurve;
            row["PathRadiusOffset"] = s.PathRadiusOffset;
            row["PathRevolutions"] = s.PathRevolutions;
            row["PathTaperX"] = s.PathTaperX;
            row["PathTaperY"] = s.PathTaperY;
            row["PathTwist"] = s.PathTwist;
            row["PathTwistBegin"] = s.PathTwistBegin;
            // profile
            row["ProfileBegin"] = s.ProfileBegin;
            row["ProfileEnd"] = s.ProfileEnd;
            row["ProfileCurve"] = s.ProfileCurve;
            row["ProfileHollow"] = s.ProfileHollow;
            row["Texture"] = s.TextureEntry;
            row["ExtraParams"] = s.ExtraParams;

            try
            {
                row["State"] = s.State;
            }
            catch (MySqlException)
            {
                lock (m_dataSet)
                {
                    // Database table was created before we got here and needs to be created! :P
                    using (
                        MySqlCommand cmd =
                            new MySqlCommand(
                                "ALTER TABLE `primshapes` ADD COLUMN `State` int NOT NULL default 0;",
                                m_connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="prim"></param>
        /// <param name="sceneGroupID"></param>
        /// <param name="regionUUID"></param>
        private void addPrim(SceneObjectPart prim, LLUUID sceneGroupID, LLUUID regionUUID)
        {
            lock (m_dataSet)
            {
                DataTable prims = m_dataSet.Tables["prims"];
                DataTable shapes = m_dataSet.Tables["primshapes"];

                DataRow primRow = prims.Rows.Find(Util.ToRawUuidString(prim.UUID));
                if (primRow == null)
                {
                    primRow = prims.NewRow();
                    fillPrimRow(primRow, prim, sceneGroupID, regionUUID);
                    prims.Rows.Add(primRow);
                }
                else
                {
                    fillPrimRow(primRow, prim, sceneGroupID, regionUUID);
                }

                DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
                if (shapeRow == null)
                {
                    shapeRow = shapes.NewRow();
                    fillShapeRow(shapeRow, prim);
                    shapes.Rows.Add(shapeRow);
                }
                else
                {
                    fillShapeRow(shapeRow, prim);
                }
            }
        }

        /// <summary>
        /// see IRegionDatastore
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="items"></param>
        public void StorePrimInventory(LLUUID primID, ICollection<TaskInventoryItem> items)
        {
            m_log.InfoFormat("[REGION DB]: Persisting Prim Inventory with prim ID {0}", primID);

            // For now, we're just going to crudely remove all the previous inventory items
            // no matter whether they have changed or not, and replace them with the current set.
            lock (m_dataSet)
            {
                RemoveItems(primID);

                // repalce with current inventory details
                foreach (TaskInventoryItem newItem in items)
                {
//                    m_log.InfoFormat(
//                        "[REGION DB]: " +
//                        "Adding item {0}, {1} to prim ID {2}",
//                        newItem.Name, newItem.ItemID, newItem.ParentPartID);

                    DataRow newItemRow = m_itemsTable.NewRow();
                    fillItemRow(newItemRow, newItem);
                    m_itemsTable.Rows.Add(newItemRow);
                }
            }

            Commit();
        }

        /***********************************************************************
         *
         *  SQL Statement Creation Functions
         *
         *  These functions create SQL statements for update, insert, and create.
         *  They can probably be factored later to have a db independant
         *  portion and a db specific portion
         *
         **********************************************************************/

        /// <summary>
        /// Create a MySQL insert command
        /// </summary>
        /// <param name="table"></param>
        /// <param name="dt"></param>
        /// <returns></returns>
        /// <remarks>
        /// This is subtle enough to deserve some commentary.
        /// Instead of doing *lots* and *lots of hardcoded strings
        /// for database definitions we'll use the fact that
        /// realistically all insert statements look like "insert
        /// into A(b, c) values(:b, :c) on the parameterized query
        /// front.  If we just have a list of b, c, etc... we can
        /// generate these strings instead of typing them out.
        /// </remarks>
        private static MySqlCommand createInsertCommand(string table, DataTable dt)
        {

            string[] cols = new string[dt.Columns.Count];
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                DataColumn col = dt.Columns[i];
                cols[i] = col.ColumnName;
            }

            string sql = "insert into " + table + "(";
            sql += String.Join(", ", cols);
            // important, the first ':' needs to be here, the rest get added in the join
            sql += ") values (?";
            sql += String.Join(", ?", cols);
            sql += ")";
            MySqlCommand cmd = new MySqlCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be
            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createMySqlParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }

        /// <summary>
        /// Create a MySQL update command
        /// </summary>
        /// <param name="table"></param>
        /// <param name="pk"></param>
        /// <param name="dt"></param>
        /// <returns></returns>
        private static MySqlCommand createUpdateCommand(string table, string pk, DataTable dt)
        {
            string sql = "update " + table + " set ";
            string subsql = String.Empty;
            foreach (DataColumn col in dt.Columns)
            {
                if (subsql.Length > 0)
                {
                    // a map function would rock so much here
                    subsql += ", ";
                }
                subsql += col.ColumnName + "=?" + col.ColumnName;
            }
            sql += subsql;
            sql += " where " + pk;
            MySqlCommand cmd = new MySqlCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be

            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createMySqlParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        // private static string defineTable(DataTable dt)
        // {
        //     string sql = "create table " + dt.TableName + "(";
        //     string subsql = String.Empty;
        //     foreach (DataColumn col in dt.Columns)
        //     {
        //         if (subsql.Length > 0)
        //         {
        //             // a map function would rock so much here
        //             subsql += ",\n";
        //         }
        //         subsql += col.ColumnName + " " + MySqlType(col.DataType);
        //         if (dt.PrimaryKey.Length > 0 && col == dt.PrimaryKey[0])
        //         {
        //             subsql += " primary key";
        //         }
        //     }
        //     sql += subsql;
        //     sql += ")";

        //     //m_log.InfoFormat("[DATASTORE]: defineTable() sql {0}", sql);

        //     return sql;
        // }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        ///<summary>
        /// <para>This is a convenience function that collapses 5 repetitive
        /// lines for defining MySqlParameters to 2 parameters:
        /// column name and database type.
        /// </para>
        /// <para>
        /// It assumes certain conventions like ?param as the param
        /// name to replace in parametrized queries, and that source
        /// version is always current version, both of which are fine
        /// for us.
        /// </para>
        /// </summary>
        /// <returns>a built MySql parameter</returns>
        private static MySqlParameter createMySqlParameter(string name, Type type)
        {
            MySqlParameter param = new MySqlParameter();
            param.ParameterName = "?" + name;
            param.DbType = dbtypeFromType(type);
            param.SourceColumn = name;
            param.SourceVersion = DataRowVersion.Current;
            return param;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void SetupPrimCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            MySqlCommand insertCommand = createInsertCommand("prims", m_primTable);
            insertCommand.Connection = conn;
            da.InsertCommand = insertCommand;

            MySqlCommand updateCommand = createUpdateCommand("prims", "UUID=?UUID", m_primTable);
            updateCommand.Connection = conn;
            da.UpdateCommand = updateCommand;

            MySqlCommand delete = new MySqlCommand("delete from prims where UUID=?UUID");
            delete.Parameters.Add(createMySqlParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void SetupItemsCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("primitems", m_itemsTable);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primitems", "itemID = ?itemID", m_itemsTable);
            da.UpdateCommand.Connection = conn;

            MySqlCommand delete = new MySqlCommand("delete from primitems where itemID = ?itemID");
            delete.Parameters.Add(createMySqlParameter("itemID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void SetupRegionSettingsCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("regionsettings", m_regionSettingsTable);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("regionsettings", "regionUUID = ?regionUUID", m_regionSettingsTable);
            da.UpdateCommand.Connection = conn;

            MySqlCommand delete = new MySqlCommand("delete from regionsettings where regionUUID = ?regionUUID");
            delete.Parameters.Add(createMySqlParameter("regionUUID", typeof(String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void SetupTerrainCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("terrain", m_dataSet.Tables["terrain"]);
            da.InsertCommand.Connection = conn;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupLandCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("land", m_dataSet.Tables["land"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("land", "UUID=?UUID", m_dataSet.Tables["land"]);
            da.UpdateCommand.Connection = conn;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void setupLandAccessCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("landaccesslist", m_dataSet.Tables["landaccesslist"]);
            da.InsertCommand.Connection = conn;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="da"></param>
        /// <param name="conn"></param>
        private void SetupShapeCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("primshapes", m_dataSet.Tables["primshapes"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primshapes", "UUID=?UUID", m_dataSet.Tables["primshapes"]);
            da.UpdateCommand.Connection = conn;

            MySqlCommand delete = new MySqlCommand("delete from primshapes where UUID = ?UUID");
            delete.Parameters.Add(createMySqlParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="conn">MySQL connection handler</param>
        // private static void InitDB(MySqlConnection conn)
        // {
        //     string createPrims = defineTable(createPrimTable());
        //     string createShapes = defineTable(createShapeTable());
        //     string createItems = defineTable(createItemsTable());
        //     string createTerrain = defineTable(createTerrainTable());

        //     // Land table is created from the Versionable Test Table routine now.
        //     //string createLand = defineTable(createLandTable());
        //     string createLandAccessList = defineTable(createLandAccessListTable());

        //     MySqlCommand pcmd = new MySqlCommand(createPrims, conn);
        //     MySqlCommand scmd = new MySqlCommand(createShapes, conn);
        //     MySqlCommand icmd = new MySqlCommand(createItems, conn);
        //     MySqlCommand tcmd = new MySqlCommand(createTerrain, conn);
        //     //MySqlCommand lcmd = new MySqlCommand(createLand, conn);
        //     MySqlCommand lalcmd = new MySqlCommand(createLandAccessList, conn);

        //     if (conn.State != ConnectionState.Open)
        //     {
        //         try
        //         {
        //             conn.Open();
        //         }
        //         catch (Exception ex)
        //         {
        //             m_log.Error("[REGION DB]: Error connecting to MySQL server: " + ex.Message);
        //             m_log.Error("[REGION DB]: Application is terminating!");
        //             Thread.CurrentThread.Abort();
        //         }
        //     }

        //     try
        //     {
        //         pcmd.ExecuteNonQuery();
        //     }
        //     catch (MySqlException e)
        //     {
        //         m_log.WarnFormat("[REGION DB]: Primitives Table Already Exists: {0}", e);
        //     }

        //     try
        //     {
        //         scmd.ExecuteNonQuery();
        //     }
        //     catch (MySqlException e)
        //     {
        //         m_log.WarnFormat("[REGION DB]: Shapes Table Already Exists: {0}", e);
        //     }

        //     try
        //     {
        //         icmd.ExecuteNonQuery();
        //     }
        //     catch (MySqlException e)
        //     {
        //         m_log.WarnFormat("[REGION DB]: Items Table Already Exists: {0}", e);
        //     }

        //     try
        //     {
        //         tcmd.ExecuteNonQuery();
        //     }
        //     catch (MySqlException e)
        //     {
        //         m_log.WarnFormat("[REGION DB]: Terrain Table Already Exists: {0}", e);
        //     }

        //     //try
        //     //{
        //         //lcmd.ExecuteNonQuery();
        //     //}
        //     //catch (MySqlException e)
        //     //{
        //         //m_log.WarnFormat("[MySql]: Land Table Already Exists: {0}", e);
        //     //}

        //     try
        //     {
        //         lalcmd.ExecuteNonQuery();
        //     }
        //     catch (MySqlException e)
        //     {
        //         m_log.WarnFormat("[REGION DB]: LandAccessList Table Already Exists: {0}", e);
        //     }
        //     conn.Close();
        // }

        /// <summary>
        ///
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="m"></param>
        /// <returns></returns>
        private bool TestTables(MySqlConnection conn, Migration m)
        {
            // we already have migrations, get out of here
            if (m.Version > 0)
                return false;

            MySqlCommand primSelectCmd = new MySqlCommand(m_primSelect, conn);
            MySqlDataAdapter pDa = new MySqlDataAdapter(primSelectCmd);
            MySqlCommand shapeSelectCmd = new MySqlCommand(m_shapeSelect, conn);
            MySqlDataAdapter sDa = new MySqlDataAdapter(shapeSelectCmd);
            MySqlCommand itemsSelectCmd = new MySqlCommand(m_itemsSelect, conn);
            MySqlDataAdapter iDa = new MySqlDataAdapter(itemsSelectCmd);
            MySqlCommand terrainSelectCmd = new MySqlCommand(m_terrainSelect, conn);
            MySqlDataAdapter tDa = new MySqlDataAdapter(terrainSelectCmd);
            MySqlCommand landSelectCmd = new MySqlCommand(m_landSelect, conn);
            MySqlDataAdapter lDa = new MySqlDataAdapter(landSelectCmd);
            MySqlCommand landAccessListSelectCmd = new MySqlCommand(m_landAccessListSelect, conn);
            MySqlDataAdapter lalDa = new MySqlDataAdapter(landAccessListSelectCmd);

            DataSet tmpDS = new DataSet();
            try
            {
                pDa.Fill(tmpDS, "prims");
                sDa.Fill(tmpDS, "primshapes");

                iDa.Fill(tmpDS, "primitems");

                tDa.Fill(tmpDS, "terrain");
                lDa.Fill(tmpDS, "land");
                lalDa.Fill(tmpDS, "landaccesslist");
            }
            catch (MySqlException)
            {
                m_log.Info("[DATASTORE]: MySql Database doesn't exist... creating");
                return false;
            }

            // we have tables, but not a migration model yet
            if (m.Version == 0)
                m.Version = 1;

            return true;

            // pDa.Fill(tmpDS, "prims");
            // sDa.Fill(tmpDS, "primshapes");

            //     iDa.Fill(tmpDS, "primitems");

            // tDa.Fill(tmpDS, "terrain");
            // lDa.Fill(tmpDS, "land");
            // lalDa.Fill(tmpDS, "landaccesslist");

            // foreach (DataColumn col in createPrimTable().Columns)
            // {
            //     if (!tmpDS.Tables["prims"].Columns.Contains(col.ColumnName))
            //     {
            //         m_log.Info("[REGION DB]: Missing required column:" + col.ColumnName);
            //         return false;
            //     }
            // }

            // foreach (DataColumn col in createShapeTable().Columns)
            // {
            //     if (!tmpDS.Tables["primshapes"].Columns.Contains(col.ColumnName))
            //     {
            //         m_log.Info("[REGION DB]: Missing required column:" + col.ColumnName);
            //         return false;
            //     }
            // }

            // // XXX primitems should probably go here eventually

            // foreach (DataColumn col in createTerrainTable().Columns)
            // {
            //     if (!tmpDS.Tables["terrain"].Columns.Contains(col.ColumnName))
            //     {
            //         m_log.Info("[REGION DB]: Missing require column:" + col.ColumnName);
            //         return false;
            //     }
            // }

            // foreach (DataColumn col in createLandTable().Columns)
            // {
            //     if (!tmpDS.Tables["land"].Columns.Contains(col.ColumnName))
            //     {
            //         m_log.Info("[REGION DB]: Missing require column:" + col.ColumnName);
            //         return false;
            //     }
            // }

            // foreach (DataColumn col in createLandAccessListTable().Columns)
            // {
            //     if (!tmpDS.Tables["landaccesslist"].Columns.Contains(col.ColumnName))
            //     {
            //         m_log.Info("[DATASTORE]: Missing require column:" + col.ColumnName);
            //         return false;
            //     }
            // }

            // return true;
        }

        /***********************************************************************
         *
         *  Type conversion functions
         *
         **********************************************************************/

        /// <summary>
        /// Type conversion functions
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static DbType dbtypeFromType(Type type)
        {
            if (type == typeof (String))
            {
                return DbType.String;
            }
            else if (type == typeof (Int32))
            {
                return DbType.Int32;
            }
            else if (type == typeof (Double))
            {
                return DbType.Double;
            }
            else if (type == typeof (Byte))
            {
                return DbType.Byte;
            }
            else if (type == typeof (Double))
            {
                return DbType.Double;
            }
            else if (type == typeof (Byte[]))
            {
                return DbType.Binary;
            }
            else
            {
                return DbType.String;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <remarks>this is something we'll need to implement for each db slightly differently.</remarks>
        // private static string MySqlType(Type type)
        // {
        //     if (type == typeof (String))
        //     {
        //         return "varchar(255)";
        //     }
        //     else if (type == typeof (Int32))
        //     {
        //         return "integer";
        //     }
        //     else if (type == typeof (Int64))
        //     {
        //         return "bigint";
        //     }
        //     else if (type == typeof (Double))
        //     {
        //         return "float";
        //     }
        //     else if (type == typeof (Byte[]))
        //     {
        //         return "longblob";
        //     }
        //     else
        //     {
        //         return "string";
        //     }
        // }
    }
}
