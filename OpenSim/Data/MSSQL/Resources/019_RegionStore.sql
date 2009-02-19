BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_regionban
	(
	regionUUID uniqueidentifier NOT NULL,
	bannedUUID uniqueidentifier NOT NULL,
	bannedIp varchar(16) NOT NULL,
	bannedIpHostMask varchar(16) NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.regionban)
	 EXEC('INSERT INTO dbo.Tmp_regionban (regionUUID, bannedUUID, bannedIp, bannedIpHostMask)
		SELECT CONVERT(uniqueidentifier, regionUUID), CONVERT(uniqueidentifier, bannedUUID), bannedIp, bannedIpHostMask FROM dbo.regionban WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.regionban

EXECUTE sp_rename N'dbo.Tmp_regionban', N'regionban', 'OBJECT' 

COMMIT
