ALTER TABLE `inventoryitems` 
	CHANGE COLUMN `type` `assetType` int(11) default NULL,
	ADD COLUMN `invType` int(11) default NULL,
	ADD COLUMN `creatorID` varchar(36) default NULL,
	ADD COLUMN `inventoryBasePermissions` int(10) unsigned NOT NULL default 0,
	ADD COLUMN `inventoryEveryOnePermissions` int(10) unsigned NOT NULL default 0,
COMMENT='Rev. 2';

UPDATE `inventoryitems` SET invType=assetType;
