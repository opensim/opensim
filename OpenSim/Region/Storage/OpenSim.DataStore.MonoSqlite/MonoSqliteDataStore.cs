using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Framework.Console;
using libsecondlife;

using System.Data;
// Yes, this won't compile on MS, need to deal with that later
using Mono.Data.SqliteClient;
using Primitive = OpenSim.Region.Environment.Scenes.Primitive;

namespace OpenSim.DataStore.MonoSqliteStorage
{

    public class MonoSqliteDataStore : IRegionDataStore
    {
        private const string primSelect = "select * from prims";
        private const string shapeSelect = "select * from primshapes";

        private Dictionary<string, DbType> primDataDefs;
        private Dictionary<string, DbType> shapeDataDefs;
        
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

        private SqliteCommand createPrimInsertCommand(Dictionary<string, DbType> defs) 
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
            
            string sql = "insert into prims(";
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

        private SqliteCommand createPrimUpdateCommand(Dictionary<string, DbType> defs) 
        {
            string sql = "update prims set ";
            foreach (string key in defs.Keys) {
                sql += key + "= :" + key + ", ";
            }
            sql += " where UUID=:UUID";
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
            
            da.InsertCommand = createPrimInsertCommand(primDataDefs);
            da.InsertCommand.Connection = conn;

            da.UpdateCommand = createPrimUpdateCommand(primDataDefs);
            da.UpdateCommand.Connection = conn;
            
            SqliteCommand delete = new SqliteCommand("delete from prims where UUID = :UUID");
            delete.Parameters.Add(createSqliteParameter("UUID", DbType.String));
            delete.Connection = conn;
            da.DeleteCommand = delete;
        }

        private void StoreSceneObject(SceneObject obj)
        {
            
        }

        public void StoreObject(AllNewSceneObjectPart2 obj)
        {
            // TODO: Serializing code
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["shapes"];
            
           
            
        }
        
        private void fillPrimRow(DataRow row, Primitive prim) 
        {
            row["UUID"] = prim.UUID;
            row["CreationDate"] = prim.CreationDate;
            row["Name"] = prim.Name;
            row["PositionX"] = prim.Pos.X;
            row["PositionY"] = prim.Pos.Y;
            row["PositionZ"] = prim.Pos.Z;
        }

        private void addPrim(Primitive prim)
        {
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["shapes"];
            
            DataRow row = prims.Rows.Find(prim.UUID);
            if (row == null) {
                row = prims.NewRow();
                fillPrimRow(row, prim);
                prims.Rows.Add(row);
            } else {
                fillPrimRow(row, prim);
            }
        }

        public void StoreObject(SceneObject obj)
        {
            foreach (Primitive prim in obj.Children.Values)
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

        public List<SceneObject> LoadObjects()
        {
            List<SceneObject> retvals = new List<SceneObject>();

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
