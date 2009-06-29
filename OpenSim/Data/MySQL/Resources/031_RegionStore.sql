BEGIN;

ALTER TABLE regionsettings DROP COLUMN loaded_creation_date;
ALTER TABLE regionsettings DROP COLUMN loaded_creation_time;
ALTER TABLE regionsettings ADD COLUMN loaded_creation_datetime int unsigned NOT NULL default 0;

COMMIT;
