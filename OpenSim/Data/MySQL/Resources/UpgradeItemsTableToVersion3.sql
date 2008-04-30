ALTER TABLE `inventoryitems` 
	ADD COLUMN `salePrice` int(11) NOT NULL,
	ADD COLUMN `saleType` tinyint(4) NOT NULL,
	ADD COLUMN `creationDate` int(11) NOT NULL,
	ADD COLUMN `groupID` varchar(36) NOT NULL default '00000000-0000-0000-0000-000000000000',
	ADD COLUMN `groupOwned` tinyint(4) NOT NULL,
	ADD COLUMN `flags` int(11) unsigned NOT NULL,
COMMENT='Rev. 3';
