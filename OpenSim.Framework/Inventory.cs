using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Assets;

namespace OpenSim.Framework.Inventory
{
    public class AgentInventory
    {
        //Holds the local copy of Inventory info for a agent
        public Dictionary<LLUUID, InventoryFolder> InventoryFolders;
        public Dictionary<LLUUID, InventoryItem> InventoryItems;
        public InventoryFolder InventoryRoot;
        public int LastCached;  //maybe used by opensim app, time this was last stored/compared to user server
        public LLUUID AgentID;
        public AvatarWearable[] Wearables;

        public AgentInventory()
        {
            InventoryFolders = new Dictionary<LLUUID, InventoryFolder>();
            InventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.Initialise();
        }

        public virtual void Initialise()
        {
            Wearables = new AvatarWearable[2]; //should be 12 of these
            for (int i = 0; i < 2; i++)
            {
                Wearables[i] = new AvatarWearable();
            }

            InventoryRoot = new InventoryFolder();
            InventoryRoot.FolderID = LLUUID.Random();
            InventoryRoot.ParentID = new LLUUID();
            InventoryRoot.Version = 1;
            InventoryRoot.DefaultType = 8;
            InventoryRoot.FolderName = "My Inventory";
            InventoryFolders.Add(InventoryRoot.FolderID, InventoryRoot);
        }

        public bool CreateNewFolder(LLUUID folderID)
        {
            InventoryFolder Folder = new InventoryFolder();
            Folder.FolderID = folderID;
            Folder.OwnerID = this.AgentID;
            this.InventoryFolders.Add(Folder.FolderID, Folder);

            return (true);
        }

        public LLUUID AddToInventory(LLUUID folderID, AssetBase asset)
        {
            if (this.InventoryFolders.ContainsKey(folderID))
            {
                LLUUID NewItemID = LLUUID.Random();

                InventoryItem Item = new InventoryItem();
                Item.FolderID = folderID;
                Item.OwnerID = AgentID;
                Item.AssetID = asset.FullID;
                Item.ItemID = NewItemID;
                Item.Type = asset.Type;
                Item.Name = asset.Name;
                Item.Description = asset.Description;
                Item.InvType = asset.InvType;
                this.InventoryItems.Add(Item.ItemID, Item);
                InventoryFolder Folder = InventoryFolders[Item.FolderID];
                Folder.Items.Add(Item);
                return (Item.ItemID);
            }
            else
            {
                return (null);
            }
        }
    }

    public class InventoryFolder
    {
        public List<InventoryItem> Items;
        //public List<InventoryFolder> Subfolders;
        public LLUUID FolderID;
        public LLUUID OwnerID;
        public LLUUID ParentID;
        public string FolderName;
        public ushort DefaultType;
        public ushort Version;

        public InventoryFolder()
        {
            Items = new List<InventoryItem>();
            //Subfolders = new List<InventoryFolder>();
        }

    }

    public class InventoryItem
    {
        public LLUUID FolderID;
        public LLUUID OwnerID;
        public LLUUID ItemID;
        public LLUUID AssetID;
        public LLUUID CreatorID;
        public sbyte InvType;
        public sbyte Type;
        public string Name;
        public string Description;

        public InventoryItem()
        {
            this.CreatorID = LLUUID.Zero;
        }
    }

    public class AvatarWearable
    {
        public LLUUID AssetID = new LLUUID("00000000-0000-0000-0000-000000000000");
        public LLUUID ItemID = new LLUUID("00000000-0000-0000-0000-000000000000");

        public AvatarWearable()
        {

        }
    }
}
