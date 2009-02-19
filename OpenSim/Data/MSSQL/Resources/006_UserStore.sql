BEGIN TRANSACTION

CREATE TABLE dbo.Tmp_users
	(
	UUID uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	username varchar(32) NOT NULL,
	lastname varchar(32) NOT NULL,
	passwordHash varchar(32) NOT NULL,
	passwordSalt varchar(32) NOT NULL,
	homeRegion bigint NULL DEFAULT (NULL),
	homeLocationX float(53) NULL DEFAULT (NULL),
	homeLocationY float(53) NULL DEFAULT (NULL),
	homeLocationZ float(53) NULL DEFAULT (NULL),
	homeLookAtX float(53) NULL DEFAULT (NULL),
	homeLookAtY float(53) NULL DEFAULT (NULL),
	homeLookAtZ float(53) NULL DEFAULT (NULL),
	created int NOT NULL,
	lastLogin int NOT NULL,
	userInventoryURI varchar(255) NULL DEFAULT (NULL),
	userAssetURI varchar(255) NULL DEFAULT (NULL),
	profileCanDoMask int NULL DEFAULT (NULL),
	profileWantDoMask int NULL DEFAULT (NULL),
	profileAboutText ntext NULL,
	profileFirstText ntext NULL,
	profileImage uniqueidentifier NULL DEFAULT (NULL),
	profileFirstImage uniqueidentifier NULL DEFAULT (NULL),
	webLoginKey uniqueidentifier NULL DEFAULT (NULL),
	homeRegionID uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	userFlags int NOT NULL DEFAULT ((0)),
	godLevel int NOT NULL DEFAULT ((0)),
	customType varchar(32) NOT NULL DEFAULT (''),
	partner uniqueidentifier NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'),
	email varchar(250) NULL
	)  ON [PRIMARY]
	 TEXTIMAGE_ON [PRIMARY]

IF EXISTS(SELECT * FROM dbo.users)
	 EXEC('INSERT INTO dbo.Tmp_users (UUID, username, lastname, passwordHash, passwordSalt, homeRegion, homeLocationX, homeLocationY, homeLocationZ, homeLookAtX, homeLookAtY, homeLookAtZ, created, lastLogin, userInventoryURI, userAssetURI, profileCanDoMask, profileWantDoMask, profileAboutText, profileFirstText, profileImage, profileFirstImage, webLoginKey, homeRegionID, userFlags, godLevel, customType, partner, email)
		SELECT CONVERT(uniqueidentifier, UUID), username, lastname, passwordHash, passwordSalt, homeRegion, homeLocationX, homeLocationY, homeLocationZ, homeLookAtX, homeLookAtY, homeLookAtZ, created, lastLogin, userInventoryURI, userAssetURI, profileCanDoMask, profileWantDoMask, profileAboutText, profileFirstText, CONVERT(uniqueidentifier, profileImage), CONVERT(uniqueidentifier, profileFirstImage), CONVERT(uniqueidentifier, webLoginKey), CONVERT(uniqueidentifier, homeRegionID), userFlags, godLevel, customType, CONVERT(uniqueidentifier, partner), email FROM dbo.users WITH (HOLDLOCK TABLOCKX)')

DROP TABLE dbo.users

EXECUTE sp_rename N'dbo.Tmp_users', N'users', 'OBJECT' 

ALTER TABLE dbo.users ADD CONSTRAINT
	PK__users__65A475E737A5467C PRIMARY KEY CLUSTERED 
	(
	UUID
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

CREATE NONCLUSTERED INDEX usernames ON dbo.users
	(
	username,
	lastname
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

COMMIT
