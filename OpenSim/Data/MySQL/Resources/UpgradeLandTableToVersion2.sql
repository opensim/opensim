ALTER TABLE `land` 
	ADD COLUMN `AuthbuyerID` varchar(36) default '00000000-0000-0000-0000-000000000000' not null,
COMMENT='Rev. 2';