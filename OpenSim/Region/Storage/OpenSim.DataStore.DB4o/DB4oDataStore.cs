using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Framework.Console;
using libsecondlife;

using Db4objects.Db4o;

namespace OpenSim.DataStore.NullStorage
{
    public class DB4oDataStore : IRegionDataStore
    {
        private IObjectContainer db;

        public void Initialise(string dbfile, string dbname)
        {
            db = Db4oFactory.OpenFile(dbfile);

            return;
        }

        public void StoreObject(SceneObject obj)
        {
            db.Set(obj);
        }

        public void RemoveObject(LLUUID obj)
        {

        }

        public List<SceneObject> LoadObjects()
        {
            return new List<SceneObject>();
        }

        public void StoreTerrain(double[,] ter)
        {

        }

        public double[,] LoadTerrain()
        {
            return null;
        }

        public void RemoveParcel(uint id)
        {

        }

        public void StoreParcel(OpenSim.Region.Environment.Parcel parcel)
        {

        }

        public List<OpenSim.Region.Environment.Parcel> LoadParcels()
        {
            return new List<OpenSim.Region.Environment.Parcel>();
        }

        public void Shutdown()
        {

        }
    }
}
