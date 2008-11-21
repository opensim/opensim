BEGIN TRANSACTION

ALTER TABLE prims ADD CollisionSound char(36) not null default '00000000-0000-0000-0000-000000000000'
ALTER TABLE prims ADD CollisionSoundVolume float not null default 0.0

COMMIT
