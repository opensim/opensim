BEGIN TRANSACTION;

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
        inventoryEveryOnePermissions integer);

CREATE TABLE inventoryfolders(
        UUID varchar(255) primary key,
        name varchar(255),
        agentID varchar(255),
        parentID varchar(255),
        type integer,
        version integer);

COMMIT;
