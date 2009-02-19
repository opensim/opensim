CREATE TABLE InventoryFolders (
  ID VARCHAR(36) NOT NULL,
  Type SMALLINT DEFAULT NULL,
  Version INT DEFAULT NULL,
  ParentID VARCHAR(36) DEFAULT NULL,
  Owner VARCHAR(36) DEFAULT NULL,
  Name VARCHAR(64) DEFAULT NULL,
  PRIMARY KEY (ID)
);

CREATE INDEX InventoryFoldersOwnerIdIndex ON InventoryFolders (Owner);
CREATE INDEX InventoryFoldersParentIdIndex ON InventoryFolders (ParentID);

CREATE TABLE InventoryItems (
  ID VARCHAR(36) NOT NULL,
  InvType INT DEFAULT NULL,
  AssetType INT DEFAULT NULL,
  AssetID VARCHAR(36) DEFAULT NULL,
  Folder VARCHAR(36) DEFAULT NULL,
  Owner VARCHAR(36) DEFAULT NULL,
  Creator VARCHAR(36) DEFAULT NULL,
  Name VARCHAR(64) DEFAULT NULL,
  Description VARCHAR(64) DEFAULT NULL,
  NextPermissions INT DEFAULT NULL,
  CurrentPermissions INT DEFAULT NULL,
  BasePermissions INT DEFAULT NULL,
  EveryOnePermissions INT DEFAULT NULL,
  GroupID VARCHAR(36) DEFAULT NULL,
  GroupOwned BOOLEAN DEFAULT NULL,
  SalePrice INT DEFAULT NULL,
  SaleType SMALLINT DEFAULT NULL,
  Flags INT DEFAULT NULL,
  CreationDate INT DEFAULT NULL,
  PRIMARY KEY (ID)
);

CREATE INDEX InventoryItemsGroupIdIndex ON InventoryItems (GroupID);
CREATE INDEX InventoryItemsOwnerIdIndex ON InventoryItems (Owner);
CREATE INDEX InventoryItemsFolderIdIndex ON InventoryItems (Folder);
