BEGIN;

ALTER TABLE assets add create_time integer default 0;
ALTER TABLE assets add access_time integer default 0;

COMMIT;
