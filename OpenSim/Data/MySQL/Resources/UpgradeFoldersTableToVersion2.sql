ALTER TABLE `inventoryfolders` 
	ADD COLUMN `type` smallint NOT NULL default 0,
	ADD COLUMN `version` int NOT NULL default 0,
COMMENT='Rev. 2';
