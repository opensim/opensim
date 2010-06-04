BEGIN TRANSACTION;

-- users table
CREATE TABLE users(
       UUID varchar(255) primary key,
       username varchar(255),
       surname varchar(255),
       passwordHash varchar(255),
       passwordSalt varchar(255),
       homeRegionX integer,
       homeRegionY integer,
       homeLocationX float,
       homeLocationY float,
       homeLocationZ float,
       homeLookAtX float,
       homeLookAtY float,
       homeLookAtZ float,
       created integer,
       lastLogin integer,
       rootInventoryFolderID varchar(255),
       userInventoryURI varchar(255),
       userAssetURI varchar(255),
       profileCanDoMask integer,
       profileWantDoMask integer,
       profileAboutText varchar(255),
       profileFirstText varchar(255),
       profileImage varchar(255),
       profileFirstImage varchar(255), 
       webLoginKey text default '00000000-0000-0000-0000-000000000000');
-- friends table
CREATE TABLE userfriends(
       ownerID varchar(255),
       friendID varchar(255),
       friendPerms integer,
       ownerPerms integer,
       datetimestamp integer);

COMMIT;

