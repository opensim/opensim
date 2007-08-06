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

namespace OpenSim.DataStore.SqliteStorage
{
    
//     public class SceneObjectQuery : Predicate
//     {
//         private LLUUID globalIDSearch;

//         public SceneObjectQuery(LLUUID find)
//         {
//             globalIDSearch = find;
//         }

//         public bool Match(SceneObject obj)
//         {
//             return obj.rootUUID == globalIDSearch;
//         }
//     }


    public class SqliteDataStore : IRegionDataStore
    {
        private const primSelect = "select * from prims";
        private const shapeSelect = "select * from primshapes";
        
        private DataSet ds;
        private IObjectContainer db;

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

        public void StoreObject(SceneObject obj)
        {
            // TODO: Serializing code 
        }

        public void RemoveObject(LLUUID obj)
        {
            // TODO: remove code
        }

        public List<SceneObject> LoadObjects()
        {
            List<SceneObject> retvals = new List<SceneObject>();

            MainLog.Instance.Verbose("DATASTORE", "Sqlite - LoadObjects found " + " objects");

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
