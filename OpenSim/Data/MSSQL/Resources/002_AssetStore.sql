BEGIN TRANSACTION

CREATE TABLE Tmp_assets
	(
	id varchar(36) NOT NULL,
	name varchar(64) NOT NULL,
	description varchar(64) NOT NULL,
	assetType tinyint NOT NULL,
	local bit NOT NULL,
	temporary bit NOT NULL,
	data image NOT NULL
	)  ON [PRIMARY]
	 TEXTIMAGE_ON [PRIMARY]

IF EXISTS(SELECT * FROM assets)
	 EXEC('INSERT INTO Tmp_assets (id, name, description, assetType, local, temporary, data)
		SELECT id, name, description, assetType, CONVERT(bit, local), CONVERT(bit, temporary), data FROM assets WITH (HOLDLOCK TABLOCKX)')

DROP TABLE assets

EXECUTE sp_rename N'Tmp_assets', N'assets', 'OBJECT' 

ALTER TABLE dbo.assets ADD CONSTRAINT
	PK__assets__id PRIMARY KEY CLUSTERED 
	(
	id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
