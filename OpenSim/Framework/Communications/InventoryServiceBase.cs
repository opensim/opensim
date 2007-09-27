using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using InventoryFolder=OpenSim.Framework.Communications.Caches.InventoryFolder;
using InventoryCategory = OpenSim.Framework.Data.InventoryCategory;

namespace OpenSim.Framework.Communications
{
    public abstract class InventoryServiceBase : MarshalByRefObject, IInventoryServices
    {
        protected IInventoryData _databasePlugin;

        public InventoryServiceBase()
        {
        }

        /// <summary>
        /// Adds a new inventory data server plugin 
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            if (!String.IsNullOrEmpty(FileName))
            {
                MainLog.Instance.Verbose("Inventory", "Inventorystorage: Attempting to load " + FileName);
                Assembly pluginAssembly = Assembly.LoadFrom(FileName);

                foreach (Type pluginType in pluginAssembly.GetTypes())
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IInventoryData", true);

                        if (typeInterface != null)
                        {
                            IInventoryData plug =
                                (IInventoryData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            plug.Initialise();
                            this._databasePlugin = plug;

                            //TODO! find a better place to create inventory skeletons
                            loadInventoryFromXmlFile(InventoryCategory.Library, "Inventory_Library.xml");
                            loadInventoryFromXmlFile(InventoryCategory.Default, "Inventory_Default.xml");
                            MainLog.Instance.Verbose("Inventorystorage: Added IInventoryData Interface");
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the root folder plus any folders in root (so down one level in the Inventory folders tree)
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID folderID)
        {
            InventoryFolderBase root = _databasePlugin.getInventoryFolder(folderID);

            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            if (root != null)
            {
                folders.Add(root);

                List<InventoryFolderBase> subFolders = _databasePlugin.getInventoryFolders(root.folderID);
                foreach (InventoryFolderBase f in subFolders)
                    folders.Add(f);
            }
            return folders;
        }

        /// <summary>
        /// 
        /// </summary>
        public InventoryFolderBase RequestUsersRoot(LLUUID userID)
        {
            return _databasePlugin.getInventoryFolder(userID);      // the id of the root folder, is the user id
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parentFolderID"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> RequestSubFolders(LLUUID parentFolderID)
        {
            return _databasePlugin.getInventoryFolders(parentFolderID);
        }

        public List<InventoryItemBase> RequestFolderItems(LLUUID folderID)
        {
            return _databasePlugin.getInventoryInFolder(folderID);
        }

        public void AddFolder(InventoryFolderBase folder)
        {
            _databasePlugin.addInventoryFolder(folder);
        }

        public void AddItem(InventoryItemBase item)
        {
            _databasePlugin.addInventoryItem(item);
        }

        public void deleteItem(InventoryItemBase item)
        {
            _databasePlugin.deleteInventoryItem(item);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inventory"></param>
        public void AddNewInventorySet(UsersInventory inventory)
        {
            foreach (InventoryFolderBase folder in inventory.Folders.Values)
            {
                this.AddFolder(folder);
            }
        }

        public void CreateNewUserInventory(LLUUID defaultFolders, LLUUID user)
        {
            try
            {
                // Get Default folder set from the database
                //TODO! We need to get the whole hierachy and not just one level down
                List<InventoryFolderBase> folders = this.RequestFirstLevelFolders(LLUUID.Parse("00000112-000f-0000-0000-000100bba000"));

                // create an index list, where each of the elements has the index of its parent in the hierachy
                // this algorithm is pretty shoddy O(n^2), but it is only executed once per user.
                int[] parentIdx = new int[folders.Count];
                for (int i = 0; i < folders.Count; i++)
                    parentIdx[i] = -1;

                for (int i = 0; i < folders.Count; i++)
                    for (int j = 0; j < folders.Count; j++)
                        if (folders[i].folderID == folders[j].parentID)
                            parentIdx[j] = i;


                //assign a new owerid and a new to the folders
                foreach (InventoryFolderBase ifb in folders)
                {
                    if (ifb.parentID == LLUUID.Zero)
                        ifb.folderID = user;
                    else
                        ifb.folderID = LLUUID.Random();

                    ifb.agentID = user;
                    ifb.category = InventoryCategory.User;
                }

                // correct the parent id
                for (int i = 0; i < folders.Count; i++)
                {
                    if (folders[i].parentID != LLUUID.Zero)
                        folders[i].parentID = folders[parentIdx[i]].folderID;     // root folder id is the same as the user id
                }

                // the list is structurally sound, using new folder id's, so save it
                foreach (InventoryFolderBase ifb in folders)
                    _databasePlugin.addInventoryFolder(ifb);
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(e.ToString());
            }
        }

        public class UsersInventory
        {
            public Dictionary<LLUUID, InventoryFolderBase> Folders = new Dictionary<LLUUID, InventoryFolderBase>();
            public Dictionary<LLUUID, InventoryItemBase> Items = new Dictionary<LLUUID, InventoryItemBase>();

            public UsersInventory()
            {

            }

            public virtual void CreateNewInventorySet(LLUUID user)
            {
                InventoryFolderBase folder = new InventoryFolderBase();
                folder.parentID = LLUUID.Zero;
                folder.agentID = user;
                folder.folderID = user;             // id of root folder is the same as the agent id
                folder.name = "My Inventory";
                folder.type = 8;
                folder.version = 1;
                folder.category = InventoryCategory.User;
                Folders.Add(folder.folderID, folder);

                LLUUID rootFolder = folder.folderID;

                folder = new InventoryFolderBase();
                folder.parentID = rootFolder;
                folder.agentID = user;
                folder.folderID = LLUUID.Random();
                folder.name = "Textures";
                folder.type = 0;
                folder.version = 1;
                folder.category = InventoryCategory.User;
                Folders.Add(folder.folderID, folder);

                folder = new InventoryFolderBase();
                folder.parentID = rootFolder;
                folder.agentID = user;
                folder.folderID = LLUUID.Random();
                folder.name = "Objects";
                folder.type = 6;
                folder.version = 1;
                folder.category = InventoryCategory.User;
                Folders.Add(folder.folderID, folder);

                folder = new InventoryFolderBase();
                folder.parentID = rootFolder;
                folder.agentID = user;
                folder.folderID = LLUUID.Random();
                folder.name = "Clothes";
                folder.type = 5;
                folder.version = 1;
                folder.category = InventoryCategory.User;
                Folders.Add(folder.folderID, folder);
            }
        }


        public void GetRootFoldersForUser(LLUUID user, out LLUUID libraryFolder, out LLUUID personalFolder)
        {
            List<InventoryFolderBase> folders = _databasePlugin.getUserRootFolders(user);
            libraryFolder = LLUUID.Zero;
            personalFolder = LLUUID.Zero;

            for (int i = 0; i < folders.Count; i++)
            {
                if (folders[i].category == InventoryCategory.Library)
                    libraryFolder = folders[i].folderID;
                else if (folders[i].category == InventoryCategory.User)
                    personalFolder = folders[i].folderID;
            }
        }

        /* 
         * Dot net has some issues, serializing a dictionary, so we cannot reuse the InventoryFolder
         * class defined in Communications.Framework.Communications.Caches. So we serialize/deserialize
         * into this simpler class, and then use that.
         */
        [XmlRoot(ElementName = "inventory", IsNullable = true)]
        public class SerializedInventory
        {
            [XmlRoot(ElementName = "folder", IsNullable = true)]
            public class SerializedFolder : InventoryFolderBase
            {
                [XmlArray(ElementName = "folders", IsNullable = true)]
                [XmlArrayItem(ElementName = "folder", IsNullable = true, Type = typeof(SerializedFolder))]
                public ArrayList SubFolders;

                [XmlArray(ElementName = "items", IsNullable = true)]
                [XmlArrayItem(ElementName = "item", IsNullable = true, Type = typeof(InventoryItemBase))]
                public ArrayList Items;
            }

            [XmlElement(ElementName = "folder", IsNullable = true)]
            public SerializedFolder root;
        }

        public void uploadInventory(SerializedInventory.SerializedFolder folder)
        {
            foreach (InventoryItemBase iib in folder.Items)
            {
                // assign default values, if they haven't assigned
                iib.avatarID = folder.agentID;
                if (iib.assetID == LLUUID.Zero)
                    iib.assetID = LLUUID.Random();
                if (iib.creatorsID == LLUUID.Zero)
                    iib.creatorsID = folder.agentID;
                if (iib.inventoryID == LLUUID.Zero)
                    iib.inventoryID = LLUUID.Random();
                if (iib.inventoryName == null || iib.inventoryName.Length == 0)
                    iib.inventoryName = "new item";
                iib.parentFolderID = folder.folderID;

                _databasePlugin.addInventoryItem(iib);
            }

            foreach (SerializedInventory.SerializedFolder sf in folder.SubFolders)
            {
                // assign default values, if they haven't assigned
                sf.agentID = folder.agentID;
                sf.category = folder.category;
                if (sf.folderID == LLUUID.Zero)
                    sf.folderID = LLUUID.Random();
                if (sf.name == null || sf.name.Length == 0)
                    sf.name = "new folder";
                sf.parentID = folder.folderID;

                _databasePlugin.addInventoryFolder(sf);
                uploadInventory(sf);
            }
        }

        public void loadInventoryFromXmlFile(InventoryCategory inventoryCategory, string fileName)
        {
            _databasePlugin.deleteInventoryCategory(inventoryCategory);

            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            XmlReader reader = new XmlTextReader(fs);
            XmlSerializer x = new XmlSerializer(typeof(SerializedInventory));
            SerializedInventory inventory = (SerializedInventory)x.Deserialize(reader);

            // the library and default inventories has no owner, so we use a random  guid.
            if (inventory.root.category == InventoryCategory.Library || inventory.root.category == InventoryCategory.Default)
            {
                if (inventory.root.folderID != LLUUID.Zero)
                    inventory.root.agentID = inventory.root.folderID;
                else
                    inventory.root.agentID = LLUUID.Random();
            }
            else if (inventory.root.category == InventoryCategory.User)
            {
                if (inventory.root.agentID == LLUUID.Zero)
                    inventory.root.agentID = LLUUID.Random();
            }

            inventory.root.folderID = inventory.root.agentID;       // the root folder always has the same id as the owning agent
            inventory.root.parentID = LLUUID.Zero;
            inventory.root.version = 0;
            inventory.root.category = inventoryCategory;

            _databasePlugin.addInventoryFolder(inventory.root);
            uploadInventory(inventory.root);
        }

        protected void saveInventoryToXmlFile(SerializedInventory inventory, string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            XmlTextWriter writer = new XmlTextWriter(fs, Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            XmlSerializer x = new XmlSerializer(typeof(SerializedInventory));
            x.Serialize(writer, inventory);
        }

        public abstract void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack);
        public abstract void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder);
        public abstract void AddNewInventoryItem(LLUUID userID, InventoryItemBase item);
        public abstract void DeleteInventoryItem(LLUUID userID, InventoryItemBase item);
    }
}
