BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_landaccesslist
	(
	LandUUID uniqueidentifier NULL,
	AccessUUID uniqueidentifier NULL,
	Flags int NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.landaccesslist)
	 EXEC('INSERT INTO dbo.Tmp_landaccesslist (LandUUID, AccessUUID, Flags)
		SELECT CONVERT(uniqueidentifier, LandUUID), CONVERT(uniqueidentifier, AccessUUID), Flags FROM dbo.landaccesslist WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.landaccesslist

EXECUTE sp_rename N'dbo.Tmp_landaccesslist', N'landaccesslist', 'OBJECT' 

COMMIT
