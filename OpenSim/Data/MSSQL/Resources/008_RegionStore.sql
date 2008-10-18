BEGIN TRANSACTION

ALTER TABLE land ADD OtherCleanTime integer NOT NULL default 0;
ALTER TABLE land ADD Dwell integer NOT NULL default 0;

COMMIT

