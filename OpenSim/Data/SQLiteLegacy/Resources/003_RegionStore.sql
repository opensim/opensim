BEGIN;

ALTER TABLE primitems add flags integer not null default 0;

COMMIT;