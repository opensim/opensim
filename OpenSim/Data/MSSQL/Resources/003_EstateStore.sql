BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_estateban
	(
	EstateID int NOT NULL,
	bannedUUID varchar(36) NOT NULL,
	bannedIp varchar(16) NULL,
	bannedIpHostMask varchar(16) NULL,
	bannedNameMask varchar(64) NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.estateban)
	 EXEC('INSERT INTO dbo.Tmp_estateban (EstateID, bannedUUID, bannedIp, bannedIpHostMask, bannedNameMask)
		SELECT EstateID, bannedUUID, bannedIp, bannedIpHostMask, bannedNameMask FROM dbo.estateban')

DROP TABLE dbo.estateban

EXECUTE sp_rename N'dbo.Tmp_estateban', N'estateban', 'OBJECT' 

CREATE NONCLUSTERED INDEX IX_estateban ON dbo.estateban
	(
	EstateID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
