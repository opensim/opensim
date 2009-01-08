BEGIN TRANSACTION

ALTER TABLE prims ADD LinkNumber integer not null default 0

COMMIT
