BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_estate_managers
	(
	EstateID int NOT NULL,
	uuid uniqueidentifier NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.estate_managers)
	 EXEC('INSERT INTO dbo.Tmp_estate_managers (EstateID, uuid)
		SELECT EstateID, CONVERT(uniqueidentifier, uuid) FROM dbo.estate_managers WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.estate_managers

EXECUTE sp_rename N'dbo.Tmp_estate_managers', N'estate_managers', 'OBJECT' 

CREATE NONCLUSTERED INDEX IX_estate_managers ON dbo.estate_managers
	(
	EstateID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
