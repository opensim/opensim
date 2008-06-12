BEGIN;

CREATE TABLE `inventoryfolders` (
  `folderID` varchar(36) NOT NULL default '',
  `agentID` varchar(36) default NULL,
  `parentFolderID` varchar(36) default NULL,
  `folderName` varchar(64) default NULL,
  `type` smallint NOT NULL default 0,
  `version` int NOT NULL default 0,
  PRIMARY KEY  (`folderID`),
  KEY `owner` (`agentID`),
  KEY `parent` (`parentFolderID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE `inventoryitems` (
  `inventoryID` varchar(36) NOT NULL default '',
  `assetID` varchar(36) default NULL,
  `assetType` int(11) default NULL,
  `parentFolderID` varchar(36) default NULL,
  `avatarID` varchar(36) default NULL,
  `inventoryName` varchar(64) default NULL,
  `inventoryDescription` varchar(128) default NULL,
  `inventoryNextPermissions` int(10) unsigned default NULL,
  `inventoryCurrentPermissions` int(10) unsigned default NULL,
  `invType` int(11) default NULL,
  `creatorID` varchar(36) default NULL,
  `inventoryBasePermissions` int(10) unsigned NOT NULL default 0,
  `inventoryEveryOnePermissions` int(10) unsigned NOT NULL default 0,
  `salePrice` int(11) NOT NULL default 0,
  `saleType` tinyint(4) NOT NULL default 0,
  `creationDate` int(11) NOT NULL default 0,
  `groupID` varchar(36) NOT NULL default '00000000-0000-0000-0000-000000000000',
  `groupOwned` tinyint(4) NOT NULL default 0,
  `flags` int(11) unsigned NOT NULL default 0,
  PRIMARY KEY  (`inventoryID`),
  KEY `owner` (`avatarID`),
  KEY `folder` (`parentFolderID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

COMMIT;