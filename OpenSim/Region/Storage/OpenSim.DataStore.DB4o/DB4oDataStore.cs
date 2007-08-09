using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Framework.Console;
using libsecondlife;

using Db4objects.Db4o;
using Db4objects.Db4o.Query;

namespace OpenSim.DataStore.DB4oStorage
{

    public class SceneObjectQuery : Predicate
    {
        private LLUUID globalIDSearch;

        public SceneObjectQuery(LLUUID find)
        {
            globalIDSearch = find;
        }

        public bool Match(SceneObjectGroup obj)
        {
            return obj.UUID == globalIDSearch;
        }
    }


    public class DB4oDataStore : IRegionDataStore
    {
        private IObjectContainer db;

        public void Initialise(string dbfile, string dbname)
        {
            MainLog.Instance.Verbose("DATASTORE", "DB4O - Opening " + dbfile);
            db = Db4oFactory.OpenFile(dbfile);

            return;
        }

        public void StoreObject(SceneObjectGroup obj)
        {
            db.Set(obj);
        }

        public void RemoveObject(LLUUID obj)
        {
            IObjectSet result = db.Query(new SceneObjectQuery(obj));
            if (result.Count > 0)
            {
                SceneObjectGroup item = (SceneObjectGroup)result.Next();
                db.Delete(item);
            }
        }

        public List<SceneObjectGroup> LoadObjects()
        {
            IObjectSet result = db.Get(typeof(SceneObjectGroup));
            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            MainLog.Instance.Verbose("DATASTORE", "DB4O - LoadObjects found " + result.Count.ToString() + " objects");

            foreach (Object obj in result)
            {
                retvals.Add((SceneObjectGroup)obj);
            }

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
            if (db != null)
            {
                db.Commit();
                db.Close();
            }
        }
    }
}
