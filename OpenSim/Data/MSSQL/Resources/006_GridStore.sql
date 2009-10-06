BEGIN TRANSACTION

ALTER TABLE regions ADD scopeid uniqueidentifier default '00000000-0000-0000-0000-000000000000';
ALTER TABLE regions ADD DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [owner_uuid];
ALTER TABLE regions ADD sizeX integer not null default 0;
ALTER TABLE regions ADD sizeY integer not null default 0;

COMMIT
