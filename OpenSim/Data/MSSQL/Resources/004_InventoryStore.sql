BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_inventoryitems
	(
	inventoryID uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	assetID uniqueidentifier NULL DEFAULT (NULL),
	assetType int NULL DEFAULT (NULL),
	parentFolderID uniqueidentifier NULL DEFAULT (NULL),
	avatarID uniqueidentifier NULL DEFAULT (NULL),
	inventoryName varchar(64) NULL DEFAULT (NULL),
	inventoryDescription varchar(128) NULL DEFAULT (NULL),
	inventoryNextPermissions int NULL DEFAULT (NULL),
	inventoryCurrentPermissions int NULL DEFAULT (NULL),
	invType int NULL DEFAULT (NULL),
	creatorID uniqueidentifier NULL DEFAULT (NULL),
	inventoryBasePermissions int NOT NULL DEFAULT ((0)),
	inventoryEveryOnePermissions int NOT NULL DEFAULT ((0)),
	salePrice int NULL DEFAULT (NULL),
	saleType tinyint NULL DEFAULT (NULL),
	creationDate int NULL DEFAULT (NULL),
	groupID uniqueidentifier NULL DEFAULT (NULL),
	groupOwned bit NULL DEFAULT (NULL),
	flags int NULL DEFAULT (NULL),
	inventoryGroupPermissions int NOT NULL DEFAULT ((0))
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.inventoryitems)
	 EXEC('INSERT INTO dbo.Tmp_inventoryitems (inventoryID, assetID, assetType, parentFolderID, avatarID, inventoryName, inventoryDescription, inventoryNextPermissions, inventoryCurrentPermissions, invType, creatorID, inventoryBasePermissions, inventoryEveryOnePermissions, salePrice, saleType, creationDate, groupID, groupOwned, flags, inventoryGroupPermissions)
		SELECT CONVERT(uniqueidentifier, inventoryID), CONVERT(uniqueidentifier, assetID), assetType, CONVERT(uniqueidentifier, parentFolderID), CONVERT(uniqueidentifier, avatarID), inventoryName, inventoryDescription, inventoryNextPermissions, inventoryCurrentPermissions, invType, CONVERT(uniqueidentifier, creatorID), inventoryBasePermissions, inventoryEveryOnePermissions, salePrice, saleType, creationDate, CONVERT(uniqueidentifier, groupID), groupOwned, flags, inventoryGroupPermissions FROM dbo.inventoryitems WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.inventoryitems

EXECUTE sp_rename N'dbo.Tmp_inventoryitems', N'inventoryitems', 'OBJECT' 

ALTER TABLE dbo.inventoryitems ADD CONSTRAINT
	PK__inventor__C4B7BC2220C1E124 PRIMARY KEY CLUSTERED 
	(
	inventoryID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]


CREATE NONCLUSTERED INDEX owner ON dbo.inventoryitems
	(
	avatarID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX folder ON dbo.inventoryitems
	(
	parentFolderID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
