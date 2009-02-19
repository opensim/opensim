create table InventoryFolders (
  ID NVARCHAR(255) not null,
   Type SMALLINT null,
   Version INT null,
   ParentID NVARCHAR(255) null,
   Owner NVARCHAR(255) null,
   Name NVARCHAR(64) null,
   primary key (ID)
)
create table InventoryItems (
  ID NVARCHAR(255) not null,
   InvType INT null,
   AssetType INT null,
   AssetID NVARCHAR(255) null,
   Folder NVARCHAR(255) null,
   Owner NVARCHAR(255) null,
   Creator NVARCHAR(255) null,
   Name NVARCHAR(64) null,
   Description NVARCHAR(64) null,
   NextPermissions INT null,
   CurrentPermissions INT null,
   BasePermissions INT null,
   EveryOnePermissions INT null,
   GroupID NVARCHAR(255) null,
   GroupOwned BIT null,
   SalePrice INT null,
   SaleType TINYINT null,
   Flags INT null,
   CreationDate INT null,
   primary key (ID)
)
create index item_group_id on InventoryItems (GroupID)
create index item_folder_id on InventoryItems (Folder)
create index item_owner_id on InventoryItems (Owner)
create index folder_owner_id on InventoryFolders (Owner)
create index folder_parent_id on InventoryFolders (ParentID)
