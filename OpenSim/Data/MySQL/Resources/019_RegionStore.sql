begin;

ALTER TABLE prims ADD COLUMN Material tinyint NOT NULL default 3;

commit;

