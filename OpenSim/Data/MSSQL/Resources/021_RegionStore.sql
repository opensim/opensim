BEGIN TRANSACTION

ALTER TABLE prims ADD PassTouches bit not null default 0

COMMIT
