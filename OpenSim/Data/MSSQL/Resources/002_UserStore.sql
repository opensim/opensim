BEGIN TRANSACTION

ALTER TABLE users ADD homeRegionID varchar(36) NOT NULL default '00000000-0000-0000-0000-000000000000';
ALTER TABLE users ADD userFlags int NOT NULL default 0;
ALTER TABLE users ADD godLevel int NOT NULL default 0;
ALTER TABLE users ADD customType varchar(32) not null default '';
ALTER TABLE users ADD partner varchar(36) not null default '00000000-0000-0000-0000-000000000000';

COMMIT
