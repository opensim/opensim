BEGIN;

ALTER TABLE prims change UUID UUIDold varchar(255);
ALTER TABLE prims change RegionUUID RegionUUIDold varchar(255);
ALTER TABLE prims change CreatorID CreatorIDold varchar(255);
ALTER TABLE prims change OwnerID OwnerIDold varchar(255);
ALTER TABLE prims change GroupID GroupIDold varchar(255);
ALTER TABLE prims change LastOwnerID LastOwnerIDold varchar(255);
ALTER TABLE prims add UUID char(36);
ALTER TABLE prims add RegionUUID char(36);
ALTER TABLE prims add CreatorID char(36);
ALTER TABLE prims add OwnerID char(36);
ALTER TABLE prims add GroupID char(36);
ALTER TABLE prims add LastOwnerID char(36);
UPDATE prims set UUID = UUIDold, RegionUUID = RegionUUIDold, CreatorID = CreatorIDold, OwnerID = OwnerIDold, GroupID = GroupIDold, LastOwnerID = LastOwnerIDold;
ALTER TABLE prims drop UUIDold;
ALTER TABLE prims drop RegionUUIDold;
ALTER TABLE prims drop CreatorIDold;
ALTER TABLE prims drop OwnerIDold;
ALTER TABLE prims drop GroupIDold;
ALTER TABLE prims drop LastOwnerIDold;
ALTER TABLE prims add constraint primary key(UUID);
ALTER TABLE prims add index prims_regionuuid(RegionUUID);

COMMIT;