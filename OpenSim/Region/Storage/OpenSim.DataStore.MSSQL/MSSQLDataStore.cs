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
using libsecondlife;
using log4net;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.DataStore.MSSQL
{
    public class MSSQLDataStore : IRegionDataStore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string primSelect = "select * from prims";
        private const string shapeSelect = "select * from primshapes";
        private const string terrainSelect = "select * from terrain";

        private DataSet ds;
        private SqlDataAdapter primDa;
        private SqlDataAdapter shapeDa;
        private SqlDataAdapter terrainDa;

        // see IRegionDataStore
        public void Initialise(string dbfile, bool persistPrimInventories)
        {
            IniFile GridDataMySqlFile = new IniFile("mssql_connection.ini");
            string settingDataSource = GridDataMySqlFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = GridDataMySqlFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = GridDataMySqlFile.ParseFileReadValue("persist_security_info");
            string settingUserId = GridDataMySqlFile.ParseFileReadValue("user_id");
            string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");

            string connectionString = "Data Source=" + settingDataSource + ";Initial Catalog=" + settingInitialCatalog +
                                      ";Persist Security Info=" + settingPersistSecurityInfo + ";User ID=" +
                                      settingUserId + ";Password=" + settingPassword + ";";

            ds = new DataSet();

            m_log.Info("[DATASTORE]: MSSQL - connecting: " + settingInitialCatalog);
            SqlConnection conn = new SqlConnection(connectionString);
            SqlCommand primSelectCmd = new SqlCommand(primSelect, conn);
            primDa = new SqlDataAdapter(primSelectCmd);
            //            SqlCommandBuilder primCb = new SqlCommandBuilder(primDa);

            SqlCommand shapeSelectCmd = new SqlCommand(shapeSelect, conn);
            shapeDa = new SqlDataAdapter(shapeSelectCmd);
            // SqlCommandBuilder shapeCb = new SqlCommandBuilder(shapeDa);

            SqlCommand terrainSelectCmd = new SqlCommand(terrainSelect, conn);
            terrainDa = new SqlDataAdapter(terrainSelectCmd);


            // We fill the data set, now we've got copies in memory for the information
            // TODO: see if the linkage actually holds.
            // primDa.FillSchema(ds, SchemaType.Source, "PrimSchema");
            TestTables(conn);

            lock (ds)
            {
                ds.Tables.Add(createPrimTable());
                setupPrimCommands(primDa, conn);
                primDa.Fill(ds.Tables["prims"]);

                ds.Tables.Add(createShapeTable());
                setupShapeCommands(shapeDa, conn);

                ds.Tables.Add(createTerrainTable());
                setupTerrainCommands(terrainDa, conn);

                // WORKAROUND: This is a work around for Sql on
                // windows, which gets really unhappy with blob columns
                // that have no sample data in them.  At some point we
                // need to actually find a proper way to handle this.
                try
                {
                    shapeDa.Fill(ds.Tables["primshapes"]);
                }
                catch (Exception)
                {
                    m_log.Info("[DATASTORE]: Caught fill error on primshapes table");
                }
                try
                {
                    terrainDa.Fill(ds.Tables["terrain"]);
                }
                catch (Exception)
                {
                    m_log.Info("[DATASTORE]: Caught fill error on terrain table");
                }
                return;
            }
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
        }

        public RegionSettings LoadRegionSettings(LLUUID regionUUID)
        {
            return null;
        }

        public void StoreObject(SceneObjectGroup obj, LLUUID regionUUID)
        {
            lock (ds)
            {
                foreach (SceneObjectPart prim in obj.Children.Values)
                {
                    //m_log.Info("[DATASTORE]: Adding obj: " + obj.UUID + " to region: " + regionUUID);
                    addPrim(prim, obj.UUID, regionUUID);
                }
            }

            Commit();
            // m_log.Info("Dump of prims:", ds.GetXml());
        }

        public void RemoveObject(LLUUID obj, LLUUID regionUUID)
        {
            m_log.InfoFormat("[DATASTORE]: Removing obj: {0} from region: {1}", obj.UUID, regionUUID);

            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

            string selectExp = "SceneGroupID = '" + obj.ToString() + "'";
            lock (ds)
            {
                DataRow[] primRows = prims.Select(selectExp);
                foreach (DataRow row in primRows)
                {
                    LLUUID uuid = new LLUUID((string) row["UUID"]);
                    DataRow shapeRow = shapes.Rows.Find(uuid);
                    if (shapeRow != null)
                    {
                        shapeRow.Delete();
                    }
                    row.Delete();
                }
            }

            Commit();
        }

        public List<SceneObjectGroup> LoadObjects(LLUUID regionUUID)
        {
            Dictionary<LLUUID, SceneObjectGroup> createdObjects = new Dictionary<LLUUID, SceneObjectGroup>();

            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

            string byRegion = "RegionUUID = '" + regionUUID.ToString() + "'";
            string orderByParent = "ParentID ASC";

            lock (ds)
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
                        if (uuid == objID) //is new SceneObjectGroup ?
                        {
                            SceneObjectGroup group = new SceneObjectGroup();
                            SceneObjectPart prim = buildPrim(primRow);
                            DataRow shapeRow = shapes.Rows.Find(prim.UUID);
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
                            SceneObjectPart prim = buildPrim(primRow);
                            DataRow shapeRow = shapes.Rows.Find(prim.UUID);
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


        public void StoreTerrain(double[,] ter, LLUUID regionID)
        {
            int revision = Util.UnixTimeSinceEpoch();

            m_log.Info("[DATASTORE]: Storing terrain revision r" + revision.ToString());

            DataTable terrain = ds.Tables["terrain"];
            lock (ds)
            {
                DataRow newrow = terrain.NewRow();
                fillTerrainRow(newrow, regionID, revision, ter);
                terrain.Rows.Add(newrow);

                Commit();
            }
        }

        public double[,] LoadTerrain(LLUUID regionID)
        {
            double[,] terret = new double[256,256];
            terret.Initialize();

            DataTable terrain = ds.Tables["terrain"];

            lock (ds)
            {
                DataRow[] rows = terrain.Select("RegionUUID = '" + regionID.ToString() + "'", "Revision DESC");

                int rev = 0;

                if (rows.Length > 0)
                {
                    DataRow row = rows[0];

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
                    m_log.Info("[DATASTORE]: No terrain found for region");
                    return null;
                }


                m_log.Info("[DATASTORE]: Loaded terrain revision r" + rev.ToString());
            }

            return terret;
        }

        public void RemoveLandObject(LLUUID globalID)
        {
        }

        public void StoreLandObject(ILandObject parcel)
        {
        }

        public List<LandData> LoadLandObjects(LLUUID regionUUID)
        {
            return new List<LandData>();
        }

        public void Commit()
        {
            lock (ds)
            {
                primDa.Update(ds, "prims");
                shapeDa.Update(ds, "primshapes");
                terrainDa.Update(ds, "terrain");
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

        private static void createCol(DataTable dt, string name, Type type)
        {
            DataColumn col = new DataColumn(name, type);
            dt.Columns.Add(col);
        }

        private static DataTable createTerrainTable()
        {
            DataTable terrain = new DataTable("terrain");

            createCol(terrain, "RegionUUID", typeof (String));
            createCol(terrain, "Revision", typeof (Int32));
            createCol(terrain, "Heightfield", typeof (Byte[]));

            return terrain;
        }

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
            // text TODO: this isn't right, but I'm not sure the right
            // way to specify this as a blob atm
            createCol(shapes, "Texture", typeof (Byte[]));
            createCol(shapes, "ExtraParams", typeof (Byte[]));

            shapes.PrimaryKey = new DataColumn[] {shapes.Columns["UUID"]};

            return shapes;
        }

        /***********************************************************************
         *
         *  Convert between ADO.NET <=> OpenSim Objects
         *
         *  These should be database independant
         *
         **********************************************************************/

        private static SceneObjectPart buildPrim(DataRow row)
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
                // Database table was created before we got here and now has null values :P
            }

            return prim;
        }


        private static void fillPrimRow(DataRow row, SceneObjectPart prim, LLUUID sceneGroupID, LLUUID regionUUID)
        {
            row["UUID"] = prim.UUID;
            row["RegionUUID"] = regionUUID;
            row["ParentID"] = prim.ParentID;
            row["CreationDate"] = prim.CreationDate;
            row["Name"] = prim.Name;
            row["SceneGroupID"] = sceneGroupID; // the UUID of the root part for this SceneObjectGroup
            // various text fields
            row["Text"] = prim.Text;
            row["Description"] = prim.Description;
            row["SitName"] = prim.SitName;
            row["TouchName"] = prim.TouchName;
            // permissions
            row["ObjectFlags"] = prim.ObjectFlags;
            row["CreatorID"] = prim.CreatorID;
            row["OwnerID"] = prim.OwnerID;
            row["GroupID"] = prim.GroupID;
            row["LastOwnerID"] = prim.LastOwnerID;
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
            catch (Exception)
            {
                // TODO: Add Sit Target Rows!
            }
        }

        private static PrimitiveBaseShape buildShape(DataRow row)
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

            return s;
        }

        private static void fillShapeRow(DataRow row, SceneObjectPart prim)
        {
            PrimitiveBaseShape s = prim.Shape;
            row["UUID"] = prim.UUID;
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
            // text TODO: this isn't right] = but I'm not sure the right
            // way to specify this as a blob atm

            // And I couldn't work out how to save binary data either
            // seems that the texture colum is being treated as a string in the Datarow
            // if you do a .getType() on it, it returns string, while the other columns return correct type
            // MW[10-08-07]
            // Added following xml hack but not really ideal , also ExtraParams isn't currently part of the database
            // am a bit worried about adding it now as some people will have old format databases, so for now including that data in this xml data
            // MW[17-08-07]
            row["Texture"] = s.TextureEntry;
            row["ExtraParams"] = s.ExtraParams;
            // TextureBlock textureBlock = new TextureBlock(s.TextureEntry);
            //             textureBlock.ExtraParams = s.ExtraParams;
            //             System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            // row["Texture"] = encoding.GetBytes(textureBlock.ToXMLString());
        }

        private void addPrim(SceneObjectPart prim, LLUUID sceneGroupID, LLUUID regionUUID)
        {
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["primshapes"];

            DataRow primRow = prims.Rows.Find(prim.UUID);
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

            DataRow shapeRow = shapes.Rows.Find(prim.UUID);
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
            // No implementation yet
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

        private static SqlCommand createInsertCommand(string table, DataTable dt)
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
            sql += ") values (@";
            sql += String.Join(", @", cols);
            sql += ")";
            SqlCommand cmd = new SqlCommand(sql);
            // this provides the binding for all our parameters, so
            // much less code than it used to be
            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createSqlParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }

        private static SqlCommand createUpdateCommand(string table, string pk, DataTable dt)
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
                subsql += col.ColumnName + "= @" + col.ColumnName;
            }
            sql += subsql;
            sql += " where " + pk;
            SqlCommand cmd = new SqlCommand(sql);
            // this provides the binding for all our parameters, so
            // much less code than it used to be

            foreach (DataColumn col in dt.Columns)
            {
                cmd.Parameters.Add(createSqlParameter(col.ColumnName, col.DataType));
            }
            return cmd;
        }


        private static string defineTable(DataTable dt)
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
                subsql += col.ColumnName + " " + SqlType(col.DataType);
                if (dt.PrimaryKey.Length > 0 && col == dt.PrimaryKey[0])
                {
                    subsql += " primary key";
                }
            }
            sql += subsql;
            sql += ")";

            return sql;
        }

        private static void fillTerrainRow(DataRow row, LLUUID regionUUID, int rev, double[,] val)
        {
            row["RegionUUID"] = regionUUID;
            row["Revision"] = rev;

            MemoryStream str = new MemoryStream(65536*sizeof (double));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < 256; x++)
                for (int y = 0; y < 256; y++)
                    bw.Write(val[x, y]);

            row["Heightfield"] = str.ToArray();
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
        /// lines for defining SqlParameters to 2 parameters:
        /// column name and database type.
        ///
        /// It assumes certain conventions like :param as the param
        /// name to replace in parametrized queries, and that source
        /// version is always current version, both of which are fine
        /// for us.
        ///</summary>
        ///<returns>a built Sql parameter</returns>
        private static SqlParameter createSqlParameter(string name, Type type)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = "@" + name;
            param.DbType = dbtypeFromType(type);
            param.SourceColumn = name;
            param.SourceVersion = DataRowVersion.Current;
            return param;
        }

        private void setupPrimCommands(SqlDataAdapter da, SqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("prims", ds.Tables["prims"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("prims", "UUID=@UUID", ds.Tables["prims"]);
            da.UpdateCommand.Connection = conn;

            SqlCommand delete = new SqlCommand("delete from prims where UUID = @UUID");
            delete.Parameters.Add(createSqlParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void setupShapeCommands(SqlDataAdapter da, SqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("primshapes", ds.Tables["primshapes"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primshapes", "UUID=@UUID", ds.Tables["primshapes"]);
            da.UpdateCommand.Connection = conn;

            SqlCommand delete = new SqlCommand("delete from primshapes where UUID = @UUID");
            delete.Parameters.Add(createSqlParameter("UUID", typeof (String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void setupTerrainCommands(SqlDataAdapter da, SqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("terrain", ds.Tables["terrain"]);
            da.InsertCommand.Connection = conn;
        }

        private static void InitDB(SqlConnection conn)
        {
            string createPrims = defineTable(createPrimTable());
            string createShapes = defineTable(createShapeTable());
            string createTerrain = defineTable(createTerrainTable());

            SqlCommand pcmd = new SqlCommand(createPrims, conn);
            SqlCommand scmd = new SqlCommand(createShapes, conn);
            SqlCommand tcmd = new SqlCommand(createTerrain, conn);
            conn.Open();
            try
            {
                pcmd.ExecuteNonQuery();
                pcmd.Dispose();
            }
            catch (SqlException)
            {
                m_log.Warn("[MSSQL]: Primitives Table Already Exists");
            }

            try
            {
                scmd.ExecuteNonQuery();
                scmd.Dispose();
            }
            catch (SqlException)
            {
                m_log.Warn("[MSSQL]: Shapes Table Already Exists");
            }

            try
            {
                tcmd.ExecuteNonQuery();
                tcmd.Dispose();
            }
            catch (SqlException)
            {
                m_log.Warn("[MSSQL]: Terrain Table Already Exists");
            }

            conn.Close();
        }

        private static bool TestTables(SqlConnection conn)
        {
            SqlCommand primSelectCmd = new SqlCommand(primSelect, conn);
            SqlDataAdapter pDa = new SqlDataAdapter(primSelectCmd);
            SqlCommand shapeSelectCmd = new SqlCommand(shapeSelect, conn);
            SqlDataAdapter sDa = new SqlDataAdapter(shapeSelectCmd);
            SqlCommand terrainSelectCmd = new SqlCommand(terrainSelect, conn);
            SqlDataAdapter tDa = new SqlDataAdapter(terrainSelectCmd);

            DataSet tmpDS = new DataSet();
            try
            {
                pDa.Fill(tmpDS, "prims");
                sDa.Fill(tmpDS, "primshapes");
                tDa.Fill(tmpDS, "terrain");
            }
            catch (SqlException)
            {
                m_log.Info("[DATASTORE]: MSSQL Database doesn't exist... creating");
                InitDB(conn);
            }

            try
            {
                if (tmpDS.Tables == null || tmpDS.Tables.Count == 0)
                {
                    pDa.Fill(tmpDS, "prims");
                    sDa.Fill(tmpDS, "primshapes");
                    tDa.Fill(tmpDS, "terrain");
                }
            }
            catch (SqlException e)
            {
                m_log.Info("[DATASTORE]: " + e.ToString());
            }

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
            foreach (DataColumn col in createTerrainTable().Columns)
            {
                if (!tmpDS.Tables["terrain"].Columns.Contains(col.ColumnName))
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
        private static string SqlType(Type type)
        {
            if (type == typeof (String))
            {
                return "varchar(255)";
            }
            else if (type == typeof (Int32))
            {
                return "integer";
            }
            else if (type == typeof (Double))
            {
                return "float";
            }
            else if (type == typeof (Byte[]))
            {
                return "image";
            }
            else
            {
                return "string";
            }
        }
    }
}
