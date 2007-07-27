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
        private InventoryFolder m_textureFolder;

        public LibraryRootFolder()
        {
            this.agentID = libOwner;
            this.folderID = new LLUUID("00000112-000f-0000-0000-000100bba000");
            this.name = "OpenSim Library";
            this.parentID = LLUUID.Zero;
            this.type = (short)-1;
            this.version = (ushort) 1;

            InventoryFolder folderInfo = new InventoryFolder();
            folderInfo.agentID = libOwner;
            folderInfo.folderID = new LLUUID("00000112-000f-0000-0000-000100bba001");
            folderInfo.name = "Texture Library";
            folderInfo.parentID = this.folderID;
            folderInfo.type = -1;
            folderInfo.version = 1;
            this.SubFolders.Add(folderInfo.folderID, folderInfo);
            this.m_textureFolder = folderInfo;

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
            item.assetType = 0;
            item.parentFolderID = m_textureFolder.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.m_textureFolder.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000003");
            item.inventoryDescription = "Rocks texture";
            item.inventoryName = "Rocks";
            item.assetType = 0;
            item.parentFolderID = m_textureFolder.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.m_textureFolder.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000001");
            item.inventoryDescription = "Bricks texture";
            item.inventoryName = "Bricks";
            item.assetType = 0;
            item.parentFolderID = m_textureFolder.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.m_textureFolder.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000004");
            item.inventoryDescription = "Granite texture";
            item.inventoryName = "Granite";
            item.assetType = 0;
            item.parentFolderID = m_textureFolder.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.m_textureFolder.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = LLUUID.Random();
            item.assetID = new LLUUID("00000000-0000-0000-9999-000000000005");
            item.inventoryDescription = "Hardwood texture";
            item.inventoryName = "Hardwood";
            item.assetType = 0;
            item.parentFolderID = m_textureFolder.folderID;
            item.inventoryBasePermissions = 0x7FFFFFFF;
            item.inventoryEveryOnePermissions = 0x7FFFFFFF;
            item.inventoryCurrentPermissions = 0x7FFFFFFF;
            item.inventoryNextPermissions = 0x7FFFFFFF;
            this.m_textureFolder.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfaba9");
            item.assetID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
            item.inventoryDescription = "Default Shape";
            item.inventoryName = "Default Shape";
            item.assetType = 13;
            item.invType = 18;
            item.parentFolderID = this.folderID;
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = new LLUUID("77c41e39-38f9-f75a-024e-585989bfabc9");
            item.assetID = new LLUUID("77c41e39-38f9-f75a-024e-585989bbabbb");
            item.inventoryDescription = "Default Skin";
            item.inventoryName = "Default Skin";
            item.assetType = 13;
            item.invType = 18;
            item.parentFolderID = this.folderID;
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = new LLUUID("77c41e39-38f9-f75a-0000-585989bf0000");
            item.assetID = new LLUUID("00000000-38f9-1111-024e-222222111110");
            item.inventoryDescription = "Default Shirt";
            item.inventoryName = "Default Shirt";
            item.assetType = 5;
            item.invType = 18;
            item.parentFolderID = this.folderID;
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = new LLUUID("77c41e39-38f9-f75a-0000-5859892f1111");
            item.assetID = new LLUUID("00000000-38f9-1111-024e-222222111120");
            item.inventoryDescription = "Default Pants";
            item.inventoryName = "Default Pants";
            item.assetType = 5;
            item.invType = 18;
            item.parentFolderID = this.folderID;
            item.inventoryCurrentPermissions = 0;
            item.inventoryNextPermissions = 0;
            this.Items.Add(item.inventoryID, item);

            item = new InventoryItemBase();
            item.avatarID = libOwner;
            item.creatorsID = libOwner;
            item.inventoryID = new LLUUID("00000000-0000-2222-4444-000000000001");
            item.assetID = new LLUUID("00000000-0000-2222-3333-000000000001");
            item.inventoryDescription = "Welcome";
            item.inventoryName = "Welcome";
            item.assetType = 7;
            item.invType = 7;
            item.parentFolderID = this.folderID;
            item.inventoryCurrentPermissions = (1 << 15);
            item.inventoryNextPermissions = (1 << 15);
            item.inventoryEveryOnePermissions = (1 << 15);
            item.inventoryBasePermissions = (1 << 15);
            this.Items.Add(item.inventoryID, item);

            
        }

    }
}
