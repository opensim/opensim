BEGIN;

CREATE TABLE `PrimItems` (
  `InvType` int(11) default NULL,
  `AssetType` int(11) default NULL,
  `Name` varchar(255) default NULL,
  `Description` varchar(255) default NULL,
  `CreationDate` bigint(20) default NULL,
  `NextPermissions` int(11) default NULL,
  `CurrentPermissions` int(11) default NULL,
  `BasePermissions` int(11) default NULL,
  `EveryonePermissions` int(11) default NULL,
  `GroupPermissions` int(11) default NULL,
  `Flags` int(11) NOT NULL default '0',
  `ItemID` char(36) NOT NULL default '',
  `PrimID` char(36) default NULL,
  `AssetID` char(36) default NULL,
  `ParentFolderID` char(36) default NULL,
  `CreatorID` char(36) default NULL,
  `OwnerID` char(36) default NULL,
  `GroupID` char(36) default NULL,
  `LastOwnerID` char(36) default NULL,
  PRIMARY KEY  (`ItemID`),
  KEY `primitems_primid` (`PrimID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;