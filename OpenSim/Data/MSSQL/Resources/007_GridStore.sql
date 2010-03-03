BEGIN TRANSACTION

ALTER TABLE regions ADD [flags] integer NOT NULL DEFAULT 0;
CREATE INDEX [flags] ON regions(flags);
ALTER TABLE [regions] ADD [last_seen] integer NOT NULL DEFAULT 0;
ALTER TABLE [regions] ADD [PrincipalID] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE [regions] ADD [Token] varchar(255) NOT NULL DEFAULT 0;

COMMIT
