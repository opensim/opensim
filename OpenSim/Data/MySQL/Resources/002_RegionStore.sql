BEGIN;

CREATE index prims_regionuuid on prims(RegionUUID);
CREATE index primitems_primid on primitems(primID);

COMMIT;