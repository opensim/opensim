using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Framework.Console;
using libsecondlife;

namespace OpenSim.DataStore.NullStorage
{
    public class NullDataStore : IRegionDataStore
    {
        
        public void Initialise(string dbfile, string dbname)
        {
            return;
        }

        public void StoreObject(SceneObject obj)
        {

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

        public void StoreParcel(OpenSim.Region.Environment.Parcels.Parcel parcel)
        {

        }

        public List<OpenSim.Region.Environment.Parcels.Parcel> LoadParcels()
        {
            return new List<OpenSim.Region.Environment.Parcels.Parcel>();
        }

        public void Shutdown()
        {

        }
    }
}
