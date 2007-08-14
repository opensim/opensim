--
-- Database schema for inventory storage
--
-- 
-- Some type mappings
-- LLUID => char(36) (in ascii hex format)
-- uint => integer
-- string => varchar(256) until such time as we know we need bigger

create table inventoryitems (
        UUID char(36) primary key, -- inventoryid
        assetID char(36), 
        assetType integer, 
        invType integer,
	parentFolderID char(36),
        avatarID char(36),
        creatorsID char(36),
        inventoryName varchar(256),
        inventoryDescription varchar(256),
        -- permissions
        inventoryNextPermissions integer,
        inventoryCurrentPermissions integer,
        inventoryBasePermissions integer,
        inventoryEveryOnePermissions integer
);

create index inventoryitems_parent on inventoryitems(parentFolderID);
create index inventoryitems_ownerid on inventoryitems(avatarID);
create index inventoryitems_assetid on inventoryitems(assetID);

create table inventoryfolders (
        -- The same UUID as prim, just to keep them easily linked
        UUID varchar(36) primary key not null, --folderid
        name varchar(256), 
        agentID char(36),
        parentID char(36),
        type integer,
        version integer
);

