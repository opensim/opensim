BEGIN TRANSACTION;

ATTACH 'inventoryStore.db' AS old;

INSERT INTO inventoryfolders (folderName, type, version, folderID, agentID, parentFolderID) SELECT `name` AS folderName, `type` AS type, `version` AS version, `UUID` AS folderID, `agentID` AS agentID, `parentID` AS parentFolderID from old.inventoryfolders;

INSERT INTO inventoryitems (assetID, assetType, inventoryName, inventoryDescription, inventoryNextPermissions, inventoryCurrentPermissions, invType, creatorID, inventoryBasePermissions, inventoryEveryOnePermissions, salePrice, saleType, creationDate, groupID, groupOwned, flags, inventoryID, parentFolderID, avatarID, inventoryGroupPermissions) SELECT `assetID`, `assetType` AS assetType, `inventoryName` AS inventoryName, `inventoryDescription` AS inventoryDescription, `inventoryNextPermissions` AS inventoryNextPermissions, `inventoryCurrentPermissions` AS inventoryCurrentPermissions, `invType` AS invType, `creatorsID` AS creatorID, `inventoryBasePermissions` AS inventoryBasePermissions, `inventoryEveryOnePermissions` AS inventoryEveryOnePermissions, `salePrice` AS salePrice, `saleType` AS saleType, `creationDate` AS creationDate, `groupID` AS groupID, `groupOwned` AS groupOwned, `flags` AS flags, `UUID` AS inventoryID, `parentFolderID` AS parentFolderID, `avatarID` AS avatarID, `inventoryGroupPermissions` AS inventoryGroupPermissions FROM old.inventoryitems;

COMMIT;
