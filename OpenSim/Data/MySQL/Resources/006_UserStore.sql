BEGIN;

ALTER TABLE agents add currentLookAt varchar(36) not null default '';

COMMIT;
