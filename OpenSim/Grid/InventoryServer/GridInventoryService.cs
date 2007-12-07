using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using libsecondlife;

namespace OpenSim.Grid.InventoryServer
{
    public class GridInventoryService : InventoryServiceBase
    {
        public override void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack,
                                                    InventoryItemInfo itemCallBack)
        {

        }

        private bool TryGetUsersInventory(LLUUID userID, out List<InventoryFolderBase> folderList, out List<InventoryItemBase> itemsList)
        {
            List<InventoryFolderBase> folders = RequestFirstLevelFolders(userID);
            List<InventoryItemBase> allItems = new List<InventoryItemBase>();

            if (folders != null)
            {
                foreach (InventoryFolderBase folder in folders)
                {
                    List<InventoryItemBase> items = RequestFolderItems(folder.folderID);
                    if (items != null)
                    {
                        allItems.InsertRange(0, items);
                    }
                }
            }
            
            folderList = folders;
            itemsList = allItems;
            if (folderList != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public InventoryCollection GetUserInventory(LLUUID userID)
        {
            InventoryCollection invCollection = new InventoryCollection();
            List<InventoryFolderBase> folders;
            List<InventoryItemBase> allItems;
            if (TryGetUsersInventory(userID, out folders, out allItems))
            {
                invCollection.AllItems = allItems;
                invCollection.Folders = folders;
                invCollection.UserID = userID;
            }
            return invCollection;
        }

        public bool CreateUsersInventory(LLUUID user)
        {
            Console.WriteLine("Creating New Set of Inventory Folders for " + user.ToStringHyphenated());
            CreateNewUserInventory(user);
            return true;
        }
        

        public override void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            AddFolder(folder);
        }

        public override void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            AddItem(item);
        }

        public bool AddInventoryFolder( InventoryFolderBase folder)
        {
            Console.WriteLine("creating new folder for " + folder.agentID.ToString());
            AddNewInventoryFolder(folder.agentID, folder);
            return true;
        }

        public bool AddInventoryItem( InventoryItemBase item)
        {
            AddNewInventoryItem(item.avatarID, item);
            return true;
        }

        public override void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            DeleteItem(item);
        }

        public bool DeleteInvItem( InventoryItemBase item)
        {
            DeleteInventoryItem(item.avatarID, item);
            return true;
        }
    }
}
