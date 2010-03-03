BEGIN;

ALTER TABLE `regions` ADD COLUMN `flags` integer NOT NULL DEFAULT 0;
CREATE INDEX flags ON regions(flags);

COMMIT;
