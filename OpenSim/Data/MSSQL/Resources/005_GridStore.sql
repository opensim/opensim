BEGIN TRANSACTION

ALTER TABLE regions ADD access int default 0;

COMMIT
