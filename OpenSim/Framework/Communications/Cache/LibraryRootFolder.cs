using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Data;

namespace OpenSim.Framework.Communications.Caches
{
    public class LibraryRootFolder : InventoryFolder
    {
        private LLUUID libOwner = new LLUUID("11111111-1111-0000-0000-000100bba000");

        public LibraryRootFolder()
        {
            this.agentID = libOwner;
            this.folderID = new LLUUID("00000112-000f-0000-0000-000100bba000");
            this.name = "OpenSim Library";
            this.parentID = LLUUID.Zero;
            this.type = (short)-1;
            this.version = (ushort) 1;

            this.CreateLibraryItems();
        }

        private void CreateLibraryItems()
        {
            InventoryItemBase item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000002");
            item.inventoryDescription = "Plywood texture";
            item.inventoryName = "Plywood";
            item.type = 0;
            item.parentFolderID = this.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000003");
            item.inventoryDescription = "Rocks texture";
            item.inventoryName = "Rocks";
            item.type = 0;
            item.parentFolderID = this.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000001");
            item.inventoryDescription = "Bricks texture";
            item.inventoryName = "Bricks";
            item.type = 0;
            item.parentFolderID = this.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000004");
            item.inventoryDescription = "Granite texture";
            item.inventoryName = "Granite";
            item.type = 0;
            item.parentFolderID = this.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000005");
            item.inventoryDescription = "Hardwood texture";
            item.inventoryName = "Hardwood";
            item.type = 0;
            item.parentFolderID = this.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfaba9");
            item.assetID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
            item.inventoryDescription = "Default Shape";
            item.inventoryName = "Default Shape";
            item.type = 13;
            item.parentFolderID = this.folderID;
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            this.Items.Add(item.inventoryID, item);
        }

    }
}
