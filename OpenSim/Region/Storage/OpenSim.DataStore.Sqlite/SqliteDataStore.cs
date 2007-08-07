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

namespace OpenSim.DataStore.SqliteStorage
{

    public class SqliteDataStore : IRegionDataStore
    {
        private const string primSelect = "select * from prims";
        private const string shapeSelect = "select * from primshapes";
        
        private DataSet ds;

        public void Initialise(string dbfile, string dbname)
        {
            // for us, dbfile will be the connect string
            MainLog.Instance.Verbose("DATASTORE", "Sqlite - connecting: " + dbfile);
            SqliteConnection conn = new SqliteConnection(dbfile);

            SqliteCommand primSelectCmd = new SqliteCommand(primSelect, conn);
            SqliteDataAdapter primDa = new SqliteDataAdapter(primSelectCmd);
            
            SqliteCommand shapeSelectCmd = new SqliteCommand(shapeSelect, conn);
            SqliteDataAdapter shapeDa = new SqliteDataAdapter(shapeSelectCmd);

            ds = new DataSet();

            // We fill the data set, now we've got copies in memory for the information
            // TODO: see if the linkage actually holds.
            primDa.FillSchema(ds, SchemaType.Mapped, "PrimSchema");
            primDa.Fill(ds, "prims");
            
            shapeDa.FillSchema(ds, SchemaType.Mapped, "ShapeSchema");
            shapeDa.Fill(ds, "primshapes");
            
            return;
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
        
        private void addPrim(Primitive prim)
        {
            DataTable prims = ds.Tables["prims"];
            DataTable shapes = ds.Tables["shapes"];
            DataRow row;
        }

        public void StoreObject(SceneObject obj)
        {
            foreach (Primitive prim in obj.Children.Values) 
            {
                addPrim(prim);
            }
            MainLog.Instance.Verbose("Dump of prims: {0}", ds.GetXml());
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
