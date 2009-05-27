BEGIN;

ALTER TABLE prims ADD COLUMN PassTouches tinyint not null default 0;

COMMIT;
