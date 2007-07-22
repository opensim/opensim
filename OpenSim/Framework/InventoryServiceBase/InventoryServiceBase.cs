using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Data;

namespace OpenSim.Framework.InventoryServiceBase
{
    public class InventoryServiceBase
    {
        protected Dictionary<string, IInventoryData> m_plugins = new Dictionary<string, IInventoryData>();
        protected IAssetServer m_assetServer;

        public InventoryServiceBase(IAssetServer assetServer)
        {
            m_assetServer = assetServer;
        }

        /// <summary>
        /// Adds a new user server plugin - plugins will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            MainLog.Instance.Verbose("Inventorytorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (!pluginType.IsAbstract)
                {
                    Type typeInterface = pluginType.GetInterface("IInventoryData", true);

                    if (typeInterface != null)
                    {
                        IInventoryData plug = (IInventoryData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise();
                        this.m_plugins.Add(plug.getName(), plug);
                        MainLog.Instance.Verbose("Inventorystorage: Added IInventoryData Interface");
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID userID)
        {
            List<InventoryFolderBase> inventoryList = new List<InventoryFolderBase>();
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                InventoryFolderBase rootFolder = plugin.Value.getUserRootFolder(userID);
                if (rootFolder != null)
                {
                    inventoryList = plugin.Value.getInventoryFolders(rootFolder.folderID);
                    inventoryList.Insert(0, rootFolder);
                    return inventoryList;
                }
            }
            return inventoryList;
        }

        /// <summary>
        /// 
        /// </summary>
        public InventoryFolderBase RequestUsersRoot(LLUUID userID)
        {
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                return plugin.Value.getUserRootFolder(userID);
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentFolderID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> RequestSubFolders(LLUUID parentFolderID)
        {
            List<InventoryFolderBase> inventoryList = new List<InventoryFolderBase>();
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                return plugin.Value.getInventoryFolders(parentFolderID);
            }
            return inventoryList;
        }

        public List<InventoryItemBase> RequestFolderItems(LLUUID folderID)
        {
            List<InventoryItemBase> itemsList = new List<InventoryItemBase>();
            foreach (KeyValuePair<string, IInventoryData> plugin in m_plugins)
            {
                itemsList = plugin.Value.getInventoryInFolder(folderID);
                return itemsList;
            }
            return itemsList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inventory"></param>
        public void AddNewInventorySet(UsersInventory inventory)
        {

        }

        public class UsersInventory
        {
            public Dictionary<LLUUID, InventoryFolderBase> Folders = new Dictionary<LLUUID, InventoryFolderBase>();
            public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();

            public UsersInventory()
            {

            }

            protected virtual void CreateNewInventorySet()
            {

            }
        }
    }
}
