BEGIN;

ALTER TABLE assets change id oldid varchar(36);
ALTER TABLE assets add id char(36) not null default '00000000-0000-0000-0000-000000000000';
UPDATE assets set id = oldid;
ALTER TABLE assets drop oldid;
ALTER TABLE assets add constraint primary key(id);

COMMIT;