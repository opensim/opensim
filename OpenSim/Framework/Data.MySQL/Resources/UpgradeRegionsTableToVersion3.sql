DROP PROCEDURE IF EXISTS upgraderegions3;

create procedure upgraderegions3()
BEGIN
DECLARE db_name varchar(64);
select database() into db_name;
IF ((select count(*) from information_schema.columns where table_name='regions' and column_name='owner_uuid' and table_schema=db_name) > 0)
THEN
    ALTER TABLE `regions`, COMMENT='Rev. 3';
ELSE
    ALTER TABLE `regions`
        ADD COLUMN `owner_uuid` varchar(36) default '00000000-0000-0000-0000-000000000000' not null after serverRemotingPort, COMMENT='Rev. 3';
END IF;
END; 

call upgraderegions3();


