BEGIN TRANSACTION

ALTER TABLE prims ADD ColorR int not null default 0;
ALTER TABLE prims ADD ColorG int not null default 0;
ALTER TABLE prims ADD ColorB int not null default 0;
ALTER TABLE prims ADD ColorA int not null default 0;
ALTER TABLE prims ADD ParticleSystem IMAGE;
ALTER TABLE prims ADD ClickAction tinyint NOT NULL default 0;

COMMIT
