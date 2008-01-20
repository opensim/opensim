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
using System.IO;
using libsecondlife;
using Mono.Data.SqliteClient;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Framework.Data.SQLite
{
    public class SQLiteRegionData : IRegionDataStore
    {
        private const string primSelect = "select * from prims";
        private const string shapeSelect = "select * from primshapes";
        private const string itemsSelect = "select * from primitems";
        private const string terrainSelect = "select * from terrain limit 1";
        private const string landSelect = "select * from land";
        private const string landAccessListSelect = "select * from landaccesslist";

        private DataSet ds;
        private SqliteDataAdapter primDa;
        private SqliteDataAdapter shapeDa;
        private SqliteDataAdapter itemsDa;
        private SqliteDataAdapter terrainDa;
        private SqliteDataAdapter landDa;
        private SqliteDataAdapter landAccessListDa;

        private SqliteConnection m_conn;

        private String m_connectionString;

        // Temporary attribute while this is experimental
        private bool persistPrimInventories;

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        // see IRegionDataStore
        public void Initialise(string connectionString, bool persistPrimInventories)
        {
            m_connectionString = connectionString;
            this.persistPrimInventories = persistPrimInventories;

            ds = new DataSet();

            MainLog.Instance.Verbose("DATASTORE", "Sqlite - connecting: " + connectionString);
            m_conn = new SqliteConnection(m_connectionString);
            m_conn.Open();

            SqliteCommand primSelectCmd = new SqliteCommand(primSelect, m_conn);
            primDa = new SqliteDataAdapter(primSelectCmd);
            //            SqliteCommandBuilder primCb = new SqliteCommandBuilder(primDa);

            SqliteCommand shapeSelectCmd = new SqliteCommand(shapeSelect, m_conn);
            shapeDa = new SqliteDataAdapter(shapeSelectCmd);
            // SqliteCommandBuilder shapeCb = new SqliteCommandBuilder(shapeDa);      

            SqliteCommand itemsSelectCmd = new SqliteCommand(itemsSelect, m_conn);
            itemsDa = new SqliteDataAdapter(itemsSelectCmd);
            
            SqliteCommand terrainSelectCmd = new SqliteCommand(terrainSelect, m_conn);
            terrainDa = new SqliteDataAdapter(terrainSelectCmd);

            SqliteCommand landSelectCmd = new SqliteCommand(landSelect, m_conn);
            landDa = new SqliteDataAdapter(landSelectCmd);

            SqliteCommand landAccessListSelectCmd = new SqliteCommand(landAccessListSelect, m_conn);
            landAccessListDa = new SqliteDataAdapter(landAccessListSelectCmd);

            // We fill the data set, now we've got copies in memory for the information
            // TODO: see if the linkage actually holds.
            // primDa.FillSchema(ds, SchemaType.Source, "PrimSchema");
            TestTables(m_conn);

            lock (ds)
            {
                ds.Tables.Add(createPrimTable());
                setupPrimCommands(primDa, m_conn);
                primDa.Fill(ds.Tables["prims"]);

                ds.Tables.Add(createShapeTable());
                setupShapeCommands(shapeDa, m_conn);
                
                if (persistPrimInventories)
                {
                    ds.Tables.Add(createItemsTable());
                    setupItemsCommands(itemsDa, m_conn);
                    itemsDa.Fill(ds.Tables["primitems"]);
                }

                ds.Tables.Add(createTerrainTable());
                setupTerrainCommands(terrainDa, m_conn);

                ds.Tables.Add(createLandTable());
                setupLandCommands(landDa, m_conn);

                ds.Tables.Add(createLandAccessListTable());
                setupLandAccessCommands(landAccessListDa, m_conn);

                // WORKAROUND: This is a work around for sqlite on
                // windows, which gets really unhappy with blob columns
                // that have no sample data in them.  At some point we
                // need to actually find a proper way to handle this.
                try
                {
                    shapeDa.Fill(ds.Tables["primshapes"]);
                }
                catch (Exception)
                {
                    MainLog.Instance.Verbose("DATASTORE", "Caught fill error on primshapes table");
                }

                try
                {
                    terrainDa.Fill(ds.Tables["terrain"]);
                }
                catch (Exception)
                {
                    MainLog.Instance.Verbose("DATASTORE", "Caught fill error on terrain table");
                }

                try
                {
                    landDa.Fill(ds.Tables["land"]);
                }
                catch (Exception)
                {
                    MainLog.Instance.Verbose("DATASTORE", "Caught fill error on land table");
                }

                try
                {
                    landAccessListDa.Fill(ds.Tables["landaccesslist"]);
                }
                catch (Exception)
                {
                    MainLog.Instance.Verbose("DATASTORE", "Caught fill error on landaccesslist table");
                }
                return;
            }
        }

        public void StoreObject(SceneObjectGroup obj, LLUUID regionUUID)
        {
            lock (ds)
            {
                foreach (SceneObjectPart prim in obj.Children.Values)
                {
                    if ((prim.ObjectFlags & (uint) LLObject.ObjectFlags.Physics) == 0)
                    {
                        MainLog.Instance.Verbose("DATASTORE", "Adding obj: " + obj.UUID + " to region: " + regionUUID);
                        addPrim(prim, Util.ToRawUuidString(obj.UUID), Util.ToRawUuidString(regionUUID));
                    }
                    else if (prim.Stopped)
                    {
                        //MainLog.Instance.Verbose("DATASTORE",
                                                 //"Adding stopped obj: " + obj.UUID + " to region: " + regionUUID);
                        //addPrim(prim, Util.ToRawUuidString(obj.UUID), Util.ToRawUuidString(regionUUID));
                    }
                    else
                    {
                        // MainLog.Instance.Verbose("DATASTORE", "Ignoring Physical obj: " + obj.UUID + " in region: " + regionUUID);
                    }
                }
            }

            Commit();
            // MainLog.Instance.Verbose("Dump of prims:", ds.GetXml());
        }

        public void RemoveObject(LLUUID obj, LLUUID regionUUID)
        {
            MainLog.Instance.Verbose("DATASTORE", "Removing obj: {0} from region: {1}", obj.UUID, regionUUID);
            
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];
            DataTable items = ds.Tables["primitems"];

            string selectExp = "SceneGroupID = '" + Util.ToRawUuidString(obj) + "'";
            lock (ds)
            {
                DataRow[] primRows = prims.Select(selectExp);
                foreach (DataRow row in primRows)
                {
                    // Remove shape rows
                    LLUUID uuid = new LLUUID((string) row["UUID"]);
                    DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(uuid));
                    if (shapeRow != null)
                    {
                        shapeRow.Delete();
                    }

                    if (persistPrimInventories)
                    {
                        // Remove items rows
                        String sql = String.Format("primID = '{0}'", uuid);            
                        DataRow[] itemRows = items.Select(sql);
                
                        foreach (DataRow itemRow in itemRows)
                        {
                            itemRow.Delete();
                        }
                    }

                    // Remove prim row
                    row.Delete();                    
                }
            }

            Commit();
        }

        /// <summary>
        /// Load persisted objects from region storage.
        /// </summary>
        /// <param name="regionUUID"></param>
        /// <returns>List of loaded groups</returns>
        public List<SceneObjectGroup> LoadObjects(LLUUID regionUUID)
        {
            Dictionary<LLUUID, SceneObjectGroup> createdObjects = new Dictionary<LLUUID, SceneObjectGroup>();

            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

            string byRegion = "RegionUUID = '" + Util.ToRawUuidString(regionUUID) + "'";
            string orderByParent = "ParentID ASC";

            lock (ds)
            {
                DataRow[] primsForRegion = prims.Select(byRegion, orderByParent);
                MainLog.Instance.Verbose("DATASTORE",
                                         "Loaded " + primsForRegion.Length + " prims for region: " + regionUUID);

                foreach (DataRow primRow in primsForRegion)
                {
                    try
                    {
                        SceneObjectPart prim = null;
                            
                        string uuid = (string) primRow["UUID"];
                        string objID = (string) primRow["SceneGroupID"];
                        if (uuid == objID) //is new SceneObjectGroup ?
                        {
                            SceneObjectGroup group = new SceneObjectGroup();
                            prim = buildPrim(primRow);
                            DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                MainLog.Instance.Notice(
                                    "No shape found for prim in storage, so setting default box shape");
                                prim.Shape = PrimitiveBaseShape.Default;
                            }
                            group.AddPart(prim);
                            group.RootPart = prim;

                            createdObjects.Add(Util.ToRawUuidString(group.UUID), group);
                            retvals.Add(group);
                        }
                        else
                        {
                            prim = buildPrim(primRow);
                            DataRow shapeRow = shapes.Rows.Find(Util.ToRawUuidString(prim.UUID));
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                MainLog.Instance.Notice(
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
                        MainLog.Instance.Error("DATASTORE", "Failed create prim object, exception and data follows");
                        MainLog.Instance.Verbose("DATASTORE", e.ToString());
                        foreach (DataColumn col in prims.Columns)
                        {
                            MainLog.Instance.Verbose("DATASTORE", "Col: " + col.ColumnName + " => " + primRow[col]);
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
            MainLog.Instance.Verbose("DATASTORE", "Loading inventory for {0}, {1}", prim.Name, prim.UUID);
            
            DataTable dbItems = ds.Tables["primitems"];
            
            String sql = String.Format("primID = '{0}'", prim.UUID.ToString());            
            DataRow[] dbItemRows = dbItems.Select(sql);
            
            IList<TaskInventoryItem> inventory = new List<TaskInventoryItem>();
            
            foreach (DataRow row in dbItemRows)
            {
                TaskInventoryItem item = buildItem(row);
                inventory.Add(item);
                
                MainLog.Instance.Verbose("DATASTORE", "Restored item {0}, {1}", item.Name, item.ItemID); 
            }
            
            prim.AddInventoryItems(inventory);
            
            // XXX A nasty little hack to recover the folder id for the prim (which is currently stored in 
            // every item).  This data should really be stored in the prim table itself.
            if (dbItemRows.Length > 0)
            {
                prim.FolderID = inventory[0].ParentID;
            }
        }

        public void StoreTerrain(double[,] ter, LLUUID regionID)
        {
            lock (ds)
            {
                int revision = Util.UnixTimeSinceEpoch();

                // the following is an work around for .NET.  The perf
                // issues associated with it aren't as bad as you think.
                MainLog.Instance.Verbose("DATASTORE", "Storing terrain revision r" + revision.ToString());
                String sql = "insert into terrain(RegionUUID, Revision, Heightfield)" +
                             " values(:RegionUUID, :Revision, :Heightfield)";

                using (SqliteCommand cmd = new SqliteCommand(sql, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":RegionUUID", Util.ToRawUuidString(regionID)));
                    cmd.Parameters.Add(new SqliteParameter(":Revision", revision));
                    cmd.Parameters.Add(new SqliteParameter(":Heightfield", serializeTerrain(ter)));
                    cmd.ExecuteNonQuery();
                }

                // This is added to get rid of the infinitely growing
                // terrain databases which negatively impact on SQLite
                // over time.  Before reenabling this feature there
                // needs to be a limitter put on the number of
                // revisions in the database, as this old
                // implementation is a DOS attack waiting to happen.

                using (
                    SqliteCommand cmd =
                        new SqliteCommand("delete from terrain where RegionUUID=:RegionUUID and Revision < :Revision",
                                          m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":RegionUUID", Util.ToRawUuidString(regionID)));
                    cmd.Parameters.Add(new SqliteParameter(":Revision", revision));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public double[,] LoadTerrain(LLUUID regionID)
        {
            lock (ds)
            {
                double[,] terret = new double[256,256];
                terret.Initialize();

                String sql = "select RegionUUID, Revision, Heightfield from terrain" +
                             " where RegionUUID=:RegionUUID order by Revision desc";

                using (SqliteCommand cmd = new SqliteCommand(sql, m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":RegionUUID", Util.ToRawUuidString(regionID)));

                    using (IDataReader row = cmd.ExecuteReader())
                    {
                        int rev = 0;
                        if (row.Read())
                        {
                            // TODO: put this into a function
                            byte[] heightmap = (byte[]) row["Heightfield"];
                            for (int x = 0; x < 256; x++)
                            {
                                for (int y = 0; y < 256; y++)
                                {
                                    terret[x, y] = BitConverter.ToDouble(heightmap, ((x*256) + y)*8);
                                }
                            }
                            rev = (int) row["Revision"];
                        }
                        else
                        {
                            MainLog.Instance.Verbose("DATASTORE", "No terrain found for region");
                            return null;
                        }

                        MainLog.Instance.Verbose("DATASTORE", "Loaded terrain revision r" + rev.ToString());
                    }
                }
                return terret;
            }
        }

        public void RemoveLandObject(LLUUID globalID)
        {
            lock (ds)
            {
                using (SqliteCommand cmd = new SqliteCommand("delete from land where UUID=:UUID", m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(globalID)));
                    cmd.ExecuteNonQuery();
                }

                using (SqliteCommand cmd = new SqliteCommand("delete from landaccesslist where LandUUID=:UUID", m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":UUID", Util.ToRawUuidString(globalID)));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void StoreLandObject(Land parcel, LLUUID regionUUID)
        {
            lock (ds)
            {
                DataTable land = ds.Tables["land"];
                DataTable landaccesslist = ds.Tables["landaccesslist"];

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
                    SqliteCommand cmd = new SqliteCommand("delete from landaccesslist where LandUUID=:LandUUID", m_conn))
                {
                    cmd.Parameters.Add(new SqliteParameter(":LandUUID", Util.ToRawUuidString(parcel.landData.globalID)));
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
            lock (ds)
            {
                DataTable land = ds.Tables["land"];
                DataTable landaccesslist = ds.Tables["landaccesslist"];
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

        public void Commit()
        {
            lock (ds)
            {
                primDa.Update(ds, "prims");
                shapeDa.Update(ds, "primshapes");
                
                if (persistPrimInventories)
                {
                    itemsDa.Update(ds, "primitems");
                }
                
                terrainDa.Update(ds, "terrain");
                landDa.Update(ds, "land");
                landAccessListDa.Update(ds, "landaccesslist");
                ds.AcceptChanges();
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

        private void createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        private DataTable createTerrainTable()
        {
            DataTable terrain = new DataTable("terrain");

            createCol(terrain, "RegionUUID", typeof (String));
            createCol(terrain, "Revision", typeof (Int32));
            createCol(terrain, "Heightfield", typeof (Byte[]));

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
            // text TODO: this isn't right, but I'm not sure the right
            // way to specify this as a blob atm
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

            createCol(items, "nextPermissions", typeof (UInt32));
            createCol(items, "currentPermissions", typeof (UInt32));
            createCol(items, "basePermissions", typeof (UInt32));
            createCol(items, "everyonePermissions", typeof (UInt32));
            createCol(items, "groupPermissions", typeof (UInt32));

            items.PrimaryKey = new DataColumn[] {items.Columns["itemID"]};

            return items;
        }

        private DataTable createLandTable()
        {
            DataTable land = new DataTable("land");
            createCol(land, "UUID", typeof (String));
            createCol(land, "RegionUUID", typeof (String));
            createCol(land, "LocalLandID", typeof (UInt32));

            // Bitmap is a byte[512]
            createCol(land, "Bitmap", typeof (Byte[]));

            createCol(land, "Name", typeof (String));
            createCol(land, "Desc", typeof (String));
            createCol(land, "OwnerUUID", typeof (String));
            createCol(land, "IsGroupOwned", typeof (Boolean));
            createCol(land, "Area", typeof (Int32));
            createCol(land, "AuctionID", typeof (Int32)); //Unemplemented
            createCol(land, "Category", typeof (Int32)); //Enum libsecondlife.Parcel.ParcelCategory
            createCol(land, "ClaimDate", typeof (Int32));
            createCol(land, "ClaimPrice", typeof (Int32));
            createCol(land, "GroupUUID", typeof (string));
            createCol(land, "SalePrice", typeof (Int32));
            createCol(land, "LandStatus", typeof (Int32)); //Enum. libsecondlife.Parcel.ParcelStatus
            createCol(land, "LandFlags", typeof (UInt32));
            createCol(land, "LandingType", typeof (Byte));
            createCol(land, "MediaAutoScale", typeof (Byte));
            createCol(land, "MediaTextureUUID", typeof (String));
            createCol(land, "MediaURL", typeof (String));
            createCol(land, "MusicURL", typeof (String));
            createCol(land, "PassHours", typeof (Double));
            createCol(land, "PassPrice", typeof (UInt32));
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
            createCol(landaccess, "Flags", typeof (UInt32));

            return landaccess;
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
            // TODO: this doesn't work yet because something more
            // interesting has to be done to actually get these values
            // back out.  Not enough time to figure it out yet.
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
                // Database table was created before we got here and now has null values :P
                m_conn.Open();
                SqliteCommand cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOffsetX float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOffsetY float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOffsetZ float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientW float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientX float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientY float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
                cmd =
                    new SqliteCommand("ALTER TABLE prims ADD COLUMN SitTargetOrientZ float NOT NULL default 0;", m_conn);
                cmd.ExecuteNonQuery();
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
            newData.landDesc = (String) row["Desc"];
            newData.ownerID = (String) row["OwnerUUID"];
            newData.isGroupOwned = (Boolean) row["IsGroupOwned"];
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
            newData.landingType = (Byte) row["LandingType"];
            newData.mediaAutoScale = (Byte) row["MediaAutoScale"];
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
            entry.Flags = (ParcelManager.AccessList) row["Flags"];
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

//         private void fillTerrainRow(DataRow row, LLUUID regionUUID, int rev, double[,] val)
//         {
//             row["RegionUUID"] = regionUUID;
//             row["Revision"] = rev;

//             MemoryStream str = new MemoryStream(65536*sizeof (double));
//             BinaryWriter bw = new BinaryWriter(str);

//             // TODO: COMPATIBILITY - Add byte-order conversions
//             for (int x = 0; x < 256; x++)
//                 for (int y = 0; y < 256; y++)
//                     bw.Write(val[x, y]);

//             row["Heightfield"] = str.ToArray();
//         }

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
            row["Desc"] = land.landDesc;
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
            // text TODO: this isn't right] = but I'm not sure the right
            // way to specify this as a blob atm

            byte[] textureEntry = (byte[]) row["Texture"];
            s.TextureEntry = textureEntry;


            s.ExtraParams = (byte[]) row["ExtraParams"];
            // System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            //             string texture = encoding.GetString((Byte[])row["Texture"]);
            //             if (!texture.StartsWith("<"))
            //             {
            //                 //here so that we can still work with old format database files (ie from before I added xml serialization)
            //                  LLObject.TextureEntry textureEntry = null;
            //                 textureEntry = new LLObject.TextureEntry(new LLUUID(texture));
            //                 s.TextureEntry = textureEntry.ToBytes();
            //             }
            //             else
            //             {
            //                 TextureBlock textureEntry = TextureBlock.FromXmlString(texture);
            //                 s.TextureEntry = textureEntry.TextureData;
            //                 s.ExtraParams = textureEntry.ExtraParams;
            // }

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
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

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
            
            if (persistPrimInventories)
            {
                addPrimInventory(prim.UUID, prim.TaskInventory);
            }
        }
        
        /// <summary>
        /// Persist prim inventory.  Deletes, updates and inserts rows.
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        private void addPrimInventory(LLUUID primID, IDictionary<LLUUID, TaskInventoryItem> items)
        {
            MainLog.Instance.Verbose("DATASTORE", "Entered addPrimInventory with prim ID {0}", primID);
            
            // Find all existing inventory rows for this prim
            DataTable dbItems = ds.Tables["primitems"];

            String sql = String.Format("primID = '{0}'", primID);            
            DataRow[] dbItemRows = dbItems.Select(sql);
            
            // Build structures for manipulation purposes
            IDictionary<String, DataRow> dbItemsToRemove = new Dictionary<String, DataRow>();
            ICollection<TaskInventoryItem> itemsToAdd 
                = new List<TaskInventoryItem>();
            
            foreach (DataRow row in dbItemRows)
            {
                dbItemsToRemove.Add((String)row["itemID"], row);
            }
            
            // Eliminate rows from the deletion set which already exist for this prim's inventory
            // TODO Very temporary, need to take account of simple metadata changes soon
            lock (items)
            {
                foreach (LLUUID itemId in items.Keys)
                {
                    String rawItemId = itemId.ToString();
                    
                    if (dbItemsToRemove.ContainsKey(rawItemId))
                    {
                        dbItemsToRemove.Remove(rawItemId);
                    }
                    else
                    {
                        itemsToAdd.Add(items[itemId]);
                    }
                }    
            }
            
            // Delete excess rows
            foreach (DataRow row in dbItemsToRemove.Values)
            {
                MainLog.Instance.Verbose(
                    "DATASTORE", 
                    "Removing item {0}, {1} from prim ID {2}", 
                    row["name"], row["itemID"], row["primID"]);
                
                row.Delete();
            }
            
            // Insert items not already present 
            foreach (TaskInventoryItem newItem in itemsToAdd)
            {
                MainLog.Instance.Verbose(
                    "DATASTORE", 
                    "Adding item {0}, {1} to prim ID {2}", 
                    newItem.Name, newItem.ItemID, newItem.ParentPartID);
                
                DataRow newItemRow = dbItems.NewRow();
                fillItemRow(newItemRow, newItem);
                dbItems.Rows.Add(newItemRow);                
            }
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

        private SqliteCommand createInsertCommand(string table, DataTable dt)
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
            sql += ") values (:";
            sql += String.Join(", :", cols);
            sql += ")";
            SqliteCommand cmd = new SqliteCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be
            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createSqliteParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }

        private SqliteCommand createUpdateCommand(string table, string pk, DataTable dt)
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
                subsql += col.ColumnName + "= :" + col.ColumnName;
            }
            sql += subsql;
            sql += " where " + pk;
            SqliteCommand cmd = new SqliteCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be

            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createSqliteParameter(col.ColumnName, col.DataType));
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
                subsql += col.ColumnName + " " + sqliteType(col.DataType);
                if (dt.PrimaryKey.Length > 0 && col == dt.PrimaryKey[0])
                {
                    subsql += " primary key";
                }
            }
            sql += subsql;
            sql += ")";
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
        /// lines for defining SqliteParameters to 2 parameters:
        /// column name and database type.
        ///        
        /// It assumes certain conventions like :param as the param
        /// name to replace in parametrized queries, and that source
        /// version is always current version, both of which are fine
        /// for us.
        ///</summary>
        ///<returns>a built sqlite parameter</returns>
        private SqliteParameter createSqliteParameter(string name, Type type)
        {
            SqliteParameter param = new SqliteParameter();
            param.ParameterName = ":" + name;
            param.DbType = dbtypeFromType(type);
            param.SourceColumn = name;
            param.SourceVersion = DataRowVersion.Current;
            return param;
        }                

        private void setupPrimCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("prims", ds.Tables["prims"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("prims", "UUID=:UUID", ds.Tables["prims"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from prims where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }
        
        private void setupItemsCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("primitems", ds.Tables["primitems"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primitems", "itemID = :itemID", ds.Tables["primitems"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from primitems where itemID = :itemID");
            delete.Parameters.Add(createSqliteParameter("itemID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }        

        private void setupTerrainCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("terrain", ds.Tables["terrain"]);
            da.InsertCommand.Connection = conn;
        }

        private void setupLandCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("land", ds.Tables["land"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("land", "UUID=:UUID", ds.Tables["land"]);
            da.UpdateCommand.Connection = conn;
        }

        private void setupLandAccessCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("landaccesslist", ds.Tables["landaccesslist"]);
            da.InsertCommand.Connection = conn;
        }

        private void setupShapeCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            da.InsertCommand = createInsertCommand("primshapes", ds.Tables["primshapes"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primshapes", "UUID=:UUID", ds.Tables["primshapes"]);
            da.UpdateCommand.Connection = conn;

            SqliteCommand delete = new SqliteCommand("delete from primshapes where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        /// <summary>
        /// Create the necessary database tables.
        /// </summary>
        /// <param name="conn"></param>
        private void InitDB(SqliteConnection conn)
        {
            string createPrims = defineTable(createPrimTable());
            string createShapes = defineTable(createShapeTable());
            string createItems = defineTable(createItemsTable());
            string createTerrain = defineTable(createTerrainTable());
            string createLand = defineTable(createLandTable());
            string createLandAccessList = defineTable(createLandAccessListTable());

            SqliteCommand pcmd = new SqliteCommand(createPrims, conn);
            SqliteCommand scmd = new SqliteCommand(createShapes, conn);
            SqliteCommand icmd = new SqliteCommand(createItems, conn);
            SqliteCommand tcmd = new SqliteCommand(createTerrain, conn);
            SqliteCommand lcmd = new SqliteCommand(createLand, conn);
            SqliteCommand lalcmd = new SqliteCommand(createLandAccessList, conn);

            conn.Open();

            try
            {
                pcmd.ExecuteNonQuery();
            }
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Warn("SQLITE", "Primitives Table Already Exists");
            }

            try
            {
                scmd.ExecuteNonQuery();
            }
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Warn("SQLITE", "Shapes Table Already Exists");
            }

            if (persistPrimInventories)
            {
                try
                {
                    icmd.ExecuteNonQuery();
                }
                catch (SqliteSyntaxException)
                {
                    MainLog.Instance.Warn("SQLITE", "Primitives Inventory Table Already Exists");
                }
            }

            try
            {
                tcmd.ExecuteNonQuery();
            }
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Warn("SQLITE", "Terrain Table Already Exists");
            }

            try
            {
                lcmd.ExecuteNonQuery();
            }
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Warn("SQLITE", "Land Table Already Exists");
            }

            try
            {
                lalcmd.ExecuteNonQuery();
            }
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Warn("SQLITE", "LandAccessList Table Already Exists");
            }
            conn.Close();
        }

        private bool TestTables(SqliteConnection conn)
        {
            SqliteCommand primSelectCmd = new SqliteCommand(primSelect, conn);
            SqliteDataAdapter pDa = new SqliteDataAdapter(primSelectCmd);

            SqliteCommand shapeSelectCmd = new SqliteCommand(shapeSelect, conn);
            SqliteDataAdapter sDa = new SqliteDataAdapter(shapeSelectCmd);

            SqliteCommand itemsSelectCmd = new SqliteCommand(itemsSelect, conn);
            SqliteDataAdapter iDa = new SqliteDataAdapter(itemsSelectCmd);

            SqliteCommand terrainSelectCmd = new SqliteCommand(terrainSelect, conn);
            SqliteDataAdapter tDa = new SqliteDataAdapter(terrainSelectCmd);

            SqliteCommand landSelectCmd = new SqliteCommand(landSelect, conn);
            SqliteDataAdapter lDa = new SqliteDataAdapter(landSelectCmd);

            SqliteCommand landAccessListSelectCmd = new SqliteCommand(landAccessListSelect, conn);
            SqliteDataAdapter lalDa = new SqliteDataAdapter(landAccessListSelectCmd);

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
            catch (SqliteSyntaxException)
            {
                MainLog.Instance.Verbose("DATASTORE", "SQLite Database doesn't exist... creating");
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
                    MainLog.Instance.Verbose("DATASTORE", "Missing required column:" + col.ColumnName);
                    return false;
                }
            }

            foreach (DataColumn col in createShapeTable().Columns)
            {
                if (!tmpDS.Tables["primshapes"].Columns.Contains(col.ColumnName))
                {
                    MainLog.Instance.Verbose("DATASTORE", "Missing required column:" + col.ColumnName);
                    return false;
                }
            }
            
            // XXX primitems should probably go here eventually

            foreach (DataColumn col in createTerrainTable().Columns)
            {
                if (!tmpDS.Tables["terrain"].Columns.Contains(col.ColumnName))
                {
                    MainLog.Instance.Verbose("DATASTORE", "Missing require column:" + col.ColumnName);
                    return false;
                }
            }

            foreach (DataColumn col in createLandTable().Columns)
            {
                if (!tmpDS.Tables["land"].Columns.Contains(col.ColumnName))
                {
                    MainLog.Instance.Verbose("DATASTORE", "Missing require column:" + col.ColumnName);
                    return false;
                }
            }

            foreach (DataColumn col in createLandAccessListTable().Columns)
            {
                if (!tmpDS.Tables["landaccesslist"].Columns.Contains(col.ColumnName))
                {
                    MainLog.Instance.Verbose("DATASTORE", "Missing require column:" + col.ColumnName);
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
        private string sqliteType(Type type)
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
                return "integer";
            }
            else if (type == typeof (Double))
            {
                return "float";
            }
            else if (type == typeof (Byte[]))
            {
                return "blob";
            }
            else
            {
                return "string";
            }
        }
    }
}
