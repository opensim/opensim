BEGIN;

update inventoryitems 
  set UUID = substr(UUID, 1, 8) || "-" || substr(UUID, 9, 4) || "-" || substr(UUID, 13, 4) || "-" || substr(UUID, 17, 4) || "-" || substr(UUID, 21, 12)
  where UUID not like '%-%';

update inventoryitems
  set assetID = substr(assetID, 1, 8) || "-" || substr(assetID, 9, 4) || "-" || substr(assetID, 13, 4) || "-" || substr(assetID, 17, 4) || "-" || substr(assetID, 21, 12) 
  where assetID not like '%-%';

update inventoryitems
  set parentFolderID = substr(parentFolderID, 1, 8) || "-" || substr(parentFolderID, 9, 4) || "-" || substr(parentFolderID, 13, 4) || "-" || substr(parentFolderID, 17, 4) || "-" || substr(parentFolderID, 21, 12) 
  where parentFolderID not like '%-%';

update inventoryitems
  set avatarID = substr(avatarID, 1, 8) || "-" || substr(avatarID, 9, 4) || "-" || substr(avatarID, 13, 4) || "-" || substr(avatarID, 17, 4) || "-" || substr(avatarID, 21, 12) 
  where avatarID not like '%-%';

update inventoryitems
  set creatorsID = substr(creatorsID, 1, 8) || "-" || substr(creatorsID, 9, 4) || "-" || substr(creatorsID, 13, 4) || "-" || substr(creatorsID, 17, 4) || "-" || substr(creatorsID, 21, 12)
  where creatorsID not like '%-%';


update inventoryfolders
  set UUID = substr(UUID, 1, 8) || "-" || substr(UUID, 9, 4) || "-" || substr(UUID, 13, 4) || "-" || substr(UUID, 17, 4) || "-" || substr(UUID, 21, 12)
  where UUID not like '%-%';

update inventoryfolders
  set agentID = substr(agentID, 1, 8) || "-" || substr(agentID, 9, 4) || "-" || substr(agentID, 13, 4) || "-" || substr(agentID, 17, 4) || "-" || substr(agentID, 21, 12)
  where agentID not like '%-%';

update inventoryfolders
  set parentID = substr(parentID, 1, 8) || "-" || substr(parentID, 9, 4) || "-" || substr(parentID, 13, 4) || "-" || substr(parentID, 17, 4) || "-" || substr(parentID, 21, 12)
  where parentID not like '%-%';

COMMIT;
