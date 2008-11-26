BEGIN;

ALTER TABLE prims ADD COLUMN LinkNumber integer not null default 0;

COMMIT;

