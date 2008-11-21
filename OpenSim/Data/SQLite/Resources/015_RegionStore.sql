BEGIN;

ALTER TABLE prims ADD COLUMN CollisionSound varchar(36) NOT NULL default '00000000-0000-0000-0000-000000000000';
ALTER TABLE prims ADD COLUMN CollisionSoundVolume float NOT NULL default 0;

COMMIT;
