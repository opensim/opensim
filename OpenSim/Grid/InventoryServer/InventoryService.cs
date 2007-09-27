using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using System.Collections.Generic;


using libsecondlife;
using OpenSim.Framework.Data;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Configuration;
using InventoryCategory = OpenSim.Framework.Data.InventoryCategory;

namespace OpenSim.Grid.InventoryServer
{
    class InventoryServiceSingleton : OpenSim.Framework.Communications.InventoryServiceBase
    {
        static InventoryManager _inventoryManager;

        static public InventoryManager InventoryManager
        {
            set { _inventoryManager = value; }
            get { return _inventoryManager; }
        }

#region Singleton Pattern
        static InventoryServiceSingleton instance=null;

        InventoryServiceSingleton()
        {
        }

        public static InventoryServiceSingleton Instance
        {
            get
            {
                if (instance==null)
                {
                    instance = new InventoryServiceSingleton();
                }
                return instance;
            }
        }
#endregion

#region IInventoryServices Members

        public override void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack)
        {
            List<InventoryFolderBase> folders = this.RequestFirstLevelFolders(userID);
            InventoryFolderBase rootFolder = null;

            //need to make sure we send root folder first
            foreach (InventoryFolderBase folder in folders)
            {
                if (folder.parentID == libsecondlife.LLUUID.Zero)
                {
                    rootFolder = folder;
                    folderCallBack(userID, folder);
                }
            }

            if (rootFolder != null)
            {
                foreach (InventoryFolderBase folder in folders)
                {
                    if (folder.folderID != rootFolder.folderID)
                    {
                        folderCallBack(userID, folder);

                        List<InventoryItemBase> items = this.RequestFolderItems(folder.folderID);
                        foreach (InventoryItemBase item in items)
                        {
                            itemCallBack(userID, item);
                        }
                    }
                }
            }

        }

        public override void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            _inventoryManager.addInventoryFolder(folder);
        }

        public override void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            throw new Exception("Not implemented exception");
        }

        public override void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            throw new Exception("Not implemented exception");
        }

        public List<InventoryItemBase> RequestFolderItems(LLUUID folderID)
        {
            return _inventoryManager.getInventoryInFolder(folderID);
        }


#endregion
    }

    class InventoryService 
    {
        InventoryServiceSingleton _inventoryServiceMethods;
        public InventoryService(InventoryManager inventoryManager, InventoryConfig cfg)
        {
            // we only need to register the tcp channel once, and we don't know which other modules use remoting
            if (ChannelServices.GetChannel("tcp") == null)
            {
                // Creating a custom formatter for a TcpChannel sink chain.
                BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider();
                serverProvider.TypeFilterLevel = TypeFilterLevel.Full;

                IDictionary props = new Hashtable();
                props["port"] = cfg.RemotingPort;
                props["typeFilterLevel"] = TypeFilterLevel.Full;

                // Pass the properties for the port setting and the server provider in the server chain argument. (Client remains null here.)
                ChannelServices.RegisterChannel(new TcpChannel(props, null, serverProvider), true);
            }

            // Register the object
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(InventoryServiceSingleton), "Inventory", WellKnownObjectMode.Singleton);

            _inventoryServiceMethods = InventoryServiceSingleton.Instance;
            InventoryServiceSingleton.InventoryManager = inventoryManager; 
        }

    }
}
