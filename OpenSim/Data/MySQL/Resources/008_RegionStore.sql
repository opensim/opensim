BEGIN;

ALTER TABLE primshapes change UUID UUIDold varchar(255);
ALTER TABLE primshapes add UUID char(36);
UPDATE primshapes set UUID = UUIDold;
ALTER TABLE primshapes drop UUIDold;
ALTER TABLE primshapes add constraint primary key(UUID);

COMMIT;