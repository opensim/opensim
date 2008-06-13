BEGIN TRANSACTION;

create index inventoryfolders_agentid on inventoryfolders(agentid);
create index inventoryfolders_parentid on inventoryfolders(parentid);
create index inventoryitems_parentfolderid on inventoryitems(parentfolderid);
create index inventoryitems_avatarid on inventoryitems(avatarid);

COMMIT;