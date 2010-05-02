BEGIN TRANSACTION;

CREATE TABLE inventoryfolders(
       folderName varchar(255),
       type integer,
       version integer,
       folderID varchar(255) primary key,
       agentID varchar(255) not null default '00000000-0000-0000-0000-000000000000',
       parentFolderID varchar(255) not null default '00000000-0000-0000-0000-000000000000');

CREATE TABLE inventoryitems(
       assetID varchar(255),
       assetType integer,
       inventoryName varchar(255),
       inventoryDescription varchar(255),
       inventoryNextPermissions integer,
       inventoryCurrentPermissions integer,
       invType integer,
       creatorID varchar(255),
       inventoryBasePermissions integer,
       inventoryEveryOnePermissions integer, 
       salePrice integer default 99, 
       saleType integer default 0, 
       creationDate integer default 2000, 
       groupID varchar(255) default '00000000-0000-0000-0000-000000000000', 
       groupOwned integer default 0, 
       flags integer default 0,
       inventoryID varchar(255) primary key,
       parentFolderID varchar(255) not null default '00000000-0000-0000-0000-000000000000',
       avatarID varchar(255) not null default '00000000-0000-0000-0000-000000000000',
       inventoryGroupPermissions integer not null default 0);

create index inventoryfolders_agentid on inventoryfolders(agentID);
create index inventoryfolders_parentid on inventoryfolders(parentFolderID);
create index inventoryitems_parentfolderid on inventoryitems(parentFolderID);
create index inventoryitems_avatarid on inventoryitems(avatarID);

COMMIT;
