BEGIN TRANSACTION

ALTER TABLE prims ADD PayPrice int not null default 0
ALTER TABLE prims ADD PayButton1 int not null default 0
ALTER TABLE prims ADD PayButton2 int not null default 0
ALTER TABLE prims ADD PayButton3 int not null default 0
ALTER TABLE prims ADD PayButton4 int not null default 0
ALTER TABLE prims ADD LoopedSound varchar(36) not null default '00000000-0000-0000-0000-000000000000';
ALTER TABLE prims ADD LoopedSoundGain float not null default 0.0;
ALTER TABLE prims ADD TextureAnimation image
ALTER TABLE prims ADD OmegaX float not null default 0.0
ALTER TABLE prims ADD OmegaY float not null default 0.0
ALTER TABLE prims ADD OmegaZ float not null default 0.0
ALTER TABLE prims ADD CameraEyeOffsetX float not null default 0.0
ALTER TABLE prims ADD CameraEyeOffsetY float not null default 0.0
ALTER TABLE prims ADD CameraEyeOffsetZ float not null default 0.0
ALTER TABLE prims ADD CameraAtOffsetX float not null default 0.0
ALTER TABLE prims ADD CameraAtOffsetY float not null default 0.0
ALTER TABLE prims ADD CameraAtOffsetZ float not null default 0.0
ALTER TABLE prims ADD ForceMouselook tinyint not null default 0
ALTER TABLE prims ADD ScriptAccessPin int not null default 0
ALTER TABLE prims ADD AllowedDrop tinyint not null default 0
ALTER TABLE prims ADD DieAtEdge tinyint not null default 0
ALTER TABLE prims ADD SalePrice int not null default 10
ALTER TABLE prims ADD SaleType tinyint not null default 0

ALTER TABLE primitems add flags integer not null default 0

ALTER TABLE land ADD AuthbuyerID varchar(36) NOT NULL default '00000000-0000-0000-0000-000000000000'

CREATE index prims_regionuuid on prims(RegionUUID)
CREATE index prims_parentid on prims(ParentID)

CREATE index primitems_primid on primitems(primID)

COMMIT
