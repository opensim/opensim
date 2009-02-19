BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_avatarattachments
	(
	UUID uniqueidentifier NOT NULL,
	attachpoint int NOT NULL,
	item uniqueidentifier NOT NULL,
	asset uniqueidentifier NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.avatarattachments)
	 EXEC('INSERT INTO dbo.Tmp_avatarattachments (UUID, attachpoint, item, asset)
		SELECT CONVERT(uniqueidentifier, UUID), attachpoint, CONVERT(uniqueidentifier, item), CONVERT(uniqueidentifier, asset) FROM dbo.avatarattachments WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.avatarattachments

EXECUTE sp_rename N'dbo.Tmp_avatarattachments', N'avatarattachments', 'OBJECT' 

CREATE NONCLUSTERED INDEX IX_avatarattachments ON dbo.avatarattachments
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
