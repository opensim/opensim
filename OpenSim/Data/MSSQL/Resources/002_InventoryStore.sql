BEGIN TRANSACTION

ALTER TABLE inventoryitems ADD inventoryGroupPermissions INTEGER NOT NULL default 0

COMMIT