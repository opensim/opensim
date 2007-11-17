using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using libsecondlife;
using MySql.Data.MySqlClient;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment.Scenes;
using System.Data.SqlClient;
using System.Data.Common;

namespace OpenSim.Framework.Data.MySQL
{
    public class MySQLDataStore : IRegionDataStore
    {
        private const string m_primSelect = "select * from prims";
        private const string m_shapeSelect = "select * from primshapes";
        private const string m_terrainSelect = "select * from terrain limit 1";

        private DataSet m_dataSet;
        private MySqlDataAdapter m_primDataAdapter;
        private MySqlDataAdapter m_shapeDataAdapter;
        private MySqlConnection m_connection;
        private MySqlDataAdapter m_terrainDataAdapter;
        private DataTable m_primTable;
        private DataTable m_shapeTable;
        private DataTable m_terrainTable;

        /***********************************************************************
         *
         *  Public Interface Functions
         *
         **********************************************************************/

        public void Initialise(string connectionstring)
        {
            m_dataSet = new DataSet();

            MainLog.Instance.Verbose("DATASTORE", "MySql - connecting: " + connectionstring);
            m_connection = new MySqlConnection(connectionstring);

            MySqlCommand primSelectCmd = new MySqlCommand(m_primSelect, m_connection);
            m_primDataAdapter = new MySqlDataAdapter(primSelectCmd);

            MySqlCommand shapeSelectCmd = new MySqlCommand(m_shapeSelect, m_connection);
            m_shapeDataAdapter = new MySqlDataAdapter(shapeSelectCmd);

            MySqlCommand terrainSelectCmd = new MySqlCommand(m_terrainSelect, m_connection);
            m_terrainDataAdapter = new MySqlDataAdapter(terrainSelectCmd);

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

                m_terrainTable = createTerrainTable();
                m_dataSet.Tables.Add(m_terrainTable);
                SetupTerrainCommands(m_terrainDataAdapter, m_connection);
                m_terrainDataAdapter.Fill(m_terrainTable);
            }
        }

        public void StoreObject(SceneObjectGroup obj, LLUUID regionUUID)
        {
            lock (m_dataSet)
            {
                foreach (SceneObjectPart prim in obj.Children.Values)
                {
                    if ((prim.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) == 0)
                    {
                        MainLog.Instance.Verbose("DATASTORE", "Adding obj: " + obj.UUID + " to region: " + regionUUID);
                        addPrim(prim, obj.UUID, regionUUID);
                    }
                    else
                    {
                        // MainLog.Instance.Verbose("DATASTORE", "Ignoring Physical obj: " + obj.UUID + " in region: " + regionUUID);
                    }
                }
            }

            Commit();
        }

        public void RemoveObject(LLUUID obj, LLUUID regionUUID)
        {
            DataTable prims = m_primTable;
            DataTable shapes = m_shapeTable;

            string selectExp = "SceneGroupID = '" + obj.ToString() + "'";
            lock (m_dataSet)
            {
                DataRow[] primRows = prims.Select(selectExp);
                foreach (DataRow row in primRows)
                {
                    LLUUID uuid = new LLUUID((string)row["UUID"]);
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

            DataTable prims = m_primTable;
            DataTable shapes = m_shapeTable;

            string byRegion = "RegionUUID = '" + regionUUID.ToString() + "'";
            string orderByParent = "ParentID ASC";

            lock (m_dataSet)
            {
                DataRow[] primsForRegion = prims.Select(byRegion, orderByParent);
                MainLog.Instance.Verbose("DATASTORE",
                                         "Loaded " + primsForRegion.Length + " prims for region: " + regionUUID);

                foreach (DataRow primRow in primsForRegion)
                {
                    try
                    {
                        string uuid = (string)primRow["UUID"];
                        string objID = (string)primRow["SceneGroupID"];
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
                                MainLog.Instance.Notice(
                                    "No shape found for prim in storage, so setting default box shape");
                                prim.Shape = BoxShape.Default;
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
                                MainLog.Instance.Notice(
                                    "No shape found for prim in storage, so setting default box shape");
                                prim.Shape = BoxShape.Default;
                            }
                            createdObjects[new LLUUID(objID)].AddPart(prim);
                        }
                    }
                    catch (Exception e)
                    {
                        MainLog.Instance.Error("DATASTORE", "Failed create prim object, exception and data follows");
                        MainLog.Instance.Verbose(e.ToString());
                        foreach (DataColumn col in prims.Columns)
                        {
                            MainLog.Instance.Verbose("Col: " + col.ColumnName + " => " + primRow[col]);
                        }
                    }
                }
            }
            return retvals;
        }


        public void StoreTerrain(double[,] ter, LLUUID regionID)
        {
            int revision = Util.UnixTimeSinceEpoch();
            MainLog.Instance.Verbose("DATASTORE", "Storing terrain revision r" + revision.ToString());

            DataTable terrain = m_dataSet.Tables["terrain"];
            lock (m_dataSet)
            {
                MySqlCommand cmd = new MySqlCommand("insert into terrain(RegionUUID, Revision, Heightfield)" +
                                                      " values(?RegionUUID, ?Revision, ?Heightfield)", m_connection);
                using (cmd)
                {

                    cmd.Parameters.Add(new MySqlParameter("?RegionUUID", regionID.ToString()));
                    cmd.Parameters.Add(new MySqlParameter("?Revision", revision));
                    cmd.Parameters.Add(new MySqlParameter("?Heightfield", serializeTerrain(ter)));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public double[,] LoadTerrain(LLUUID regionID)
        {
            double[,] terret = new double[256, 256];
            terret.Initialize();

            MySqlCommand cmd = new MySqlCommand(
                @"select RegionUUID, Revision, Heightfield from terrain
                where RegionUUID=?RegionUUID order by Revision desc limit 1"
                , m_connection);

            MySqlParameter param = new MySqlParameter();
            cmd.Parameters.Add(new MySqlParameter("?RegionUUID", regionID.ToString()));

            if (m_connection.State != ConnectionState.Open)
            {
                m_connection.Open();
            }

            using (MySqlDataReader row = cmd.ExecuteReader())
            {
                int rev = 0;
                if (row.Read())
                {
                    byte[] heightmap = (byte[])row["Heightfield"];
                    for (int x = 0; x < 256; x++)
                    {
                        for (int y = 0; y < 256; y++)
                        {
                            terret[x, y] = BitConverter.ToDouble(heightmap, ((x * 256) + y) * 8);
                        }
                    }
                    rev = (int)row["Revision"];
                }
                else
                {
                    MainLog.Instance.Verbose("DATASTORE", "No terrain found for region");
                    return null;
                }

                MainLog.Instance.Verbose("DATASTORE", "Loaded terrain revision r" + rev.ToString());
            }

            return terret;
        }

        public void RemoveLandObject(uint id)
        {
        }

        public void StoreParcel(Land parcel)
        {
        }

        public List<Land> LoadLandObjects()
        {
            return new List<Land>();
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
                    if (row.HasVersion(System.Data.DataRowVersion.Original))
                    {
                        Debug.Write("Original Row Values ===> ");
                        foreach (DataColumn column in table.Columns)
                            Debug.Write(column.ColumnName + " = " +
                                        row[column, DataRowVersion.Original] + ", ");
                        Debug.WriteLine("");
                    }
                    //--- Display the current values, if there are any.
                    if (row.HasVersion(System.Data.DataRowVersion.Current))
                    {
                        Debug.Write("Current Row Values ====> ");
                        foreach (DataColumn column in table.Columns)
                            Debug.Write(column.ColumnName + " = " +
                                        row[column, DataRowVersion.Current] + ", ");
                        Debug.WriteLine("");
                    }
                    Debug.WriteLine("");
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
                m_terrainDataAdapter.Update(m_terrainTable);

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

            createCol(terrain, "RegionUUID", typeof(String));
            createCol(terrain, "Revision", typeof(Int32));
            DataColumn heightField = createCol(terrain, "Heightfield", typeof(Byte[]));
            return terrain;
        }

        private DataTable createPrimTable()
        {
            DataTable prims = new DataTable("prims");

            createCol(prims, "UUID", typeof(String));
            createCol(prims, "RegionUUID", typeof(String));
            createCol(prims, "ParentID", typeof(Int32));
            createCol(prims, "CreationDate", typeof(Int32));
            createCol(prims, "Name", typeof(String));
            createCol(prims, "SceneGroupID", typeof(String));
            // various text fields
            createCol(prims, "Text", typeof(String));
            createCol(prims, "Description", typeof(String));
            createCol(prims, "SitName", typeof(String));
            createCol(prims, "TouchName", typeof(String));
            // permissions
            createCol(prims, "ObjectFlags", typeof(Int32));
            createCol(prims, "CreatorID", typeof(String));
            createCol(prims, "OwnerID", typeof(String));
            createCol(prims, "GroupID", typeof(String));
            createCol(prims, "LastOwnerID", typeof(String));
            createCol(prims, "OwnerMask", typeof(Int32));
            createCol(prims, "NextOwnerMask", typeof(Int32));
            createCol(prims, "GroupMask", typeof(Int32));
            createCol(prims, "EveryoneMask", typeof(Int32));
            createCol(prims, "BaseMask", typeof(Int32));
            // vectors
            createCol(prims, "PositionX", typeof(Double));
            createCol(prims, "PositionY", typeof(Double));
            createCol(prims, "PositionZ", typeof(Double));
            createCol(prims, "GroupPositionX", typeof(Double));
            createCol(prims, "GroupPositionY", typeof(Double));
            createCol(prims, "GroupPositionZ", typeof(Double));
            createCol(prims, "VelocityX", typeof(Double));
            createCol(prims, "VelocityY", typeof(Double));
            createCol(prims, "VelocityZ", typeof(Double));
            createCol(prims, "AngularVelocityX", typeof(Double));
            createCol(prims, "AngularVelocityY", typeof(Double));
            createCol(prims, "AngularVelocityZ", typeof(Double));
            createCol(prims, "AccelerationX", typeof(Double));
            createCol(prims, "AccelerationY", typeof(Double));
            createCol(prims, "AccelerationZ", typeof(Double));
            // quaternions
            createCol(prims, "RotationX", typeof(Double));
            createCol(prims, "RotationY", typeof(Double));
            createCol(prims, "RotationZ", typeof(Double));
            createCol(prims, "RotationW", typeof(Double));

            // Add in contraints
            prims.PrimaryKey = new DataColumn[] { prims.Columns["UUID"] };

            return prims;
        }

        private DataTable createShapeTable()
        {
            DataTable shapes = new DataTable("primshapes");
            createCol(shapes, "UUID", typeof(String));
            // shape is an enum
            createCol(shapes, "Shape", typeof(Int32));
            // vectors
            createCol(shapes, "ScaleX", typeof(Double));
            createCol(shapes, "ScaleY", typeof(Double));
            createCol(shapes, "ScaleZ", typeof(Double));
            // paths
            createCol(shapes, "PCode", typeof(Int32));
            createCol(shapes, "PathBegin", typeof(Int32));
            createCol(shapes, "PathEnd", typeof(Int32));
            createCol(shapes, "PathScaleX", typeof(Int32));
            createCol(shapes, "PathScaleY", typeof(Int32));
            createCol(shapes, "PathShearX", typeof(Int32));
            createCol(shapes, "PathShearY", typeof(Int32));
            createCol(shapes, "PathSkew", typeof(Int32));
            createCol(shapes, "PathCurve", typeof(Int32));
            createCol(shapes, "PathRadiusOffset", typeof(Int32));
            createCol(shapes, "PathRevolutions", typeof(Int32));
            createCol(shapes, "PathTaperX", typeof(Int32));
            createCol(shapes, "PathTaperY", typeof(Int32));
            createCol(shapes, "PathTwist", typeof(Int32));
            createCol(shapes, "PathTwistBegin", typeof(Int32));
            // profile
            createCol(shapes, "ProfileBegin", typeof(Int32));
            createCol(shapes, "ProfileEnd", typeof(Int32));
            createCol(shapes, "ProfileCurve", typeof(Int32));
            createCol(shapes, "ProfileHollow", typeof(Int32));
            createCol(shapes, "Texture", typeof(Byte[]));
            createCol(shapes, "ExtraParams", typeof(Byte[]));

            shapes.PrimaryKey = new DataColumn[] { shapes.Columns["UUID"] };

            return shapes;
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
            prim.UUID = new LLUUID((String)row["UUID"]);
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
            prim.CreatorID = new LLUUID((String)row["CreatorID"]);
            prim.OwnerID = new LLUUID((String)row["OwnerID"]);
            prim.GroupID = new LLUUID((String)row["GroupID"]);
            prim.LastOwnerID = new LLUUID((String)row["LastOwnerID"]);
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

            return prim;
        }

        private Array serializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream(65536 * sizeof(double));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < 256; x++)
                for (int y = 0; y < 256; y++)
                    bw.Write(val[x, y]);

            return str.ToArray();
        }

        private void fillPrimRow(DataRow row, SceneObjectPart prim, LLUUID sceneGroupID, LLUUID regionUUID)
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
            s.TextureEntry = (byte[])row["Texture"];
            s.ExtraParams = (byte[])row["ExtraParams"];

            return s;
        }

        private void fillShapeRow(DataRow row, SceneObjectPart prim)
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
            row["Texture"] = s.TextureEntry;
            row["ExtraParams"] = s.ExtraParams;
        }

        private void addPrim(SceneObjectPart prim, LLUUID sceneGroupID, LLUUID regionUUID)
        {
            DataTable prims = m_dataSet.Tables["prims"];
            DataTable shapes = m_dataSet.Tables["primshapes"];

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
            string subsql = "";
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
            string subsql = "";
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
            delete.Parameters.Add(createMySqlParameter("UUID", typeof(String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void SetupTerrainCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("terrain", m_dataSet.Tables["terrain"]);
            da.InsertCommand.Connection = conn;
        }

        private void SetupShapeCommands(MySqlDataAdapter da, MySqlConnection conn)
        {
            da.InsertCommand = createInsertCommand("primshapes", m_dataSet.Tables["primshapes"]);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primshapes", "UUID=?UUID", m_dataSet.Tables["primshapes"]);
            da.UpdateCommand.Connection = conn;

            MySqlCommand delete = new MySqlCommand("delete from primshapes where UUID = ?UUID");
            delete.Parameters.Add(createMySqlParameter("UUID", typeof(String)));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void InitDB(MySqlConnection conn)
        {
            string createPrims = defineTable(createPrimTable());
            string createShapes = defineTable(createShapeTable());
            string createTerrain = defineTable(createTerrainTable());

            MySqlCommand pcmd = new MySqlCommand(createPrims, conn);
            MySqlCommand scmd = new MySqlCommand(createShapes, conn);
            MySqlCommand tcmd = new MySqlCommand(createTerrain, conn);

            if (conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            try
            {
                pcmd.ExecuteNonQuery();
            }
            catch (MySqlException)
            {
                MainLog.Instance.Warn("MySql", "Primitives Table Already Exists");
            }

            try
            {
                scmd.ExecuteNonQuery();
            }
            catch (MySqlException)
            {
                MainLog.Instance.Warn("MySql", "Shapes Table Already Exists");
            }

            try
            {
                tcmd.ExecuteNonQuery();
            }
            catch (MySqlException)
            {
                MainLog.Instance.Warn("MySql", "Terrain Table Already Exists");
            }

            conn.Close();
        }

        private bool TestTables(MySqlConnection conn)
        {
            MySqlCommand primSelectCmd = new MySqlCommand(m_primSelect, conn);
            MySqlDataAdapter pDa = new MySqlDataAdapter(primSelectCmd);
            MySqlCommand shapeSelectCmd = new MySqlCommand(m_shapeSelect, conn);
            MySqlDataAdapter sDa = new MySqlDataAdapter(shapeSelectCmd);
            MySqlCommand terrainSelectCmd = new MySqlCommand(m_terrainSelect, conn);
            MySqlDataAdapter tDa = new MySqlDataAdapter(terrainSelectCmd);

            DataSet tmpDS = new DataSet();
            try
            {
                pDa.Fill(tmpDS, "prims");
                sDa.Fill(tmpDS, "primshapes");
                tDa.Fill(tmpDS, "terrain");
            }
            catch (MySqlException)
            {
                MainLog.Instance.Verbose("DATASTORE", "MySql Database doesn't exist... creating");
                InitDB(conn);
            }

            pDa.Fill(tmpDS, "prims");
            sDa.Fill(tmpDS, "primshapes");
            tDa.Fill(tmpDS, "terrain");

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
            foreach (DataColumn col in createTerrainTable().Columns)
            {
                if (!tmpDS.Tables["terrain"].Columns.Contains(col.ColumnName))
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
            if (type == typeof(String))
            {
                return DbType.String;
            }
            else if (type == typeof(Int32))
            {
                return DbType.Int32;
            }
            else if (type == typeof(Double))
            {
                return DbType.Double;
            }
            else if (type == typeof(Byte))
            {
                return DbType.Byte;
            }
            else if (type == typeof(Double))
            {
                return DbType.Double;
            }
            else if (type == typeof(Byte[]))
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
            if (type == typeof(String))
            {
                return "varchar(255)";
            }
            else if (type == typeof(Int32))
            {
                return "integer";
            }
            else if (type == typeof(Double))
            {
                return "float";
            }
            else if (type == typeof(Byte[]))
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
