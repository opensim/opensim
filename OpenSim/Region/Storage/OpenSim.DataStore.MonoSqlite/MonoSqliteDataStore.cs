using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Framework.Console;
using OpenSim.Framework.Types;
using libsecondlife;

using System.Data;
// Yes, this won't compile on MS, need to deal with that later
using Mono.Data.SqliteClient;

namespace OpenSim.DataStore.MonoSqliteStorage
{

    public class MonoSqliteDataStore : IRegionDataStore
    {
        private const string primSelect = "select * from prims";
        private const string shapeSelect = "select * from primshapes";

        private DataSet ds;
        private SqliteDataAdapter primDa;
        private SqliteDataAdapter shapeDa;

        public void Initialise(string dbfile, string dbname)
        {
            // for us, dbfile will be the connect string
            MainLog.Instance.Verbose("DATASTORE", "Sqlite - connecting: " + dbfile);
            SqliteConnection conn = new SqliteConnection(dbfile);

            SqliteCommand primSelectCmd = new SqliteCommand(primSelect, conn);
            primDa = new SqliteDataAdapter(primSelectCmd);
            //            SqliteCommandBuilder primCb = new SqliteCommandBuilder(primDa);
            
            SqliteCommand shapeSelectCmd = new SqliteCommand(shapeSelect, conn);
            shapeDa = new SqliteDataAdapter(shapeSelectCmd);
            // SqliteCommandBuilder shapeCb = new SqliteCommandBuilder(shapeDa);

            ds = new DataSet();

            // We fill the data set, now we've got copies in memory for the information
            // TODO: see if the linkage actually holds.
            // primDa.FillSchema(ds, SchemaType.Source, "PrimSchema");
            primDa.Fill(ds, "prims");
            ds.AcceptChanges();

            DataTable prims = ds.Tables["prims"];
            prims.PrimaryKey = new DataColumn[] { prims.Columns["UUID"] };
            setupPrimCommands(primDa, conn);
            
            // shapeDa.FillSchema(ds, SchemaType.Source, "ShapeSchema");
            shapeDa.Fill(ds, "primshapes");
            DataTable shapes = ds.Tables["primshapes"];
            shapes.PrimaryKey = new DataColumn[] { shapes.Columns["UUID"] };
            setupShapeCommands(shapeDa, conn);
            
            return;
        }

        private SqliteParameter createSqliteParameter(string name, DbType type)
        {
            SqliteParameter param = new SqliteParameter();
            param.ParameterName = ":" + name;
            param.DbType = type;
            param.SourceColumn = name;
            param.SourceVersion = DataRowVersion.Current;
            return param;
        }

        private Dictionary<string, DbType> createPrimDataDefs()
        {
            Dictionary<string, DbType> data = new Dictionary<string, DbType>();
            data.Add("UUID", DbType.String);
            data.Add("ParentID", DbType.Int32);
            data.Add("CreationDate", DbType.Int32);
            data.Add("Name", DbType.String);
            // various text fields
            data.Add("Text", DbType.String);
            data.Add("Description", DbType.String);
            data.Add("SitName", DbType.String);
            data.Add("TouchName", DbType.String);
            // permissions
            data.Add("CreatorID", DbType.String);
            data.Add("OwnerID", DbType.String);
            data.Add("GroupID", DbType.String);
            data.Add("LastOwnerID", DbType.String);
            data.Add("OwnerMask", DbType.Int32);
            data.Add("NextOwnerMask", DbType.Int32);
            data.Add("GroupMask", DbType.Int32);
            data.Add("EveryoneMask", DbType.Int32);
            data.Add("BaseMask", DbType.Int32);
            // vectors
            data.Add("PositionX", DbType.Double);
            data.Add("PositionY", DbType.Double);
            data.Add("PositionZ", DbType.Double);
            data.Add("VelocityX", DbType.Double);
            data.Add("VelocityY", DbType.Double);
            data.Add("VelocityZ", DbType.Double);
            data.Add("AngularVelocityX", DbType.Double);
            data.Add("AngularVelocityY", DbType.Double);
            data.Add("AngularVelocityZ", DbType.Double);
            data.Add("AccelerationX", DbType.Double);
            data.Add("AccelerationY", DbType.Double);
            data.Add("AccelerationZ", DbType.Double);
            // quaternions
            data.Add("RotationX", DbType.Double);
            data.Add("RotationY", DbType.Double);
            data.Add("RotationZ", DbType.Double);
            data.Add("RotationW", DbType.Double);
            return data;
        }

        private Dictionary<string, DbType> createShapeDataDefs()
        {
            Dictionary<string, DbType> data = new Dictionary<string, DbType>();
            data.Add("UUID", DbType.String);
            // shape is an enum
            data.Add("Shape", DbType.Int32);
            // vectors
            data.Add("ScaleX", DbType.Double);
            data.Add("ScaleY", DbType.Double);
            data.Add("ScaleZ", DbType.Double);
            // paths
            data.Add("PCode", DbType.Int32);
            data.Add("PathBegin", DbType.Int32);
            data.Add("PathEnd", DbType.Int32);
            data.Add("PathScaleX", DbType.Int32);
            data.Add("PathScaleY", DbType.Int32);
            data.Add("PathShearX", DbType.Int32);
            data.Add("PathShearY", DbType.Int32);
            data.Add("PathSkew", DbType.Int32);
            data.Add("PathCurve", DbType.Int32);
            data.Add("PathRadiusOffset", DbType.Int32);
            data.Add("PathRevolutions", DbType.Int32);
            data.Add("PathTaperX", DbType.Int32);
            data.Add("PathTaperY", DbType.Int32);
            data.Add("PathTwist", DbType.Int32);
            data.Add("PathTwistBegin", DbType.Int32);
            // profile
            data.Add("ProfileBegin", DbType.Int32);
            data.Add("ProfileEnd", DbType.Int32);
            data.Add("ProfileCurve", DbType.Int32);
            data.Add("ProfileHollow", DbType.Int32);
            // text TODO: this isn't right, but I'm not sure the right
            // way to specify this as a blob atm
            data.Add("Texture", DbType.Binary);
            return data;
        }

        private SqliteCommand createInsertCommand(string table, Dictionary<string, DbType> defs) 
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
            string[] cols = new string[defs.Keys.Count];
            defs.Keys.CopyTo(cols, 0);
            
            string sql = "insert into " + table + "(";
            sql += String.Join(", ", cols);
            // important, the first ':' needs to be here, the rest get added in the join
            sql += ") values (:";
            sql += String.Join(", :", cols);
            sql += ")";
            SqliteCommand cmd = new SqliteCommand(sql);
            
            // this provides the binding for all our parameters, so
            // much less code than it used to be
            foreach (KeyValuePair<string, DbType> kvp in defs) {
                cmd.Parameters.Add(createSqliteParameter(kvp.Key, kvp.Value));
            }
            return cmd;
        }

        private SqliteCommand createUpdateCommand(string table, string pk, Dictionary<string, DbType> defs) 
        {
            string sql = "update " + table + " set ";
            foreach (string key in defs.Keys) {
                sql += key + "= :" + key + ", ";
            }
            sql += " where " + pk;
            SqliteCommand cmd = new SqliteCommand(sql);

            // this provides the binding for all our parameters, so
            // much less code than it used to be
            foreach (KeyValuePair<string, DbType> kvp in defs) {
                cmd.Parameters.Add(createSqliteParameter(kvp.Key, kvp.Value));
            }
            return cmd;
        }

        private void setupPrimCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            Dictionary<string, DbType> primDataDefs = createPrimDataDefs();
            
            da.InsertCommand = createInsertCommand("prims", primDataDefs);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("prims", "UUID=:UUID", primDataDefs);
            da.UpdateCommand.Connection = conn;
            
            SqliteCommand delete = new SqliteCommand("delete from prims where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", DbType.String));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void setupShapeCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            Dictionary<string, DbType> shapeDataDefs = createShapeDataDefs();
            
            da.InsertCommand = createInsertCommand("primshapes", shapeDataDefs);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createUpdateCommand("primshapes", "UUID=:UUID", shapeDataDefs);
            da.UpdateCommand.Connection = conn;
            
            SqliteCommand delete = new SqliteCommand("delete from primshapes where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", DbType.String));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void fillPrimRow(DataRow row, SceneObjectPart prim) 
        {
            row["UUID"] = prim.UUID;
            row["ParentID"] = prim.ParentID;
            row["CreationDate"] = prim.CreationDate;
            row["Name"] = prim.PartName;
            // various text fields
            row["Text"] = prim.Text;
            row["Description"] = prim.Description;
            row["SitName"] = prim.SitName;
            row["TouchName"] = prim.TouchName;
            // permissions
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
            // text TODO: this isn't right] = but I'm not sure the right
            // way to specify this as a blob atm
            row["Texture"] = s.TextureEntry;

        }

        private void addPrim(SceneObjectPart prim)
        {
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["shapes"];
            
            DataRow primRow = prims.Rows.Find(prim.UUID);
            if (primRow == null) {
                primRow = prims.NewRow();
                fillPrimRow(primRow, prim);
                prims.Rows.Add(primRow);
            } else {
                fillPrimRow(primRow, prim);
            }

            DataRow shapeRow = shapes.Rows.Find(prim.UUID);
            if (shapeRow == null) {
                shapeRow = prims.NewRow();
                fillShapeRow(shapeRow, prim);
                prims.Rows.Add(shapeRow);
            } else {
                fillPrimRow(shapeRow, prim);
            }
        }

        public void StoreObject(SceneObjectGroup obj)
        {
            foreach (SceneObjectPart prim in obj.Children.Values)
            {
                addPrim(prim);
            }
            
            MainLog.Instance.Verbose("Attempting to do update....");
            primDa.Update(ds, "prims");
            MainLog.Instance.Verbose("Dump of prims:", ds.GetXml());
        }

        public void RemoveObject(LLUUID obj)
        {
            // TODO: remove code
        }

        public List<SceneObjectGroup> LoadObjects()
        {
            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            MainLog.Instance.Verbose("DATASTORE", "Sqlite - LoadObjects found " + " objects");

            return retvals;
        }
        
        public void StoreTerrain(double[,] ter)
        {

        }

        public double[,] LoadTerrain()
        {
            return null;
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

        public void Shutdown()
        {
            // TODO: DataSet commit
        }
    }
}
