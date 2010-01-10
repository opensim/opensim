BEGIN;

ALTER TABLE `regions` ADD COLUMN `last_seen` integer NOT NULL DEFAULT 0;

COMMIT;
