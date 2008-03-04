ALTER TABLE `regions` 
	ADD COLUMN `originUUID` varchar(36),
COMMENT='Rev. 2';
UPDATE `regions` SET originUUID=uuid;
