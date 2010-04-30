BEGIN;

ALTER TABLE prims ADD COLUMN PayPrice INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN PayButton1 INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN PayButton2 INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN PayButton3 INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN PayButton4 INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN LoopedSound varchar(36) NOT NULL default '00000000-0000-0000-0000-000000000000';
ALTER TABLE prims ADD COLUMN LoopedSoundGain float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN TextureAnimation string;
ALTER TABLE prims ADD COLUMN ParticleSystem string;
ALTER TABLE prims ADD COLUMN OmegaX float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN OmegaY float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN OmegaZ float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN CameraEyeOffsetX float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN CameraEyeOffsetY float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN CameraEyeOffsetZ float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN CameraAtOffsetX float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN CameraAtOffsetY float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN CameraAtOffsetZ float NOT NULL default 0;
ALTER TABLE prims ADD COLUMN ForceMouselook string NOT NULL default 0;
ALTER TABLE prims ADD COLUMN ScriptAccessPin INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN AllowedDrop INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN DieAtEdge string NOT NULL default 0;
ALTER TABLE prims ADD COLUMN SalePrice INTEGER NOT NULL default 0;
ALTER TABLE prims ADD COLUMN SaleType string NOT NULL default 0;

COMMIT;