using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public class ConfigSettings
    {
        private string m_physicsEngine;

        public string PhysicsEngine
        {
            get { return m_physicsEngine; }
            set { m_physicsEngine = value; }
        }
        private string m_meshEngineName;

        public string MeshEngineName
        {
            get { return m_meshEngineName; }
            set { m_meshEngineName = value; }
        }

        private bool m_standalone;

        public bool Standalone
        {
            get { return m_standalone; }
            set { m_standalone = value; }
        }

        private bool m_see_into_region_from_neighbor;

        public bool See_into_region_from_neighbor
        {
            get { return m_see_into_region_from_neighbor; }
            set { m_see_into_region_from_neighbor = value; }
        }

        private string m_storageDll;

        public string StorageDll
        {
            get { return m_storageDll; }
            set { m_storageDll = value; }
        }

        private string m_clientstackDll;

        public string ClientstackDll
        {
            get { return m_clientstackDll; }
            set { m_clientstackDll = value; }
        }

        private bool m_physicalPrim;

        public bool PhysicalPrim
        {
            get { return m_physicalPrim; }
            set { m_physicalPrim = value; }
        }

        private bool m_standaloneAuthenticate = false;

        public bool StandaloneAuthenticate
        {
            get { return m_standaloneAuthenticate; }
            set { m_standaloneAuthenticate = value; }
        }

        private string m_standaloneWelcomeMessage = null;

        public string StandaloneWelcomeMessage
        {
            get { return m_standaloneWelcomeMessage; }
            set { m_standaloneWelcomeMessage = value; }
        }

        private string m_standaloneInventoryPlugin;

        public string StandaloneInventoryPlugin
        {
            get { return m_standaloneInventoryPlugin; }
            set { m_standaloneInventoryPlugin = value; }
        }

        private string m_standaloneAssetPlugin;

        public string StandaloneAssetPlugin
        {
            get { return m_standaloneAssetPlugin; }
            set { m_standaloneAssetPlugin = value; }
        }

        private string m_standaloneUserPlugin;

        public string StandaloneUserPlugin
        {
            get { return m_standaloneUserPlugin; }
            set { m_standaloneUserPlugin = value; }
        }

        private string m_standaloneInventorySource;

        public string StandaloneInventorySource
        {
            get { return m_standaloneInventorySource; }
            set { m_standaloneInventorySource = value; }
        }

        private string m_standaloneAssetSource;

        public string StandaloneAssetSource
        {
            get { return m_standaloneAssetSource; }
            set { m_standaloneAssetSource = value; }
        }

        private string m_standaloneUserSource;

        public string StandaloneUserSource
        {
            get { return m_standaloneUserSource; }
            set { m_standaloneUserSource = value; }
        }

        private string m_assetStorage = "local";

        public string AssetStorage
        {
            get { return m_assetStorage; }
            set { m_assetStorage = value; }
        }

        protected string m_storageConnectionString;

        public string StorageConnectionString
        {
            get { return m_storageConnectionString; }
            set { m_storageConnectionString = value; }
        }

        protected string m_estateConnectionString;

        public string EstateConnectionString
        {
            get { return m_estateConnectionString; }
            set { m_estateConnectionString = value; }
        }

        protected bool m_dumpAssetsToFile;

        public bool DumpAssetsToFile
        {
            get { return m_dumpAssetsToFile; }
            set { m_dumpAssetsToFile = value; }
        }
    }
}
