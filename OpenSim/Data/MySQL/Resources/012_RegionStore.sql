BEGIN;

ALTER TABLE prims add index prims_parentid(ParentID);

COMMIT;