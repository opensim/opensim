BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_estate_groups
	(
	EstateID int NOT NULL,
	uuid uniqueidentifier NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.estate_groups)
	 EXEC('INSERT INTO dbo.Tmp_estate_groups (EstateID, uuid)
		SELECT EstateID, CONVERT(uniqueidentifier, uuid) FROM dbo.estate_groups WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.estate_groups

EXECUTE sp_rename N'dbo.Tmp_estate_groups', N'estate_groups', 'OBJECT' 

CREATE NONCLUSTERED INDEX IX_estate_groups ON dbo.estate_groups
	(
	EstateID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
