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
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using libsecondlife;
using MySql.Data.MySqlClient;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Framework.Data.MySQL
{
    public class MySQLDataStore : IRegionDataStore
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string m_primSelect = "select * from prims";
        private const string m_shapeSelect = "select * from primshapes";
        private const string m_itemsSelect = "select * from primitems";
        private const string m_terrainSelect = "select * from terrain limit 1";
        private const string m_landSelect = "select * from land";
        private const string m_landAccessListSelect = "select * from landaccesslist";

        private DataSet m_dataSet;
        private MySqlDataAdapter m_primDataAdapter;
        private MySqlDataAdapter m_shapeDataAdapter;
        private MySqlDataAdapter m_itemsDataAdapter;
        private MySqlConnection m_connection;
        private MySqlDataAdapter m_terrainDataAdapter;
        private MySqlDataAdapter m_landDataAdapter;
        private MySqlDataAdapter m_landAccessListDataAdapter;
        
        private DataTable m_primTable;
        private DataTable m_shapeTable;
        private DataTable m_itemsTable;
        private DataTable m_terrainTable;
        private DataTable m_landTable;
        private DataTable m_landAccessListTable;
        
        // Temporary attribute while this is experimental
        private bool persistPrimInventories;        

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        // see IRegionDataStore
        public void Initialise(string connectionstring, bool persistPrimInventories)
        {
            m_dataSet = new DataSet();
            this.persistPrimInventories = persistPrimInventories;

            m_log.Info("[DATASTORE]: MySql - connecting: " + connectionstring);
            m_connection = new MySqlConnection(connectionstring);

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

            TestTables(m_connection);

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
                
                if (persistPrimInventories)
                {
                    m_itemsTable = createItemsTable();
                    m_dataSet.Tables.Add(m_itemsTable);
                    SetupItemsCommands(m_itemsDataAdapter, m_connection);
                    m_itemsDataAdapter.Fill(m_itemsTable);                
                }

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
            }
        }

        public void StoreObject(SceneObjectGroup obj, LLUUID regionUUID)
        {
            lock (m_dataSet)
            {
                foreach (SceneObjectPart prim in obj.Children.Values)
                {
                    if ((prim.ObjectFlags & (uint) LLObject.ObjectFlags.Physics) == 0)
                    {
                        m_log.Info("[DATASTORE]: Adding obj: " + obj.UUID + " to region: " + regionUUID);
                        addPrim(prim, obj.UUID, regionUUID);
                    }
                    else
                    {
                        // m_log.Info("[DATASTORE]: Ignoring Physical obj: " + obj.UUID + " in region: " + regionUUID);
                    }
                }
            }

            Commit();
        }

        public void RemoveObject(LLUUID obj, LLUUID regionUUID)
        {
            m_log.InfoFormat("[DATASTORE]: Removing obj: {0} from region: {1}", obj.UUID, regionUUID);

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
                    
                    if (persistPrimInventories)
                    {                    
                        RemoveItems(uuid);
                    }
                    
                    // Remove prim row
                    row.Delete();
                }
            }

            Commit();
        }

        /// <summary>
        /// Remove all persisted items of the given prim.
        /// The caller must acquire the necessrary synchronization locks and commit or rollback changes.
        /// </summary>
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
                DataRow[] primsForRegion = prims.Select(byRegion, orderByParent);
                m_log.Info("[DATASTORE]: " +
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
                        
                        if (persistPrimInventories)
                        {
                            LoadItems(prim);
                        }                        
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[DATASTORE]: Failed create prim object, exception and data follows");
                        m_log.Info("[DATASTORE]: " + e.ToString());
                        foreach (DataColumn col in prims.Columns)
                        {
                            m_log.Info("[DATASTORE]: Col: " + col.ColumnName + " => " + primRow[col]);
                        }
                    }
                }
            }
            return retvals;
        }
        
        /// <summary>
        /// Load in a prim's persisted inventory.
        /// </summary>
        /// <param name="prim"></param>
        private void LoadItems(SceneObjectPart prim)
        {
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

        public void StoreTerrain(double[,] ter, LLUUID regionID)
        {
            int revision = Util.UnixTimeSinceEpoch();
            m_log.Info("[DATASTORE]: Storing terrain revision r" + revision.ToString());

            DataTable terrain = m_dataSet.Tables["terrain"];
            lock (m_dataSet)
            {
                MySqlCommand cmd = new MySqlCommand("insert into terrain(RegionUUID, Revision, Heightfield)" +
                                                    " values(?RegionUUID, ?Revision, ?Heightfield)", m_connection);
                using (cmd)
                {
                    cmd.Parameters.Add(new MySqlParameter("?RegionUUID", Util.ToRawUuidString(regionID)));
                    cmd.Parameters.Add(new MySqlParameter("?Revision", revision));
                    cmd.Parameters.Add(new MySqlParameter("?Heightfield", serializeTerrain(ter)));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public double[,] LoadTerrain(LLUUID regionID)
        {
            double[,] terret = new double[256,256];
            terret.Initialize();

            MySqlCommand cmd = new MySqlCommand(
                @"select RegionUUID, Revision, Heightfield from terrain
                where RegionUUID=?RegionUUID order by Revision desc limit 1"
                , m_connection);

            MySqlParameter param = new MySqlParameter();
            cmd.Parameters.Add(new MySqlParameter("?RegionUUID", Util.ToRawUuidString(regionID)));

            if (m_connection.State != ConnectionState.Open)
            {
                m_connection.Open();
            }

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
                    m_log.Info("[DATASTORE]: No terrain found for region");
                    return null;
                }

                m_log.Info("[DATASTORE]: Loaded terrain revision r" + rev.ToString());
            }

            return terret;
        }

        public void RemoveLandObject(LLUUID globalID)
        {
            lock (m_dataSet)
            {
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

        static Random rnd = new Random();
        public void StoreLandObject(Land parcel, LLUUID regionUUID)
        {
            // Does the new locking fix it?
            m_log.Info("[DATASTORE]: Tedds temp fix: Waiting 3 seconds to avoid others writing to table while we hold a dataset of it. (Someone please fix! :))");
            System.Threading.Thread.Sleep(2500 + rnd.Next(0, 1000));
            
            lock (m_dataSet)
            {
                DataTable land = m_landTable;
                DataTable landaccesslist = m_landAccessListTable;

                DataRow landRow = land.Rows.Find(Util.ToRawUuidString(parcel.landData.globalID));
                if (landRow == null)
                {
                    landRow = land.NewRow();
                    fillLandRow(landRow, parcel.landData, regionUUID);
                    land.Rows.Add(landRow);
                }
                else
                {
                    fillLandRow(landRow, parcel.landData, regionUUID);
                }

                using (
                    MySqlCommand cmd =
                        new MySqlCommand("delete from landaccesslist where LandUUID=?LandUUID", m_connection))
                {
                    cmd.Parameters.Add(new MySqlParameter("?LandUUID", Util.ToRawUuidString(parcel.landData.globalID)));
                    cmd.ExecuteNonQuery();
                }

                foreach (ParcelManager.ParcelAccessEntry entry in parcel.landData.parcelAccessList)
                {
                    DataRow newAccessRow = landaccesslist.NewRow();
                    fillLandAccessRow(newAccessRow, entry, parcel.landData.globalID);
                    landaccesslist.Rows.Add(newAccessRow);
                }

            }
            Commit();
        }

        public List<LandData> LoadLandObjects(LLUUID regionUUID)
        {
            List<LandData> landDataForRegion = new List<LandData>();
            lock (m_dataSet)
            {
                DataTable land = m_landTable;
                DataTable landaccesslist = m_landAccessListTable;
                string searchExp = "RegionUUID = '" + Util.ToRawUuidString(regionUUID) + "'";
                DataRow[] rawDataForRegion = land.Select(searchExp);
                foreach (DataRow rawDataLand in rawDataForRegion)
                {
                    LandData newLand = buildLandData(rawDataLand);
                    string accessListSearchExp = "LandUUID = '" + Util.ToRawUuidString(newLand.globalID) + "'";
                    DataRow[] rawDataForLandAccessList = landaccesslist.Select(accessListSearchExp);
                    foreach (DataRow rawDataLandAccess in rawDataForLandAccessList)
                    {
                        newLand.parcelAccessList.Add(buildLandAccessData(rawDataLandAccess));
                    }

                    landDataForRegion.Add(newLand);
                }
            }
            return landDataForRegion;
        }

        private void DisplayDataSet(DataSet ds, string title)
        {
            Debug.WriteLine(title);
            //--- Loop through the DataTables
            foreach (DataTable table in ds.Tables)
            {
                Debug.WriteLine("*** DataTable: " + table.TableName + "***");
                //--- Loop through each DataTable's DataRows
                foreach (DataRow row in table.Rows)
                {
                    //--- Display the original values, if there are any.
                    if (row.HasVersion(DataRowVersion.Original))
                    {
                        Debug.Write("Original Row Values ===> ");
                        foreach (DataColumn column in table.Columns)
                            Debug.Write(column.ColumnName + " = " +
                                        row[column, DataRowVersion.Original] + ", ");
                        Debug.WriteLine(String.Empty);
                    }
                    //--- Display the current values, if there are any.
                    if (row.HasVersion(DataRowVersion.Current))
                    {
                        Debug.Write("Current Row Values ====> ");
                        foreach (DataColumn column in table.Columns)
                            Debug.Write(column.ColumnName + " = " +
                                        row[column, DataRowVersion.Current] + ", ");
                        Debug.WriteLine(String.Empty);
                    }
                    Debug.WriteLine(String.Empty);
                }
            }
        }

        public void Commit()
        {
            if (m_connection.State != ConnectionState.Open)
            {
                m_connection.Open();
            }

            lock (m_dataSet)
            {
                // DisplayDataSet(m_dataSet, "Region DataSet");

                m_primDataAdapter.Update(m_primTable);
                m_shapeDataAdapter.Update(m_shapeTable);

                if (persistPrimInventories)
                {
                    m_itemsDataAdapter.Update(m_itemsTable);
                }

                m_terrainDataAdapter.Update(m_terrainTable);
                m_landDataAdapter.Update(m_landTable);
                m_landAccessListDataAdapter.Update(m_landAccessListTable);

                m_dataSet.AcceptChanges();
            }
        }


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

        private DataColumn createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
            return col;
        }

        private DataTable createTerrainTable()
        {
            DataTable terrain = new DataTable("terrain");

            createCol(terrain, "RegionUUID", typeof (String));
            createCol(terrain, "Revision", typeof (Int32));
            DataColumn heightField = createCol(terrain, "Heightfield", typeof (Byte[]));
            return terrain;
        }

        private DataTable createPrimTable()
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

        private DataTable createLandTable()
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

            land.PrimaryKey = new DataColumn[] {land.Columns["UUID"]};

            return land;
        }

        private DataTable createLandAccessListTable()
        {
            DataTable landaccess = new DataTable("landaccesslist");
            createCol(landaccess, "LandUUID", typeof (String));
            createCol(landaccess, "AccessUUID", typeof (String));
            createCol(landaccess, "Flags", typeof (Int32));

            return landaccess;
        }

        private DataTable createShapeTable()
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
            createCol(shapes, "Texture", typeof (Byte[]));
            createCol(shapes, "ExtraParams", typeof (Byte[]));

            shapes.PrimaryKey = new DataColumn[] {shapes.Columns["UUID"]};

            return shapes;
        }
        
        private DataTable createItemsTable()
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
                prim.SetSitTargetLL(new LLVector3(
                                        Convert.ToSingle(row["SitTargetOffsetX"]),
                                        Convert.ToSingle(row["SitTargetOffsetY"]),
                                        Convert.ToSingle(row["SitTargetOffsetZ"])), new LLQuaternion(
                                                                                        Convert.ToSingle(
                                                                                            row["SitTargetOrientX"]),
                                                                                        Convert.ToSingle(
                                                                                            row["SitTargetOrientY"]),
                                                                                        Convert.ToSingle(
                                                                                            row["SitTargetOrientZ"]),
                                                                                        Convert.ToSingle(
                                                                                            row["SitTargetOrientW"])));
            }
            catch (InvalidCastException)
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
            return prim;
        }
        
        
        /// <summary>
        /// Build a prim inventory item from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private TaskInventoryItem buildItem(DataRow row)
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
            
            taskItem.NextOwnerMask = Convert.ToUInt32(row["nextPermissions"]);
            taskItem.OwnerMask     = Convert.ToUInt32(row["currentPermissions"]);
            taskItem.BaseMask      = Convert.ToUInt32(row["basePermissions"]);
            taskItem.EveryoneMask  = Convert.ToUInt32(row["everyonePermissions"]);
            taskItem.GroupMask     = Convert.ToUInt32(row["groupPermissions"]);
            
            return taskItem;
        }        

        private LandData buildLandData(DataRow row)
        {
            LandData newData = new LandData();

            newData.globalID = new LLUUID((String) row["UUID"]);
            newData.localID = Convert.ToInt32(row["LocalLandID"]);

            // Bitmap is a byte[512]
            newData.landBitmapByteArray = (Byte[]) row["Bitmap"];

            newData.landName = (String) row["Name"];
            newData.landDesc = (String) row["Description"];
            newData.ownerID = (String) row["OwnerUUID"];
            newData.isGroupOwned = Convert.ToBoolean(row["IsGroupOwned"]);
            newData.area = Convert.ToInt32(row["Area"]);
            newData.auctionID = Convert.ToUInt32(row["AuctionID"]); //Unemplemented
            newData.category = (Parcel.ParcelCategory) Convert.ToInt32(row["Category"]);
                //Enum libsecondlife.Parcel.ParcelCategory
            newData.claimDate = Convert.ToInt32(row["ClaimDate"]);
            newData.claimPrice = Convert.ToInt32(row["ClaimPrice"]);
            newData.groupID = new LLUUID((String) row["GroupUUID"]);
            newData.salePrice = Convert.ToInt32(row["SalePrice"]);
            newData.landStatus = (Parcel.ParcelStatus) Convert.ToInt32(row["LandStatus"]);
                //Enum. libsecondlife.Parcel.ParcelStatus
            newData.landFlags = Convert.ToUInt32(row["LandFlags"]);
            newData.landingType = Convert.ToByte(row["LandingType"]);
            newData.mediaAutoScale = Convert.ToByte(row["MediaAutoScale"]);
            newData.mediaID = new LLUUID((String) row["MediaTextureUUID"]);
            newData.mediaURL = (String) row["MediaURL"];
            newData.musicURL = (String) row["MusicURL"];
            newData.passHours = Convert.ToSingle(row["PassHours"]);
            newData.passPrice = Convert.ToInt32(row["PassPrice"]);
            newData.snapshotID = (String) row["SnapshotUUID"];

            newData.userLocation =
                new LLVector3(Convert.ToSingle(row["UserLocationX"]), Convert.ToSingle(row["UserLocationY"]),
                              Convert.ToSingle(row["UserLocationZ"]));
            newData.userLookAt =
                new LLVector3(Convert.ToSingle(row["UserLookAtX"]), Convert.ToSingle(row["UserLookAtY"]),
                              Convert.ToSingle(row["UserLookAtZ"]));
            newData.parcelAccessList = new List<ParcelManager.ParcelAccessEntry>();

            return newData;
        }

        private ParcelManager.ParcelAccessEntry buildLandAccessData(DataRow row)
        {
            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = new LLUUID((string) row["AccessUUID"]);
            entry.Flags = (ParcelManager.AccessList) Convert.ToInt32(row["Flags"]);
            entry.Time = new DateTime();
            return entry;
        }

        private Array serializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream(65536*sizeof (double));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < 256; x++)
                for (int y = 0; y < 256; y++)
                    bw.Write(val[x, y]);

            return str.ToArray();
        }

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
                LLVector3 sitTargetPos = prim.GetSitTargetPositionLL();
                row["SitTargetOffsetX"] = sitTargetPos.X;
                row["SitTargetOffsetY"] = sitTargetPos.Y;
                row["SitTargetOffsetZ"] = sitTargetPos.Z;

                LLQuaternion sitTargetOrient = prim.GetSitTargetOrientationLL();
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
        
        private void fillItemRow(DataRow row, TaskInventoryItem taskItem)
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
            row["nextPermissions"] = taskItem.NextOwnerMask;
            row["currentPermissions"] = taskItem.OwnerMask;
            row["basePermissions"] = taskItem.BaseMask;
            row["everyonePermissions"] = taskItem.EveryoneMask;
            row["groupPermissions"] = taskItem.GroupMask;
        }        

        private void fillLandRow(DataRow row, LandData land, LLUUID regionUUID)
        {
            row["UUID"] = Util.ToRawUuidString(land.globalID);
            row["RegionUUID"] = Util.ToRawUuidString(regionUUID);
            row["LocalLandID"] = land.localID;

            // Bitmap is a byte[512]
            row["Bitmap"] = land.landBitmapByteArray;

            row["Name"] = land.landName;
            row["Description"] = land.landDesc;
            row["OwnerUUID"] = Util.ToRawUuidString(land.ownerID);
            row["IsGroupOwned"] = land.isGroupOwned;
            row["Area"] = land.area;
            row["AuctionID"] = land.auctionID; //Unemplemented
            row["Category"] = land.category; //Enum libsecondlife.Parcel.ParcelCategory
            row["ClaimDate"] = land.claimDate;
            row["ClaimPrice"] = land.claimPrice;
            row["GroupUUID"] = Util.ToRawUuidString(land.groupID);
            row["SalePrice"] = land.salePrice;
            row["LandStatus"] = land.landStatus; //Enum. libsecondlife.Parcel.ParcelStatus
            row["LandFlags"] = land.landFlags;
            row["LandingType"] = land.landingType;
            row["MediaAutoScale"] = land.mediaAutoScale;
            row["MediaTextureUUID"] = Util.ToRawUuidString(land.mediaID);
            row["MediaURL"] = land.mediaURL;
            row["MusicURL"] = land.musicURL;
            row["PassHours"] = land.passHours;
            row["PassPrice"] = land.passPrice;
            row["SnapshotUUID"] = Util.ToRawUuidString(land.snapshotID);
            row["UserLocationX"] = land.userLocation.X;
            row["UserLocationY"] = land.userLocation.Y;
            row["UserLocationZ"] = land.userLocation.Z;
            row["UserLookAtX"] = land.userLookAt.X;
            row["UserLookAtY"] = land.userLookAt.Y;
            row["UserLookAtZ"] = land.userLookAt.Z;
        }

        private void fillLandAccessRow(DataRow row, ParcelManager.ParcelAccessEntry entry, LLUUID parcelID)
        {
            row["LandUUID"] = Util.ToRawUuidString(parcelID);
            row["AccessUUID"] = Util.ToRawUuidString(entry.AgentID);
            row["Flags"] = entry.Flags;
        }

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

            return s;
        }

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
        }

        private void addPrim(SceneObjectPart prim, LLUUID sceneGroupID, LLUUID regionUUID)
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
        
        // see IRegionDatastore
        public void StorePrimInventory(LLUUID primID, ICollection<TaskInventoryItem> items)
        {
            if (!persistPrimInventories)
                return;
                     
            m_log.InfoFormat("[DATASTORE]: Persisting Prim Inventory with prim ID {0}", primID);
            
            // For now, we're just going to crudely remove all the previous inventory items 
            // no matter whether they have changed or not, and replace them with the current set.
            lock (m_dataSet)
            {                              
                RemoveItems(primID);              
                
                // repalce with current inventory details
                foreach (TaskInventoryItem newItem in items)
                {
//                    m_log.InfoFormat(
//                        "[DATASTORE]: " +
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

        private MySqlCommand createInsertCommand(string table, DataTable dt)
        {
            /**
             *  This is subtle enough to deserve some commentary.
             *  Instead of doing *lots* and *lots of hardcoded strings
             *  for database definitions we'll use the fact that
             *  realistically all insert statements look like "insert
             *  into A(b, c) values(:b, :c) on the parameterized query
             *  front.  If we just have a list of b, c, etc... we can
             *  generate these strings instead of typing them out.
             */
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

        private MySqlCommand createUpdateCommand(string table, string pk, DataTable dt)
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

        private string defineTable(DataTable dt)
        {
            string sql = "create table " + dt.TableName + "(";
            string subsql = String.Empty;
            foreach (DataColumn col in dt.Columns)
            {
                if (subsql.Length > 0)
                {
                    // a map function would rock so much here
                    subsql += ",\n";
                }
                subsql += col.ColumnName + " " + MySqlType(col.DataType);
                if (dt.PrimaryKey.Length > 0 && col == dt.PrimaryKey[0])
                {
                    subsql += " primary key";
                }
            }
            sql += subsql;
            sql += ")";
            
            //m_log.InfoFormat("[DATASTORE]: defineTable() sql {0}", sql);
            
            return sql;
        }

        /***********************************************************************
         *
         *  Database Binding functions
         *
         *  These will be db specific due to typing, and minor differences
         *  in databases.
         *
         **********************************************************************/

        ///<summary>
        /// This is a convenience function that collapses 5 repetitive
        /// lines for defining MySqlParameters to 2 parameters:
        /// column name and database type.
        ///        
        /// It assumes certain conventions like ?param as the param
        /// name to replace in parametrized queries, and that source
        /// version is always current version, both of which are fine
        /// for us.
        ///</summary>
        ///<returns>a built MySql parameter</returns>
        private MySqlParameter createMySqlParameter(string name, Type type)
        {
            MySqlParameter param = new MySqlParameter();
            param.ParameterName = "?" + name;
            param.DbType = dbtypeFromType(type);
            param.SourceColumn = name;
            param.SourceVersion = DataRowVersion.Current;
            return param;
        }

        private MySqlParameter createParamWithValue(string name, Type type, Object o)
        {
            MySqlParameter param = createMySqlParameter(name, type);
            param.Value = o;
            return param;
        }

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

        private void SetupTerrainCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("terrain", m_dataSet.Tables["terrain"]);
            da.InsertCommand.Connection = conn;
        }

        private void setupLandCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("land", m_dataSet.Tables["land"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("land", "UUID=?UUID", m_dataSet.Tables["land"]);
            da.UpdateCommand.Connection = conn;
        }

        private void setupLandAccessCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("landaccesslist", m_dataSet.Tables["landaccesslist"]);
            da.InsertCommand.Connection = conn;
        }

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

        private void InitDB(MySqlConnection conn)
        {
            string createPrims = defineTable(createPrimTable());
            string createShapes = defineTable(createShapeTable());
            string createItems = defineTable(createItemsTable());
            string createTerrain = defineTable(createTerrainTable());
            string createLand = defineTable(createLandTable());
            string createLandAccessList = defineTable(createLandAccessListTable());

            MySqlCommand pcmd = new MySqlCommand(createPrims, conn);
            MySqlCommand scmd = new MySqlCommand(createShapes, conn);
            MySqlCommand icmd = new MySqlCommand(createItems, conn);
            MySqlCommand tcmd = new MySqlCommand(createTerrain, conn);
            MySqlCommand lcmd = new MySqlCommand(createLand, conn);
            MySqlCommand lalcmd = new MySqlCommand(createLandAccessList, conn);

            if (conn.State != ConnectionState.Open)
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    m_log.Error("[MySql]: Error connecting to MySQL server: " + ex.Message);
                    m_log.Error("[MySql]: Application is terminating!");
                    System.Threading.Thread.CurrentThread.Abort();
                }
            }

            try
            {
                pcmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                m_log.WarnFormat("[MySql]: Primitives Table Already Exists: {0}", e);
            }

            try
            {
                scmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                m_log.WarnFormat("[MySql]: Shapes Table Already Exists: {0}", e);
            }
            
            try
            {
                icmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                m_log.WarnFormat("[MySql]: Items Table Already Exists: {0}", e);
            }            

            try
            {
                tcmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                m_log.WarnFormat("[MySql]: Terrain Table Already Exists: {0}", e);
            }

            try
            {
                lcmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                m_log.WarnFormat("[MySql]: Land Table Already Exists: {0}", e);
            }

            try
            {
                lalcmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                m_log.WarnFormat("[MySql]: LandAccessList Table Already Exists: {0}", e);
            }
            conn.Close();
        }

        private bool TestTables(MySqlConnection conn)
        {
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
                
                if (persistPrimInventories)
                    iDa.Fill(tmpDS, "primitems");
                
                tDa.Fill(tmpDS, "terrain");
                lDa.Fill(tmpDS, "land");
                lalDa.Fill(tmpDS, "landaccesslist");
            }
            catch (MySqlException)
            {
                m_log.Info("[DATASTORE]: MySql Database doesn't exist... creating");
                InitDB(conn);
            }

            pDa.Fill(tmpDS, "prims");
            sDa.Fill(tmpDS, "primshapes");
            
            if (persistPrimInventories)
                iDa.Fill(tmpDS, "primitems");
            
            tDa.Fill(tmpDS, "terrain");
            lDa.Fill(tmpDS, "land");
            lalDa.Fill(tmpDS, "landaccesslist");

            foreach (DataColumn col in createPrimTable().Columns)
            {
                if (!tmpDS.Tables["prims"].Columns.Contains(col.ColumnName))
                {
                    m_log.Info("[DATASTORE]: Missing required column:" + col.ColumnName);
                    return false;
                }
            }
            
            foreach (DataColumn col in createShapeTable().Columns)
            {
                if (!tmpDS.Tables["primshapes"].Columns.Contains(col.ColumnName))
                {
                    m_log.Info("[DATASTORE]: Missing required column:" + col.ColumnName);
                    return false;
                }
            }
            
            // XXX primitems should probably go here eventually
            
            foreach (DataColumn col in createTerrainTable().Columns)
            {
                if (!tmpDS.Tables["terrain"].Columns.Contains(col.ColumnName))
                {
                    m_log.Info("[DATASTORE]: Missing require column:" + col.ColumnName);
                    return false;
                }
            }
            
            foreach (DataColumn col in createLandTable().Columns)
            {
                if (!tmpDS.Tables["land"].Columns.Contains(col.ColumnName))
                {
                    m_log.Info("[DATASTORE]: Missing require column:" + col.ColumnName);
                    return false;
                }
            }
            
            foreach (DataColumn col in createLandAccessListTable().Columns)
            {
                if (!tmpDS.Tables["landaccesslist"].Columns.Contains(col.ColumnName))
                {
                    m_log.Info("[DATASTORE]: Missing require column:" + col.ColumnName);
                    return false;
                }
            }
            
            return true;
        }

        /***********************************************************************
         *
         *  Type conversion functions
         *
         **********************************************************************/

        private DbType dbtypeFromType(Type type)
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

        // this is something we'll need to implement for each db
        // slightly differently.
        private string MySqlType(Type type)
        {
            if (type == typeof (String))
            {
                return "varchar(255)";
            }
            else if (type == typeof (Int32))
            {
                return "integer";
            }
            else if (type == typeof (Int64))
            {
                return "bigint";
            }            
            else if (type == typeof (Double))
            {
                return "float";
            }
            else if (type == typeof (Byte[]))
            {
                return "longblob";
            }
            else
            {
                return "string";
            }
        }
    }
}
