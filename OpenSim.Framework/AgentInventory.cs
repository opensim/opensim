using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Types;

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
            Wearables = new AvatarWearable[13]; //should be 12 of these
            for (int i = 0; i < 13; i++)
            {
                Wearables[i] = new AvatarWearable();
            }
      
        }

        public bool CreateNewFolder(LLUUID folderID, ushort type)
        {
            InventoryFolder Folder = new InventoryFolder();
            Folder.FolderID = folderID;
            Folder.OwnerID = this.AgentID;
            Folder.DefaultType = type;
            this.InventoryFolders.Add(Folder.FolderID, Folder);
            return (true);
        }

        public void CreateRootFolder(LLUUID newAgentID, bool createTextures)
        {
            this.AgentID = newAgentID;
            InventoryRoot = new InventoryFolder();
            InventoryRoot.FolderID = LLUUID.Random();
            InventoryRoot.ParentID = new LLUUID();
            InventoryRoot.Version = 1;
            InventoryRoot.DefaultType = 8;
            InventoryRoot.OwnerID = this.AgentID;
            InventoryRoot.FolderName = "My Inventory-";
            InventoryFolders.Add(InventoryRoot.FolderID, InventoryRoot);
            InventoryRoot.OwnerID = this.AgentID;
            if (createTextures)
            {
                this.CreateNewFolder(LLUUID.Random(), 0, "Textures", InventoryRoot.FolderID);
            }
        }

        public bool CreateNewFolder(LLUUID folderID, ushort type, string folderName)
        {
            InventoryFolder Folder = new InventoryFolder();
            Folder.FolderID = folderID;
            Folder.OwnerID = this.AgentID;
            Folder.DefaultType = type;
            Folder.FolderName = folderName;
            this.InventoryFolders.Add(Folder.FolderID, Folder);

            return (true);
        }

        public bool CreateNewFolder(LLUUID folderID, ushort type, string folderName, LLUUID parent)
        {
            Console.WriteLine("creating new folder called " + folderName + " in agents inventory");
            InventoryFolder Folder = new InventoryFolder();
            Folder.FolderID = folderID;
            Folder.OwnerID = this.AgentID;
            Folder.DefaultType = type;
            Folder.FolderName = folderName;
            Folder.ParentID = parent;
            this.InventoryFolders.Add(Folder.FolderID, Folder);

            return (true);
        }

        public bool HasFolder(LLUUID folderID)
        {
            if (this.InventoryFolders.ContainsKey(folderID))
            {
                return true;
            }
            return false;
        }

        public bool UpdateItemAsset(LLUUID itemID, AssetBase asset)
        {
            if(this.InventoryItems.ContainsKey(itemID))
            {
                InventoryItem Item = this.InventoryItems[itemID];
                Item.AssetID = asset.FullID;
                Console.WriteLine("updated inventory item " + itemID.ToStringHyphenated() + " so it now is set to asset " + asset.FullID.ToStringHyphenated());
                //TODO need to update the rest of the info
            }
            return true;
        }

        public bool UpdateItemDetails(LLUUID itemID, UpdateInventoryItemPacket.InventoryDataBlock packet)
        {
            Console.WriteLine("updating inventory item details");
            if (this.InventoryItems.ContainsKey(itemID))
            {
                Console.WriteLine("changing name to "+ Helpers.FieldToString(packet.Name));
                InventoryItem Item = this.InventoryItems[itemID];
                Item.Name = Helpers.FieldToString(packet.Name);
                Console.WriteLine("updated inventory item " + itemID.ToStringHyphenated());
                //TODO need to update the rest of the info
            }
            return true;
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

        public bool DeleteFromInventory(LLUUID itemID)
        {
            bool res = false;
            if (this.InventoryItems.ContainsKey(itemID))
            {
                InventoryItem item = this.InventoryItems[itemID];
                this.InventoryItems.Remove(itemID);
                foreach (InventoryFolder fold in InventoryFolders.Values)
                {
                    if (fold.Items.Contains(item))
                    {
                        fold.Items.Remove(item);
                        break;
                    }
                }
                res = true;
                
            }
            return res;
        }
    }

    public class InventoryFolder
    {
        public List<InventoryItem> Items;
        //public List<InventoryFolder> Subfolders;
        public LLUUID FolderID;
        public LLUUID OwnerID;
        public LLUUID ParentID = LLUUID.Zero;
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
        public string Name ="";
        public string Description;

        public InventoryItem()
        {
            this.CreatorID = LLUUID.Zero;
        }

        public string ExportString()
        {
            string typ = "notecard";
            string result = "";
            result += "\tinv_object\t0\n\t{\n";
            result += "\t\tobj_id\t%s\n";
            result +=  "\t\tparent_id\t"+ ItemID.ToString() +"\n";
            result += "\t\ttype\t"+ typ +"\n";
            result += "\t\tname\t" + Name+"|\n";
            result += "\t}\n";
            return result;
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
