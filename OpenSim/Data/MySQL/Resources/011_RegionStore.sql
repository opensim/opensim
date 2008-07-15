BEGIN;

ALTER TABLE prims change SceneGroupID SceneGroupIDold varchar(255);
ALTER TABLE prims add SceneGroupID char(36);
UPDATE prims set SceneGroupID = SceneGroupIDold;
ALTER TABLE prims drop SceneGroupIDold;
ALTER TABLE prims add index prims_scenegroupid(SceneGroupID);

COMMIT;