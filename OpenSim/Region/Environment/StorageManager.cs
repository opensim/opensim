using System;
using System.Reflection;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment
{
    public class StorageManager
    {
        private IRegionDataStore m_dataStore;

        public IRegionDataStore DataStore
        {
            get { return m_dataStore; }
        }

        public StorageManager(IRegionDataStore storage)
        {
            m_dataStore = storage;
        }

        public StorageManager(string dllName, string dataStoreFile, string dataStoreDB)
        {
            MainLog.Instance.Verbose("DATASTORE", "Attempting to load " + dllName);
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    Type typeInterface = pluginType.GetInterface("IRegionDataStore", true);

                    if (typeInterface != null)
                    {
                        IRegionDataStore plug =
                            (IRegionDataStore) Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise(dataStoreFile, dataStoreDB);

                        m_dataStore = plug;

                        MainLog.Instance.Verbose("DATASTORE", "Added IRegionDataStore Interface");
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;

            //TODO: Add checking and warning to make sure it initialised.
        }
    }
}