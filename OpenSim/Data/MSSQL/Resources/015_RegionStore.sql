BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_primitems
	(
	itemID uniqueidentifier NOT NULL,
	primID uniqueidentifier NULL,
	assetID uniqueidentifier NULL,
	parentFolderID uniqueidentifier NULL,
	invType int NULL,
	assetType int NULL,
	name varchar(255) NULL,
	description varchar(255) NULL,
	creationDate varchar(255) NULL,
	creatorID uniqueidentifier NULL,
	ownerID uniqueidentifier NULL,
	lastOwnerID uniqueidentifier NULL,
	groupID uniqueidentifier NULL,
	nextPermissions int NULL,
	currentPermissions int NULL,
	basePermissions int NULL,
	everyonePermissions int NULL,
	groupPermissions int NULL,
	flags int NOT NULL DEFAULT ((0))
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.primitems)
	 EXEC('INSERT INTO dbo.Tmp_primitems (itemID, primID, assetID, parentFolderID, invType, assetType, name, description, creationDate, creatorID, ownerID, lastOwnerID, groupID, nextPermissions, currentPermissions, basePermissions, everyonePermissions, groupPermissions, flags)
		SELECT CONVERT(uniqueidentifier, itemID), CONVERT(uniqueidentifier, primID), CONVERT(uniqueidentifier, assetID), CONVERT(uniqueidentifier, parentFolderID), invType, assetType, name, description, creationDate, CONVERT(uniqueidentifier, creatorID), CONVERT(uniqueidentifier, ownerID), CONVERT(uniqueidentifier, lastOwnerID), CONVERT(uniqueidentifier, groupID), nextPermissions, currentPermissions, basePermissions, everyonePermissions, groupPermissions, flags FROM dbo.primitems WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.primitems

EXECUTE sp_rename N'dbo.Tmp_primitems', N'primitems', 'OBJECT' 

ALTER TABLE dbo.primitems ADD CONSTRAINT
	PK__primitems__0A688BB1 PRIMARY KEY CLUSTERED 
	(
	itemID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX primitems_primid ON dbo.primitems
	(
	primID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
