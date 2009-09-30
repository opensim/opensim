BEGIN;

ALTER TABLE regions add column sizeX integer not null default 0;
ALTER TABLE regions add column sizeY integer not null default 0;

COMMIT;
