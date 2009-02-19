BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_estate_users
	(
	EstateID int NOT NULL,
	uuid uniqueidentifier NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.estate_users)
	 EXEC('INSERT INTO dbo.Tmp_estate_users (EstateID, uuid)
		SELECT EstateID, CONVERT(uniqueidentifier, uuid) FROM dbo.estate_users WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.estate_users

EXECUTE sp_rename N'dbo.Tmp_estate_users', N'estate_users', 'OBJECT' 

CREATE NONCLUSTERED INDEX IX_estate_users ON dbo.estate_users
	(
	EstateID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
