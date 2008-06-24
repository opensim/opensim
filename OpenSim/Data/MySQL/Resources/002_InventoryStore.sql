BEGIN;

ALTER TABLE inventoryfolders change folderID folderIDold varchar(36);
ALTER TABLE inventoryfolders change agentID agentIDold varchar(36);
ALTER TABLE inventoryfolders change parentFolderID parentFolderIDold varchar(36);
ALTER TABLE inventoryfolders add folderID char(36) not null default '00000000-0000-0000-0000-000000000000';
ALTER TABLE inventoryfolders add agentID char(36) default NULL;
ALTER TABLE inventoryfolders add parentFolderID char(36) default NULL;
UPDATE inventoryfolders set folderID = folderIDold, agentID = agentIDold, parentFolderID = parentFolderIDold;
ALTER TABLE inventoryfolders drop folderIDold;
ALTER TABLE inventoryfolders drop agentIDold;
ALTER TABLE inventoryfolders drop parentFolderIDold;
ALTER TABLE inventoryfolders add constraint primary key(folderID);
ALTER TABLE inventoryfolders add index inventoryfolders_agentid(agentID);
ALTER TABLE inventoryfolders add index inventoryfolders_parentFolderid(parentFolderID);

ALTER TABLE inventoryitems change inventoryID inventoryIDold varchar(36);
ALTER TABLE inventoryitems change avatarID avatarIDold varchar(36);
ALTER TABLE inventoryitems change parentFolderID parentFolderIDold varchar(36);
ALTER TABLE inventoryitems add inventoryID char(36) not null default '00000000-0000-0000-0000-000000000000';
ALTER TABLE inventoryitems add avatarID char(36) default NULL;
ALTER TABLE inventoryitems add parentFolderID char(36) default NULL;
UPDATE inventoryitems set inventoryID = inventoryIDold, avatarID = avatarIDold, parentFolderID = parentFolderIDold;
ALTER TABLE inventoryitems drop inventoryIDold;
ALTER TABLE inventoryitems drop avatarIDold;
ALTER TABLE inventoryitems drop parentFolderIDold;
ALTER TABLE inventoryitems add constraint primary key(inventoryID);
ALTER TABLE inventoryitems add index inventoryitems_avatarid(avatarID);
ALTER TABLE inventoryitems add index inventoryitems_parentFolderid(parentFolderID);

COMMIT;