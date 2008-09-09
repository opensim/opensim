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
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A MSSQL Interface for the Region Server.
    /// </summary>
    public class MSSQLRegionDataStore : IRegionDataStore
    {
        // private static FileSystemDataStore Instance = new FileSystemDataStore();
        private static readonly ILog _Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MSSQLManager _Database;

//        private const string _PrimSelect = "SELECT * FROM PRIMS WHERE RegionUUID = @RegionUUID AND (SceneGroupID LIKE @SceneGroupID OR UUID = @UUID)";
//        private const string _ShapeSelect = "SELECT * FROM PRIMSHAPES WHERE UUID in (SELECT UUID FROM PRIMS WHERE RegionUUID = @RegionUUID AND (SceneGroupID LIKE @SceneGroupID OR UUID = @UUID))";
//        private const string _ItemsSelect = "SELECT * FROM PRIMITEMS WHERE primID in (SELECT UUID FROM PRIMS WHERE RegionUUID = @RegionUUID AND (SceneGroupID LIKE @SceneGroupID OR UUID = @UUID))";
        private const string _PrimSelect = "SELECT * FROM PRIMS WHERE RegionUUID = @RegionUUID AND (SceneGroupID LIKE @SceneGroupID OR UUID IN (@UUID))";
        private const string _ShapeSelect = "SELECT * FROM PRIMSHAPES WHERE UUID in (SELECT UUID FROM PRIMS WHERE RegionUUID = @RegionUUID AND (SceneGroupID LIKE @SceneGroupID OR UUID IN (@UUID)))";
        private const string _ItemsSelect = "SELECT * FROM PRIMITEMS WHERE primID in (SELECT UUID FROM PRIMS WHERE RegionUUID = @RegionUUID AND (SceneGroupID LIKE @SceneGroupID OR UUID IN (@UUID)))";

        private DataSet _PrimsDataSet;
        private SqlDataAdapter _PrimDataAdapter;
        private SqlDataAdapter _ShapeDataAdapter;
        private SqlDataAdapter _ItemsDataAdapter;

        /// <summary>
        /// Initialises the region datastore
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public void Initialise(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                //Add MSSQLManager (dont know if we need it)
                _Database = new MSSQLManager(connectionString);
            }
            else
            {
                IniFile iniFile = new IniFile("mssql_connection.ini");
                string settingDataSource = iniFile.ParseFileReadValue("data_source");
                string settingInitialCatalog = iniFile.ParseFileReadValue("initial_catalog");
                string settingPersistSecurityInfo = iniFile.ParseFileReadValue("persist_security_info");
                string settingUserId = iniFile.ParseFileReadValue("user_id");
                string settingPassword = iniFile.ParseFileReadValue("password");

                _Database =
                    new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                     settingPassword);


                SqlConnectionStringBuilder conBuilder = new SqlConnectionStringBuilder();
                conBuilder.DataSource = settingDataSource;
                conBuilder.InitialCatalog = settingInitialCatalog;
                conBuilder.PersistSecurityInfo = Convert.ToBoolean(settingPersistSecurityInfo);
                conBuilder.UserID = settingUserId;
                conBuilder.Password = settingPassword;
                conBuilder.ApplicationName = Assembly.GetEntryAssembly().Location;

                connectionString = conBuilder.ToString();
            }

            //Migration settings
            Assembly assem = GetType().Assembly;

            using (SqlConnection connection = _Database.DatabaseConnection())
            {
                MSSQLMigration m = new MSSQLMigration(connection, assem, "RegionStore");

                m.Update();

                //Create Dataset. Not filled!!!
                _PrimsDataSet = new DataSet("primsdata");

                using (SqlCommand primSelectCmd = new SqlCommand(_PrimSelect, connection))
                {
                    primSelectCmd.Parameters.AddWithValue("@RegionUUID", "");
                    primSelectCmd.Parameters.AddWithValue("@SceneGroupID", "%");
                    primSelectCmd.Parameters.AddWithValue("@UUID", "");
                    _PrimDataAdapter = new SqlDataAdapter(primSelectCmd);

                    DataTable primDataTable = new DataTable("prims");
                    _PrimDataAdapter.Fill(primDataTable);
                    primDataTable.PrimaryKey = new DataColumn[] { primDataTable.Columns["UUID"] };
                    _PrimsDataSet.Tables.Add(primDataTable);

                    SetupCommands(_PrimDataAdapter); //, connection);
                    //SetupPrimCommands(_PrimDataAdapter, connection);

                    primDataTable.Clear();
                }

                using (SqlCommand shapeSelectCmd = new SqlCommand(_ShapeSelect, connection))
                {
                    shapeSelectCmd.Parameters.AddWithValue("@RegionUUID", "");
                    shapeSelectCmd.Parameters.AddWithValue("@SceneGroupID", "%");
                    shapeSelectCmd.Parameters.AddWithValue("@UUID", "");
                    _ShapeDataAdapter = new SqlDataAdapter(shapeSelectCmd);

                    DataTable shapeDataTable = new DataTable("primshapes");
                    _ShapeDataAdapter.Fill(shapeDataTable);
                    shapeDataTable.PrimaryKey = new DataColumn[] { shapeDataTable.Columns["UUID"] };
                    _PrimsDataSet.Tables.Add(shapeDataTable);

                    SetupCommands(_ShapeDataAdapter); //, connection);
                    //SetupShapeCommands(_ShapeDataAdapter, connection);

                    shapeDataTable.Clear();
                }

                using (SqlCommand itemSelectCmd = new SqlCommand(_ItemsSelect, connection))
                {
                    itemSelectCmd.Parameters.AddWithValue("@RegionUUID", "");
                    itemSelectCmd.Parameters.AddWithValue("@SceneGroupID", "%");
                    itemSelectCmd.Parameters.AddWithValue("@UUID", "");
                    _ItemsDataAdapter = new SqlDataAdapter(itemSelectCmd);

                    DataTable itemsDataTable = new DataTable("primitems");
                    _ItemsDataAdapter.Fill(itemsDataTable);
                    itemsDataTable.PrimaryKey = new DataColumn[] { itemsDataTable.Columns["itemID"] };
                    _PrimsDataSet.Tables.Add(itemsDataTable);

                    SetupCommands(_ItemsDataAdapter); //, connection);
                    //SetupItemsCommands(_ItemsDataAdapter, connection);

                    itemsDataTable.Clear();
                }

                connection.Close();
            }

            //After this we have a empty fully configured DataSet.
        }

        /// <summary>
        /// Loads the objects present in the region.
        /// </summary>
        /// <param name="regionUUID">The region UUID.</param>
        /// <returns></returns>
        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            Dictionary<UUID, SceneObjectGroup> createdObjects = new Dictionary<UUID, SceneObjectGroup>();

            //Retrieve all values of current region
            RetrievePrimsDataForRegion(regionUUID, UUID.Zero, "");

            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            DataTable prims = _PrimsDataSet.Tables["prims"];
            DataTable shapes = _PrimsDataSet.Tables["primshapes"];

            lock (_PrimsDataSet)
            {
                DataRow[] primsForRegion = prims.Select("", "ParentID ASC"); //.Select(byRegion, orderByParent);

                _Log.Info("[REGION DB]: " + "Loaded " + primsForRegion.Length + " prims for region: " + regionUUID);

                foreach (DataRow primRow in primsForRegion)
                {
                    try
                    {
                        string uuid = (string)primRow["UUID"];
                        string objID = (string)primRow["SceneGroupID"];

                        SceneObjectPart prim = buildPrim(primRow);

                        if (uuid == objID) //is new SceneObjectGroup ?
                        {
                            SceneObjectGroup group = new SceneObjectGroup();

                            DataRow shapeRow = shapes.Rows.Find(prim.UUID.ToString());
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                _Log.Info(
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
                            DataRow shapeRow = shapes.Rows.Find(prim.UUID.ToString());
                            if (shapeRow != null)
                            {
                                prim.Shape = buildShape(shapeRow);
                            }
                            else
                            {
                                _Log.Info(
                                    "No shape found for prim in storage, so setting default box shape");
                                prim.Shape = PrimitiveBaseShape.Default;
                            }
                            createdObjects[new UUID(objID)].AddPart(prim);
                        }

                        LoadItems(prim);
                    }
                    catch (Exception e)
                    {
                        _Log.Error("[REGION DB]: Failed create prim object, exception and data follows");
                        _Log.Info("[REGION DB]: " + e.ToString());
                        foreach (DataColumn col in prims.Columns)
                        {
                            _Log.Info("[REGION DB]: Col: " + col.ColumnName + " => " + primRow[col]);
                        }
                    }
                }

                _PrimsDataSet.Tables["prims"].Clear();
                _PrimsDataSet.Tables["primshapes"].Clear();
                _PrimsDataSet.Tables["primitems"].Clear();
            }
            return retvals;

            #region Experimental

//
//            //Get all prims
//            string sql = "select * from prims where RegionUUID = @RegionUUID";
//
//            using (AutoClosingSqlCommand cmdPrims = _Database.Query(sql))
//            {
//                cmdPrims.Parameters.AddWithValue("@RegionUUID", regionUUID.ToString());
//                using (SqlDataReader readerPrims = cmdPrims.ExecuteReader())
//                {
//                    while (readerPrims.Read())
//                    {
//                        string uuid = (string)readerPrims["UUID"];
//                        string objID = (string)readerPrims["SceneGroupID"];
//                        SceneObjectPart prim = buildPrim(readerPrims);
//
//                        //Setting default shape, will change shape ltr
//                        prim.Shape = PrimitiveBaseShape.Default;
//
//                        //Load inventory items of prim
//                        //LoadItems(prim);
//
//                        if (uuid == objID)
//                        {
//                            SceneObjectGroup group = new SceneObjectGroup();
//
//                            group.AddPart(prim);
//                            group.RootPart = prim;
//
//                            createdObjects.Add(group.UUID, group);
//                            retvals.Add(group);
//                        }
//                        else
//                        {
//                            createdObjects[new UUID(objID)].AddPart(prim);
//                        }
//                    }
//                }
//            }
//            m_log.Info("[REGION DB]: Loaded " + retvals.Count + " prim objects for region: " + regionUUID);
//
//            //Find all shapes related with prims
//            sql = "select * from primshapes";
//            using (AutoClosingSqlCommand cmdShapes = _Database.Query(sql))
//            {
//                using (SqlDataReader readerShapes = cmdShapes.ExecuteReader())
//                {
//                    while (readerShapes.Read())
//                    {
//                        UUID UUID = new UUID((string) readerShapes["UUID"]);
//
//                        foreach (SceneObjectGroup objectGroup in createdObjects.Values)
//                        {
//                            if (objectGroup.Children.ContainsKey(UUID))
//                            {
//                                objectGroup.Children[UUID].Shape = buildShape(readerShapes);
//                            }
//                        }
//                    }
//                }
//            }
//            return retvals;

            #endregion
        }

        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
            //Retrieve all values of current region, and current scene/or prims
            //Build primID's, we use IN so I can select all prims from objgroup
            string primID = "";
            foreach (SceneObjectPart prim in obj.Children.Values)
            {
                primID += prim.UUID + "', '";
            }
            primID = primID.Remove(primID.LastIndexOf("',"));

            RetrievePrimsDataForRegion(regionUUID, obj.UUID, primID);

            _Log.InfoFormat("[REGION DB]: Adding/Changing SceneObjectGroup: {0} to region: {1}, object has {2} prims.", obj.UUID, regionUUID, obj.Children.Count);

            foreach (SceneObjectPart prim in obj.Children.Values)
            {
                if ((prim.GetEffectiveObjectFlags() & (uint)PrimFlags.Physics) == 0
                    && (prim.GetEffectiveObjectFlags() & (uint)PrimFlags.Temporary) == 0
                    && (prim.GetEffectiveObjectFlags() & (uint)PrimFlags.TemporaryOnRez) == 0)
                {
                    lock (_PrimsDataSet)
                    {
                        DataTable prims = _PrimsDataSet.Tables["prims"];
                        DataTable shapes = _PrimsDataSet.Tables["primshapes"];

                        DataRow primRow = prims.Rows.Find(prim.UUID.ToString());
                        if (primRow == null)
                        {
                            primRow = prims.NewRow();
                            fillPrimRow(primRow, prim, obj.UUID, regionUUID);
                            prims.Rows.Add(primRow);
                        }
                        else
                        {
                            fillPrimRow(primRow, prim, obj.UUID, regionUUID);
                        }

                        DataRow shapeRow = shapes.Rows.Find(prim.UUID.ToString());
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
                else
                {
                    // m_log.Info("[DATASTORE]: Ignoring Physical obj: " + obj.UUID + " in region: " + regionUUID);
                }
            }

            //Save changes
            CommitDataSet();
        }

        /// <summary>
        /// Removes a object from the database.
        /// Meaning removing it from tables Prims, PrimShapes and PrimItems
        /// </summary>
        /// <param name="objectID">id of scenegroup</param>
        /// <param name="regionUUID">regionUUID (is this used anyway</param>
        public void RemoveObject(UUID objectID, UUID regionUUID)
        {
            _Log.InfoFormat("[REGION DB]: Removing obj: {0} from region: {1}", objectID, regionUUID);

            //Remove from prims and primsitem table
            string sqlPrims = string.Format("DELETE FROM PRIMS WHERE SceneGroupID = '{0}'", objectID);
            string sqlPrimItems = string.Format("DELETE FROM PRIMITEMS WHERE primID in (SELECT UUID FROM PRIMS WHERE SceneGroupID = '{0}')", objectID);
            string sqlPrimShapes = string.Format("DELETE FROM PRIMSHAPES WHERE uuid in (SELECT UUID FROM PRIMS WHERE SceneGroupID = '{0}')", objectID);

            //Using the non transaction mode.
            using (AutoClosingSqlCommand cmd = _Database.Query(sqlPrimShapes))
            {
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlPrimItems;
                cmd.ExecuteNonQuery();

                cmd.CommandText = sqlPrims;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Store the inventory of a prim. Warning deletes everything first and then adds all again.
        /// </summary>
        /// <param name="primID"></param>
        /// <param name="items"></param>
        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
            _Log.InfoFormat("[REGION DB]: Persisting Prim Inventory with prim ID {0}", primID);

            //Statement from MySQL section!
            // For now, we're just going to crudely remove all the previous inventory items
            // no matter whether they have changed or not, and replace them with the current set.

            //Delete everything from PrimID
            //TODO add index on PrimID in DB, if not already exist
            using (AutoClosingSqlCommand cmd = _Database.Query("DELETE PRIMITEMS WHERE primID = @primID"))
            {
                cmd.Parameters.AddWithValue("@primID", primID.ToString());
                cmd.ExecuteNonQuery();
            }

            string sql =
                "INSERT INTO [primitems] ([itemID],[primID],[assetID],[parentFolderID],[invType],[assetType],[name],[description],[creationDate],[creatorID],[ownerID],[lastOwnerID],[groupID],[nextPermissions],[currentPermissions],[basePermissions],[everyonePermissions],[groupPermissions],[flags]) VALUES (@itemID,@primID,@assetID,@parentFolderID,@invType,@assetType,@name,@description,@creationDate,@creatorID,@ownerID,@lastOwnerID,@groupID,@nextPermissions,@currentPermissions,@basePermissions,@everyonePermissions,@groupPermissions,@flags)";

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                foreach (TaskInventoryItem newItem in items)
                {
                    //
                    cmd.Parameters.AddRange(CreatePrimInventoryParameters(newItem));

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();
                }
            }
        }

        /// <summary>
        /// Loads the terrain map.
        /// </summary>
        /// <param name="regionID">regionID.</param>
        /// <returns></returns>
        public double[,] LoadTerrain(UUID regionID)
        {
            double[,] terrain = new double[256, 256];
            terrain.Initialize();

            string sql = "select top 1 RegionUUID, Revision, Heightfield from terrain where RegionUUID = @RegionUUID order by Revision desc";

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                // MySqlParameter param = new MySqlParameter();
                cmd.Parameters.AddWithValue("@RegionUUID", regionID.ToString());

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    int rev = 0;
                    if (reader.Read())
                    {
                        MemoryStream str = new MemoryStream((byte[])reader["Heightfield"]);
                        BinaryReader br = new BinaryReader(str);
                        for (int x = 0; x < 256; x++)
                        {
                            for (int y = 0; y < 256; y++)
                            {
                                terrain[x, y] = br.ReadDouble();
                            }
                        }
                        rev = (int)reader["Revision"];
                    }
                    else
                    {
                        _Log.Info("[REGION DB]: No terrain found for region");
                        return null;
                    }
                    _Log.Info("[REGION DB]: Loaded terrain revision r" + rev);
                }
            }

            return terrain;
        }

        /// <summary>
        /// Stores the terrain map to DB.
        /// </summary>
        /// <param name="terrain">terrain map data.</param>
        /// <param name="regionID">regionID.</param>
        public void StoreTerrain(double[,] terrain, UUID regionID)
        {
            int revision = Util.UnixTimeSinceEpoch();

            //Delete old terrain map
            string sql = "delete from terrain where RegionUUID=@RegionUUID";
            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.AddWithValue("@RegionUUID", regionID.ToString());
                cmd.ExecuteNonQuery();
            }

            sql = "insert into terrain(RegionUUID, Revision, Heightfield)" +
                               " values(@RegionUUID, @Revision, @Heightfield)";

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.AddWithValue("@RegionUUID", regionID.ToString());
                cmd.Parameters.AddWithValue("@Revision", revision);
                cmd.Parameters.AddWithValue("@Heightfield", serializeTerrain(terrain));
                cmd.ExecuteNonQuery();
            }

            _Log.Info("[REGION DB]: Stored terrain revision r" + revision);
        }

        /// <summary>
        /// Loads all the land objects of a region.
        /// </summary>
        /// <param name="regionUUID">The region UUID.</param>
        /// <returns></returns>
        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            List<LandData> landDataForRegion = new List<LandData>();

            string sql = "select * from land where RegionUUID = @RegionUUID";

            //Retrieve all land data from region
            using (AutoClosingSqlCommand cmdLandData = _Database.Query(sql))
            {
                cmdLandData.Parameters.AddWithValue("@RegionUUID", regionUUID.ToString());

                using (SqlDataReader readerLandData = cmdLandData.ExecuteReader())
                {
                    while (readerLandData.Read())
                    {
                        landDataForRegion.Add(buildLandData(readerLandData));
                    }
                }
            }

            //Retrieve all accesslist data for all landdata
            foreach (LandData landData in landDataForRegion)
            {
                sql = "select * from landaccesslist where LandUUID = @LandUUID";
                using (AutoClosingSqlCommand cmdAccessList = _Database.Query(sql))
                {
                    cmdAccessList.Parameters.AddWithValue("@LandUUID", landData.GlobalID);
                    using (SqlDataReader readerAccessList = cmdAccessList.ExecuteReader())
                    {
                        while (readerAccessList.Read())
                        {
                            landData.ParcelAccessList.Add(buildLandAccessData(readerAccessList));
                        }
                    }
                }
            }

            //Return data
            return landDataForRegion;
        }

        /// <summary>
        /// Stores land object with landaccess list.
        /// </summary>
        /// <param name="parcel">parcel data.</param>
        public void StoreLandObject(ILandObject parcel)
        {
            //As this is only one record in land table I just delete all and then add a new record.
            //As the delete landaccess is already in the mysql code

            //Delete old values
            RemoveLandObject(parcel.landData.GlobalID);

            //Insert new values
            string sql = @"INSERT INTO [land] 
([UUID],[RegionUUID],[LocalLandID],[Bitmap],[Name],[Description],[OwnerUUID],[IsGroupOwned],[Area],[AuctionID],[Category],[ClaimDate],[ClaimPrice],[GroupUUID],[SalePrice],[LandStatus],[LandFlags],[LandingType],[MediaAutoScale],[MediaTextureUUID],[MediaURL],[MusicURL],[PassHours],[PassPrice],[SnapshotUUID],[UserLocationX],[UserLocationY],[UserLocationZ],[UserLookAtX],[UserLookAtY],[UserLookAtZ],[AuthbuyerID])
VALUES
(@UUID,@RegionUUID,@LocalLandID,@Bitmap,@Name,@Description,@OwnerUUID,@IsGroupOwned,@Area,@AuctionID,@Category,@ClaimDate,@ClaimPrice,@GroupUUID,@SalePrice,@LandStatus,@LandFlags,@LandingType,@MediaAutoScale,@MediaTextureUUID,@MediaURL,@MusicURL,@PassHours,@PassPrice,@SnapshotUUID,@UserLocationX,@UserLocationY,@UserLocationZ,@UserLookAtX,@UserLookAtY,@UserLookAtZ,@AuthbuyerID)";

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.AddRange(CreateLandParameters(parcel.landData, parcel.regionUUID));

                cmd.ExecuteNonQuery();
            }

            sql = "INSERT INTO [landaccesslist] ([LandUUID],[AccessUUID],[Flags]) VALUES (@LandUUID,@AccessUUID,@Flags)";

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                foreach (ParcelManager.ParcelAccessEntry parcelAccessEntry in parcel.landData.ParcelAccessList)
                {
                    cmd.Parameters.AddRange(CreateLandAccessParameters(parcelAccessEntry, parcel.regionUUID));

                    cmd.ExecuteNonQuery();

                    cmd.Parameters.Clear();
                }
            }
        }

        /// <summary>
        /// Removes a land object from DB.
        /// </summary>
        /// <param name="globalID">UUID of landobject</param>
        public void RemoveLandObject(UUID globalID)
        {
            using (AutoClosingSqlCommand cmd = _Database.Query("delete from land where UUID=@UUID"))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@UUID", globalID));
                cmd.ExecuteNonQuery();
            }

            using (AutoClosingSqlCommand cmd = _Database.Query("delete from landaccesslist where LandUUID=@UUID"))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@UUID", globalID));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Loads the settings of a region.
        /// </summary>
        /// <param name="regionUUID">The region UUID.</param>
        /// <returns></returns>
        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            string sql = "select * from regionsettings where regionUUID = @regionUUID";
            RegionSettings regionSettings;
            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@regionUUID", regionUUID.ToString()));
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        regionSettings = buildRegionSettings(reader);
                        regionSettings.OnSave += StoreRegionSettings;

                        return regionSettings;
                    }
                }
            }

            //If comes here then there is now region setting for that region
            regionSettings = new RegionSettings();
            regionSettings.RegionUUID = regionUUID;
            regionSettings.OnSave += StoreRegionSettings;

            //Store new values
            StoreNewRegionSettings(regionSettings);

            return regionSettings;
        }

        /// <summary>
        /// Store region settings, need to check if the check is really necesary. If we can make something for creating new region.
        /// </summary>
        /// <param name="regionSettings">region settings.</param>
        public void StoreRegionSettings(RegionSettings regionSettings)
        {
            //Little check if regionUUID already exist in DB
            string regionUUID = null;
            using (AutoClosingSqlCommand cmd = _Database.Query("SELECT regionUUID FROM regionsettings WHERE regionUUID = @regionUUID"))
            {
                cmd.Parameters.Add(_Database.CreateParameter("@regionUUID", regionSettings.RegionUUID));
                regionUUID = cmd.ExecuteScalar().ToString();
            }

            if (string.IsNullOrEmpty(regionUUID))
            {
                StoreNewRegionSettings(regionSettings);
            }
            else
            {
                //This method only updates region settings!!! First call LoadRegionSettings to create new region settings in DB
                string sql =
                    @"UPDATE [regionsettings] SET [block_terraform] = @block_terraform ,[block_fly] = @block_fly ,[allow_damage] = @allow_damage 
,[restrict_pushing] = @restrict_pushing ,[allow_land_resell] = @allow_land_resell ,[allow_land_join_divide] = @allow_land_join_divide 
,[block_show_in_search] = @block_show_in_search ,[agent_limit] = @agent_limit ,[object_bonus] = @object_bonus ,[maturity] = @maturity 
,[disable_scripts] = @disable_scripts ,[disable_collisions] = @disable_collisions ,[disable_physics] = @disable_physics 
,[terrain_texture_1] = @terrain_texture_1 ,[terrain_texture_2] = @terrain_texture_2 ,[terrain_texture_3] = @terrain_texture_3 
,[terrain_texture_4] = @terrain_texture_4 ,[elevation_1_nw] = @elevation_1_nw ,[elevation_2_nw] = @elevation_2_nw 
,[elevation_1_ne] = @elevation_1_ne ,[elevation_2_ne] = @elevation_2_ne ,[elevation_1_se] = @elevation_1_se ,[elevation_2_se] = @elevation_2_se 
,[elevation_1_sw] = @elevation_1_sw ,[elevation_2_sw] = @elevation_2_sw ,[water_height] = @water_height ,[terrain_raise_limit] = @terrain_raise_limit 
,[terrain_lower_limit] = @terrain_lower_limit ,[use_estate_sun] = @use_estate_sun ,[fixed_sun] = @fixed_sun ,[sun_position] = @sun_position 
,[covenant] = @covenant ,[Sandbox] = @Sandbox WHERE [regionUUID] = @regionUUID";

                using (AutoClosingSqlCommand cmd = _Database.Query(sql))
                {
                    cmd.Parameters.AddRange(CreateRegionSettingParameters(regionSettings));

                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Shutdown()
        {
            //Not used??
        }

        #region Private Methods

        /// <summary>
        /// Load in a prim's persisted inventory.
        /// </summary>
        /// <param name="prim">The prim</param>
        private void LoadItems(SceneObjectPart prim)
        {
            DataTable dbItems = _PrimsDataSet.Tables["primitems"];

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

        /// <summary>
        /// Serializes the terrain data for storage in DB.
        /// </summary>
        /// <param name="val">terrain data</param>
        /// <returns></returns>
        private static Array serializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream(65536 * sizeof(double));
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
        /// Stores new regionsettings.
        /// </summary>
        /// <param name="regionSettings">The region settings.</param>
        private void StoreNewRegionSettings(RegionSettings regionSettings)
        {
            string sql = @"INSERT INTO [regionsettings]
([regionUUID],[block_terraform],[block_fly],[allow_damage],[restrict_pushing],[allow_land_resell],[allow_land_join_divide],[block_show_in_search],[agent_limit],[object_bonus],[maturity],[disable_scripts],[disable_collisions],[disable_physics],[terrain_texture_1],[terrain_texture_2],[terrain_texture_3],[terrain_texture_4],[elevation_1_nw],[elevation_2_nw],[elevation_1_ne],[elevation_2_ne],[elevation_1_se],[elevation_2_se],[elevation_1_sw],[elevation_2_sw],[water_height],[terrain_raise_limit],[terrain_lower_limit],[use_estate_sun],[fixed_sun],[sun_position],[covenant],[Sandbox]) VALUES
(@regionUUID,@block_terraform,@block_fly,@allow_damage,@restrict_pushing,@allow_land_resell,@allow_land_join_divide,@block_show_in_search,@agent_limit,@object_bonus,@maturity,@disable_scripts,@disable_collisions,@disable_physics,@terrain_texture_1,@terrain_texture_2,@terrain_texture_3,@terrain_texture_4,@elevation_1_nw,@elevation_2_nw,@elevation_1_ne,@elevation_2_ne,@elevation_1_se,@elevation_2_se,@elevation_1_sw,@elevation_2_sw,@water_height,@terrain_raise_limit,@terrain_lower_limit,@use_estate_sun,@fixed_sun,@sun_position,@covenant,@Sandbox)";

            using (AutoClosingSqlCommand cmd = _Database.Query(sql))
            {
                cmd.Parameters.AddRange(CreateRegionSettingParameters(regionSettings));

                cmd.ExecuteNonQuery();
            }
        }

        #region Private DataRecord conversion methods

        /// <summary>
        /// Builds the region settings from a datarecod.
        /// </summary>
        /// <param name="row">datarecord with regionsettings.</param>
        /// <returns></returns>
        private static RegionSettings buildRegionSettings(IDataRecord row)
        {
            //TODO change this is some more generic code so we doesnt have to change it every time a new field is added?
            RegionSettings newSettings = new RegionSettings();

            newSettings.RegionUUID = new UUID((string)row["regionUUID"]);
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
            newSettings.TerrainTexture1 = new UUID((String)row["terrain_texture_1"]);
            newSettings.TerrainTexture2 = new UUID((String)row["terrain_texture_2"]);
            newSettings.TerrainTexture3 = new UUID((String)row["terrain_texture_3"]);
            newSettings.TerrainTexture4 = new UUID((String)row["terrain_texture_4"]);
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
            newSettings.Covenant = new UUID((String)row["covenant"]);

            return newSettings;
        }

        /// <summary>
        /// Builds the land data from a datarecord.
        /// </summary>
        /// <param name="row">datarecord with land data</param>
        /// <returns></returns>
        private static LandData buildLandData(IDataRecord row)
        {
            LandData newData = new LandData();

            newData.GlobalID = new UUID((String)row["UUID"]);
            newData.LocalID = Convert.ToInt32(row["LocalLandID"]);

            // Bitmap is a byte[512]
            newData.Bitmap = (Byte[])row["Bitmap"];

            newData.Name = (String)row["Name"];
            newData.Description = (String)row["Description"];
            newData.OwnerID = (String)row["OwnerUUID"];
            newData.IsGroupOwned = Convert.ToBoolean(row["IsGroupOwned"]);
            newData.Area = Convert.ToInt32(row["Area"]);
            newData.AuctionID = Convert.ToUInt32(row["AuctionID"]); //Unemplemented
            newData.Category = (Parcel.ParcelCategory)Convert.ToInt32(row["Category"]);
            //Enum libsecondlife.Parcel.ParcelCategory
            newData.ClaimDate = Convert.ToInt32(row["ClaimDate"]);
            newData.ClaimPrice = Convert.ToInt32(row["ClaimPrice"]);
            newData.GroupID = new UUID((String)row["GroupUUID"]);
            newData.SalePrice = Convert.ToInt32(row["SalePrice"]);
            newData.Status = (Parcel.ParcelStatus)Convert.ToInt32(row["LandStatus"]);
            //Enum. libsecondlife.Parcel.ParcelStatus
            newData.Flags = Convert.ToUInt32(row["LandFlags"]);
            newData.LandingType = Convert.ToByte(row["LandingType"]);
            newData.MediaAutoScale = Convert.ToByte(row["MediaAutoScale"]);
            newData.MediaID = new UUID((String)row["MediaTextureUUID"]);
            newData.MediaURL = (String)row["MediaURL"];
            newData.MusicURL = (String)row["MusicURL"];
            newData.PassHours = Convert.ToSingle(row["PassHours"]);
            newData.PassPrice = Convert.ToInt32(row["PassPrice"]);

            UUID authedbuyer;
            UUID snapshotID;

            if (UUID.TryParse((string)row["AuthBuyerID"], out authedbuyer))
                newData.AuthBuyerID = authedbuyer;

            if (UUID.TryParse((string)row["SnapshotUUID"], out snapshotID))
                newData.SnapshotID = snapshotID;

            try
            {
                newData.UserLocation =
                    new Vector3(Convert.ToSingle(row["UserLocationX"]), Convert.ToSingle(row["UserLocationY"]),
                                  Convert.ToSingle(row["UserLocationZ"]));
                newData.UserLookAt =
                    new Vector3(Convert.ToSingle(row["UserLookAtX"]), Convert.ToSingle(row["UserLookAtY"]),
                                  Convert.ToSingle(row["UserLookAtZ"]));
            }
            catch (InvalidCastException)
            {
                newData.UserLocation = Vector3.Zero;
                newData.UserLookAt = Vector3.Zero;
                _Log.ErrorFormat("[PARCEL]: unable to get parcel telehub settings for {1}", newData.Name);
            }

            newData.ParcelAccessList = new List<ParcelManager.ParcelAccessEntry>();

            return newData;
        }

        /// <summary>
        /// Builds the landaccess data from a data record.
        /// </summary>
        /// <param name="row">datarecord with landaccess data</param>
        /// <returns></returns>
        private static ParcelManager.ParcelAccessEntry buildLandAccessData(IDataRecord row)
        {
            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
            entry.AgentID = new UUID((string)row["AccessUUID"]);
            entry.Flags = (ParcelManager.AccessList)Convert.ToInt32(row["Flags"]);
            entry.Time = new DateTime();
            return entry;
        }

        /// <summary>
        /// Builds the prim from a datarecord.
        /// </summary>
        /// <param name="row">datarecord</param>
        /// <returns></returns>
        private static SceneObjectPart buildPrim(DataRow row)
        {
            SceneObjectPart prim = new SceneObjectPart();

            prim.UUID = new UUID((String)row["UUID"]);
            // explicit conversion of integers is required, which sort
            // of sucks.  No idea if there is a shortcut here or not.
            prim.ParentID = Convert.ToUInt32(row["ParentID"]);
            prim.CreationDate = Convert.ToInt32(row["CreationDate"]);
            prim.Name = (String)row["Name"];
            // various text fields
            prim.Text = (String)row["Text"];
            prim.Description = (String)row["Description"];
            prim.SitName = (String)row["SitName"];
            prim.TouchName = (String)row["TouchName"];
            // permissions
            prim.ObjectFlags = Convert.ToUInt32(row["ObjectFlags"]);
            prim.CreatorID = new UUID((String)row["CreatorID"]);
            prim.OwnerID = new UUID((String)row["OwnerID"]);
            prim.GroupID = new UUID((String)row["GroupID"]);
            prim.LastOwnerID = new UUID((String)row["LastOwnerID"]);
            prim.OwnerMask = Convert.ToUInt32(row["OwnerMask"]);
            prim.NextOwnerMask = Convert.ToUInt32(row["NextOwnerMask"]);
            prim.GroupMask = Convert.ToUInt32(row["GroupMask"]);
            prim.EveryoneMask = Convert.ToUInt32(row["EveryoneMask"]);
            prim.BaseMask = Convert.ToUInt32(row["BaseMask"]);
            // vectors
            prim.OffsetPosition = new Vector3(
                Convert.ToSingle(row["PositionX"]),
                Convert.ToSingle(row["PositionY"]),
                Convert.ToSingle(row["PositionZ"])
                );
            prim.GroupPosition = new Vector3(
                Convert.ToSingle(row["GroupPositionX"]),
                Convert.ToSingle(row["GroupPositionY"]),
                Convert.ToSingle(row["GroupPositionZ"])
                );
            prim.Velocity = new Vector3(
                Convert.ToSingle(row["VelocityX"]),
                Convert.ToSingle(row["VelocityY"]),
                Convert.ToSingle(row["VelocityZ"])
                );
            prim.AngularVelocity = new Vector3(
                Convert.ToSingle(row["AngularVelocityX"]),
                Convert.ToSingle(row["AngularVelocityY"]),
                Convert.ToSingle(row["AngularVelocityZ"])
                );
            prim.Acceleration = new Vector3(
                Convert.ToSingle(row["AccelerationX"]),
                Convert.ToSingle(row["AccelerationY"]),
                Convert.ToSingle(row["AccelerationZ"])
                );
            // quaternions
            prim.RotationOffset = new Quaternion(
                Convert.ToSingle(row["RotationX"]),
                Convert.ToSingle(row["RotationY"]),
                Convert.ToSingle(row["RotationZ"]),
                Convert.ToSingle(row["RotationW"])
                );
            prim.SitTargetPositionLL = new Vector3(
                Convert.ToSingle(row["SitTargetOffsetX"]),
                Convert.ToSingle(row["SitTargetOffsetY"]),
                Convert.ToSingle(row["SitTargetOffsetZ"])
                );
            prim.SitTargetOrientationLL = new Quaternion(
                Convert.ToSingle(row["SitTargetOrientX"]),
                Convert.ToSingle(row["SitTargetOrientY"]),
                Convert.ToSingle(row["SitTargetOrientZ"]),
                Convert.ToSingle(row["SitTargetOrientW"])
                );

            prim.PayPrice[0] = Convert.ToInt32(row["PayPrice"]);
            prim.PayPrice[1] = Convert.ToInt32(row["PayButton1"]);
            prim.PayPrice[2] = Convert.ToInt32(row["PayButton2"]);
            prim.PayPrice[3] = Convert.ToInt32(row["PayButton3"]);
            prim.PayPrice[4] = Convert.ToInt32(row["PayButton4"]);

            prim.Sound = new UUID(row["LoopedSound"].ToString());
            prim.SoundGain = Convert.ToSingle(row["LoopedSoundGain"]);
            prim.SoundFlags = 1; // If it's persisted at all, it's looped

            if (row["TextureAnimation"] != null && row["TextureAnimation"] != DBNull.Value)
                prim.TextureAnimation = (Byte[])row["TextureAnimation"];

            prim.RotationalVelocity = new Vector3(
                Convert.ToSingle(row["OmegaX"]),
                Convert.ToSingle(row["OmegaY"]),
                Convert.ToSingle(row["OmegaZ"])
                );

            // TODO: Rotation
            // OmegaX, OmegaY, OmegaZ

            prim.SetCameraEyeOffset(new Vector3(
                                        Convert.ToSingle(row["CameraEyeOffsetX"]),
                                        Convert.ToSingle(row["CameraEyeOffsetY"]),
                                        Convert.ToSingle(row["CameraEyeOffsetZ"])
                                        ));

            prim.SetCameraAtOffset(new Vector3(
                                       Convert.ToSingle(row["CameraAtOffsetX"]),
                                       Convert.ToSingle(row["CameraAtOffsetY"]),
                                       Convert.ToSingle(row["CameraAtOffsetZ"])
                                       ));

            if (Convert.ToInt16(row["ForceMouselook"]) != 0)
                prim.SetForceMouselook(true);

            prim.ScriptAccessPin = Convert.ToInt32(row["ScriptAccessPin"]);

            if (Convert.ToInt16(row["AllowedDrop"]) != 0)
                prim.AllowedDrop = true;

            if (Convert.ToInt16(row["DieAtEdge"]) != 0)
                prim.DIE_AT_EDGE = true;

            prim.SalePrice = Convert.ToInt32(row["SalePrice"]);
            prim.ObjectSaleType = Convert.ToByte(row["SaleType"]);

            return prim;
        }

        /// <summary>
        /// Builds the prim shape from a datarecord.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns></returns>
        private static PrimitiveBaseShape buildShape(DataRow row)
        {
            PrimitiveBaseShape s = new PrimitiveBaseShape();
            s.Scale = new Vector3(
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

            byte[] textureEntry = (byte[])row["Texture"];
            s.TextureEntry = textureEntry;

            s.ExtraParams = (byte[])row["ExtraParams"];

            try
            {
                s.State = Convert.ToByte(row["State"]);
            }
            catch (InvalidCastException)
            {
            }

            return s;
        }

        /// <summary>
        /// Build a prim inventory item from the persisted data.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        private static TaskInventoryItem buildItem(DataRow row)
        {
            TaskInventoryItem taskItem = new TaskInventoryItem();

            taskItem.ItemID = new UUID((String)row["itemID"]);
            taskItem.ParentPartID = new UUID((String)row["primID"]);
            taskItem.AssetID = new UUID((String)row["assetID"]);
            taskItem.ParentID = new UUID((String)row["parentFolderID"]);

            taskItem.InvType = Convert.ToInt32(row["invType"]);
            taskItem.Type = Convert.ToInt32(row["assetType"]);

            taskItem.Name = (String)row["name"];
            taskItem.Description = (String)row["description"];
            taskItem.CreationDate = Convert.ToUInt32(row["creationDate"]);
            taskItem.CreatorID = new UUID((String)row["creatorID"]);
            taskItem.OwnerID = new UUID((String)row["ownerID"]);
            taskItem.LastOwnerID = new UUID((String)row["lastOwnerID"]);
            taskItem.GroupID = new UUID((String)row["groupID"]);

            taskItem.NextPermissions = Convert.ToUInt32(row["nextPermissions"]);
            taskItem.CurrentPermissions = Convert.ToUInt32(row["currentPermissions"]);
            taskItem.BasePermissions = Convert.ToUInt32(row["basePermissions"]);
            taskItem.EveryonePermissions = Convert.ToUInt32(row["everyonePermissions"]);
            taskItem.GroupPermissions = Convert.ToUInt32(row["groupPermissions"]);
            taskItem.Flags = Convert.ToUInt32(row["flags"]);

            return taskItem;
        }

        #endregion

        #region Create parameters methods

        /// <summary>
        /// Creates the prim inventory parameters.
        /// </summary>
        /// <param name="taskItem">item in inventory.</param>
        /// <returns></returns>
        private SqlParameter[] CreatePrimInventoryParameters(TaskInventoryItem taskItem)
        {
            SqlParameter[] parameters = new SqlParameter[19];

            parameters[0] = _Database.CreateParameter("itemID", taskItem.ItemID);
            parameters[1] = _Database.CreateParameter("primID", taskItem.ParentPartID);
            parameters[2] = _Database.CreateParameter("assetID", taskItem.AssetID);
            parameters[3] = _Database.CreateParameter("parentFolderID", taskItem.ParentID);

            parameters[4] = _Database.CreateParameter("invType", taskItem.InvType);
            parameters[5] = _Database.CreateParameter("assetType", taskItem.Type);

            parameters[6] = _Database.CreateParameter("name", taskItem.Name);
            parameters[7] = _Database.CreateParameter("description", taskItem.Description);
            parameters[8] = _Database.CreateParameter("creationDate", taskItem.CreationDate);
            parameters[9] = _Database.CreateParameter("creatorID", taskItem.CreatorID);
            parameters[10] = _Database.CreateParameter("ownerID", taskItem.OwnerID);
            parameters[11] = _Database.CreateParameter("lastOwnerID", taskItem.LastOwnerID);
            parameters[12] = _Database.CreateParameter("groupID", taskItem.GroupID);
            parameters[13] = _Database.CreateParameter("nextPermissions", taskItem.NextPermissions);
            parameters[14] = _Database.CreateParameter("currentPermissions", taskItem.CurrentPermissions);
            parameters[15] = _Database.CreateParameter("basePermissions", taskItem.BasePermissions);
            parameters[16] = _Database.CreateParameter("everyonePermissions", taskItem.EveryonePermissions);
            parameters[17] = _Database.CreateParameter("groupPermissions", taskItem.GroupPermissions);
            parameters[18] = _Database.CreateParameter("flags", taskItem.Flags);

            return parameters;
        }

        /// <summary>
        /// Creates the region setting parameters.
        /// </summary>
        /// <param name="settings">regionsettings.</param>
        /// <returns></returns>
        private SqlParameter[] CreateRegionSettingParameters(RegionSettings settings)
        {
            SqlParameter[] parameters = new SqlParameter[34];

            parameters[0] = _Database.CreateParameter("regionUUID", settings.RegionUUID);
            parameters[1] = _Database.CreateParameter("block_terraform", settings.BlockTerraform);
            parameters[2] = _Database.CreateParameter("block_fly", settings.BlockFly);
            parameters[3] = _Database.CreateParameter("allow_damage", settings.AllowDamage);
            parameters[4] = _Database.CreateParameter("restrict_pushing", settings.RestrictPushing);
            parameters[5] = _Database.CreateParameter("allow_land_resell", settings.AllowLandResell);
            parameters[6] = _Database.CreateParameter("allow_land_join_divide", settings.AllowLandJoinDivide);
            parameters[7] = _Database.CreateParameter("block_show_in_search", settings.BlockShowInSearch);
            parameters[8] = _Database.CreateParameter("agent_limit", settings.AgentLimit);
            parameters[9] = _Database.CreateParameter("object_bonus", settings.ObjectBonus);
            parameters[10] = _Database.CreateParameter("maturity", settings.Maturity);
            parameters[11] = _Database.CreateParameter("disable_scripts", settings.DisableScripts);
            parameters[12] = _Database.CreateParameter("disable_collisions", settings.DisableCollisions);
            parameters[13] = _Database.CreateParameter("disable_physics", settings.DisablePhysics);
            parameters[14] = _Database.CreateParameter("terrain_texture_1", settings.TerrainTexture1);
            parameters[15] = _Database.CreateParameter("terrain_texture_2", settings.TerrainTexture2);
            parameters[16] = _Database.CreateParameter("terrain_texture_3", settings.TerrainTexture3);
            parameters[17] = _Database.CreateParameter("terrain_texture_4", settings.TerrainTexture4);
            parameters[18] = _Database.CreateParameter("elevation_1_nw", settings.Elevation1NW);
            parameters[19] = _Database.CreateParameter("elevation_2_nw", settings.Elevation2NW);
            parameters[20] = _Database.CreateParameter("elevation_1_ne", settings.Elevation1NE);
            parameters[21] = _Database.CreateParameter("elevation_2_ne", settings.Elevation2NE);
            parameters[22] = _Database.CreateParameter("elevation_1_se", settings.Elevation1SE);
            parameters[23] = _Database.CreateParameter("elevation_2_se", settings.Elevation2SE);
            parameters[24] = _Database.CreateParameter("elevation_1_sw", settings.Elevation1SW);
            parameters[25] = _Database.CreateParameter("elevation_2_sw", settings.Elevation2SW);
            parameters[26] = _Database.CreateParameter("water_height", settings.WaterHeight);
            parameters[27] = _Database.CreateParameter("terrain_raise_limit", settings.TerrainRaiseLimit);
            parameters[28] = _Database.CreateParameter("terrain_lower_limit", settings.TerrainLowerLimit);
            parameters[29] = _Database.CreateParameter("use_estate_sun", settings.UseEstateSun);
            parameters[30] = _Database.CreateParameter("sandbox", settings.Sandbox);
            parameters[31] = _Database.CreateParameter("fixed_sun", settings.FixedSun);
            parameters[32] = _Database.CreateParameter("sun_position", settings.SunPosition);
            parameters[33] = _Database.CreateParameter("covenant", settings.Covenant);

            return parameters;
        }

        /// <summary>
        /// Creates the land parameters.
        /// </summary>
        /// <param name="land">land parameters.</param>
        /// <param name="regionUUID">region UUID.</param>
        /// <returns></returns>
        private SqlParameter[] CreateLandParameters(LandData land, UUID regionUUID)
        {
            SqlParameter[] parameters = new SqlParameter[32];

            parameters[0] = _Database.CreateParameter("UUID", land.GlobalID);
            parameters[1] = _Database.CreateParameter("RegionUUID", regionUUID);
            parameters[2] = _Database.CreateParameter("LocalLandID", land.LocalID);

            // Bitmap is a byte[512]
            parameters[3] = _Database.CreateParameter("Bitmap", land.Bitmap);

            parameters[4] = _Database.CreateParameter("Name", land.Name);
            parameters[5] = _Database.CreateParameter("Description", land.Description);
            parameters[6] = _Database.CreateParameter("OwnerUUID", land.OwnerID);
            parameters[7] = _Database.CreateParameter("IsGroupOwned", land.IsGroupOwned);
            parameters[8] = _Database.CreateParameter("Area", land.Area);
            parameters[9] = _Database.CreateParameter("AuctionID", land.AuctionID); //Unemplemented
            parameters[10] = _Database.CreateParameter("Category", (int)land.Category); //Enum libsecondlife.Parcel.ParcelCategory
            parameters[11] = _Database.CreateParameter("ClaimDate", land.ClaimDate);
            parameters[12] = _Database.CreateParameter("ClaimPrice", land.ClaimPrice);
            parameters[13] = _Database.CreateParameter("GroupUUID", land.GroupID);
            parameters[14] = _Database.CreateParameter("SalePrice", land.SalePrice);
            parameters[15] = _Database.CreateParameter("LandStatus", (int)land.Status); //Enum. libsecondlife.Parcel.ParcelStatus
            parameters[16] = _Database.CreateParameter("LandFlags", land.Flags);
            parameters[17] = _Database.CreateParameter("LandingType", land.LandingType);
            parameters[18] = _Database.CreateParameter("MediaAutoScale", land.MediaAutoScale);
            parameters[19] = _Database.CreateParameter("MediaTextureUUID", land.MediaID);
            parameters[20] = _Database.CreateParameter("MediaURL", land.MediaURL);
            parameters[21] = _Database.CreateParameter("MusicURL", land.MusicURL);
            parameters[22] = _Database.CreateParameter("PassHours", land.PassHours);
            parameters[23] = _Database.CreateParameter("PassPrice", land.PassPrice);
            parameters[24] = _Database.CreateParameter("SnapshotUUID", land.SnapshotID);
            parameters[25] = _Database.CreateParameter("UserLocationX", land.UserLocation.X);
            parameters[26] = _Database.CreateParameter("UserLocationY", land.UserLocation.Y);
            parameters[27] = _Database.CreateParameter("UserLocationZ", land.UserLocation.Z);
            parameters[28] = _Database.CreateParameter("UserLookAtX", land.UserLookAt.X);
            parameters[29] = _Database.CreateParameter("UserLookAtY", land.UserLookAt.Y);
            parameters[30] = _Database.CreateParameter("UserLookAtZ", land.UserLookAt.Z);
            parameters[31] = _Database.CreateParameter("AuthBuyerID", land.AuthBuyerID);

            return parameters;
        }

        /// <summary>
        /// Creates the land access parameters.
        /// </summary>
        /// <param name="parcelAccessEntry">parcel access entry.</param>
        /// <param name="parcelID">parcel ID.</param>
        /// <returns></returns>
        private SqlParameter[] CreateLandAccessParameters(ParcelManager.ParcelAccessEntry parcelAccessEntry, UUID parcelID)
        {
            SqlParameter[] parameters = new SqlParameter[3];

            parameters[0] = _Database.CreateParameter("LandUUID", parcelID);
            parameters[1] = _Database.CreateParameter("AccessUUID", parcelAccessEntry.AgentID);
            parameters[2] = _Database.CreateParameter("Flags", parcelAccessEntry.Flags);

            return parameters;
        }

        /// <summary>
        /// Fills/Updates the prim datarow.
        /// </summary>
        /// <param name="row">datarow.</param>
        /// <param name="prim">prim data.</param>
        /// <param name="sceneGroupID">scenegroup ID.</param>
        /// <param name="regionUUID">regionUUID.</param>
        private static void fillPrimRow(DataRow row, SceneObjectPart prim, UUID sceneGroupID, UUID regionUUID)
        {
            row["UUID"] = prim.UUID.ToString();
            row["RegionUUID"] = regionUUID.ToString();
            row["ParentID"] = prim.ParentID;
            row["CreationDate"] = prim.CreationDate;
            row["Name"] = prim.Name;
            row["SceneGroupID"] = sceneGroupID.ToString();
            // the UUID of the root part for this SceneObjectGroup
            // various text fields
            row["Text"] = prim.Text;
            row["Description"] = prim.Description;
            row["SitName"] = prim.SitName;
            row["TouchName"] = prim.TouchName;
            // permissions
            row["ObjectFlags"] = prim.ObjectFlags;
            row["CreatorID"] = prim.CreatorID.ToString();
            row["OwnerID"] = prim.OwnerID.ToString();
            row["GroupID"] = prim.GroupID.ToString();
            row["LastOwnerID"] = prim.LastOwnerID.ToString();
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
            Vector3 sitTargetPos = prim.SitTargetPositionLL;
            row["SitTargetOffsetX"] = sitTargetPos.X;
            row["SitTargetOffsetY"] = sitTargetPos.Y;
            row["SitTargetOffsetZ"] = sitTargetPos.Z;

            Quaternion sitTargetOrient = prim.SitTargetOrientationLL;
            row["SitTargetOrientW"] = sitTargetOrient.W;
            row["SitTargetOrientX"] = sitTargetOrient.X;
            row["SitTargetOrientY"] = sitTargetOrient.Y;
            row["SitTargetOrientZ"] = sitTargetOrient.Z;

            row["PayPrice"] = prim.PayPrice[0];
            row["PayButton1"] = prim.PayPrice[1];
            row["PayButton2"] = prim.PayPrice[2];
            row["PayButton3"] = prim.PayPrice[3];
            row["PayButton4"] = prim.PayPrice[4];

            if ((prim.SoundFlags & 1) != 0) // Looped
            {
                row["LoopedSound"] = prim.Sound.ToString();
                row["LoopedSoundGain"] = prim.SoundGain;
            }
            else
            {
                row["LoopedSound"] = UUID.Zero;
                row["LoopedSoundGain"] = 0.0f;
            }

            row["TextureAnimation"] = prim.TextureAnimation;

            row["OmegaX"] = prim.RotationalVelocity.X;
            row["OmegaY"] = prim.RotationalVelocity.Y;
            row["OmegaZ"] = prim.RotationalVelocity.Z;

            row["CameraEyeOffsetX"] = prim.GetCameraEyeOffset().X;
            row["CameraEyeOffsetY"] = prim.GetCameraEyeOffset().Y;
            row["CameraEyeOffsetZ"] = prim.GetCameraEyeOffset().Z;

            row["CameraAtOffsetX"] = prim.GetCameraAtOffset().X;
            row["CameraAtOffsetY"] = prim.GetCameraAtOffset().Y;
            row["CameraAtOffsetZ"] = prim.GetCameraAtOffset().Z;

            if (prim.GetForceMouselook())
                row["ForceMouselook"] = 1;
            else
                row["ForceMouselook"] = 0;

            row["ScriptAccessPin"] = prim.ScriptAccessPin;

            if (prim.AllowedDrop)
                row["AllowedDrop"] = 1;
            else
                row["AllowedDrop"] = 0;

            if (prim.DIE_AT_EDGE)
                row["DieAtEdge"] = 1;
            else
                row["DieAtEdge"] = 0;

            row["SalePrice"] = prim.SalePrice;
            row["SaleType"] = Convert.ToInt16(prim.ObjectSaleType);
        }

        /// <summary>
        /// Fills/Updates the shape datarow.
        /// </summary>
        /// <param name="row">datarow to fill/update.</param>
        /// <param name="prim">prim shape data.</param>
        private static void fillShapeRow(DataRow row, SceneObjectPart prim)
        {
            PrimitiveBaseShape s = prim.Shape;
            row["UUID"] = prim.UUID.ToString();
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
            row["State"] = s.State;
        }

        #endregion

        private void RetrievePrimsDataForRegion(UUID regionUUID, UUID sceneGroupID, string primID)
        {
            using (SqlConnection connection = _Database.DatabaseConnection())
            {
                _PrimDataAdapter.SelectCommand.Connection = connection;
                _PrimDataAdapter.SelectCommand.Parameters["@RegionUUID"].Value = regionUUID.ToString();
                if (sceneGroupID != UUID.Zero)
                    _PrimDataAdapter.SelectCommand.Parameters["@SceneGroupID"].Value = sceneGroupID.ToString();
                else
                    _PrimDataAdapter.SelectCommand.Parameters["@SceneGroupID"].Value = "%";
                _PrimDataAdapter.SelectCommand.Parameters["@UUID"].Value = primID;

                _PrimDataAdapter.Fill(_PrimsDataSet, "prims");

                _ShapeDataAdapter.SelectCommand.Connection = connection;
                _ShapeDataAdapter.SelectCommand.Parameters["@RegionUUID"].Value = regionUUID.ToString();
                if (sceneGroupID != UUID.Zero)
                    _ShapeDataAdapter.SelectCommand.Parameters["@SceneGroupID"].Value = sceneGroupID.ToString();
                else
                    _ShapeDataAdapter.SelectCommand.Parameters["@SceneGroupID"].Value = "%";
                _ShapeDataAdapter.SelectCommand.Parameters["@UUID"].Value = primID;

                _ShapeDataAdapter.Fill(_PrimsDataSet, "primshapes");

                _ItemsDataAdapter.SelectCommand.Connection = connection;
                _ItemsDataAdapter.SelectCommand.Parameters["@RegionUUID"].Value = regionUUID.ToString();
                if (sceneGroupID != UUID.Zero)
                    _ItemsDataAdapter.SelectCommand.Parameters["@SceneGroupID"].Value = sceneGroupID.ToString();
                else
                    _ItemsDataAdapter.SelectCommand.Parameters["@SceneGroupID"].Value = "%";
                _ItemsDataAdapter.SelectCommand.Parameters["@UUID"].Value = primID;

                _ItemsDataAdapter.Fill(_PrimsDataSet, "primitems");
            }
        }

        private void CommitDataSet()
        {
            lock (_PrimsDataSet)
            {
                using (SqlConnection connection = _Database.DatabaseConnection())
                {
                    _PrimDataAdapter.InsertCommand.Connection = connection;
                    _PrimDataAdapter.UpdateCommand.Connection = connection;
                    _PrimDataAdapter.DeleteCommand.Connection = connection;

                    _ShapeDataAdapter.InsertCommand.Connection = connection;
                    _ShapeDataAdapter.UpdateCommand.Connection = connection;
                    _ShapeDataAdapter.DeleteCommand.Connection = connection;

                    _ItemsDataAdapter.InsertCommand.Connection = connection;
                    _ItemsDataAdapter.UpdateCommand.Connection = connection;
                    _ItemsDataAdapter.DeleteCommand.Connection = connection;

                    _PrimDataAdapter.Update(_PrimsDataSet.Tables["prims"]);
                    _ShapeDataAdapter.Update(_PrimsDataSet.Tables["primshapes"]);
                    _ItemsDataAdapter.Update(_PrimsDataSet.Tables["primitems"]);

                    _PrimsDataSet.AcceptChanges();

                    _PrimsDataSet.Tables["prims"].Clear();
                    _PrimsDataSet.Tables["primshapes"].Clear();
                    _PrimsDataSet.Tables["primitems"].Clear();
                }
            }
        }

        private static void SetupCommands(SqlDataAdapter dataAdapter)
        {
            SqlCommandBuilder commandBuilder = new SqlCommandBuilder(dataAdapter);

            dataAdapter.InsertCommand = commandBuilder.GetInsertCommand(true);
            dataAdapter.UpdateCommand = commandBuilder.GetUpdateCommand(true);
            dataAdapter.DeleteCommand = commandBuilder.GetDeleteCommand(true);
        }
        #endregion
    }
}
