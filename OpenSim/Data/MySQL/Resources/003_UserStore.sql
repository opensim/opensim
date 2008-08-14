BEGIN;

ALTER TABLE users add userFlags integer NOT NULL default 0;
ALTER TABLE users add godLevel integer NOT NULL default 0;

COMMIT;
