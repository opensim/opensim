BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_estate_map
	(
	RegionID uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	EstateID int NOT NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.estate_map)
	 EXEC('INSERT INTO dbo.Tmp_estate_map (RegionID, EstateID)
		SELECT CONVERT(uniqueidentifier, RegionID), EstateID FROM dbo.estate_map WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.estate_map

EXECUTE sp_rename N'dbo.Tmp_estate_map', N'estate_map', 'OBJECT' 

ALTER TABLE dbo.estate_map ADD CONSTRAINT
	PK_estate_map PRIMARY KEY CLUSTERED 
	(
	RegionID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]


COMMIT
