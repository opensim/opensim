START TRANSACTION;

CREATE TABLE `InventoryFolders` (
  `ID` char(36) NOT NULL,
  `Type` int(11) default NULL,
  `Version` int(11) default NULL,
  `ParentID` char(36) default NULL,
  `Owner` char(36) default NULL,
  `Name` varchar(64) default NULL,
  PRIMARY KEY  (`ID`),
  KEY `folder_owner_id` (`Owner`),
  KEY `folder_parent_id` (`ParentID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE `InventoryItems` (
  `ID` char(36) NOT NULL,
  `InvType` smallint(6) default NULL,
  `AssetType` smallint(6) default NULL,
  `AssetID` char(36) default NULL,
  `Folder` char(36) default NULL,
  `Owner` char(36) default NULL,
  `Creator` char(36) default NULL,
  `Name` varchar(64) default NULL,
  `Description` varchar(64) default NULL,
  `NextPermissions` int(11) default NULL,
  `CurrentPermissions` int(11) default NULL,
  `BasePermissions` int(11) default NULL,
  `EveryOnePermissions` int(11) default NULL,
  `GroupID` char(36) default NULL,
  `GroupOwned` tinyint(1) default NULL,
  `SalePrice` int(11) default NULL,
  `SaleType` smallint(6) default NULL,
  `Flags` int(11) default NULL,
  `CreationDate` int(11) default NULL,
  PRIMARY KEY  (`ID`),
  KEY `item_group_id` (`GroupID`),
  KEY `item_owner_id` (`Owner`),
  KEY `item_folder_id` (`Folder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;