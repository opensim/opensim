BEGIN;

ALTER TABLE regionsettings ADD COLUMN loaded_creation_date varchar(20) default NULL;
ALTER TABLE regionsettings ADD COLUMN loaded_creation_time varchar(20) default NULL;
ALTER TABLE regionsettings ADD COLUMN loaded_creation_id varchar(64) default NULL;

COMMIT;
