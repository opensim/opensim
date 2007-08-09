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

        private SqliteCommand createInsertCommand(Dictionary<string, DbType> defs) 
        {
            SqliteCommand cmd = new SqliteCommand();
            string sql = "insert into prims(";
            
            return cmd;
        }

        private void setupPrimCommands(SqliteDataAdapter da, SqliteConnection conn)
        {
            Dictionary<string, DbType> primDataDefs = createPrimDataDefs();
            
            da.InsertCommand = createInsertCommand(primDataDefs);
            /* 
             * Create all the bound parameters.  Try to keep these in the same order
             * as the sql file, with comments in the same places, or your head will probably
             * explode trying to do corolations
             */
            SqliteParameter UUID = createSqliteParameter("UUID", DbType.String);
            SqliteParameter ParentID = createSqliteParameter("ParentID", DbType.Int32);
            SqliteParameter CreationDate = createSqliteParameter("CreationDate", DbType.Int32);
            SqliteParameter Name = createSqliteParameter("Name", DbType.String);
            // various text fields
            SqliteParameter Text = createSqliteParameter("Text", DbType.String);
            SqliteParameter Description = createSqliteParameter("Description", DbType.String);
            SqliteParameter SitName = createSqliteParameter("SitName", DbType.String);
            SqliteParameter TouchName = createSqliteParameter("TouchName", DbType.String);
            // permissions
            SqliteParameter CreatorID = createSqliteParameter("CreatorID", DbType.String);
            SqliteParameter OwnerID = createSqliteParameter("OwnerID", DbType.String);
            SqliteParameter GroupID = createSqliteParameter("GroupID", DbType.String);
            SqliteParameter LastOwnerID = createSqliteParameter("LastOwnerID", DbType.String);
            SqliteParameter OwnerMask = createSqliteParameter("OwnerMask", DbType.Int32);
            SqliteParameter NextOwnerMask = createSqliteParameter("NextOwnerMask", DbType.Int32);
            SqliteParameter GroupMask = createSqliteParameter("GroupMask", DbType.Int32);
            SqliteParameter EveryoneMask = createSqliteParameter("EveryoneMask", DbType.Int32);
            SqliteParameter BaseMask = createSqliteParameter("BaseMask", DbType.Int32);
            // vectors
            SqliteParameter PositionX = createSqliteParameter("PositionX", DbType.Double);
            SqliteParameter PositionY = createSqliteParameter("PositionY", DbType.Double);
            SqliteParameter PositionZ = createSqliteParameter("PositionZ", DbType.Double);
            SqliteParameter VelocityX = createSqliteParameter("VelocityX", DbType.Double);
            SqliteParameter VelocityY = createSqliteParameter("VelocityY", DbType.Double);
            SqliteParameter VelocityZ = createSqliteParameter("VelocityZ", DbType.Double);
            SqliteParameter AngularVelocityX = createSqliteParameter("AngularVelocityX", DbType.Double);
            SqliteParameter AngularVelocityY = createSqliteParameter("AngularVelocityY", DbType.Double);
            SqliteParameter AngularVelocityZ = createSqliteParameter("AngularVelocityZ", DbType.Double);
            SqliteParameter AccelerationX = createSqliteParameter("AccelerationX", DbType.Double);
            SqliteParameter AccelerationY = createSqliteParameter("AccelerationY", DbType.Double);
            SqliteParameter AccelerationZ = createSqliteParameter("AccelerationZ", DbType.Double);
            // quaternions
            SqliteParameter RotationX = createSqliteParameter("RotationX", DbType.Double);
            SqliteParameter RotationY = createSqliteParameter("RotationY", DbType.Double);
            SqliteParameter RotationZ = createSqliteParameter("RotationZ", DbType.Double);
            SqliteParameter RotationW = createSqliteParameter("RotationW", DbType.Double);

            
            SqliteCommand delete = new SqliteCommand("delete from prims where UUID = :UUID");
            delete.Connection = conn;
            
            SqliteCommand insert = 
                new SqliteCommand("insert into prims(" +
                                  "UUID, ParentID, CreationDate, Name, " +
                                  "Text, Description, SitName, TouchName, " +
                                  "CreatorID, OwnerID, GroupID, LastOwnerID, " +
                                  "OwnerMask, NextOwnerMask, GroupMask, EveryoneMask, BaseMask, " +
                                  "PositionX, PositionY, PositionZ, " +
                                  "VelocityX, VelocityY, VelocityZ, " +
                                  "AngularVelocityX, AngularVelocityY, AngularVelocityZ, " +
                                  "AccelerationX, AccelerationY, AccelerationZ, " +
                                  "RotationX, RotationY, RotationZ, RotationW" +
                                  ") values (" +
                                  ":UUID, :ParentID, :CreationDate, :Name, " +
                                  ":Text, :Description, :SitName, :TouchName, " +
                                  ":CreatorID, :OwnerID, :GroupID, :LastOwnerID, " +
                                  ":OwnerMask, :NextOwnerMask, :GroupMask, :EveryoneMask, :BaseMask, " +
                                  ":PositionX, :PositionY, :PositionZ, " +
                                  ":VelocityX, :VelocityY, :VelocityZ, " +
                                  ":AngularVelocityX, :AngularVelocityY, :AngularVelocityZ, " +
                                  ":AccelerationX, :AccelerationY, :AccelerationZ, " +
                                  ":RotationX, :RotationY, :RotationZ, :RotationW)");
                                  
            insert.Connection = conn;
            
            SqliteCommand update =
                new SqliteCommand("update prims set " +
                                  "UUID = :UUID, ParentID = :ParentID, CreationDate = :CreationDate, Name = :Name, " +
                                  "Text = :Text, Description = :Description, SitName = :SitName, TouchName = :TouchName, " +
                                  "CreatorID = :CreatorID, OwnerID = :OwnerID, GroupID = :GroupID, LastOwnerID = :LastOwnerID, " +
                                  "OwnerMask = :OwnerMask, NextOwnerMask = :NextOwnerMask, GroupMask = :GroupMask,  = :EveryoneMask,  = :BaseMask, " +
                                  " = :PositionX,  = :PositionY,  = :PositionZ, " +
                                  " = :VelocityX,  = :VelocityY,  = :VelocityZ, " +
                                  " = :AngularVelocityX,  = :AngularVelocityY,  = :AngularVelocityZ, " +
                                  " = :AccelerationX,  = :AccelerationY,  = :AccelerationZ, " +
                                  " = :RotationX,  = :RotationY,  = :RotationZ,  = :RotationW " +
                                  "where UUID = :UUID");
            update.Connection = conn;

            delete.Parameters.Add(UUID);

            insert.Parameters.Add(UUID);
            insert.Parameters.Add(Name);
            insert.Parameters.Add(CreationDate);
            insert.Parameters.Add(PositionX);
            insert.Parameters.Add(PositionY);
            insert.Parameters.Add(PositionZ);

            update.Parameters.Add(UUID);
            update.Parameters.Add(Name);
            update.Parameters.Add(CreationDate);
            update.Parameters.Add(PositionX);
            update.Parameters.Add(PositionY);
            update.Parameters.Add(PositionZ);

            da.DeleteCommand = delete;
            da.InsertCommand = insert;
            da.UpdateCommand = update;
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
