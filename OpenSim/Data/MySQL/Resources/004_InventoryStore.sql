BEGIN;

update inventoryitems set creatorID = '00000000-0000-0000-0000-000000000000' where creatorID is NULL;
update inventoryitems set creatorID = '00000000-0000-0000-0000-000000000000' where creatorID = '';
alter table inventoryitems modify column creatorID varchar(36) not NULL default '00000000-0000-0000-0000-000000000000';

COMMIT;
