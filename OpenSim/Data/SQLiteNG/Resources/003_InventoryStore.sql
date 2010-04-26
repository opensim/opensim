BEGIN;

alter table inventoryitems add column inventoryGroupPermissions integer unsigned not null default 0;

COMMIT;
