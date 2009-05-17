BEGIN;

ALTER TABLE regions add column access integer unsigned default 1;

COMMIT;
