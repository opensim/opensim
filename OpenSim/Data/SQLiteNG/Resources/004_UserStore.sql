BEGIN;

ALTER TABLE users add customType varchar(32) not null default '';
ALTER TABLE users add partner char(36) not null default '00000000-0000-0000-0000-000000000000';

COMMIT;
