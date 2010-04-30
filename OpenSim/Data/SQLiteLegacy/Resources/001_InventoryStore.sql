BEGIN TRANSACTION;

CREATE TABLE inventoryfolders(
       UUID varchar(255) primary key,
       name varchar(255),
       agentID varchar(255),
       parentID varchar(255),
       type integer,
       version integer);

CREATE TABLE inventoryitems(
       UUID varchar(255) primary key,
       assetID varchar(255),
       assetType integer,
       invType integer,
       parentFolderID varchar(255),
       avatarID varchar(255),
       creatorsID varchar(255),
       inventoryName varchar(255),
       inventoryDescription varchar(255),
       inventoryNextPermissions integer,
       inventoryCurrentPermissions integer,
       inventoryBasePermissions integer,
       inventoryEveryOnePermissions integer, 
       salePrice integer default 99, 
       saleType integer default 0, 
       creationDate integer default 2000, 
       groupID varchar(255) default '00000000-0000-0000-0000-000000000000', 
       groupOwned integer default 0, 
       flags integer default 0);

COMMIT;
