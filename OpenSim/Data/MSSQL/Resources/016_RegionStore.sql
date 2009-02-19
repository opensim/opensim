BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_terrain
	(
	RegionUUID uniqueidentifier NULL,
	Revision int NULL,
	Heightfield image NULL
	)  ON [PRIMARY]
	 TEXTIMAGE_ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.terrain)
	 EXEC('INSERT INTO dbo.Tmp_terrain (RegionUUID, Revision, Heightfield)
		SELECT CONVERT(uniqueidentifier, RegionUUID), Revision, Heightfield FROM dbo.terrain WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.terrain

EXECUTE sp_rename N'dbo.Tmp_terrain', N'terrain', 'OBJECT' 

COMMIT
