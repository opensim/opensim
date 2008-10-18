begin;

ALTER TABLE land ADD COLUMN OtherCleanTime integer NOT NULL default 0;
ALTER TABLE land ADD COLUMN Dwell integer NOT NULL default 0;

commit;

