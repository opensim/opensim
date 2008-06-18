BEGIN TRANSACTION;

CREATE TABLE InventoryFolders (
       ID varchar(36) not null primary key, 
       Type int,
       Version int,
       ParentID varchar(36),
       Owner varchar(36),
       Name varchar(64)
);

create table InventoryItems (
   ID varchar(36) not null primary key,
   InvType int,
   AssetType int,
   AssetID varchar(36),
   Folder varchar(36),
   Owner varchar(36),
   Creator varchar(36),
   Name varchar(64),
   Description varchar(64),
   NextPermissions int,
   CurrentPermissions int,
   BasePermissions int,
   EveryOnePermissions int,
   GroupID varchar(36),
   GroupOwned int,
   SalePrice int,
   SaleType int,
   Flags int,
   CreationDate int
);

CREATE INDEX folder_owner_id on InventoryFolders (Owner);
CREATE INDEX folder_parent_id on InventoryFolders (ParentID);
CREATE INDEX item_group_id on InventoryItems (GroupID);
CREATE INDEX item_owner_id on InventoryItems (Owner);
CREATE INDEX item_folder_id on InventoryItems (Folder);

COMMIT;
