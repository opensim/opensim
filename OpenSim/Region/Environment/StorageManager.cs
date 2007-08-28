using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Region.Capabilities;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;

using System.Reflection;

namespace OpenSim.Region.Environment
{
    public class StorageManager
    {
        private IRegionDataStore m_dataStore;

        public IRegionDataStore DataStore
        {
            get
            {
                return m_dataStore;
            }
        }

        public StorageManager(IRegionDataStore storage)
        {
            m_dataStore = storage;
        }

        public StorageManager(string dllName, string dataStoreFile, string dataStoreDB)
        {
            OpenSim.Framework.Console.MainLog.Instance.Verbose("DATASTORE", "Attempting to load " + dllName);
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    Type typeInterface = pluginType.GetInterface("IRegionDataStore", true);

                    if (typeInterface != null)
                    {
                        IRegionDataStore plug = (IRegionDataStore)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise(dataStoreFile, dataStoreDB);

                        m_dataStore = plug;

                        OpenSim.Framework.Console.MainLog.Instance.Verbose("DATASTORE", "Added IRegionDataStore Interface");
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;

            //TODO: Add checking and warning to make sure it initialised.
        }
    }
}
