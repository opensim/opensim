BEGIN;

ALTER TABLE prims ADD COLUMN ColorR integer not null default 0;
ALTER TABLE prims ADD COLUMN ColorG integer not null default 0;
ALTER TABLE prims ADD COLUMN ColorB integer not null default 0;
ALTER TABLE prims ADD COLUMN ColorA integer not null default 0;
ALTER TABLE prims ADD COLUMN ParticleSystem blob;

COMMIT;
