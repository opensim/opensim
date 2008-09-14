BEGIN TRANSACTION

CREATE TABLE Tmp_primitems
	(
	itemID varchar(36) NOT NULL,
	primID varchar(36) NULL,
	assetID varchar(36) NULL,
	parentFolderID varchar(36) NULL,
	invType int NULL,
	assetType int NULL,
	name varchar(255) NULL,
	description varchar(255) NULL,
	creationDate varchar(255) NULL,
	creatorID varchar(36) NULL,
	ownerID varchar(36) NULL,
	lastOwnerID varchar(36) NULL,
	groupID varchar(36) NULL,
	nextPermissions int NULL,
	currentPermissions int NULL,
	basePermissions int NULL,
	everyonePermissions int NULL,
	groupPermissions int NULL
	)  ON [PRIMARY]

IF EXISTS(SELECT * FROM primitems)
	 EXEC('INSERT INTO Tmp_primitems (itemID, primID, assetID, parentFolderID, invType, assetType, name, description, creationDate, creatorID, ownerID, lastOwnerID, groupID, nextPermissions, currentPermissions, basePermissions, everyonePermissions, groupPermissions)
		SELECT CONVERT(varchar(36), itemID), CONVERT(varchar(36), primID), CONVERT(varchar(36), assetID), CONVERT(varchar(36), parentFolderID), invType, assetType, name, description, creationDate, CONVERT(varchar(36), creatorID), CONVERT(varchar(36), ownerID), CONVERT(varchar(36), lastOwnerID), CONVERT(varchar(36), groupID), nextPermissions, currentPermissions, basePermissions, everyonePermissions, groupPermissions')

DROP TABLE primitems

EXECUTE sp_rename N'Tmp_primitems', N'primitems', 'OBJECT' 

ALTER TABLE primitems ADD CONSTRAINT
	PK__primitems__0A688BB1 PRIMARY KEY CLUSTERED 
	(
	itemID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]


COMMIT
