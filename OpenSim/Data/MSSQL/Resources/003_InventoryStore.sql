/* To prevent any potential data loss issues, you should review this script in detail before running it outside the context of the database designer.*/
BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_inventoryfolders
	(
	folderID uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	agentID uniqueidentifier NULL DEFAULT (NULL),
	parentFolderID uniqueidentifier NULL DEFAULT (NULL),
	folderName varchar(64) NULL DEFAULT (NULL),
	type smallint NOT NULL DEFAULT ((0)),
	version int NOT NULL DEFAULT ((0))
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.inventoryfolders)
	 EXEC('INSERT INTO dbo.Tmp_inventoryfolders (folderID, agentID, parentFolderID, folderName, type, version)
		SELECT CONVERT(uniqueidentifier, folderID), CONVERT(uniqueidentifier, agentID), CONVERT(uniqueidentifier, parentFolderID), folderName, type, version FROM dbo.inventoryfolders WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.inventoryfolders

EXECUTE sp_rename N'dbo.Tmp_inventoryfolders', N'inventoryfolders', 'OBJECT' 

ALTER TABLE dbo.inventoryfolders ADD CONSTRAINT
	PK__inventor__C2FABFB3173876EA PRIMARY KEY CLUSTERED 
	(
	folderID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX owner ON dbo.inventoryfolders
	(
	agentID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX parent ON dbo.inventoryfolders
	(
	parentFolderID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
