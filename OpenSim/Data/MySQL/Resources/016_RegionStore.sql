BEGIN;

ALTER TABLE prims ADD COLUMN PayPrice integer not null default 0;
ALTER TABLE prims ADD COLUMN PayButton1 integer not null default 0;
ALTER TABLE prims ADD COLUMN PayButton2 integer not null default 0;
ALTER TABLE prims ADD COLUMN PayButton3 integer not null default 0;
ALTER TABLE prims ADD COLUMN PayButton4 integer not null default 0;
ALTER TABLE prims ADD COLUMN LoopedSound char(36) not null default '00000000-0000-0000-0000-000000000000';
ALTER TABLE prims ADD COLUMN LoopedSoundGain float not null default 0.0;
ALTER TABLE prims ADD COLUMN TextureAnimation blob;
ALTER TABLE prims ADD COLUMN OmegaX float not null default 0.0;
ALTER TABLE prims ADD COLUMN OmegaY float not null default 0.0;
ALTER TABLE prims ADD COLUMN OmegaZ float not null default 0.0;
ALTER TABLE prims ADD COLUMN CameraEyeOffsetX float not null default 0.0;
ALTER TABLE prims ADD COLUMN CameraEyeOffsetY float not null default 0.0;
ALTER TABLE prims ADD COLUMN CameraEyeOffsetZ float not null default 0.0;
ALTER TABLE prims ADD COLUMN CameraAtOffsetX float not null default 0.0;
ALTER TABLE prims ADD COLUMN CameraAtOffsetY float not null default 0.0;
ALTER TABLE prims ADD COLUMN CameraAtOffsetZ float not null default 0.0;
ALTER TABLE prims ADD COLUMN ForceMouselook tinyint not null default 0;
ALTER TABLE prims ADD COLUMN ScriptAccessPin integer not null default 0;
ALTER TABLE prims ADD COLUMN AllowedDrop tinyint not null default 0;
ALTER TABLE prims ADD COLUMN DieAtEdge tinyint not null default 0;
ALTER TABLE prims ADD COLUMN SalePrice integer not null default 10;
ALTER TABLE prims ADD COLUMN SaleType tinyint not null default 0;

COMMIT;
